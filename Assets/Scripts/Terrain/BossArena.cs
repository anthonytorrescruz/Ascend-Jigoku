using System;
using System.Collections.Generic;
using UnityEngine;

// Rock enclosure around a boss floor, built by VerticalLevelGenerator in place of the encounters' flat seal bars.
// Walls and a roof made of skinned pillar rock, one-way ledges inside for dodging, and for the Fire Worm:
// rock gates that drop shut when the fight starts and a plug in the roof that crumbles on victory so the player climbs out.
// All positions are relative to the floor's top surface (this object's position), x across the pillar.
public sealed class BossArena : MonoBehaviour
{
    private const float WallThickness = 2f, RoofThickness = 1.2f;
    private const float GateGap = 2.8f;     // headroom under a raised gate, enough to walk in
    private const float MaxPiece = 24;      // longer rock is split so each skin texture stays within its size limit

    private GameObject prefab;
    private Color rock, glow;
    private float glowDensity;
    private int seed, pieces;
    private Func<bool> started, defeated;
    private Transform[] gates = new Transform[0];
    private float gateOpenY, gateShutY;
    private GameObject plug;
    private bool plugOpened;

    // Fire Worm: arena narrower than the pillar, entered under raised gates, left through the roof after the win.
    public static BossArena BuildFireWorm(Transform room, GameObject platformPrefab, float pillarWidth, float halfWidth, float height,
        float exitSide, Color rockColor, float zoneHeight, int seed, Func<bool> started, Func<bool> defeated)
    {
        var arena = Create(room, platformPrefab, rockColor, zoneHeight, seed, started, defeated);
        float half = pillarWidth * 0.5f + WallThickness;
        float side = exitSide >= 0 ? 1 : -1;
        float holeX = side * (halfWidth - 3f), holeHalf = 1.6f;

        // Roof across the whole pillar with a hole near one wall; the plug fills it until the boss dies.
        arena.Row(-half, holeX - holeHalf, height + RoofThickness * 0.5f, RoofThickness, false);
        arena.Row(holeX + holeHalf, half, height + RoofThickness * 0.5f, RoofThickness, false);
        arena.plug = arena.Rock("Roof plug", holeX, height + RoofThickness * 0.5f, holeHalf * 2, RoofThickness, false, 0);

        // Fixed rock above each gate; the gate hides behind it while raised.
        arena.gateOpenY = GateGap + (height - GateGap) * 0.5f;
        arena.gateShutY = (height - GateGap) * 0.5f - 0.1f;
        var gateList = new List<Transform>();
        foreach (float s in new[] { -1f, 1f })
        {
            float x = s * (halfWidth + WallThickness * 0.5f);
            arena.Rock("Arena lintel", x, GateGap + (height - GateGap) * 0.5f, WallThickness, height - GateGap, false, 2);
            gateList.Add(arena.Rock("Arena gate", x, arena.gateOpenY, WallThickness, height - GateGap, false, 0).transform);
        }
        arena.gates = gateList.ToArray();

        // Ledges: one low on each side to get above ground attacks, and a higher step under the roof hole.
        arena.Ledge(-side * 8, 3.4f, 5);
        arena.Ledge(side * 8, 3.4f, 5);
        arena.Ledge(holeX, 6f, 3.2f);

        // Rebuilt after the win (chunk reload or a saved run): start finished instead of animating.
        if (defeated())
        {
            foreach (var gate in arena.gates) gate.localPosition = new Vector3(gate.localPosition.x, arena.gateShutY, 0);
            arena.plug.SetActive(false);
            arena.plugOpened = true;
        }
        return arena;
    }

    // Grim Death: the arena spans the pillar, so the walls are permanent. Its checkpoint sits inside, so no exit hole.
    // avoid holds generator-local boxes (x, top, width, height) of ledges already inside the arena; new ledges skip them.
    public static BossArena BuildGrimDeath(Transform room, GameObject platformPrefab, float halfWidth, float height, List<Vector4> avoid,
        Color rockColor, float zoneHeight, int seed, Func<bool> started, Func<bool> defeated)
    {
        var arena = Create(room, platformPrefab, rockColor, zoneHeight, seed, started, defeated);
        float half = halfWidth + WallThickness;
        arena.Row(-half, half, height + RoofThickness * 0.5f, RoofThickness, false);
        foreach (float s in new[] { -1f, 1f })
            arena.Rock("Arena wall", s * (halfWidth + WallThickness * 0.5f), (height + RoofThickness) * 0.5f - 0.6f,
                WallThickness, height + RoofThickness + 1.2f, false, 0);
        // Two tiers of ledges; each slides sideways to the nearest spot clear of the route ledges.
        foreach (var spot in new[] { new Vector2(-26, 3.4f), new Vector2(-12, 3.4f), new Vector2(12, 3.4f), new Vector2(26, 3.4f),
                     new Vector2(-19, 7.8f), new Vector2(0, 7.8f), new Vector2(19, 7.8f) })
            for (float shift = 0; shift <= 6; shift += 1.5f)
            {
                float x = spot.x + shift * (spot.x > 0 ? -1 : 1);
                if (Mathf.Abs(x) > halfWidth - 4 || !Clear(avoid, room.position, x, spot.y, 6)) continue;
                arena.Ledge(x, spot.y, 6);
                break;
            }
        return arena;
    }

