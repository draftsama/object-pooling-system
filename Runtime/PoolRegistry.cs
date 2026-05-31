using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
namespace OPS
{
    /// <summary>
    /// Owns all pool state and logic. Plain class (not MonoBehaviour); instantiated
    /// by ObjectPooler in Awake. Eliminates reflection by using Func/Action delegates.
    /// </summary>
    public class PoolRegistry
    {
        private Dictionary<string, object> pools = new Dictionary<string, object>();
        private Dictionary<GameObject, PooledObjectTracker> trackedObjects = new Dictionary<GameObject, PooledObjectTracker>();
        private Dictionary<string, PoolMetadata> poolMetadata = new Dictionary<string, PoolMetadata>();

        // ========== Inner types ==========

        private class PoolMetadata
        {
            public string originalPoolName;
            public Transform parent;
            public int defaultCapacity;
            public int maxSize;
            public PoolStatistics statistics;
            public Func<int> GetCountActive;    // D-06: set at pool creation, no reflection
            public Func<int> GetCountInactive;  // D-06: set at pool creation, no reflection
        }

        private class PooledObjectTracker
        {
            public GameObject GameObject { get; }
            public string PoolKey { get; }
            private readonly Action _releaseAction;
            public bool IsValid => GameObject != null && GameObject;

            public PooledObjectTracker(GameObject go, string poolKey, Action releaseAction)
            {
                GameObject = go;
                PoolKey = poolKey;
                _releaseAction = releaseAction;
            }

            public void Release()
            {
                if (!IsValid)
                {
                    PoolLogger.LogWarning($"Attempting to release invalid object from pool {PoolKey}");
                    return;
                }
                _releaseAction?.Invoke();
            }
        }

        // ========== Pool key helper (D-13) ==========

        private string GetPoolNameFromKey(string poolKey)
        {
            int idx = poolKey.IndexOf("::");
            return idx >= 0 ? poolKey.Substring(idx + 2) : poolKey;
        }

        // ========== Pool creation ==========

        private void CreatePool<T>(string poolKey, T prefab, int defaultCapacity, int maxSize) where T : Component
        {
            Transform poolParent = new GameObject($"Pool_{poolKey}").transform;

            var metadata = new PoolMetadata
            {
                originalPoolName = GetPoolNameFromKey(poolKey),
                parent = poolParent,
                defaultCapacity = defaultCapacity,
                maxSize = maxSize,
                statistics = new PoolStatistics()
            };
            poolMetadata[poolKey] = metadata;

            ObjectPool<T> typedPool = null;
            typedPool = new ObjectPool<T>(
                createFunc: () => UnityEngine.Object.Instantiate(prefab, poolParent),
                actionOnGet: obj =>
                {
                    metadata.statistics.RecordGet(typedPool.CountActive);
                    obj.gameObject.SetActive(true);
                    (obj as IPooledObject)?.OnSpawn();
                },
                actionOnRelease: obj =>
                {
                    (obj as IPooledObject)?.OnDespawn();
                    obj.gameObject.SetActive(false);
                    obj.transform.SetParent(poolParent);
                    obj.transform.localPosition = Vector3.zero;
                    obj.transform.localRotation = Quaternion.identity;
                    obj.transform.localScale = Vector3.one;
                    metadata.statistics.RecordRelease(typedPool.CountActive);
                },
                actionOnDestroy: obj => UnityEngine.Object.Destroy(obj.gameObject),
                collectionCheck: false,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
            );

            pools[poolKey] = typedPool;
            metadata.GetCountActive = () => typedPool.CountActive;
            metadata.GetCountInactive = () => typedPool.CountInactive;
        }

        private void CreatePoolGameObject(string poolKey, GameObject prefab, int defaultCapacity, int maxSize)
        {
            Transform poolParent = new GameObject($"Pool_{poolKey}").transform;

            var metadata = new PoolMetadata
            {
                originalPoolName = GetPoolNameFromKey(poolKey),
                parent = poolParent,
                defaultCapacity = defaultCapacity,
                maxSize = maxSize,
                statistics = new PoolStatistics()
            };
            poolMetadata[poolKey] = metadata;

            ObjectPool<GameObject> typedPool = null;
            typedPool = new ObjectPool<GameObject>(
                createFunc: () => UnityEngine.Object.Instantiate(prefab, poolParent),
                actionOnGet: obj =>
                {
                    metadata.statistics.RecordGet(typedPool.CountActive);
                    obj.SetActive(true);
                    (obj.GetComponent<IPooledObject>())?.OnSpawn();
                },
                actionOnRelease: obj =>
                {
                    (obj.GetComponent<IPooledObject>())?.OnDespawn();
                    obj.SetActive(false);
                    obj.transform.SetParent(poolParent);
                    obj.transform.localPosition = Vector3.zero;
                    obj.transform.localRotation = Quaternion.identity;
                    obj.transform.localScale = Vector3.one;
                    metadata.statistics.RecordRelease(typedPool.CountActive);
                },
                actionOnDestroy: obj => UnityEngine.Object.Destroy(obj),
                collectionCheck: false,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
            );

            pools[poolKey] = typedPool;
            metadata.GetCountActive = () => typedPool.CountActive;
            metadata.GetCountInactive = () => typedPool.CountInactive;
        }

