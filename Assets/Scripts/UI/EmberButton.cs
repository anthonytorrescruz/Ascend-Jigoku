using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AscendJigoku.UI
{
    // Added by MenuStyle.ApplyButton. Shows the flame diamond beside the selected option,
    // and lets mouse hover move the selection so keyboard, pad and mouse share one highlight.
    [DisallowMultipleComponent]
    public sealed class EmberButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        private const float Gap = 14, Size = 11;

        private Button button;
        private Text label;
        private RectTransform marker;
        private Image markerImage;
        private bool lit;

        private void Awake()
        {
            button = GetComponent<Button>();
            label = GetComponentInChildren<Text>(true);
            var go = new GameObject("Marker", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            marker = (RectTransform)go.transform;
            marker.sizeDelta = new Vector2(Size, Size);
            marker.localRotation = Quaternion.Euler(0, 0, 45);
            markerImage = go.GetComponent<Image>();
            markerImage.color = MenuStyle.Ember;
            markerImage.raycastTarget = false;
            go.SetActive(false);
        }

        private void OnEnable()
        {
            var events = EventSystem.current;
            SetLit(events != null && events.currentSelectedGameObject == gameObject);
        }

        private void OnDisable() => SetLit(false);

        public void OnSelect(BaseEventData eventData) => SetLit(true);
        public void OnDeselect(BaseEventData eventData) => SetLit(false);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && button.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        private void SetLit(bool value)
        {
            lit = value;
            if (marker != null) marker.gameObject.SetActive(value);
        }

        // Follows the label's text width (labels change, e.g. "Press a key...") and breathes slightly.
        private void LateUpdate()
        {
            if (!lit || label == null) return;
            var rect = (RectTransform)transform;
            float width = Mathf.Min(label.preferredWidth, rect.rect.width);
            float x = label.alignment == TextAnchor.MiddleLeft || label.alignment == TextAnchor.UpperLeft || label.alignment == TextAnchor.LowerLeft
                ? -rect.rect.width * 0.5f - Gap
                : -width * 0.5f - Gap;
            marker.anchoredPosition = new Vector2(x, 0);
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
            marker.localScale = new Vector3(pulse, pulse, 1);
        }
    }
}
