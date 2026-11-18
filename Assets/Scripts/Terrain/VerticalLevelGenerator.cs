using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Builds the climb as seeded chunks of rows (see VerticalLayout); only terrain and enemies near the player exist as GameObjects.
// Streams chunks in above and out below as the player moves, tracks falls and checkpoints, places the boss arenas, and keeps the
// current run (seed, checkpoint, upgrades) so Retry and Continue resume it. Other scripts read it through Active.
[DefaultExecutionOrder(-100)]
public sealed class VerticalLevelGenerator : MonoBehaviour
{
    [SerializeField] private VerticalGenerationSettings settings;
    [SerializeField] private PlayerController player;
    [SerializeField] private int currentSeed;
    public UnityEvent<string> onHeightReached = new UnityEvent<string>();

    // The current run, kept in a static so it survives scene reloads (Retry) until BeginNewRun.
    private sealed class Run
    {
        public VerticalGenerationSettings Settings;
        public string Scene;
        public int Seed, Checkpoint;   // Checkpoint is the highest checkpoint row reached
        public bool BossDefeated, FinalBossDefeated;
        // Player state at the last checkpoint/purchase (AJ-75). Restored on Retry and Continue.
        public bool HasSnapshot;
        public int[] Purchases = new int[3], Amounts = new int[3];
        public int Coins = -1;
    }

    private static Run run;
    private static AscendJigoku.Gameplay.RunSaveData pendingContinue;
    public static VerticalLevelGenerator Active { get; private set; }

    // A vertical slice of RowsPerChunk rows. Rows NextRow..EndRow-1 are still waiting to be built, a few per frame.
    private sealed class Chunk
    {
        public GameObject Root;
        public int NextRow, EndRow, Built;
    }

    // Remembers an enemy's health per row so it is unchanged when its chunk unloads and reloads. Actor is null while unloaded.
    private sealed class EnemyRecord
    {
        public int Row, Health;
        public EnemyHealth Actor;
    }

    private readonly Dictionary<int, Chunk> chunks = new Dictionary<int, Chunk>();
    private readonly Dictionary<int, EnemyRecord> enemyRecords = new Dictionary<int, EnemyRecord>();
    private readonly HashSet<EnemyRecord> activeEnemies = new HashSet<EnemyRecord>();
    private readonly HashSet<int> reachedMarkers = new HashSet<int>();
    private readonly List<int> removal = new List<int>();
    private readonly List<Chunk> buildOrder = new List<Chunk>();
    private readonly List<EnemyRecord> enemyRemoval = new List<EnemyRecord>();
    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    // Landings are cheap to recompute but are looked up constantly, so they are cached by row.
    private readonly Dictionary<int, VerticalLayout.Landing> landingCache = new Dictionary<int, VerticalLayout.Landing>();
    private readonly List<Vector3> tops = new List<Vector3>(), bottoms = new List<Vector3>();
    private Transform generatedRoot;
    private Rigidbody2D body;
    private float fallGravity; // normal gravity, so a slowed air-dash or wall slide never counts as a hard fall
    private Collider2D playerShape;
    private PlayerHealth health;
    private PlayerRespawn respawn;
    private PlayerCombat combat;
    private PlayerParry parry;
    private VerticalLayout.Metrics metrics;
    private float nextRefresh, fallPeak, footOffset, nextFallDamageAt;   // fallPeak: highest foot y since the last safe landing
    private int lastSafeRow;
    private Vector3 safePosition;   // where a respawn or bounce puts the player: the last landing they stood on
    private int bossRow = -1, bossCheckpointRow = -1;
    private BossFightGate bossGate;
    private int finalBossRow = -1;
    private GrimDeathEncounter finalBossGate;
    private bool checkpointResting;
    private float nextSanctuarySweep;
    // The row whose checkpoint finishes the run: the one after the final boss, or the summit when there is no final boss.
    public int CompletionCheckpointRow => finalBossRow >= 0
        ? VerticalLayout.MarkerRow(settings.heightMarkers[settings.finalBossEncounter.checkpointMarkerIndex], metrics)
        : metrics.LastRow;
    public bool CompletionReady => run != null && FinalBossDefeated && (!BossRouteLocked) && run.Checkpoint >= CompletionCheckpointRow;
    public BossFightGate BossGate => bossGate;
    public GrimDeathEncounter FinalBossGate => finalBossGate;
    public int FinalBossRow => finalBossRow;
    public bool FinalBossDefeated => run != null && run.FinalBossDefeated;
    public bool FinalBossRouteLocked => finalBossRow >= 0 && !FinalBossDefeated;
    // Highest row the player may reach right now: an undefeated boss blocks everything above its floor.
    public int RouteLimitRow => BossRouteLocked ? bossRow : FinalBossRouteLocked ? finalBossRow : CompletionCheckpointRow;
    public int BossRow => bossRow;
    public bool BossDefeated => run != null && run.BossDefeated;
    public bool BossRouteLocked => bossRow >= 0 && !BossDefeated;
    public int CurrentSeed => currentSeed;
    public int LoadedChunkCount => chunks.Count;
    public int ActiveEnemyCount => activeEnemies.Count;
    public int CheckpointRow => run != null ? run.Checkpoint : 0;
    public int LastSafeRow => lastSafeRow;
    public bool IsFallBouncing { get; private set; }
    // Fired once per landing as it is built (and again if its chunk is rebuilt). Cosmetic systems dress the landing here.
    // Spawn anything under ChunkRoot: it is destroyed when the chunk unloads. Never parent to Platform (it is scaled).
    public struct BuiltPlatform
    {
        public Transform ChunkRoot;
        public GameObject Platform;
        public VerticalLayout.Landing Landing;
        public Vector3 WorldTop;      // centre of the walkable top edge, world space
        public float ZoneHeight;      // landing height above the pillar base (for PillarEnvironment.ZoneAt)
        public int Seed;              // deterministic per landing and run
        public bool IsBossFloor, IsCheckpoint, IsSummit;
        public Vector3[] Tops;        // every walkable top of the landing: world x, world y, half width (the main one first)
        public Vector3[] Bottoms;     // open undersides props can hang from, same layout
    }
    public static event Action<BuiltPlatform> PlatformBuilt;
    public VerticalGenerationSettings Settings => settings;
    public PlayerController Player => player;
    public float LoadAboveDistance { get; private set; }
    public float KeepBelowDistance { get; private set; }
    public VerticalLayout.Metrics Metrics => metrics;
    public int ActivePlatformCount
    {
        get
        {
            int count = 0;
            foreach (var c in chunks.Values) count += c.Built;
            return count;
        }
    }

