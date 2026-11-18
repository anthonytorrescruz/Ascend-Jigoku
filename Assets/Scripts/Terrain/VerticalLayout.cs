using System;
using System.Collections.Generic;
using UnityEngine;

// Pure layout math for the pillar: given a seed and a row number it returns that row's landing (position, width, rock shape,
// pieces, enemy). Random access: any row/chunk can be rebuilt from the seed without planning the whole pillar.
// A "row" is one rung of the climb, RowHeight world units above the previous.
public static class VerticalLayout
{
    // Rock silhouettes a landing can take; Tables below says which appear in each zone.
    public enum RockShape { Shelf, Block, Spire, Terrace, Stairs, Mushroom, Arch, Bridge, Island, Cage, Torii, Ledge, Pillar, Crag }

    // Colour family of a piece: ordinary rock, the darker body under a ledge, bone-white, or red lacquer (torii).
    public enum Tone { Rock, Body, Bone, Lacquer }

    // One solid box, generator-local. Top is its upper edge. Cap = lit walkable top (also tops of walls); Flat = no ragged underside.
    public struct Piece
    {
        public float X, Top, Width, Height;
        public bool Cap, Flat;
        public Tone Tone;
    }
    // Center/Width describe the main walkable surface (Pieces[0]); Pieces may add steps, chunks and walls inside the same footprint.
    public struct Landing
    {
        public int Row, EnemyIndex, MarkerIndex, Approach, Depart;
        public Vector2 Center;
        public float Width, Thickness, EnemySide, Raise;
        public RockShape Shape;
        public Color Color;
        public Piece[] Pieces;
    }

    // Numbers derived from the player's movement that keep every gap jumpable (see Measure).
    // RowHeight: vertical spacing between rows. TravelLimit: max safe horizontal distance between neighbouring ledges.
    // SingleJumpRise: apex height of one jump. LastRow: the summit row.
    public struct Metrics
    {
        public int RowsPerChunk, LastRow;
        public float RowHeight, ChunkHeight, TravelLimit, SingleJumpRise;
    }

