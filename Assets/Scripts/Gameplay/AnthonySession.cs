using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AscendJigoku.Gameplay
{
    // This entire flow is attached only to AnthonyScene. Main scenes and menu artwork are untouched.
    [DefaultExecutionOrder(-600)]
    public sealed class AnthonySession : MonoBehaviour
    {
        public enum ScreenMode { Title, Playing, Pause, Dead, Victory }
        public PlayerHealth player;
        public FireWormBoss boss;
        private PlayerControlLock controlLock;
        private ActiveInputPrompts prompts;
        private InputAction pause;
        private GameObject modal;
        private Text title, body, hint, healthLabel, goalLabel, bossLabel, controlsLabel;
        private readonly Button[] buttons = new Button[3];
        private int lastBackFrame = -1;
        private bool leaving;
        public ScreenMode Mode { get; private set; } = ScreenMode.Title;
        public bool GameplayActive => Mode == ScreenMode.Playing;
        public ActiveInputPrompts Prompts => prompts;
        public Button FirstButton => buttons[0];
        public string HealthText => healthLabel != null ? healthLabel.text : "";
        private void Awake()
        {
            prompts = gameObject.AddComponent<ActiveInputPrompts>();
            controlLock = player.GetComponent<PlayerControlLock>();
            pause = new InputAction("PauseTestScene", InputActionType.Button, "<Keyboard>/escape"); pause.AddBinding("<Gamepad>/start");
            CreateUI();
        }
        private void OnEnable() => pause.Enable();
        private void OnDisable() { pause.Disable(); Time.timeScale = 1; }
        private void OnDestroy() { pause.Dispose(); if (player != null) player.HealthChanged -= UpdateHealth; if (boss != null) boss.GetComponent<EnemyHealth>().HealthChanged -= OnBossHealth; }
        private void Start()
        {
            player.HealthChanged += UpdateHealth;
            UpdateHealth(player.CurrentHealth, player.MaxHealth); goalLabel.text = "Visit the checkpoint merchant, then cross right to fight the Fire Worm.";
            if (boss != null) boss.GetComponent<EnemyHealth>().HealthChanged += OnBossHealth;
            ShowTitle();
        }
        private void Update()
        {
            controlsLabel.text = prompts.Controls;
            var shop = player.GetComponent<ShopMenu>();
            if (pause.WasPressedThisFrame() && lastBackFrame != Time.frameCount && (shop == null || !shop.ConsumesBackInput)) Back();
            if (GameplayActive && boss != null)
            {
                var hp = boss.GetComponent<EnemyHealth>();
                bossLabel.text = Vector2.Distance(player.transform.position, boss.transform.position) < 22
                    ? $"FIRE WORM   {hp.CurrentHealth}/{hp.MaxHealth}   Phase {boss.HealthPhase}/3\n{BossTell()}" : "";
            }
            else bossLabel.text = "";
            if (modal.activeSelf) hint.text = $"{prompts.Confirm}: select    {prompts.Back}: back";
        }
        private string BossTell() => boss.State == FireWormState.SlamWindup ? "SLAM - leave the marked ground, or parry / pogo the impact"
            : boss.State == FireWormState.VolleyWindup ? "FAN VOLLEY - jump the spread, parry, or pogo a fireball"
            : boss.State == FireWormState.FireballWindup ? "AIMED FIREBALL - dodge, parry, or pogo"
            : boss.State == FireWormState.Transition ? "PHASE CHANGE - recovery opportunity"
            : boss.State == FireWormState.Recovery || boss.State == FireWormState.Stunned ? "OPENING - strike now" : "";
        private void OnBossHealth(int health, int maximum) { if (health <= 0) StartCoroutine(VictoryAfterDeathAnimation()); }
        private System.Collections.IEnumerator VictoryAfterDeathAnimation()
        { yield return new WaitForSeconds(1.15f); ShowVictory(); }
        private void UpdateHealth(int current, int max) => healthLabel.text = $"HEALTH  {current} / {max}";
        private void PauseWorld(ScreenMode mode)
        {
            Mode = mode; controlLock.Lock(); Time.timeScale = 0;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        }
        public void ShowTitle()
        {
            PauseWorld(ScreenMode.Title);
            ShowMenu("ANTHONY'S PLAYGROUND", "Visit the merchant on the blue checkpoint to try upgrades.\nE / Y opens the shop. Test coins and upgrades reset with the scene.\n\nCross the gap to fight the Fire Worm.\nThree phases: aimed fireballs, fan volleys and slam shockwaves.",
                new[] { "START", "QUIT" }, new Action[] { StartPlaying, AscendJigoku.UI.MainMenu.QuitApplication });
        }
        public void StartPlaying()
        { if (Mode != ScreenMode.Title && Mode != ScreenMode.Pause) return; Mode = ScreenMode.Playing; modal.SetActive(false); Time.timeScale = 1; controlLock.Unlock(); }
        public void ShowPause()
        {
            if (!GameplayActive) return;
            PauseWorld(ScreenMode.Pause);
            ShowMenu("PAUSED", "Take a breath. Your run is preserved.", new[] { "RESUME", "NEW RUN", "QUIT" },
                new Action[] { StartPlaying, Reload, AscendJigoku.UI.MainMenu.QuitApplication });
        }
        public void ShowDeath()
        {
            if (Mode == ScreenMode.Dead || Mode == ScreenMode.Victory) return;
            PauseWorld(ScreenMode.Dead);
            ShowMenu("YOU DIED", "Retry starts a fresh test run, with full health and a restored boss.", new[] { "RETRY", "GIVE UP" },
                new Action[] { Reload, AscendJigoku.UI.MainMenu.QuitApplication });
            title.color = new Color(1, 0.2f, 0.15f);
        }
        public void ShowVictory()
        {
            if (Mode == ScreenMode.Victory || Mode == ScreenMode.Dead) return;
            PauseWorld(ScreenMode.Victory);
            ShowMenu("VICTORY", "The Fire Worm is defeated. Try a new run to test different upgrades.", new[] { "NEW RUN", "QUIT" },
                new Action[] { Reload, AscendJigoku.UI.MainMenu.QuitApplication });
        }
        public void Back()
        {
            if (lastBackFrame == Time.frameCount) return;
            lastBackFrame = Time.frameCount;
            if (Mode == ScreenMode.Pause) { StartPlaying(); return; }
            if (GameplayActive) { ShowPause(); return; }
            // Root menus cannot silently resume a dead/finished run. Back selects their safe first action.
            SelectFirst();
        }
        public void Reload()
        {
            if (leaving) return; leaving = true; Time.timeScale = 1;
            string path = gameObject.scene.path;
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(path, LoadSceneMode.Single);
#endif
        }
        private void SelectFirst()
        { if (EventSystem.current != null) { EventSystem.current.SetSelectedGameObject(null); EventSystem.current.SetSelectedGameObject(buttons[0].gameObject); } }
        private void ShowMenu(string heading, string content, string[] labels, Action[] actions)
        {
            modal.SetActive(true); title.text = heading; title.color = Color.white; body.text = content;
            for (int i = 0; i < buttons.Length; i++)
            {
                bool active = i < labels.Length; buttons[i].gameObject.SetActive(active); buttons[i].onClick.RemoveAllListeners();
                if (!active) continue;
                int slot = i;
                buttons[i].GetComponentInChildren<Text>().text = labels[i];
                buttons[i].onClick.AddListener(() => actions[slot]());
                var nav = buttons[i].navigation; nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = buttons[(i + labels.Length - 1) % labels.Length]; nav.selectOnDown = buttons[(i + 1) % labels.Length]; buttons[i].navigation = nav;
            }
            SelectFirst();
        }
        private void CreateUI()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var root = new GameObject("Anthony Runtime UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 0.5f;
            healthLabel = Label(root.transform, "Health", new Vector2(0, 1), new Vector2(25, -25), new Vector2(300, 45), 30, TextAnchor.UpperLeft);
            goalLabel = Label(root.transform, "Objective", new Vector2(0, 1), new Vector2(25, -80), new Vector2(650, 60), 23, TextAnchor.UpperLeft);
            bossLabel = Label(root.transform, "Boss status", new Vector2(0.5f, 1), new Vector2(0, -25), new Vector2(850, 110), 26, TextAnchor.UpperCenter);
            controlsLabel = Label(root.transform, "Controls", new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(1530, 65), 20, TextAnchor.LowerCenter);
            modal = new GameObject("Menu", typeof(RectTransform), typeof(Image)); modal.transform.SetParent(root.transform, false);
            var mr = (RectTransform)modal.transform; mr.anchorMin = Vector2.zero; mr.anchorMax = Vector2.one; mr.offsetMin = mr.offsetMax = Vector2.zero;
            modal.GetComponent<Image>().color = new Color(0.025f, 0.025f, 0.035f, 0.96f);
            title = Label(modal.transform, "Title", new Vector2(0.5f, 0.5f), new Vector2(0, 290), new Vector2(1320, 90), 52, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            body = Label(modal.transform, "Body", new Vector2(0.5f, 0.5f), new Vector2(0, 105), new Vector2(1320, 230), 28, TextAnchor.MiddleCenter);
            for (int i = 0; i < buttons.Length; i++)
            {
                var go = new GameObject("Option " + i, typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuBackHandler));
                go.transform.SetParent(modal.transform, false); Place((RectTransform)go.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -90 - i * 82), new Vector2(500, 66));
                go.GetComponent<Image>().color = new Color(0.19f, 0.2f, 0.24f);
                buttons[i] = go.GetComponent<Button>();
                var colors = buttons[i].colors; colors.selectedColor = new Color(0.55f, 0.7f, 1); colors.highlightedColor = colors.selectedColor; buttons[i].colors = colors;
                go.GetComponent<MenuBackHandler>().session = this;
                Label(go.transform, "Text", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480, 60), 29, TextAnchor.MiddleCenter);
            }
            hint = Label(modal.transform, "Menu controls", new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(1200, 55), 23, TextAnchor.LowerCenter);
        }
        private static Text Label(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, anchor, position, size);
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize; text.color = Color.white; text.alignment = alignment; text.raycastTarget = false;
            return text;
        }
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
        }
    }
}
