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

        // 아트 v3 (2026-07-25): 종별 전용 페인털리 스프라이트 (SceneSetup 주입) —
        //  스프라이트가 재질색을 담으므로 틴트는 흰색 베이스 (HP 어두워짐 곱만 유지).
        public static Sprite[] TypeSprites;
        public static readonly Color[] TypeColors = {
            Color.white, Color.white, Color.white, Color.white,
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
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = TypeColors[(int)t];
                // v3 종별 스프라이트 (주입 전/에디트타임엔 기존 스프라이트 유지)
                if (TypeSprites != null && TypeSprites.Length > (int)t && TypeSprites[(int)t] != null)
                    sr.sprite = TypeSprites[(int)t];
            }
            maxHp = 200f * TypeHpMul[(int)t];
            hp = maxHp;
        }

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            // 씬에 구워진 값 대신 코드 표를 정본으로 (2026-08-01 리뷰 #8).
            //  AnimalEntity 와 같은 함정이었다 — Awake 가 직렬화된 maxHp 를 읽고
            //  SetType 을 부르지 않아, 씬의 광맥 45개가 베이크 시점 스탯에 동결돼
            //  TypeHpMul 을 고쳐도 실제 게임엔 반영되지 않았다.
            SetType(type);
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
                // D3 몰입 — 곡괭이질 순간 돌파편 (SFX 스로틀과 동기 = 채광 리듬).
                ParticleFx.Burst(transform.position + Vector3.up * 0.2f,
                                 new Color(0.62f, 0.64f, 0.68f, 1f));
            }
            if (hp <= 0f)
            {
                // 운영자 fb v4 - 레퍼런스 콜로니심 정상 흐름: 즉시 +N 안 함. chunk 만 drop.
                int yieldN = Random.Range(stoneYieldMin, stoneYieldMax + 1);
                // #게임필 배치2 — 보상 순간: 붕괴가 들리고 산출이 보인다 (TreeEntity 와 대칭).
                AudioBank.Instance?.PlayRockbreak();
                // D3 몰입 — 붕괴 순간 큰 돌파편 버스트 (TreeEntity 쓰러짐과 대칭).
                ParticleFx.Burst(transform.position + Vector3.up * 0.25f,
                                 new Color(0.62f, 0.64f, 0.68f, 1f), count: 14, speed: 2.4f);
                FloatingText.Spawn(transform.position + Vector3.up * 0.4f,
                                   $"+{yieldN} 석재", new Color(0.75f, 0.78f, 0.85f, 1f));
                // #215 운영자 fb "캐면 순간이동" — StoneChunkEntity.EnsureSprite 가 sprite
                //  null 이어도 코드 기본 돌덩이를 보장하므로, 즉시 AddStone 폴백(텔레포트)을
                //  완전히 제거하고 항상 물리 chunk 를 떨어뜨린다(hauler 가 운반해야 적립).
                for (int i = 0; i < yieldN; i++)
                {
                    Vector3 off = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0);
                    StoneChunkEntity.Spawn(transform.position + off, 1, StoneChunkSprite);
                Debug.Log($"[Mine] mined {name} @ ({transform.position.x:F1},{transform.position.y:F1})");
                }
                Destroy(gameObject);
                return true;
            }
            return false;
        }
    }
}