    public int PendingPlatformCount
    {
        get
        {
            int count = 0;
            foreach (var c in chunks.Values) count += c.EndRow - c.NextRow;
            return count;
        }
    }

    public bool IsChunkLoaded(int index) => chunks.ContainsKey(index);
    public bool TryGetChunkProgress(int index, out int built, out int total)
    {
        built = total = 0;
        if (!chunks.TryGetValue(index, out var chunk)) return false;
        built = chunk.Built;
        total = chunk.EndRow - index * metrics.RowsPerChunk;
        return true;
    }

    // The landing for a row, from the pure VerticalLayout plus this generator's overrides: boss rows become a full-width slab,
    // and rows within 2 of the first boss get no enemy.
    public VerticalLayout.Landing GetLanding(int row)
    {
        if (landingCache.TryGetValue(row, out var landing)) return landing;
        int nearestBoss = finalBossRow >= 0 && (bossRow < 0 || Mathf.Abs(row - finalBossRow) < Mathf.Abs(row - bossRow))
            ? finalBossRow
            : bossRow;
        landing = VerticalLayout.GetLanding(settings, currentSeed, row, metrics, nearestBoss);
        if (row == bossRow || row == finalBossRow)
        {
            landing.Center.x = 0;
            landing.Width = settings.pillarWidth;
            landing.Thickness = 1.2f;
            landing.EnemyIndex = -1;
            landing.MarkerIndex = -1;
            landing.Color = row == finalBossRow ? new Color(.28f, .23f, .34f) : new Color(0.35f, 0.35f, 0.39f);
            VerticalLayout.Slab(ref landing);
        }
        else if (!settings.enemyOnEveryPlatform && bossRow >= 0 && Mathf.Abs(row - bossRow) <= 2) landing.EnemyIndex = -1;
        landingCache[row] = landing;
        return landing;
    }

    public static void BeginNewRun()
    {
        run = null;
        pendingContinue = null;
    }

