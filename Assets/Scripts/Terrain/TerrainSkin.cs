using AscendJigoku.Gameplay;
using UnityEngine;

// Simple pixel-art rock for generated platforms, painted per platform at the character's 32 px/unit so nothing stretches.
// Deterministic from (run seed, row): a rebuilt chunk looks identical. The platform keeps its own SpriteRenderer
// (disabled) and collider; the skin is a child sprite, and its texture is freed when the chunk unloads.
// Placeholder until authored tiles exist; turn it off with PillarEnvironment > Pixel Art Terrain.
[DisallowMultipleComponent]
public sealed class TerrainSkin : MonoBehaviour
{
    public const float PixelsPerUnit = 32;
    private const int MaxPixels = 2048;

    private Texture2D texture;
    private Sprite sprite;

    // ledge = walkable top (crust + lit top edge). Otherwise a rock body under a ledge (shadowed top).
    public static void Apply(SpriteRenderer source, int seed, bool ledge, Color glow, float glowDensity, int sortingOffset)
    {
        if (source == null) return;
        Bounds bounds = source.bounds;
        if (bounds.size.x < 0.05f || bounds.size.y < 0.05f) return;
        int w = Mathf.Clamp(Mathf.RoundToInt(bounds.size.x * PixelsPerUnit), 4, MaxPixels);
        int h = Mathf.Clamp(Mathf.RoundToInt(bounds.size.y * PixelsPerUnit), 4, MaxPixels);

        var tex = ProceduralSprites.NewTexture(w, h, ledge ? "Rock ledge skin" : "Rock body skin");
        tex.SetPixels32(Paint(w, h, source.color, glow, Mathf.Clamp01(glowDensity), ledge, seed));
        tex.Apply(false, true);
        var spr = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        spr.name = tex.name;

        var go = new GameObject(tex.name);
        go.transform.SetParent(source.transform, false);
        go.transform.position = new Vector3(bounds.center.x, bounds.center.y, source.transform.position.z);
        Vector3 parent = source.transform.lossyScale;
        go.transform.localScale = new Vector3(
            bounds.size.x / (w / PixelsPerUnit) / Safe(parent.x),
            bounds.size.y / (h / PixelsPerUnit) / Safe(parent.y), 1);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = spr;
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder + sortingOffset;
        source.enabled = false;

        var skin = go.AddComponent<TerrainSkin>();
        skin.texture = tex; skin.sprite = spr;
    }

    private void OnDestroy()
    {
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
    }

    private static float Safe(float v) => Mathf.Max(0.0001f, Mathf.Abs(v));

    // ---------- Painting ----------
    // Flat, low-detail style to match the character sprites and parallax: 5 colours per platform,
    // navy-shifted shadows, a bold lit top, a chunky shadow band, a few slab seams and the odd glow vein.

    private static readonly Color Navy = new Color(26 / 255f, 25 / 255f, 50 / 255f, 1);
    private static readonly Color Ink = new Color(14 / 255f, 7 / 255f, 27 / 255f, 1);

