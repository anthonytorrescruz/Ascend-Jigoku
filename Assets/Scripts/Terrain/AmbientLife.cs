using System;
using System.Collections.Generic;
using AscendJigoku.Gameplay;
using AscendJigoku.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pooled, purely cosmetic ambient life around the camera, mixed by the zone the camera is in:
// sparks, lava bubbles and fire bursts (Cinder Pit), ash and crows (Ashen Stair), wisps and dust (Bone Gallery),
// lanterns, ofuda, hitodama and distant lightning (Gate of Jigoku). Added automatically next to VerticalLevelGenerator.
[DefaultExecutionOrder(1001)]
[RequireComponent(typeof(VerticalLevelGenerator))]
public sealed class AmbientLife : MonoBehaviour
{
    [SerializeField, Range(0f, 2f)] private float density = 1f;
    [SerializeField] private int poolSize = 72;
    [SerializeField] private float zoneBlendHeight = 14f;   // cross-fade half-width around zone borders, world units
    [SerializeField, Range(0f, 0.25f)] private float foregroundAlpha = 0.18f;
    [SerializeField] private bool lightning = true;

    private enum Kind { Spark, Bubble, Burst, Ash, Crow, Wisp, Mote, Lantern, Ofuda, Hitodama }

    private sealed class Particle
    {
        public Transform T; public SpriteRenderer R;
        public Kind Kind; public Vector2 Vel; public float Age, Life, Alpha, Size, Phase, Spin;
    }

    private const int Zones = 4;
    // Spawns per second at full zone weight, per zone: kind and rate.
    private static readonly (Kind kind, float rate)[][] Spawns =
    {
        new[] { (Kind.Spark, 5f), (Kind.Bubble, 1.2f), (Kind.Burst, 0.12f) },
        new[] { (Kind.Ash, 4f) },
        new[] { (Kind.Wisp, 0.5f), (Kind.Mote, 2.5f) },
        new[] { (Kind.Lantern, 0.6f), (Kind.Ofuda, 0.8f), (Kind.Hitodama, 0.5f) },
    };
    private const float CrowFlockRate = 0.07f;   // per second in the Ashen Stair
    private const float LightningEvery = 14f;     // average seconds in the Gate of Jigoku

    private static Sprite dot, comet, crowUp, crowFlat, lantern, ofuda, flake;
    private static readonly Color Ember = new Color(0.94f, 0.54f, 0.24f), Bone = new Color(0.95f, 0.89f, 0.8f);