    private static BossArena Create(Transform room, GameObject platformPrefab, Color rockColor, float zoneHeight, int seed,
        Func<bool> started, Func<bool> defeated)
    {
        var arena = new GameObject("Boss arena rock").AddComponent<BossArena>();
        arena.transform.SetParent(room, false);
        arena.prefab = platformPrefab;
        arena.rock = rockColor;
        arena.seed = seed;
        arena.started = started;
        arena.defeated = defeated;
        PillarEnvironment.GlowAt(zoneHeight, out arena.glow, out arena.glowDensity);
        return arena;
    }

    // True when a ledge at (x, top, width) would not overlap any box in avoid, with a little headroom around it.
    private static bool Clear(List<Vector4> avoid, Vector3 origin, float x, float top, float width)
    {
        if (avoid == null) return true;
        foreach (var box in avoid)
        {
            float bx = box.x - origin.x, btop = box.y - origin.y;
            bool apartX = Mathf.Abs(bx - x) > (box.z + width) * 0.5f + 1;
            bool apartY = btop - box.w > top + 2.5f || btop < top - 2.5f;
            if (!apartX && !apartY) return false;
        }
        return true;
    }

    // A horizontal run of rock from x0 to x1, split into pieces no longer than MaxPiece.
    private void Row(float x0, float x1, float y, float thickness, bool walkable)
    {
        int count = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / MaxPiece));
        float step = (x1 - x0) / count;
        for (int i = 0; i < count; i++)
            Rock("Arena roof", x0 + step * (i + 0.5f), y, step, thickness, walkable, 0);
    }

    private GameObject Ledge(float x, float top, float width)
    {
        var ledge = Rock("Arena ledge", x, top - 0.4f, width, 0.8f, true, 1);
        var effector = ledge.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.useOneWayGrouping = true;
        ledge.GetComponent<Collider2D>().usedByEffector = true;
        return ledge;
    }

    // One box of pillar rock centred at (x, y), skinned like the generated ledges. walkable = lit top edge.
    private GameObject Rock(string name, float x, float y, float width, float height, bool walkable, int order)
    {
        var go = Instantiate(prefab, transform);
        go.name = name;
        go.transform.localPosition = new Vector3(x, y, 0);
        go.transform.localScale = new Vector3(width, height, 1);
        var render = go.GetComponent<SpriteRenderer>();
        if (render != null)
        {
            render.color = rock;
            render.sortingOrder += order;
            if (PillarEnvironment.PixelArtTerrain)
                TerrainSkin.Apply(render, seed ^ (++pieces * 0x2f6b4d1), walkable, glow, glowDensity, walkable ? 1 : 0, !walkable);
        }
        return go;
    }

    private void Update()
    {
        bool shut = started() || defeated();
        float targetY = shut ? gateShutY : gateOpenY;
        foreach (var gate in gates)
        {
            var p = gate.localPosition;
            if (Mathf.Approximately(p.y, targetY)) continue;
            p.y = Mathf.MoveTowards(p.y, targetY, Time.deltaTime * 30); // slams shut in about a quarter second
            gate.localPosition = p;
        }
        if (plug != null && !plugOpened && defeated()) { plugOpened = true; StartCoroutine(Crumble(plug)); }
    }

    // Shakes the roof plug, then lets it drop and fade so the way up opens.
    private System.Collections.IEnumerator Crumble(GameObject block)
    {
        var start = block.transform.localPosition;
        for (float t = 0; t < 0.5f; t += Time.deltaTime)
        {
            block.transform.localPosition = start + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.06f);
            yield return null;
        }
        foreach (var shape in block.GetComponentsInChildren<Collider2D>()) shape.enabled = false;
        var renders = block.GetComponentsInChildren<SpriteRenderer>();
        for (float t = 0; t < 0.6f; t += Time.deltaTime)
        {
            block.transform.localPosition = start + Vector3.down * (t * t * 6);
            foreach (var r in renders) { var c = r.color; c.a = 1 - t / 0.6f; r.color = c; }
            yield return null;
        }
        block.SetActive(false);
    }

    // The encounters still build their flat seal bars; the rock replaces them, so hide those (their scripts toggle the objects, not these components).
    public static void HideSeals(Transform room)
    {
        foreach (Transform child in room)
        {
            if (!child.name.ToLowerInvariant().Contains("seal")) continue;
            foreach (var r in child.GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = false;
            foreach (var c in child.GetComponentsInChildren<Collider2D>(true)) c.enabled = false;
        }
    }
}
