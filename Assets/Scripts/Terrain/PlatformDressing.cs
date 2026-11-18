using System.Collections.Generic;
using AscendJigoku.Settings;
using UnityEngine;

// Cosmetic set dressing for each generated landing: zone prop kits standing on the ledge edges and hanging underneath.
// Pixel sprites are drawn once in code and shared; a single driver animates flames, sway, drips and embers.
// Deterministic from the landing seed. Props are decoration only (no colliders) and sit behind the player.
public static class PlatformDressing
{
    private const float PixelsPerUnit = 32;
    private const int OrderProp = -4, OrderFlame = -3, OrderEmber = -2, OrderGlow = -5;
    private const float EdgeMin = 0.5f, EdgeMax = 0.78f; // standing props sit this fraction of the half-width from the centre

    // Every sprite we draw. The zone kits below pick from these; Flame, TealFlame, Glow and Ember are child effects of other props.
    private enum Prop
    {
        Brazier, Stump, Spikes, AshPile, BrokenPillar, DeadBranch, SkullPile, Ribs, BoneFence, CandleSkull,
        Toro, ToriiStub, PrayerPost, Shimenawa, Chain, Drip, Banner, Roots, Debris, Ofuda, CheckLantern, Flame, TealFlame, Glow, Ember
    }
    // Props standing on a ledge, per zone (Cinder Pit, Ashen Stair, Bone Gallery, Gate of Jigoku).
    private static readonly Prop[][] Standing =
    {
        new[] { Prop.Brazier, Prop.Stump, Prop.Spikes },
        new[] { Prop.AshPile, Prop.BrokenPillar, Prop.DeadBranch },
        new[] { Prop.SkullPile, Prop.Ribs, Prop.BoneFence, Prop.CandleSkull },
        new[] { Prop.Toro, Prop.ToriiStub, Prop.PrayerPost, Prop.Shimenawa },
    };
    // Props hanging under a ledge, per zone; repeats weight the pick (two Drips out of three in the Cinder Pit).
    private static readonly Prop[][] Hanging =
    {
        new[] { Prop.Drip, Prop.Drip, Prop.Roots },
        new[] { Prop.Banner, Prop.Roots },
        new[] { Prop.Roots, Prop.Chain },
        new[] { Prop.Chain, Prop.Chain, Prop.Banner },
    };

    private static readonly Dictionary<int, Sprite[]> cache = new Dictionary<int, Sprite[]>();
    private static Driver driver;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        // HideAndDontSave objects and DontSave textures outlive play mode, so free the last session's.
        foreach (var frames in cache.Values)
            foreach (var frame in frames)
                if (frame != null)
                {
                    Object.Destroy(frame.texture);
                    Object.Destroy(frame);
                }