    // Turns the player's jump speed, run speed, gravity and number of (double) jumps into row spacing and reach limits.
    // jumpSafety scales both, so the layout only asks for a fraction of what the player can really do.
    public static Metrics Measure(VerticalGenerationSettings settings, float jumpSpeed, float moveSpeed, float gravity, int jumps)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (jumpSpeed <= 0 || moveSpeed <= 0 || gravity <= 0 || jumps < 1)
            throw new ArgumentException("Generation needs positive jump speed, movement, gravity and jump count.");
        int rows = Mathf.Clamp(settings.platformsPerChunk, 4, 24);
        float safety = Mathf.Clamp(settings.jumpSafety, 0.3f, 0.95f);
        // Height of one jump: v^2 / 2g.
        float apex = jumpSpeed * jumpSpeed / (2 * gravity);
        float variation = Mathf.Clamp(settings.heightVariation, 0, 0.12f);
        // Row spacing: chunk height split evenly, but never more than the safe total jump height. A neighbour can sit
        // up to 2 * variation higher than the nominal step (this row jittered down, the next up), hence the divide.
        float step = Mathf.Min(Mathf.Max(8, settings.chunkHeight) / rows, apex * jumps * safety / (1 + 2 * variation));
        float maxRise = step * (1 + 2 * variation);   // worst-case climb to the next ledge
        // Air time for that worst-case climb: each earlier jump takes v / g to reach its apex; the last jump rises the remainder
        // (maxRise minus the earlier apexes) and then falls back down onto the ledge (quadratic solved for the landing time).
        float flight = (jumps - 1) * jumpSpeed / gravity
            + (jumpSpeed + Mathf.Sqrt(Mathf.Max(0, jumpSpeed * jumpSpeed - 2 * gravity * (maxRise - (jumps - 1) * apex)))) / gravity;
        return new Metrics {
            RowsPerChunk = rows, RowHeight = step, ChunkHeight = step * rows,
            LastRow = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(20, settings.pillarHeight) / step), 1, 1000000),
            TravelLimit = moveSpeed * flight * safety, SingleJumpRise = apex   // horizontal reach = run speed * air time
        };
    }

    // Row number of a height marker, kept off row 0 and the summit.
    public static int MarkerRow(VerticalGenerationSettings.HeightMarker marker, Metrics metrics)
        => Mathf.Clamp(Mathf.RoundToInt(marker.height / metrics.RowHeight), 1, metrics.LastRow - 1);

    // Horizontal layout of the pillar: Tower = pillar width, Lanes = vertical routes ledges are placed on, Pitch = distance
    // between lane centres, Jitter = random sideways wobble per ledge, MinWidth/MaxWidth = allowed ledge widths.
    private struct Grid
    {
        public float Tower, Jitter, MinWidth, MaxWidth, Pitch;
        public int Lanes;
    }

    // Fits ledge widths, lane spacing and lane count inside the pillar and the jump reach. The ordering matters:
    // each line clamps against the values computed before it.
    private static Grid MakeGrid(VerticalGenerationSettings settings, Metrics metrics)
    {
        var g = new Grid { Tower = Mathf.Max(8, settings.pillarWidth) };
        // Keep an open shaft between neighbors: solid blocks cannot be jumped through.
        g.Jitter = Mathf.Min(Mathf.Max(0, settings.horizontalVariation), metrics.TravelLimit * 0.04f, g.Tower * 0.02f);
        float widthLimit = Mathf.Max(2.5f, (g.Tower - 1.8f - 4 * g.Jitter) * 0.5f);
        g.MinWidth = Mathf.Clamp(Mathf.Min(settings.platformWidth.x, settings.platformWidth.y), 2.5f, widthLimit);
        // Lane spacing: the gap between neighbouring ledges' edges (pitch minus width, minus jitter) must stay under TravelLimit.
        g.Pitch = Mathf.Clamp(settings.zigzagWidth, 5, Mathf.Max(5, metrics.TravelLimit + g.MinWidth - 2.5f - 2 * g.Jitter));
        g.MaxWidth = Mathf.Clamp(Mathf.Max(settings.platformWidth.x, settings.platformWidth.y), g.MinWidth,
            Mathf.Max(g.MinWidth, Mathf.Min(widthLimit, g.Pitch - 2 * g.Jitter - 1.8f)));
        // Room left for lane centres is Tower minus the widest ledge and jitter; as many lanes as fit, then respace them evenly.
        g.Pitch = Mathf.Min(g.Pitch, g.Tower - g.MaxWidth - 2 * g.Jitter);
        g.Lanes = Mathf.Max(2, Mathf.FloorToInt((g.Tower - g.MaxWidth - 2 * g.Jitter) / g.Pitch) + 1);
        g.Pitch = Mathf.Min(g.Pitch, (g.Tower - g.MaxWidth - 2 * g.Jitter) / (g.Lanes - 1));
        return g;
    }
    // Lane position before clamping to the pillar; neighbours only need its sign, so shapes never depend on each other's shapes.
    private static float LaneX(Grid g, int seed, int row)
    {
        if (row <= 0) return 0;
        var jitter = new System.Random(unchecked(seed ^ (row * 83492791) ^ 0x2545F491));
        return (Lane(seed, row, g.Lanes) - (g.Lanes - 1) * 0.5f) * g.Pitch + Range(jitter, -g.Jitter, g.Jitter);
    }

    // Which way the route goes from `row` to `other`: +1 if the other row's lane is to the right, -1 left, 0 if there is
    // no such row (or it is the boss floor). Used to pick shapes that suit the approach and departure directions.
    private static int Side(Grid g, int seed, int row, int other, Metrics metrics, int bossRow)
    {
        if (other <= 0 || other > metrics.LastRow || other == bossRow) return 0;
        return LaneX(g, seed, other) > LaneX(g, seed, row) ? 1 : -1;
    }

    // Weighted shape tables per zone (Cinder Pit, Ashen Stair, Bone Gallery, Gate of Jigoku).
    private static readonly (RockShape shape, int weight)[][] Tables =
    {
        new[] { (RockShape.Block, 2), (RockShape.Terrace, 2), (RockShape.Spire, 2), (RockShape.Stairs, 3), (RockShape.Mushroom, 2), (RockShape.Arch, 2),
                (RockShape.Island, 3), (RockShape.Crag, 2), (RockShape.Ledge, 3), (RockShape.Pillar, 2), (RockShape.Bridge, 1), (RockShape.Shelf, 1) },
        new[] { (RockShape.Stairs, 4), (RockShape.Bridge, 3), (RockShape.Arch, 3), (RockShape.Island, 2), (RockShape.Shelf, 2), (RockShape.Mushroom, 2),
                (RockShape.Ledge, 3), (RockShape.Pillar, 2), (RockShape.Terrace, 1), (RockShape.Crag, 1), (RockShape.Spire, 1) },
        new[] { (RockShape.Cage, 4), (RockShape.Mushroom, 2), (RockShape.Bridge, 2), (RockShape.Spire, 2), (RockShape.Island, 2), (RockShape.Arch, 2),
                (RockShape.Ledge, 3), (RockShape.Pillar, 2), (RockShape.Stairs, 2), (RockShape.Terrace, 1) },
        new[] { (RockShape.Torii, 4), (RockShape.Arch, 2), (RockShape.Stairs, 3), (RockShape.Pillar, 3), (RockShape.Bridge, 2), (RockShape.Mushroom, 2),
                (RockShape.Island, 2), (RockShape.Ledge, 3), (RockShape.Spire, 1), (RockShape.Shelf, 1), (RockShape.Crag, 1) },
    };

    // Zone 0-3: the pillar height split into four equal bands.
    private static int ZoneOf(VerticalGenerationSettings settings, Metrics metrics, int row)
        => Mathf.Clamp(Mathf.FloorToInt(row * metrics.RowHeight / Mathf.Max(20, settings.pillarHeight) * 4), 0, 3);

    // Stairs need a route that changes direction; ledge, pillar and crag (wall on one end) need it to continue the same way.
    private static bool Eligible(RockShape shape, int approach, int depart)
    {
        if (shape == RockShape.Stairs) return approach != 0 && depart != 0 && approach != depart;
        if (shape == RockShape.Ledge || shape == RockShape.Pillar || shape == RockShape.Crag) return approach != 0 && approach == depart;
        return true;
    }

    // Weighted random pick from the zone's table, skipping `avoid` and (optionally) shapes that don't fit the route.
    private static RockShape PickShape(int zone, System.Random random, int approach, int depart, RockShape? avoid, bool checkEligible)
    {
        var table = Tables[zone];
        int total = 0;
        foreach (var entry in table)
            if (entry.shape != avoid && (!checkEligible || Eligible(entry.shape, approach, depart))) total += entry.weight;

        int choice = random.Next(Mathf.Max(1, total));
        foreach (var entry in table)
        {
            if (entry.shape == avoid || (checkEligible && !Eligible(entry.shape, approach, depart))) continue;
            choice -= entry.weight;
            if (choice < 0) return entry.shape;
        }
        return RockShape.Block;
    }

    private static System.Random ShapeRandom(int seed, int row, int salt) => new System.Random(unchecked(seed ^ (row * 19349663) ^ salt));

    // Builds the landing for one row, fully determined by (seed, row). Boss rows get a plain shape near the arena;
    // "wide" rows (start, summit, markers, every wideLandingEvery rows) are full-width safe landings.
    public static Landing GetLanding(VerticalGenerationSettings settings, int seed, int row, Metrics metrics, int bossRow = -1)
    {
        row = Mathf.Clamp(row, 0, metrics.LastRow);
        var random = new System.Random(unchecked(seed ^ (row * 73856093)));
        var g = MakeGrid(settings, metrics);
        float baseDepth = Mathf.Max(0.3f, settings.platformThickness);
        int markerIndex = -1;
        if (settings.heightMarkers != null)
            for (int i = 0; i < settings.heightMarkers.Length; i++)
                if (settings.heightMarkers[i] != null && MarkerRow(settings.heightMarkers[i], metrics) == row)
                {
                    markerIndex = i;
                    break;
                }

        bool wide = row == 0 || row == metrics.LastRow || row % Mathf.Max(1, settings.wideLandingEvery) == 0 || markerIndex >= 0;
        var color = Color.HSVToRGB(Range(random, 0.015f, 0.1f), Range(random, 0.16f, 0.38f), Range(random, 0.3f, 0.5f));
        float enemySide = random.Next(2) == 0 ? -1 : 1;
        float width = Range(random, g.MinWidth, g.MaxWidth), depth = baseDepth * Range(random, 0.8f, 1.2f);
        var shape = RockShape.Shelf;
        int approach = 0, depart = 0;
        if (wide)
        {
            width = row == 0 ? g.Tower : g.MaxWidth;
            depth = baseDepth;
        }
        else
        {
            approach = Side(g, seed, row, row - 1, metrics, bossRow);
            depart = Side(g, seed, row, row + 1, metrics, bossRow);
            int zone = ZoneOf(settings, metrics, row);
            // First pick ignores the route, so the previous row can recompute its own shape (same salt) without recursion.
            // Redo the pick if this shape doesn't fit the route or repeats the previous row's shape.
            shape = PickShape(zone, ShapeRandom(seed, row, 0x1f3d5b79), 0, 0, null, false);
            var previous = PickShape(ZoneOf(settings, metrics, row - 1), ShapeRandom(seed, row - 1, 0x1f3d5b79), 0, 0, null, false);
            if (!Eligible(shape, approach, depart) || shape == previous)
                shape = PickShape(zone, ShapeRandom(seed, row, 0x2c1b3c6d), approach, depart, previous, true);
            if (Mathf.Abs(row - bossRow) <= 2 && bossRow >= 0
                && (shape == RockShape.Ledge || shape == RockShape.Pillar || shape == RockShape.Crag))
                shape = RockShape.Block;
            (width, depth) = Dimensions(shape, width, depth, g);
            // Two rows may return to the same lane; leave room for a standing player there, plus a metre for walls.
            depth = Mathf.Min(depth, Mathf.Max(1, metrics.RowHeight * (2 - 2 * settings.heightVariation) - 2.6f));
            if (depth < 2f && shape != RockShape.Shelf && shape != RockShape.Block) shape = RockShape.Block;
            if (shape == RockShape.Bridge && width < 8) shape = RockShape.Arch;
            if (width < 7.4f && (shape == RockShape.Stairs || shape == RockShape.Ledge || shape == RockShape.Pillar || shape == RockShape.Crag))
                shape = RockShape.Block;
        }

        // Centre x: the lane position, clamped so the whole ledge stays inside the tower.
        float x = row == 0
            ? 0
            : Mathf.Clamp(LaneX(g, seed, row), -Mathf.Max(0, (g.Tower - width) * 0.5f), Mathf.Max(0, (g.Tower - width) * 0.5f));
        // Small random vertical offset (up to heightVariation rows), none on wide rows or the first row of a chunk.
        float yJitter = wide || row % metrics.RowsPerChunk == 0 ? 0 : Range(random, -1, 1) * Mathf.Clamp(settings.heightVariation, 0, 0.12f);
        var landing = new Landing
        {
            Row = row, Center = new Vector2(x, (row + yJitter) * metrics.RowHeight), Width = width, Thickness = Mathf.Max(0.3f, depth),
            Shape = shape, Color = color, EnemyIndex = -1, MarkerIndex = markerIndex, EnemySide = enemySide,
            Approach = approach, Depart = depart
        };
        // Does the ledge two rows up sit over this one? If so a wall here must stay low or it would block that ledge.
        bool overlapsAbove = row + 2 <= metrics.LastRow
            && (row + 2 == bossRow || Mathf.Abs(LaneX(g, seed, row + 2) - x) < g.MaxWidth * 0.5f + width * 0.5f + 0.3f);
        Build(ref landing, x, width, g, overlapsAbove, new System.Random(unchecked(seed ^ (row * 40503) ^ 0x6a09e667)));
        // Fixed eligible rows make spawn choices independent of which chunks were visited first.
        bool checkpoint = markerIndex >= 0 && settings.heightMarkers[markerIndex].checkpoint;
        if (settings.enemyOnEveryPlatform ? row > 0 && !checkpoint
            : !wide && row >= settings.firstEnemyRow && row % Mathf.Max(1, settings.enemyRowSpacing) == 0
                && random.NextDouble() < settings.enemyChance)
            landing.EnemyIndex = PickEnemy(settings, row, random);
        return landing;
    }

    // Per-shape adjustment of the rolled width and thickness: most blend the width toward the maximum (the number is the blend)
    // and multiply the thickness (chunkier shapes are deeper).
    private static (float width, float depth) Dimensions(RockShape shape, float w, float d, Grid g)
    {
        float max = g.MaxWidth, min = g.MinWidth;
        switch (shape)
        {
            case RockShape.Shelf: return (Mathf.Lerp(w, max, 0.6f), d * 0.6f);
            case RockShape.Spire: return (Mathf.Lerp(min, w, 0.35f), d * 1.9f);
            case RockShape.Terrace: return (Mathf.Lerp(w, max, 0.4f), d * 1.4f);
            case RockShape.Stairs: return (Mathf.Lerp(w, max, 0.7f), d * 1.3f);
            case RockShape.Mushroom: return (Mathf.Lerp(w, max, 0.5f), d * 1.8f);
            case RockShape.Arch: return (Mathf.Lerp(w, max, 0.5f), d * 1.5f);
            case RockShape.Bridge: return (Mathf.Lerp(w, max, 0.85f), d * 1.2f);
            case RockShape.Island: return (w, d * 1.8f);
            case RockShape.Cage: return (Mathf.Lerp(w, max, 0.5f), d * 1.8f);
            case RockShape.Torii: return (Mathf.Lerp(w, max, 0.4f), d * 1.9f);
            case RockShape.Ledge: return (Mathf.Lerp(w, max, 0.6f), d * 1.5f);
            case RockShape.Pillar: return (Mathf.Lerp(w, max, 0.5f), d * 1.5f);
            case RockShape.Crag: return (Mathf.Lerp(w, max, 0.7f), d * 1.3f);
            default: return (w, d);
        }
    }

    // Everything stays inside the footprint (x +/- width/2, top - depth .. top) except walls, which rise on the one end no route uses.
    // P makes a box from its centre x, top edge, width and height.
    private static Piece P(float x, float top, float w, float h, bool cap, bool flat, Tone tone = Tone.Rock)
        => new Piece { X = x, Top = top, Width = w, Height = h, Cap = cap, Flat = flat, Tone = tone };

    // A piece hanging below the main slab, whose bottom edge is slabBottom.
    private static Piece Under(float x, float slabBottom, float w, float h, Tone tone = Tone.Body, bool flat = false)
        => P(x, slabBottom + 0.12f, w, h + 0.12f, false, flat, tone); // starts inside the slab so no seam shows

    // Fills l.Pieces with the boxes that make up the chosen shape, and updates Center.x / Width to the main walkable surface.
    // Names: T = top y, D = total depth, L / R = left / right edges, t = thickness of the walkable cap slab,
    // rest = depth left under the cap, b = bottom of the cap. Everything below the cap is built with Under().
    private static void Build(ref Landing l, float x, float W, Grid g, bool overlapsAbove, System.Random r)
    {
        float T = l.Center.y, D = l.Thickness, L = x - W * 0.5f, R = x + W * 0.5f;
        var list = new List<Piece>(8);
        float mainX = x, mainW = W;
        float t = Mathf.Min(D * 0.4f, Range(r, 0.8f, 1.1f)), rest = D - t, b = T - t;
        switch (l.Shape)
        {
            case RockShape.Terrace:   // two stacked slabs narrowing downward
            {
                list.Add(P(x, T, W, t, true, false));
                float h1 = rest * 0.5f;
                list.Add(Under(x + W * Range(r, -0.05f, 0.05f), b, W * Range(r, 0.7f, 0.85f), h1));
                list.Add(Under(x + W * Range(r, -0.08f, 0.08f), b - h1, Mathf.Max(1.4f, W * Range(r, 0.35f, 0.5f)), rest - h1));
                break;
            }
            case RockShape.Spire:   // like Terrace but a steep taper to a point
            {
                list.Add(P(x, T, W, t, true, false));
                float h1 = rest * 0.4f;
                list.Add(Under(x + W * Range(r, -0.06f, 0.06f), b, W * 0.62f, h1));
                list.Add(Under(x + W * Range(r, -0.1f, 0.1f), b - h1, Mathf.Max(1.3f, W * 0.3f), rest - h1));
                break;
            }
            case RockShape.Mushroom:   // a thin stalk with a small flared base
            {
                list.Add(P(x, T, W, t, true, false));
                float sw = Mathf.Max(1.3f, W * Range(r, 0.2f, 0.3f)), sx = x + W * Range(r, -0.12f, 0.12f), flare = Mathf.Min(0.7f, rest * 0.3f);
                list.Add(Under(sx, b, sw, rest - flare, Tone.Body, true));
                list.Add(P(sx, T - D + flare + 0.1f, sw * 1.6f, flare + 0.1f, false, false, Tone.Body));
                break;
            }
            case RockShape.Arch:   // two legs at the ends, open between them
            {
                list.Add(P(x, T, W, t, true, false));
                float a = Range(r, 1.1f, 1.9f), c = Range(r, 1.1f, 1.9f);
                list.Add(Under(L + a * 0.5f + Range(r, 0, 0.25f), b, a, rest));
                list.Add(Under(R - c * 0.5f - Range(r, 0, 0.25f), b, c, rest));
                break;
            }
            case RockShape.Island:   // 2-3 separate hanging chunks, the tall ones with a smaller block under them
            {
                list.Add(P(x, T, W, t, true, false));
                int n = W > 8.5f ? 3 : 2;
                float seg = (W - 0.6f) / n;
                for (int k = 0; k < n; k++)
                {
                    float cw = Range(r, 1.0f, Mathf.Max(1.1f, Mathf.Min(2.4f, seg - 0.4f)));
                    float cx = L + 0.3f + seg * (k + 0.5f) + Range(r, -0.5f, 0.5f) * (seg - cw), ch = rest * Range(r, 0.35f, 1f);
                    if (ch > 1.5f && cw > 1.4f)
                    {
                        list.Add(Under(cx, b, cw, ch - 0.7f));
                        list.Add(P(cx, b - ch + 0.8f, cw * 0.55f, 0.8f, false, false, Tone.Body));
                    }
                    else list.Add(Under(cx, b, cw, ch));
                }
                break;
            }
            case RockShape.Cage:   // a row of hanging bone bars, longest in the middle
            {
                list.Add(P(x, T, W, t, true, false));
                int n = Mathf.Clamp(Mathf.RoundToInt(W / 1.25f), 4, 8);
                for (int k = 0; k < n; k++)
                    list.Add(Under(L + 0.55f + k * (W - 1.1f) / (n - 1), b, 0.42f, rest * (0.6f + 0.4f * Mathf.Sin(Mathf.PI * k / (n - 1))), Tone.Bone));
                break;
            }
            case RockShape.Torii:   // two lacquered posts joined by a crossbeam
            {
                list.Add(P(x, T, W, t, true, false));
                float pw = Range(r, 1f, 1.3f), inset = W * 0.12f;
                list.Add(Under(L + inset + pw * 0.5f, b, pw, rest, Tone.Lacquer, true));
                list.Add(Under(R - inset - pw * 0.5f, b, pw, rest, Tone.Lacquer, true));
                list.Add(P(x, b - rest * Range(r, 0.25f, 0.4f), W * 0.78f, 0.5f, false, true, Tone.Lacquer));
                break;
            }
            case RockShape.Bridge:   // three separate slabs with small gaps; the middle one is the main surface
            {
                const float gap = 0.5f;
                float wm = Mathf.Max(3.6f, W * Range(r, 0.4f, 0.55f)), sides = W - wm - 2 * gap, wl = sides * Range(r, 0.35f, 0.65f), wr = sides - wl;
                float[] xs = { L + wl + gap + wm * 0.5f, L + wl * 0.5f, R - wr * 0.5f }, ws = { wm, wl, wr };
                for (int k = 0; k < 3; k++)
                {
                    float ct = Range(r, 0.8f, 1.1f), body = (D - ct) * Range(r, k == 0 ? 0.3f : 0, 1f);
                    list.Add(P(xs[k], T, ws[k], ct, true, false));
                    if (body > 0.6f) list.Add(Under(xs[k], T - ct, ws[k] * Range(r, 0.5f, 0.85f), body));
                }

                mainX = xs[0];
                mainW = wm;
                break;
            }
            case RockShape.Stairs:   // 2-3 steps, each one rising by `rise` toward the side the route departs on
            {
                float wm = Mathf.Clamp(W * Range(r, 0.45f, 0.55f), 4.2f, W - 2.4f), remain = W - wm, rise = Range(r, 0.55f, 0.8f);
                int n = remain >= 3.4f && W >= 10 ? 3 : 2;
                rise = Mathf.Min(rise, (D - 0.9f) / (n - 1));
                float w1 = n == 3 ? remain * Range(r, 0.4f, 0.6f) : remain, w2 = remain - w1, q = l.Depart;
                float edge = q > 0 ? R : L;
                float[] ws = { wm, w1, w2 };
                for (int k = 0; k < n; k++)
                {
                    float cx = edge - q * ws[k] * 0.5f;
                    list.Add(P(cx, T - k * rise, ws[k], D - k * rise, true, false));
                    edge -= q * ws[k];
                    if (k == 0)
                    {
                        mainX = cx;
                        mainW = wm;
                    }
                }
                break;
            }
            case RockShape.Ledge: case RockShape.Pillar: case RockShape.Crag:
            {
                // A walkable slab with a wall at the end the route does not use (f = -Approach picks that end);
                // H is the wall's height above the top. The wall is what the player climbs past, so it is capped lower when
                // the ledge above overlaps. Enemies spawn on the open side.
                bool pillar = l.Shape == RockShape.Pillar, crag = l.Shape == RockShape.Crag;
                float f = -l.Approach;
                float ww = crag ? Range(r, 1f, 1.6f) : pillar ? Range(r, 2.2f, 3f) : Range(r, 1.3f, 2f);
                float wanted = crag ? Range(r, 0.7f, 1.1f) : pillar ? Range(r, 3f, 4.6f) : Range(r, 1.9f, 4f);
                float H = Mathf.Min(wanted, overlapsAbove ? 2.5f : 4.6f);
                float wallX = f > 0 ? R - ww * 0.5f : L + ww * 0.5f;
                mainW = W - ww;
                mainX = f > 0 ? L + mainW * 0.5f : R - mainW * 0.5f;
                list.Add(P(mainX + f * 0.06f, T, mainW + 0.12f, t, true, false));
                list.Add(P(wallX, T + H, ww, H + D, true, false));
                float bodyW = mainW * (pillar ? 0.3f : 0.55f);
                if (crag)
                {
                    list.Add(Under(mainX, b, mainW * 0.8f, rest * 0.5f));
                    list.Add(Under(mainX, b - rest * 0.5f, mainW * 0.45f, rest * 0.5f));
                }
                else list.Add(Under(f > 0 ? L + mainW - bodyW * 0.5f : R - mainW + bodyW * 0.5f, b, bodyW, pillar ? rest * 0.5f : rest));
                l.Raise = H;
                l.EnemySide = -f;
                break;
            }
            default:
                list.Add(P(x, T, W, D, true, false));
                break;
        }
        l.Pieces = list.ToArray();
        l.Center.x = mainX;
        l.Width = mainW;
    }

    // Used when a landing is overridden (boss floor): one slab matching Center/Width/Thickness.
    public static void Slab(ref Landing l)
    {
        l.Shape = RockShape.Shelf;
        l.Raise = 0;
        l.Pieces = new[] { P(l.Center.x, l.Center.y, l.Width, l.Thickness, true, false) };
    }
    // Walkable tops (x, y, half width) and open undersides props can hang from.
    public static void Surfaces(Piece[] pieces, List<Vector3> tops, List<Vector3> bottoms)
    {
        for (int i = 0; i < pieces.Length; i++)
        {
            var p = pieces[i];
            if (p.Cap) tops.Add(new Vector3(p.X, p.Top, p.Width * 0.5f));
            float bottom = p.Top - p.Height;
            // A bottom counts as open (props may hang from it) unless another piece sits just below it.
            bool open = p.Width >= 1;
            for (int j = 0; open && j < pieces.Length; j++)
            {
                var q = pieces[j];
                if (j != i && Mathf.Abs(q.X - p.X) < (q.Width + p.Width) * 0.5f - 0.2f
                    && q.Top > bottom - 0.5f && q.Top - q.Height < bottom - 0.3f)
                    open = false;
            }
            if (open) bottoms.Add(new Vector3(p.X, bottom, p.Width * 0.5f));
        }
    }

    // Which lane (0 .. count-1) row sits in. The route is a random walk that restarts at the centre lane every 12 rows,
    // so any row is found by replaying at most 11 steps from its block's start, with no state from other rows.
    private static int Lane(int seed, int row, int count)
    {
        // Short seeded excursions meet at a shared anchor; random access costs at most 12 steps.
        const int length = 12;
        int center = count / 2, lane = center;
        var random = new System.Random(unchecked(seed ^ ((row / length) * 19349663)));
        int direction = random.Next(2) == 0 ? -1 : 1;
        for (int step = 1; step <= row % length; step++)
        {
            if (random.NextDouble() < 0.22) direction = -direction;   // 22% chance to turn around
            // Turn at the walls, and also whenever staying out this far would leave too few steps to get back to the centre by the block end.
            int next = lane + direction;
            if (next < 0 || next >= count || Mathf.Abs(next - center) > length - step) direction = -direction;
            lane += direction;
        }
        return lane;
    }

    // Weighted pick of an enemy index in settings.enemies (those unlocked at this row); -1 if none.
    private static int PickEnemy(VerticalGenerationSettings settings, int row, System.Random random)
    {
        if (settings.enemies == null) return -1;
        int total = 0;
        foreach (var enemy in settings.enemies)
            if (enemy != null && enemy.prefab != null && row >= enemy.firstRow) total += Mathf.Clamp(enemy.weight, 1, 1000);
        if (total == 0) return -1;
        int choice = random.Next(total);
        for (int i = 0; i < settings.enemies.Length; i++)
        {
            var enemy = settings.enemies[i];
            if (enemy == null || enemy.prefab == null || row < enemy.firstRow) continue;
            choice -= Mathf.Clamp(enemy.weight, 1, 1000);
            if (choice < 0) return i;
        }
        return -1;
    }

    // Seeded uniform float in [min, max].
    private static float Range(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
}
