using AscendJigoku.Gameplay;
using AscendJigoku.Settings;
using UnityEngine;

// Pixel-art rock for generated platforms, painted per platform at the character's 32 px/unit so nothing stretches.
// Deterministic from (run seed, row): a rebuilt chunk looks identical. The platform keeps its own SpriteRenderer
// (disabled) and collider; the skin is a child sprite that may hang below the collider (stalactites, ragged undersides).
// Material follows the height zone (basalt / ash strata / bone-studded rock / obsidian). Glowing cracks live in a second
// sprite whose alpha breathes (steady with Reduce Flashing). Turn it off with PillarEnvironment > Pixel Art Terrain.
[DisallowMultipleComponent]
public sealed class TerrainSkin : MonoBehaviour
{
    public const float PixelsPerUnit = 32;
    private const int MaxPixels = 2048;
    private const int MaxHang = 26;           // px the skin may extend below the collider
    private const float PulseSpeed = 1.6f, PulseDepth = 0.35f, SteadyAlpha = 0.8f;

    private Texture2D texture, glowTexture;
    private Sprite sprite, glowSprite;
    private SpriteRenderer glowRenderer;
    private float phase;

    // Called by the level generator for each platform. Paints a rock sprite sized to source's bounds and hides source.
    // ledge = walkable top (crust + lit top edge). Otherwise a rock body under a ledge (shadowed top). flat = no ragged underside.
    // zoneBlend is how far the platform's colour is pulled toward the zone's rock colour (0 = keep source colour).
    public static void Apply(SpriteRenderer source, int seed, bool ledge, Color glow, float glowDensity, int sortingOffset,
        bool flat = false, float zoneBlend = 0.6f)
    {
        if (source == null) return;
        Bounds bounds = source.bounds;
        if (bounds.size.x < 0.05f || bounds.size.y < 0.05f) return;
        int w = Mathf.Clamp(Mathf.RoundToInt(bounds.size.x * PixelsPerUnit), 4, MaxPixels);
        int h = Mathf.Clamp(Mathf.RoundToInt(bounds.size.y * PixelsPerUnit), 4, MaxPixels);

        var random = new System.Random(seed);
        // pad = extra pixel rows added below the collider for the ragged underside.
        int pad = flat ? 0 : Mathf.Min(MaxHang, (ledge ? 6 : 10) + random.Next(ledge ? 10 : 18));
        int total = h + pad;
        int zone = ZoneOf(source.transform.position.y);

        var canvas = Paint(w, total, pad, source.color, glow, Mathf.Clamp01(glowDensity), ledge, zone, random, zoneBlend);
        var tex = ProceduralSprites.NewTexture(w, total, ledge ? "Rock ledge skin" : "Rock body skin");
        tex.SetPixels32(canvas.Pixels);
        tex.Apply(false);
        var spr = Sprite.Create(tex, new Rect(0, 0, w, total), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        spr.name = tex.name;

        // The sprite is taller than the collider by pad rows, so shift it down by half of that to keep the top aligned.
        float texelY = bounds.size.y / h;
        var go = new GameObject(tex.name);
        go.transform.SetParent(source.transform, false);
        go.transform.position = new Vector3(bounds.center.x, bounds.center.y - pad * 0.5f * texelY, source.transform.position.z);
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
        skin.texture = tex;
        skin.sprite = spr;
        skin.phase = (seed & 1023) * 0.0061f;

        if (canvas.Glow != null)
        {
            var gtex = ProceduralSprites.NewTexture(w, total, "Rock glow skin");
            gtex.SetPixels32(canvas.Glow);
            gtex.Apply(false);
            var gspr = Sprite.Create(gtex, new Rect(0, 0, w, total), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            gspr.name = gtex.name;
            var child = new GameObject(gtex.name);
            child.transform.SetParent(go.transform, false);
            var gr = child.AddComponent<SpriteRenderer>();
            gr.sprite = gspr;
            gr.sharedMaterial = source.sharedMaterial;
            gr.sortingLayerID = source.sortingLayerID;
            gr.sortingOrder = renderer.sortingOrder + 1;
            skin.glowTexture = gtex;
            skin.glowSprite = gspr;
            skin.glowRenderer = gr;
            skin.SetGlowAlpha(SteadyAlpha);
        }
        else skin.enabled = false;   // no glowing cracks, so nothing to animate
    }

    // Breathes the crack glow's alpha; skipped while off screen.
    private void Update()
    {
        if (glowRenderer == null || !glowRenderer.isVisible) return;
        SetGlowAlpha(GameSettings.ReduceFlashing ? SteadyAlpha : 1 - PulseDepth * (0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed + phase)));
    }

    private void SetGlowAlpha(float a)
    {
        var c = glowRenderer.color;
        c.a = a;
        glowRenderer.color = c;
    }

    private void OnDestroy()
    {
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
        if (glowSprite != null) Destroy(glowSprite);
        if (glowTexture != null) Destroy(glowTexture);
    }

    private static float Safe(float v) => Mathf.Max(0.0001f, Mathf.Abs(v));

    private static int ZoneOf(float worldY)
    {
        var generator = VerticalLevelGenerator.Active; // not a Find: the tutorial skins every platform and has no generator
        return PillarEnvironment.ZoneAt(worldY - (generator != null ? generator.transform.position.y : 0));
    }

    // ---------- Painting ----------
    // Working pixel grid, y = 0 at the bottom. Inside marks the rock silhouette, Exposed its outline pixels,
    // Glow (created on demand) holds the glowing-crack pixels for the second sprite.
    private sealed class Canvas
    {
        public int W, H, Top;
        public Color32[] Pixels, Glow;
        public bool[] Inside, Exposed;
    }

    private static readonly Color Ink = new Color(14 / 255f, 7 / 255f, 27 / 255f, 1);
    // Rock colour per zone: basalt, ash, bone-pale stone, obsidian.
    private static readonly Color[] ZoneRock = {
        new Color(0.17f, 0.13f, 0.14f), new Color(0.31f, 0.30f, 0.30f), new Color(0.47f, 0.43f, 0.37f), new Color(0.17f, 0.11f, 0.23f) };
    private static readonly Color Bone = new Color(0.78f, 0.72f, 0.60f), Lacquer = new Color(0.62f, 0.17f, 0.13f), Straw = new Color(0.55f, 0.43f, 0.25f);

    // Paints the whole rock in passes: silhouette, base shading, zone material, glowing cracks, outline and ledge cap.
    // h includes the pad rows; the platform itself occupies the top h - pad rows.
    private static Canvas Paint(int w, int h, int pad, Color source, Color glow, float glowDensity, bool ledge, int zone,
        System.Random r, float zoneBlend)
    {
        var c = new Canvas
        {
            W = w, H = h, Top = h - 1, Pixels = new Color32[w * h], Inside = new bool[w * h], Exposed = new bool[w * h]
        };
        int top = c.Top;
        glow.a = 1;
        Color baseColor = Color.Lerp(source, ZoneRock[Mathf.Clamp(zone, 0, 3)], zoneBlend) * (0.9f + 0.2f * (float)r.NextDouble());
        baseColor.a = 1;
        // Five tones from the base colour: dark (outline) < shadow < mid (fill) < light < cap (walkable top).
        Color mid = baseColor, shadow = Color.Lerp(baseColor * 0.65f, Ink, 0.3f), dark = Color.Lerp(baseColor * 0.4f, Ink, 0.5f);
        Color light = Brighten(baseColor, 1.3f, 0.03f), cap = Brighten(baseColor, 1.65f, 0.1f);
        shadow.a = dark.a = 1;
        if (zone == 3) cap = Color.Lerp(Lacquer, cap, 0.25f);

        // Silhouette: flat walkable top, stepped/jagged underside with hanging spikes, ragged chipped sides with bites.
        // bottom[x] is the lowest filled row in column x (higher = shorter column).
        var bottom = new int[w];
        int baseLevel = pad > 0 ? Mathf.RoundToInt(pad * 0.55f) : 0, range = pad > 0 ? Mathf.Max(2, pad / 2) : 3;
        // Underside steps: runs of 3-10 columns share one random level.
        for (int x0 = 0; x0 < w;)
        {
            int x1 = Mathf.Min(w, x0 + 3 + r.Next(8));
            int level = Mathf.Max(0, baseLevel + r.Next(range) - range / 2);
            for (int x = x0; x < x1; x++)
                bottom[x] = level;

            x0 = x1;
        }
        // Stalactites: triangular dips cut down from the underside.
        int spikes = pad > 0 ? Mathf.Max(1, w / (11 + r.Next(10))) : 0;
        for (int k = 0; k < spikes; k++)
        {
            int cx = r.Next(w), half = 1 + r.Next(3), len = Mathf.Max(2, Mathf.RoundToInt(bottom[cx] * (0.6f + 0.4f * (float)r.NextDouble())));
            for (int x = Mathf.Max(0, cx - half); x <= Mathf.Min(w - 1, cx + half); x++)
                bottom[x] = Mathf.Max(0, bottom[x] - Mathf.RoundToInt(len * (1f - Mathf.Abs(x - cx) / (half + 1f))));
        }
        for (int x = 0; x < w; x++)
            bottom[x] = Mathf.Min(bottom[x], top - 3);

        // left[y] / right[y] = how many pixels each side is shaved in at row y (random walk, plus bites chipped out).
        var left = new int[h];
        var right = new int[h];
        int maxInset = Mathf.Max(1, w / 5);
        for (int side = 0; side < 2; side++)
        {
            var inset = side == 0 ? left : right;
            int v = r.Next(2);
            for (int y = top; y >= 0; y--)
            {
                if (top - y >= 2 && r.Next(3) == 0) v = Mathf.Clamp(v + r.Next(4) - 1, 0, 3);
                inset[y] = y < pad ? v + (pad - y) / 4 : v;
            }
            for (int n = 0, bites = 1 + w / 70 + r.Next(2); n < bites; n++)
            {
                int y0 = r.Next(Mathf.Max(1, top - 3)), hh = 3 + r.Next(6), depth = 2 + r.Next(3);
                for (int y = y0; y < Mathf.Min(top - 1, y0 + hh); y++) inset[y] = Mathf.Max(inset[y], depth);
            }
            for (int y = 0; y <= top; y++) inset[y] = Mathf.Min(inset[y], maxInset);
        }
        for (int y = 0; y < h; y++)
            for (int x = left[y]; x < w - right[y]; x++)
                if (y >= bottom[x]) c.Inside[y * w + x] = true;

        if (zone == 1) // crumbling edges: randomly knock out ~28% of the outline pixels
        {
            var copy = (bool[])c.Inside.Clone();
            for (int y = 1; y < top - 2; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int i = y * w + x;
                    if (copy[i] && (!copy[i - 1] || !copy[i + 1] || !copy[i - w]) && r.Next(100) < 28) c.Inside[i] = false;
                }
        }

        // Base shading: lit left faces, shadowed right faces and a stepped shadow along the underside.
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!c.Inside[i]) continue;
                Color col = mid;
                if (y - bottom[x] < 2 + (x / 5) % 3 || (!ledge && top - y < 3)) col = shadow;
                else if (x + 1 >= w || !c.Inside[i + 1]) col = shadow;
                else if (x == 0 || !c.Inside[i - 1]) col = light;
                c.Pixels[i] = col;
            }

