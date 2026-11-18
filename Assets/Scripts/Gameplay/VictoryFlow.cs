using System;
using System.Collections;
using AscendJigoku.Settings;
using AscendJigoku.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AscendJigoku.Gameplay
{
    // Pillar I summit ending: a short triumph, the reveal of six taller pillars, then the demo end card.
    // Starts after the second boss when the player reaches its checkpoint; VictoryFlow.Play() triggers it by hand.
    public sealed class VictoryFlow : MonoBehaviour
    {
        private const string MenuScene = "MainMenu";
        private static VictoryFlow current;
        private static float playSeconds;
        private static int deaths;
        public static bool IsPlaying => current != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStats()
        {
            current = null;
            playSeconds = 0;
            deaths = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Watch(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Returning to the main menu starts a new run's stats.
            if (scene.name == MenuScene)
            {
                playSeconds = 0;
                deaths = 0;
            }

            Watch(scene);
        }

        // Adds one VictoryWatcher to scenes that have a level generator (the real run), so Play() fires on the summit.
        private static void Watch(Scene scene)
        {
            if (!scene.IsValid() || FindAnyObjectByType<VictoryWatcher>() != null) return;
            var generator = FindAnyObjectByType<VerticalLevelGenerator>();
            if (generator == null || generator.gameObject.scene != scene) return;
            var go = new GameObject("Victory Watcher");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<VictoryWatcher>();
        }

        // Starts the ending for the current player. Ignored while it is already running or the player is dead.
        public static bool Play()
        {
            if (current != null) return false;
            var health = FindAnyObjectByType<PlayerHealth>();
            if (health == null || health.CurrentHealth <= 0) return false;
            var go = new GameObject("Victory Flow");
            SceneManager.MoveGameObjectToScene(go, health.gameObject.scene);
            go.AddComponent<VictoryFlow>();
            return current != null;
        }

        // Counts time and deaths for the end card and starts the ending on the summit.
        private sealed class VictoryWatcher : MonoBehaviour
        {
            private PlayerHealth health;
            private Rigidbody2D body;
            private Collider2D shape;
            private bool wasDead;

            private void Update()
            {
                var generator = VerticalLevelGenerator.Active;
                if (generator == null || generator.gameObject.scene != gameObject.scene) return;
                if (health == null)
                {
                    health = FindAnyObjectByType<PlayerHealth>();
                    if (health == null) return;
                    body = health.GetComponent<Rigidbody2D>();
                    shape = health.GetComponent<Collider2D>();
                    wasDead = false;
                }
                bool dead = health.CurrentHealth <= 0;
                if (dead && !wasDead) deaths++;
                wasDead = dead;
                if (current != null) return;
                if (!PauseMenu.IsPaused) playSeconds += Time.unscaledDeltaTime;
                if (dead || body == null || shape == null || generator.Metrics.LastRow <= 0) return;
                if (!generator.CompletionReady || !generator.IsSafeCheckpointFor(health)) return;

                // Triggers when the player stands still on the final checkpoint landing.
                var landing = generator.GetLanding(generator.CompletionCheckpointRow);
                Vector3 top = generator.transform.position + (Vector3)landing.Center;
                var bounds = shape.bounds;
                if (Mathf.Abs(bounds.min.y - top.y) < 0.4f && Mathf.Abs(bounds.center.x - top.x) <= landing.Width * 0.5f
                    && Mathf.Abs(body.linearVelocity.y) < 0.5f) Play();
            }
        }

        private PlayerHealth player;
        private PlayerControlLock controlLock;
        private CameraFollow follow;
        private Camera view;
        private Behaviour pixelPerfect;
        private Canvas hud;
        private float startSize;
        private Vector3 startPosition;
        private VictoryStage stage;
        private RectTransform canvas;
        private Image fade;
        private Text title, kicker, line, byline;
        private Text[] numerals = new Text[6];
        private Button firstButton;
        private bool ended, safetyEntered, leaving;
        private CursorLockMode oldCursorLock;
        private bool oldCursorVisible;
        private readonly System.Collections.Generic.List<Canvas> hiddenCanvases = new System.Collections.Generic.List<Canvas>();
        private Rigidbody2D playerBody;
        private bool wasSimulated, generatorWasEnabled;
        private VerticalLevelGenerator level;
        private float gameplayScale = 1;
        public bool EndCardVisible => ended;

        private void Awake()
        {
            if (current != null)
            {
                Destroy(gameObject);
                return;
            }

            current = this;
        }

        private void Start()
        {
            player = FindAnyObjectByType<PlayerHealth>();
            view = Camera.main;
            if (player == null || view == null)
            {
                Destroy(gameObject);
                return;
            }
            controlLock = player.GetComponent<PlayerControlLock>();
            if (controlLock == null) controlLock = player.gameObject.AddComponent<PlayerControlLock>();
            follow = view.GetComponent<CameraFollow>();
            player.GetComponent<ShopMenu>()?.Close();
            controlLock.Lock();
            gameplayScale = GameSettings.GameplayTimeScale;
            safetyEntered = true; // from here on OnDestroy must Restore() the game state
            oldCursorLock = Cursor.lockState;
            oldCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            playerBody = player.GetComponent<Rigidbody2D>();
            if (playerBody != null)
            {
                wasSimulated = playerBody.simulated;
                playerBody.linearVelocity = Vector2.zero;
                playerBody.simulated = false;
            }

            level = VerticalLevelGenerator.Active;
            if (level != null)
            {
                generatorWasEnabled = level.enabled;
                level.enabled = false;
            }
            CheckpointSanctuary.ClearHostiles(player.gameObject.scene, default, true);
            CombatResolver.ClearSceneCombat();
            foreach (var source in FindObjectsByType<AudioSource>()) source.Stop();
            Time.timeScale = 0; // The cinematic uses unscaled time; the live game must not keep running underneath it.
            if (follow != null) follow.Hold = true;
            // The camera zoom-out would fight a pixel-perfect camera, so switch it off until Restore().
            foreach (var b in view.GetComponents<Behaviour>())
                if (b.GetType().Name == "PixelPerfectCamera" && b.enabled)
                {
                    pixelPerfect = b;
                    b.enabled = false;
                }

            startSize = view.orthographicSize;
            startPosition = view.transform.position;
            hud = FindAnyObjectByType<GameplayHud>()?.GetComponentInChildren<Canvas>(true);
            // Hide every other overlay canvas in this scene (HUD etc.); Restore() turns them back on.
            foreach (var overlay in FindObjectsByType<Canvas>())
                if (overlay.gameObject.scene == player.gameObject.scene && overlay.enabled)
                {
                    hiddenCanvases.Add(overlay);
                    overlay.enabled = false;
                }
            BuildUi(); // Retain checkpoint progress until the end card is actually reached.
            StartCoroutine(Sequence());
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
            stage?.Destroy();
            if (safetyEntered) Restore();
        }

        // Undoes everything Start() changed (time scale, cursor, camera, hidden canvases, player physics and input).
        private void Restore()
        {
            Time.timeScale = leaving ? 1 : gameplayScale;
            Cursor.lockState = leaving ? CursorLockMode.None : oldCursorLock;
            Cursor.visible = leaving || oldCursorVisible;
            foreach (var overlay in hiddenCanvases)
                if (overlay != null) overlay.enabled = true;
            if (playerBody != null) playerBody.simulated = wasSimulated;
            if (level != null) level.enabled = generatorWasEnabled;
            if (view != null)
            {
                view.orthographicSize = startSize;
                view.transform.position = startPosition;
            }
            if (pixelPerfect != null) pixelPerfect.enabled = true;
            if (hud != null) hud.enabled = true;
            if (follow != null) follow.Hold = false;
            if (controlLock != null) controlLock.Unlock();
        }

        private void BuildUi()
        {
            canvas = MenuStyle.CreateCanvas("Victory Canvas", 180, transform);
            fade = MenuStyle.FullScreen(canvas, "Fade", Color.clear, false);
            MenuStyle.Embers(canvas);
            var holder = MenuStyle.Group(canvas, "Text");
            kicker = MenuStyle.Label(holder, "Kicker", "", new Vector2(0, 190), new Vector2(1000, 40), 30, color: MenuStyle.Ember);
            kicker.font = MenuStyle.TitleFont;
            title = MenuStyle.Title(holder, "", 130, 72);
            line = MenuStyle.Label(holder, "Narrator", "", new Vector2(0, -250), new Vector2(1000, 70), 36, color: MenuStyle.TextColor);
            byline = MenuStyle.Label(holder, "Byline", "", new Vector2(0, -296), new Vector2(1000, 30), 22, color: MenuStyle.MutedText);
            for (int i = 0; i < numerals.Length; i++)
            {
                numerals[i] = MenuStyle.Label(holder, "Numeral", Roman(i + 2), Vector2.zero, new Vector2(200, 60), 44, color: MenuStyle.Ember);
                numerals[i].font = MenuStyle.TitleFont;
                Alpha(numerals[i], 0);
            }
        }

        private static string Roman(int n) => new[] { "", "I", "II", "III", "IV", "V", "VI", "VII" }[n];

        private static float Now => Time.unscaledTime;

        private static bool SkipPressed()
        {
            var k = Keyboard.current;
            var m = Mouse.current;
            var g = Gamepad.current;
            return (k != null && k.anyKey.wasPressedThisFrame) || (m != null && m.leftButton.wasPressedThisFrame)
                || (g != null && (g.buttonSouth.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame || g.startButton.wasPressedThisFrame));
        }

        // Runs step(0..1) over seconds; a skippable step may be cut short by a button press after skipAfter seconds.
        private IEnumerator Run(float seconds, Action<float> step, float skipAfter = -1)
        {
            float t = 0;
            while (t < seconds)
            {
                if (player == null || player.CurrentHealth <= 0)
                {
                    Destroy(gameObject);
                    yield break;
                }
                if (!PauseMenu.IsPaused) t += Time.unscaledDeltaTime;
                step(Mathf.Clamp01(t / seconds));
                yield return null;
                if (skipAfter >= 0 && t > skipAfter && SkipPressed()) break;
            }
            step(1);
        }

        private static float Smooth(float x) => x * x * (3 - 2 * x);

        private static void Alpha(Graphic g, float a)
        {
            var c = g.color;
            c.a = a;
            g.color = c;
        }

        // The whole cinematic: triumph title, camera pull-back revealing six more pillars, then the demo end card.
        private IEnumerator Sequence()
        {
            var generator = VerticalLevelGenerator.Active;
            Vector3 feet = new Vector3(player.transform.position.x, player.GetComponent<Collider2D>().bounds.min.y, 0);
            Vector3 summit = generator != null
                ? generator.transform.position + (Vector3)generator.GetLanding(generator.CompletionCheckpointRow).Center
                : feet;
            stage = new VictoryStage(summit);
            var prefab = generator != null && generator.Settings != null ? generator.Settings.checkpointShopPrefab : null;
            var anim = prefab != null ? prefab.GetComponentInChildren<ShopkeeperAnimation>(true) : null;
            if (anim != null)
            {
                float side = player.transform.position.x > summit.x ? -1 : 1;
                stage.AddSkeleton(new Vector3(player.transform.position.x + 3.2f * side, summit.y + 0.05f, 0), anim.frames, anim.transform.lossyScale);
                if (side < 0) stage.Skeleton.flipX = !anim.GetComponent<SpriteRenderer>().flipX;
                else stage.Skeleton.flipX = anim.GetComponent<SpriteRenderer>().flipX;
            }
            bool still = GameSettings.ReduceFlashing; // skip the title zoom and pillar sway
            float born = Now;

            // 1. The brief triumph.
            kicker.text = "THE CINDER PIT BELOW YOU";
            title.text = "PILLAR I CLEARED";
            yield return Run(5.5f, p =>
            {
                float rise = Smooth(Mathf.Clamp01(p * 5));
                Alpha(title, Mathf.Clamp01(p * 6) * Mathf.Clamp01((1 - p) * 6));
                title.rectTransform.localScale = Vector3.one * (still ? 1 : 1.12f - 0.12f * rise);
                Alpha(kicker, Mathf.Clamp01((p - 0.1f) * 6) * Mathf.Clamp01((1 - p) * 6));
                stage.Animate(Now - born, Smooth(Mathf.Clamp01((p - 0.1f) * 5)) * Mathf.Clamp01((1 - p) * 8));
                Alpha(fade, p * 0.25f);
            }, 2.5f);
            kicker.text = ""; title.text = "";

            // 2. The fake-out: pull back and up to six taller pillars.
            Vector3 from = view.transform.position;
            float fromSize = view.orthographicSize;
            Vector3 to = new Vector3(summit.x, summit.y + 32, from.z);
            float toSize = 38;
            string[] lines = { "That was the easy one, chum.", "Six more. They get a bit taller." };
            float revealStart = Now;
            kicker.text = "PILLAR I OF VII";
            yield return Run(13f, p =>
            {
                float move = Smooth(Mathf.Clamp01(p / 0.55f));
                view.transform.position = Vector3.Lerp(from, to, move);
                view.orthographicSize = Mathf.Lerp(fromSize, toSize, move);
                float shown = Smooth(Mathf.Clamp01((p - 0.12f) / 0.4f));
                stage.SetPillars(shown, still ? 0 : Mathf.Sin((Now - revealStart) * 2f));
                stage.Animate(Now - born, Mathf.Clamp01(1 - move * 2));
                Alpha(fade, 0.25f + 0.35f * move);
                Alpha(kicker, Mathf.Clamp01((p - 0.5f) * 6));
                for (int i = 0; i < numerals.Length; i++)
                {
                    var vp = view.WorldToViewportPoint(stage.Labels[i]);
                    var rect = numerals[i].rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(vp.x, vp.y);
                    rect.anchoredPosition = Vector2.zero;
                    Alpha(numerals[i], Mathf.Clamp01(shown * 6 - i * 0.8f) * (vp.y < 0.97f ? 1 : 0));
                }
                // Narrator lines type out one after another (about 11 characters per 3 seconds of local progress).
                string text = p < 0.48f ? "" : p < 0.74f ? lines[0] : lines[1];
                float local = p < 0.74f ? (p - 0.48f) / 0.26f : (p - 0.74f) / 0.26f;
                int count = Mathf.Min(text.Length, Mathf.FloorToInt(local * 11 * text.Length / 3f));
                line.text = text.Substring(0, count);
                byline.text = text.Length > 0 ? "MEMENTO MORI" : "";
                Alpha(line, p > 0.93f ? (1 - p) * 14 : 1);
                Alpha(byline, line.color.a);
            }, 5f);
            line.text = "";
            byline.text = "";
            kicker.text = "";
            foreach (var n in numerals) n.text = "";

            // 3. End card.
            var card = MenuStyle.Group(canvas, "End Card");
            var backdrop = MenuStyle.FullScreen(card, "Backdrop",
                new Color(MenuStyle.Background.r, MenuStyle.Background.g, MenuStyle.Background.b, 0), false);
            card.SetSiblingIndex(1);
            yield return Run(1.2f, p => Alpha(backdrop, p));
            Alpha(fade, 0);
            stage.Destroy();
            if (follow != null) follow.Hold = true;
            var content = MenuStyle.Group(canvas, "End Card Content");
            MenuStyle.CornerTitle(content, "PILLAR I OF VII CLEARED", "THANK YOU FOR\nPLAYING THE DEMO", 64);
            int seconds = Mathf.FloorToInt(playSeconds);
            // h:mm:ss once over an hour, otherwise m:ss.
            string time = seconds >= 3600 ? $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}" : $"{seconds / 60}:{seconds % 60:00}";
            Stat(content, "TIME", time, -330);
            Stat(content, "DEATHS", deaths.ToString(), -368);
            var options = MenuStyle.LeftColumn(content, 2 * 56);
            string[] labels = { "MAIN MENU", "QUIT" };
            Action[] actions = { ToMenu, MainMenu.QuitApplication };
            for (int i = 0; i < labels.Length; i++)
            {
                var button = MenuStyle.Button(options, labels[i], new Vector2(0, 28 - i * 56), actions[i], alignment: TextAnchor.MiddleLeft);
                if (i == 0) firstButton = button;
            }
            ended = true;
            RunSave.Delete(); // A completed run must not reappear through the backup slot.
            MenuStyle.SelectForController(firstButton);
            var group = content.gameObject.AddComponent<CanvasGroup>();
            yield return Run(1f, p => group.alpha = p);
            MenuStyle.SelectForController(firstButton);
        }

        // One "LABEL    value" row on the end card, pinned to the top-left corner.
        private static void Stat(Transform parent, string label, string value, float y)
        {
            var a = MenuStyle.Label(parent, label, label, new Vector2(100, y), new Vector2(200, 32), 24, TextAnchor.MiddleLeft, MenuStyle.MutedText);
            var b = MenuStyle.Label(parent, label + " Value", value, new Vector2(300, y), new Vector2(300, 32), 28, TextAnchor.MiddleLeft, MenuStyle.TitleColor);
            foreach (var t in new[] { a, b })
            {
                var r = t.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
                r.anchoredPosition = new Vector2(t == a ? 100 : 260, y);
            }
        }

        private void ToMenu()
        {
            if (!Application.CanStreamedLevelBeLoaded(MenuScene))
            {
                Debug.LogError("Add " + MenuScene + " to the build scene list.");
                return;
            }
            leaving = true;
            Time.timeScale = 1;
            RunSave.Delete();
            SceneManager.LoadScene(MenuScene);
        }
    }
}
