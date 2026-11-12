using System;
using System.Collections.Generic;
using UnityEngine;

namespace OPS
{
    internal sealed class PoolRegistry
    {
        private readonly Transform _root;
        private readonly Action<PoolEventArgs> _spawned;
        private readonly Action<PoolEventArgs> _despawned;
        private readonly Dictionary<string, Pool> _pools = new Dictionary<string, Pool>();
        private readonly Dictionary<GameObject, Pool.Instance> _instances = new Dictionary<GameObject, Pool.Instance>();

        public PoolRegistry(Transform root, Action<PoolEventArgs> spawned, Action<PoolEventArgs> despawned)
        {
            _root = root;
            _spawned = spawned;
            _despawned = despawned;
        }

        public GameObject Spawn(string name, GameObject prefab, Transform parent, int initialCapacity, int maxInactive)
            => GetOrCreate(name, prefab, initialCapacity, maxInactive).Spawn(parent).GameObject;

        public T Spawn<T>(string name, T prefab, Transform parent, int initialCapacity, int maxInactive) where T : Component
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));

            Pool pool = GetOrCreate(name, prefab.gameObject, initialCapacity, maxInactive);
            if (pool.Prefab != prefab.gameObject && pool.Prefab.GetComponent<T>() == null)
                throw new InvalidOperationException(
                    $"Pool '{name}' spawns '{pool.Prefab.name}', which has no {typeof(T).Name}.");

            return pool.Spawn(parent).GetComponent<T>();
        }

        public void Prewarm(string name, GameObject prefab, int count, int initialCapacity, int maxInactive)
            => GetOrCreate(name, prefab, initialCapacity, maxInactive).Prewarm(count);

        public void Despawn(GameObject gameObject)
        {
            if (ReferenceEquals(gameObject, null)) return;

            if (!_instances.TryGetValue(gameObject, out Pool.Instance instance))
            {
                if (gameObject != null)
                    PoolLogger.LogWarning($"'{gameObject.name}' was not spawned from a pool; ignoring Despawn.");
                return;
            }

            if (gameObject == null)
            {
                instance.Pool.DestroyInstance(instance);
                return;
            }

            instance.Pool.Despawn(instance);
        }

        public void DespawnAll(string name)
        {
            if (_pools.TryGetValue(name, out Pool pool)) pool.DespawnAll();
        }

        public void DespawnAll()
        {
            foreach (Pool pool in new List<Pool>(_pools.Values)) pool.DespawnAll();
        }

        public void DestroyInstance(GameObject gameObject)
        {
            if (ReferenceEquals(gameObject, null)) return;

            if (_instances.TryGetValue(gameObject, out Pool.Instance instance))
            {
                instance.Pool.DestroyInstance(instance);
                return;
            }

            if (gameObject != null) Pool.DestroyObject(gameObject);
        }

        public void DestroyPool(string name)
        {
            if (!_pools.TryGetValue(name, out Pool pool)) return;
            _pools.Remove(name);
            pool.Destroy();
        }

        public void DestroyAllPools()
        {
            var pools = new List<Pool>(_pools.Values);
            _pools.Clear();
            foreach (Pool pool in pools) pool.Destroy();
        }

        public void Forget()
        {
            _pools.Clear();
            _instances.Clear();
        }

        public int CountActive(string name)
        {
            if (!_pools.TryGetValue(name, out Pool pool)) return 0;
            pool.PruneDestroyed();
            return pool.ActiveCount;
        }

        public void PruneDestroyed()
        {
            foreach (Pool pool in _pools.Values) pool.PruneDestroyed();
        }

        public bool TryGetSnapshot(string name, out PoolSnapshot snapshot)
        {
            if (_pools.TryGetValue(name, out Pool pool))
            {
                pool.PruneDestroyed();
                snapshot = pool.Snapshot();
                return true;
            }
            snapshot = default;
            return false;
        }

        public void GetAllSnapshots(List<PoolSnapshot> results)
        {
            results.Clear();
            foreach (Pool pool in _pools.Values)
            {
                pool.PruneDestroyed();
                results.Add(pool.Snapshot());
            }
        }

        private Pool GetOrCreate(string name, GameObject prefab, int initialCapacity, int maxInactive)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (_pools.TryGetValue(name, out Pool pool))
            {
                if (pool.Prefab != prefab && !pool.PrefabMismatchReported)
                {
                    pool.PrefabMismatchReported = true;
                    PoolLogger.LogError(
                        $"Pool '{name}' was created with prefab '{pool.Prefab.name}'; ignoring '{(prefab != null ? prefab.name : "null")}' and spawning from the existing pool.");
                }
                return pool;
            }

            if (prefab == null) throw new ArgumentNullException(nameof(prefab));

            pool = new Pool(name, prefab, initialCapacity, maxInactive, _root, _instances, _spawned, _despawned);
            _pools.Add(name, pool);
            return pool;
        }
    }
}