    // Main menu CONTINUE: the next Generate() in the saved scene resumes this run.
    public static void ContinueRun(AscendJigoku.Gameplay.RunSaveData save)
    {
        run = null;
        pendingContinue = save;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRun()
    {
        BeginNewRun();
        Active = null;
    }

    private void Start() => Generate();

    // (Re)builds the whole level state: resolves which run this is (new, retry or continue), finds the boss rows,
    // measures jump limits, builds the starting chunk at the last checkpoint and puts the player there. Returns false if misconfigured.
    public bool Generate()
    {
        IsFallBouncing = false;
        nextFallDamageAt = 0;
        if (settings == null || settings.platformPrefab == null || player == null)
        {
            Debug.LogError("Generator needs settings, platform prefab and player.", this);
            return false;
        }

        body = player.GetComponent<Rigidbody2D>();
        playerShape = player.GetComponent<Collider2D>();
        fallGravity = Mathf.Max(1, Mathf.Abs(Physics2D.gravity.y * body.gravityScale));
        health = player.GetComponent<PlayerHealth>();
        respawn = player.GetComponent<PlayerRespawn>();
        if (settings.checkpointShopPrefab != null)
        {
            var wallet = player.GetComponent<AscendJigoku.Gameplay.CurrencyWallet>();
            if (wallet == null) wallet = player.gameObject.AddComponent<AscendJigoku.Gameplay.PrototypeWallet>();
            var shop = player.GetComponent<AscendJigoku.Gameplay.ShopMenu>();
            if (shop == null) shop = player.gameObject.AddComponent<AscendJigoku.Gameplay.ShopMenu>();
            shop.wallet = wallet;
        }
        combat = player.GetComponent<PlayerCombat>();
        parry = player.GetComponent<PlayerParry>();
        if (body == null || playerShape == null || health == null || respawn == null || body.gravityScale <= 0 || Physics2D.gravity.y >= 0)
        {
            Debug.LogError("Generator needs player health, respawn, collider and downward gravity.", this);
            return false;
        }

        metrics = VerticalLayout.Measure(settings, player.JumpSpeed, player.MoveSpeed,
            Mathf.Abs(Physics2D.gravity.y * body.gravityScale), player.MaxJumps);
        Active = this;
        if (pendingContinue != null)
        {
            var save = pendingContinue;
            pendingContinue = null;
            if (save.scene == gameObject.scene.path) run = RunFromSave(save);
            else Debug.LogWarning("Saved run belongs to " + save.scene + "; starting a new run here.", this);
        }
        // Start a new run unless a matching one (same settings, scene and seed) is already in progress, e.g. after Retry.
        bool freshRun = false;
        if (run == null || run.Settings != settings || run.Scene != gameObject.scene.path || (!settings.randomizeSeed && run.Seed != settings.seed))
        {
            run = new Run
            {
                Settings = settings, Scene = gameObject.scene.path,
                Seed = settings.randomizeSeed ? System.Guid.NewGuid().GetHashCode() : settings.seed
            };
            freshRun = true;
        }
        currentSeed = run.Seed;
        bossRow = bossCheckpointRow = finalBossRow = -1;
        bossGate = null;
        finalBossGate = null;
        landingCache.Clear();
        // Boss floors sit rowsBeforeCheckpoint rows below the checkpoint marker they guard (at least 3 below).
        var encounter = settings.bossEncounter;
        if (encounter != null && encounter.bossPrefab != null && encounter.checkpointMarkerIndex >= 0
            && settings.heightMarkers != null && encounter.checkpointMarkerIndex < settings.heightMarkers.Length)
        {
            bossCheckpointRow = VerticalLayout.MarkerRow(settings.heightMarkers[encounter.checkpointMarkerIndex], metrics);
            bossRow = Mathf.Max(2, bossCheckpointRow - Mathf.Max(3, encounter.rowsBeforeCheckpoint));
        }
        var finalEncounter = settings.finalBossEncounter;
        if (finalEncounter != null && finalEncounter.bossPrefab != null && settings.heightMarkers != null
            && finalEncounter.checkpointMarkerIndex >= 0 && finalEncounter.checkpointMarkerIndex < settings.heightMarkers.Length)
        {
            int checkpoint = VerticalLayout.MarkerRow(settings.heightMarkers[finalEncounter.checkpointMarkerIndex], metrics);
            int row = checkpoint - Mathf.Max(3, finalEncounter.rowsBeforeCheckpoint);
            if (row > Mathf.Max(2, bossCheckpointRow + 3)) finalBossRow = row;
        }
        // A save made before this boss was added must enter from the previous checkpoint.
        if (FinalBossRouteLocked && run.Checkpoint > finalBossRow)
            run.Checkpoint = Mathf.Max(0, bossCheckpointRow);
        if (FinalBossDefeated && run.Checkpoint > CompletionCheckpointRow) run.Checkpoint = CompletionCheckpointRow;
        if (generatedRoot != null)
        {
            generatedRoot.gameObject.SetActive(false);
            Destroy(generatedRoot.gameObject);
        }

        chunks.Clear();
        enemyRecords.Clear();
        activeEnemies.Clear();
        reachedMarkers.Clear();
        generatedRoot = new GameObject("Streamed pillar - seed " + currentSeed).transform;
        generatedRoot.SetParent(transform, false);
        footOffset = player.transform.position.y - playerShape.bounds.min.y;
        lastSafeRow = Mathf.Clamp(run.Checkpoint, 0, metrics.LastRow);
        run.Checkpoint = lastSafeRow;
        safePosition = StandPosition(GetLanding(lastSafeRow), GetLanding(lastSafeRow).Center.x);
        EnsureRowReady(lastSafeRow);
        respawn.BeforeRespawn -= PrepareRespawn;
        respawn.BeforeRespawn += PrepareRespawn;
        respawn.SetRespawnPoint(safePosition);
        if (lastSafeRow > 0)
        {
            reachedMarkers.Add(lastSafeRow);
            respawn.Respawn();
        }

        fallPeak = playerShape.bounds.min.y;
        ApplySnapshot();
        if (freshRun) SaveRun(); // so CONTINUE exists even before the first checkpoint
        RefreshStreaming();
        return true;
    }

    // ---------- Saved runs (AJ-75) ----------

    private Run RunFromSave(AscendJigoku.Gameplay.RunSaveData save)
    {
        if (!string.IsNullOrEmpty(save.settings) && save.settings != settings.name)
            Debug.LogWarning($"Saved run used generation settings '{save.settings}', now '{settings.name}'. Layout may differ.", this);
        return new Run
        {
            Settings = settings, Scene = save.scene, Seed = save.seed, Checkpoint = Mathf.Max(0, save.checkpointRow),
            BossDefeated = save.bossDefeated, FinalBossDefeated = save.finalBossDefeated,
            HasSnapshot = save.hasSnapshot, Purchases = AscendJigoku.Gameplay.RunSave.Fit(save.purchases),
            Amounts = AscendJigoku.Gameplay.RunSave.Fit(save.upgradeAmounts), Coins = save.coins
        };
    }

    private void CaptureSnapshot()
    {
        if (run == null || player == null) return;
        var upgrades = player.GetComponent<AscendJigoku.Gameplay.PlayerUpgrades>();
        var wallet = player.GetComponent<AscendJigoku.Gameplay.CurrencyWallet>();
        if (upgrades != null) upgrades.CopyState(run.Purchases, run.Amounts);
        run.Coins = wallet != null ? wallet.Balance : -1;
        run.HasSnapshot = true;
    }

    private void ApplySnapshot()
    {
        if (run == null || !run.HasSnapshot || player == null) return;
        var upgrades = player.GetComponent<AscendJigoku.Gameplay.PlayerUpgrades>();
        bool firstRestore = upgrades == null || !upgrades.RunStateRestored;
        if (upgrades != null) upgrades.RestoreState(run.Purchases, run.Amounts);
        var wallet = player.GetComponent<AscendJigoku.Gameplay.CurrencyWallet>();
        if (firstRestore && wallet != null && run.Coins >= 0) wallet.TrySetBalance(run.Coins);
    }

    private void SaveRun()
    {
        if (run == null || settings == null) return;
        AscendJigoku.Gameplay.RunSave.Write(new AscendJigoku.Gameplay.RunSaveData
        {
            scene = run.Scene, settings = settings.name, seed = run.Seed, checkpointRow = run.Checkpoint,
            checkpointHeight = GetLanding(run.Checkpoint).Center.y, bossDefeated = run.BossDefeated,
            finalBossDefeated = run.FinalBossDefeated, hasSnapshot = run.HasSnapshot,
            purchases = (int[])run.Purchases.Clone(), upgradeAmounts = (int[])run.Amounts.Clone(), coins = run.Coins
        });
    }

    // Shops stand on checkpoints, so a purchase counts as checkpoint progress.
    public void SaveAfterPurchase()
    {
        CaptureSnapshot();
        SaveRun();
    }

    // ---------- Debug helpers (F1 actions; see DebugMenuGameBindings) ----------

    public int PlayerRow => playerShape == null ? 0
        : Mathf.Clamp(Mathf.RoundToInt((playerShape.bounds.min.y - transform.position.y) / metrics.RowHeight), 0, metrics.LastRow);

    // direction +1 = next checkpoint above the player, -1 = previous one below. Returns a message for the log.
    public string DebugTeleportToCheckpoint(int direction)
    {
        if (generatedRoot == null || settings.heightMarkers == null) return "No generated pillar.";
        int current = PlayerRow, target = direction > 0 ? metrics.LastRow : 0;
        foreach (var marker in settings.heightMarkers)
        {
            if (marker == null || !marker.checkpoint) continue;
            int row = VerticalLayout.MarkerRow(marker, metrics);
            if (direction > 0 && row > current && row < target) target = row;
            if (direction < 0 && row < current && row > target) target = row;
        }
        return DebugTeleportToRow(target);
    }

    public string DebugTeleportRows(int rows) => DebugTeleportToRow(PlayerRow + rows);

    public string DebugTeleportToRow(int row)
    {
        if (generatedRoot == null || player == null) return "No generated pillar.";
        row = Mathf.Clamp(row, 0, metrics.LastRow);
        string note = "";
        if (row > RouteLimitRow) { row = RouteLimitRow; note = " The boss blocks the route: use Defeat boss to continue."; }
        var landing = GetLanding(row);
        Vector3 destination = StandPosition(landing, landing.Center.x);
        PrepareRespawn(destination); // builds the row and resets fall tracking so the jump is not fall damage
        combat?.Cancel();
        parry?.Cancel();
        body.linearVelocity = Vector2.zero;
        body.position = destination;
        player.transform.position = destination;
        Physics2D.SyncTransforms();
        RefreshStreaming();
        return $"Teleported to row {row} (height {landing.Center.y:F0}).{note}";
    }

    public string DebugDefeatBoss()
    {
        if (!BossRouteLocked && FinalBossRouteLocked)
        {
            if (finalBossGate != null && finalBossGate.Boss != null)
            {
                var hp = finalBossGate.Boss.Health;
                hp.DamageEnabled = true;
                // The boss caps normal attacks at one damage each. Debug defeat deliberately applies all hits.
                int hits = hp.CurrentHealth;
                for (int i = 0; i < hits; i++) hp.TakeDamage(1);
                return "Grim Death defeated; wait for the death animation to open the route.";
            }
            run.FinalBossDefeated = true;
            SaveRun();
            return "Grim Death route opened.";
        }
        if (bossRow < 0) return "This pillar has no boss.";
        if (BossDefeated) return "Boss already defeated.";
        if (bossGate != null && bossGate.Boss != null)
        {
            var bossHealth = bossGate.Boss.GetComponent<EnemyHealth>();
            if (bossHealth != null)
            {
                bossHealth.DamageEnabled = true;
                bossHealth.TakeDamage(bossHealth.MaxHealth * 10);
            }
        }

        if (!BossDefeated)
        {
            // boss not loaded yet: just open the route
            run.BossDefeated = true;
            SaveRun();
        }

        return "Boss defeated; the route above is open.";
    }

    // World position where the player's feet rest on the landing at local x, kept at least a body width from the edges.
    private Vector3 StandPosition(VerticalLayout.Landing landing, float x)
    {
        float margin = playerShape != null ? playerShape.bounds.extents.x + 0.2f : 0.7f;
        float half = Mathf.Max(0, landing.Width * 0.5f - margin);
        return transform.position
            + new Vector3(Mathf.Clamp(x, landing.Center.x - half, landing.Center.x + half), landing.Center.y + footOffset + 0.08f, 0);
    }

    // Gets the chunk with this index, creating an empty one (rows not yet built) if needed.
    private Chunk GetChunk(int index)
    {
        if (chunks.TryGetValue(index, out var chunk)) return chunk;
        var root = new GameObject("Chunk " + index);
        root.transform.SetParent(generatedRoot, false);
        chunk = new Chunk
        {
            Root = root, NextRow = index * metrics.RowsPerChunk,
            EndRow = Mathf.Min((index + 1) * metrics.RowsPerChunk, metrics.LastRow + 1)
        };
        chunks.Add(index, chunk);
        return chunk;
    }

    // Builds the whole chunk containing this row right now (not spread over frames), so the player never lands on nothing.
    private void EnsureRowReady(int row)
    {
        var chunk = GetChunk(Mathf.Clamp(row, 0, metrics.LastRow) / metrics.RowsPerChunk);
        while (chunk.NextRow < chunk.EndRow)
            if (!BuildNext(chunk)) break;
    }

    // Respawn hook: make sure the destination exists and reset fall tracking so the respawn is not mistaken for a fall.
    private void PrepareRespawn(Vector3 destination)
    {
        IsFallBouncing = false;
        playerShape.enabled = true; // undo a fatal fall if the player is revived in place
        var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow != null) follow.Hold = false;
        nextFallDamageAt = Time.time + 0.5f;
        EnsureRowReady(Mathf.RoundToInt((destination.y - transform.position.y - footOffset) / metrics.RowHeight));
        fallPeak = destination.y - footOffset;
        lastSafeRow = Mathf.Clamp(Mathf.RoundToInt((destination.y - transform.position.y - footOffset) / metrics.RowHeight), 0, metrics.LastRow);
        safePosition = destination;
    }

