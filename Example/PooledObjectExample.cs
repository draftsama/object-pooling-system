using UnityEngine;

namespace OPS.Demo
{
    /// <summary>
    /// Example of a pooled object that implements IPooledObject interface.
    /// This demonstrates how to properly set up and clean up pooled objects.
    /// </summary>
    public class PooledObjectExample : MonoBehaviour, IPooledObject
    {
        [SerializeField] private float lifetime = 2f;
        [SerializeField] private float moveSpeed = 5f;

        private Vector3 direction;
        private float spawnTime;

        public void OnSpawn()
        {
            // Called when the object is retrieved from the pool
            spawnTime = Time.time;

            // Set random direction
            direction = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                0f
            ).normalized;

#if UNITY_EDITOR
            Debug.Log($"{gameObject.name} spawned at {transform.position}");
#endif
        }

        public void OnDespawn()
        {
            // Called when the object is returned to the pool
            // Clean up any resources, stop coroutines, etc.
#if UNITY_EDITOR
            Debug.Log($"{gameObject.name} despawned");
#endif
        }

        private void Update()
        {
            // Move in random direction
            transform.position += direction * moveSpeed * Time.deltaTime;

            // Auto-release after lifetime expires
            if (Time.time - spawnTime >= lifetime)
            {
                ObjectPooler.Release(this);
            }
        }
    }
}