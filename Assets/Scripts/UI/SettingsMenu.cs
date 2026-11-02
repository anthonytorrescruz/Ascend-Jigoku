using System;
using System.Collections.Generic;
using AscendJigoku.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AscendJigoku.UI
{
    // Runtime-built settings screen shared by the main menu and the pause menu.
    // Works while Time.timeScale is 0. Escape / B / Start goes back; Escape cancels a key being rebound.
    public sealed class SettingsMenu : MonoBehaviour
    {
        private enum Page { Audio, Controls, Accessibility }

        private static SettingsMenu current;
        public static bool IsOpen => current != null;
        // Frame on which the menu consumed a back press, so the caller does not also treat it as "back".
        public static int LastBackFrame { get; private set; } = -10;

        private Action onClosed;
        private InputAction back;
        private RectTransform canvas, content;
        private Page page;
        private Text status;
        private InputActionRebindingExtensions.RebindingOperation rebinding;
        private int rebindEndedFrame = -10;
        private readonly List<KeyValuePair<Button, KeybindSettings.Entry>> keyButtons = new List<KeyValuePair<Button, KeybindSettings.Entry>>();
        private int sortingOrder;

        public static SettingsMenu Open(Action onClosed, int sortingOrder = 200)
        {
            if (current != null) return current;
            var menu = new GameObject("Settings Menu").AddComponent<SettingsMenu>();
            menu.onClosed = onClosed;
            menu.sortingOrder = sortingOrder;
            menu.Build();
            return menu;
        }

        private void Awake()
        {
            current = this;
            back = new InputAction("Settings Back", InputActionType.Button, "<Keyboard>/escape");
            back.AddBinding("<Gamepad>/buttonEast");
            back.AddBinding("<Gamepad>/start");
            back.Enable();
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
            CancelRebind();
            back?.Dispose();
        }

        private void Update()
        {
            if (rebinding != null)
            {
                // Pads cannot rebind keyboard keys; let B back out of listening.
                var pad = Gamepad.current;
                if (pad != null && pad.buttonEast.wasPressedThisFrame) CancelRebind();
                return;
            }
            if (Time.frameCount - rebindEndedFrame <= 1) return;
            if (back.WasPressedThisFrame()) Close();
        }

        public void Close()
        {
            CancelRebind();
            GameSettings.Save();
            LastBackFrame = Time.frameCount;
            var callback = onClosed; onClosed = null;
            Destroy(gameObject);
            callback?.Invoke();
        }

        private void Build()
        {
            if (canvas != null) Destroy(canvas.gameObject);
            keyButtons.Clear();
            canvas = MenuStyle.CreateCanvas("Settings Canvas", sortingOrder, transform);
            MenuStyle.FullScreen(canvas, "Backdrop", MenuStyle.Background);
            MenuStyle.Title(canvas, "SETTINGS", 300, 52);

            string[] names = { "AUDIO", "CONTROLS", "ACCESSIBILITY" };
            Button firstTab = null;
            for (int i = 0; i < names.Length; i++)
            {
                var target = (Page)i;
                var tab = MenuStyle.Button(canvas, names[i], new Vector2(-240 + i * 240, 215), () => ShowPage(target), new Vector2(220, 48), 22);
                if (target == page) { var colors = tab.colors; colors.normalColor = MenuStyle.TabActive; tab.colors = colors; }
                if (i == 0) firstTab = tab;
            }

            content = MenuStyle.Group(canvas, "Page");
            status = MenuStyle.Label(canvas, "Status", "", new Vector2(0, -248), new Vector2(1000, 34), 20, TextAnchor.MiddleCenter, MenuStyle.MutedText);
            MenuStyle.Button(canvas, "DEFAULTS", new Vector2(-170, -300), ResetPage, new Vector2(300, 56));
            MenuStyle.Button(canvas, "BACK", new Vector2(170, -300), Close, new Vector2(300, 56));

            if (page == Page.Audio) BuildAudio();
            else if (page == Page.Controls) BuildControls();
            else BuildAccessibility();
            MenuStyle.SelectForController(firstTab);
        }

        private void ShowPage(Page target)
        {
            if (rebinding != null) return;
            page = target;
            Build();
        }

        private void ResetPage()
        {
            if (rebinding != null) return;
            if (page == Page.Controls) { KeybindSettings.ResetToDefaults(); Build(); status.text = "Keys restored to defaults."; }
            else { GameSettings.ResetToDefaults(); GameSettings.Save(); Build(); status.text = "Audio and accessibility restored to defaults."; }
        }

        // ---------- Pages ----------

        private void BuildAudio()
        {
            PercentRow("Master volume", 150, GameSettings.MasterVolume, GameSettings.SetMasterVolume);
            PercentRow("Music", 90, GameSettings.MusicVolume, GameSettings.SetMusicVolume);
            PercentRow("Sound effects", 30, GameSettings.SfxVolume, GameSettings.SetSfxVolume);
        }

        private void BuildControls()
        {
            MenuStyle.Label(content, "Help", "Click an action, then press a key.  Esc cancels.  Arrow keys always move.  Controller layout is fixed.",
                new Vector2(0, 168), new Vector2(1100, 30), 18, TextAnchor.MiddleCenter, MenuStyle.MutedText);
            var entries = KeybindSettings.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float y = 128 - i * 37;
                RowLabel(entry.Label, y, 20);
                Button button = null;
                button = MenuStyle.Button(content, entry.DisplayKey, new Vector2(200, y), () => BeginRebind(button, entry), new Vector2(260, 32), 20);
                keyButtons.Add(new KeyValuePair<Button, KeybindSettings.Entry>(button, entry));
            }
        }

        private void BuildAccessibility()
        {
            Text shakeValue = null;
            RowLabel("Screen shake", 150);
            MenuStyle.Slider(content, new Vector2(200, 150), 300, 0, 10, true, Mathf.Round(GameSettings.ScreenShake * 10),
                v => { GameSettings.SetScreenShake(v / 10f); shakeValue.text = ShakeText(v / 10f); });
            shakeValue = ValueLabel(150, ShakeText(GameSettings.ScreenShake));

            Text speedValue = null;
            RowLabel("Game speed (assist)", 95);
            MenuStyle.Slider(content, new Vector2(200, 95), 300, GameSettings.MinGameSpeed * 10, 10, true, Mathf.Round(GameSettings.GameSpeed * 10),
                v => { GameSettings.SetGameSpeed(v / 10f); speedValue.text = Mathf.RoundToInt(v * 10) + "%"; });
            speedValue = ValueLabel(95, Mathf.RoundToInt(GameSettings.GameSpeed * 100) + "%");

            ToggleRow("Hit-stop (impact freeze)", 40, () => GameSettings.HitStop, GameSettings.SetHitStop);
            ToggleRow("Reduce flashing", -15, () => GameSettings.ReduceFlashing, GameSettings.SetReduceFlashing);
            ToggleRow("Stronger attack warnings", -70, () => GameSettings.StrongTelegraphs, GameSettings.SetStrongTelegraphs);
            ToggleRow("Low-health vignette", -125, () => GameSettings.DamageVignette, GameSettings.SetDamageVignette);
            MenuStyle.Label(content, "Note", "Game speed slows the whole game, including enemies. It applies when you return to play.",
                new Vector2(0, -180), new Vector2(1000, 30), 18, TextAnchor.MiddleCenter, MenuStyle.MutedText);
        }

        private static string ShakeText(float value) => value <= 0 ? "Off" : Mathf.RoundToInt(value * 100) + "%";

        // ---------- Rows ----------

        private void RowLabel(string text, float y, int size = 24)
            => MenuStyle.Label(content, text + " Label", text, new Vector2(-230, y), new Vector2(400, 40), size, TextAnchor.MiddleLeft);

        private Text ValueLabel(float y, string text)
            => MenuStyle.Label(content, "Value", text, new Vector2(410, y), new Vector2(100, 40), 22, TextAnchor.MiddleLeft, MenuStyle.MutedText);

        private void PercentRow(string label, float y, float value, Action<float> set)
        {
            RowLabel(label, y);
            Text valueText = null;
            MenuStyle.Slider(content, new Vector2(200, y), 300, 0, 100, true, Mathf.Round(value * 100),
                v => { set(v / 100f); valueText.text = Mathf.RoundToInt(v) + "%"; });
            valueText = ValueLabel(y, Mathf.RoundToInt(value * 100) + "%");
        }

        private void ToggleRow(string label, float y, Func<bool> get, Action<bool> set)
        {
            RowLabel(label, y);
            Button toggle = null;
            toggle = MenuStyle.Button(content, get() ? "ON" : "OFF", new Vector2(130, y), () =>
            {
                set(!get());
                MenuStyle.SetButtonText(toggle, get() ? "ON" : "OFF");
            }, new Vector2(160, 42), 22);
        }

        // ---------- Rebinding ----------

        private void BeginRebind(Button button, KeybindSettings.Entry entry)
        {
            if (rebinding != null) return;
            // Deselect so the key being captured cannot also "submit" this button.
            if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            MenuStyle.SetButtonText(button, "Press a key...");
            status.text = "Press a key for " + entry.Label + ".  Esc cancels.";
            rebinding = KeybindSettings.StartRebind(entry, message =>
            {
                rebinding = null;
                rebindEndedFrame = Time.frameCount;
                if (status == null) return; // menu closed while listening
                status.text = message;
                RefreshKeyLabels();
                MenuStyle.SelectForController(button);
            });
        }

        private void CancelRebind()
        {
            if (rebinding == null) return;
            var operation = rebinding;
            rebinding = null;
            rebindEndedFrame = Time.frameCount;
            operation.Cancel();
            RefreshKeyLabels();
        }

        private void RefreshKeyLabels()
        {
            foreach (var pair in keyButtons)
                if (pair.Key != null) MenuStyle.SetButtonText(pair.Key, pair.Value.DisplayKey);
        }
    }
}