    // Decides which chunks should exist around the player (loadAbove up, keepBelow down), queues them for BuildFrame,
    // unloads the rest (saving enemy health first), and sets LoadAboveDistance / KeepBelowDistance for the debug overlay.
    public void RefreshStreaming()
    {
        if (AscendJigoku.Gameplay.VictoryFlow.IsPlaying || generatedRoot == null) return;
        float height = Mathf.Clamp(player.transform.position.y - transform.position.y, 0, metrics.LastRow * metrics.RowHeight);
        float view = Camera.main != null && Camera.main.orthographic ? Camera.main.orthographicSize + 5 : 12;
        float above = Mathf.Max(view, settings.loadAbove);
        float below = Mathf.Max(view, settings.keepBelow, settings.fallDistance + metrics.RowHeight + 2);
        LoadAboveDistance = above;
        KeepBelowDistance = below;
        int lastChunk = metrics.LastRow / metrics.RowsPerChunk;
        int first = Mathf.Clamp(Mathf.FloorToInt((height - below) / metrics.ChunkHeight), 0, lastChunk);
        int last = Mathf.Clamp(Mathf.FloorToInt((height + above) / metrics.ChunkHeight), 0, lastChunk);
        int center = Mathf.Clamp(Mathf.FloorToInt(height / metrics.ChunkHeight), first, last);
        // Build order: the player's chunk first, then upward, then downward.
        buildOrder.Clear();
        buildOrder.Add(GetChunk(center));
        for (int i = center + 1; i <= last; i++)
            buildOrder.Add(GetChunk(i));

        for (int i = center - 1; i >= first; i--)
            buildOrder.Add(GetChunk(i));

        removal.Clear();
        foreach (var pair in chunks)
        {
            // Keep the unbeaten boss and its floor together; streaming must not reset its HP or drop it.
            if (BossRouteLocked && pair.Key == bossRow / metrics.RowsPerChunk) continue;
            if (FinalBossRouteLocked && pair.Key == finalBossRow / metrics.RowsPerChunk) continue;
            float low = pair.Key * metrics.ChunkHeight, high = low + metrics.ChunkHeight;
            if (high < height - below - settings.unloadBuffer || low > height + above + settings.unloadBuffer)
                removal.Add(pair.Key);
        }
        foreach (int index in removal)
        {
            chunks[index].Root.SetActive(false);
            Destroy(chunks[index].Root);
            chunks.Remove(index);
        }

        enemyRemoval.Clear();
        foreach (var enemy in activeEnemies)
        {
            if (enemy.Actor == null)
            {
                enemyRemoval.Add(enemy);
                continue;
            }

            float y = enemy.Actor.transform.position.y - transform.position.y;
            bool homeLoaded = chunks.ContainsKey(enemy.Row / metrics.RowsPerChunk);
            if (!homeLoaded && (y < height - below - settings.unloadBuffer || y > height + above + settings.unloadBuffer))
            {
                enemy.Health = enemy.Actor.CurrentHealth;
                enemy.Actor.gameObject.SetActive(false);
                Destroy(enemy.Actor.gameObject);
                enemy.Actor = null;
                enemyRemoval.Add(enemy);
            }
        }
        foreach (var enemy in enemyRemoval)
            activeEnemies.Remove(enemy);
    }

