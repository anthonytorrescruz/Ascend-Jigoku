using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AscendJigoku.UI
{
    // One place for menu look-and-feel. Values copy the MainMenu scene so runtime menus match it.
    // When the UI direction is decided, restyle here instead of in each menu.
    public static class MenuStyle
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1280, 720);
        public static readonly Color Background = new Color(0.09f, 0.09f, 0.1f, 1f);
        public static readonly Color Overlay = new Color(0.09f, 0.09f, 0.1f, 0.92f);
        public static readonly Color ButtonNormal = new Color(0.25f, 0.25f, 0.27f, 1f);
        public static readonly Color ButtonHighlighted = new Color(0.52f, 0.52f, 0.55f, 1f);
        public static readonly Color ButtonPressed = new Color(0.7f, 0.7f, 0.73f, 1f);
        public static readonly Color TextColor = Color.white;
        public static readonly Color MutedText = new Color(0.72f, 0.72f, 0.75f, 1f);
        public static readonly Color TabActive = new Color(0.52f, 0.52f, 0.55f, 1f);
        public static readonly Vector2 ButtonSize = new Vector2(320, 60);
        public const int TitleSize = 64;
        public const int ButtonTextSize = 26;
        public const int BodyTextSize = 22;
        public const float FadeDuration = 0.1f;

        private static Font font;
        public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static ColorBlock ButtonColors
        {
            get
            {
                var colors = ColorBlock.defaultColorBlock;
                colors.normalColor = ButtonNormal;
                colors.highlightedColor = ButtonHighlighted;
                colors.selectedColor = ButtonHighlighted;
                colors.pressedColor = ButtonPressed;
                colors.fadeDuration = FadeDuration;
                return colors;
            }
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        public static RectTransform CreateCanvas(string name, int sortingOrder, Transform parent = null)
        {
            EnsureEventSystem();
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }

        public static Image FullScreen(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        public static RectTransform Group(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size,
            int fontSize, TextAnchor alignment = TextAnchor.MiddleCenter, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, size);
            var label = go.GetComponent<Text>();
            label.font = Font; label.fontSize = fontSize; label.alignment = alignment;
            label.color = color ?? TextColor; label.raycastTarget = false; label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static Text Title(Transform parent, string text, float y, int size = TitleSize)
            => Label(parent, "Title", text, new Vector2(0, y), new Vector2(1000, 90), size);

        public static Button Button(Transform parent, string text, Vector2 position, Action onClick, Vector2? size = null, int fontSize = ButtonTextSize)
        {
            var dimensions = size ?? ButtonSize;
            var go = new GameObject(text + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, dimensions);
            var image = go.GetComponent<Image>(); image.color = Color.white;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image; button.transition = Selectable.Transition.ColorTint; button.colors = ButtonColors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            Label(go.transform, "Label", text, Vector2.zero, dimensions, fontSize);
            return button;
        }

        public static void SetButtonText(Button button, string text)
        {
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = text;
        }

        // Horizontal slider in the menu palette. Value text is optional and updated by the caller.
        public static Slider Slider(Transform parent, Vector2 position, float width, float min, float max, bool wholeNumbers, float value, Action<float> onChanged)
        {
            var root = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            Place((RectTransform)root.transform, position, new Vector2(width, 30));

            var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(root.transform, false);
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(0, 0.5f); trackRect.anchorMax = new Vector2(1, 0.5f);
            trackRect.sizeDelta = new Vector2(0, 10); trackRect.anchoredPosition = Vector2.zero;
            track.GetComponent<Image>().color = ButtonNormal;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            var fillAreaRect = (RectTransform)fillArea.transform;
            fillAreaRect.anchorMin = new Vector2(0, 0.5f); fillAreaRect.anchorMax = new Vector2(1, 0.5f);
            fillAreaRect.sizeDelta = new Vector2(0, 10); fillAreaRect.anchoredPosition = Vector2.zero;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = (RectTransform)fill.transform; fillRect.sizeDelta = Vector2.zero;
            fill.GetComponent<Image>().color = ButtonHighlighted;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            var handleAreaRect = (RectTransform)handleArea.transform;
            handleAreaRect.anchorMin = Vector2.zero; handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(9, 0); handleAreaRect.offsetMax = new Vector2(-9, 0);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = (RectTransform)handle.transform; handleRect.sizeDelta = new Vector2(18, 0);
            var handleImage = handle.GetComponent<Image>(); handleImage.color = Color.white;

            var slider = root.GetComponent<Slider>();
            slider.fillRect = fillRect; slider.handleRect = handleRect; slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = ButtonPressed; colors.highlightedColor = Color.white; colors.selectedColor = Color.white;
            colors.pressedColor = ButtonHighlighted; colors.fadeDuration = FadeDuration;
            slider.colors = colors;
            slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = wholeNumbers;
            slider.SetValueWithoutNotify(value);
            if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        public static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        // Main menu deliberately has no moving selection for mouse users; pads still need a starting point.
        public static void SelectForController(Selectable target)
        {
            if (target == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            if (Gamepad.current != null) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
    }
}
