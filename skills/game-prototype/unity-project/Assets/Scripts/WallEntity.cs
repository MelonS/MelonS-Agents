using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>#150 - 자재별 벽 HP (wiki: wood 100, stone 280, steel 300).</summary>
    public enum WallMaterial { Wood, Stone, Steel }

    /// <summary>Day 17: a built wall.  #150 - 자재별 HP + tint.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class WallEntity : MonoBehaviour
    {
        [SerializeField] private WallMaterial material = WallMaterial.Wood;
        [SerializeField] private float maxHp = 100f;
        private float hp;

        public WallMaterial Material => material;
        public float Hp => hp;
        public float MaxHp => maxHp;
        public string MaterialKr => material switch
        {
            WallMaterial.Wood => "목재 벽",
            WallMaterial.Stone => "석재 벽",
            WallMaterial.Steel => "철강 벽",
            _ => "벽",
        };

        // wiki spec - wood 100 / stone 280 / steel 300
        public static readonly (float hp, Color tint)[] MaterialStats = {
            (100f, new Color(1.00f, 1.00f, 1.00f, 1f)),
            (280f, new Color(0.78f, 0.78f, 0.80f, 1f)),
            (300f, new Color(0.55f, 0.60f, 0.70f, 1f)),
        };

        public bool ProvidesCover => true;

        public void SetMaterial(WallMaterial m)
        {
            material = m;
            var (h, tint) = MaterialStats[(int)m];
            maxHp = h;
            hp = h;
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = tint;
        }

        private void Awake()
        {
            if (hp <= 0f) hp = maxHp;
        }

        public void TakeDamage(float dmg)
        {
            hp -= dmg;
            // #158 - 시각 피드백: HP 비율 × material tint (#156 lesson - hue 보존).
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && maxHp > 0f)
            {
                float t = Mathf.Clamp01(hp / maxHp);
                var (_, baseTint) = MaterialStats[(int)material];
                // 0.4 minimum brightness 유지 (완전히 검정 되면 안 보임)
                float b = 0.4f + 0.6f * t;
                sr.color = new Color(baseTint.r * b, baseTint.g * b, baseTint.b * b, baseTint.a);
            }
            if (hp <= 0f) Destroy(gameObject);
        }
    }
}