    // Builds up to platformsPerFrame queued landings, in the order RefreshStreaming set, to avoid frame spikes.
    public void BuildFrame()
    {
        if (AscendJigoku.Gameplay.VictoryFlow.IsPlaying) return;
        int budget = Mathf.Clamp(settings.platformsPerFrame, 1, 12);
        foreach (var chunk in buildOrder)
            while (chunk.Root != null && chunk.NextRow < chunk.EndRow && budget > 0)
            {
                if (!BuildNext(chunk)) break;
                budget--;
            }
    }

    // Builds the chunk's next landing: the platform pieces and their colours, boss rooms, height markers and shops,
    // the pixel-art skin, the PlatformBuilt event for dressing, and its enemy. Returns false once past the route limit.
    private bool BuildNext(Chunk chunk)
    {
        if (chunk.NextRow > RouteLimitRow) return false;
        var landing = GetLanding(chunk.NextRow++);
        // Rows between the Fire Worm floor and its checkpoint would cut through the arena; BossArena owns that space.
        if (bossRow >= 0 && landing.Row > bossRow && landing.Row < bossCheckpointRow) { chunk.Built++; return true; }
        var pieces = landing.Pieces;
        var platform = Instantiate(settings.platformPrefab, chunk.Root.transform);
        platform.name = "Landing " + landing.Row + " - " + landing.Shape;
        Place(platform.transform, pieces[0]);
        // Zone tint (PillarEnvironment); the boss floor keeps its authored colour.
        Color rock = landing.Row == bossRow || landing.Row == finalBossRow ? landing.Color : PillarEnvironment.TintRock(landing.Color, landing.Center.y);
        bool skin = PillarEnvironment.PixelArtTerrain;
        int skinSeed = currentSeed ^ (landing.Row * 73856093);
        PillarEnvironment.GlowAt(landing.Center.y, out Color glow, out float glowDensity);
        if (landing.MarkerIndex >= 0)
        {
            // checkpoints read as "safe"
            glow = new Color(0.45f, 1f, 0.95f);
            glowDensity = 0.75f;
        }

        var render = platform.GetComponent<SpriteRenderer>();
        var renderers = new SpriteRenderer[pieces.Length];
        for (int k = 0; k < pieces.Length; k++)
        {
            var go = k == 0 ? platform : Instantiate(settings.platformPrefab, chunk.Root.transform);
            if (k > 0)
            {
                go.name = "Rock piece";
                Place(go.transform, pieces[k]);
            }

            renderers[k] = go.GetComponent<SpriteRenderer>();
            if (renderers[k] != null) renderers[k].color = PieceColor(pieces[k], rock);
        }
        if (render != null)
        {
            if (landing.Row == metrics.LastRow) render.color = new Color(0.8f, 0.45f, 0.2f);
            else if (landing.MarkerIndex >= 0) render.color = new Color(0.35f, 0.65f, 0.7f);
        }
        if (landing.Row == bossRow || landing.Row == finalBossRow)
        {
            var floor = platform.GetComponent<Collider2D>();
            // One-way until the fight starts; BossArenaLock then makes the floor solid.
            var effector = platform.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.useOneWayGrouping = true;
            floor.usedByEffector = true;
            if (landing.Row == bossRow)
            {
                var room = new GameObject("Fire Worm - Required Encounter");
                room.transform.SetParent(chunk.Root.transform, false);
                room.transform.position = transform.position + (Vector3)landing.Center;
                var encounter = settings.bossEncounter;
                Func<bool> started = () => true, defeated = () => true;   // already beaten: gates shut, roof open
                if (!BossDefeated)
                {
                    bossGate = room.AddComponent<BossFightGate>();
                    bossGate.Initialize(encounter, floor, player, () => { run.BossDefeated = true; SaveRun(); });
                    var gate = bossGate;
                    started = () => gate.Started;
                    defeated = () => gate.Defeated;
                    room.AddComponent<BossArenaLock>().Initialize(floor, player, encounter.combatWidth * 0.5f,
                        encounter.ceilingHeight, () => gate.Started && !gate.Defeated);
                    BossArena.HideSeals(room.transform);
                }
                // The climb-out hole goes on the checkpoint ledge's side, unless that would put it right under the ledge.
                var exit = GetLanding(bossCheckpointRow);
                float holeX = encounter.combatWidth * 0.5f - 3;
                float side = exit.Center.x >= landing.Center.x ? 1 : -1;
                if (Mathf.Abs(exit.Center.x - landing.Center.x - side * holeX) < exit.Width * 0.5f + 2) side = -side;
                BossArena.BuildFireWorm(room.transform, settings.platformPrefab, settings.pillarWidth, encounter.combatWidth * 0.5f,
                    encounter.ceilingHeight, side, landing.Color, landing.Center.y, skinSeed, started, defeated);
            }
            else if (landing.Row == finalBossRow)
            {
                var room = new GameObject("Grim Death - Final Encounter");
                room.transform.SetParent(chunk.Root.transform, false);
                room.transform.position = transform.position + (Vector3)landing.Center;
                var encounter = settings.finalBossEncounter;
                Func<bool> started = () => true, defeated = () => true;
                if (!FinalBossDefeated)
                {
                    finalBossGate = room.AddComponent<GrimDeathEncounter>();
                    finalBossGate.Initialize(encounter, floor, player, () => { run.FinalBossDefeated = true; SaveRun(); });
                    var finalGate = finalBossGate;
                    started = () => finalGate.Started;
                    defeated = () => finalGate.Defeated;
                    room.AddComponent<BossArenaLock>().Initialize(floor, player, finalGate.CombatWidth * 0.5f,
                        encounter.ceilingHeight, () => finalGate.Started && !finalGate.Defeated);
                    BossArena.HideSeals(room.transform);
                }
                // The route ledges inside this arena only appear once it is won; keep the dodge ledges clear of them.
                var inside = new List<Vector4>();
                for (int row = finalBossRow + 1; row <= metrics.LastRow && GetLanding(row).Center.y < landing.Center.y + encounter.ceilingHeight; row++)
                    foreach (var piece in GetLanding(row).Pieces)
                        inside.Add(new Vector4(transform.position.x + piece.X, transform.position.y + piece.Top, piece.Width, piece.Height));
                BossArena.BuildGrimDeath(room.transform, settings.platformPrefab, landing.Width * 0.5f, encounter.ceilingHeight, inside,
                    landing.Color, landing.Center.y, skinSeed, started, defeated);
            }
        }
        if (landing.MarkerIndex >= 0 && settings.heightMarkers[landing.MarkerIndex].prefab != null)
            Instantiate(settings.heightMarkers[landing.MarkerIndex].prefab,
                transform.position + (Vector3)landing.Center, Quaternion.identity, chunk.Root.transform);
        if (landing.MarkerIndex >= 0 && settings.heightMarkers[landing.MarkerIndex].checkpoint && settings.checkpointShopPrefab != null)
        {
            float offset = Mathf.Min(settings.checkpointShopOffset, Mathf.Max(0, landing.Width * .5f - 1.5f));
            var merchant = Instantiate(settings.checkpointShopPrefab,
                transform.position + (Vector3)landing.Center + new Vector3(offset, .02f, 0), Quaternion.identity, chunk.Root.transform);
            merchant.name = "Memento Mori - " + settings.heightMarkers[landing.MarkerIndex].id;
        }
        if (skin) // after the boss gate reads the floor renderer
            for (int k = 0; k < pieces.Length; k++)
                if (renderers[k] != null)
                    TerrainSkin.Apply(renderers[k], skinSeed ^ (k * 0x51ed27), pieces[k].Cap, glow, glowDensity,
                        pieces[k].Cap ? 1 : 0, pieces[k].Flat, Blend(pieces[k].Tone));

        tops.Clear();
        bottoms.Clear();
        VerticalLayout.Surfaces(pieces, tops, bottoms);
        PlatformBuilt?.Invoke(new BuiltPlatform
        {
            Tops = World(tops), Bottoms = World(bottoms),
            ChunkRoot = chunk.Root.transform, Platform = platform, Landing = landing,
            WorldTop = transform.position + (Vector3)landing.Center, ZoneHeight = landing.Center.y, Seed = skinSeed,
            IsBossFloor = landing.Row == bossRow || landing.Row == finalBossRow, IsCheckpoint = landing.MarkerIndex >= 0,
            IsSummit = landing.Row == metrics.LastRow
        });
        chunk.Built++;
        Physics2D.SyncTransforms();
        SpawnEnemy(landing);
        return true;
    }

