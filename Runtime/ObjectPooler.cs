using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OPS
{
    [System.Serializable]
    public class PoolInfo
    {
        public string poolName;
        public int activeCount;
        public int inactiveCount;
        public int totalCount;
        public int capacity;
        public int maxSize;
    }

    /// <summary>
    /// Payload for <see cref="ObjectPooler.OnSpawn"/> and <see cref="ObjectPooler.OnDespawn"/>.
    /// </summary>
    public readonly struct PoolEventArgs
    {
        /// <summary>Name passed to Spawn/CreatePool, without the type prefix.</summary>
        public readonly string PoolName;

        /// <summary>Type-qualified internal key, e.g. "Bullet::enemy_bullets". Unique per pool.</summary>
        public readonly string PoolKey;

        /// <summary>The spawned/despawned instance.</summary>
        public readonly GameObject GameObject;

        public PoolEventArgs(string poolName, string poolKey, GameObject gameObject)
        {
            PoolName = poolName;
            PoolKey = poolKey;
            GameObject = gameObject;
        }

        public override string ToString() => $"{PoolKey} -> {(GameObject != null ? GameObject.name : "<destroyed>")}";
    }

    /// <summary>
    /// Singleton facade for the ObjectPooling system. Delegates all state and logic to PoolRegistry.
    /// </summary>
    public class ObjectPooler : MonoBehaviour
    {
        private static ObjectPooler instance;
        private PoolRegistry _registry;

        // ========== Global spawn/despawn events ==========

        /// <summary>
        /// Raised after an object is taken from a pool and its <see cref="IPooledObject.OnSpawn"/> ran.
        /// Static: subscribers MUST unsubscribe in OnDisable/OnDestroy or they leak across scenes.
        /// </summary>
        public static event Action<PoolEventArgs> OnSpawn;

        /// <summary>
        /// Raised when an object is returned to its pool, after <see cref="IPooledObject.OnDespawn"/>
        /// and before the object is deactivated and its transform reset — so subscribers can still
        /// read its final position/parent.
        /// Not raised for objects destroyed via DestroyPooledObject, ClearPool or ClearAllPools.
        /// </summary>
        public static event Action<PoolEventArgs> OnDespawn;

        internal static void RaiseSpawn(PoolEventArgs args)
        {
            var handler = OnSpawn;
            if (handler == null) return;
            try { handler(args); }
            catch (Exception e) { PoolLogger.LogError($"ObjectPooler.OnSpawn subscriber threw for {args}: {e}"); }
        }

        internal static void RaiseDespawn(PoolEventArgs args)
        {
            var handler = OnDespawn;
            if (handler == null) return;
            try { handler(args); }
            catch (Exception e) { PoolLogger.LogError($"ObjectPooler.OnDespawn subscriber threw for {args}: {e}"); }
        }

        /// <summary>
        /// Clears static state so stale subscribers from a previous play session cannot survive
        /// when Domain Reload is disabled (Enter Play Mode Options).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnSpawn = null;
            OnDespawn = null;
            instance = null;
        }

        [Header("Pool Information")]
        [SerializeField] private List<PoolInfo> poolInfoList = new List<PoolInfo>();

        [Header("Statistics")]
        [SerializeField] private int totalPools;
        [SerializeField] private int totalActiveObjects;
        [SerializeField] private int totalInactiveObjects;

        private readonly WaitForSeconds _integrityWait = new WaitForSeconds(10f);

#if UNITY_EDITOR
        private readonly WaitForSeconds _updateWait = new WaitForSeconds(0.5f);

        private IEnumerator UpdatePoolInfoRoutine()
        {
            while (true)
            {
                _registry.PopulatePoolInfo(poolInfoList, out totalPools, out totalActiveObjects, out totalInactiveObjects);
                yield return _updateWait;
            }
        }
#endif

        private IEnumerator IntegrityCheckRoutine()
        {
            yield return _integrityWait;
            while (true)
            {
                _registry.ValidatePoolIntegrityInternal();
                yield return _integrityWait;
            }
        }

        public static ObjectPooler GetInstance()
        {
            if (instance == null)
            {
                instance = new GameObject("ObjectPooler").AddComponent<ObjectPooler>();
            }
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            _registry = new PoolRegistry();
            DontDestroyOnLoad(gameObject);
#if UNITY_EDITOR
            StartCoroutine(UpdatePoolInfoRoutine());
#endif
            StartCoroutine(IntegrityCheckRoutine());
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                StopAllCoroutines();
                _registry?.ClearAll();
                instance = null;
            }
        }

        // ========== Builder factory methods (D-03) ==========

        public static PoolBuilder<T> CreatePool<T>(string poolName, T prefab) where T : Component
            => new PoolBuilder<T>(poolName, prefab);

        public static GameObjectPoolBuilder CreatePool(string poolName, GameObject prefab)
            => new GameObjectPoolBuilder(poolName, prefab);

        // ========== Spawn / Release API (D-01, D-02) ==========

        public static T Spawn<T>(string poolName, T prefab, int defaultCapacity = 10, int maxSize = 100) where T : Component
            => GetInstance()._registry.GetFromPool(poolName, prefab, defaultCapacity, maxSize);

        public static GameObject Spawn(string poolName, GameObject prefab, int defaultCapacity = 10, int maxSize = 100)
            => GetInstance()._registry.GetFromPoolGameObject(poolName, prefab, defaultCapacity, maxSize);

        public static void Release<T>(T obj) where T : Component
            => GetInstance()._registry.ReturnToPool(obj.gameObject);

        public static void Release(GameObject obj)
            => GetInstance()._registry.ReturnToPool(obj);

        // ========== Destroy ==========

        public static void DestroyPooledObject<T>(T obj) where T : Component
            => GetInstance()._registry.DestroyAndRemoveFromPool(obj.gameObject);

        public static void DestroyPooledObject(GameObject obj)
            => GetInstance()._registry.DestroyAndRemoveFromPool(obj);

        // ========== Release / Clear API (D-12: no bool-flag overloads) ==========

        public static void ReleaseAllPool(string poolName)
            => GetInstance()._registry.ReleaseAllFromPool(poolName);

        public static void ReleaseAndClearPool(string poolName)
            => GetInstance()._registry.ReleaseAndClearFromPool(poolName);

        public static void ReleaseAllPools()
            => GetInstance()._registry.ReleaseAllFromAllPools();

        public static void ClearPool(string poolName)
            => GetInstance()._registry.ClearSpecificPool(poolName);

        public static void ClearAllPools()
            => GetInstance()._registry.ClearAll();

        // ========== Statistics & integrity ==========

        public static PoolStatistics GetStatistics(string poolName)
            => GetInstance()._registry.GetStatisticsInternal(poolName);

        public static Dictionary<string, PoolStatistics> GetAllStatistics()
            => GetInstance()._registry.GetAllStatisticsInternal();

        public static int GetActiveObjectCount(string poolName)
            => GetInstance()._registry.GetActiveObjectCountInternal(poolName);

        public static int ValidatePoolIntegrity()
            => GetInstance()._registry.ValidatePoolIntegrityInternal();

        public static void RecoverCorruptedPools()
            => GetInstance()._registry.RecoverCorruptedPoolsInternal();
    }
}
