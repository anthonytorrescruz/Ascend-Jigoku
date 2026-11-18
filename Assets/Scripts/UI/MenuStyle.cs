using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AscendJigoku.UI
{
    // One place for the game's UI look ("Ember"): text-only menus, a flame diamond on the selected option,
    // embers rising behind. Every runtime menu builds from these helpers, so restyle here, not in each menu.
    // Fonts live in Assets/Resources/Fonts (both SIL OFL licensed).
    public static class MenuStyle
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1280, 720);

        // Palette
        public static readonly Color Background = Hex(0x0A0505);
        public static readonly Color Overlay = new Color(0.04f, 0.02f, 0.02f, 0.88f);
        public static readonly Color TextColor = Hex(0xFBEEDD);
        public static readonly Color TitleColor = Hex(0xF2E4CC);
        public static readonly Color DimText = Hex(0x6F5C53);
        public static readonly Color MutedText = Hex(0x8A7469);
        public static readonly Color Ember = Hex(0xF08A3C);
        public static readonly Color EmberDeep = Hex(0xD6452A);
        public static readonly Color Ash = Hex(0x2A1A15);
        public static readonly Color TabActive = Ember;

        public static readonly Vector2 ButtonSize = new Vector2(320, 48);
        public const int TitleSize = 64;
        public const int ButtonTextSize = 28;
        public const int BodyTextSize = 22;
        public const float FadeDuration = 0.08f;

        private static Font font, titleFont;
        public static Font Font => font != null ? font : (font = LoadFont("Fonts/PixelifySans"));
        public static Font TitleFont => titleFont != null ? titleFont : (titleFont = LoadFont("Fonts/Silkscreen-Regular"));

        private static Font LoadFont(string path)
        {
            var loaded = Resources.Load<Font>(path);
            return loaded != null ? loaded : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        // Buttons tint their label, not a background: dim at rest, bright when selected or hovered.
        public static ColorBlock ButtonColors
        {
            get
            {
                var colors = ColorBlock.defaultColorBlock;
                colors.normalColor = DimText;
                colors.highlightedColor = TextColor;
                colors.selectedColor = TextColor;
                colors.pressedColor = Ember;
                colors.disabledColor = new Color(DimText.r, DimText.g, DimText.b, 0.4f);
                colors.fadeDuration = FadeDuration;
                return colors;
            }
        }

        // uGUI needs an EventSystem for clicks and keyboard/pad navigation; scenes without one get one created here.
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        // Overlay canvas scaled to 1280x720. Every runtime menu starts here; higher sortingOrder draws on top.
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
            // Ultrawide fits to height, otherwise menus laid out for 720 tall clip top and bottom.
            scaler.matchWidthOrHeight = Screen.width * 9 > Screen.height * 16 ? 1f : 0.5f;
            return (RectTransform)go.transform;
        }

        // Solid backdrop with embers rising over it.
        public static Image FullScreen(Transform parent, string name, Color color, bool embers = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var image = go.GetComponent<Image>();
            image.color = color;
            if (embers) Embers(parent);
            return image;
        }

        public static EmberField Embers(Transform parent)
        {
            var go = new GameObject("Embers", typeof(RectTransform), typeof(EmberField));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            return go.GetComponent<EmberField>();
        }

        public static RectTransform Group(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect);
            return rect;
        }

        // Group pinned to the lower-left corner, for the menu's option list.
        public static RectTransform LeftColumn(Transform parent, float height)
        {
            var rect = Group(parent, "Options");
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(96, 72);
            rect.sizeDelta = new Vector2(ButtonSize.x, height);
            return rect;
        }

        // Plain text object centered on `position` within the parent. Text never blocks clicks.
        public static Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size,
            int fontSize, TextAnchor alignment = TextAnchor.MiddleCenter, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, size);
            var label = go.GetComponent<Text>();
            label.font = Font;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color ?? TextColor;
            label.raycastTarget = false;
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public static Text Title(Transform parent, string text, float y, int size = TitleSize)
        {
            var title = Label(parent, "Title", text, new Vector2(0, y), new Vector2(1000, size * 1.4f), size);
            StyleTitle(title, size);
            return title;
        }

        // Big title pinned to the upper-left, with a small ember line above it ("ASCEND" over "JIGOKU").
        public static Text CornerTitle(Transform parent, string kicker, string text, int size = 84)
        {
            var title = Label(parent, "Title", text, Vector2.zero, new Vector2(900, size * 1.2f), size, TextAnchor.UpperLeft);
            var rect = (RectTransform)title.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(96, -120);
            StyleTitle(title, size);
            if (!string.IsNullOrEmpty(kicker))
            {
                var small = Label(parent, "Kicker", kicker, Vector2.zero, new Vector2(900, 40), size / 2, TextAnchor.LowerLeft, Ember);
                small.font = TitleFont;
                var smallRect = (RectTransform)small.transform;
                smallRect.anchorMin = smallRect.anchorMax = smallRect.pivot = new Vector2(0, 1);
                smallRect.anchoredPosition = new Vector2(100, -78);
            }
            return title;
        }

        public static void StyleTitle(Text title, int size)
        {
            title.font = TitleFont;
            title.fontSize = size;
            title.color = TitleColor;
            var shadow = title.GetComponent<Shadow>();
            if (shadow == null) shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(EmberDeep.r, EmberDeep.g, EmberDeep.b, 0.75f);
            shadow.effectDistance = new Vector2(Mathf.Max(2, size / 20), -Mathf.Max(2, size / 20));
        }

        // Text-only button: the label is the clickable graphic and ApplyButton adds the Ember styling.
        public static Button Button(Transform parent, string text, Vector2 position, Action onClick, Vector2? size = null,
            int fontSize = ButtonTextSize, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var dimensions = size ?? ButtonSize;
            var go = new GameObject(text + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, dimensions);
            var button = go.GetComponent<Button>();
            if (onClick != null) button.onClick.AddListener(() => onClick());
            Label(go.transform, "Label", text, Vector2.zero, dimensions, fontSize, alignment, Color.white);
            ApplyButton(button);
            return button;
        }

        // Turns any uGUI button (including ones authored in a scene) into an Ember text button.
        public static void ApplyButton(Button button, int? fontSize = null, TextAnchor? alignment = null)
        {
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                // Invisible, but still receives clicks.
                image.sprite = null;
                image.color = Color.clear;
            }
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.font = Font;
                label.color = Color.white; // tinted by the button's colors
                if (fontSize.HasValue) label.fontSize = fontSize.Value;
                if (alignment.HasValue) label.alignment = alignment.Value;
                button.targetGraphic = label;
            }
            button.transition = Selectable.Transition.ColorTint;
            button.colors = ButtonColors;
            if (button.GetComponent<EmberButton>() == null) button.gameObject.AddComponent<EmberButton>();
        }

        public static void SetButtonText(Button button, string text)
        {
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = text;
        }

        // Thin ember slider: ash track, ember fill, small bright handle.
        // Built from scratch (track, fill, rotated-square handle) because the default uGUI slider needs sprites.
        // The initial value is set without firing onChanged.
        public static Slider Slider(
            Transform parent, Vector2 position, float width, float min, float max, bool wholeNumbers, float value,
            Action<float> onChanged)
        {
            var root = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            Place((RectTransform)root.transform, position, new Vector2(width, 30));

            var track = new GameObject("Track", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(root.transform, false);
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(0, 0.5f);
            trackRect.anchorMax = new Vector2(1, 0.5f);
            trackRect.sizeDelta = new Vector2(0, 6);
            trackRect.anchoredPosition = Vector2.zero;
            track.GetComponent<Image>().color = Ash;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            var fillAreaRect = (RectTransform)fillArea.transform;
            fillAreaRect.anchorMin = new Vector2(0, 0.5f);
            fillAreaRect.anchorMax = new Vector2(1, 0.5f);
            fillAreaRect.sizeDelta = new Vector2(0, 6);
            fillAreaRect.anchoredPosition = Vector2.zero;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = (RectTransform)fill.transform;
            fillRect.sizeDelta = Vector2.zero;
            fill.GetComponent<Image>().color = EmberDeep;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            var handleAreaRect = (RectTransform)handleArea.transform;
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(7, 0);
            handleAreaRect.offsetMax = new Vector2(-7, 0);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = (RectTransform)handle.transform;
            handleRect.sizeDelta = new Vector2(14, -14);
            handleRect.localRotation = Quaternion.Euler(0, 0, 45);
            var handleImage = handle.GetComponent<Image>();
            handleImage.color = Color.white;

            var slider = root.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = MutedText;
            colors.highlightedColor = Ember;
            colors.selectedColor = Ember;
            colors.pressedColor = TextColor;
            colors.fadeDuration = FadeDuration;
            slider.colors = colors;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.SetValueWithoutNotify(value);
            if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));
            return slider;
        }

        // Centered anchor and pivot, so `position` is an offset from the parent's middle.
        public static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // Start every menu with its first option selected so keyboard and pad work immediately.
        // Hovering with the mouse moves the selection, so there is only ever one lit option.
        public static void SelectForController(Selectable target)
        {
            if (target == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }
    }
}