        // ========== Public get/return API ==========

        public T GetFromPool<T>(string poolName, T prefab, int defaultCapacity, int maxSize) where T : Component
        {
            string poolKey = GetPoolKey<T>(poolName);

            if (!pools.ContainsKey(poolKey))
            {
                CreatePool(poolKey, prefab, defaultCapacity, maxSize);
            }

            ObjectPool<T> pool = pools[poolKey] as ObjectPool<T>;
            T obj = pool.Get();

            var releaseAction = (Action)(() => pool.Release(obj));
            trackedObjects[obj.gameObject] = new PooledObjectTracker(obj.gameObject, poolKey, releaseAction);

            return obj;
        }

        public GameObject GetFromPoolGameObject(string poolName, GameObject prefab, int defaultCapacity, int maxSize)
        {
            string poolKey = GetPoolKey<GameObject>(poolName);

            if (!pools.ContainsKey(poolKey))
            {
                CreatePoolGameObject(poolKey, prefab, defaultCapacity, maxSize);
            }

            ObjectPool<GameObject> pool = pools[poolKey] as ObjectPool<GameObject>;
            GameObject obj = pool.Get();

            var releaseAction = (Action)(() => pool.Release(obj));
            trackedObjects[obj] = new PooledObjectTracker(obj, poolKey, releaseAction);

            return obj;
        }

        public void ReturnToPool(GameObject obj)
        {
            if (obj == null) return;

            if (!trackedObjects.TryGetValue(obj, out PooledObjectTracker tracker))
                return;

            trackedObjects.Remove(obj);
            tracker.Release();
        }

        public void DestroyAndRemoveFromPool(GameObject obj)
        {
            if (obj == null) return;
            trackedObjects.Remove(obj);
            UnityEngine.Object.Destroy(obj);
        }

        // ========== Release / Clear API (D-12: no bool-flag overloads) ==========

        /// <summary>Release all active objects from a specific pool back to it.</summary>
        public void ReleaseAllFromPool(string poolName)
        {
            CleanupDestroyedObjects();
            var objectsToRelease = CollectTrackedObjectsForPool(poolName);
            foreach (var obj in objectsToRelease)
            {
                if (obj != null) ReturnToPool(obj);
            }
        }

        /// <summary>Release all active objects from a specific pool, then clear it.</summary>
        public void ReleaseAndClearFromPool(string poolName)
        {
            ReleaseAllFromPool(poolName);
            ClearSpecificPool(poolName);
        }

        /// <summary>Release all active objects from all pools back to them.</summary>
        public void ReleaseAllFromAllPools()
        {
            CleanupDestroyedObjects();
            List<GameObject> objectsToRelease = new List<GameObject>();
            foreach (var kvp in trackedObjects)
            {
                if (kvp.Value.IsValid) objectsToRelease.Add(kvp.Key);
            }
            foreach (var obj in objectsToRelease)
            {
                if (obj != null) ReturnToPool(obj);
            }
        }

        public void ClearSpecificPool(string poolName)
        {
            List<string> keysToRemove = new List<string>();
            foreach (var poolKey in pools.Keys)
            {
                if (GetPoolNameFromKey(poolKey) == poolName)
                    keysToRemove.Add(poolKey);
            }

            foreach (var poolKey in keysToRemove)
            {
                if (pools.TryGetValue(poolKey, out object poolObj))
                {
                    if (poolObj is IDisposable disposable) disposable.Dispose();
                    pools.Remove(poolKey);

                    if (poolMetadata.TryGetValue(poolKey, out var metadata) && metadata.parent != null)
                        UnityEngine.Object.Destroy(metadata.parent.gameObject);
                    poolMetadata.Remove(poolKey);

                    // Remove tracked objects for this pool
                    List<GameObject> keysToRemoveFromTracked = new List<GameObject>();
                    foreach (var kvp in trackedObjects)
                    {
                        if (kvp.Value.PoolKey == poolKey)
                            keysToRemoveFromTracked.Add(kvp.Key);
                    }
                    foreach (var key in keysToRemoveFromTracked)
                        trackedObjects.Remove(key);
                }
            }
        }

        public void ClearAll()
        {
            foreach (var poolObj in pools.Values)
            {
                if (poolObj is IDisposable disposable) disposable.Dispose();
            }
            pools.Clear();
            trackedObjects.Clear();
            poolMetadata.Clear();
        }