    private static Color32[] Paint(int w, int h, Color baseColor, Color glow, float glowDensity, bool ledge, int seed)
    {
        var random = new System.Random(seed);
        baseColor.a = 1; glow.a = 1;
        Color outline = Color.Lerp(baseColor * 0.35f, Ink, 0.6f); outline.a = 1;
        Color shadow = Color.Lerp(baseColor * 0.72f, Navy, 0.35f); shadow.a = 1;
        Color mid = baseColor;
        Color light = Brighten(baseColor, 1.25f, 0.04f);
        Color cap = Brighten(baseColor, 1.55f, 0.1f);

        var pixels = new Color32[w * h];
        var inside = new bool[w * h];

        // Silhouette: chipped corners and a few one/two-pixel steps along the underside.
        int chip = 2 + random.Next(2);
        for (int x0 = 0; x0 < w;)
        {
            int x1 = Mathf.Min(w, x0 + 6 + random.Next(14));
            int[] drops = ledge ? new[] { 0, 0, 1 } : new[] { 0, 1, 1, 2 };
            int drop = drops[random.Next(drops.Length)];
            for (int x = x0; x < x1; x++)
            {
                int edge = Mathf.Min(x, w - 1 - x);
                for (int y = 0; y < h; y++)
                {
                    int fromTop = h - 1 - y;
                    if (y < drop || (edge == 0 && (fromTop < 1 || y < chip)) || (edge < chip && y < chip - edge)) continue;
                    inside[y * w + x] = true;
                }
            }
            x0 = x1;
        }

        // Flat fill with a chunky stepped shadow band in the lower part.
        int band = Mathf.Max(3, (int)(h * (ledge ? 0.32f : 0.45f)));
        var bandEdge = new int[w];
        for (int x0 = 0; x0 < w;)
        {
            int x1 = Mathf.Min(w, x0 + 6 + random.Next(8));
            int[] offsets = { -2, 0, 0, 2 };
            int e = Mathf.Clamp(band + offsets[random.Next(offsets.Length)], 2, Mathf.Max(2, h - 4));
            for (int x = x0; x < x1; x++) bandEdge[x] = e;
            x0 = x1;
        }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!inside[i]) continue;
                bool shaded = y < bandEdge[x] || (!ledge && y >= h - 3); // rock bodies are shaded under the ledge
                pixels[i] = shaded ? shadow : mid;
            }

        // Slab seams: sparse vertical breaks with a lit left edge and one small jog.
        for (int x = 24 + random.Next(26); x < w - 10; x += 36 + random.Next(30))
        {
            int jog = random.Next(3, Mathf.Max(4, h - 3)), shift = random.Next(2) == 0 ? -1 : 1;
            for (int y = 0; y < h - (ledge ? 4 : 3); y++)
            {
                int xx = x + (y < jog ? shift : 0);
                Set(pixels, inside, w, h, xx, y, shadow);
                int left = y * w + xx - 1;
                if (xx - 1 >= 0 && inside[left] && Same(pixels[left], mid)) pixels[left] = light;
            }
        }

        // A couple of chunky pebbles.
        for (int k = 0; k < Mathf.Max(1, w / 60); k++)
        {
            int x = random.Next(3, Mathf.Max(4, w - 5)), y = random.Next(2, Mathf.Max(3, h - 7));
            Set(pixels, inside, w, h, x, y, shadow); Set(pixels, inside, w, h, x + 1, y, shadow);
            Set(pixels, inside, w, h, x, y + 1, light); Set(pixels, inside, w, h, x + 1, y + 1, mid);
        }

        // Occasional bold glow vein (zone colour; cyan on checkpoints).
        if (random.NextDouble() < glowDensity * 0.6f)
        {
            int low = Mathf.Max(1, h / 3);
            int x = random.Next(8, Mathf.Max(9, w - 8)), y = random.Next(low, Mathf.Max(low + 1, h - 8));
            int length = 5 + random.Next(5);
            for (int k = 0; k < length; k++)
            {
                Set(pixels, inside, w, h, x, y, glow); Set(pixels, inside, w, h, x + 1, y, glow);
                y++;
                if (k % 2 == 1) x += random.Next(2) == 0 ? -1 : 1;
            }
        }

        // Outline exposed edges; the walkable top gets a bold lit cap instead.
        var final = (Color32[])pixels.Clone();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!inside[i]) continue;
                bool topEdge = y == h - 1 || !inside[i + w];
                bool exposed = x == 0 || x == w - 1 || y == 0 || !inside[i - 1] || !inside[i + 1] || !inside[i - w] || topEdge;
                if (exposed && !(ledge && topEdge)) final[i] = outline;
            }
        if (ledge)
        {
            for (int x = 0; x < w; x++)
            {
                int top = -1;
                for (int y = h - 1; y >= 0; y--) if (inside[y * w + x]) { top = y; break; }
                if (top < 0) continue;
                bool side = x == 0 || x == w - 1 || !inside[top * w + x - 1] || !inside[top * w + x + 1];
                final[top * w + x] = side ? (Color32)outline : (Color32)cap;
                for (int k = 1; k <= 2; k++)
                {
                    int yy = top - k;
                    if (yy < 0 || !inside[yy * w + x] || Same(final[yy * w + x], outline)) continue;
                    final[yy * w + x] = k == 1 ? cap : light;
                }
            }
        }
        return final;
    }

    private static bool Same(Color32 a, Color b)
    {
        Color32 c = b;
        return a.r == c.r && a.g == c.g && a.b == c.b;
    }

    private static void Set(Color32[] pixels, bool[] inside, int w, int h, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        int i = y * w + x;
        if (inside[i]) pixels[i] = c;
    }

    private static Color Brighten(Color c, float scale, float add)
        => new Color(Mathf.Clamp01(c.r * scale + add), Mathf.Clamp01(c.g * scale + add), Mathf.Clamp01(c.b * scale + add), 1);
}