        // A few flat 2x1 chips instead of per-pixel noise, to match the flat character sprites.
        for (int k = 0, chips = w * h / 220; k < chips; k++)
        {
            int x = r.Next(Mathf.Max(1, w - 2)), y = r.Next(Mathf.Max(1, top - 3));
            if (y - bottom[x] < 3) continue;
            Color chip = r.Next(3) == 0 ? light : shadow;
            Set(c, x, y, chip);
            Set(c, x + 1, y, chip);
        }

        // Zone material.
        float phase = (float)r.NextDouble() * 6.28f;
        switch (zone)
        {
            case 0: // basalt columns: dark/light seams every 7-11 px, stepping sideways by 1 px every few rows
                for (int x = 5 + r.Next(5), col = 0; x < w - 3; x += 7 + r.Next(5), col++)
                    for (int y = 0; y < top - 2; y++)
                    {
                        int xx = x + ((y / (3 + col % 3)) & 1);
                        Set(c, xx, y, dark);
                        Set(c, xx + 1, y, light);
                    }
                break;
            case 1: // layered ash strata
                for (int y = 0; y < top - 2; y++)
                    for (int x = 0; x < w; x++)
                    {
                        // 3-px wavy bands; each band gets a hash that makes it dark (2/8), light (1/8) or plain.
                        int band = Mathf.FloorToInt((y + 2.2f * Mathf.Sin(x * 0.11f + phase)) / 3f);
                        int hash = (band * 2654435 ^ 0x5bd1) & 7;
                        if (hash < 2) Set(c, x, y, shadow);
                        else if (hash == 7) Set(c, x, y, light);
                    }
                break;
            case 2: // bones set into pale rock
                for (int k = 0, n = Mathf.Max(1, w / 34); k < n; k++)
                    Fossil(c, r, 4 + r.Next(Mathf.Max(1, w - 14)), Mathf.Min(top - 5, pad + 3 + r.Next(Mathf.Max(1, h - pad - 8))), glow);
                break;
            case 3: // obsidian sheen
                int sheenPhase = r.Next(13);
                for (int y = 0; y < top - 2; y++)
                    for (int x = 0; x < w; x++)
                        // Diagonal sheen lines every 13 px, only in alternating 24-px wide stripes.
                        if ((x + y + sheenPhase) % 13 == 0 && (x / 24) % 2 == 0)
                            Set(c, x, y, Color.Lerp(mid, new Color(0.5f, 0.25f, 0.6f), 0.5f));
                if (ledge && h - pad >= 9 && w >= 56 && r.Next(2) == 0) Shimenawa(c, r, top);
                break;
        }

