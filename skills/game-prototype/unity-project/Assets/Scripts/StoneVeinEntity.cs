using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// #119 - 레퍼런스 콜로니심 광맥 (stone vein).
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
            //  #167 - TintHelper 로 통합.
            if (sr != null)
            {
                TintHelper.ApplyHpBrightness(sr, TypeColors[(int)type], hp / maxHp, minBright: 0.4f);
            }
            if (Time.time - lastSfxTime >= SfxInterval)
            {
                // W-M3-01 Lane D: PlayMine() replaces PlayChop() — wiki #7 acceptance:
                // "Mining ... plays a distinct sound" (pick-on-stone, not the chop thunk).
                // AudioBank.PlayMine() has its own _lastMineTime throttle (0.25s, independent
                // of the chop throttle) + graceful null no-op if sfxMine unassigned.
                // Entity-level SfxInterval guard (0.6s) above is kept intact.
                AudioBank.Instance?.PlayMine();
                lastSfxTime = Time.time;
            }
            if (hp <= 0f)
            {
                // 운영자 fb v4 - 레퍼런스 콜로니심 정상 흐름: 즉시 +N 안 함. chunk 만 drop.
                int yieldN = Random.Range(stoneYieldMin, stoneYieldMax + 1);
                // #게임필 배치2 — 보상 순간: 붕괴가 들리고 산출이 보인다 (TreeEntity 와 대칭).
                AudioBank.Instance?.PlayRockbreak();
                FloatingText.Spawn(transform.position + Vector3.up * 0.4f,
                                   $"+{yieldN} 석재", new Color(0.75f, 0.78f, 0.85f, 1f));
                // #215 운영자 fb "캐면 순간이동" — StoneChunkEntity.EnsureSprite 가 sprite
                //  null 이어도 코드 기본 돌덩이를 보장하므로, 즉시 AddStone 폴백(텔레포트)을
                //  완전히 제거하고 항상 물리 chunk 를 떨어뜨린다(hauler 가 운반해야 적립).
                for (int i = 0; i < yieldN; i++)
                {
                    Vector3 off = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0);
                    StoneChunkEntity.Spawn(transform.position + off, 1, StoneChunkSprite);
                }
                Destroy(gameObject);
                return true;
            }
            return false;
        }
    }
}
