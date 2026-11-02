using System;
using System.Collections.Generic;
using AscendJigoku.Gameplay;
using AscendJigoku.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Environmental variation for the streamed pillar (AJ-77):
//  - Height zones change the sky colour, tint generated rock and recolour the backdrop.
//  - Two parallax layers of distant pillars, generated from the run seed (no art assets needed yet).
//  - Ambient embers and an optional zone title when the player climbs into a new zone.
// Added automatically next to VerticalLevelGenerator. Add it to the generator object yourself to tune it in the Inspector.
[DefaultExecutionOrder(1000)] // after CameraFollow, so layers track the final camera position
[RequireComponent(typeof(VerticalLevelGenerator))]
public sealed class PillarEnvironment : MonoBehaviour
{
    [Serializable]
    public sealed class Zone
    {
        public string title = "Zone";
        [Range(0, 1)] public float startsAt;
        public Color sky = Color.black;
        [Tooltip("Multiplies generated rock colour after desaturation.")]
        public Color rockTint = Color.white;
        [Range(0, 1)] public float rockDesaturate;
        public Color farLayer = Color.gray;
        public Color midLayer = Color.gray;
        public Color embers = new Color(1f, 0.5f, 0.15f);
        [Range(0, 1)] public float emberDensity = 1;
    }

    [Tooltip("Ordered by Starts At (fraction of the pillar height). Titles are placeholders.")]
    public Zone[] zones =
    {
        new Zone { title = "THE CINDER PIT", startsAt = 0f, sky = new Color(0.12f, 0.04f, 0.035f), rockTint = new Color(1.08f, 0.86f, 0.8f), rockDesaturate = 0f,
                   farLayer = new Color(0.24f, 0.07f, 0.055f), midLayer = new Color(0.16f, 0.05f, 0.04f), embers = new Color(1f, 0.5f, 0.15f), emberDensity = 1f },
        new Zone { title = "ASHEN STAIR", startsAt = 0.25f, sky = new Color(0.085f, 0.072f, 0.07f), rockTint = new Color(1f, 0.98f, 0.97f), rockDesaturate = 0.7f,
                   farLayer = new Color(0.17f, 0.14f, 0.135f), midLayer = new Color(0.11f, 0.09f, 0.09f), embers = new Color(0.75f, 0.7f, 0.65f), emberDensity = 0.65f },
        new Zone { title = "BONE GALLERY", startsAt = 0.5f, sky = new Color(0.07f, 0.06f, 0.085f), rockTint = new Color(1.3f, 1.2f, 1.02f), rockDesaturate = 0.6f,
                   farLayer = new Color(0.15f, 0.13f, 0.17f), midLayer = new Color(0.1f, 0.085f, 0.12f), embers = new Color(0.95f, 0.88f, 0.72f), emberDensity = 0.35f },
        new Zone { title = "GATE OF JIGOKU", startsAt = 0.75f, sky = new Color(0.05f, 0.03f, 0.075f), rockTint = new Color(0.95f, 0.78f, 1.2f), rockDesaturate = 0.5f,
                   farLayer = new Color(0.14f, 0.06f, 0.19f), midLayer = new Color(0.09f, 0.04f, 0.12f), embers = new Color(0.85f, 0.4f, 1f), emberDensity = 0.55f },
    };
    [Range(0, 0.1f), Tooltip("Blend width either side of a zone boundary, as a fraction of pillar height.")]
    public float zoneBlend = 0.03f;

    [Header("Parallax (0 = moves with the world, 1 = fixed to the screen)")]
    [Range(0, 1)] public float farFactor = 0.9f;
    [Range(0, 1)] public float midFactor = 0.72f;
    public bool parallax = true;

    [Header("Terrain")]
    [Tooltip("Paint generated platforms as pixel-art rock (TerrainSkin). Off = the old flat tinted blocks.")]
    public bool pixelArtTerrain = true;

    [Header("Ambience")]
    public bool ambientEmbers = true;
    [Range(0, 120)] public int maxEmbers = 45;
    public bool showZoneTitles = true;

    public static PillarEnvironment Active { get; private set; }

    private VerticalLevelGenerator generator;
    private Camera view;
    private SpriteRenderer far, mid;
    private Vector2 farTile, midTile;
    private readonly List<Ember> embers = new List<Ember>();
    private Text zoneTitle;
    private float titleAge = float.PositiveInfinity;
    private int highestZoneShown = -1;

    private sealed class Ember { public Transform Transform; public SpriteRenderer Renderer; public Vector2 Velocity; public float Phase, Age, Life, Size; }

    // ---------- Install ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachTo(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachTo(scene);