        // Glowing cracks (also leaking into the pale zones as a few dim veins). The expected count scales with width;
        // the fractional part becomes a chance of one extra crack.
        float cracks = (zone == 0 ? 0.9f : zone == 1 ? 0.35f : 0.5f) * (0.4f + glowDensity) * w / 48f;
        for (int k = 0, n = Mathf.FloorToInt(cracks) + (r.NextDouble() < cracks - Mathf.Floor(cracks) ? 1 : 0); k < n; k++)
        {
            int x = 3 + r.Next(Mathf.Max(1, w - 6));
            Crack(c, r, glow, x, bottom[x] + 1, 5 + r.Next(Mathf.Max(2, h - 6)), false, mid);
        }

        // Exposed edges: outline (zone 3 undersides get lacquer); the walkable top gets a bold lit cap instead.
        var final = (Color32[])c.Pixels.Clone();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!c.Inside[i]) continue;
                bool topEdge = y == top || !c.Inside[i + w];
                bool under = y == 0 || !c.Inside[i - w];
                bool exposed = x == 0 || x == w - 1 || under || !c.Inside[i - 1] || !c.Inside[i + 1] || topEdge;
                c.Exposed[i] = exposed;
                if (!exposed || (ledge && topEdge)) continue;
                final[i] = zone == 3 && under && ledge ? Lacquer * 0.7f : dark;
                if (c.Glow != null) c.Glow[i] = default;
            }
        // Walkable top: per column, the topmost filled pixel becomes the bright cap (dark where the side is exposed),
        // with two lit rows fading down below it.
        if (ledge)
            for (int x = 0; x < w; x++)
            {
                int ty = -1;
                for (int y = top; y >= 0; y--)
                    if (c.Inside[y * w + x])
                    {
                        ty = y;
                        break;
                    }

                if (ty < 0) continue;
                bool side = x == 0 || x == w - 1 || !c.Inside[ty * w + x - 1] || !c.Inside[ty * w + x + 1];
                final[ty * w + x] = side ? (Color32)dark : (zone == 1 && x % 5 == 2 ? (Color32)light : (Color32)cap);
                for (int k = 1; k <= 2; k++)
                {
                    int yy = ty - k;
                    if (yy < 0 || !c.Inside[yy * w + x] || c.Exposed[yy * w + x]) continue;
                    if (c.Glow != null && c.Glow[yy * w + x].a > 0) continue;
                    final[yy * w + x] = k == 1
                        ? (zone == 3 ? (Color32)Color.Lerp(Lacquer, shadow, 0.55f) : (Color32)light)
                        : (Color32)Color.Lerp(light, mid, 0.5f);
                }
            }

        if (zone == 1 && pad >= 8) // loose rubble hanging below
            for (int k = 0, n = 1 + w / 50; k < n; k++)
            {
                int x = 2 + r.Next(Mathf.Max(1, w - 5)), y = r.Next(Mathf.Max(1, bottom[x] - 4));
                if (bottom[x] < 6 || c.Inside[y * w + x] || c.Inside[(y + 2) * w + x]) continue;
                for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                        final[(y + dy) * w + x + dx] = dy == 1 ? mid : dark;
            }

        // Force every painted pixel fully opaque.
        for (int i = 0; i < final.Length; i++)
            if (final[i].a > 0) final[i].a = 255;

        c.Pixels = final;
        return c;
    }

    // Paints a pixel only if it is inside the rock.
    private static void Set(Canvas c, int x, int y, Color col)
    {
        if (x < 0 || y < 0 || x >= c.W || y >= c.H) return;
        int i = y * c.W + x;
        if (c.Inside[i]) c.Pixels[i] = col;
    }

    // Paints a glowing pixel: a dim version on the main sprite and full brightness on the separate glow layer.
    private static void Glow(Canvas c, int x, int y, Color glow, Color baseCol)
    {
        if (x < 0 || y < 0 || x >= c.W || y >= c.H) return;
        int i = y * c.W + x;
        if (!c.Inside[i]) return;
        c.Pixels[i] = Color.Lerp(glow * 0.55f, baseCol, 0.25f);
        c.Glow ??= new Color32[c.W * c.H];
        c.Glow[i] = new Color(glow.r, glow.g, glow.b, 1);
    }

    // Random-walks upward from (x, y) leaving glowing pixels; may spawn one half-length branch.
    private static void Crack(Canvas c, System.Random r, Color glow, int x, int y, int len, bool branch, Color baseCol)
    {
        for (int k = 0; k < len && y < c.Top - 2; k++)
        {
            Glow(c, x, y, glow, baseCol);
            if (k % 3 == 2) Glow(c, x + 1, y, glow, baseCol);
            y++;
            x += r.Next(3) - 1;
            if (!branch && len > 6 && r.Next(10) == 0) Crack(c, r, glow, x, y, len / 2, true, baseCol);
        }
    }

    // Bone embedded in the rock: a skull, a femur or a set of ribs, picked at random.
    private static void Fossil(Canvas c, System.Random r, int x, int y, Color glow)
    {
        Color bone = Bone * (0.85f + 0.15f * (float)r.NextDouble());
        bone.a = 1;
        Color dim = bone * 0.7f;
        dim.a = 1;
        switch (r.Next(3))
        {
            case 0: // skull
                // X = bone, O = eye socket / nose hole, . = empty; rows are listed top to bottom.
                string[] skull = { ".XXXX.", "XXXXXX", "XOXXOX", "XXXXXX", ".XOOX." };
                for (int j = 0; j < skull.Length; j++)
                    for (int i = 0; i < 6; i++)
                    {
                        char ch = skull[j][i];
                        if (ch == '.') continue;
                        int px = x + i, py = y + skull.Length - 1 - j;
                        if (ch == 'X') Set(c, px, py, i == 0 || j == 4 ? dim : bone);
                        else if (j == 2 && r.Next(2) == 0) Glow(c, px, py, glow * 0.8f, Ink);
                        else Set(c, px, py, Ink);
                    }
                break;
            case 1: // femur
                int len = 7 + r.Next(5), slope = r.Next(3) - 1;
                for (int i = 0; i < len; i++)
                    Set(c, x + i, y + i * slope / 4, i % 4 == 3 ? dim : bone);

                // Knobbed ends.
                foreach (int end in new[] { 0, len - 1 })
                    for (int d = 0; d < 2; d++)
                    {
                        Set(c, x + end, y + end * slope / 4 + d, bone);
                        Set(c, x + end + (end == 0 ? -1 : 1), y + end * slope / 4 + d, bone);
                    }
                break;
            default: // ribs
                for (int k = 0; k < 3; k++)
                    for (int i = 0; i < 5; i++)
                        Set(c, x + k * 3 + (i == 0 || i == 4 ? 0 : 1), y + i, k == 1 ? dim : bone);
                break;
        }
    }

    // Sagging straw rope across the top of an obsidian ledge, with a paper charm hanging every 15 px.
    // sag * 4 * t * (1 - t) is a parabola: 0 at both ends, `sag` pixels at the middle.
    private static void Shimenawa(Canvas c, System.Random r, int top)
    {
        int x0 = 6 + r.Next(Mathf.Max(1, c.W / 6)), x1 = c.W - 6 - r.Next(Mathf.Max(1, c.W / 6)), baseY = top - 4;
        float sag = 2 + r.Next(3);
        Color paper = new Color(0.78f, 0.75f, 0.66f);
        for (int x = x0; x <= x1; x++)
        {
            float t = (x - x0) / (float)Mathf.Max(1, x1 - x0);
            int y = baseY - Mathf.RoundToInt(sag * 4 * t * (1 - t));
            Set(c, x, y, x % 4 < 2 ? Straw : Straw * 0.7f);
            Set(c, x, y + 1, x % 4 < 2 ? Straw * 0.7f : Straw);
            if ((x - x0) % 15 == 7)
                for (int k = 1; k <= 4; k++)
                    Set(c, x + (k % 2), y - k, paper);
        }
    }

    private static Color Brighten(Color c, float scale, float add)
        => new Color(Mathf.Clamp01(c.r * scale + add), Mathf.Clamp01(c.g * scale + add), Mathf.Clamp01(c.b * scale + add), 1);
}