    private readonly List<Particle> pool = new List<Particle>();
    private readonly float[] weight = new float[Zones];
    private readonly float[] carry = new float[16];
    private float weightTimer, crowCarry, nextLightning;
    private SpriteRenderer flash;
    private float flashAge = 9f;
    private VerticalLevelGenerator generator;
    private Camera view;
    private Transform root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { dot = comet = crowUp = crowFlat = lantern = ofuda = flake = null; }

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
        var found = FindAnyObjectByType<VerticalLevelGenerator>();
        if (found == null || found.gameObject.scene != scene || found.GetComponent<AmbientLife>() != null) return;
        found.gameObject.AddComponent<AmbientLife>();
    }

    private void Awake() { generator = GetComponent<VerticalLevelGenerator>(); BuildSprites(); }

    private void Start()
    {
        root = new GameObject("Ambient life").transform;
        root.SetParent(transform, false);
        for (int i = 0; i < poolSize; i++) pool.Add(NewParticle());
        flash = NewParticle().R;
        flash.transform.localScale = new Vector3(60, 40, 1);
        flash.sortingOrder = -140;
        flash.color = Color.clear;
        nextLightning = UnityEngine.Random.Range(6f, LightningEvery);
        view = Camera.main;
        if (view != null) { UpdateWeights(); Prewarm(); }
    }

    private Particle NewParticle()
    {
        var go = new GameObject("Life");
        go.transform.SetParent(root, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = dot; r.color = Color.clear;
        go.SetActive(false);
        return new Particle { T = go.transform, R = r };
    }

    private void Prewarm()
    {
        for (int i = 0; i < 40; i++) { Spawn(Time.deltaTime + 0.25f); }
        foreach (var p in pool) if (p.T.gameObject.activeSelf) p.Age = UnityEngine.Random.value * p.Life * 0.6f;
    }

    private void Update()
    {
        if (view == null) { view = Camera.main; if (view == null) return; }
        float dt = Time.deltaTime;
        if ((weightTimer -= dt) <= 0) { weightTimer = 0.25f; UpdateWeights(); }
        Spawn(dt);
        Vector3 cam = view.transform.position;
        float halfH = view.orthographicSize;
        float halfW = halfH * view.aspect;
        foreach (var p in pool) if (p.T.gameObject.activeSelf) Step(p, dt, cam, halfW, halfH);
        UpdateLightning(dt, cam);
    }

    private void UpdateWeights()
    {
        float h = view.transform.position.y - transform.position.y;
        Array.Clear(weight, 0, Zones);
        const int samples = 5;
        for (int i = 0; i < samples; i++)
        {
            float offset = (i / (samples - 1f) * 2f - 1f) * zoneBlendHeight;
            weight[Mathf.Clamp(PillarEnvironment.ZoneAt(h + offset), 0, Zones - 1)] += 1f / samples;
        }
    }

    // ---------- Spawning ----------

    private void Spawn(float dt)
    {
        Vector3 cam = view.transform.position;
        float halfH = view.orthographicSize, halfW = halfH * view.aspect;
        int slot = 0;
        for (int z = 0; z < Zones; z++)
            foreach (var (kind, rate) in Spawns[z])
            {
                carry[slot] += rate * weight[z] * density * dt;
                while (carry[slot] >= 1f) { carry[slot] -= 1f; Emit(kind, cam, halfW, halfH); }
                slot++;
            }
        crowCarry += CrowFlockRate * weight[1] * density * dt;
        if (crowCarry >= 1f) { crowCarry = 0; EmitFlock(cam, halfW, halfH); }
    }

    private Particle Take()
    {
        foreach (var p in pool) if (!p.T.gameObject.activeSelf) return p;
        return null;
    }

    private static float Rand(float a, float b) => UnityEngine.Random.Range(a, b);

    private void Emit(Kind kind, Vector3 cam, float halfW, float halfH)
    {
        var p = Take();
        if (p == null) return;
        float x = cam.x + Rand(-halfW, halfW) * 1.05f;
        float bottom = cam.y - halfH * 1.05f, top = cam.y + halfH * 1.05f;
        float y = cam.y + Rand(-halfH, halfH);
        Color color = Color.white; int order = (int)Rand(-130, -100);
        var sprite = dot;
        Vector2 vel = Vector2.zero; float life = 6, alpha = 0.5f, size = 0.3f, spin = 0;
        switch (kind)
        {
            case Kind.Spark:
                y = bottom; vel = new Vector2(Rand(-0.6f, 0.6f), Rand(2.5f, 5f)); life = Rand(2.5f, 4.5f);
                alpha = Rand(0.35f, 0.7f); size = Rand(0.08f, 0.16f); order = -110;
                color = Color.Lerp(Ember, new Color(1f, 0.8f, 0.4f), Rand(0, 1));
                break;
            case Kind.Bubble:
                y = cam.y + Rand(-halfH, -halfH * 0.2f); life = Rand(0.9f, 1.5f);
                alpha = 0.45f; size = Rand(0.25f, 0.6f); order = -105; color = new Color(1f, 0.45f, 0.15f);
                break;
            case Kind.Burst:
                y = cam.y + Rand(-halfH, 0); vel = new Vector2(0, Rand(2f, 4f)); life = Rand(1.2f, 2f);
                alpha = 0.2f; size = Rand(1.5f, 2.6f); order = -125; color = new Color(1f, 0.4f, 0.12f);
                break;
            case Kind.Ash:
                if (Rand(0, 1) < 0.15f)
                { order = Rand(0, 1) < 0.5f ? 20 : 21; alpha = foregroundAlpha * Rand(0.6f, 1f); size = Rand(0.5f, 0.8f); }
                else { alpha = Rand(0.2f, 0.5f); size = Rand(0.8f, 1.4f); }
                y = top; vel = new Vector2(Rand(-0.5f, 0.2f), -Rand(0.5f, 1.2f)); life = Rand(7f, 12f);
                sprite = flake; spin = Rand(-60f, 60f);
                color = Color.Lerp(new Color(0.55f, 0.5f, 0.47f), new Color(0.35f, 0.3f, 0.28f), Rand(0, 1));
                break;
            case Kind.Wisp:
                vel = new Vector2(Rand(-0.7f, 0.7f), Rand(-0.3f, 0.4f)); life = Rand(7f, 12f);
                alpha = 0.5f; size = Rand(1f, 1.5f); order = -118; sprite = comet; color = new Color(0.78f, 0.9f, 0.9f);
                break;
            case Kind.Mote:
                vel = new Vector2(Rand(-0.2f, 0.2f), Rand(-0.15f, 0.25f)); life = Rand(5f, 9f);
                alpha = Rand(0.15f, 0.35f); size = Rand(0.08f, 0.14f); color = Bone;
                break;
            case Kind.Lantern:
                y = bottom; vel = new Vector2(Rand(-0.2f, 0.3f), Rand(0.5f, 0.9f)); life = Rand(12f, 18f);
                alpha = 0.7f; size = Rand(0.8f, 1.1f); order = -112; sprite = lantern;
                break;
            case Kind.Ofuda:
                y = top; vel = new Vector2(Rand(-0.6f, -0.1f), -Rand(0.3f, 0.7f)); life = Rand(10f, 15f);
                alpha = 0.55f; size = Rand(0.8f, 1.2f); order = -115; sprite = ofuda; spin = Rand(-25f, 25f);
                break;
            case Kind.Hitodama:
                vel = new Vector2(Rand(-0.8f, 0.8f), Rand(0.1f, 0.6f)); life = Rand(7f, 11f);
                alpha = 0.6f; size = Rand(1f, 1.4f); order = -108; sprite = comet; color = new Color(0.7f, 0.4f, 1f);
                break;
        }
        Place(p, kind, new Vector2(x, y), vel, life, alpha, size, spin, order, sprite, color);
    }

    private void EmitFlock(Vector3 cam, float halfW, float halfH)
    {
        int count = UnityEngine.Random.Range(3, 6);
        float dir = Rand(0, 1) < 0.5f ? -1 : 1, speed = Rand(2f, 3.5f);
        float x = cam.x - dir * (halfW + 2), y = cam.y + Rand(0f, halfH * 0.8f);
        for (int i = 0; i < count; i++)
        {
            var p = Take();
            if (p == null) return;
            Vector2 pos = new Vector2(x - dir * Rand(0, 3f), y + Rand(-0.8f, 0.8f));
            Place(p, Kind.Crow, pos, new Vector2(dir * speed * Rand(0.9f, 1.1f), Rand(-0.1f, 0.2f)),
                (halfW * 2 + 8) / speed, 0.7f, Rand(1.1f, 1.5f), 0, -135, crowUp, new Color(0.1f, 0.06f, 0.06f));
            p.R.flipX = dir < 0;
        }
    }

    private static void Place(Particle p, Kind kind, Vector2 pos, Vector2 vel, float life, float alpha, float size,
        float spin, int order, Sprite sprite, Color color)
    {
        p.Kind = kind; p.Vel = vel; p.Life = life; p.Alpha = alpha; p.Size = size; p.Spin = spin;
        p.Age = 0; p.Phase = Rand(0, 6.28f);
        p.T.position = pos; p.T.rotation = Quaternion.identity; p.T.localScale = Vector3.one * size;
        p.R.sprite = sprite; p.R.sortingOrder = order; p.R.color = new Color(color.r, color.g, color.b, 0); p.R.flipX = false;
        p.T.gameObject.SetActive(true);
    }

    // ---------- Motion ----------

    private void Step(Particle p, float dt, Vector3 cam, float halfW, float halfH)
    {
        p.Age += dt;
        if (p.Age >= p.Life || Mathf.Abs(p.T.position.y - cam.y) > halfH * 2.5f || Mathf.Abs(p.T.position.x - cam.x) > halfW * 2.5f + 6)
        { p.T.gameObject.SetActive(false); return; }

        float t = p.Age / p.Life, time = p.Age + p.Phase;
        Vector2 vel = p.Vel;
        float fade = Mathf.Min(1f, p.Age / 0.6f, (p.Life - p.Age) / 1f), scale = p.Size, alpha = p.Alpha;
        switch (p.Kind)
        {
            case Kind.Spark: vel.x += Mathf.Sin(time * 3f) * 1.2f; alpha *= 1f - t * 0.6f; break;
            case Kind.Bubble: scale *= Mathf.Lerp(0.4f, 1f, Mathf.Min(1f, t * 1.6f)) * (t > 0.8f ? 1.4f : 1f); fade = t > 0.8f ? (1f - t) / 0.2f : Mathf.Min(1f, t / 0.15f); break;
            case Kind.Burst: scale *= 0.5f + t; alpha *= 1f - t; fade = Mathf.Min(1f, p.Age / 0.2f); break;
            case Kind.Ash: vel.x += Mathf.Sin(time * 1.3f) * 0.4f; p.T.Rotate(0, 0, p.Spin * dt); break;
            case Kind.Crow: vel.y += Mathf.Sin(time * 2f) * 0.25f; p.R.sprite = Mathf.Sin(time * 14f) > 0 ? crowUp : crowFlat; fade = 1f; break;
            case Kind.Wisp: case Kind.Hitodama:
                vel.y += Mathf.Sin(time * 0.9f) * 0.4f; vel.x += Mathf.Cos(time * 0.6f) * 0.3f;
                p.T.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg + 180f);
                alpha *= 0.8f + 0.2f * Mathf.Sin(time * 2.3f);
                break;
            case Kind.Mote: vel += new Vector2(Mathf.Sin(time * 0.8f), Mathf.Cos(time * 0.6f)) * 0.15f; alpha *= 0.7f + 0.3f * Mathf.Sin(time * 1.7f); break;
            case Kind.Lantern: vel.x += Mathf.Sin(time * 0.7f) * 0.25f; alpha *= 0.85f + 0.15f * Mathf.Sin(time * 2.1f); break;
            case Kind.Ofuda: vel.x += Mathf.Sin(time * 1.1f) * 0.35f; p.T.rotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 1.4f) * 35f + p.Spin); break;
        }
        p.T.position += (Vector3)(vel * dt);
        p.T.localScale = new Vector3(scale, scale, 1f);
        var c = p.R.color; c.a = alpha * fade; p.R.color = c;
    }

    private void UpdateLightning(float dt, Vector3 cam)
    {
        bool allowed = lightning && !GameSettings.ReduceFlashing && weight[3] > 0.5f;
        flashAge += dt;
        if (allowed && (nextLightning -= dt) <= 0) { nextLightning = Rand(LightningEvery * 0.5f, LightningEvery * 1.5f); flashAge = 0; }
        float a = allowed && flashAge < 0.9f ? 0.14f * Mathf.Exp(-flashAge * 4f) * (flashAge < 0.1f ? flashAge / 0.1f : 1f) : 0f;
        flash.transform.gameObject.SetActive(a > 0.001f);
        if (a <= 0.001f) return;
        flash.transform.position = new Vector3(cam.x, cam.y + view.orthographicSize * 0.5f, 0);
        flash.color = new Color(0.7f, 0.55f, 1f, a);
    }

    // ---------- Sprites ----------

    private static Sprite Make(int w, int h, string name, Func<int, int, Color> pixel)
    {
        var tex = ProceduralSprites.NewTexture(w, h, name);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) px[y * w + x] = pixel(x, y);
        tex.SetPixels32(px); tex.Apply(false, true);
        var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32);
        s.name = name;
        return s;
    }

    private static void BuildSprites()
    {
        if (dot != null) return;
        dot = Make(16, 16, "Ambient dot", (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(8, 8)) / 8f;
            float a = Mathf.Clamp01(1f - d); return new Color(1, 1, 1, a * a * (d < 0.35f ? 1f : 0.75f));
        });
        comet = Make(32, 8, "Ambient comet", (x, y) =>
        {
            float head = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(4, 4)) / 4f);
            float tail = x < 4 ? 0 : Mathf.Clamp01(1f - (x - 4) / 27f) * Mathf.Clamp01(1f - Mathf.Abs(y + 0.5f - 4) / (4f * (1f - (x - 4) / 28f) + 0.5f)) * 0.6f;
            return new Color(1, 1, 1, Mathf.Max(head, tail));
        });
        crowUp = Make(7, 4, "Ambient crow up", (x, y) => Mathf.Abs(x - 3) + y == 3 || (x == 3 && y == 0) ? Color.white : Color.clear);
        crowFlat = Make(7, 4, "Ambient crow flat", (x, y) => y == 1 && x != 3 || (x == 3 && y == 2) ? Color.white : Color.clear);
        flake = Make(4, 4, "Ambient flake", (x, y) => (x + y) % 3 == 2 ? Color.clear : Color.white);
        ofuda = Make(5, 12, "Ambient ofuda", (x, y) =>
        {
            if (x == 0 || x == 4) return new Color(0.7f, 0.6f, 0.5f);
            if (x == 2 && y > 2 && y < 10 && y % 3 != 0) return new Color(0.84f, 0.27f, 0.16f);
            return new Color(0.95f, 0.89f, 0.8f);
        });
        lantern = Make(14, 18, "Ambient lantern", (x, y) =>
        {
            float halo = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(7, 9)) / 7f) * 0.25f;
            if (x >= 4 && x < 10 && y >= 3 && y < 15)
            {
                if (y == 3 || y == 14) return new Color(0.16f, 0.1f, 0.08f);
                return (x == 4 || x == 9) ? new Color(0.84f, 0.27f, 0.16f) : new Color(1f, 0.67f, 0.28f);
            }
            return new Color(1f, 0.55f, 0.2f, halo);
        });
    }
}
