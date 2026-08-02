# Object Pooling System

A flexible and efficient object pooling system for Unity that helps optimize performance by reusing objects instead of constantly instantiating and destroying them. Built on top of `UnityEngine.Pool.ObjectPool<T>`.

## Features

- **Component & GameObject pools**: `Spawn<T>` for any `Component`, or plain `GameObject`
- **Automatic pool creation**: pools are created on-demand with configurable capacity and max size
- **Fluent builder API**: `CreatePool(...).WithCapacity(...).WithPrewarm(...).Prewarm()`
- **IPooledObject interface**: optional spawn/despawn lifecycle callbacks per object
- **Global events**: `OnSpawn` / `OnDespawn` static events reporting pool name and instance
- **Statistics**: per-pool gets, releases, peak active count, lifetime
- **Integrity checking**: periodic sweep for destroyed-but-tracked objects, with recovery
- **Inspector view**: live pool info in the `ObjectPooler` GameObject (Editor only)
- **DontDestroyOnLoad**: the pooler persists across scenes automatically

Everything lives in the `OPS` namespace.

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
namespace OPS
{
    public interface IPooledObject
    {
        void OnSpawn();   // Called when retrieved from pool
        void OnDespawn(); // Called when returned to pool
    }
}
```

For Component pools the interface is checked on the spawned component itself.
For GameObject pools it is resolved with `GetComponent<IPooledObject>()`.

### 2. ObjectPooler (Singleton)

Static facade for all pool operations. The singleton GameObject is created automatically on first use.

### 3. PoolRegistry

Holds all pool state. Not used directly — `ObjectPooler` delegates to it.

## Usage

### Basic Usage

```csharp
using OPS;
using UnityEngine;

public class Example : MonoBehaviour
{
    [SerializeField] private MyComponent prefab;

    void SpawnObject()
    {
        // Get object from pool (creates the pool if it doesn't exist)
        MyComponent obj = ObjectPooler.Spawn("MyPool", prefab, defaultCapacity: 10, maxSize: 100);

        obj.transform.position = Vector3.zero;

        // Return to pool when done
        ObjectPooler.Release(obj);
    }
}
```

GameObject prefabs work the same way:

```csharp
GameObject go = ObjectPooler.Spawn("Effects", effectPrefab);
ObjectPooler.Release(go);
```

> Pools are keyed by **type + name**, so `Spawn("fx", bulletComponent)` and
> `Spawn("fx", someGameObject)` are two different pools.

### Fluent builder (prewarming)

```csharp
// Pre-populate 20 bullets, max 100, without getting one back
ObjectPooler.CreatePool("BulletPool", bulletPrefab)
            .WithCapacity(20)
            .WithMaxSize(100)
            .WithPrewarm(20)
            .Prewarm();

// Or prewarm and take one immediately
Bullet first = ObjectPooler.CreatePool("BulletPool", bulletPrefab)
                           .WithPrewarm(20)
                           .BuildAndGet();
```

### With IPooledObject Interface

```csharp
using OPS;
using UnityEngine;

public class Bullet : MonoBehaviour, IPooledObject
{
    public void OnSpawn()
    {
        // Initialize when spawned
        GetComponent<Rigidbody>().linearVelocity = transform.forward * 10f;
    }

    public void OnDespawn()
    {
        // Cleanup when despawned
        GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
    }
}
```

## Global Spawn / Despawn Events

`ObjectPooler` raises two **static** events for every pooled instance, so systems that
don't own the object (audio, VFX budgeting, analytics, debug overlays) can react without
touching the prefab.

```csharp
public readonly struct PoolEventArgs
{
    public readonly string     PoolName;   // name passed to Spawn/CreatePool, e.g. "BulletPool"
    public readonly string     PoolKey;    // type-qualified key, e.g. "Bullet::BulletPool"
    public readonly GameObject GameObject; // the instance
}

