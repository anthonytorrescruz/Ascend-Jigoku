using AscendJigoku.Gameplay;
using AscendJigoku.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AscendJigoku.UI
{
    // Esc / Start pauses gameplay. Installed automatically in any scene with a PlayerHealth,
    // except AnthonyScene (AnthonySession has its own test-scene pause). Add it by hand to tune it.
    // Pausing sets Time.timeScale to 0, locks player input and shows a menu built in code (Build). Esc / Start / B resumes.
    [DefaultExecutionOrder(100)]
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private string mainMenuScene = "MainMenu";

        private static PauseMenu current;
        public static bool IsPaused => current != null && current.paused;

        private InputAction toggle, back;
        private PlayerHealth player;
        private PlayerControlLock controlLock;
        private ShopMenu shop;
        private RectTransform canvas;
        private Button firstButton, restartButton;
        private Text statusLine;
        private float restartConfirmUntil;
        private bool paused, leaving;
        private CursorLockMode cursorLock;
        private bool cursorVisible;

        // Hooks scene loads so every gameplay scene gets a pause menu without any scene setup.
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
            if (!scene.IsValid() || FindAnyObjectByType<PauseMenu>() != null || FindAnyObjectByType<AnthonySession>() != null) return;
            var health = FindAnyObjectByType<PlayerHealth>();
            if (health == null || health.gameObject.scene != scene) return;
            var go = new GameObject("Pause Menu");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<PauseMenu>();
        }

        private void Awake()
        {
            current = this;
            toggle = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            toggle.AddBinding("<Gamepad>/start");
            back = new InputAction("Pause Back", InputActionType.Button, "<Gamepad>/buttonEast");
        }

        private void Start()
        {
            player = FindAnyObjectByType<PlayerHealth>();
            if (player != null)
            {
                shop = player.GetComponent<ShopMenu>();
                controlLock = player.GetComponent<PlayerControlLock>();
                if (controlLock == null && player.GetComponent<PlayerController>() != null)
                    controlLock = player.gameObject.AddComponent<PlayerControlLock>();
            }
            Build();
            canvas.gameObject.SetActive(false);
            // Gameplay starts at the player's chosen speed (Settings > Accessibility > Game speed).
            if (Time.timeScale > 0) Time.timeScale = GameSettings.GameplayTimeScale;
        }

        private void OnEnable()
        {
            toggle.Enable();
            back.Enable();
        }

        private void OnDisable()
        {
            toggle.Disable();
            back.Disable();
        }

        private void OnDestroy()
        {
            if (paused && !leaving) Time.timeScale = GameSettings.GameplayTimeScale;
            if (current == this) current = null;
            toggle.Dispose();
            back.Dispose();
        }

        // While paused: expire the restart confirmation, and close on Esc/Start/B unless the settings menu is open or
        // just used that same press to go back (LastBackFrame).
        private void Update()
        {
            if (leaving || canvas == null) return;
            if (!paused)
            {
                if (toggle.WasPressedThisFrame() && CanPause()) Pause();
                return;
            }
            if (restartConfirmUntil > 0 && Time.unscaledTime > restartConfirmUntil) ResetRestart();
            if (SettingsMenu.IsOpen || SettingsMenu.LastBackFrame == Time.frameCount) return;
            if (toggle.WasPressedThisFrame() || back.WasPressedThisFrame()) Resume();
        }

        // Not while dead, in a shop, or when something else (cutscene, dialogue) has locked the player.
        private bool CanPause()
        {
            if (Time.timeScale <= 0 || player == null || player.CurrentHealth <= 0) return false;
            if (shop == null) shop = player.GetComponent<ShopMenu>(); // the generator can add the shop after Start
            if (shop != null && shop.ConsumesBackInput) return false;
            if (controlLock == null) controlLock = player.GetComponent<PlayerControlLock>();
            return controlLock == null || !controlLock.IsLocked;
        }

        public void Pause()
        {
            if (paused) return;
            paused = true;
            Time.timeScale = 0;
            controlLock?.Lock();
            cursorLock = Cursor.lockState;
            cursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            canvas.gameObject.SetActive(true);
            statusLine.text = QolRunHud.StatusLine();
            ResetRestart();
            MenuStyle.SelectForController(firstButton);
        }

        public void Resume()
        {
            if (!paused) return;
            paused = false;
            ResetRestart();
            canvas.gameObject.SetActive(false);
            Time.timeScale = GameSettings.GameplayTimeScale;
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
            controlLock?.Unlock();
        }

        // Hides the pause menu while settings are open; the callback brings it back when settings close.
        private void OpenSettings()
        {
            canvas.gameObject.SetActive(false);
            SettingsMenu.Open(() =>
            {
                if (this == null || !paused) return;
                canvas.gameObject.SetActive(true);
                MenuStyle.SelectForController(firstButton);
            });
        }

        // First press asks to confirm (like NEW RUN); a second press within a few seconds restarts at the last checkpoint.
        private void RestartFromCheckpoint()
        {
            if (leaving) return;
            if (restartConfirmUntil <= 0)
            {
                restartConfirmUntil = Time.unscaledTime + 4;
                MenuStyle.SetButtonText(restartButton, "CONFIRM RESTART?");
                return;
            }
            string scenePath = gameObject.scene.path;
            if (!Application.CanStreamedLevelBeLoaded(scenePath))
            {
                Debug.LogError("Scene is not in the build scene list: " + scenePath);
                ResetRestart();
                return;
            }
            leaving = true;
            Time.timeScale = GameSettings.GameplayTimeScale;
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
            SceneManager.LoadScene(scenePath);
        }

        private void ResetRestart()
        {
            restartConfirmUntil = 0;
            if (restartButton != null) MenuStyle.SetButtonText(restartButton, "RESTART CHECKPOINT");
        }

        private void ReturnToMainMenu()
        {
            if (leaving) return;
            if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
            {
                Debug.LogError("Add " + mainMenuScene + " to the build scene list.");
                return;
            }
            leaving = true;
            Time.timeScale = 1;
            SceneManager.LoadScene(mainMenuScene);
        }

        private void Quit()
        {
            leaving = true;
            Time.timeScale = 1;
            MainMenu.QuitApplication();
        }

        // Builds the (initially hidden) menu once: dark overlay, title and a left-aligned option list. labels[i] and
        // actions[i] must stay in the same order; firstButton gets the initial selection, restartButton changes its text.
        private void Build()
        {
            canvas = MenuStyle.CreateCanvas("Pause Canvas", 150, transform);
            MenuStyle.FullScreen(canvas, "Backdrop", MenuStyle.Overlay);
            MenuStyle.CornerTitle(canvas, "", "PAUSED", 72);
            var options = MenuStyle.LeftColumn(canvas, 5 * 56);
            string[] labels = { "RESUME", "RESTART CHECKPOINT", "SETTINGS", "MAIN MENU", "QUIT" };
            System.Action[] actions = { Resume, RestartFromCheckpoint, OpenSettings, ReturnToMainMenu, Quit };
            for (int i = 0; i < labels.Length; i++)
            {
                var button = MenuStyle.Button(
                    options, labels[i], new Vector2(0, 112 - i * 56), actions[i], alignment: TextAnchor.MiddleLeft);
                if (i == 0) firstButton = button;
                if (i == 1) restartButton = button;
            }
            // Run stats line (QolRunHud.StatusLine), pinned to the bottom-left corner.
            statusLine = MenuStyle.Label(
                canvas, "Status", "", new Vector2(0, 0), new Vector2(900, 28), 18, TextAnchor.MiddleLeft, MenuStyle.MutedText);
            var rect = statusLine.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(60, 40);
        }
    }
}
