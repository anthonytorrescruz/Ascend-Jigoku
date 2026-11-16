using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AscendJigoku.UI
{
    // Applies the Ember look to the two hand-authored menu scenes (MainMenu.unity, Death.unity) when they load.
    // The scenes keep their plain blockout objects and button wiring; nothing here is saved into them.
    public static class MenuSceneSkin
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SkinDeathScreen(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SkinDeathScreen(scene);

        // Called by MainMenuExtras once it has added CONTINUE / SETTINGS, with the buttons top to bottom.
        public static void SkinMainMenu(MainMenu menu, IList<Button> buttons)
        {
            var canvas = menu.GetComponentInParent<Canvas>();
            if (canvas == null || IsSkinned(canvas)) return;
            Backdrop(canvas.transform);
            HideSceneTitle(canvas.transform);
            MenuStyle.CornerTitle(canvas.transform, "ASCEND", "JIGOKU", 96);

            var options = MenuStyle.LeftColumn(canvas.transform, buttons.Count * 56);
            for (int i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i];
                button.transform.SetParent(options, false);
                SizeButton(button, MenuStyle.ButtonSize, new Vector2(0, (buttons.Count - 1) * 28 - i * 56));
                MenuStyle.ApplyButton(button, MenuStyle.ButtonTextSize, TextAnchor.MiddleLeft);
            }
            if (buttons.Count > 0) MenuStyle.SelectForController(buttons[0]);
        }

        private static void SkinDeathScreen(Scene scene)
        {
            var death = Object.FindAnyObjectByType<DeathScreen>();
            if (death == null || death.gameObject.scene != scene) return;
            var canvas = death.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null || IsSkinned(canvas)) return;

            Backdrop(canvas.transform);
            var title = FindTitle(canvas.transform);
            if (title != null)
            {
                MenuStyle.StyleTitle(title, 104);
                title.color = MenuStyle.EmberDeep;
                ((RectTransform)title.transform).anchoredPosition = new Vector2(0, 60);
                ((RectTransform)title.transform).sizeDelta = new Vector2(1100, 150);
            }

            Button retry = null, giveUp = null;
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (button.name.Contains("Retry")) retry = button;
                else if (button.name.Contains("Give")) giveUp = button;
            }
            var size = new Vector2(220, 56);
            if (retry != null) { SizeButton(retry, size, new Vector2(-130, -80)); MenuStyle.ApplyButton(retry, 34); }
            if (giveUp != null) { SizeButton(giveUp, size, new Vector2(130, -80)); MenuStyle.ApplyButton(giveUp, 34); }
        }

        private static bool IsSkinned(Canvas canvas) => canvas.transform.Find("Ember Backdrop") != null;

        // Dark ground plus embers, behind everything else on the canvas.
        private static void Backdrop(Transform canvas)
        {
            var backdrop = MenuStyle.FullScreen(canvas, "Ember Backdrop", MenuStyle.Background, false);
            backdrop.raycastTarget = false;
            backdrop.transform.SetSiblingIndex(0);
            MenuStyle.Embers(canvas).transform.SetSiblingIndex(1);
            if (Camera.main != null) Camera.main.backgroundColor = MenuStyle.Background;
        }

        private static Text FindTitle(Transform canvas)
        {
            foreach (var text in canvas.GetComponentsInChildren<Text>(true))
                if (text.GetComponentInParent<Button>() == null) return text;
            return null;
        }

        private static void HideSceneTitle(Transform canvas)
        {
            var title = FindTitle(canvas);
            if (title != null) title.gameObject.SetActive(false);
        }

        private static void SizeButton(Button button, Vector2 size, Vector2 position)
        {
            MenuStyle.Place((RectTransform)button.transform, position, size);
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null) MenuStyle.Stretch((RectTransform)label.transform);
        }
    }
}