        // ========== Statistics & integrity ==========

        public PoolStatistics GetStatisticsInternal(string poolName)
        {
            foreach (var kvp in poolMetadata)
            {
                if (kvp.Value.originalPoolName == poolName)
                    return kvp.Value.statistics;
            }
            return null;
        }

        public Dictionary<string, PoolStatistics> GetAllStatisticsInternal()
        {
            var result = new Dictionary<string, PoolStatistics>();
            foreach (var kvp in poolMetadata)
                result[kvp.Value.originalPoolName] = kvp.Value.statistics;
            return result;
        }

        public int GetActiveObjectCountInternal(string poolName)
        {
            int activeCount = 0;
            foreach (var poolKey in pools.Keys)
            {
                if (GetPoolNameFromKey(poolKey) == poolName)
                {
                    if (poolMetadata.TryGetValue(poolKey, out var metadata))
                        activeCount += metadata.GetCountActive();
                }
            }
            return activeCount;
        }

        public int ValidatePoolIntegrityInternal()
        {
            int issuesFixed = 0;
            List<GameObject> invalidObjects = new List<GameObject>();

            foreach (var kvp in trackedObjects)
            {
                if (!kvp.Value.IsValid)
                {
                    invalidObjects.Add(kvp.Key);
                    issuesFixed++;
                }
            }

            foreach (var obj in invalidObjects)
                trackedObjects.Remove(obj);

            if (issuesFixed > 0)
                PoolLogger.LogWarning($"Pool integrity check found and fixed {issuesFixed} corrupted entries.");

            return issuesFixed;
        }

        public void RecoverCorruptedPoolsInternal()
        {
            PoolLogger.LogInfo("Starting pool recovery...");

            int fixedTracking = ValidatePoolIntegrityInternal();
            int fixedMetadata = 0;

            List<string> orphanedMetadata = new List<string>();
            foreach (var poolKey in poolMetadata.Keys)
            {
                if (!pools.ContainsKey(poolKey))
                {
                    orphanedMetadata.Add(poolKey);
                    fixedMetadata++;
                }
            }

            foreach (var poolKey in orphanedMetadata)
            {
                var metadata = poolMetadata[poolKey];
                if (metadata.parent != null)
                    UnityEngine.Object.Destroy(metadata.parent.gameObject);
                poolMetadata.Remove(poolKey);
            }

            PoolLogger.LogInfo($"Recovery complete. Fixed {fixedTracking} tracking issues and {fixedMetadata} metadata issues.");
        }

        /// <summary>
        /// Populate PoolInfo list for inspector display. Called by ObjectPooler's coroutine (D-06).
        /// </summary>
        public void PopulatePoolInfo(List<PoolInfo> infoList, out int totalPools, out int totalActive, out int totalInactive)
        {
            infoList.Clear();
            totalPools = pools.Count;
            totalActive = 0;
            totalInactive = 0;

            foreach (var kvp in poolMetadata)
            {
                string poolKey = kvp.Key;
                var metadata = kvp.Value;

                if (!pools.ContainsKey(poolKey)) continue;

                int active = metadata.GetCountActive();
                int inactive = metadata.GetCountInactive();

                infoList.Add(new PoolInfo
                {
                    poolName = metadata.originalPoolName,
                    activeCount = active,
                    inactiveCount = inactive,
                    totalCount = active + inactive,
                    capacity = metadata.defaultCapacity,
                    maxSize = metadata.maxSize
                });

                totalActive += active;
                totalInactive += inactive;
            }
        }

        // ========== Private helpers ==========

        private string GetPoolKey<T>(string poolName)
        {
            return $"{typeof(T).Name}::{poolName}";
        }

        private void CleanupDestroyedObjects()
        {
            List<GameObject> destroyedObjects = new List<GameObject>();
            foreach (var kvp in trackedObjects)
            {
                if (!kvp.Value.IsValid)
                    destroyedObjects.Add(kvp.Key);
            }
            foreach (var go in destroyedObjects)
                trackedObjects.Remove(go);

            if (destroyedObjects.Count > 0)
                PoolLogger.LogWarning($"Cleaned up {destroyedObjects.Count} destroyed objects from tracking. Objects should be released back to pool, not destroyed manually.");
        }

        private List<GameObject> CollectTrackedObjectsForPool(string poolName)
        {
            List<string> matchingKeys = new List<string>();
            foreach (var poolKey in pools.Keys)
            {
                if (GetPoolNameFromKey(poolKey) == poolName)
                    matchingKeys.Add(poolKey);
            }

            List<GameObject> result = new List<GameObject>();
            foreach (var kvp in trackedObjects)
            {
                if (matchingKeys.Contains(kvp.Value.PoolKey) && kvp.Value.IsValid)
                    result.Add(kvp.Key);
            }
            return result;
        }
    }
}