using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OPS
{
    /// <summary>
    /// Static entry point for every pool. The first Spawn or Prewarm in Play mode creates a
    /// DontDestroyOnLoad "ObjectPooler" GameObject that keeps every pool and its inactive instances,
    /// so pools survive scene loads. Active instances live in the scene they were spawned into.
    /// Pools are identified by name only.
    /// </summary>
    public sealed class ObjectPooler : MonoBehaviour
    {
        public const int DefaultInitialCapacity = 10;
        public const int DefaultMaxInactive = 100;

        private static ObjectPooler _instance;
        private static bool _quitting;
        private PoolRegistry _registry;

        /// <summary>
        /// Raised after an instance is activated and its <see cref="IPooledObject.OnSpawn"/> callbacks ran.
        /// Not raised by <see cref="Prewarm(string, GameObject, int, int, int)"/>.
        /// Static: unsubscribe in OnDisable/OnDestroy.
        /// </summary>
        public static event Action<PoolEventArgs> Spawned;

        /// <summary>
        /// Raised after <see cref="IPooledObject.OnDespawn"/> ran and before the instance is deactivated
        /// and its transform reset, so its final position and parent are still readable.
        /// Not raised by <see cref="DestroyInstance(GameObject)"/>.
        /// </summary>
        public static event Action<PoolEventArgs> Despawned;

        private static void RaiseSpawned(PoolEventArgs args)
        {
            Action<PoolEventArgs> handler = Spawned;
            if (handler == null) return;
            try { handler(args); }
            catch (Exception e) { PoolLogger.LogError($"ObjectPooler.Spawned subscriber threw for {args}: {e}"); }
        }

        private static void RaiseDespawned(PoolEventArgs args)
        {
            Action<PoolEventArgs> handler = Despawned;
            if (handler == null) return;
            try { handler(args); }
            catch (Exception e) { PoolLogger.LogError($"ObjectPooler.Despawned subscriber threw for {args}: {e}"); }
        }

        // Domain Reload can be disabled (Enter Play Mode Options), so statics must be reset by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Spawned = null;
            Despawned = null;
            _instance = null;
            _quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting() => _quitting = true;

        private static PoolRegistry GetOrCreateRegistry()
        {
            if (_instance == null)
            {
                if (_quitting) throw new InvalidOperationException("ObjectPooler cannot create pools while the application is quitting.");
                if (!Application.isPlaying) throw new InvalidOperationException("ObjectPooler works only in Play mode.");
                _instance = new GameObject(nameof(ObjectPooler)).AddComponent<ObjectPooler>();
            }
            return _instance._registry;
        }

        private static bool TryGetRegistry(out PoolRegistry registry)
        {
            registry = _instance != null ? _instance._registry : null;
            return registry != null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _registry = new PoolRegistry(transform, RaiseSpawned, RaiseDespawned);
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable() => SceneManager.sceneUnloaded += OnSceneUnloaded;

        private void OnDisable() => SceneManager.sceneUnloaded -= OnSceneUnloaded;

        private void OnSceneUnloaded(Scene scene) => _registry?.PruneDestroyed();

        private void OnDestroy()
        {
            if (_instance != this) return;
            _registry?.Forget();
            _instance = null;
        }

        // ========== Spawn / Despawn ==========

        /// <summary>
        /// Takes an instance from the pool named <paramref name="poolName"/>, creating the pool on first use.
        /// The instance is placed at the prefab's local position, rotation and scale under
        /// <paramref name="parent"/> (or at the root of the active scene) before it is activated.
        /// <paramref name="initialCapacity"/> and <paramref name="maxInactive"/> apply only when the pool is created.
        /// A later call with a different prefab logs an error once and spawns from the existing pool.
        /// </summary>
        /// <exception cref="InvalidOperationException">Outside Play mode, or while the application is quitting.</exception>
        public static GameObject Spawn(string poolName, GameObject prefab, Transform parent = null,
            int initialCapacity = DefaultInitialCapacity, int maxInactive = DefaultMaxInactive)
            => GetOrCreateRegistry().Spawn(poolName, prefab, parent, initialCapacity, maxInactive);

        /// <inheritdoc cref="Spawn(string, GameObject, Transform, int, int)"/>
        /// <exception cref="InvalidOperationException">The pool's prefab has no <typeparamref name="T"/>.</exception>
        public static T Spawn<T>(string poolName, T prefab, Transform parent = null,
            int initialCapacity = DefaultInitialCapacity, int maxInactive = DefaultMaxInactive) where T : Component
            => GetOrCreateRegistry().Spawn(poolName, prefab, parent, initialCapacity, maxInactive);

        public static GameObject Spawn(string poolName, GameObject prefab, int initialCapacity, int maxInactive = DefaultMaxInactive)
            => Spawn(poolName, prefab, null, initialCapacity, maxInactive);

        public static T Spawn<T>(string poolName, T prefab, int initialCapacity, int maxInactive = DefaultMaxInactive) where T : Component
            => Spawn(poolName, prefab, null, initialCapacity, maxInactive);

        /// <summary>
        /// Returns an instance to its pool. Logs a warning for an object no pool spawned or one already despawned.
        /// Beyond the pool's max inactive count the instance is destroyed instead.
        /// Don't call it from the instance's OnDisable/OnDestroy: during hierarchy teardown Unity rejects the reparent.
        /// </summary>
        public static void Despawn(GameObject instance)
        {
            if (TryGetRegistry(out PoolRegistry registry)) registry.Despawn(instance);
        }

        /// <inheritdoc cref="Despawn(GameObject)"/>
        public static void Despawn(Component instance)
        {
            if (instance != null) Despawn(instance.gameObject);
        }

        /// <summary>Despawns every active instance of one pool.</summary>
        public static void DespawnAll(string poolName)
        {
            if (TryGetRegistry(out PoolRegistry registry)) registry.DespawnAll(poolName);
        }

        /// <summary>Despawns every active instance of every pool.</summary>
        public static void DespawnAll()
        {
            if (TryGetRegistry(out PoolRegistry registry)) registry.DespawnAll();
        }

        // ========== Prewarm ==========

        /// <summary>
        /// Creates inactive instances until the pool holds <paramref name="count"/> of them (capped at max inactive).
        /// No <see cref="IPooledObject"/> callbacks or events run.
        /// </summary>
        public static void Prewarm(string poolName, GameObject prefab, int count,
            int initialCapacity = DefaultInitialCapacity, int maxInactive = DefaultMaxInactive)
            => GetOrCreateRegistry().Prewarm(poolName, prefab, count, initialCapacity, maxInactive);

        /// <inheritdoc cref="Prewarm(string, GameObject, int, int, int)"/>
        public static void Prewarm(string poolName, Component prefab, int count,
            int initialCapacity = DefaultInitialCapacity, int maxInactive = DefaultMaxInactive)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            Prewarm(poolName, prefab.gameObject, count, initialCapacity, maxInactive);
        }

        // ========== Destroy ==========

        /// <summary>
        /// Destroys one instance and removes it from its pool. An active instance runs its
        /// <see cref="IPooledObject.OnDespawn"/> callbacks and raises <see cref="Despawned"/> first.
        /// Destroys a non-pooled object as <c>Object.Destroy</c> would.
        /// </summary>
        public static void DestroyInstance(GameObject instance)
        {
            if (TryGetRegistry(out PoolRegistry registry)) registry.DestroyInstance(instance);
            else if (instance != null) Destroy(instance);
        }

        /// <inheritdoc cref="DestroyInstance(GameObject)"/>
        public static void DestroyInstance(Component instance)
        {
            if (instance != null) DestroyInstance(instance.gameObject);
        }

        /// <summary>Despawns every active instance of the pool (events fire), then destroys the pool and all its instances.</summary>
        public static void DestroyPool(string poolName)
        {
            if (TryGetRegistry(out PoolRegistry registry)) registry.DestroyPool(poolName);
        }

        /// <summary><see cref="DestroyPool"/> for every pool.</summary>
        public static void DestroyAllPools()
        {
            if (TryGetRegistry(out PoolRegistry registry)) registry.DestroyAllPools();
        }

        // ========== Queries ==========

        public static int CountActive(string poolName)
            => TryGetRegistry(out PoolRegistry registry) ? registry.CountActive(poolName) : 0;

        public static bool TryGetSnapshot(string poolName, out PoolSnapshot snapshot)
        {
            if (TryGetRegistry(out PoolRegistry registry)) return registry.TryGetSnapshot(poolName, out snapshot);
            snapshot = default;
            return false;
        }

        /// <summary>Clears <paramref name="results"/> and fills it with one snapshot per pool.</summary>
        public static void GetAllSnapshots(List<PoolSnapshot> results)
        {
            if (TryGetRegistry(out PoolRegistry registry)) registry.GetAllSnapshots(results);
            else results.Clear();
        }

#if UNITY_EDITOR
        [Serializable]
        private sealed class InspectorRow
        {
            public string name;
            public int active;
            public int inactive;
            public int peakActive;
            public int spawnCount;
            public int despawnCount;
            public int maxInactive;
        }

        [SerializeField] private List<InspectorRow> _pools = new List<InspectorRow>();
        private readonly List<PoolSnapshot> _snapshots = new List<PoolSnapshot>();
        private float _nextInspectorRefresh;

        private void Update()
        {
            if (Time.unscaledTime < _nextInspectorRefresh) return;
            _nextInspectorRefresh = Time.unscaledTime + 0.5f;

            _registry.GetAllSnapshots(_snapshots);
            while (_pools.Count < _snapshots.Count) _pools.Add(new InspectorRow());
            if (_pools.Count > _snapshots.Count) _pools.RemoveRange(_snapshots.Count, _pools.Count - _snapshots.Count);

            for (int i = 0; i < _snapshots.Count; i++)
            {
                PoolSnapshot s = _snapshots[i];
                InspectorRow row = _pools[i];
                row.name = s.Name;
                row.active = s.Active;
                row.inactive = s.Inactive;
                row.peakActive = s.PeakActive;
                row.spawnCount = s.SpawnCount;
                row.despawnCount = s.DespawnCount;
                row.maxInactive = s.MaxInactive;
            }
        }
#endif
    }
}
