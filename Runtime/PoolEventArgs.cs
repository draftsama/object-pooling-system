using UnityEngine;

namespace OPS
{
    /// <summary>
    /// Payload for <see cref="ObjectPooler.Spawned"/> and <see cref="ObjectPooler.Despawned"/>.
    /// </summary>
    public readonly struct PoolEventArgs
    {
        public readonly string PoolName;
        public readonly GameObject Instance;

        public PoolEventArgs(string poolName, GameObject instance)
        {
            PoolName = poolName;
            Instance = instance;
        }

        public override string ToString() => $"{PoolName} -> {(Instance != null ? Instance.name : "<destroyed>")}";
    }
}
