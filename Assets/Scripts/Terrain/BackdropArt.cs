using AscendJigoku.Gameplay;
using UnityEngine;

// Generated art for PillarEnvironment's backdrop: seamless 256x256 silhouette tiles per zone and depth,
// lava falls, fog, the sky glow gradient and the giant gate. Colours are baked, so farther layers sit closer to the sky.
internal static class BackdropArt
{
    public const int Size = 256;
    // Accent per zone: lava, ash, bone, vermilion.
    private static readonly Color[] Accents = { new Color(0.95f, 0.38f, 0.12f), new Color(0.5f, 0.46f, 0.42f), new Color(0.85f, 0.78f, 0.64f), new Color(0.85f, 0.2f, 0.12f) };
    private static readonly float[] AccentMix = { 0.3f, 0.45f, 0.55f };
    private static readonly Color Lantern = new Color(0.95f, 0.55f, 0.2f);

    private sealed class Pix
    {
        public readonly Color32[] Px;
        private readonly int w, h;
        public Pix(int w, int h) { this.w = w; this.h = h; Px = new Color32[w * h]; }

        public void Set(int x, int y, Color32 c) { x %= w; if (x < 0) x += w; y %= h; if (y < 0) y += h; Px[y * w + x] = c; }

        public void Rect(float x0, float y0, float x1, float y1, Color32 c)
        {
            for (int y = Mathf.RoundToInt(y0); y <= Mathf.RoundToInt(y1); y++)
                for (int x = Mathf.RoundToInt(x0); x <= Mathf.RoundToInt(x1); x++) Set(x, y, c);
        }

        public void Disc(float cx, float cy, float rx, float ry, Color32 c)
        {
            for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
                for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                {
                    float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                    if (nx * nx + ny * ny <= 1) Set(x, y, c);
                }
        }

        // Wobbling vertical shaft; integer frequencies keep it seamless top to bottom.
        public void Column(float cx, float half, int f1, int f2, float p1, float p2, float wobble, Color32 c, Color32 rim, int ledgeEvery = 0)
        {
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                float width = half + wobble * Mathf.Sin(6.283f * f1 * v + p1) + wobble * 0.4f * Mathf.Sin(6.283f * f2 * v + p2);
                if (ledgeEvery > 0 && Mathf.Repeat(v * ledgeEvery, 1) < 0.05f) width += 6;
                int x0 = Mathf.RoundToInt(cx - width), x1 = Mathf.RoundToInt(cx + width);
                for (int x = x0; x <= x1; x++) Set(x, y, x < x0 + 2 ? rim : c);
            }
        }

        public void Rib(float cx, float y, int side, float scale, Color32 c)
        {
            for (float t = 0; t <= 1; t += 0.012f)
                Rect(cx + side * (6 + 44 * scale * Mathf.Sin(t * 1.57f)), y - 36 * scale * t * t, cx + side * (6 + 44 * scale * Mathf.Sin(t * 1.57f)) + 1, y - 36 * scale * t * t + 2, c);
        }

        public void Skull(float cx, float cy, float r, Color32 bone, Color32 hole)
        {
            Disc(cx, cy, r, r * 0.9f, bone);
            Rect(cx - r * 0.5f, cy - r * 1.2f, cx + r * 0.5f, cy - r * 0.6f, bone);
            Disc(cx - r * 0.4f, cy + r * 0.1f, r * 0.26f, r * 0.3f, hole);
            Disc(cx + r * 0.4f, cy + r * 0.1f, r * 0.26f, r * 0.3f, hole);
            Rect(cx, cy - r * 0.3f, cx, cy - r * 0.1f, hole);
            for (float x = cx - r * 0.4f; x <= cx + r * 0.4f; x += 2) Rect(x, cy - r * 1.0f, x, cy - r * 0.8f, hole);
        }