        cache.Clear();
        if (driver != null) Object.Destroy(driver.gameObject);
        driver = null;
        VerticalLevelGenerator.PlatformBuilt -= Dress;
        VerticalLevelGenerator.PlatformBuilt += Dress;
    }

    // Runs for every platform the generator builds. Rolls 0-4 props from the landing's seed and places them in random slots:
    // left/right standing, centre debris, and two hanging spots under the ledge. Checkpoints get a pair of lanterns instead.
    private static void Dress(VerticalLevelGenerator.BuiltPlatform p)
    {
        if (p.ChunkRoot == null || p.IsBossFloor) return;
        var rng = new Rng(p.Seed);
        int zone = Mathf.Clamp(PillarEnvironment.ZoneAt(p.ZoneHeight), 0, 3);
        float half = p.Landing.Width * 0.5f, top = p.WorldTop.y, cx = p.WorldTop.x;
        if (half < 1.5f) return;
        if (p.IsCheckpoint)
        {
            foreach (float side in new[] { -1f, 1f })
                Spawn(p.ChunkRoot, Prop.CheckLantern, 0, cx + side * half * 0.72f, top, side < 0, rng.Next());
            return;
        }
        if (p.IsSummit) return;

        float roll = rng.Next();
        // Prop count odds: 0 = 22%, 1 = 28%, 2 = 30%, 3 = 15%, 4 = 5%.
        int count = roll < 0.22f ? 0 : roll < 0.5f ? 1 : roll < 0.8f ? 2 : roll < 0.95f ? 3 : 4;
        int[] slots = { 0, 1, 2, 3, 4 }; // 0 left, 1 right, 2 centre debris, 3-4 hanging
        // Fisher-Yates shuffle, so the first `count` slots are a random subset.
        for (int i = 4; i > 0; i--)
        {
            int j = (int)(rng.Next() * (i + 1));
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }

        for (int i = 0; i < count; i++)
        {
            int slot = slots[i];
            bool flip = rng.Next() < 0.5f;
            int variant = rng.Next() < 0.5f ? 0 : 1;
            if (slot < 2)
            {
                var kit = Standing[zone];
                var s = Pick(p.Tops, rng.Next(), 1.2f, p.WorldTop);
                float x = s.x + (slot == 0 ? -1 : 1) * s.z * Mathf.Lerp(EdgeMin, EdgeMax, rng.Next());
                Spawn(p.ChunkRoot, kit[(int)(rng.Next() * kit.Length) % kit.Length], variant, x, s.y, flip, rng.Next());
            }
            else if (slot == 2)
            {
                var s = Pick(p.Tops, rng.Next(), 1.2f, p.WorldTop);
                Spawn(p.ChunkRoot, Prop.Debris, zone * 2 + variant, s.x + (rng.Next() - 0.5f) * s.z, s.y, flip, rng.Next());
            }
            else
            {
                var kit = Hanging[zone];
                var s = Pick(p.Bottoms, rng.Next(), 0.9f, p.WorldTop - Vector3.up * p.Landing.Thickness);
                Spawn(p.ChunkRoot, kit[(int)(rng.Next() * kit.Length) % kit.Length], variant,
                    s.x + (rng.Next() * 2 - 1) * s.z * 0.8f, s.y + 0.15f, flip, rng.Next());
            }
        }
    }

    // A surface (world x, world y, half width) chosen by width; the main surface when there is none.
    private static Vector3 Pick(Vector3[] spots, float roll, float minHalf, Vector3 fallback)
    {
        float total = 0;
        if (spots != null)
            foreach (var s in spots)
                if (s.z >= minHalf) total += s.z;

        if (total <= 0) return new Vector3(fallback.x, fallback.y, 3);
        roll *= total;
        foreach (var s in spots)
        {
            if (s.z < minHalf) continue;
            roll -= s.z;
            if (roll < 0) return s;
        }
        return spots[0];
    }

    // Creates one prop sprite and attaches whatever animation it needs (flame, sway, drip). phase de-syncs neighbours.
    private static void Spawn(Transform root, Prop prop, int variant, float x, float y, bool flip, float phase)
    {
        var frames = Frames(prop, variant);
        var go = new GameObject(prop.ToString());
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(x, y, 0);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sortingOrder = OrderProp;
        sr.flipX = flip;
        var d = GetDriver();
        float s = flip ? -1 : 1;
        switch (prop)
        {
            // The flame offsets are local heights of each prop's fire bowl; only the brazier throws embers.
            case Prop.Brazier:
                Flame(go.transform, d, Prop.Flame, new Vector3(0, 0.44f), phase, true);
                break;
            case Prop.CandleSkull:
                Flame(go.transform, d, Prop.Flame, new Vector3(0, 0.3f), phase, false);
                break;
            case Prop.Toro:
                Flame(go.transform, d, Prop.Flame, new Vector3(0, 0.5f), phase, false);
                break;
            case Prop.CheckLantern:
                Flame(go.transform, d, Prop.TealFlame, new Vector3(0, 0.4f), phase, false);
                break;
            case Prop.Shimenawa:
                // Two paper charms hanging from the rope, which sags between the posts (see RopeY).
                for (int i = 0; i < 2; i++)
                {
                    // t is the position along the rope (0..1), w the sprite width in world units; s mirrors it when flipped.
                    float t = i == 0 ? 0.33f : 0.68f, w = 20f / PixelsPerUnit;
                    var o = Child(go.transform, Prop.Ofuda, 0,
                        new Vector3((t - 0.5f) * w * s, RopeY(t) / PixelsPerUnit - 0.03f), OrderProp + 1);
                    d.Add(new Item { Kind = Kind.Sway, T = o.transform, A = phase * 6 + i * 2, B = 9, C = 1.6f });
                }
                break;
            case Prop.Chain: case Prop.Banner: case Prop.Roots:
                // Swings from the top pivot: B degrees of swing, C speed (chains swing less and slower).
                d.Add(new Item
                {
                    Kind = Kind.Sway, T = go.transform, A = phase * 6,
                    B = prop == Prop.Chain ? 2.5f : 4, C = prop == Prop.Chain ? 1.2f : 1.5f
                });
                break;
            case Prop.Drip:
                // The ledge sprite is the stalactite; a separate bead grows, falls 2.6 units and fades, every C seconds.
                var bead = Child(go.transform, Prop.Drip, 1, Vector3.zero, OrderProp + 1);
                d.Add(new Item
                {
                    Kind = Kind.Drip, T = bead.transform, R = bead, A = phase * 4, B = 2.6f, C = 3.5f + phase * 2, Base = Vector3.zero
                });
                break;
        }
    }

    // A flickering flame sprite plus a soft glow behind it, and optionally two rising embers.
    private static void Flame(Transform parent, Driver d, Prop type, Vector3 at, float phase, bool embers)
    {
        var f = Child(parent, type, 0, at, OrderFlame);
        d.Add(new Item { Kind = Kind.Flame, R = f, Frames = Frames(type, 0), A = phase * 8 });
        var g = Child(parent, Prop.Glow, 0, at, OrderGlow);
        g.transform.localScale = Vector3.one * 2.2f;
        g.color = type == Prop.TealFlame ? new Color(0.3f, 0.85f, 0.8f, 0.2f) : new Color(0.94f, 0.45f, 0.18f, 0.22f);
        d.Add(new Item { Kind = Kind.Glow, R = g, A = phase * 10, B = 0.2f });
        if (!embers) return;
        for (int i = 0; i < 2; i++)
        {
            var e = Child(parent, Prop.Ember, 0, at, OrderEmber);
            e.color = new Color(0.95f, 0.55f, 0.2f, 0);
            d.Add(new Item
            {
                Kind = Kind.Ember, T = e.transform, R = e, A = phase * 5 + i * 1.3f, B = 2.2f + i * 0.7f, Base = at
            });
        }
    }

    private static SpriteRenderer Child(Transform parent, Prop prop, int variant, Vector3 local, int order)
    {
        var go = new GameObject(prop.ToString());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Frames(prop, variant)[0];
        sr.sortingOrder = order;
        return sr;
    }

    // ---------- animation driver ----------
    private enum Kind : byte { Flame, Glow, Sway, Drip, Ember }

    // One animated thing. A is a phase offset; B and C are per-kind parameters:
    // Glow: B = base alpha. Sway: B = swing in degrees, C = speed. Drip: B = fall distance, C = cycle seconds.
    // Ember: B = rise cycle seconds. Base is the local rest position.
    private struct Item
    {
        public Kind Kind;
        public Transform T;
        public SpriteRenderer R;
        public Sprite[] Frames;
        public float A, B, C;
        public Vector3 Base;
    }

    private static Driver GetDriver()
    {
        if (driver != null) return driver;
        var go = new GameObject("PlatformDressing") { hideFlags = HideFlags.HideAndDontSave };
        Object.DontDestroyOnLoad(go);
        return driver = go.AddComponent<Driver>();
    }

    // One hidden object that animates every dressing item each frame, so props need no scripts of their own.
    // Flicker is frozen when Reduce Flashing is on.
    private sealed class Driver : MonoBehaviour
    {
        private readonly List<Item> items = new List<Item>(256);
        public void Add(Item item) => items.Add(item);

        private void Update()
        {
            float time = Time.time;
            bool calm = GameSettings.ReduceFlashing;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                var live = it.R != null ? (Component)it.R : it.T;
                // The prop was destroyed with its chunk: swap-remove the item.
                if (live == null)
                {
                    items[i] = items[items.Count - 1];
                    items.RemoveAt(items.Count - 1);
                    continue;
                }

                switch (it.Kind)
                {
                    case Kind.Flame:
                        it.R.sprite = it.Frames[calm ? 0 : (int)((time * 9 + it.A) % it.Frames.Length)];
                        break;
                    case Kind.Glow:
                        var c = it.R.color;
                        c.a = calm ? it.B : it.B * (0.75f + 0.25f * Mathf.Sin(time * 11 + it.A) * Mathf.Sin(time * 4.3f + it.A * 2));
                        it.R.color = c;
                        break;
                    case Kind.Sway:
                        it.T.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * it.C + it.A) * it.B);
                        break;
                    case Kind.Drip:
                        // t is the cycle position: first 20% the bead swells at the tip, then it falls (accelerating) and fades.
                        float t = ((time + it.A) % it.C) / it.C, s = (t - 0.2f) / 0.8f;
                        if (t < 0.2f)
                        {
                            it.T.localPosition = it.Base;
                            it.T.localScale = new Vector3(1, t / 0.2f, 1);
                        }
                        else
                        {
                            it.T.localScale = Vector3.one;
                            it.T.localPosition = it.Base + new Vector3(0, -it.B * s * s, 0);
                            var dc = it.R.color;
                            dc.a = 1 - s * s * s;
                            it.R.color = dc;
                        }

                        if (t < 0.2f)
                        {
                            var dc = it.R.color;
                            dc.a = 1;
                            it.R.color = dc;
                        }

                        break;
                    case Kind.Ember:
                        float e = ((time + it.A) % it.B) / it.B;
                        it.T.localPosition = it.Base + new Vector3(Mathf.Sin(e * 6 + it.A) * 0.09f, 0.05f + e * 0.9f, 0);
                        var ec = it.R.color;
                        ec.a = (1 - e) * 0.9f;
                        it.R.color = ec;
                        break;
                }
            }
        }
    }

    // ---------- deterministic rng ----------
    // Tiny xorshift32 so the same landing seed always gives the same props. Next() returns a float in [0, 1).
    private struct Rng
    {
        private uint s;

        public Rng(int seed)
        {
            s = (uint)seed * 2654435761u + 0x9E3779B9u;
            if (s == 0) s = 1;
            Next();
            Next();
        }

        public float Next()
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return (s & 0xFFFFFF) / (float)0x1000000;
        }
    }

    // ---------- sprites ----------
    // Cached sprite frames for a prop; the cache key packs the prop and its variant (0 or 1, or zone * 2 + variant for Debris).
    private static Sprite[] Frames(Prop prop, int variant)
    {
        int key = (int)prop * 16 + variant;
        if (cache.TryGetValue(key, out var found) && found != null && found[0] != null) return found;
        var made = Build(prop, variant);
        cache[key] = made;
        return made;
    }

    // Pixel-art canvas, y = 0 at the bottom. Px sets a pixel (clipped), R fills a rect (x, y, width, height),
    // Line draws a one-pixel line, Skull a 5x4 skull. ToSprite pivots at the bottom, or at the top for hanging props.
    private sealed class Canvas
    {
        public readonly int W, H;
        public readonly Color32[] P;

        public Canvas(int w, int h)
        {
            W = w;
            H = h;
            P = new Color32[w * h];
        }

        public void Px(int x, int y, Color32 c)
        {
            if (x >= 0 && y >= 0 && x < W && y < H) P[y * W + x] = c;
        }

        public void R(int x, int y, int w, int h, Color32 c)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    Px(x + i, y + j, c);
        }

        public void Line(int x0, int y0, int x1, int y1, Color32 c)
        {
            int n = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), 1);
            for (int i = 0; i <= n; i++)
                Px(Mathf.RoundToInt(Mathf.Lerp(x0, x1, i / (float)n)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, i / (float)n)), c);
        }

        public void Skull(int cx, int y, Color32 bone, Color32 dark)
        {
            R(cx - 2, y + 1, 5, 3, bone);
            R(cx - 1, y, 3, 1, bone);
            R(cx - 1, y + 2, 1, 1, dark);
            R(cx + 1, y + 2, 1, 1, dark);
            Px(cx, y + 1, dark);
        }

        public Sprite ToSprite(string name, bool hang, float ppu = PixelsPerUnit)
        {
            var tex = AscendJigoku.Gameplay.ProceduralSprites.NewTexture(W, H, name);
            tex.SetPixels32(P);
            tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, hang ? 1f : 0f), ppu);
            sp.name = name;
            return sp;
        }
    }

    private static Color32 C(int hex) => new Color32((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);

    // Palette per zone: dark, mid, light.
    private static readonly Color32 Basalt0 = C(0x1A1210), Basalt1 = C(0x2E201B), Basalt2 = C(0x4A322A),
        Iron = C(0x3A3A42), IronHi = C(0x5A5A64), Lava = C(0xC8481E);
    private static readonly Color32 Ash0 = C(0x2E2A28), Ash1 = C(0x4A4440), Ash2 = C(0x6A625C);
    private static readonly Color32 Bone0 = C(0x3A3228), Bone1 = C(0x7A6E5A), Bone2 = C(0xA89C82), Dark = C(0x120C0A);
    private static readonly Color32 Stone0 = C(0x2E2832), Stone1 = C(0x4C4252), Stone2 = C(0x6E6076),
        Verm0 = C(0x6E1E1A), Verm1 = C(0xA03228), Paper = C(0xC8B89A), Gold = C(0xB8903A);

    // Height in pixels of the sagging shimenawa rope at t (0..1) between its posts: a sine dip of 3.5 px below y = 14.
    private static float RopeY(float t) => 14 - 3.5f * Mathf.Sin(Mathf.PI * t);

    // Draws the pixel art for a prop. v is the variant (0 or 1; Debris uses zone * 2 + v). Each case is a small sprite built
    // from rects (c.R), single pixels (c.Px) and lines; most return one frame, flames return four.
    private static Sprite[] Build(Prop prop, int v)
    {
        string n = "Dressing " + prop + v;
        Canvas c;
        switch (prop)
        {
            case Prop.Brazier:
                c = new Canvas(10, 14);
                c.R(1, 8, 8, 2, Iron); c.R(2, 7, 6, 1, Iron); c.R(2, 10, 6, 1, Lava); c.Px(1, 9, IronHi);   // bowl with glowing coals
                c.R(4, 2, 2, 6, Iron); c.R(2, 0, 6, 2, Iron); c.R(3, 2, 1, 1, IronHi);   // stem and foot
                return One(c, n, false);
            case Prop.Stump:
                c = new Canvas(9, 10);
                int h = 7 + v * 2;
                c.R(1, 0, 7, 2, Basalt1); c.R(2, 2, 5, h - 2, Basalt0); c.R(1, h - 1, 7, 1, Basalt1);
                c.Px(3, h, Basalt1); c.Px(6, h + 1 > 9 ? 9 : h + 1, Basalt1);
                c.R(3, 1, 1, h - 2, Basalt1); c.Px(5, 3, Lava); c.Px(4, 5, Lava);
                return One(c, n, false);
            case Prop.Spikes:
                c = new Canvas(14, 10);
                c.R(0, 0, 14, 2, Basalt1);
                for (int i = 0; i < 3; i++)
                {
                    // Three spikes of alternating height (5 or 8 px); the top 2 rows narrow to a point.
                    int x = 1 + i * 4 + (v == 1 && i == 1 ? 1 : 0), ht = 5 + ((i + v) % 2) * 3;
                    for (int y = 0; y < ht; y++) c.R(x + (y > ht - 3 ? 1 : 0), 2 + y, y > ht - 3 ? 1 : 3, 1, y % 3 == 0 ? IronHi : Iron);
                }
                return One(c, n, false);
            case Prop.AshPile:
                c = new Canvas(16, 7);
                // Mound with a sine-shaped profile (5 px tall in the middle), jittered by 1 px on every 5th column.
                for (int x = 0; x < 16; x++)
                {
                    int ht = Mathf.RoundToInt(5 * Mathf.Sin(Mathf.PI * (x + 0.5f) / 16f)) + (x % 5 == 2 ? 1 : 0);
                    c.R(x, 0, 1, Mathf.Max(1, ht), x % 4 == 0 ? Ash2 : Ash1);
                }

                c.R(5 + v * 4, 3, 2, 1, Ash0); c.Px(9, 2, Ash0);
                return One(c, n, false);
            case Prop.BrokenPillar:
                c = new Canvas(10, 18);
                int ph = 11 + v * 5;   // pillar height; the top pixels below make a jagged break
                c.R(0, 0, 10, 2, Ash1); c.R(1, 2, 8, 1, Ash0);
                for (int y = 3; y < ph; y++)
                    c.R(2, y, 6, 1, Ash1);

                c.R(2, 3, 1, ph - 3, Ash2); c.R(6, 3, 2, ph - 3, Ash0); c.R(4, 6, 1, 3, Ash0);
                c.Px(2, ph, Ash1); c.Px(3, ph, Ash1); c.Px(6, ph, Ash1); c.Px(3, ph + 1, Ash1); c.Px(7, ph - 1, Dark);
                return One(c, n, false);
            case Prop.DeadBranch:
                c = new Canvas(14, 14);
                c.R(6, 0, 2, 3, Ash0);   // trunk base
                c.Line(7, 3, 6, 8, Ash0); c.Line(6, 8, 3, 12 + v, Ash0); c.Line(6, 7, 10, 10, Ash0); c.Line(10, 10, 12, 13, Ash0);
                c.Line(6, 5, 2, 7, Ash0); c.Line(4, 10, 7, 12, Ash0); c.Px(3, 13, Ash0); c.Px(12, 11, Ash1); c.Px(7, 4, Ash1);
                return One(c, n, false);
            case Prop.SkullPile:
                c = new Canvas(14, 10);
                c.R(0, 0, 14, 2, Bone0); c.Skull(3, 1, Bone1, Dark); c.Skull(8, 1, Bone2, Dark); c.Skull(12, 1, Bone1, Dark);
                if (v == 0)
                    c.Skull(6, 4, Bone2, Dark);
                else
                    c.Skull(10, 4, Bone1, Dark);

                c.R(1, 3, 3, 1, Bone2);
                return One(c, n, false);
            case Prop.Ribs:
                // Spine (v = 1 is shorter) with up to four pairs of ribs curling down either side.
                c = new Canvas(12, 13);
                c.R(5, 0, 2, 11 - v * 2, Bone1);
                for (int i = 0; i < 4; i++)
                {
                    int y = 9 - v * 2 - i * 2 - (i > 2 ? 0 : 0);
                    if (y < 1) continue;
                    c.Line(5, y, 2, y - 1, Bone2); c.Px(1, y - 2 < 0 ? 0 : y - 2, Bone1);
                    c.Line(6, y, 9, y - 1, Bone2); c.Px(10, y - 2 < 0 ? 0 : y - 2, Bone1);
                }

                c.Px(5, 11 - v * 2, Bone2);
                return One(c, n, false);
            case Prop.BoneFence:
                c = new Canvas(18, 9);
                c.R(0, 5, 18, 1, Bone1); c.R(0, 3, 18, 1, Bone0);
                // Four posts (7 or 8 px tall, alternating) with knobbed tops.
                for (int i = 0; i < 4; i++)
                {
                    int x = 1 + i * 5, ht = 7 + ((i + v) % 2);
                    c.R(x, 0, 2, ht, Bone1); c.Px(x, ht - 1, Bone2); c.Px(x - 1, ht, Bone2); c.Px(x + 2, ht, Bone2);
                }
                return One(c, n, false);
            case Prop.CandleSkull:
                c = new Canvas(7, 8);
                c.Skull(3, 0, Bone1, Dark); c.R(2, 4, 3, 2, Paper); c.Px(3, 6, Dark);   // skull with a candle on top (Flame added in Spawn)
                c.Px(1, 0, Bone2); c.Px(5, 4, Paper);
                return One(c, n, false);
            case Prop.Toro:
                // Stone lantern: base, post, lamp box with a lit window, then the roof.
                c = new Canvas(12, 20);
                int tb = v * 2;
                c.R(2, 0, 8, 2, Stone1); c.R(4, 2, 4, 5, Stone0); c.R(3, 7, 6, 1, Stone2); c.R(1, 8, 10, 1, Stone2);
                c.R(3, 9, 6, 5, Stone1); c.R(5, 10, 2, 3, C(0xD8802A));
                c.R(2, 14, 8, 2, Stone2); c.R(1, 16, 10, 1, Stone1); c.R(3, 17, 6, 1, Stone2); c.R(5, 18, 2, 1, Stone1);
                c.R(3, 9, 1, 5, Stone2); c.R(8, 9, 1, 5, Stone0);
                c.Px(4, 3, Stone1); c.Px(5, 4, Stone2);
                if (tb > 0) c.R(2, 0, 1, 1, Stone0);
                return One(c, n, false);
            case Prop.ToriiStub:
                // Broken torii: a tall left post with a beam stub (bh sets its height) and a short right stump.
                c = new Canvas(16, 24);
                int bh = v == 0 ? 20 : 15;
                c.R(1, 0, 4, 2, Stone1); c.R(1, 2, 3, bh - 2, Verm0); c.R(1, 2, 1, bh - 2, Verm1); c.R(0, 0, 5, 1, Stone2);
                c.R(11, 0, 4, 2, Stone1); c.R(12, 2, 3, 6, Verm0); c.R(12, 2, 1, 6, Verm1); c.R(12, 8, 1, 1, Dark); c.Px(13, 9, Verm0); c.Px(12, 5, Dark);
                c.R(0, bh - 2, 8, 2, Dark); c.R(0, bh, 7, 1, Verm0); c.R(-1 + 1, bh - 1, 7, 1, Verm1);
                c.Px(8, bh - 1, Verm0); c.Px(9, bh - 2, Dark);
                return One(c, n, false);
            case Prop.PrayerPost:
                c = new Canvas(6, 18);
                int ph2 = 14 + v * 3;
                c.R(1, 0, 4, 2, Stone1); c.R(2, 2, 2, ph2, Verm0); c.R(2, 2, 1, ph2, Verm1); c.R(1, ph2 + 1, 4, 1, Dark); c.R(2, ph2 - 4, 2, 3, Paper); c.Px(3, ph2 - 2, Verm0);
                return One(c, n, false);
            case Prop.Shimenawa:
                c = new Canvas(20, 18);
                c.R(0, 0, 3, 2, Stone1); c.R(1, 2, 2, 13, Verm0); c.R(1, 2, 1, 13, Verm1);
                c.R(17, 0, 3, 2, Stone1); c.R(17, 2, 2, 13, Verm0); c.R(17, 2, 1, 13, Verm1);
                // Rope between the posts: alternating light and dark strands following the RopeY sag.
                for (int x = 3; x < 17; x++)
                {
                    int y = Mathf.RoundToInt(RopeY((x - 2.5f) / 15f));
                    c.Px(x, y, x % 2 == 0 ? Paper : Bone1);
                    c.Px(x, y - 1, Bone0);
                }

                c.Px(3, 15, Paper); c.Px(16, 15, Paper);
                return One(c, n, false);
            case Prop.Chain:
                int len = v == 0 ? 16 : 27;
                c = new Canvas(5, len);
                // Links repeat every 3 rows counted from the top: a flat bar, then two rows of side loops.
                for (int y = 0; y < len; y++)
                {
                    int row = len - 1 - y;
                    if (row % 3 == 0) c.R(1, y, 3, 1, Iron);
                    else
                    {
                        c.Px(1, y, row % 3 == 1 ? IronHi : Iron);
                        c.Px(3, y, Iron);
                    }
                }

                c.R(0, len - 2, 5, 2, Iron);
                return One(c, n, true);
            case Prop.Drip:
                // v = 0 is the rock stalactite, v = 1 the small lava bead that Spawn animates falling from it.
                if (v == 0)
                {
                    c = new Canvas(3, 6);
                    c.R(0, 3, 3, 3, Basalt0); c.R(1, 0, 1, 3, Basalt0); c.Px(1, 4, Basalt2);
                    return One(c, n, true);
                }

                c = new Canvas(2, 3);
                c.R(0, 0, 2, 3, Lava); c.Px(0, 2, C(0xF08A3C));
                return One(c, n, true);
            case Prop.Banner:
                // Tattered banner hanging from a top rod: each column's length is bl minus a pseudo-random fray; v = 1 is longer and red.
                c = new Canvas(10, 20);
                int bl = 14 + v * 5;
                c.R(0, 19, 10, 1, Ash1); c.Px(0, 18, Ash2); c.Px(9, 18, Ash2);
                for (int x = 1; x < 9; x++)
                {
                    int hh = bl - ((x * 7 + v * 3) % 4) * 2 - (x == 4 ? 3 : 0);
                    c.R(x, 19 - hh, 1, hh, x % 3 == 0 ? Ash0 : (v == 0 ? Ash1 : Verm0));
                }

                return One(c, n, true);
            case Prop.Roots:
                int rl = v == 0 ? 14 : 22;
                c = new Canvas(8, rl);
                c.R(2, rl - 2, 4, 2, Ash0); c.Line(3, rl - 2, 4, rl / 2, Ash0); c.Line(4, rl / 2, 3, 1, Ash0);
                c.Line(4, rl - 5, 7, rl - 9, Ash0); c.Line(3, rl - 4, 0, rl - 8, Ash0); c.Px(1, rl - 9, Ash1);
                if (v == 1)
                {
                    c.Line(4, 9, 6, 4, Ash0);
                    c.Px(6, 3, Ash1);
                }

                return One(c, n, true);
            case Prop.Debris:
                c = new Canvas(7, 4);
                // Debris variant is zone * 2 + v: z picks the zone's two stone colours (a base, b highlight); the Bone Gallery's first is a skull.
                int z = v / 2;
                Color32 a = z == 0 ? Basalt1 : z == 1 ? Ash1 : z == 2 ? Bone1 : Stone1, b = z == 0 ? Basalt2 : z == 1 ? Ash2 : z == 2 ? Bone2 : Stone2;
                if (z == 2 && v % 2 == 0)
                {
                    c.Skull(3, 0, a, Dark);
                    c.Px(0, 0, b);
                    c.Px(6, 0, b);
                }
                else
                {
                    c.R(1, 0, 3, 2, a); c.Px(2, 2, a); c.R(4, 0, 2, 1, b); c.Px(0, 0, b);
                    if (z == 3) c.R(5, 1, 1, 2, Verm1);
                }

                return One(c, n, false);
            case Prop.Ofuda:
                // Paper charm with red script marks; hangs from the Shimenawa rope.
                c = new Canvas(4, 8);
                c.R(0, 0, 4, 7, Paper); c.R(0, 7, 4, 1, Dark); c.Px(1, 5, Verm0); c.Px(2, 3, Verm0); c.Px(1, 2, Verm0); c.Px(0, 0, Dark); c.Px(3, 0, Dark);
                return One(c, n, true);
            case Prop.CheckLantern:
                c = new Canvas(10, 16);
                c.R(1, 0, 8, 2, Stone1); c.R(3, 2, 4, 3, Stone0); c.R(1, 5, 8, 1, Stone2);
                c.R(2, 6, 6, 5, C(0x1E5A58)); c.R(3, 7, 4, 3, C(0x2A8A84)); c.R(2, 6, 1, 5, Stone2); c.R(7, 6, 1, 5, Stone0);
                c.R(0, 11, 10, 1, Stone2); c.R(2, 12, 6, 1, Stone1); c.R(4, 13, 2, 1, Stone2);
                return One(c, n, false);
            case Prop.Flame: case Prop.TealFlame:
                // Four animation frames; each differs in height and in which way the tip leans. o = outer, m = middle, k = hot core.
                bool teal = prop == Prop.TealFlame;
                Color32 o = teal ? C(0x1E8C88) : C(0xD6452A), m = teal ? C(0x4FD1C5) : C(0xF08A3C), k = teal ? C(0xD8FFF8) : C(0xFFD27A);
                var fr = new Sprite[4];
                int[] heights = { 6, 8, 7, 9 }, lean = { 0, 1, 0, -1 };
                for (int f = 0; f < 4; f++)
                {
                    c = new Canvas(5, 10);
                    int hf = heights[f];
                    for (int y = 0; y < hf; y++)
                    {
                        // Width by row: 3 at the base, 5 through the body, then tapering to 3 and a 1 px tip.
                        int wid = y < 2 ? 3 : y < hf - 3 ? 5 : y < hf - 1 ? 3 : 1, x0 = (5 - wid) / 2 + (y > 4 ? lean[f] : 0);
                        c.R(Mathf.Clamp(x0, 0, 5 - wid), y, wid, 1, y < 3 ? m : o);
                        if (y < hf - 3 && wid >= 3) c.R(Mathf.Clamp(x0 + 1, 0, 3), y, wid - 2, 1, y < 2 ? k : m);
                    }
                    fr[f] = c.ToSprite(n + f, false);
                }
                return fr;
            case Prop.Glow:
                // Round glow quantised into 4 alpha bands (hard steps keep it pixel-art); tinted by the sprite colour.
                c = new Canvas(16, 16);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        float d = Mathf.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f)) / 8f;
                        int band = d >= 1 ? 0 : Mathf.CeilToInt((1 - d) * 4);
                        if (band > 0) c.Px(x, y, new Color32(255, 255, 255, (byte)(band * band * 15 + 15)));
                    }

                var gs = c.ToSprite(n, false, 16); gs.texture.filterMode = FilterMode.Point; // pivot is bottom; recentre below
                var centred = Sprite.Create(gs.texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
                centred.name = n;
                return new[] { centred };
            default: // Ember
                c = new Canvas(2, 2);
                c.R(0, 0, 2, 2, new Color32(255, 255, 255, 255));
                var es = Sprite.Create(c.ToSprite(n, false).texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), PixelsPerUnit);
                es.name = n;
                return new[] { es };
        }
    }

    private static Sprite[] One(Canvas c, string name, bool hang) => new[] { c.ToSprite(name, hang) };
}
