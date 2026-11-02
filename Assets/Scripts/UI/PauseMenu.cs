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
        private Button firstButton;
        private bool paused, leaving;
        private CursorLockMode cursorLock;
        private bool cursorVisible;

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

        private void OnEnable() { toggle.Enable(); back.Enable(); }
        private void OnDisable() { toggle.Disable(); back.Disable(); }

        private void OnDestroy()
        {
            if (paused && !leaving) Time.timeScale = GameSettings.GameplayTimeScale;
            if (current == this) current = null;
            toggle.Dispose(); back.Dispose();
        }

        private void Update()
        {
            if (leaving || canvas == null) return;
            if (!paused)
            {
                if (toggle.WasPressedThisFrame() && CanPause()) Pause();
                return;
            }
            if (SettingsMenu.IsOpen || SettingsMenu.LastBackFrame == Time.frameCount) return;
            if (toggle.WasPressedThisFrame() || back.WasPressedThisFrame()) Resume();
        }

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
            cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            canvas.gameObject.SetActive(true);
            MenuStyle.SelectForController(firstButton);
        }

        public void Resume()
        {
            if (!paused) return;
            paused = false;
            canvas.gameObject.SetActive(false);
            Time.timeScale = GameSettings.GameplayTimeScale;
            Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
            controlLock?.Unlock();
        }

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

        private void ReturnToMainMenu()
        {
            if (leaving) return;
            if (!Application.CanStreamedLevelBeLoaded(mainMenuScene)) { Debug.LogError("Add " + mainMenuScene + " to the build scene list."); return; }
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

        private void Build()
        {
            canvas = MenuStyle.CreateCanvas("Pause Canvas", 150, transform);
            MenuStyle.FullScreen(canvas, "Backdrop", MenuStyle.Overlay);
            MenuStyle.Title(canvas, "PAUSED", 145);
            firstButton = MenuStyle.Button(canvas, "RESUME", new Vector2(0, 25), Resume);
            MenuStyle.Button(canvas, "SETTINGS", new Vector2(0, -55), OpenSettings);
            MenuStyle.Button(canvas, "MAIN MENU", new Vector2(0, -135), ReturnToMainMenu);
            MenuStyle.Button(canvas, "QUIT", new Vector2(0, -215), Quit);
        }
    }
}
