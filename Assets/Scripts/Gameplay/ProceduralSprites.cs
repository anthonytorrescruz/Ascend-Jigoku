using UnityEngine;

namespace AscendJigoku.Gameplay
{
    // Tiny generated sprites for placeholder effects, so the effects need no art assets.
    // Swap these for authored sprites when art exists.
    public static class ProceduralSprites
    {
        private static Sprite square, exclamation;
        private static Texture2D vignette;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { square = exclamation = null; vignette = null; }

        public static Texture2D NewTexture(int width, int height, string name, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = wrap, hideFlags = HideFlags.DontSave };
            return texture;
        }

        // 1 x 1 world unit white square.
        public static Sprite Square
        {
            get
            {
                if (square != null) return square;
                var texture = NewTexture(4, 4, "Generated square");
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels); texture.Apply(false, true);
                square = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
                square.name = "Generated square";
                return square;
            }
        }

        // Pixel-art "!" with a dark outline, bottom-centre pivot. About 0.45 x 0.95 units.
        public static Sprite Exclamation
        {
            get
            {
                if (exclamation != null) return exclamation;
                // Authored top-down: a stem, a one-pixel gap, then the dot.
                string[] rows =
                {
                    ".ooo.",
                    ".oxo.",
                    ".oxo.",
                    ".oxo.",
                    ".oxo.",
                    ".oxo.",
                    ".ooo.",
                    ".....",
                    ".ooo.",
                    ".oxo.",
                    ".ooo.",
                };
                int w = rows[0].Length, h = rows.Length;
                var texture = NewTexture(w, h, "Generated exclamation");
                var clear = new Color32(0, 0, 0, 0);
                var outline = new Color32(25, 10, 10, 255);
                var fill = new Color32(255, 255, 255, 255);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        char c = rows[h - 1 - y][x];
                        texture.SetPixel(x, y, c == 'x' ? fill : c == 'o' ? outline : clear);
                    }
                texture.Apply(false, true);
                exclamation = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 12);
                exclamation.name = "Generated exclamation";
                return exclamation;
            }
        }

        // Transparent centre fading to opaque white at the screen edges; tinted by the UI colour.
        public static Texture2D Vignette
        {
            get
            {
                if (vignette != null) return vignette;
                const int size = 128;
                vignette = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "Generated vignette", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                        float d = Mathf.Sqrt(dx * dx * 0.85f + dy * dy * 1.15f);
                        float a = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.55f, 1.25f, d));
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255));
                    }
                vignette.SetPixels32(pixels); vignette.Apply(false, true);
                return vignette;
            }
        }
    }
}
