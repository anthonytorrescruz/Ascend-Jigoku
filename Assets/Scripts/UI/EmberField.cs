using UnityEngine;
using UnityEngine.UI;

namespace AscendJigoku.UI
{
    // Small square embers drifting up behind a menu. Runs on unscaled time so it moves while paused.
    // Respects Settings > Accessibility > Reduce flashing by holding still.
    [RequireComponent(typeof(RectTransform))]
    public sealed class EmberField : MonoBehaviour
    {
        [Min(0)] public int count = 42;

        // X and Y are 0-1 positions within the area; Speed is area heights per second; Phase offsets the side-to-side sway.
        private struct Ember
        {
            public RectTransform Rect;
            public Image Image;
            public float X, Y, Speed, Phase, Size;
        }

        private Ember[] embers;
        private RectTransform area;

        private void Start()
        {
            area = (RectTransform)transform;
            embers = new Ember[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Ember", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                image.color = i % 4 == 0 ? new Color(1f, 0.75f, 0.42f) : MenuStyle.Ember;
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                float size = i % 5 == 0 ? 6 : 4;
                rect.sizeDelta = new Vector2(size, size);
                embers[i] = new Ember
                {
                    Rect = rect,
                    Image = image,
                    X = Random.value,
                    Y = Random.value,
                    Speed = Random.Range(0.025f, 0.08f),
                    Phase = Random.value * 6.28f,
                    Size = size
                };
            }
        }

        private void Update()
        {
            if (embers == null) return;
            bool still = AscendJigoku.Settings.GameSettings.ReduceFlashing;
            Vector2 size = area.rect.size;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < embers.Length; i++)
            {
                ref var e = ref embers[i];
                if (!still) e.Y += e.Speed * dt;
                // Past the top: wrap to the bottom at a new random column.
                if (e.Y > 1.05f)
                {
                    e.Y = -0.05f;
                    e.X = Random.value;
                }
                float sway = Mathf.Sin(Time.unscaledTime * 0.7f + e.Phase) * 10f;
                // Snap to whole pixels so they read as pixel art.
                e.Rect.anchoredPosition = new Vector2(Mathf.Round(e.X * size.x + sway), Mathf.Round(e.Y * size.y));
                // Fade out as it rises.
                var c = e.Image.color;
                c.a = Mathf.Clamp01(1.1f - e.Y) * 0.85f;
                e.Image.color = c;
            }
        }
    }
}