public static event Action<PoolEventArgs> OnSpawn;
public static event Action<PoolEventArgs> OnDespawn;
```

### Subscribing

```csharp
using OPS;
using UnityEngine;

public class SpawnMonitor : MonoBehaviour
{
    void OnEnable()
    {
        ObjectPooler.OnSpawn   += HandleSpawn;
        ObjectPooler.OnDespawn += HandleDespawn;
    }

    void OnDisable()   // ALWAYS unsubscribe — the events are static
    {
        ObjectPooler.OnSpawn   -= HandleSpawn;
        ObjectPooler.OnDespawn -= HandleDespawn;
    }

    void HandleSpawn(PoolEventArgs e)
    {
        if (e.PoolName != "BulletPool") return;   // events are global, filter yourself
        Debug.Log($"spawned {e.GameObject.name} from {e.PoolName}");
    }

    void HandleDespawn(PoolEventArgs e)
    {
        // still safe to read the object's final position/parent here
        Debug.Log($"despawned at {e.GameObject.transform.position}");
    }
}
```

### When they fire

| Event | Timing |
| --- | --- |
| `OnSpawn` | after the object is activated **and** after `IPooledObject.OnSpawn()` — the instance is fully initialized |
| `OnDespawn` | after `IPooledObject.OnDespawn()`, **before** `SetActive(false)` and before the transform is reset — final position/rotation/parent are still readable |

### Rules and caveats

- **Static events leak.** Subscribe in `OnEnable`, unsubscribe in `OnDisable`/`OnDestroy`.
  A handler on a destroyed object keeps the whole object alive and will throw on invoke.
- **Statics are reset on play.** `ResetStatics()` runs at `SubsystemRegistration`, so
  subscribers from a previous play session cannot survive when *Enter Play Mode Options*
  disables Domain Reload.
- **Handler exceptions are swallowed** and logged through `PoolLogger.LogError` so one bad
  subscriber cannot corrupt the pool's get/release cycle. Keep handlers cheap — they run
  synchronously inside every spawn and release.
- **Not raised on destruction.** `DestroyPooledObject`, `ClearPool` and `ClearAllPools`
  destroy objects without a release, so no `OnDespawn` fires for them. `ReleaseAndClearPool`
  does release first, so it does fire.
- **Don't reparent inside `OnDespawn`.** The pool resets parent, position, rotation and
  scale immediately after the event returns.

## API Reference

All methods are `static` on `OPS.ObjectPooler`.

### Spawn / Release

```csharp
T          Spawn<T>(string poolName, T prefab, int defaultCapacity = 10, int maxSize = 100) where T : Component
GameObject Spawn(string poolName, GameObject prefab, int defaultCapacity = 10, int maxSize = 100)

