using System;
using UnityEngine;

// Random access: any row/chunk can be rebuilt from the seed without planning the whole pillar.
public static class VerticalLayout
{
    public enum RockShape { Shelf, Block, Spire, Terrace }
    public struct Landing
    {
        public int Row, EnemyIndex, MarkerIndex;
        public Vector2 Center;
        public float Width, Thickness, EnemySide;
        public RockShape Shape;
        public Color Color;
    }
    public struct Metrics
    {
        public int RowsPerChunk, LastRow;
        public float RowHeight, ChunkHeight, TravelLimit, SingleJumpRise;
    }
    public static Metrics Measure(VerticalGenerationSettings settings, float jumpSpeed, float moveSpeed, float gravity, int jumps)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (jumpSpeed <= 0 || moveSpeed <= 0 || gravity <= 0 || jumps < 1)
            throw new ArgumentException("Generation needs positive jump speed, movement, gravity and jump count.");
        int rows = Mathf.Clamp(settings.platformsPerChunk, 4, 24);
        float safety = Mathf.Clamp(settings.jumpSafety, 0.3f, 0.95f);
        float apex = jumpSpeed * jumpSpeed / (2 * gravity);
        float variation = Mathf.Clamp(settings.heightVariation, 0, 0.12f);
        float step = Mathf.Min(Mathf.Max(8, settings.chunkHeight) / rows, apex * jumps * safety / (1 + 2 * variation));
        float maxRise = step * (1 + 2 * variation);
        float flight = (jumps - 1) * jumpSpeed / gravity
            + (jumpSpeed + Mathf.Sqrt(Mathf.Max(0, jumpSpeed * jumpSpeed - 2 * gravity * (maxRise - (jumps - 1) * apex)))) / gravity;
        return new Metrics {
            RowsPerChunk = rows, RowHeight = step, ChunkHeight = step * rows,
            LastRow = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(20, settings.pillarHeight) / step), 1, 1000000),
            TravelLimit = moveSpeed * flight * safety, SingleJumpRise = apex
        };
    }
    public static int MarkerRow(VerticalGenerationSettings.HeightMarker marker, Metrics metrics)
        => Mathf.Clamp(Mathf.RoundToInt(marker.height / metrics.RowHeight), 1, metrics.LastRow - 1);
    public static Landing GetLanding(VerticalGenerationSettings settings, int seed, int row, Metrics metrics)
    {
        row = Mathf.Clamp(row, 0, metrics.LastRow);
        var random = new System.Random(unchecked(seed ^ (row * 73856093)));
        float tower = Mathf.Max(8, settings.pillarWidth);
        // Keep an open shaft between neighbors: solid blocks cannot be jumped through.
        float jitter = Mathf.Min(Mathf.Max(0, settings.horizontalVariation), metrics.TravelLimit * 0.04f, tower * 0.02f);
        float widthLimit = Mathf.Max(2.5f, (tower - 1.8f - 4 * jitter) * 0.5f);
        float minWidth = Mathf.Clamp(Mathf.Min(settings.platformWidth.x, settings.platformWidth.y), 2.5f, widthLimit);
        float pitch = Mathf.Clamp(settings.zigzagWidth, 5, Mathf.Max(5, metrics.TravelLimit + minWidth - 2.5f - 2 * jitter));
        float maxWidth = Mathf.Clamp(Mathf.Max(settings.platformWidth.x, settings.platformWidth.y), minWidth,
            Mathf.Max(minWidth, Mathf.Min(widthLimit, pitch - 2 * jitter - 1.8f)));
        pitch = Mathf.Min(pitch, tower - maxWidth - 2 * jitter);
        int lanes = Mathf.Max(2, Mathf.FloorToInt((tower - maxWidth - 2 * jitter) / pitch) + 1);
        pitch = Mathf.Min(pitch, (tower - maxWidth - 2 * jitter) / (lanes - 1));
        var shape = (RockShape)random.Next(4);
        float width = Range(random, minWidth, maxWidth);
        float depth = Mathf.Max(0.3f, settings.platformThickness) * Range(random, 0.8f, 1.2f);
        if (shape == RockShape.Shelf) { width = Mathf.Lerp(width, maxWidth, 0.6f); depth *= 0.6f; }
        if (shape == RockShape.Spire) { width = Mathf.Lerp(minWidth, width, 0.35f); depth *= 1.8f; }
        if (shape == RockShape.Terrace) { width = Mathf.Lerp(width, maxWidth, 0.4f); depth *= 1.4f; }
        // Two rows may return to the same lane; leave room for a standing player there.
        depth = Mathf.Min(depth, metrics.RowHeight * (2 - 2 * settings.heightVariation) - 1.6f);
        bool wide = row == 0 || row == metrics.LastRow || row % Mathf.Max(1, settings.wideLandingEvery) == 0;
        if (wide) { width = row == 0 ? tower : maxWidth; shape = RockShape.Shelf; depth = settings.platformThickness; }
        int markerIndex = -1;
        if (settings.heightMarkers != null)
            for (int i = 0; i < settings.heightMarkers.Length; i++)
                if (settings.heightMarkers[i] != null && MarkerRow(settings.heightMarkers[i], metrics) == row)
                { markerIndex = i; wide = true; width = maxWidth; shape = RockShape.Shelf; depth = settings.platformThickness; break; }
        float x = row == 0 ? 0 : (Lane(seed, row, lanes) - (lanes - 1) * 0.5f) * pitch + Range(random, -jitter, jitter);
        float edge = Mathf.Max(0, (tower - width) * 0.5f);
        x = Mathf.Clamp(x, -edge, edge);
        float yJitter = wide || row % metrics.RowsPerChunk == 0 ? 0 : Range(random, -1, 1) * Mathf.Clamp(settings.heightVariation, 0, 0.12f);
        var landing = new Landing {
            Row = row, Center = new Vector2(x, (row + yJitter) * metrics.RowHeight), Width = width, Thickness = Mathf.Max(0.3f, depth), Shape = shape,
            Color = Color.HSVToRGB(Range(random, 0.015f, 0.1f), Range(random, 0.16f, 0.38f), Range(random, 0.3f, 0.5f)),
            EnemyIndex = -1, MarkerIndex = markerIndex, EnemySide = random.Next(2) == 0 ? -1 : 1
        };
        // Fixed eligible rows make spawn choices independent of which chunks were visited first.
        if (!wide && row >= settings.firstEnemyRow && row % Mathf.Max(1, settings.enemyRowSpacing) == 0
            && random.NextDouble() < settings.enemyChance)
            landing.EnemyIndex = PickEnemy(settings, row, random);
        return landing;
    }
    private static int Lane(int seed, int row, int count)
    {
        // Short seeded excursions meet at a shared anchor; random access costs at most 12 steps.
        const int length = 12;
        int center = count / 2, lane = center;
        var random = new System.Random(unchecked(seed ^ ((row / length) * 19349663)));
        int direction = random.Next(2) == 0 ? -1 : 1;
        for (int step = 1; step <= row % length; step++)
        {
            if (random.NextDouble() < 0.22) direction = -direction;
            int next = lane + direction;
            if (next < 0 || next >= count || Mathf.Abs(next - center) > length - step) direction = -direction;
            lane += direction;
        }
        return lane;
    }
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
    private static float Range(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
}
