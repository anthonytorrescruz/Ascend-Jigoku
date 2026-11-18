using UnityEngine;
using System.Collections;

// Spawns one copy of a prefab at a point when the scene starts.
public class TwoDSpriteSpawner : MonoBehaviour
{
    [Header("Prefab Settings")]
    public GameObject spritePrefab;   // The 2D sprite prefab to spawn
    public Transform spawnPoint;      // Parent or position to spawn from

    void Start()
    {
        if (spritePrefab == null || spawnPoint == null) return;
        Instantiate(spritePrefab, spawnPoint.position, Quaternion.identity);
    }
}
