using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 50 — Arrow projectile.  Travels in a straight line, damages
    /// the first WolfEnemy/BanditEnemy/AnimalEntity it overlaps, then despawns.
    /// Auto-despawn after lifetime expires (3 sec).
    /// </summary>
    public class ArrowProjectile : MonoBehaviour
    {
        public Vector2 direction;
        public float speed = 8f;
        public int damage = 4;
        public float lifetime = 3f;
        public GameObject shooter;

        private float spawnTime;

        public static GameObject SpawnArrow(Vector3 origin, Vector2 dir, int damage, GameObject shooter, Sprite arrowSprite)
        {
            GameObject go = new GameObject("Arrow");
            go.transform.position = origin;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0, 0, ang);
            // 12x4 arrow at PPU 16 = 0.75 x 0.25 world unit.  Scale up slightly.
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = arrowSprite;
            sr.sortingOrder = 22;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.15f;
            col.isTrigger = true;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            var ap = go.AddComponent<ArrowProjectile>();
            ap.direction = dir.normalized;
            ap.damage = damage;
            ap.shooter = shooter;
            return go;
        }

        private void Start()
        {
            spawnTime = Time.time;
        }

        private void Update()
        {
            if (Time.time - spawnTime > lifetime) { Destroy(gameObject); return; }
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || other.gameObject == shooter) return;
            // Don't hit other pawns
            if (other.GetComponent<PawnEntity>() != null) return;
            WolfEnemy wolf = other.GetComponent<WolfEnemy>();
            BanditEnemy bandit = other.GetComponent<BanditEnemy>();
            AnimalEntity animal = other.GetComponent<AnimalEntity>();
            bool hit = false;
            if (wolf != null) { wolf.TakeDamage(damage, shooter); hit = true; }
            else if (bandit != null) { bandit.TakeDamage(damage, shooter); hit = true; }
            else if (animal != null) { animal.TakeDamage(damage); hit = true; }
            if (hit) Destroy(gameObject);
        }
    }
}
