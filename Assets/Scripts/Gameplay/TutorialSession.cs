using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace AscendJigoku.Gameplay
{
    // Opening and tutorial (AJ-71). The first New Run (and the TUTORIAL menu entry) plays this before the climb.
    // It runs inside the pillar scene: the generator is switched off, a short course is built far to the side of
    // the pillar, and when it ends (finished, skipped or after the unwinnable finale) the generator starts and the
    // player is put on the first ledge.
    public static class Tutorial
    {
        private const string DoneKey = "AscendJigoku.TutorialDone";
        private static bool pending;

        public static bool Done => PlayerPrefs.GetInt(DoneKey, 0) == 1;
        public static void ArmForNewRun() => pending = !Done;   // later runs skip it
        public static void ArmReplay() => pending = true;
        internal static void MarkDone()
        {
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => pending = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryStart(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryStart(scene);

        private static void TryStart(Scene scene)
        {
            if (!pending || !scene.IsValid()) return;
            var generator = UnityEngine.Object.FindAnyObjectByType<VerticalLevelGenerator>();
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
            if (generator == null || player == null || generator.gameObject.scene != scene
                || generator.Settings == null || generator.Settings.platformPrefab == null) return;
            pending = false;
            generator.enabled = false; // before its Start, so no pillar is built and no fall check runs
            var go = new GameObject("Tutorial");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<TutorialSession>().Begin(generator, player);
        }
    }

    public sealed class TutorialDeathHandler : PlayerDeathHandler
    {
        public TutorialSession session;
        public override void OnPlayerDied() => session.OnPlayerDied();
    }

    public sealed class TutorialSession : MonoBehaviour
    {
        private sealed class Station
        {
            public string Title;
            public Func<string> Text;
            public Func<bool> Done;
            public float Rest;
            public Action Begin, End;
            public TutorialSeal Seal;
        }

        private const float ArenaGate = 288, ArenaTrigger = 298, SkipSeconds = 1.2f;
        private static readonly string[] OpeningLines =
        {
            "Below the last circle of Jigoku, the fire has no floor.",
            "You fell here. You do not remember why.",
            "Somewhere above, very far above, there is a door out.\nSo you climb.",
        };
        private static readonly string[] SkeletonLines =
        {
            "Hahahaha... oh, that never gets old.",
            "Here, on the house, chum. I enjoy watching you suffer, but the next one will cost ya.",
        };

        private VerticalLevelGenerator generator;
        private PlayerHealth health;
        private PlayerController controller;
        private PlayerCombat combat;
        private PlayerParry parry;
        private PlayerSpell spell;
        private PlayerControlLock controlLock;
        private PlayerRespawn respawn;
        private Rigidbody2D body;
        private Collider2D shape;
        private ActiveInputPrompts prompts;
        private TutorialUI ui;
        private TutorialLevel level;
        private TutorialHand hand;
        private TutorialSeal gate;
        private TutorialDeathHandler deathHandler;
        private readonly List<Station> stations = new List<Station>();
        private readonly List<GameObject> spawned = new List<GameObject>();
        private Coroutine main;
        private float footOffset, skipHeld;
        private bool finale, ending, leaving;
        public int StationIndex { get; private set; }
        public bool InFinale => finale;

        // Counters the lessons read; reset when each lesson begins.
        private int swings, pogos, dashes, parries, shotsBase, healsBase;
        private CombatAttack.Swing lastSwing, pogoSwing;
        private bool wasDashing;
        private float parrySeen = float.NegativeInfinity;

        private string K(string label) => TutorialUI.Key(label);
        private string Key(string action) => K(prompts.Button(action));
        private float PlayerX => body.position.x - level.Origin.x;
        private float Feet => shape.bounds.min.y - level.Origin.y;

        public void Begin(VerticalLevelGenerator gen, PlayerHealth player)
        {
            generator = gen;
            health = player;
            controller = player.GetComponent<PlayerController>();
            combat = player.GetComponent<PlayerCombat>();
            parry = player.GetComponent<PlayerParry>();
            spell = player.GetComponent<PlayerSpell>();
            respawn = player.GetComponent<PlayerRespawn>();
            body = player.GetComponent<Rigidbody2D>();
            shape = player.GetComponent<Collider2D>();
            controlLock = player.GetComponent<PlayerControlLock>();
            if (controlLock == null) controlLock = player.gameObject.AddComponent<PlayerControlLock>();
            prompts = FindAnyObjectByType<ActiveInputPrompts>();
            if (prompts == null) prompts = gameObject.AddComponent<ActiveInputPrompts>();
            ui = TutorialUI.Create(transform, prompts);
            footOffset = player.transform.position.y - shape.bounds.min.y;
            wasDashing = controller.DashOnCooldown;
            deathHandler = player.gameObject.AddComponent<TutorialDeathHandler>();
            deathHandler.session = this;
            level = new TutorialLevel(gen, gen.transform.position + new Vector3(260, 0, 0));
            BuildCourse();
            controlLock.Lock();
            Place(2);
            main = StartCoroutine(Run());
        }

        // ---------- The course ----------

        private void BuildCourse()
        {
            level.Block(-14, 140, 0, -10);           // floor, cut by the pit between 140 and 190
            level.Block(190, 345, 0, -10);
            level.Block(-18, -14, 40, -10);          // back wall
            level.Block(346, 350, 40, -10);          // arena end
            level.Block(30, 36, 2.2f, 0);            // step: needs a jump
            level.Block(48, 54, 4.3f, 0);            // block: needs a double jump
            level.Block(68, 74, 9.5f, 3.4f);         // shaft: hanging wall you walk under ...
            level.Block(80, 98, 9.5f, 0);            // ... and the tall wall opposite, 6 apart
            BuildPit();

            string down = prompts.UsesController ? "Down" : "S / Down";
            Func<bool> grounded = () => controller.IsGrounded;
            AddStation("MOVE", () => prompts.UsesController ? $"{K("Left stick")} or {K("D-pad")} to move." : $"{K("A")} {K("D")} or the arrow keys to move.",
                () => PlayerX > 12, 2);
            AddStation("JUMP", () => $"Press {Key("Jump")} to jump onto the step.",
                () => grounded() && Feet > 1.8f && PlayerX > 28 && PlayerX < 38, 20);
            AddStation("DOUBLE JUMP", () => $"Press {Key("Jump")} again in mid-air to reach the block.",
                () => grounded() && Feet > 3.6f && PlayerX > 46 && PlayerX < 56, 40);
            AddStation("WALL JUMP", () => $"Hold toward a wall while falling to slide, then press {Key("Jump")} to kick off it. Climb the shaft.",
                () => grounded() && Feet > 9 && PlayerX > 66 && PlayerX < 100, 60);
            AddStation("DASH", () => $"Press {Key("Dash")} to dash. Once in the air, once on the ground.",
                () => dashes > 0, 102, 112);
            AddStation("ATTACK", () => $"Press {Key("Attack")} to strike. Hold up first to strike upward.",
                () => swings > 0, 114, 128);
            AddStation("POGO", () => $"In the air, hold {K(down)} and press {Key("Attack")} over spikes to bounce off them. Cross the pit.",
                () => pogos > 0 || (grounded() && PlayerX > 192), 136);
            AddStation("PARRY", () => $"The enemy winds up a slow strike. Press {Key("Parry")} just as it lands to deflect it.",
                () => parries > 0 || noFodder, 196, 248, SpawnFodder,
                () => { if (fodder != null) Destroy(fodder); });
            AddStation("SHOOT", () => $"Tap {Key("CastOrHeal")} to shoot.",
                () => spell != null && spell.ShotsFired > shotsBase, 252, 264, () => shotsBase = spell.ShotsFired);
            AddStation("HEAL", () => $"That hurt. Hold {Key("CastOrHeal")} until the ring fills to heal.",
                () => spell != null && spell.HealsCompleted > healsBase, 266, 278,
                () => { healsBase = spell.HealsCompleted; health.TakeFallDamage(1); });
        }

        private void AddStation(string title, Func<string> text, Func<bool> done, float rest, float sealX = -1, Action begin = null, Action end = null)
        {
            var station = new Station { Title = title, Text = text, Done = done, Rest = rest, Begin = begin, End = end };
            if (sealX > 0) station.Seal = level.Seal(sealX, title);
            stations.Add(station);
        }

        private void BuildPit()
        {
            level.SpikeBed(140, 190, -4.2f);
            foreach (float x in new[] { 153f, 166f, 179f }) level.SpikePost(x, -0.8f);
        }

        private GameObject fodder;
        private bool noFodder; // no enemy prefab to parry: let the lesson pass instead of locking the player in
        private float nextFodder;
        private GameObject EnemyPrefab(string preferred)
        {
            GameObject first = null;
            foreach (var spawn in generator.Settings.enemies)
            {
                var prefab = spawn != null ? spawn.prefab : null;
                if (prefab == null || prefab.GetComponent<EnemyHealth>() == null || prefab.GetComponent<Collider2D>() == null) continue;
                if (preferred != null && prefab.name.Contains(preferred)) return prefab;
                if (first == null) first = prefab;
            }
            return first;
        }

        private GameObject Spawn(GameObject prefab, float x, float y)
        {
            var enemy = Instantiate(prefab, level.At(x, y), Quaternion.identity, level.Root);
            enemy.GetComponent<EnemyBrain>()?.SetHome(enemy.transform.position);
            spawned.Add(enemy);
            return enemy;
        }

        private void SpawnFodder()
        {
            var prefab = EnemyPrefab("Fodder");
            if (prefab == null)
            {
                noFodder = true;
                return;
            }
            fodder = Spawn(prefab, 232, 1.5f);
            // The lesson needs a strike to parry: the pillar's imp only hurts by touch and has no real swing.
            var combat = fodder.GetComponent<EnemyCombat>();
            if (combat != null && combat.Definition != null)
            {
                var profile = Instantiate(combat.Definition);
                profile.attackSize = new Vector2(1.25f, 1.25f);
                profile.contactDamage = 0;
                profile.canFly = false;
                profile.evadeAttacks = false;
                combat.UseDefinition(profile);
            }
            var hp = fodder.GetComponent<EnemyHealth>();
            if (hp != null) hp.DamageEnabled = false; // can't be cut down before the parry lands
        }

        // ---------- Flow ----------

        private IEnumerator Run()
        {
            yield return ui.Opening(OpeningLines);
            yield return ui.FadeCover(0, 0.9f);
            controlLock.Unlock();

            for (int i = 0; i < stations.Count; i++)
            {
                var station = stations[i]; StationIndex = i;
                swings = pogos = dashes = parries = 0;
                if (station.Title != "HEAL" && health.CurrentHealth < health.MaxHealth) health.Heal(health.MaxHealth);
                respawn.SetRespawnPoint(level.At(station.Rest, footOffset + 0.08f));
                station.Begin?.Invoke();
                string shown = null;
                while (!station.Done())
                {
                    string text = station.Text();
                    if (!ui.Flashing && text != shown)
                    {
                        ui.ShowPrompt($"{i + 1} / {stations.Count}    {station.Title}", text);
                        shown = text;
                    }

                    // Died and was revived: hurt again so there is something to heal.
                    if (station.Title == "HEAL" && health.CurrentHealth >= health.MaxHealth) health.TakeFallDamage(1);
                    if (station.Title == "PARRY" && fodder == null && Time.time > nextFodder)
                    {
                        nextFodder = Time.time + 1.5f;
                        SpawnFodder();
                    }
                    yield return null;
                }
                ui.FlashDone("Good.");
                station.End?.Invoke();
                StationIndex = i + 1;
                if (station.Seal != null) station.Seal.Dissolve();
                yield return new WaitForSeconds(0.6f);
            }

            ui.ShowPrompt("LAST LESSON", "Defeat them.");
            while (PlayerX < ArenaTrigger) yield return null;
            finale = true;
            gate = level.Seal(ArenaGate, "gate");
            SpawnHorde();
            hand = TutorialHand.Create(level.Root);
            float follow = 1.2f, warn = 0.7f;
            yield return new WaitForSeconds(4);
            while (true)
            {
                yield return hand.Strike(body.transform, level.Origin.y, follow, warn);
                follow = Mathf.Max(0.6f, follow - 0.15f); warn = Mathf.Max(0.35f, warn - 0.08f);
                yield return new WaitForSeconds(1.2f);
            }
        }

        private void SpawnHorde()
        {
            var types = new List<GameObject>();
            foreach (var spawn in generator.Settings.enemies)
                if (spawn != null && spawn.prefab != null && spawn.prefab.GetComponent<EnemyHealth>() != null
                    && spawn.prefab.GetComponent<Collider2D>() != null) types.Add(spawn.prefab);
            for (int i = 0; i < 7 && types.Count > 0; i++)
            {
                var enemy = Spawn(types[i % types.Count], PlayerX + 8 + i * 3.5f, 2.5f);
                var hp = enemy.GetComponent<EnemyHealth>();
                if (hp != null) hp.DamageEnabled = false; // nothing here can be killed
            }
        }

        public void OnPlayerDied()
        {
            if (leaving) return;
            if (!finale)
            {
                StartCoroutine(Revived());
                return;
            }
            if (ending) return;
            ending = true;
            if (main != null) StopCoroutine(main);
            StartCoroutine(Ending());
        }

        private IEnumerator Revived()
        {
            Revive();
            respawn.Respawn();
            health.ProtectFor(1);
            yield break;
        }

        private IEnumerator Ending()
        {
            ui.HidePrompt();
            yield return ui.FadeCover(1, 0.8f);
            ui.ShowCoverText(false);
            ClearEnemies();
            if (hand != null) Destroy(hand.gameObject);
            Revive();
            controlLock.Lock();
            Place(ArenaGate + 4);
            var shop = generator.Settings.checkpointShopPrefab;
            var animation = shop != null ? shop.GetComponentInChildren<ShopkeeperAnimation>(true) : null;
            AudioClip voice = shop != null && shop.catalog != null ? shop.catalog.openSound : null;
            yield return new WaitForSeconds(0.4f);
            yield return ui.Talk(animation != null ? animation.frames : null, "MEMENTO MORI", SkeletonLines, voice);
            yield return HandOff();
        }

        private IEnumerator HandOff()
        {
            leaving = true;
            ui.HidePrompt();
            yield return ui.FadeCover(1, ui.CoverAlpha > 0.5f ? 0.01f : 0.6f);
            ui.ShowCoverText(false);
            ClearEnemies();
            Tutorial.MarkDone();
            if (deathHandler != null) Destroy(deathHandler);
            if (health.CurrentHealth <= 0) Revive(); else health.Heal(health.MaxHealth);
            controlLock.Lock();
            generator.enabled = true; // Start() builds the pillar on the next frame
            for (int i = 0; i < 120 && VerticalLevelGenerator.Active != generator; i++) yield return null;
            Debug.Log(generator.DebugTeleportToRow(0));
            Destroy(level.Root.gameObject);
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.SnapToTarget();
            yield return new WaitForSeconds(0.3f);
            controlLock.Unlock();
            yield return ui.FadeCover(0, 1f);
            Destroy(gameObject);
        }

        private void ClearEnemies()
        {
            foreach (var enemy in spawned) if (enemy != null) Destroy(enemy);
            spawned.Clear();
            spell.Cancel(true);
        }

        // ---------- Player helpers ----------

        private void Place(float x)
        {
            Vector3 position = level.At(x, footOffset + 0.08f);
            body.linearVelocity = Vector2.zero;
            body.position = position;
            body.transform.position = position;
            Physics2D.SyncTransforms();
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.SnapToTarget();
        }

        // PlayerHealth has no revive (death normally reloads the scene), so set the health directly.
        private void Revive()
        {
            health.Revive();
            controller.ResetMovement(); controller.ResetAnimation();
        }

        // ---------- Per-frame ----------

        private void Update()
        {
            if (leaving) return;
            var swing = combat.Attack.CurrentSwing;
            if (swing != null && swing != lastSwing)
            {
                lastSwing = swing;
                swings++;
            }

            if (swing != null && swing.PogoTriggered && swing != pogoSwing)
            {
                pogoSwing = swing;
                pogos++;
            }
            bool dashing = controller.DashOnCooldown;
            if (dashing && !wasDashing) dashes++;
            wasDashing = dashing;
            if (parry.LastSuccessTime > parrySeen)
            {
                parrySeen = parry.LastSuccessTime;
                parries++;
            }
            if (body.position.y < level.Origin.y - 30 && health.CurrentHealth > 0) respawn.Respawn();

            bool held = (Keyboard.current != null && Keyboard.current.tabKey.isPressed);
            foreach (var pad in Gamepad.all) held |= pad.selectButton.isPressed;
            skipHeld = held && Time.timeScale > 0 ? skipHeld + Time.unscaledDeltaTime : 0; // not while paused or shopping
            ui.SetSkip(prompts.UsesController ? "Back" : "Tab", skipHeld / SkipSeconds);
            if (skipHeld >= SkipSeconds) Skip();
        }

        private void Skip()
        {
            if (leaving) return;
            leaving = true;
            StopAllCoroutines();
            StartCoroutine(HandOff());
        }

        private void OnDestroy()
        {
            if (deathHandler != null) Destroy(deathHandler);
            if (generator != null && !generator.enabled) generator.enabled = true; // never leave the pillar switched off
        }
    }
}
