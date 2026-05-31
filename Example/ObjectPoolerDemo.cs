using OPS;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Demonstration of how to use the ObjectPooler system.
/// Attach this to a GameObject in your scene to test the pooling system.
/// </summary>
public class ObjectPoolerDemo : MonoBehaviour
{
    [Header("Pool Settings")]
    [SerializeField] private GameObject prefab;
    [SerializeField] private string poolName = "ExamplePool";
    [SerializeField] private int defaultCapacity = 10;
    [SerializeField] private int maxSize = 50;
    
    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 0.5f;
    [SerializeField] private float spawnRadius = 3f;
    [SerializeField] private bool autoSpawn = true;
    
    private float lastSpawnTime;

    private void Update()
    {
        if (autoSpawn && Time.time - lastSpawnTime >= spawnInterval)
        {
            SpawnObject();
            lastSpawnTime = Time.time;
        }
        
        // Manual spawn with spacebar
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            SpawnObject();
        }
        
        // Clear all pools with C key
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            ObjectPooler.ClearAllPools();
            Debug.Log("All pools cleared!");
        }
        
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            ObjectPooler.ReleaseAndClearPool(poolName);
            Debug.Log($"All objects in pool {poolName} released!");
        }
    }

    private void SpawnObject()
    {
        if (prefab == null)
        {
            Debug.LogError("Prefab is not assigned!");
            return;
        }
        // Get object from pool
        GameObject obj = ObjectPooler.Spawn(poolName, prefab, defaultCapacity, maxSize);
        
        // Set random position within radius
        Vector3 randomPos = transform.position + Random.insideUnitSphere * spawnRadius;
        randomPos.z = 0; // Keep on 2D plane
        obj.transform.position = randomPos;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize spawn radius in editor
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
