using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #119 - 림월드 광맥 (stone vein).
    ///  채광 시 stone chunk drop (1-3 개).  HP 200 (나무 100 보다 더 단단).
    ///  PawnMiner 가 채광.  HaulStoneAction 으로 chunk 운반.
    /// </summary>
    public enum StoneType { Sandstone, Limestone, Granite, Marble }

    [RequireComponent(typeof(SpriteRenderer))]
    public class StoneVeinEntity : MonoBehaviour
    {
        [SerializeField] private float maxHp = 200f;
        [SerializeField] private int stoneYieldMin = 1;
        [SerializeField] private int stoneYieldMax = 3;
        [SerializeField] private StoneType type = StoneType.Sandstone;

        public StoneType Type => type;
        public string TypeKr => type switch {
            StoneType.Sandstone => "사암",
            StoneType.Limestone => "석회암",
            StoneType.Granite   => "화강암",
            StoneType.Marble    => "대리석",
            _ => "돌",
        };

        // 종류별 색 (sprite tint)
        public static readonly Color[] TypeColors = {
            new Color(0.95f, 0.85f, 0.65f, 1f),   // Sandstone 황갈
            new Color(0.85f, 0.85f, 0.78f, 1f),   // Limestone 회백
            new Color(0.55f, 0.55f, 0.60f, 1f),   // Granite 진회
            new Color(0.95f, 0.95f, 0.98f, 1f),   // Marble 흰
        };
        public static readonly float[] TypeHpMul = { 0.7f, 0.9f, 1.4f, 1.0f };

        private float hp;
        private SpriteRenderer sr;

        // chunk drop sprite (GameManager 에서 박음)
        public static Sprite StoneChunkSprite;

        public bool IsDestroyed => hp <= 0f;

        public void SetType(StoneType t)
        {
            type = t;
            if (sr != null) sr.color = TypeColors[(int)t];
            maxHp = 200f * TypeHpMul[(int)t];
            hp = maxHp;
        }

        private void Awake()
        {
            hp = maxHp;
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = TypeColors[(int)type];
        }

        private float lastSfxTime = -10f;
        private const float SfxInterval = 0.6f;

        /// <summary>채광 데미지. true 반환 = 광맥 소진.</summary>
        public bool TakeMineDamage(float dmg)
        {
            if (IsDestroyed) return false;
            hp -= dmg;
            // #160 - #156 lesson: grayscale 덮어쓰기 → type tint × brightness 곱 유지.
            //  화강암 (진회) → 채광 중에도 진회 hue 유지.
            if (sr != null)
            {
                float t = Mathf.Clamp01(hp / maxHp);
                Color baseTint = TypeColors[(int)type];
                float b = 0.4f + 0.6f * t;
                sr.color = new Color(baseTint.r * b, baseTint.g * b, baseTint.b * b, baseTint.a);
            }
            if (Time.time - lastSfxTime >= SfxInterval)
            {
                AudioBank.Instance?.PlayChop();  // 채광 SFX 도 chop 재활용
                lastSfxTime = Time.time;
            }
            if (hp <= 0f)
            {
                // 운영자 fb v4 - 림월드 정상 흐름: 즉시 +N 안 함. chunk 만 drop.
                int yieldN = Random.Range(stoneYieldMin, stoneYieldMax + 1);
                if (StoneChunkSprite != null)
                {
                    for (int i = 0; i < yieldN; i++)
                    {
                        Vector3 off = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0);
                        StoneChunkEntity.Spawn(transform.position + off, 1, StoneChunkSprite);
                    }
                }
                else
                {
                    ResourceManager.Instance?.AddStone(yieldN);
                }
                Destroy(gameObject);
                return true;
            }
            return false;
        }
    }
}