        public void Torii(float cx, float y, float width, float height, Color32 post, Color32 beam)
        {
            float t = Mathf.Max(2, width * 0.07f), half = width * 0.38f;
            Rect(cx - half - t, y, cx - half + t, y + height, post);
            Rect(cx + half - t, y, cx + half + t, y + height, post);
            Rect(cx - half, y + height * 0.7f, cx + half, y + height * 0.7f + t, post);
            for (float dx = -width * 0.5f; dx <= width * 0.5f; dx++)
            {
                float lift = Mathf.Pow(Mathf.Abs(dx) / (width * 0.5f), 3) * width * 0.07f;
                Rect(cx + dx, y + height - t + lift, cx + dx, y + height + t * 1.2f + lift, beam);
            }
        }
    }

    private static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

    // layer: 0 far, 1 mid, 2 near. tone is the silhouette colour for that zone and depth.
    public static Texture2D Layer(int zone, int layer, int seed, Color sky, Color tone)
    {
        var r = new System.Random(seed ^ (zone * 7919) ^ (layer * 104729));
        var p = new Pix(Size, Size);
        Color32 body = tone, rim = Color.Lerp(tone, Accents[zone], 0.2f);
        Color32 accent = Color.Lerp(tone, Accents[zone], AccentMix[layer]);
        Color32 hole = Color.Lerp(sky, Color.black, 0.5f);
        float f = Size;
        switch (zone)
        {
            case 0:
                if (layer == 0) for (int i = 0; i < 5 + r.Next(3); i++) p.Column(Rand(r, 0, f), Rand(r, 5, 14), 1 + r.Next(3), 3 + r.Next(5), Rand(r, 0, 6), Rand(r, 0, 6), 3, body, rim, 2 + r.Next(3));
                else if (layer == 1)
                    for (int i = 0; i < 3; i++)
                    {
                        float cx = Rand(r, 0, f);
                        p.Column(cx, Rand(r, 12, 22), 1 + r.Next(2), 4 + r.Next(3), Rand(r, 0, 6), Rand(r, 0, 6), 5, body, rim);
                        int crack = Mathf.RoundToInt(cx + Rand(r, -4, 4));
                        for (int y = 0; y < Size; y++) if (y % 64 < 44) p.Set(crack + Mathf.RoundToInt(2 * Mathf.Sin(y * 0.049f)), y, accent);
                    }
                else
                {
                    for (int i = 0; i < 2; i++) p.Column(Rand(r, 0, f), Rand(r, 8, 14), 1, 4, Rand(r, 0, 6), Rand(r, 0, 6), 4, body, body);
                    for (int i = 0; i < 14; i++) p.Set(r.Next(Size), r.Next(Size), accent);
                }
                break;
            case 1:
                if (layer == 0)
                    for (int i = 0; i < 3; i++)
                    {
                        int cx = r.Next(Size);
                        p.Column(cx, Rand(r, 9, 15), 1, 2, Rand(r, 0, 6), Rand(r, 0, 6), 1, body, rim, 3);
                        for (int y = 20 + r.Next(10); y < Size; y += 52) p.Rect(cx - 1, y, cx, y + 6, hole);
                    }
                else if (layer == 1)
                    for (int i = 0; i < 2; i++)
                    {
                        float cx = Rand(r, 0, f), span = 26;
                        p.Column(cx - span, 5, 1, 2, 0, 0, 1, body, body);
                        p.Column(cx + span, 5, 1, 2, 1, 2, 1, body, body);
                        for (int yc = r.Next(40); yc < Size; yc += 128)
                            for (float a = 0.2f; a < 2.95f; a += 0.03f) if (a < 1.2f || a > 1.5f) p.Rect(cx + Mathf.Cos(a) * span, yc + Mathf.Sin(a) * 22, cx + Mathf.Cos(a) * span + 3, yc + Mathf.Sin(a) * 22 + 3, body);
                    }
                else for (int i = 0; i < 3; i++) p.Column(Rand(r, 0, f), Rand(r, 4, 7), 1 + r.Next(2), 3, Rand(r, 0, 6), Rand(r, 0, 6), 2, body, body);
                break;
            case 2:
                if (layer == 0)
                    for (int i = 0; i < 2; i++)
                    {
                        float cx = 64 + i * 128 + r.Next(40);
                        for (int k = 0, y = 0; y < Size; y += 9, k++) p.Rect(cx - 4 - k % 2, y, cx + 4 + k % 2, y + 7, body);
                        for (int yc = 24; yc < Size; yc += 42) { float s = Rand(r, 0.8f, 1.2f); p.Rib(cx, yc, -1, s, body); p.Rib(cx, yc, 1, s, body); }
                    }
                else if (layer == 1)
                {
                    float cx = Rand(r, 60, 200);
                    Color32 wall = Color.Lerp(sky, tone, 0.6f);
                    p.Column(cx, 28, 1, 3, Rand(r, 0, 6), Rand(r, 0, 6), 3, wall, body);
                    for (int i = 0; i < 3; i++) p.Skull(cx + Rand(r, -14, 14), 30 + i * 85, Rand(r, 9, 12), body, hole);
                }
                else for (int i = 0; i < 3; i++) p.Rib(Rand(r, 0, f), 40 + i * 85, r.Next(2) * 2 - 1, Rand(r, 0.5f, 0.8f), body);
                break;
            default:
                Color32 vermilion = Color.Lerp(tone, Accents[3], 0.22f); // kept dim so torii never read as platforms
                if (layer == 0) { p.Torii(70 + r.Next(30), 0, 100, 130, vermilion, body); p.Torii(190 + r.Next(30), 128, 90, 120, vermilion, body); }
                else if (layer == 1)
                {
                    for (int i = 0; i < 3; i++) p.Torii(Rand(r, 0, f), i * 85, 50, 64, vermilion, body);
                    for (int i = 0; i < 3; i++)
                    {
                        float x = Rand(r, 0, f), y = 40 + i * 85;
                        p.Rect(x, y + 6, x, y + 22, body);
                        p.Rect(x - 2, y, x + 2, y + 6, Color.Lerp(tone, Lantern, 0.55f));
                    }
                }
                else
                    for (int i = 0; i < 3; i++)
                    {
                        int x = r.Next(Size), start = r.Next(Size), length = (int)Rand(r, 50, 120);
                        for (int y = start; y < start + length; y++) { p.Set(x, y % Size, body); if (y % 8 < 4) p.Set(x + 1, y % Size, body); }
                    }
                break;
        }
        return Finish(p.Px, Size, Size, $"Backdrop z{zone} l{layer}", FilterMode.Point);
    }

    // Vertical streaks of lava that scroll down; transparent elsewhere.
    public static Texture2D Falls(int seed, Color glow)
    {
        var r = new System.Random(seed ^ 0x2f1a);
        var p = new Pix(Size, Size);
        for (int i = 0; i < 5; i++)
        {
            int cx = r.Next(Size), width = 1 + r.Next(2), k = 2 + r.Next(3);
            float phase = Rand(r, 0, 6.283f);
            for (int y = 0; y < Size; y++)
            {
                float v = 0.5f + 0.5f * Mathf.Sin(6.283f * k * y / Size + phase);
                if (v < 0.25f) continue;
                var c = glow; c.a = v;
                var halo = glow; halo.a = v * 0.25f;
                p.Set(cx - 1, y, halo); p.Set(cx + width, y, halo);
                for (int x = 0; x < width; x++) p.Set(cx + x, y, c);
            }
        }
        return Finish(p.Px, Size, Size, "Backdrop falls", FilterMode.Point);
    }

    // Soft white drifting bands (alpha only); tint with the sprite colour.
    public static Texture2D Fog(int seed)
    {
        const int w = 256, h = 128, gx = 4, gy = 3;
        var r = new System.Random(seed ^ 0x6f09);
        var lattice = new float[gx, gy];
        for (int x = 0; x < gx; x++) for (int y = 0; y < gy; y++) lattice[x, y] = (float)r.NextDouble();
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float fx = (float)x * gx / w, fy = (float)y * gy / h;
                int ix = (int)fx, iy = (int)fy;
                float tx = Mathf.SmoothStep(0, 1, fx - ix), ty = Mathf.SmoothStep(0, 1, fy - iy);
                float top = Mathf.Lerp(lattice[ix, iy], lattice[(ix + 1) % gx, iy], tx);
                float bottom = Mathf.Lerp(lattice[ix, (iy + 1) % gy], lattice[(ix + 1) % gx, (iy + 1) % gy], tx);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.SmoothStep(0.35f, 0.95f, Mathf.Lerp(top, bottom, ty)) * 255));
            }
        return Finish(px, w, h, "Backdrop fog", FilterMode.Bilinear);
    }

    // 1 x 64 vertical gradient, opaque at the bottom.
    public static Texture2D Glow()
    {
        var px = new Color32[64];
        for (int y = 0; y < 64; y++) px[y] = new Color32(255, 255, 255, (byte)(Mathf.Pow(1 - y / 63f, 2.2f) * 255));
        return Finish(px, 1, 64, "Backdrop glow", FilterMode.Bilinear);
    }

    // Huge torii for the top of the pillar (192 x 128).
    public static Texture2D GiantGate(Color sky)
    {
        var p = new Pix(192, 128);
        p.Torii(96, 0, 170, 112, Color.Lerp(sky, Accents[3], 0.18f), Color.Lerp(sky, Color.black, 0.3f));
        return Finish(p.Px, 192, 128, "Backdrop gate", FilterMode.Point);
    }

    private static Texture2D Finish(Color32[] pixels, int w, int h, string name, FilterMode filter)
    {
        var texture = ProceduralSprites.NewTexture(w, h, name, TextureWrapMode.Repeat);
        texture.filterMode = filter;
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
