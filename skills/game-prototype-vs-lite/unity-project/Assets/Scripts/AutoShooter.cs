using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Auto-aim nearest enemy, fire on interval.</summary>
    public class AutoShooter : MonoBehaviour
    {
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float fireInterval = 0.45f;
        [SerializeField] private float aimRange = 8f;
        private float lastFire = -10f;

        public void SetProjectilePrefab(GameObject prefab) { projectilePrefab = prefab; }

        private void Update()
        {
            if (Time.time - lastFire < fireInterval) return;
            if (projectilePrefab == null) return;
            EnemyEntity target = FindNearest();
            if (target == null) return;
            Vector2 dir = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            var p = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
            var proj = p.GetComponent<Projectile>();
            if (proj != null) proj.Launch(dir);
            if (AudioBank.Instance != null) AudioBank.Instance.PlayShoot();
            lastFire = Time.time;
        }

        private EnemyEntity FindNearest()
        {
            var arr = Object.FindObjectsByType<EnemyEntity>(FindObjectsSortMode.None);
            EnemyEntity best = null;
            float bd = aimRange * aimRange;
            Vector2 me = transform.position;
            foreach (var e in arr)
            {
                if (e == null || e.IsDead) continue;
                float d = ((Vector2)e.transform.position - me).sqrMagnitude;
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }
    }
}
