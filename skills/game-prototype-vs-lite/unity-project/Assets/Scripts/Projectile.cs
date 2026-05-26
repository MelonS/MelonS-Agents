using UnityEngine;

namespace MelonS.GameProto
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 12f;
        [SerializeField] private float lifetime = 2f;
        [SerializeField] private int damage = 3;
        private Vector2 dir;
        private float spawnTime;

        public void Launch(Vector2 direction)
        {
            dir = direction;
            spawnTime = Time.time;
        }

        private void Update()
        {
            if (Time.time - spawnTime > lifetime) { Destroy(gameObject); return; }
            transform.position += (Vector3)(dir * speed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var enemy = other.GetComponent<EnemyEntity>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, gameObject);
                Destroy(gameObject);
            }
        }
    }
}
