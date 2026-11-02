using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Seeded chunks; only nearby terrain and enemies exist as GameObjects.
[DefaultExecutionOrder(-100)]
public sealed class VerticalLevelGenerator : MonoBehaviour
{
    [SerializeField] private VerticalGenerationSettings settings;
    [SerializeField] private PlayerController player;
    [SerializeField] private int currentSeed;
    public UnityEvent<string> onHeightReached = new UnityEvent<string>();
    private sealed class Run { public VerticalGenerationSettings Settings; public string Scene; public int Seed, Checkpoint; public bool BossDefeated; }
    private static Run run;
    private sealed class Chunk { public GameObject Root; public int NextRow, EndRow, Built; }
    private sealed class EnemyRecord { public int Row, Health; public EnemyHealth Actor; }
    private readonly Dictionary<int, Chunk> chunks = new Dictionary<int, Chunk>();
    private readonly Dictionary<int, EnemyRecord> enemyRecords = new Dictionary<int, EnemyRecord>();
    private readonly HashSet<EnemyRecord> activeEnemies = new HashSet<EnemyRecord>();
    private readonly HashSet<int> reachedMarkers = new HashSet<int>();
    private readonly List<int> removal = new List<int>();
    private readonly List<Chunk> buildOrder = new List<Chunk>();
    private readonly List<EnemyRecord> enemyRemoval = new List<EnemyRecord>();
    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    private Transform generatedRoot;
    private Rigidbody2D body;
    private Collider2D playerShape;
    private PlayerHealth health;
    private PlayerRespawn respawn;
    private PlayerCombat combat;
    private PlayerParry parry;
    private VerticalLayout.Metrics metrics;
    private float nextRefresh, fallPeak, footOffset, nextFallDamageAt;
    private int lastSafeRow;
    private Vector3 safePosition;
    private int bossRow = -1, bossCheckpointRow = -1;
    private BossFightGate bossGate;
    public BossFightGate BossGate => bossGate;
    public int BossRow => bossRow;
    public bool BossDefeated => run != null && run.BossDefeated;
    public bool BossRouteLocked => bossRow >= 0 && !BossDefeated;
    public int CurrentSeed => currentSeed;
    public int LoadedChunkCount => chunks.Count;
    public int ActiveEnemyCount => activeEnemies.Count;
    public int CheckpointRow => run != null ? run.Checkpoint : 0;
    public int LastSafeRow => lastSafeRow;
    public bool IsFallBouncing { get; private set; }
    public VerticalGenerationSettings Settings => settings;
    public PlayerController Player => player;
    public float LoadAboveDistance { get; private set; }
    public float KeepBelowDistance { get; private set; }
    public VerticalLayout.Metrics Metrics => metrics;
    public int ActivePlatformCount { get { int count = 0; foreach (var c in chunks.Values) count += c.Built; return count; } }
    public int PendingPlatformCount { get { int count = 0; foreach (var c in chunks.Values) count += c.EndRow - c.NextRow; return count; } }
    public bool IsChunkLoaded(int index) => chunks.ContainsKey(index);
    public bool TryGetChunkProgress(int index, out int built, out int total)
    {
        built = total = 0;
        if (!chunks.TryGetValue(index, out var chunk)) return false;
        built = chunk.Built; total = chunk.EndRow - index * metrics.RowsPerChunk;
        return true;
    }
    public VerticalLayout.Landing GetLanding(int row)
    {
        var landing = VerticalLayout.GetLanding(settings, currentSeed, row, metrics);
        if (row == bossRow)
        {
            landing.Center.x = 0; landing.Width = settings.pillarWidth; landing.Thickness = 1.2f;
            landing.Shape = VerticalLayout.RockShape.Shelf; landing.EnemyIndex = -1; landing.MarkerIndex = -1;
            landing.Color = new Color(0.35f, 0.35f, 0.39f);
        }
        else if (bossRow >= 0 && Mathf.Abs(row - bossRow) <= 2) landing.EnemyIndex = -1;
        return landing;
    }
    public static void BeginNewRun() => run = null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRun() => BeginNewRun();
    private void Start() => Generate();
    public bool Generate()
    {
        IsFallBouncing = false;
        nextFallDamageAt = 0;
        if (settings == null || settings.platformPrefab == null || player == null)
        { Debug.LogError("Generator needs settings, platform prefab and player.", this); return false; }
        body = player.GetComponent<Rigidbody2D>(); playerShape = player.GetComponent<Collider2D>();
        health = player.GetComponent<PlayerHealth>(); respawn = player.GetComponent<PlayerRespawn>();
        if (settings.checkpointShopPrefab != null)
        {
            var wallet = player.GetComponent<AscendJigoku.Gameplay.CurrencyWallet>();
            if (wallet == null) wallet = player.gameObject.AddComponent<AscendJigoku.Gameplay.PrototypeWallet>();
            var shop = player.GetComponent<AscendJigoku.Gameplay.ShopMenu>();
            if (shop == null) shop = player.gameObject.AddComponent<AscendJigoku.Gameplay.ShopMenu>();
            shop.wallet = wallet;
        }
        combat = player.GetComponent<PlayerCombat>(); parry = player.GetComponent<PlayerParry>();
        if (body == null || playerShape == null || health == null || respawn == null || body.gravityScale <= 0 || Physics2D.gravity.y >= 0)
        { Debug.LogError("Generator needs player health, respawn, collider and downward gravity.", this); return false; }
        metrics = VerticalLayout.Measure(settings, player.JumpSpeed, player.MoveSpeed, Mathf.Abs(Physics2D.gravity.y * body.gravityScale), player.MaxJumps);
        if (run == null || run.Settings != settings || run.Scene != gameObject.scene.path || (!settings.randomizeSeed && run.Seed != settings.seed))
            run = new Run { Settings = settings, Scene = gameObject.scene.path, Seed = settings.randomizeSeed ? System.Guid.NewGuid().GetHashCode() : settings.seed };
        currentSeed = run.Seed;
        bossRow = bossCheckpointRow = -1; bossGate = null;
        var encounter = settings.bossEncounter;
        if (encounter != null && encounter.bossPrefab != null && encounter.checkpointMarkerIndex >= 0
            && settings.heightMarkers != null && encounter.checkpointMarkerIndex < settings.heightMarkers.Length)
        {
            bossCheckpointRow = VerticalLayout.MarkerRow(settings.heightMarkers[encounter.checkpointMarkerIndex], metrics);
            bossRow = Mathf.Max(2, bossCheckpointRow - Mathf.Max(3, encounter.rowsBeforeCheckpoint));
        }
        if (generatedRoot != null) { generatedRoot.gameObject.SetActive(false); Destroy(generatedRoot.gameObject); }
        chunks.Clear(); enemyRecords.Clear(); activeEnemies.Clear(); reachedMarkers.Clear();
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
        if (lastSafeRow > 0) { reachedMarkers.Add(lastSafeRow); respawn.Respawn(); }
        fallPeak = playerShape.bounds.min.y;
        RefreshStreaming();
        return true;
    }
    private Vector3 StandPosition(VerticalLayout.Landing landing, float x)
    {
        float margin = playerShape != null ? playerShape.bounds.extents.x + 0.2f : 0.7f;
        float half = Mathf.Max(0, landing.Width * 0.5f - margin);
        return transform.position + new Vector3(Mathf.Clamp(x, landing.Center.x - half, landing.Center.x + half), landing.Center.y + footOffset + 0.08f, 0);
    }
    private Chunk GetChunk(int index)
    {
        if (chunks.TryGetValue(index, out var chunk)) return chunk;
        var root = new GameObject("Chunk " + index); root.transform.SetParent(generatedRoot, false);
        chunk = new Chunk { Root = root, NextRow = index * metrics.RowsPerChunk,
            EndRow = Mathf.Min((index + 1) * metrics.RowsPerChunk, metrics.LastRow + 1) };
        chunks.Add(index, chunk);
        return chunk;
    }
    private void EnsureRowReady(int row)
    {
        var chunk = GetChunk(Mathf.Clamp(row, 0, metrics.LastRow) / metrics.RowsPerChunk);
        while (chunk.NextRow < chunk.EndRow) if (!BuildNext(chunk)) break;
    }
    private void PrepareRespawn(Vector3 destination)
    {
        IsFallBouncing = false;
        nextFallDamageAt = Time.time + 0.5f;
        EnsureRowReady(Mathf.RoundToInt((destination.y - transform.position.y - footOffset) / metrics.RowHeight));
        fallPeak = destination.y - footOffset;
        lastSafeRow = Mathf.Clamp(Mathf.RoundToInt((destination.y - transform.position.y - footOffset) / metrics.RowHeight), 0, metrics.LastRow);
        safePosition = destination;
    }
    public void RefreshStreaming()
    {
        if (generatedRoot == null) return;
        float height = Mathf.Clamp(player.transform.position.y - transform.position.y, 0, metrics.LastRow * metrics.RowHeight);
        float view = Camera.main != null && Camera.main.orthographic ? Camera.main.orthographicSize + 5 : 12;
        float above = Mathf.Max(view, settings.loadAbove);
        float below = Mathf.Max(view, settings.keepBelow, settings.fallDistance + metrics.RowHeight + 2);
        LoadAboveDistance = above; KeepBelowDistance = below;
        int lastChunk = metrics.LastRow / metrics.RowsPerChunk;
        int first = Mathf.Clamp(Mathf.FloorToInt((height - below) / metrics.ChunkHeight), 0, lastChunk);
        int last = Mathf.Clamp(Mathf.FloorToInt((height + above) / metrics.ChunkHeight), 0, lastChunk);
        int center = Mathf.Clamp(Mathf.FloorToInt(height / metrics.ChunkHeight), first, last);
        buildOrder.Clear(); buildOrder.Add(GetChunk(center));
        for (int i = center + 1; i <= last; i++) buildOrder.Add(GetChunk(i));
        for (int i = center - 1; i >= first; i--) buildOrder.Add(GetChunk(i));
        removal.Clear();
        foreach (var pair in chunks)
        {
            // Keep the unbeaten boss and its floor together; streaming must not reset its HP or drop it.
            if (BossRouteLocked && pair.Key == bossRow / metrics.RowsPerChunk) continue;
            float low = pair.Key * metrics.ChunkHeight, high = low + metrics.ChunkHeight;
            if (high < height - below - settings.unloadBuffer || low > height + above + settings.unloadBuffer)
                removal.Add(pair.Key);
        }
        foreach (int index in removal)
        { chunks[index].Root.SetActive(false); Destroy(chunks[index].Root); chunks.Remove(index); }
        enemyRemoval.Clear();
        foreach (var enemy in activeEnemies)
        {
            if (enemy.Actor == null) { enemyRemoval.Add(enemy); continue; }
            float y = enemy.Actor.transform.position.y - transform.position.y;
            bool homeLoaded = chunks.ContainsKey(enemy.Row / metrics.RowsPerChunk);
            if (!homeLoaded && (y < height - below - settings.unloadBuffer || y > height + above + settings.unloadBuffer))
            {
                enemy.Health = enemy.Actor.CurrentHealth;
                enemy.Actor.gameObject.SetActive(false); Destroy(enemy.Actor.gameObject); enemy.Actor = null;
                enemyRemoval.Add(enemy);
            }
        }
        foreach (var enemy in enemyRemoval) activeEnemies.Remove(enemy);
    }
    public void BuildFrame()
    {
        int budget = Mathf.Clamp(settings.platformsPerFrame, 1, 12);
        foreach (var chunk in buildOrder)
            while (chunk.Root != null && chunk.NextRow < chunk.EndRow && budget > 0)
            { if (!BuildNext(chunk)) break; budget--; }
    }
    private bool BuildNext(Chunk chunk)
    {
        if (BossRouteLocked && chunk.NextRow > bossRow) return false;
        var landing = GetLanding(chunk.NextRow++);
        var platform = Instantiate(settings.platformPrefab, chunk.Root.transform);
        platform.name = "Landing " + landing.Row + " - " + landing.Shape;
        float thickness = landing.Shape == VerticalLayout.RockShape.Spire || landing.Shape == VerticalLayout.RockShape.Terrace
            ? Mathf.Min(0.9f, landing.Thickness) : landing.Thickness;
        platform.transform.position = transform.position + new Vector3(landing.Center.x, landing.Center.y - thickness * 0.5f, 0);
        platform.transform.localScale = new Vector3(landing.Width, thickness, 1);
        var render = platform.GetComponent<SpriteRenderer>();
        if (render != null)
        {
            render.color = landing.Color;
            if (landing.Row == metrics.LastRow) render.color = new Color(0.8f, 0.45f, 0.2f);
            else if (landing.MarkerIndex >= 0) render.color = new Color(0.35f, 0.65f, 0.7f);
        }
        if (landing.Thickness > thickness)
        {
            var support = Instantiate(settings.platformPrefab, platform.transform);
            support.name = "Rock body";
            float depth = landing.Thickness - thickness;
            support.transform.localPosition = new Vector3(0, -0.5f - depth / (2 * thickness), 0);
            support.transform.localScale = new Vector3(landing.Shape == VerticalLayout.RockShape.Spire ? 0.6f : 0.8f, depth / thickness, 1);
            var supportRenderer = support.GetComponent<SpriteRenderer>();
            if (supportRenderer != null) supportRenderer.color = new Color(landing.Color.r * 0.8f, landing.Color.g * 0.8f, landing.Color.b * 0.8f);
        }
        if (landing.Row == bossRow)
        {
            var floor = platform.GetComponent<Collider2D>();
            var effector = platform.AddComponent<PlatformEffector2D>(); effector.useOneWay = true; effector.useOneWayGrouping = true;
            floor.usedByEffector = true;
            if (!BossDefeated)
            {
                var room = new GameObject("Fire Worm - Required Encounter"); room.transform.SetParent(chunk.Root.transform, false);
                room.transform.position = transform.position + (Vector3)landing.Center;
                bossGate = room.AddComponent<BossFightGate>();
                bossGate.Initialize(settings.bossEncounter, floor, player, () => run.BossDefeated = true);
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
        chunk.Built++;
        Physics2D.SyncTransforms();
        SpawnEnemy(landing);
        return true;
    }
    private void SpawnEnemy(VerticalLayout.Landing landing)
    {
        if (landing.EnemyIndex < 0) return;
        if (enemyRecords.TryGetValue(landing.Row, out var record))
        { if (record.Health <= 0 || record.Actor != null) return; }
        else { record = new EnemyRecord { Row = landing.Row, Health = int.MaxValue }; enemyRecords.Add(landing.Row, record); }
        var prefab = settings.enemies[landing.EnemyIndex].prefab;
        if (prefab.GetComponent<EnemyHealth>() == null || prefab.GetComponent<Collider2D>() == null) return;
        Vector3 top = transform.position + (Vector3)landing.Center;
        var enemy = Instantiate(prefab, top + Vector3.up * 2, Quaternion.identity, generatedRoot);
        var shape = enemy.GetComponent<Collider2D>(); Physics2D.SyncTransforms();
        float half = shape.bounds.extents.x, offset = landing.Width * 0.5f - half - 0.4f;
        var profile = enemy.GetComponent<EnemyCombat>()?.Definition;
        bool flying = profile != null && profile.movement == EnemyMovement.Flying;
        if (offset < Mathf.Max(settings.clearLandingHalfWidth, playerShape.bounds.extents.x + 0.5f) + half + 0.25f)
        { enemy.SetActive(false); Destroy(enemy); return; }
        Vector3 position = enemy.transform.position;
        position.x += landing.EnemySide * offset;
        position.y += top.y - shape.bounds.min.y + (flying ? 1.5f : 0.04f);
        enemy.transform.position = position; Physics2D.SyncTransforms();
        var filter = new ContactFilter2D { useTriggers = false }; filter.SetLayerMask(1 << 3);
        overlaps.Clear(); Physics2D.OverlapBox(shape.bounds.center, (Vector2)shape.bounds.size * 0.95f, 0, filter, overlaps);
        if (overlaps.Count > 0) { enemy.SetActive(false); Destroy(enemy); return; }
        enemy.GetComponent<EnemyBrain>()?.SetHome(position);
        enemy.name = prefab.name + " - row " + landing.Row;
        record.Actor = enemy.GetComponent<EnemyHealth>();
        record.Actor.RestoreHealth(record.Health);
        record.Health = record.Actor.CurrentHealth;
        record.Actor.HealthChanged += (hp, max) => record.Health = hp;
        activeEnemies.Add(record);
    }
    private void Update()
    {
        if (generatedRoot == null || player == null || Time.timeScale <= 0) return;
        if (health.CurrentHealth > 0) TrackPlayer();
        if (Time.time >= nextRefresh) { nextRefresh = Time.time + 0.15f; RefreshStreaming(); }
        BuildFrame();
    }
    private void TrackPlayer()
    {
        float feet = playerShape.bounds.min.y;
        if (IsFallBouncing && (body.linearVelocityY <= 0 || player.IsGrounded)) IsFallBouncing = false;
        fallPeak = Mathf.Max(fallPeak, feet);
        if (!IsFallBouncing && Time.time >= nextFallDamageAt && body.linearVelocityY <= 0.1f
            && (fallPeak - feet > Mathf.Max(2, settings.fallDistance) || feet < transform.position.y - 6))
        {
            health.TakeFallDamage(Mathf.Max(1, settings.fallDamage));
            nextFallDamageAt = Time.time + 0.6f;
            if (health.CurrentHealth > 0)
            {
                combat?.Cancel(); parry?.Cancel();
                float gravity = Mathf.Abs(Physics2D.gravity.y * body.gravityScale);
                player.FallBounce(Mathf.Sqrt(2 * gravity * Mathf.Max(2, settings.fallBounceHeight)));
                health.ProtectFor(0.8f);
                IsFallBouncing = true;
                fallPeak = feet;
            }
            return;
        }
        if (!player.IsGrounded) return;
        int row = Mathf.Clamp(Mathf.RoundToInt((feet - transform.position.y) / metrics.RowHeight), 0, metrics.LastRow);
        if (BossRouteLocked && row > bossRow) return;
        var landing = GetLanding(row);
        float x = playerShape.bounds.center.x - transform.position.x;
        float margin = playerShape.bounds.extents.x + 0.1f;
        if (Mathf.Abs(feet - transform.position.y - landing.Center.y) > 0.22f
            || Mathf.Abs(x - landing.Center.x) > landing.Width * 0.5f - margin) return;
        fallPeak = feet;
        lastSafeRow = row; safePosition = StandPosition(landing, x);
        if (landing.MarkerIndex < 0 || !reachedMarkers.Add(row)) return;
        var marker = settings.heightMarkers[landing.MarkerIndex];
        if (marker.checkpoint && row > run.Checkpoint)
        { run.Checkpoint = row; respawn.SetRespawnPoint(StandPosition(landing, landing.Center.x)); }
        onHeightReached.Invoke(marker.id);
    }
    private void OnDisable() => IsFallBouncing = false;
    private void OnDestroy()
    { if (respawn != null) respawn.BeforeRespawn -= PrepareRespawn; }
}
