using System.Collections.Generic;
using UnityEngine;
namespace OPS
{
    /// <summary>
    /// Abstract CRTP base for pool builders. Provides shared fluent API methods
    /// (WithCapacity, WithMaxSize, WithPrewarm) without duplication.
    /// </summary>
    public abstract class PoolBuilderBase<TSelf> where TSelf : PoolBuilderBase<TSelf>
    {
        protected string poolName;
        protected int capacity = 10;
        protected int maxSize = 100;
        protected int prewarmCount = 0;

        /// <summary>Set the default capacity of the pool (initial size).</summary>
        public TSelf WithCapacity(int capacity) { this.capacity = capacity; return (TSelf)this; }

        /// <summary>Set the maximum size of the pool.</summary>
        public TSelf WithMaxSize(int maxSize) { this.maxSize = maxSize; return (TSelf)this; }

        /// <summary>Prewarm the pool by creating objects in advance.</summary>
        public TSelf WithPrewarm(int count) { this.prewarmCount = count; return (TSelf)this; }

        /// <summary>Pre-populate the pool without returning an object. Renamed from Build() (D-17).</summary>
        public abstract void Prewarm();
    }

    /// <summary>
    /// Fluent API builder for creating Component pools.
    /// </summary>
    public class PoolBuilder<T> : PoolBuilderBase<PoolBuilder<T>> where T : Component
    {
        private T prefab;

        internal PoolBuilder(string poolName, T prefab)
        {
            this.poolName = poolName;
            this.prefab = prefab;
        }

        /// <summary>Pre-populate the pool (does not return an object).</summary>
        public override void Prewarm()
        {
            List<T> prewarmObjects = new List<T>();
            int count = prewarmCount > 0 ? prewarmCount : 1;

            for (int i = 0; i < count; i++)
            {
                prewarmObjects.Add(ObjectPooler.Spawn(poolName, prefab, capacity, maxSize));
            }

            foreach (var obj in prewarmObjects)
            {
                ObjectPooler.Release(obj);
            }
        }

        /// <summary>Prewarm the pool and get an object from it immediately.</summary>
        public T BuildAndGet()
        {
            if (prewarmCount > 0)
            {
                List<T> prewarmObjects = new List<T>();
                for (int i = 0; i < prewarmCount; i++)
                {
                    prewarmObjects.Add(ObjectPooler.Spawn(poolName, prefab, capacity, maxSize));
                }
                foreach (var obj in prewarmObjects)
                {
                    ObjectPooler.Release(obj);
                }
            }

            return ObjectPooler.Spawn(poolName, prefab, capacity, maxSize);
        }
    }

    /// <summary>
    /// Fluent API builder for creating GameObject pools.
    /// </summary>
    public class GameObjectPoolBuilder : PoolBuilderBase<GameObjectPoolBuilder>
    {
        private GameObject prefab;

        internal GameObjectPoolBuilder(string poolName, GameObject prefab)
        {
            this.poolName = poolName;
            this.prefab = prefab;
        }

        /// <summary>Pre-populate the pool (does not return an object).</summary>
        public override void Prewarm()
        {
            List<GameObject> prewarmObjects = new List<GameObject>();
            int count = prewarmCount > 0 ? prewarmCount : 1;

            for (int i = 0; i < count; i++)
            {
                prewarmObjects.Add(ObjectPooler.Spawn(poolName, prefab, capacity, maxSize));
            }

            foreach (var obj in prewarmObjects)
            {
                ObjectPooler.Release(obj);
            }
        }

        /// <summary>Prewarm the pool and get a GameObject from it immediately.</summary>
        public GameObject BuildAndGet()
        {
            if (prewarmCount > 0)
            {
                List<GameObject> prewarmObjects = new List<GameObject>();
                for (int i = 0; i < prewarmCount; i++)
                {
                    prewarmObjects.Add(ObjectPooler.Spawn(poolName, prefab, capacity, maxSize));
                }
                foreach (var obj in prewarmObjects)
                {
                    ObjectPooler.Release(obj);
                }
            }

            return ObjectPooler.Spawn(poolName, prefab, capacity, maxSize);
        }
    }
}
