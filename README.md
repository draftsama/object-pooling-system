# Object Pooling System

Reuse GameObjects instead of instantiating and destroying them. Everything lives in the `OPS`
namespace and goes through the static `ObjectPooler` class. Terms used below are defined in
[GLOSSARY.md](GLOSSARY.md).

## Install as a submodule

```bash
git submodule add git@github.com:draftsama/object-pooling-system.git Assets/object-pooling-system
```

## How it works

- A **pool** is identified by its **name** only. The first `Spawn` or `Prewarm` with a name creates
  the pool from the prefab you pass; later calls reuse it.
- The first call creates a `DontDestroyOnLoad` GameObject named `ObjectPooler`. Each pool keeps its
  inactive instances under an inactive `Pool_<name>` child of it, so pools survive scene loads.
- A spawned instance is placed under the `parent` you pass, or at the root of the active scene, at
  the prefab's local position, rotation and scale, **before** it is activated. Unparented active
  instances are destroyed with their scene, like `Instantiate`d objects; the pool forgets them.
- Works in Play mode only.

## Usage

```csharp
using OPS;
using UnityEngine;

public class Gun : MonoBehaviour
{
    private const string BulletPool = "Bullet";

    [SerializeField] private Bullet _bulletPrefab;
    [SerializeField] private Transform _muzzle;

    private void Start()
    {
        ObjectPooler.Prewarm(BulletPool, _bulletPrefab, count: 20, maxInactive: 100);
    }

    private void Fire()
    {
        Bullet bullet = ObjectPooler.Spawn(BulletPool, _bulletPrefab);
        bullet.transform.SetPositionAndRotation(_muzzle.position, _muzzle.rotation);
    }
}

public class Bullet : MonoBehaviour, IPooledObject
{
    public void OnSpawn() { }
    public void OnDespawn() { }

    private void OnCollisionEnter(Collision collision) => ObjectPooler.Despawn(this);
}
```

UI prefabs: pass the Canvas (or a child of it) as `parent`.

```csharp
var toast = ObjectPooler.Spawn("Toast", toastPrefab, canvasTransform);
```

## IPooledObject

```csharp
public interface IPooledObject
{
    void OnSpawn();   // after the instance is placed and activated
    void OnDespawn(); // before it is deactivated
}
```

Every `IPooledObject` on the prefab's **root** GameObject is called, in component order. Components
on children, or added at runtime, are not called. An exception in one callback is logged and the
others still run.

## Drop-ins (`Runtime/Components`)

Add one to a prefab; spawn it as usual and it despawns itself.

| Drop-in | Despawns the instance when |
| --- | --- |
| `DespawnAfterDelay` | `Delay` seconds have passed since spawn (`_useUnscaledTime` ignores `Time.timeScale`) |
| `DespawnWhenParticlesFinish` | the `ParticleSystem` and all its children have finished (looping systems never finish) |

## Events

```csharp
public static event Action<PoolEventArgs> Spawned;    // after every OnSpawn
public static event Action<PoolEventArgs> Despawned;  // after every OnDespawn, before deactivation

public readonly struct PoolEventArgs
{
    public readonly string     PoolName;
    public readonly GameObject Instance;
}
```

- Events are static: subscribe in `OnEnable`, unsubscribe in `OnDisable`.
- They are reset when entering Play mode, so they are safe with Domain Reload disabled.
- An exception in a handler is logged and does not break the pool.
- Handlers run synchronously inside every spawn and despawn; keep them cheap.

## When callbacks and events run

| Call | `OnSpawn` + `Spawned` | `OnDespawn` + `Despawned` |
| --- | --- | --- |
| `Spawn` | yes | — |
| `Prewarm` | no | no |
| `Despawn`, `DespawnAll` | — | yes, for each active instance |
| `DestroyInstance` | — | yes, if the instance was active |
| `DestroyPool`, `DestroyAllPools` | — | yes, for each active instance, then everything is destroyed |
| `ObjectPooler` destroyed (quit, leaving Play mode) | — | no |

## API

All members are `static` on `OPS.ObjectPooler`. `initialCapacity` and `maxInactive` apply only when
the call creates the pool.

