using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AscendJigoku.UI
{
    // Built once on death, so no large UI geometry or polling exists during gameplay.
    public sealed class DeathScreen : MonoBehaviour
    {
        private static DeathScreen current;
        private Scene retryScene;
        private float previousTimeScale;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private bool leaving;
        private Font font;

        public static void Show(Scene scene)
        {
            if (current != null) return;
            var root = new GameObject("You Died", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            current = root.AddComponent<DeathScreen>();
            current.Build(scene);
        }

        private void Build(Scene scene)
        {
            retryScene = scene;
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            // Fit the entire menu even in tall or ultrawide windows.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var background = Rect("Black Background", transform, Vector2.zero, Vector2.zero);
            Stretch(background);
            background.gameObject.AddComponent<Image>().color = Color.black;
            var group = background.gameObject.AddComponent<CanvasGroup>();
            var content = Rect("Menu", background, Vector2.zero, new Vector2(720, 520));
            Label("Caption", content, "THE ASCENT ENDS HERE", new Vector2(0, 160), new Vector2(700, 50),
                22, new Color(0.5f, 0.5f, 0.5f), FontStyle.Normal);
            Label("Title", content, "YOU DIED", new Vector2(0, 65), new Vector2(720, 130),
                96, new Color(0.9f, 0.08f, 0.1f), FontStyle.Bold);
            var line = Rect("Red Accent", content, new Vector2(0, -35), new Vector2(120, 3));
            line.gameObject.AddComponent<Image>().color = new Color(0.65f, 0.03f, 0.05f);
            var retry = MenuButton("Retry", "RETRY", content, -115, Retry);
            var giveUp = MenuButton("Give Up", "GIVE UP", content, -205, GiveUp);
            var navigation = retry.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnDown = giveUp;
            navigation.selectOnUp = giveUp;
            retry.navigation = navigation;
            navigation.selectOnDown = retry;
            navigation.selectOnUp = retry;
            giveUp.navigation = navigation;
            if (EventSystem.current == null)
                new GameObject("Death Menu EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var input = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            if (input == null) input = EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
            if (input.actionsAsset == null || input.leftClick?.action == null || input.submit?.action == null)
                input.AssignDefaultActions();
            EventSystem.current.firstSelectedGameObject = retry.gameObject;
            EventSystem.current.SetSelectedGameObject(retry.gameObject);
            StartCoroutine(Reveal(group));
        }

        private static IEnumerator Reveal(CanvasGroup group)
        {
            float elapsed = 0;
            while (elapsed < 0.2f)
            {
                group.alpha = Mathf.Clamp01(elapsed / 0.2f);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            group.alpha = 1;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void Label(string name, Transform parent, string value, Vector2 position, Vector2 size,
            int fontSize, Color color, FontStyle style)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
        }

        private Button MenuButton(string name, string label, Transform parent, float y, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(name, parent, new Vector2(0, y), new Vector2(360, 70));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color(0.08f, 0.08f, 0.08f);
            colors.highlightedColor = colors.selectedColor = new Color(0.38f, 0.04f, 0.06f);
            colors.pressedColor = new Color(0.65f, 0.06f, 0.08f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.48f, 0.05f, 0.07f);
            outline.effectDistance = Vector2.one;
            Label("Label", rect, label, Vector2.zero, rect.sizeDelta, 28, Color.white, FontStyle.Bold);
            button.onClick.AddListener(action);
            return button;
        }

        public void Retry()
        {
            if (leaving) return;
            leaving = true;
            RestoreGameplay();
            SceneManager.LoadScene(retryScene.name);
        }

        public void GiveUp()
        {
            if (leaving) return;
            leaving = true;
            RestoreGameplay();
            MainMenu.QuitApplication();
        }

        private void RestoreGameplay()
        {
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }

        private void OnDestroy()
        {
            if (current != this) return;
            if (!leaving) RestoreGameplay();
            current = null;
        }
    }
}
