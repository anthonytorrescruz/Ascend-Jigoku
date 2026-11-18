using UnityEngine;
using UnityEngine.UI;

namespace AscendJigoku.UI
{
    // Short Ember-style message under the zone title. QolToast.Show("CHECKPOINT").
    public sealed class QolToast : MonoBehaviour
    {
        private const float FadeIn = 0.15f, Hold = 1.4f, FadeOut = 0.5f;
        private static QolToast current;
        private Text label;
        private float age = float.PositiveInfinity;

        // Creates the toast object on first use; later calls reuse it and restart the fade (fade-in, hold, fade-out; seconds).
        public static void Show(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (current == null)
            {
                var go = new GameObject("Toast");
                current = go.AddComponent<QolToast>();
                var canvas = MenuStyle.CreateCanvas("Toast Canvas", 60, go.transform);
                current.label = MenuStyle.Label(
                    canvas, "Toast", "", new Vector2(0, 130), new Vector2(900, 44), 30, TextAnchor.MiddleCenter, MenuStyle.Ember);
                var shadow = current.label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.8f);
                shadow.effectDistance = new Vector2(2, -2);
            }
            current.label.text = message;
            current.age = 0;
        }

        private void Update()
        {
            if (label == null) return;
            age += Time.unscaledDeltaTime;
            float alpha = age < FadeIn ? age / FadeIn : age < FadeIn + Hold ? 1 : Mathf.Clamp01(1 - (age - FadeIn - Hold) / FadeOut);
            var c = label.color;
            c.a = alpha;
            label.color = c;
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
        }
    }
}
