using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AscendJigoku.UI
{
    // Per-run extras in gameplay scenes: checkpoint toast, optional run timer, and the death count / status line
    // the pause menu shows. Totals live in statics so they survive the scene reload on retry.
    public sealed class QolRunHud : MonoBehaviour
    {
        public static int Deaths { get; private set; }
        public static float Seconds { get; private set; }
        private static int seed;
        private static bool hasSeed;

        private VerticalLevelGenerator generator;
        private PlayerHealth health;
        private Text timer;
        private int lastCheckpoint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachTo(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachTo(scene);

        private static void AttachTo(Scene scene)
        {
            if (!scene.IsValid()) return;
            var gen = FindAnyObjectByType<VerticalLevelGenerator>();
            if (gen == null || gen.gameObject.scene != scene)
            {
                // Returning to the main menu ends the run, so its totals reset.
                if (scene.name == "MainMenu")
                {
                    Deaths = 0;
                    Seconds = 0;
                    hasSeed = false;
                }
                return;
            }
            if (FindAnyObjectByType<QolRunHud>() != null) return;
            var go = new GameObject("Run Hud");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<QolRunHud>();
        }

        public static void NoteDeath() => Deaths++;

        // "ASHEN STAIR  |  Height 412  |  Deaths 3"
        public static string StatusLine()
        {
            var gen = VerticalLevelGenerator.Active;
            var player = gen != null ? gen.Player : null;
            string text = "Deaths " + Deaths;
            if (player == null) return text;
            float height = Mathf.Max(0, player.transform.position.y - gen.transform.position.y);
            var env = PillarEnvironment.Active;
            string zone = env != null && env.zones != null && env.zones.Length > 0
                ? env.zones[Mathf.Clamp(PillarEnvironment.ZoneAt(height), 0, env.zones.Length - 1)].title : "";
            return (string.IsNullOrEmpty(zone) ? "" : zone + "   |   ") + "Height " + Mathf.RoundToInt(height) + "   |   " + text;
        }

        // m:ss.t, or h:mm:ss.t from one hour up.
        public static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds), tenths = Mathf.FloorToInt((seconds - total) * 10);
            return total >= 3600
                ? $"{total / 3600}:{total / 60 % 60:00}:{total % 60:00}.{tenths}"
                : $"{total / 60}:{total % 60:00}.{tenths}";
        }

        private void Start()
        {
            generator = FindAnyObjectByType<VerticalLevelGenerator>();
            health = FindAnyObjectByType<PlayerHealth>();
            if (generator == null)
            {
                Destroy(gameObject);
                return;
            }
            // A different seed means a new run, so the death count and timer start over.
            if (!hasSeed || seed != generator.CurrentSeed)
            {
                Deaths = 0;
                Seconds = 0;
                seed = generator.CurrentSeed;
                hasSeed = true;
            }
            lastCheckpoint = generator.CheckpointRow;
            var canvas = MenuStyle.CreateCanvas("Run Canvas", 6, transform);
            timer = MenuStyle.Label(
                canvas, "Timer", "", Vector2.zero, new Vector2(300, 32), 24, TextAnchor.MiddleRight, MenuStyle.MutedText);
            var rect = timer.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-44, -72); // under the HUD coin count
        }

        // Shows a toast on each new checkpoint and counts run time while the player is alive and the game isn't paused.
        private void Update()
        {
            if (generator == null) return;
            if (generator.CheckpointRow > lastCheckpoint) QolToast.Show("CHECKPOINT");
            lastCheckpoint = generator.CheckpointRow;
            bool alive = health != null && health.CurrentHealth > 0;
            if (alive && Time.timeScale > 0) Seconds += Time.unscaledDeltaTime;
            bool show = Settings.GameSettings.ShowTimer;
            if (timer.enabled != show) timer.enabled = show;
            if (show) timer.text = FormatTime(Seconds);
        }
    }
}