    private static void AttachTo(Scene scene)
    {
        // Runs before generator Start(), so the first platforms are already tinted.
        var generator = FindAnyObjectByType<VerticalLevelGenerator>();
        if (generator == null || generator.gameObject.scene != scene || generator.GetComponent<PillarEnvironment>() != null) return;
        generator.gameObject.AddComponent<PillarEnvironment>();
    }

    private void Awake()
    {
        Active = this;
        generator = GetComponent<VerticalLevelGenerator>();
        Array.Sort(zones, (a, b) => a.startsAt.CompareTo(b.startsAt));
    }

    private void OnDestroy() { if (Active == this) Active = null; }

    private void Start()
    {
        view = Camera.main;
        int seed = generator != null ? generator.CurrentSeed : 0;
        if (parallax)
        {
            far = CreateLayer("Parallax far", FarTexture(seed), 8, -200, out farTile);
            mid = CreateLayer("Parallax mid", MidTexture(seed ^ 0x5bd1e995), 16, -150, out midTile);
        }
        if (showZoneTitles) CreateTitle();
        LateUpdate();
    }

    // ---------- Zones ----------

    private float PillarHeight
    {
        get
        {
            if (generator == null) return 1200;
            var metrics = generator.Metrics;
            float measured = metrics.LastRow * metrics.RowHeight;
            if (measured > 1) return measured;
            return generator.Settings != null ? generator.Settings.pillarHeight : 1200;
        }
    }

    private float HeightFraction(float worldY) => Mathf.Clamp01((worldY - transform.position.y) / Mathf.Max(1, PillarHeight));

    private int ZoneIndex(float fraction)
    {
        int index = 0;
        for (int i = 0; i < zones.Length; i++) if (fraction >= zones[i].startsAt) index = i;
        return index;
    }

    // Blends the zone below/above near a boundary.
    private void Sample(float fraction, out Zone a, out Zone b, out float t)
    {
        int i = ZoneIndex(fraction);
        a = b = zones[i]; t = 0;
        if (zoneBlend <= 0) return;
        if (i + 1 < zones.Length && fraction > zones[i + 1].startsAt - zoneBlend)
        { b = zones[i + 1]; t = Mathf.InverseLerp(zones[i + 1].startsAt - zoneBlend, zones[i + 1].startsAt + zoneBlend, fraction); }
        else if (i > 0 && fraction < zones[i].startsAt + zoneBlend)
        { a = zones[i - 1]; b = zones[i]; t = Mathf.InverseLerp(zones[i].startsAt - zoneBlend, zones[i].startsAt + zoneBlend, fraction); }
    }

    // Called by the generator for every landing. Deterministic, so rebuilt chunks match.
    public static Color TintRock(Color baseColor, float localHeight)
    {
        var self = Active;
        if (self == null || self.zones == null || self.zones.Length == 0) return baseColor;
        float fraction = Mathf.Clamp01(localHeight / Mathf.Max(1, self.PillarHeight));
        self.Sample(fraction, out var a, out var b, out float t);
        return Color.Lerp(Tint(baseColor, a), Tint(baseColor, b), t);
    }

    public static bool PixelArtTerrain => Active == null || Active.pixelArtTerrain;

    // Lava-vein colour and how often veins appear, following the zone's ember settings.
    public static void GlowAt(float localHeight, out Color color, out float density)
    {
        color = new Color(1f, 0.5f, 0.15f); density = 0.8f;
        var self = Active;
        if (self == null || self.zones == null || self.zones.Length == 0) return;
        self.Sample(Mathf.Clamp01(localHeight / Mathf.Max(1, self.PillarHeight)), out var a, out var b, out float t);
        color = Color.Lerp(a.embers, b.embers, t);
        density = Mathf.Lerp(a.emberDensity, b.emberDensity, t);
    }

    private static Color Tint(Color c, Zone zone)
    {
        float luminance = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
        var grey = new Color(luminance, luminance, luminance, c.a);
        var mixed = Color.Lerp(c, grey, zone.rockDesaturate);
        return new Color(Mathf.Clamp01(mixed.r * zone.rockTint.r), Mathf.Clamp01(mixed.g * zone.rockTint.g), Mathf.Clamp01(mixed.b * zone.rockTint.b), c.a);
    }

    // ---------- Frame ----------

