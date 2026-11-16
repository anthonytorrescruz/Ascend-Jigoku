using System;
using System.Collections.Generic;
using AscendJigoku.Gameplay;
using AscendJigoku.Settings;
using AscendJigoku.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Environmental variation for the streamed pillar (AJ-77):
//  - Height zones change the sky colour, tint generated rock and recolour the backdrop.
//  - Layered zone scenery (BackdropArt): far / mid / near silhouettes that crossfade between zones, sky glow, lava falls, drifting fog.
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
    [Range(0, 1)] public float nearFactor = 0.45f;
    public bool parallax = true;

    [Header("Backdrop")]
    [Range(0, 1)] public float glowStrength = 1f;
    [Range(0, 1)] public float fogStrength = 1f;
    [Tooltip("Units per second.")] public float fogDrift = 0.35f, fallSpeed = 1.6f;

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
    private const int LayerCount = 3;
    private static readonly int[] LayerPpu = { 8, 12, 16 };
    private static readonly int[] LayerOrder = { -200, -150, -110 };
    private static readonly float[] ZoneGlow = { 0.34f, 0.07f, 0.07f, 0.16f };
    private readonly SpriteRenderer[,] layers = new SpriteRenderer[LayerCount, 2];
    private readonly Sprite[,] layerSprites = new Sprite[LayerCount, 4];
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    private SpriteRenderer glow, falls, fog, fogNear, gate;
    private int seed;
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

    private void OnDestroy()
    {
        if (Active == this) Active = null;
        foreach (var o in owned) if (o != null) Destroy(o);
    }

    private void Start()
    {
        view = Camera.main;
        seed = generator != null ? generator.CurrentSeed : 0;
        if (parallax) BuildBackdrop();
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

    // Zone index (0 = THE CINDER PIT ... 3 = GATE OF JIGOKU) at a height above the pillar base. 0 when no environment.
    public static int ZoneAt(float localHeight)
    {
        var self = Active;
        if (self == null || self.zones == null || self.zones.Length == 0) return 0;
        return self.ZoneIndex(Mathf.Clamp01(localHeight / Mathf.Max(1, self.PillarHeight)));
    }

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
        if (parallax) UpdateBackdrop(cam, viewSize, fraction, a, b, t);

        if (ambientEmbers) UpdateEmbers(cam, viewSize, Color.Lerp(a.embers, b.embers, t), Mathf.Lerp(a.emberDensity, b.emberDensity, t));
        UpdateTitle();
    }

    private static void PlaceLayer(SpriteRenderer layer, Vector2 tile, float factor, Vector2 cam, Vector2 viewSize, Vector2 scroll)
    {
        // Snap the tiled sprite to whole tiles so its content appears to move at (1 - factor) of world speed.
        var size = new Vector2(Mathf.Ceil(viewSize.x / tile.x + 2) * tile.x, Mathf.Ceil(viewSize.y / tile.y + 2) * tile.y);
        if (layer.size != size) layer.size = size;
        float x = cam.x - Mathf.Repeat(cam.x * (1 - factor) + scroll.x, tile.x);
        float y = cam.y - Mathf.Repeat(cam.y * (1 - factor) + scroll.y, tile.y);
        layer.transform.position = new Vector3(x, y, 10);
    }

    // ---------- Backdrop ----------

    private Sprite MakeSprite(Texture2D texture, int ppu)
    {
        var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        owned.Add(texture); owned.Add(sprite);
        return sprite;
    }

    private SpriteRenderer NewRenderer(string name, Sprite sprite, int order, bool tiled)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        if (tiled) { renderer.drawMode = SpriteDrawMode.Tiled; renderer.tileMode = SpriteTileMode.Continuous; }
        renderer.sortingOrder = order;
        return renderer;
    }

    private void BuildBackdrop()
    {
        glow = NewRenderer("Sky glow", MakeSprite(BackdropArt.Glow(), 1), -210, false);
        falls = NewRenderer("Lava falls", MakeSprite(BackdropArt.Falls(seed, Accents0), 8), -190, true);
        var fogTexture = BackdropArt.Fog(seed);
        fog = NewRenderer("Fog far", MakeSprite(fogTexture, 8), -120, true);
        fogNear = NewRenderer("Fog near", MakeSprite(fogTexture, 6), -92, true);
        gate = NewRenderer("Giant gate", MakeSprite(BackdropArt.GiantGate(zones[zones.Length - 1].sky), 8), -195, false);
        for (int l = 0; l < LayerCount; l++)
            for (int k = 0; k < 2; k++) layers[l, k] = NewRenderer($"Backdrop {l}{(k == 0 ? "a" : "b")}", ZoneSprite(l, 0), LayerOrder[l] + k, true);
    }

    private Sprite ZoneSprite(int layer, int zone)
    {
        if (layerSprites[layer, zone] != null) return layerSprites[layer, zone];
        var z = zones[zone];
        return layerSprites[layer, zone] = MakeSprite(BackdropArt.Layer(zone, layer, seed, z.sky, LayerTone(z, layer)), LayerPpu[layer]);
    }

    private static Color LayerTone(Zone zone, int layer) => layer == 0 ? zone.farLayer : layer == 1 ? zone.midLayer : Color.Lerp(zone.midLayer, Color.black, 0.35f);

    private void UpdateBackdrop(Vector2 cam, Vector2 viewSize, float fraction, Zone a, Zone b, float t)
    {
        int ia = Mathf.Min(Array.IndexOf(zones, a), 3), ib = Mathf.Min(Array.IndexOf(zones, b), 3);
        float time = Time.time;
        float pulse = GameSettings.ReduceFlashing ? 1 : 1 + 0.12f * Mathf.Sin(time * 0.8f);
        float[] factors = { farFactor, midFactor, nearFactor };
        for (int l = 0; l < LayerCount; l++)
        {
            var tile = Vector2.one * (BackdropArt.Size / (float)LayerPpu[l]);
            var back = layers[l, 0]; var front = layers[l, 1];
            back.sprite = ZoneSprite(l, ia);
            front.sprite = ZoneSprite(l, ib);
            front.enabled = ib != ia && t > 0.01f;
            front.color = new Color(1, 1, 1, t);
            PlaceLayer(back, tile, factors[l], cam, viewSize, Vector2.zero);
            if (front.enabled) PlaceLayer(front, tile, factors[l], cam, viewSize, Vector2.zero);
        }

        // Sky glow: zone colour from below, pooled magma near the pillar base.
        var glowColor = Color.Lerp(a.embers, b.embers, t);
        float strength = Mathf.Lerp(ZoneGlow[ia], ZoneGlow[ib], t) + 0.3f * Mathf.Clamp01(1 - fraction / 0.08f);
        glowColor.a = Mathf.Clamp01(strength * glowStrength * pulse);
        glow.color = glowColor;
        glow.transform.position = new Vector3(cam.x, cam.y, 10);
        glow.transform.localScale = new Vector3(viewSize.x * 1.4f, viewSize.y * 1.4f / 64, 1); // oversized so its edges never show

        // Lava falls belong to the Cinder Pit.
        float lava = (ia == 0 ? 1 - t : 0) + (ib == 0 ? t : 0);
        falls.enabled = lava > 0.01f;
        if (falls.enabled)
        {
            falls.color = new Color(Accents0.r, Accents0.g, Accents0.b, 0.5f * lava * (0.85f + 0.15f * pulse));
            PlaceLayer(falls, Vector2.one * (BackdropArt.Size / 8f), farFactor, cam, viewSize, new Vector2(0, time * fallSpeed));
        }

        // Fog: ash and purple haze are heaviest; the pit and bone zones get thin bands.
        var fogColor = Color.Lerp(a.farLayer, b.farLayer, t) * 1.7f;
        float density = Mathf.Lerp(FogDensity[ia], FogDensity[ib], t) * fogStrength;
        fogColor.a = 0.22f * density;
        fog.color = fogColor;
        fogNear.color = new Color(fogColor.r, fogColor.g, fogColor.b, 0.1f * density);
        PlaceLayer(fog, new Vector2(32, 16), 0.6f, cam, viewSize, new Vector2(time * fogDrift, 0));
        PlaceLayer(fogNear, new Vector2(256 / 6f, 128 / 6f), 0.3f, cam, viewSize, new Vector2(-time * fogDrift * 1.6f, 0));

        // Giant gate hangs above the top of the pillar, rising into view as the camera climbs.
        float gateY = transform.position.y + PillarHeight * 1.01f;
        float gateAlpha = Mathf.Clamp01((fraction - 0.7f) * 10);
        gate.enabled = gateAlpha > 0;
        if (gate.enabled)
        {
            gate.transform.position = new Vector3(cam.x, cam.y + (gateY - cam.y) * (1 - farFactor), 10);
            gate.color = new Color(1, 1, 1, gateAlpha);
        }
    }

    private static readonly Color Accents0 = new Color(0.95f, 0.38f, 0.12f);
    private static readonly float[] FogDensity = { 0.5f, 1f, 0.6f, 0.9f };

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
