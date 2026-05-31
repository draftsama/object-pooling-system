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
    /// Singleton facade for the ObjectPooling system. Delegates all state and logic to PoolRegistry.
    /// </summary>
    public class ObjectPooler : MonoBehaviour
    {
        private static ObjectPooler instance;
        private PoolRegistry _registry;

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
