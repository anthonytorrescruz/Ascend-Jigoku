using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AscendJigoku.UI
{
    // The menu is authored in its own scene (Death.unity). Whoever kills the player calls Show(level), which remembers
    // the level, time scale and cursor state in statics (only these survive the level unload) and loads the Death scene.
    // Retry (button or R) reloads that level and restores gameplay state; Give Up quits via MainMenu.
    public sealed class DeathScreen : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/Death.unity";

        [Tooltip("Level loaded by Retry when opening the Death scene directly for testing.")]
        [SerializeField] private string fallbackGameplayScene = "ProceduralPillar";
        [SerializeField] private Button retryButton;
        [SerializeField] private Button giveUpButton;
        [SerializeField] private CanvasGroup revealGroup;
        [Min(0), SerializeField] private float revealSeconds = 0.2f;

        private static DeathScreen current;
        private static bool loading, hasRetryContext;
        private static string retryScenePath;
        private static float gameplayTimeScale = 1;
        private static CursorLockMode gameplayCursorLock;
        private static bool gameplayCursorVisible = true;
        private bool leaving;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetContext()
        {
            current = null;
            loading = hasRetryContext = false;
            retryScenePath = null;
            gameplayTimeScale = 1;
            gameplayCursorLock = CursorLockMode.None;
            gameplayCursorVisible = true;
        }

        // Called from gameplay with the scene the player died in.
        public static void Show(Scene level)
        {
            if (loading || current != null) return;
            string origin = string.IsNullOrEmpty(level.path) ? level.name : level.path;
            if (!Application.CanStreamedLevelBeLoaded(ScenePath))
            {
                Debug.LogError("Add " + ScenePath + " to the build scene list.");
                // Recover to gameplay rather than leave the player disabled if the build is misconfigured.
                SceneManager.LoadScene(origin);
                return;
            }
            retryScenePath = origin;
            gameplayTimeScale = Time.timeScale > 0 ? Time.timeScale : 1;
            gameplayCursorLock = Cursor.lockState;
            gameplayCursorVisible = Cursor.visible;
            hasRetryContext = loading = true;
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
        }

        private void Awake()
        {
            current = this;
            loading = false;
            // The level is unloaded, so there is no gameplay simulation to pause behind the menu.
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Pre-selects Retry so keyboard and pad work immediately, fades the buttons in and adds the "R" hint label in code.
        private void Start()
        {
            if (EventSystem.current != null && retryButton != null)
            {
                EventSystem.current.firstSelectedGameObject = retryButton.gameObject;
                EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
            }
            StartCoroutine(Reveal());
            var canvas = retryButton != null ? retryButton.GetComponentInParent<Canvas>() : null;
            if (canvas != null)
            {
                var hint = MenuStyle.Label(
                    canvas.transform, "Retry Hint", "Press R to retry", new Vector2(0, -170), new Vector2(600, 30), 20,
                    TextAnchor.MiddleCenter, MenuStyle.MutedText);
                hint.raycastTarget = false;
            }
        }

        // Quick retry without reaching for the mouse. Short guard so a key still held from play cannot skip the screen.
        private void Update()
        {
            if (leaving || Time.timeSinceLevelLoad < 0.15f) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.rKey.wasPressedThisFrame)) Retry();
        }

        // Fade-in on unscaled time (revealSeconds is in seconds).
        private IEnumerator Reveal()
        {
            if (revealGroup == null) yield break;
            revealGroup.alpha = 0;
            float elapsed = 0;
            while (elapsed < revealSeconds)
            {
                revealGroup.alpha = Mathf.Clamp01(elapsed / revealSeconds);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            revealGroup.alpha = 1;
        }

        // Wired to the Retry button in the scene. Falls back to fallbackGameplayScene when the Death scene was opened directly.
        public void Retry()
        {
            if (leaving) return;
            string target = hasRetryContext ? retryScenePath : fallbackGameplayScene;
            if (!Application.CanStreamedLevelBeLoaded(target))
            {
                Debug.LogError("Retry level is not in the build scene list: " + target);
                return;
            }
            leaving = true;
            RestoreGameplay();
            SceneManager.LoadScene(target, LoadSceneMode.Single);
        }

        public void GiveUp()
        {
            if (leaving) return;
            leaving = true;
            RestoreGameplay();
            MainMenu.QuitApplication();
        }

        // Puts back the time scale and cursor the level had before dying, and forgets the retry context.
        private static void RestoreGameplay()
        {
            Time.timeScale = gameplayTimeScale;
            Cursor.lockState = gameplayCursorLock;
            Cursor.visible = gameplayCursorVisible;
            hasRetryContext = false;
            retryScenePath = null;
            gameplayTimeScale = 1;
            gameplayCursorLock = CursorLockMode.None;
            gameplayCursorVisible = true;
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
        }
    }
}
