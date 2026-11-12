using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OPS
{
    internal sealed class Pool
    {
        internal sealed class Instance
        {
            public readonly Pool Pool;
            public readonly GameObject GameObject;
            public readonly Transform Transform;
            public readonly IPooledObject[] Callbacks;
            public int ActiveSlot = -1;
            private Component _typed;

            public Instance(Pool pool, GameObject gameObject)
            {
                Pool = pool;
                GameObject = gameObject;
                Transform = gameObject.transform;
                Callbacks = gameObject.GetComponents<IPooledObject>();
            }

            public bool IsActive => ActiveSlot >= 0;

            public T GetComponent<T>() where T : Component
            {
                if (_typed is T cached) return cached;
                T found = GameObject.GetComponent<T>();
                if (found != null) _typed = found;
                return found;
            }
        }

        public readonly string Name;
        public readonly GameObject Prefab;
        public readonly int InitialCapacity;
        public readonly int MaxInactive;
        public bool PrefabMismatchReported;

        private readonly Transform _container;
        private readonly Vector3 _restPosition;
        private readonly Quaternion _restRotation;
        private readonly Vector3 _restScale;
        private readonly Dictionary<GameObject, Instance> _index;
        private readonly Action<PoolEventArgs> _spawned;
        private readonly Action<PoolEventArgs> _despawned;
        private readonly List<Instance> _inactive;
        private readonly List<Instance> _active = new List<Instance>();

        private int _spawnCount;
        private int _despawnCount;
        private int _peakActive;

        public Pool(string name, GameObject prefab, int initialCapacity, int maxInactive, Transform root,
            Dictionary<GameObject, Instance> index, Action<PoolEventArgs> spawned, Action<PoolEventArgs> despawned)
        {
            Name = name;
            Prefab = prefab;
            InitialCapacity = Mathf.Max(0, initialCapacity);
            MaxInactive = Mathf.Max(0, maxInactive);
            _index = index;
            _spawned = spawned;
            _despawned = despawned;
            _inactive = new List<Instance>(InitialCapacity);

            Transform prefabTransform = prefab.transform;
            _restPosition = prefabTransform.localPosition;
            _restRotation = prefabTransform.localRotation;
            _restScale = prefabTransform.localScale;

            var container = new GameObject($"Pool_{name}");
            container.SetActive(false);
            _container = container.transform;
            _container.SetParent(root, false);
        }

        public int ActiveCount => _active.Count;
        public int InactiveCount => _inactive.Count;

        public PoolSnapshot Snapshot()
            => new PoolSnapshot(Name, _active.Count, _inactive.Count, _spawnCount, _despawnCount, _peakActive,
                InitialCapacity, MaxInactive);

        public Instance Spawn(Transform parent)
        {
            Instance instance = PopInactive() ?? Create();
            GameObject gameObject = instance.GameObject;

            instance.ActiveSlot = _active.Count;
            _active.Add(instance);
            _spawnCount++;
            if (_active.Count > _peakActive) _peakActive = _active.Count;

            Transform transform = instance.Transform;
            transform.SetParent(parent, false);
            if (parent == null)
            {
                Scene activeScene = SceneManager.GetActiveScene();
                if (gameObject.scene != activeScene) SceneManager.MoveGameObjectToScene(gameObject, activeScene);
            }
            transform.SetLocalPositionAndRotation(_restPosition, _restRotation);
            transform.localScale = _restScale;

            gameObject.SetActive(true);

            IPooledObject[] callbacks = instance.Callbacks;
            for (int i = 0; i < callbacks.Length && instance.IsActive; i++)
            {
                try { callbacks[i].OnSpawn(); }
                catch (Exception e) { Debug.LogException(e, gameObject); }
            }

            if (instance.IsActive) _spawned?.Invoke(new PoolEventArgs(Name, gameObject));
            return instance;
        }

        public void Despawn(Instance instance)
        {
            if (!instance.IsActive)
            {
                PoolLogger.LogWarning($"'{instance.GameObject.name}' is already despawned in pool '{Name}'.");
                return;
            }

            LeaveActive(instance);

            GameObject gameObject = instance.GameObject;
            if (gameObject == null)
            {
                _index.Remove(gameObject);
                return;
            }

            if (_inactive.Count >= MaxInactive)
            {
                _index.Remove(gameObject);
                DestroyObject(gameObject);
                return;
            }

            gameObject.SetActive(false);
            instance.Transform.SetParent(_container, false);
            _inactive.Add(instance);
        }

        public void DespawnAll()
        {
            PruneDestroyed();
            for (int budget = _active.Count; budget > 0 && _active.Count > 0; budget--)
            {
                Despawn(_active[_active.Count - 1]);
            }
        }

        public void Prewarm(int count)
        {
            if (count > MaxInactive)
                PoolLogger.LogWarning($"Prewarm({count}) for pool '{Name}' is capped at its max inactive count {MaxInactive}.");

            int target = Mathf.Min(count, MaxInactive);
            while (_inactive.Count < target) _inactive.Add(Create());
        }

        public void DestroyInstance(Instance instance)
        {
            if (instance.IsActive) LeaveActive(instance);
            else _inactive.Remove(instance);

            _index.Remove(instance.GameObject);
            if (instance.GameObject != null) DestroyObject(instance.GameObject);
        }

        public void Destroy()
        {
            DespawnAll();

            for (int i = _active.Count - 1; i >= 0; i--) DestroyInstance(_active[i]);
            for (int i = 0; i < _inactive.Count; i++) _index.Remove(_inactive[i].GameObject);
            _inactive.Clear();

            if (_container != null) DestroyObject(_container.gameObject);
        }

        public void PruneDestroyed()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Instance instance = _active[i];
                if (instance.GameObject != null) continue;
                RemoveActiveSlot(instance);
                _index.Remove(instance.GameObject);
            }

            for (int i = _inactive.Count - 1; i >= 0; i--)
            {
                Instance instance = _inactive[i];
                if (instance.GameObject != null) continue;
                _inactive.RemoveAt(i);
                _index.Remove(instance.GameObject);
            }
        }

        internal static void DestroyObject(UnityEngine.Object target)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }

        private void LeaveActive(Instance instance)
        {
            RemoveActiveSlot(instance);
            _despawnCount++;

            GameObject gameObject = instance.GameObject;
            if (gameObject == null) return;

            IPooledObject[] callbacks = instance.Callbacks;
            for (int i = 0; i < callbacks.Length; i++)
            {
                try { callbacks[i].OnDespawn(); }
                catch (Exception e) { Debug.LogException(e, gameObject); }
            }
            _despawned?.Invoke(new PoolEventArgs(Name, gameObject));
        }

        private Instance Create()
        {
            GameObject gameObject = UnityEngine.Object.Instantiate(Prefab, _container);
            gameObject.SetActive(false);
            var instance = new Instance(this, gameObject);
            _index[gameObject] = instance;
            return instance;
        }

        private Instance PopInactive()
        {
            while (_inactive.Count > 0)
            {
                int last = _inactive.Count - 1;
                Instance instance = _inactive[last];
                _inactive.RemoveAt(last);
                if (instance.GameObject != null) return instance;
                _index.Remove(instance.GameObject);
            }
            return null;
        }

        private void RemoveActiveSlot(Instance instance)
        {
            int slot = instance.ActiveSlot;
            int last = _active.Count - 1;
            Instance moved = _active[last];
            _active[slot] = moved;
            moved.ActiveSlot = slot;
            _active.RemoveAt(last);
            instance.ActiveSlot = -1;
        }
    }
}