```csharp
GameObject Spawn(string poolName, GameObject prefab, Transform parent = null, int initialCapacity = 10, int maxInactive = 100)
T          Spawn<T>(string poolName, T prefab, Transform parent = null, int initialCapacity = 10, int maxInactive = 100) where T : Component

void Despawn(GameObject instance)
void Despawn(Component instance)
void DespawnAll(string poolName)
void DespawnAll()

void Prewarm(string poolName, GameObject prefab, int count, int initialCapacity = 10, int maxInactive = 100)
void Prewarm(string poolName, Component prefab, int count, int initialCapacity = 10, int maxInactive = 100)

void DestroyInstance(GameObject instance)
void DestroyInstance(Component instance)
void DestroyPool(string poolName)
void DestroyAllPools()

int  CountActive(string poolName)
bool TryGetSnapshot(string poolName, out PoolSnapshot snapshot)
void GetAllSnapshots(List<PoolSnapshot> results)   // clears, then fills; no allocation
```

- `initialCapacity` reserves storage only; it does not create instances. Use `Prewarm` for that.
- `maxInactive` caps how many inactive instances a pool keeps. A despawn beyond it destroys the
  instance. The number of active instances is not capped.
- `Prewarm` creates inactive instances until the pool holds `count`, capped at `maxInactive`. The
  instances' `Awake` runs on their first spawn, not during prewarm.
- `Spawn` with a prefab other than the one the pool was created with logs an error once and spawns
  from the existing pool. `Spawn<T>` throws `InvalidOperationException` if that prefab has no `T`.
- `Despawn` of an object no pool spawned, or one already despawned, logs a warning.
- `PoolSnapshot` has `Name`, `Active`, `Inactive`, `SpawnCount`, `DespawnCount`, `PeakActive`,
  `InitialCapacity` and `MaxInactive`. In the Editor, the `ObjectPooler` Inspector shows them live.

### Logging

```csharp
PoolLogger.LogLevel = PoolLogLevel.None;   // None | Errors | Warnings (default) | All
```

## Rules

- Never `Destroy()` a spawned instance; use `Despawn` or `DestroyInstance`.
- Reset per-spawn state in `OnSpawn` / `OnDespawn`.
- Don't call `Despawn` from an instance's own `OnDisable` / `OnDestroy`: while its hierarchy is being
  torn down Unity rejects the reparent.
- Don't reparent an instance inside `OnDespawn` or a `Despawned` handler; the pool reparents it
  right after.

## Migrating from 1.x

| 1.x | Now |
| --- | --- |
| `Release(obj)` | `Despawn(obj)` |
| `ReleaseAllPool(name)` | `DespawnAll(name)` |
| `ReleaseAllPools()` | `DespawnAll()` |
| `ClearPool(name)`, `ReleaseAndClearPool(name)` | `DestroyPool(name)` (always despawns first) |
| `ClearAllPools()` | `DestroyAllPools()` |
| `DestroyPooledObject(obj)` | `DestroyInstance(obj)` (now runs `OnDespawn` if active) |
| `CreatePool(name, prefab).WithCapacity(c).WithMaxSize(m).WithPrewarm(n).Prewarm()` | `Prewarm(name, prefab, n, c, m)` |
| `CreatePool(...).BuildAndGet()` | `Prewarm(...)` then `Spawn(...)` |
| `defaultCapacity`, `maxSize` parameters | `initialCapacity`, `maxInactive` |
| `GetActiveObjectCount(name)` | `CountActive(name)` |
| `GetStatistics(name)`, `GetAllStatistics()` | `TryGetSnapshot(name, out s)`, `GetAllSnapshots(list)` |
| `ValidatePoolIntegrity()`, `RecoverCorruptedPools()` | removed; destroyed instances are dropped automatically |
| `GetInstance()` | removed |
| events `OnSpawn` / `OnDespawn` | `Spawned` / `Despawned` |
| `PoolEventArgs.GameObject`, `PoolEventArgs.PoolKey` | `PoolEventArgs.Instance`; no key, the name is the identity |
| `PooledReleaseDelay`, `PooledParticle` | `DespawnAfterDelay`, `DespawnWhenParticlesFinish` (same GUIDs, prefabs keep working) |

Behaviour changes:

- Pools are keyed by name only: `Spawn<Bullet>("fx", …)` and `Spawn("fx", go)` now share one pool.
- Pools survive scene loads; previously their inactive instances were destroyed with the scene and
  the next `Spawn` threw `MissingReferenceException`.
- Every `IPooledObject` on the root is called, for both `Spawn` and `Spawn<T>`.
- `Spawn` restores the prefab's local transform; despawn no longer forces zero/identity/one.
- `Spawn` with a parent reparents before activation, so `OnEnable` and `OnSpawn` see the final parent.
- `Prewarm` no longer runs callbacks or events.
