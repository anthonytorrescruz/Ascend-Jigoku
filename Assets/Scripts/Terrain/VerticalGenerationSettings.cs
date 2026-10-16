using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Ascend Jigoku/Vertical Generation Settings")]
public sealed class VerticalGenerationSettings : ScriptableObject
{
    [Header("Pillar / Chunky Ledges")]
    public GameObject platformPrefab;
    [Min(20)] public float pillarHeight = 1200;
    [Min(8)] public float pillarWidth = 76;
    public Vector2 platformWidth = new Vector2(5, 11);
    [Tooltip("Base depth; shelves, blocks, spires and terraces scale this differently.")]
    [Min(0.2f)] public float platformThickness = 1.8f;
    [Tooltip("Spacing between route lanes. Seeded paths wander across the pillar; gaps are capped by jump reach.")]
    [Min(0)] public float zigzagWidth = 14;
    [Range(0, 1)] public float horizontalVariation = 0.7f;
    [Range(0, 0.12f)] public float heightVariation = 0.1f;
    [Range(0.3f, 0.95f)] public float jumpSafety = 0.9f;
    [Min(1)] public int wideLandingEvery = 15;
    [Header("Streaming - World Units")]
    [Min(8)] public float chunkHeight = 40;
    [Range(4, 24)] public int platformsPerChunk = 10;
    [Min(8)] public float loadAbove = 64;
    [Min(8)] public float keepBelow = 28;
    [Min(0)] public float unloadBuffer = 12;
    [Range(1, 12)] public int platformsPerFrame = 3;
    [Header("Long Falls")]
    [Min(2)] public float fallDistance = 12;
    [Min(1)] public int fallDamage = 1;
    [Min(2)] public float fallBounceHeight = 14;
    [Header("Run")]
    public bool randomizeSeed = true;
    public int seed = 12345;
    [Header("Height Markers - Snap to Nearest Safe Landing")]
    public HeightMarker[] heightMarkers = Array.Empty<HeightMarker>();
    [Header("Enemies")]
    [Range(0, 1)] public float enemyChance = 0.45f;
    [Min(1)] public int firstEnemyRow = 5;
    [Min(1)] public int enemyRowSpacing = 3;
    [Min(0)] public float clearLandingHalfWidth = 1.25f;
    public EnemySpawn[] enemies = Array.Empty<EnemySpawn>();
    [Serializable]
    public sealed class HeightMarker
    {
        public string id = "Checkpoint";
        [Min(1)] public float height = 200;
        public bool checkpoint = true;
        public GameObject prefab;
    }
    [Serializable]
    public sealed class EnemySpawn
    {
        public GameObject prefab;
        [Min(1)] public int weight = 1;
        [Min(1)] public int firstRow = 1;
    }
}