    // Positions and scales a unit-square platform prefab to cover a piece (generator-local box, centred on its middle).
    private void Place(Transform box, VerticalLayout.Piece piece)
    {
        box.position = transform.position + new Vector3(piece.X, piece.Top - piece.Height * 0.5f, 0);
        box.localScale = new Vector3(piece.Width, piece.Height, 1);
    }

    private static Color PieceColor(VerticalLayout.Piece piece, Color rock)
    {
        switch (piece.Tone)
        {
            case VerticalLayout.Tone.Body: return new Color(rock.r * 0.8f, rock.g * 0.8f, rock.b * 0.8f);
            case VerticalLayout.Tone.Bone: return new Color(0.78f, 0.72f, 0.6f);
            case VerticalLayout.Tone.Lacquer: return new Color(0.62f, 0.17f, 0.13f);
            default: return rock;
        }
    }

    // How strongly TerrainSkin pulls a piece toward the zone's rock colour (bone and lacquer keep most of their own).
    private static float Blend(VerticalLayout.Tone tone) => tone == VerticalLayout.Tone.Bone ? 0.2f : tone == VerticalLayout.Tone.Lacquer ? 0.1f : 0.6f;

    // Converts generator-local (x, y, half width) surface points to world space; z (the half width) is unchanged.
    private Vector3[] World(List<Vector3> local)
    {
        var world = new Vector3[local.Count];
        for (int i = 0; i < world.Length; i++)
            world[i] = new Vector3(transform.position.x + local[i].x, transform.position.y + local[i].y, local[i].z);

        return world;
    }