void Release<T>(T obj) where T : Component
void Release(GameObject obj)
```

`Spawn` creates the pool on first call. `Release` is a no-op for objects the pooler isn't tracking.

### Builders

```csharp
PoolBuilder<T>         CreatePool<T>(string poolName, T prefab) where T : Component
GameObjectPoolBuilder  CreatePool(string poolName, GameObject prefab)
```

Both builders expose `WithCapacity(int)`, `WithMaxSize(int)`, `WithPrewarm(int)`,
then terminate with `Prewarm()` (no return) or `BuildAndGet()` (returns one instance).

### Destroy

```csharp
void DestroyPooledObject<T>(T obj) where T : Component
void DestroyPooledObject(GameObject obj)
```

Removes the object from tracking and destroys it. Use when an instance must not go back to the pool.

### Release / Clear

```csharp
void ReleaseAllPool(string poolName)       // return every active object of that pool
void ReleaseAndClearPool(string poolName)  // release all, then dispose the pool
void ReleaseAllPools()                     // return every active object of every pool
void ClearPool(string poolName)            // dispose the pool (does not release actives first)
void ClearAllPools()                       // dispose everything
```

### Statistics & integrity

```csharp
PoolStatistics                        GetStatistics(string poolName)
Dictionary<string, PoolStatistics>    GetAllStatistics()
int                                   GetActiveObjectCount(string poolName)
int                                   ValidatePoolIntegrity()   // returns issues fixed
void                                  RecoverCorruptedPools()
```

`PoolStatistics` exposes `totalGets`, `totalReleases`, `peakActiveCount`,
`currentActiveCount`, `creationTime`, `lastGetTime`, `lastReleaseTime`, `GetLifetime()`.

An integrity sweep runs automatically every 10 seconds.

### Logging

```csharp
PoolLogger.LogLevel = PoolLogLevel.None;   // None | Errors | Warnings | All (default: Warnings)
```

## Best Practices

1. **Use consistent pool names** — declare them as `const string`.
2. **Set appropriate capacities** — `defaultCapacity` near steady-state usage, `maxSize` as the hard ceiling.
3. **Implement `IPooledObject`** for per-object reset; use the global events for cross-cutting systems.
4. **Always release** — never `Destroy()` a pooled object directly; use `Release` or `DestroyPooledObject`.
5. **Unsubscribe from the static events** in `OnDisable`.
6. **Keep event handlers cheap** — they run on every spawn and release.

## Example Scenario: Bullet System

```csharp
using OPS;
using UnityEngine;

public class Gun : MonoBehaviour
{
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private Transform firePoint;
    private const string BULLET_POOL = "BulletPool";

    void Start()
    {
        ObjectPooler.CreatePool(BULLET_POOL, bulletPrefab)
                    .WithCapacity(20)
                    .WithMaxSize(100)
                    .WithPrewarm(20)
                    .Prewarm();
    }

    void Fire()
    {
        Bullet bullet = ObjectPooler.Spawn(BULLET_POOL, bulletPrefab, 20, 100);
        bullet.transform.SetPositionAndRotation(firePoint.position, firePoint.rotation);
    }
}

public class Bullet : MonoBehaviour, IPooledObject
{
    [SerializeField] private float lifetime = 3f;
    private float spawnTime;

    public void OnSpawn()
    {
        spawnTime = Time.time;
        GetComponent<Rigidbody>().linearVelocity = transform.forward * 20f;
    }

    public void OnDespawn()
    {
        GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
    }

    void Update()
    {
        if (Time.time - spawnTime >= lifetime)
            ObjectPooler.Release(this);
    }

    void OnCollisionEnter(Collision collision)
    {
        ObjectPooler.Release(this);
    }
}

// Spawn an impact VFX for every bullet despawn, without the Bullet knowing about VFX
public class ImpactFx : MonoBehaviour
{
    [SerializeField] private GameObject impactPrefab;

    void OnEnable()  => ObjectPooler.OnDespawn += HandleDespawn;
    void OnDisable() => ObjectPooler.OnDespawn -= HandleDespawn;

    void HandleDespawn(PoolEventArgs e)
    {
        if (e.PoolName != "BulletPool") return;
        var fx = ObjectPooler.Spawn("ImpactFx", impactPrefab);
        fx.transform.position = e.GameObject.transform.position;
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

- Verify the prefab is assigned and the pool name matches
- Check `OnSpawn()` isn't deactivating or moving the object off-screen

**Issue**: Pool growing unbounded

- Set an appropriate `maxSize`
- Make sure every spawn has a matching `Release`

**Issue**: Objects behaving incorrectly on reuse

- Reset all mutable state in `OnDespawn()` / `OnSpawn()`
- Physics: zero out velocity and angular velocity

**Issue**: "Cleaned up N destroyed objects from tracking" warning

- Something called `Destroy()` on a pooled object. Use `ObjectPooler.Release` or
  `ObjectPooler.DestroyPooledObject` instead.

**Issue**: Event handler fires on a destroyed listener

- A subscriber forgot to unsubscribe. Pair every `+=` with a `-=`.

## License

This object pooling system is part of your Unity project.
