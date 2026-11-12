using UnityEngine;

namespace OPS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public class DespawnWhenParticlesFinish : MonoBehaviour, IPooledObject
    {
        private ParticleSystem _particleSystem;
        private bool _isPlaying;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        public void OnSpawn()
        {
            if (_particleSystem == null) _particleSystem = GetComponent<ParticleSystem>();

            _particleSystem.Clear(true);
            _particleSystem.Play(true);
            _isPlaying = true;
        }

        public void OnDespawn()
        {
            _isPlaying = false;
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Update()
        {
            if (!_isPlaying) return;
            if (_particleSystem.IsAlive(true)) return;

            _isPlaying = false;
            ObjectPooler.Despawn(gameObject);
        }
    }
}
