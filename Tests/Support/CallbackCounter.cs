using System;
using UnityEngine;

namespace OPS.Tests
{
    public sealed class CallbackCounter : MonoBehaviour, IPooledObject
    {
        public static Action<CallbackCounter> SpawnHook;
        public static Action<CallbackCounter> DespawnHook;

        public int Spawns;
        public int Despawns;

        public void OnSpawn()
        {
            Spawns++;
            SpawnHook?.Invoke(this);
        }

        public void OnDespawn()
        {
            Despawns++;
            DespawnHook?.Invoke(this);
        }
    }
}
