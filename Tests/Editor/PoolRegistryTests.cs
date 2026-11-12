using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OPS.Tests
{
    public class PoolRegistryTests
    {
        private const string PoolName = "p";

        private GameObject _root;
        private GameObject _prefab;
        private PoolRegistry _registry;
        private List<PoolEventArgs> _spawned;
        private List<PoolEventArgs> _despawned;
        private PoolLogLevel _savedLogLevel;

        [SetUp]
        public void SetUp()
        {
            _savedLogLevel = PoolLogger.LogLevel;
            PoolLogger.LogLevel = PoolLogLevel.Warnings;

            _root = new GameObject("OPS.Tests.Root");
            _prefab = NewPrefab("Prefab");
            _spawned = new List<PoolEventArgs>();
            _despawned = new List<PoolEventArgs>();
            _registry = new PoolRegistry(_root.transform, _spawned.Add, _despawned.Add);
        }

        [TearDown]
        public void TearDown()
        {
            CallbackCounter.SpawnHook = null;
            CallbackCounter.DespawnHook = null;
            _registry.DestroyAllPools();
            Object.DestroyImmediate(_root);
            PoolLogger.LogLevel = _savedLogLevel;
        }

        private GameObject NewPrefab(string name)
        {
            var prefab = new GameObject(name);
            prefab.transform.SetParent(_root.transform, false);
            prefab.AddComponent<CallbackCounter>();
            return prefab;
        }

        private GameObject Spawn(int maxInactive = 100)
            => _registry.Spawn(PoolName, _prefab, _root.transform, 10, maxInactive);

        private PoolSnapshot Snapshot()
        {
            Assert.IsTrue(_registry.TryGetSnapshot(PoolName, out PoolSnapshot snapshot));
            return snapshot;
        }

        [Test]
        public void Spawn_RunsEveryOnSpawnCallbackAndRaisesSpawned()
        {
            _prefab.AddComponent<CallbackCounter>();

            GameObject instance = Spawn();

            Assert.IsTrue(instance.activeSelf);
            foreach (CallbackCounter counter in instance.GetComponents<CallbackCounter>())
                Assert.AreEqual(1, counter.Spawns);
            Assert.AreEqual(1, _spawned.Count);
            Assert.AreEqual(PoolName, _spawned[0].PoolName);
            Assert.AreSame(instance, _spawned[0].Instance);
        }

        [Test]
        public void Despawn_RunsOnDespawnRaisesDespawnedAndDeactivates()
        {
            GameObject instance = Spawn();

            _registry.Despawn(instance);

            Assert.IsFalse(instance.activeSelf);
            Assert.AreEqual(1, instance.GetComponent<CallbackCounter>().Despawns);
            Assert.AreEqual(1, _despawned.Count);
            Assert.AreEqual(0, _registry.CountActive(PoolName));
        }

        [Test]
        public void Spawn_AfterDespawn_ReusesTheInstance()
        {
            GameObject first = Spawn();
            _registry.Despawn(first);

            GameObject second = Spawn();

            Assert.AreSame(first, second);
            Assert.AreEqual(2, Snapshot().SpawnCount);
        }

        [Test]
        public void Spawn_RestoresPrefabLocalTransform()
        {
            _prefab.transform.localScale = new Vector3(2f, 2f, 2f);
            GameObject instance = Spawn();
            instance.transform.localScale = new Vector3(5f, 5f, 5f);
            instance.transform.localPosition = new Vector3(3f, 0f, 0f);
            _registry.Despawn(instance);

            instance = Spawn();

            Assert.AreEqual(new Vector3(2f, 2f, 2f), instance.transform.localScale);
            Assert.AreEqual(Vector3.zero, instance.transform.localPosition);
        }

        [Test]
        public void Prewarm_CreatesInactiveInstancesWithoutCallbacksOrEvents()
        {
            _registry.Prewarm(PoolName, _prefab, 3, 10, 100);

            Assert.AreEqual(3, Snapshot().Inactive);
            Assert.AreEqual(0, _spawned.Count);

            GameObject instance = Spawn();
            Assert.AreEqual(1, instance.GetComponent<CallbackCounter>().Spawns);
            Assert.AreEqual(2, Snapshot().Inactive);
        }

        [Test]
        public void Prewarm_BeyondMaxInactive_IsCappedWithWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("capped at its max inactive count 2"));

            _registry.Prewarm(PoolName, _prefab, 5, 10, 2);

            Assert.AreEqual(2, Snapshot().Inactive);
        }

        [Test]
        public void Spawn_SkipsInactiveInstanceDestroyedOutsideThePool()
        {
            GameObject first = Spawn();
            _registry.Despawn(first);
            Object.DestroyImmediate(first);

            GameObject second = Spawn();

            Assert.IsTrue(second != null);
            Assert.AreEqual(1, Snapshot().Active);
        }

        [Test]
        public void Despawn_WhenMaxInactiveReached_DestroysTheInstance()
        {
            GameObject a = Spawn(maxInactive: 1);
            GameObject b = Spawn(maxInactive: 1);

            _registry.Despawn(a);
            _registry.Despawn(b);

            Assert.IsTrue(a != null);
            Assert.IsTrue(b == null);
            Assert.AreEqual(1, Snapshot().Inactive);
        }

        [Test]
        public void Spawn_WithDifferentPrefabUnderSameName_LogsErrorOnceAndUsesExistingPool()
        {
            GameObject other = NewPrefab("Other");
            Spawn();
            LogAssert.Expect(LogType.Error, new Regex("was created with prefab 'Prefab'"));

            GameObject a = _registry.Spawn(PoolName, other, _root.transform, 10, 100);
            GameObject b = _registry.Spawn(PoolName, other, _root.transform, 10, 100);

            StringAssert.StartsWith("Prefab", a.name);
            StringAssert.StartsWith("Prefab", b.name);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SpawnGeneric_WhenPoolPrefabLacksTheComponent_ThrowsWithoutSpawning()
        {
            Spawn();
            GameObject other = NewPrefab("Other");
            BoxCollider collider = other.AddComponent<BoxCollider>();
            LogAssert.Expect(LogType.Error, new Regex("was created with prefab"));

            Assert.Throws<InvalidOperationException>(
                () => _registry.Spawn(PoolName, collider, _root.transform, 10, 100));
            Assert.AreEqual(1, Snapshot().Active);
        }

        [Test]
        public void SpawnGeneric_SharesThePoolWithGameObjectSpawn()
        {
            CallbackCounter counter = _registry.Spawn(PoolName, _prefab.GetComponent<CallbackCounter>(), _root.transform, 10, 100);
            Spawn();

            var snapshots = new List<PoolSnapshot>();
            _registry.GetAllSnapshots(snapshots);

            Assert.IsNotNull(counter);
            Assert.AreEqual(1, snapshots.Count);
            Assert.AreEqual(2, snapshots[0].Active);
        }

        [Test]
        public void Despawn_Twice_LogsWarning()
        {
            GameObject instance = Spawn();
            _registry.Despawn(instance);
            LogAssert.Expect(LogType.Warning, new Regex("already despawned"));

            _registry.Despawn(instance);

            Assert.AreEqual(1, _despawned.Count);
        }

        [Test]
        public void Despawn_ObjectNotFromAPool_LogsWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex("was not spawned from a pool"));

            _registry.Despawn(_prefab);

            Assert.IsTrue(_prefab.activeSelf);
        }

        [Test]
        public void Despawn_CalledFromOnSpawn_LeavesInstanceInactiveWithoutSpawnedEvent()
        {
            CallbackCounter.SpawnHook = counter => _registry.Despawn(counter.gameObject);

            GameObject instance = Spawn();

            Assert.IsFalse(instance.activeSelf);
            Assert.AreEqual(0, _spawned.Count);
            Assert.AreEqual(0, Snapshot().Active);
            Assert.AreEqual(1, Snapshot().Inactive);
        }

        [Test]
        public void DespawnAll_WhenOnDespawnSpawnsIntoSamePool_Terminates()
        {
            Spawn();
            Spawn();
            CallbackCounter.DespawnHook = _ => Spawn();

            _registry.DespawnAll(PoolName);

            Assert.AreEqual(2, Snapshot().Active);
        }

        [Test]
        public void DestroyPool_DespawnsActiveInstancesThenRemovesThePool()
        {
            GameObject a = Spawn();
            GameObject b = Spawn();

            _registry.DestroyPool(PoolName);

            Assert.AreEqual(2, _despawned.Count);
            Assert.IsFalse(_registry.TryGetSnapshot(PoolName, out _));
            Assert.IsTrue(a == null);
            Assert.IsTrue(b == null);
        }

        [Test]
        public void DestroyPool_WhenDespawnedHandlerSpawnsSameName_LeavesAFreshPool()
        {
            Spawn();
            CallbackCounter.DespawnHook = _ =>
            {
                CallbackCounter.DespawnHook = null;
                Spawn();
            };

            _registry.DestroyPool(PoolName);

            PoolSnapshot snapshot = Snapshot();
            Assert.AreEqual(1, snapshot.Active);
            Assert.AreEqual(1, snapshot.SpawnCount);
        }

        [Test]
        public void DestroyInstance_OnActiveInstance_RunsOnDespawnAndRemovesIt()
        {
            GameObject instance = Spawn();
            bool despawnRan = false;
            CallbackCounter.DespawnHook = _ => despawnRan = true;

            _registry.DestroyInstance(instance);

            Assert.IsTrue(despawnRan);
            Assert.AreEqual(1, _despawned.Count);
            Assert.IsTrue(instance == null);
            Assert.AreEqual(0, _registry.CountActive(PoolName));
        }

        [Test]
        public void CountActive_IgnoresActiveInstanceDestroyedOutsideThePool()
        {
            GameObject instance = Spawn();
            Object.DestroyImmediate(instance);

            Assert.AreEqual(0, _registry.CountActive(PoolName));
        }
    }
}
