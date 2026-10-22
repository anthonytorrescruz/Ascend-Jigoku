using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AscendJigoku.UI
{
    // The menu is authored in its own scene. Only retry context survives the level unload.
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

        private void Start()
        {
            if (EventSystem.current != null && retryButton != null)
            {
                EventSystem.current.firstSelectedGameObject = retryButton.gameObject;
                EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
            }
            StartCoroutine(Reveal());
        }

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