    private void PayKill()
    {
        var wallet = player != null ? player.GetComponent<AscendJigoku.Gameplay.CurrencyWallet>() : null;
        if (wallet != null && settings.coinsPerKill > 0) wallet.TrySetBalance(wallet.Balance + settings.coinsPerKill);
    }

    // Spawns the landing's enemy, restoring its saved health. Skips it if already killed, already alive, or if it
    // would not fit on the ledge or would overlap level geometry.
    private void SpawnEnemy(VerticalLayout.Landing landing)
    {
        if (landing.EnemyIndex < 0) return;
        if (enemyRecords.TryGetValue(landing.Row, out var record))
        {
            if (record.Health <= 0 || record.Actor != null) return;
        }
        else
        {
            record = new EnemyRecord { Row = landing.Row, Health = int.MaxValue };
            enemyRecords.Add(landing.Row, record);
        }

        var prefab = settings.enemies[landing.EnemyIndex].prefab;
        if (prefab.GetComponent<EnemyHealth>() == null || prefab.GetComponent<Collider2D>() == null) return;
        Vector3 top = transform.position + (Vector3)landing.Center;
        var enemy = Instantiate(prefab, top + Vector3.up * 2, Quaternion.identity, generatedRoot);
        var shape = enemy.GetComponent<Collider2D>();
        Physics2D.SyncTransforms();
        // offset = how far from the ledge centre the enemy stands, on the open side (EnemySide), kept 0.4 inside the edge.
        float half = shape.bounds.extents.x, offset = landing.Width * 0.5f - half - 0.4f;
        var profile = enemy.GetComponent<EnemyCombat>()?.Definition;
        bool flying = profile != null && profile.movement == EnemyMovement.Flying;
        // Dense encounters can share the landing space, but must still fit fully on the ledge.
        float clearance = settings.enemyOnEveryPlatform ? half + .1f
            : Mathf.Max(settings.clearLandingHalfWidth, playerShape.bounds.extents.x + .5f) + half + .25f;
        if (offset < clearance)
        {
            enemy.SetActive(false);
            Destroy(enemy);
            return;
        }

        Vector3 position = enemy.transform.position;
        position.x += landing.EnemySide * offset;
        position.y += top.y - shape.bounds.min.y + (flying ? 1.5f : 0.04f);
        enemy.transform.position = position;
        Physics2D.SyncTransforms();
        // Layer 3 holds the terrain; if the enemy overlaps it, discard it.
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(1 << 3);
        overlaps.Clear();
        Physics2D.OverlapBox(shape.bounds.center, (Vector2)shape.bounds.size * 0.95f, 0, filter, overlaps);
        if (overlaps.Count > 0)
        {
            enemy.SetActive(false);
            Destroy(enemy);
            return;
        }

        enemy.GetComponent<EnemyBrain>()?.SetHome(position);
        enemy.name = prefab.name + " - row " + landing.Row;
        record.Actor = enemy.GetComponent<EnemyHealth>();
        record.Actor.RestoreHealth(record.Health);
        record.Health = record.Actor.CurrentHealth;
        record.Actor.HealthChanged += (hp, max) =>
        {
            if (hp <= 0 && record.Health > 0) PayKill();
            record.Health = hp;
        };
        activeEnemies.Add(record);
    }

