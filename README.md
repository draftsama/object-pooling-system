# Object Pooling System

A flexible and efficient object pooling system for Unity that helps optimize performance by reusing objects instead of constantly instantiating and destroying them.

## Features

- **Generic Pool Support**: Works with any Unity Component type
- **Automatic Pool Management**: Creates pools on-demand with configurable capacity and max size
- **IPooledObject Interface**: Optional interface for spawn/despawn lifecycle callbacks
- **Singleton Pattern**: Easy global access through static methods
- **Multiple Pool Support**: Manage different pools with unique names
- **Delayed Release**: Option for immediate or delayed object release
- **Memory Management**: Proper cleanup and disposal of pools
- **DontDestroyOnLoad Support**: Optional persistence across scenes

### How to add submodule by command line
```bash
git submodule add  <url> <relative_path>
```
Example
```bash
git submodule add git@github.com:draftsama/object-pooling-system.git Assets/object-pooling-system
```



## Components

### 1. IPooledObject Interface
Optional interface for objects that need lifecycle callbacks:
```csharp
public interface IPooledObject
{
    void OnSpawn();   // Called when retrieved from pool
    void OnDespawn(); // Called when returned to pool
}
```

### 2. ObjectPooler (Singleton)
Main pooling system manager that handles all pool operations.

## Usage

### Basic Usage

```csharp
using UnityEngine;

public class Example : MonoBehaviour
{
    [SerializeField] private MyComponent prefab;
    
    void SpawnObject()
    {
        // Get object from pool (creates pool if it doesn't exist)
        MyComponent obj = ObjectPooler.GetPool("MyPool", prefab, defaultCapacity: 10, maxSize: 100);
        
        // Use the object
        obj.transform.position = Vector3.zero;
        
        // Return to pool when done
        ObjectPooler.ReleasePool(obj, immediate: false);
    }
}
```

### With IPooledObject Interface

```csharp
public class Bullet : MonoBehaviour, IPooledObject
{
    public void OnSpawn()
    {
        // Initialize when spawned
        GetComponent<Rigidbody>().velocity = transform.forward * 10f;
    }
    
    public void OnDespawn()
    {
        // Cleanup when despawned
        GetComponent<Rigidbody>().velocity = Vector3.zero;
    }
}
```

### Advanced Features

```csharp
// Configure DontDestroyOnLoad
ObjectPooler.DontDestroyOnLoad(true);

// Clear specific pool
ObjectPooler.ClearPool<Bullet>("BulletPool");

// Clear all pools
ObjectPooler.ClearAllPools();

// Delayed release (waits one frame)
ObjectPooler.ReleasePool(obj, immediate: false);

// Immediate release
ObjectPooler.ReleasePool(obj, immediate: true);
```

## API Reference

### Static Methods

#### GetPool<T>
```csharp
public static T GetPool<T>(string poolName, T prefab, int defaultCapacity = 10, int maxSize = 100) where T : Component
```
Retrieves an object from the pool. Creates the pool if it doesn't exist.

**Parameters:**
- `poolName`: Unique identifier for the pool
- `prefab`: Prefab to instantiate for new objects
- `defaultCapacity`: Initial pool size (default: 10)
- `maxSize`: Maximum pool size (default: 100)

**Returns:** Pooled object of type T

#### ReleasePool<T>
```csharp
public static void ReleasePool<T>(T obj, bool immediate = false) where T : Component
```
Returns an object to its pool.

**Parameters:**
- `obj`: Object to return
- `immediate`: If true, returns immediately; if false, waits one frame

#### ClearPool<T>
```csharp
public static void ClearPool<T>(string poolName) where T : Component
```
Clears and destroys a specific pool.

**Parameters:**
- `poolName`: Name of the pool to clear

#### ClearAllPools
```csharp
public static void ClearAllPools()
```
Clears and destroys all pools.

#### DontDestroyOnLoad
```csharp
public static void DontDestroyOnLoad(bool value)
```
Configures whether the ObjectPooler persists across scenes.

**Parameters:**
- `value`: True to persist, false otherwise

## Best Practices

1. **Use Consistent Pool Names**: Use clear, consistent naming for your pools
2. **Set Appropriate Capacities**: Start with realistic defaultCapacity based on expected usage
3. **Implement IPooledObject**: Use the interface for proper initialization/cleanup
4. **Release When Done**: Always return objects to the pool when finished
5. **Avoid Over-Pooling**: Don't create pools for objects that are rarely used
6. **Use Delayed Release**: Prefer `immediate: false` to avoid timing conflicts

## Example Scenario: Bullet System

```csharp
public class Gun : MonoBehaviour
{
    [SerializeField] private Bullet bulletPrefab;
    private const string BULLET_POOL = "BulletPool";
    
    void Start()
    {
        // Pre-warm pool with 20 bullets, max 100
        for (int i = 0; i < 20; i++)
        {
            var bullet = ObjectPooler.GetPool(BULLET_POOL, bulletPrefab, 20, 100);
            ObjectPooler.ReleasePool(bullet, true);
        }
    }
    
    void Fire()
    {
        Bullet bullet = ObjectPooler.GetPool(BULLET_POOL, bulletPrefab, 20, 100);
        bullet.transform.position = firePoint.position;
        bullet.transform.rotation = firePoint.rotation;
    }
}

public class Bullet : MonoBehaviour, IPooledObject
{
    [SerializeField] private float lifetime = 3f;
    private float spawnTime;
    
    public void OnSpawn()
    {
        spawnTime = Time.time;
        GetComponent<Rigidbody>().velocity = transform.forward * 20f;
    }
    
    public void OnDespawn()
    {
        GetComponent<Rigidbody>().velocity = Vector3.zero;
    }
    
    void Update()
    {
        if (Time.time - spawnTime >= lifetime)
        {
            ObjectPooler.ReleasePool(this, false);
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        // Handle collision
        ObjectPooler.ReleasePool(this, false);
    }
}
```

## Performance Benefits

- **Reduced GC Pressure**: Minimizes garbage collection by reusing objects
- **Faster Spawning**: Object retrieval is faster than Instantiate
- **Predictable Performance**: No allocation spikes during gameplay
- **Memory Efficiency**: Controlled memory usage with max pool sizes

## Troubleshooting

**Issue**: Objects not appearing
- Check that `OnSpawn()` properly activates/initializes the object
- Verify the prefab is assigned correctly

**Issue**: Pool growing unbounded
- Set appropriate `maxSize` parameter
- Ensure objects are being released properly

**Issue**: Objects behaving incorrectly on reuse
- Implement `OnDespawn()` to properly reset state
- Check that all properties are reset in `OnSpawn()`

## License

This object pooling system is part of your Unity project.
