using UnityEngine;
using System.Collections;

public class TwoDSpriteSpawner : MonoBehaviour
{
    [Header("Prefab Settings")]
    public GameObject spritePrefab;   // The 2D sprite prefab to spawn
    public Transform spawnPoint;      // Parent or position to spawn from

    //[Header("Spawning Settings")]
    //public float spawnInterval = 2f; // Seconds between spawns
    //public float randomSizeRange = 0.5f; // How much size can vary
    //public bool randomRotation = true; // Whether to randomize rotation

    //private float timer;

    //void Start()
    //{
        //timer = 0f;
    //}

    //void Update()
    //{
        //timer += Time.deltaTime;
        //if (timer >= spawnInterval)
        //{
            //SpawnSprite();
            //timer = 0f;
        //}
    //}

    private void Start()
    {
        // Spawn at spawnPoint position
        GameObject spawned = Instantiate(spritePrefab, spawnPoint.position, Quaternion.identity);

        // Optional: randomize size
        /*if (spritePrefab != null)
        {
            float size = spritePrefab.transform.localScale.x + Random.Range(-randomSizeRange, randomSizeRange);
            spawned.transform.localScale = new Vector3(size, size, 1f);
        }*/

        // Optional: randomize rotation
        /*if (randomRotation)
        {
            spawned.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        }*/
    }
}