    private void Update()
    {
        if (AscendJigoku.Gameplay.VictoryFlow.IsPlaying || generatedRoot == null || player == null || Time.timeScale <= 0) return;
        if (health.CurrentHealth > 0) TrackPlayer();
        if (Time.time >= nextRefresh)
        {
            nextRefresh = Time.time + 0.15f;
            RefreshStreaming();
        }

        BuildFrame();
    }

    // Per-frame player bookkeeping: hard-fall damage and bounce, remembering the last safe landing, and checkpoint
    // arrival (heal, clear hostiles, save the run, fire onHeightReached).
    private void TrackPlayer()
    {
        bool wasResting = checkpointResting; // kept while hopping in place, so landing again does not re-announce and re-heal
        float feet = playerShape.bounds.min.y;
        if (IsFallBouncing && (body.linearVelocityY <= 0 || player.IsGrounded)) IsFallBouncing = false;
        fallPeak = Mathf.Max(fallPeak, feet);
        // Only a hard fall hurts: falling as fast as a free drop of fallDistance (or out of the pillar's bottom).
        float hardFallSpeed = Mathf.Sqrt(2 * fallGravity * Mathf.Max(2, settings.fallDistance));
        if (!IsFallBouncing && Time.time >= nextFallDamageAt
            && (body.linearVelocityY <= -hardFallSpeed || feet < transform.position.y - 6))
        {
            int damage = Mathf.Max(1, settings.fallDamage);
            nextFallDamageAt = Time.time + 0.6f;
            combat?.Cancel();
            parry?.Cancel();
            if (health.CurrentHealth <= damage)
            {
                FallToDeath(damage);
                return;
            }

            health.TakeFallDamage(damage);
            // Bounce back up about as far as you fell, so one mistake costs one hit, not the run.
            // Launch speed sqrt(2 * g * h) reaches height h.
            float height = Mathf.Clamp(fallPeak - feet + 1.5f, settings.fallBounceHeight, settings.fallDistance * 2);
            player.FallBounce(Mathf.Sqrt(2 * fallGravity * height));
            health.ProtectFor(1f);
            IsFallBouncing = true;
            fallPeak = feet;
            return;
        }
        if (!player.IsGrounded) return;
        int row = Mathf.Clamp(Mathf.RoundToInt((feet - transform.position.y) / metrics.RowHeight), 0, metrics.LastRow);
        if (row > RouteLimitRow) return;
        var landing = GetLanding(row);
        float x = playerShape.bounds.center.x - transform.position.x;
        float margin = playerShape.bounds.extents.x + 0.1f;
        if (Mathf.Abs(feet - transform.position.y - landing.Center.y) > 0.22f
            || Mathf.Abs(x - landing.Center.x) > landing.Width * 0.5f - margin) return;
        fallPeak = feet;
        lastSafeRow = row;
        safePosition = StandPosition(landing, x);
        checkpointResting = false;
        if (landing.MarkerIndex < 0) return;
        var marker = settings.heightMarkers[landing.MarkerIndex];
        if (marker.checkpoint)
        {
            checkpointResting = true;
            if (!wasResting || Time.time >= nextSanctuarySweep)
            {
                nextSanctuarySweep = Time.time + .3f;
                AscendJigoku.Gameplay.CheckpointSanctuary.ClearHostiles(gameObject.scene,
                    AscendJigoku.Gameplay.CheckpointSanctuary.VisibleRegion(transform.position + (Vector3)landing.Center, landing.Width * .5f));
                if (!wasResting)
                {
                    health.Heal(health.MaxHealth);
                    AscendJigoku.UI.GameplayHud.Announce("CHECKPOINT — safe to rest.");
                }
            }
        }
        if (!reachedMarkers.Add(row)) return;
        if (marker.checkpoint && row > run.Checkpoint)
        {
            run.Checkpoint = row;
            respawn.SetRespawnPoint(StandPosition(landing, landing.Center.x));
            CaptureSnapshot();
            SaveRun();
        }

        onHeightReached.Invoke(marker.id);
    }

    // True while actor is standing on a checkpoint landing (used to decide whether they are safe there).
    public bool IsSafeCheckpointFor(PlayerHealth actor)
    {
        if (actor == null || actor != health || playerShape == null || !player.IsGrounded || generatedRoot == null) return false;
        float feet = playerShape.bounds.min.y;
        int row = Mathf.Clamp(Mathf.RoundToInt((feet - transform.position.y) / metrics.RowHeight), 0, metrics.LastRow);
        if (row > RouteLimitRow) return false;
        var landing = GetLanding(row);
        return landing.MarkerIndex >= 0 && settings.heightMarkers[landing.MarkerIndex].checkpoint
            && Mathf.Abs(feet - transform.position.y - landing.Center.y) < .22f
            && Mathf.Abs(playerShape.bounds.center.x - transform.position.x - landing.Center.x) < landing.Width * .5f;
    }

    public void ForgetClearedEnemy(EnemyHealth enemy)
    {
        foreach (var record in enemyRecords.Values)
            if (record.Actor == enemy)
            {
                record.Health = 0;
                break;
            }
    }

    // Last hit point: no bounce. The camera holds and the player drops through the level and off screen.
    private void FallToDeath(int damage)
    {
        var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (follow != null) follow.Hold = true;
        playerShape.enabled = false;
        health.TakeFallDamage(damage);
    }

    private void OnDisable() => IsFallBouncing = false;

    private void OnDestroy()
    {
        if (respawn != null) respawn.BeforeRespawn -= PrepareRespawn;
        if (Active == this) Active = null;
    }
}
