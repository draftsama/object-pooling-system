using UnityEngine;

namespace OPS
{
    [DisallowMultipleComponent]
    public class DespawnAfterDelay : MonoBehaviour, IPooledObject
    {
        [SerializeField, Min(0f)] private float _delay = 1f;
        [SerializeField] private bool _useUnscaledTime;

        private float _remaining;
        private bool _isCounting;

        public float Delay
        {
            get => _delay;
            set => _delay = Mathf.Max(0f, value);
        }

        public void OnSpawn()
        {
            _remaining = _delay;
            _isCounting = true;
        }

        public void OnDespawn()
        {
            _isCounting = false;
        }

        private void Update()
        {
            if (!_isCounting) return;

            _remaining -= _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_remaining > 0f) return;

            _isCounting = false;
            ObjectPooler.Despawn(gameObject);
        }
    }
}
