using UnityEngine;

namespace MelonS.GameProto
{
    [RequireComponent(typeof(Collider2D))]
    public class EnemyEntity : MonoBehaviour
    {
        [SerializeField] private int maxHp = 5;
        [SerializeField] private int contactDamage = 5;
        [SerializeField] private float hitFlashSeconds = 0.06f;
        [SerializeField] private GameObject xpGemPrefab;
        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;
        private SpriteRenderer sr;
        private Color baseColor = Color.white;
        private float flashUntil = -1f;
        private float lastContact = -10f;

        public void SetXpPrefab(GameObject g) { xpGemPrefab = g; }
        public void SetMaxHp(int h) { maxHp = h; Hp = h; }

        private void Awake()
        {
            Hp = maxHp;
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
        }

        private void Update()
        {
            if (sr != null && flashUntil > 0 && Time.time > flashUntil)
            {
                sr.color = baseColor;
                flashUntil = -1f;
            }
        }

        private void OnCollisionStay2D(Collision2D c)
        {
            if (Time.time - lastContact < 0.5f) return;
            var ph = c.gameObject.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(contactDamage);
                lastContact = Time.time;
            }
        }

        public void TakeDamage(int dmg, GameObject source = null)
        {
            if (IsDead) return;
            Hp = Mathf.Max(0, Hp - dmg);
            if (sr != null) { sr.color = Color.white; flashUntil = Time.time + hitFlashSeconds; }
            if (AudioBank.Instance != null) AudioBank.Instance.PlayHit();
            if (Hp <= 0)
            {
                if (xpGemPrefab != null) Instantiate(xpGemPrefab, transform.position, Quaternion.identity);
                Destroy(gameObject);
            }
        }
    }
}