    private void LateUpdate()
    {
        if (view == null) view = Camera.main;
        if (view == null || zones == null || zones.Length == 0) return;
        Vector2 cam = view.transform.position;
        float fraction = HeightFraction(cam.y);
        Sample(fraction, out var a, out var b, out float t);

        view.backgroundColor = Color.Lerp(a.sky, b.sky, t);
        float viewHeight = view.orthographic ? view.orthographicSize * 2 : 20;
        Vector2 viewSize = new Vector2(viewHeight * view.aspect, viewHeight);
        if (far != null) { PlaceLayer(far, farTile, farFactor, cam, viewSize); far.color = Color.Lerp(a.farLayer, b.farLayer, t); }
        if (mid != null) { PlaceLayer(mid, midTile, midFactor, cam, viewSize); mid.color = Color.Lerp(a.midLayer, b.midLayer, t); }

        if (ambientEmbers) UpdateEmbers(cam, viewSize, Color.Lerp(a.embers, b.embers, t), Mathf.Lerp(a.emberDensity, b.emberDensity, t));
        UpdateTitle();
    }

    private static void PlaceLayer(SpriteRenderer layer, Vector2 tile, float factor, Vector2 cam, Vector2 viewSize)
    {
        // Snap the tiled sprite to whole tiles so its content appears to move at (1 - factor) of world speed.
        var size = new Vector2(Mathf.Ceil(viewSize.x / tile.x + 2) * tile.x, Mathf.Ceil(viewSize.y / tile.y + 2) * tile.y);
        if (layer.size != size) layer.size = size;
        float x = cam.x - Mathf.Repeat(cam.x * (1 - factor), tile.x);
        float y = cam.y - Mathf.Repeat(cam.y * (1 - factor), tile.y);
        layer.transform.position = new Vector3(x, y, 10);
    }

