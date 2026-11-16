using UnityEngine;
using AscendJigoku.UI;

namespace AscendJigoku.Gameplay
{
    // World-space props for the victory fake-out: six dark pillars with ember glow, and the Memento Mori applauding.
    internal sealed class VictoryStage
    {
        public readonly GameObject Root = new GameObject("Victory Stage");
        public readonly SpriteRenderer[] Pillars = new SpriteRenderer[6];
        public readonly Vector3[] Labels = new Vector3[6];
        public SpriteRenderer Skeleton;
        public Vector3 SkeletonBase;
        private Sprite[] frames;
        private Vector3 baseScale = Vector3.one;

        private static readonly float[] Offsets = { -17, 18, -33, 35, -50, 52 };
        private static readonly float[] Tops = { 30, 38, 48, 58, 70, 86 };
        private static readonly float[] Widths = { 5, 6, 6, 7, 8, 9 };

        public VictoryStage(Vector3 summit)
        {
            for (int i = 0; i < 6; i++)
            {
                float depth = i / 5f;
                var go = new GameObject("Pillar " + (i + 2));
                go.transform.SetParent(Root.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                int height = Mathf.RoundToInt((Tops[i] + 46) * 2);
                sr.sprite = PillarSprite(Mathf.RoundToInt(Widths[i] * 2), height, 91 + i * 17, 1 - depth * 0.45f);
                sr.sortingOrder = -30 + i * -2;
                sr.color = new Color(1, 1, 1, 0);
                go.transform.position = new Vector3(summit.x + Offsets[i], summit.y - 46, 0);
                Pillars[i] = sr;
                Labels[i] = new Vector3(summit.x + Offsets[i], summit.y + Tops[i] - 4, 0);
            }
        }

        private static Sprite PillarSprite(int w, int h, int seed, float light)
        {
            var rng = new System.Random(seed);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var body = new Color(0.07f, 0.03f, 0.03f);
            var glow = MenuStyle.EmberDeep;
            int top = h - 1 - rng.Next(0, 3);
            for (int x = 0; x < w; x++)
            {
                int edge = Mathf.Min(x, w - 1 - x);
                int column = top - (edge == 0 ? 3 : 0) - rng.Next(0, 3);
                for (int y = 0; y < h; y++)
                {
                    if (y > column) { tex.SetPixel(x, y, Color.clear); continue; }
                    float rise = y / (float)h;
                    var c = Color.Lerp(body, glow, (edge == 0 ? 0.55f : edge == 1 ? 0.18f : 0f) * light);
                    c = Color.Lerp(c, glow, (1 - rise) * 0.14f * light);
                    if (y % 14 == 7 && rng.Next(0, 4) == 0) c = Color.Lerp(c, MenuStyle.Ember, 0.5f * light);
                    tex.SetPixel(x, y, c);
                }
                for (int k = 0; k < 3; k++) // embers glowing in the cracks
                {
                    int y = rng.Next(h / 4, column);
                    if (edge > 1 && rng.Next(0, 9) == 0) tex.SetPixel(x, y, Color.Lerp(body, MenuStyle.Ember, 0.8f * light));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0), 2f);
        }

        // Memento Mori borrowed from the checkpoint merchant prefab; null-safe when the generator has none.
        public void AddSkeleton(Vector3 position, Sprite[] sheet, Vector3 scale)
        {
            frames = sheet; baseScale = scale;
            if (frames == null || frames.Length == 0) return;
            var go = new GameObject("Memento Mori");
            go.transform.SetParent(Root.transform, false);
            Skeleton = go.AddComponent<SpriteRenderer>();
            Skeleton.sprite = frames[0];
            Skeleton.sortingOrder = 5;
            SkeletonBase = position;
            go.transform.position = position;
            Skeleton.color = new Color(1, 1, 1, 0);
        }

        public void Animate(float time, float alpha)
        {
            if (Skeleton == null) return;
            Skeleton.sprite = frames[Mathf.FloorToInt(time * 12.5f) % frames.Length];
            Skeleton.color = new Color(1, 1, 1, alpha);
            float clap = Mathf.Max(0, Mathf.Sin(time * 3.2f)); // slow applause: one squash per beat
            Skeleton.transform.position = SkeletonBase + Vector3.up * clap * 0.12f;
            Skeleton.transform.localScale = Vector3.Scale(baseScale, new Vector3(1 + clap * 0.03f, 1 - clap * 0.03f, 1));
        }

        public void SetPillars(float alpha, float pulse)
        {
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Clamp01(alpha * 6 - i * 0.55f);
                Pillars[i].color = new Color(1, 1, 1, a * (1 - pulse * 0.08f));
            }
        }

        public void Destroy() { if (Root != null) Object.Destroy(Root); }
    }
}