    private SpriteRenderer CreateLayer(string name, Texture2D texture, float pixelsPerUnit, int order, out Vector2 tile)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);
        var renderer = go.GetComponent<SpriteRenderer>();
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.sortingOrder = order;
        tile = new Vector2(texture.width / pixelsPerUnit, texture.height / pixelsPerUnit);
        return renderer;
    }

    // ---------- Generated backdrops (seamless in both directions) ----------

    private static Texture2D FarTexture(int seed)
    {
        // Distant spires: tall columns with wobbling edges and a lit rim.
        const int w = 256, h = 256;
        var random = new System.Random(seed);
        var pixels = new Color32[w * h];
        int columns = 4 + random.Next(3);
        for (int c = 0; c < columns; c++)
        {
            float cx = (float)random.NextDouble() * w;
            float half = 6 + (float)random.NextDouble() * 18;
            int f1 = 1 + random.Next(3), f2 = 3 + random.Next(5);
            float p1 = (float)random.NextDouble() * 6.283f, p2 = (float)random.NextDouble() * 6.283f;
            int ledgeEvery = 2 + random.Next(3);
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                float width = half + 4 * Mathf.Sin(6.283f * f1 * v + p1) + 2 * Mathf.Sin(6.283f * f2 * v + p2);
                if (Mathf.Repeat(v * ledgeEvery, 1) < 0.05f) width += 7; // occasional ledge
                for (int x = 0; x < w; x++)
                {
                    float dx = WrapDistance(x + 0.5f, cx, w);
                    if (dx >= width) continue;
                    bool rim = (x + 0.5f - cx + w * 1.5f) % w - w * 0.5f < 0 && dx > width - 2.5f;
                    byte value = rim ? (byte)255 : (byte)170;
                    int index = y * w + x;
                    if (pixels[index].a == 0 || pixels[index].r < value) pixels[index] = new Color32(value, value, value, 255);
                }
            }
        }
        return Finish(pixels, w, h, "Generated far parallax");
    }

    private static Texture2D MidTexture(int seed)
    {
        // Nearer broken pillars: fewer, wider, with dark horizontal fractures.
        const int w = 256, h = 256;
        var random = new System.Random(seed);
        var pixels = new Color32[w * h];
        int columns = 2 + random.Next(2);
        for (int c = 0; c < columns; c++)
        {
            float cx = (float)random.NextDouble() * w;
            float half = 14 + (float)random.NextDouble() * 22;
            int f1 = 1 + random.Next(2), f2 = 4 + random.Next(4);
            float p1 = (float)random.NextDouble() * 6.283f, p2 = (float)random.NextDouble() * 6.283f;
            int cracks = 3 + random.Next(4);
            float crackPhase = (float)random.NextDouble();
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                float width = half + 6 * Mathf.Sin(6.283f * f1 * v + p1) + 3 * Mathf.Sin(6.283f * f2 * v + p2);
                bool crackRow = Mathf.Repeat(v * cracks + crackPhase, 1) < 0.012f;
                for (int x = 0; x < w; x++)
                {
                    float dx = WrapDistance(x + 0.5f, cx, w);
                    if (dx >= width) continue;
                    byte value = crackRow ? (byte)110 : dx > width - 3 ? (byte)230 : (byte)200;
                    pixels[y * w + x] = new Color32(value, value, value, 255);
                }
            }
        }
        return Finish(pixels, w, h, "Generated mid parallax");
    }

    private static float WrapDistance(float x, float center, float width)
    {
        float d = Mathf.Abs(x - center) % width;
        return Mathf.Min(d, width - d);
    }

    private static Texture2D Finish(Color32[] pixels, int w, int h, string name)
    {
        var texture = ProceduralSprites.NewTexture(w, h, name, TextureWrapMode.Repeat);
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    // ---------- Embers ----------

    private void UpdateEmbers(Vector2 cam, Vector2 viewSize, Color color, float density)
    {
        int wanted = Mathf.RoundToInt(maxEmbers * Mathf.Clamp01(density));
        while (embers.Count < wanted) embers.Add(NewEmber(cam, viewSize, true));
        float dt = Time.deltaTime;
        Rect bounds = new Rect(cam - viewSize * 0.5f - Vector2.one * 2, viewSize + Vector2.one * 4);
        for (int i = 0; i < embers.Count; i++)
        {
            var e = embers[i];
            bool retire = i >= wanted;
            e.Age += dt;
            Vector3 p = e.Transform.position;
            p.x += (e.Velocity.x + Mathf.Sin(e.Phase + e.Age * 1.7f) * 0.35f) * dt;
            p.y += e.Velocity.y * dt;
            e.Transform.position = p;
            if (!bounds.Contains(p) || e.Age >= e.Life)
            {
                if (retire) { e.Renderer.enabled = false; continue; }
                Respawn(e, cam, viewSize, !bounds.Contains(p) && Mathf.Abs(p.y - cam.y) > viewSize.y);
            }
            e.Renderer.enabled = !retire || e.Age < e.Life;
            float fade = Mathf.Min(1, e.Age / 0.6f) * Mathf.Clamp01((e.Life - e.Age) / 0.8f);
            var c = color; c.a = 0.75f * fade; e.Renderer.color = c;
            e.Transform.localScale = Vector3.one * e.Size;
        }
    }

    private Ember NewEmber(Vector2 cam, Vector2 viewSize, bool anywhere)
    {
        var go = new GameObject("Ember", typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = ProceduralSprites.Square;
        renderer.sortingOrder = -100;
        var ember = new Ember { Transform = go.transform, Renderer = renderer };
        Respawn(ember, cam, viewSize, anywhere);
        return ember;
    }

    private static void Respawn(Ember e, Vector2 cam, Vector2 viewSize, bool anywhere)
    {
        float x = cam.x + UnityEngine.Random.Range(-0.5f, 0.5f) * viewSize.x;
        float y = anywhere ? cam.y + UnityEngine.Random.Range(-0.5f, 0.5f) * viewSize.y : cam.y - viewSize.y * 0.5f - 1;
        e.Transform.position = new Vector3(x, y, 5);
        e.Velocity = new Vector2(UnityEngine.Random.Range(-0.3f, 0.3f), UnityEngine.Random.Range(0.6f, 1.8f));
        e.Phase = UnityEngine.Random.Range(0f, 6.283f);
        e.Age = 0; e.Life = UnityEngine.Random.Range(4f, 9f); e.Size = UnityEngine.Random.Range(0.06f, 0.14f);
    }

    // ---------- Zone title ----------

    private void CreateTitle()
    {
        var canvas = MenuStyle.CreateCanvas("Zone Title", -40, transform);
        zoneTitle = MenuStyle.Label(canvas, "Zone name", "", new Vector2(0, 210), new Vector2(1100, 70), 40, TextAnchor.MiddleCenter);
        zoneTitle.color = new Color(1, 1, 1, 0);
    }

    private void UpdateTitle()
    {
        if (zoneTitle == null || generator == null || generator.Player == null) return;
        int zone = ZoneIndex(HeightFraction(generator.Player.transform.position.y));
        if (zone > highestZoneShown)
        {
            highestZoneShown = zone;
            zoneTitle.text = zones[zone].title;
            titleAge = string.IsNullOrEmpty(zones[zone].title) ? float.PositiveInfinity : 0;
        }
        if (float.IsPositiveInfinity(titleAge)) return;
        titleAge += Time.unscaledDeltaTime;
        float alpha = titleAge < 0.6f ? titleAge / 0.6f : titleAge < 2.4f ? 1 : Mathf.Clamp01(1 - (titleAge - 2.4f) / 0.9f);
        var c = zoneTitle.color; c.a = alpha * 0.85f; zoneTitle.color = c;
        if (alpha <= 0 && titleAge > 2.4f) titleAge = float.PositiveInfinity;
    }
}
