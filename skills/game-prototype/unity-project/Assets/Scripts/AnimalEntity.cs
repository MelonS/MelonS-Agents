using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Day 23: peaceful wandering animal.  HP 12, drops 5 food
    /// when killed.  Wanders random 0.5 unit/sec, idle 2-5s between.
    /// Currently no flee-when-hit (Day 24+).  Pawns don't auto-attack
    /// animals — only attack hostile (BanditEnemy).  Operator can
    /// manually order via Day 24 drafted commands (future).
    /// </summary>
    /// <summary>#132 - 동물 종류.</summary>
    public enum AnimalSpecies { Deer, Boar, Chicken, Rabbit }

    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class AnimalEntity : MonoBehaviour
    {
        [SerializeField] private int maxHp = 12;
        [SerializeField] private int foodDrop = 5;
        [SerializeField] private float wanderSpeed = 0.6f;
        [SerializeField] private float wanderMin = 2f;
        [SerializeField] private float wanderMax = 5f;
        [SerializeField] private float wanderRadius = 4f;
        [SerializeField] private AnimalSpecies species = AnimalSpecies.Deer;

        public AnimalSpecies Species => species;
        public string SpeciesKr => species switch {
            AnimalSpecies.Deer    => "사슴",
            AnimalSpecies.Boar    => "멧돼지",
            AnimalSpecies.Chicken => "닭",
            AnimalSpecies.Rabbit  => "토끼",
            _ => "동물",
        };

        // 종류별 stats (HP, food drop, speed, tame rate, scale, color tint)
        public static readonly (int hp, int food, float spd, float tame, float scale, Color tint)[] SpeciesStats = {
            (12, 5, 0.6f, 0.30f, 1.0f, new Color(1f, 1f, 1f, 1f)),               // Deer
            (25, 8, 0.5f, 0.15f, 1.1f, new Color(0.55f, 0.45f, 0.40f, 1f)),     // Boar 갈회색
            (4,  2, 0.4f, 0.60f, 0.7f, new Color(1.0f, 0.95f, 0.85f, 1f)),      // Chicken 흰
            (3,  1, 0.9f, 0.50f, 0.6f, new Color(0.80f, 0.75f, 0.72f, 1f)),     // Rabbit 옅은 회색
        };

        // 운영자 2026-06-02: 멧돼지·닭·토끼가 사슴 실루엣 공유 → 종별 스프라이트.
        //  SceneSetup 이 [Deer,Boar,Chicken,Rabbit] 순으로 주입.  SetSpecies 가 스왑하므로
        //  Wildlife 의 baked 스폰 + AIDirector 런타임 스폰 모두 올바른 모양이 된다.
        public static Sprite[] SpeciesSprites;

        public void SetSpecies(AnimalSpecies s)
        {
            species = s;
            var (hp, food, spd, tame, scale, tint) = SpeciesStats[(int)s];
            maxHp = hp; foodDrop = food; wanderSpeed = spd; tameSuccessRate = tame;
            transform.localScale = new Vector3(scale, scale, 1);
            Hp = hp;
            var sR = GetComponent<SpriteRenderer>();
            // 종별 스프라이트 주입돼 있으면 그걸 쓰고 tint 는 흰색(스프라이트가 색을 가짐).
            //  없으면 기존 deer 스프라이트 + 종 tint 폴백(회귀 안전).
            bool hasSprite = SpeciesSprites != null && (int)s < SpeciesSprites.Length
                             && SpeciesSprites[(int)s] != null;
            if (sR != null)
            {
                if (hasSprite) { sR.sprite = SpeciesSprites[(int)s]; sR.color = Color.white; }
                else sR.color = tint;
            }
            // #162 - hit-flash 후 복원 baseColor (스프라이트 있으면 흰색).
            baseColor = hasSprite ? Color.white : tint;
        }

        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;

        // Stretch: Animal taming
        public bool IsTamed { get; private set; }
        [SerializeField] private float tameSuccessRate = 0.3f;
        [SerializeField] private int tameFoodCost = 1;

        /// <summary>
        /// food 소모 + 30% 성공 확률.  성공 시 IsTamed = true,
        /// 푸른빛 tint 로 시각 구별, AnimalEntity Update 가 owner 따라감(향후).
        /// </summary>
        public bool TryTame()
        {
            var rm = MelonS.GameProto.Core.Services.Get<ResourceManager>();
            if (rm == null) return false;
            if (IsTamed) { Debug.Log("[Tame] 이미 길들임"); return false; }
            if (rm.food < tameFoodCost) { Debug.Log("[Tame] 식량 부족"); return false; }
            // #버그헌트3(2026-06-04): 길들이기 식량도 SpendStockpiledFood 로 — 카운터 + 물리 InStockpile
            //  MeatPile 함께 차감(cook/eat/trade 와 동일).  이전 raw AddFood(-cost)는 카운터만 줄여
            //  '카운터 < 실제 물리 더미' desync 를 만들었다(자원모델 단일화 누락분).
            rm.SpendStockpiledFood(tameFoodCost);
            float roll = Random.value;
            if (roll < tameSuccessRate)
            {
                IsTamed = true;
                if (sr != null) sr.color = new Color(0.75f, 0.85f, 1.0f, 1f);  // 푸른빛 = tamed
                Debug.Log($"[Tame] 성공! (roll={roll:F2} < {tameSuccessRate})");
                return true;
            }
            Debug.Log($"[Tame] 실패 (roll={roll:F2} ≥ {tameSuccessRate}), food -{tameFoodCost}");
            return false;
        }

        private Rigidbody2D rb;
        private Vector2 target;
        private float nextIdleEnd = 0f;
        private bool walking = false;

        private SpriteRenderer sr;
        private float flashUntil = -1f;
        // #162 - 종 tint 보존 (#156 lesson): hit-flash 끝나면 baseColor 로 복원.
        //  이전 sr.color = Color.white 가 species tint (멧돼지 갈회색 등) 영구 손실.
        private Color baseColor = Color.white;

        private void Awake()
        {
            Hp = maxHp;
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
            BlobShadow.Attach(gameObject, 0.7f, -0.34f);  // D1 몰입 — 발밑 접지 그림자
            PickNewTarget();
        }

        private void Update()
        {
            if (IsDead) return;
            if (sr != null && flashUntil > 0 && Time.time > flashUntil)
            {
                // #162 - tamed 면 푸른빛 유지, 아니면 species baseColor 로 복원.
                sr.color = IsTamed ? new Color(0.75f, 0.85f, 1.0f, 1f) : baseColor;
                flashUntil = -1f;
            }
            if (walking)
            {
                Vector2 me = rb.position;
                Vector2 dir = target - me;
                if (dir.sqrMagnitude < 0.05f)
                {
                    walking = false;
                    nextIdleEnd = Time.time + Random.Range(wanderMin, wanderMax);
                    return;
                }
                // #202: 벽/물/바위 존중.  pawn 은 #199 A* 그리드로 막힌 셀을 돌아가지만
                //  동물은 직선 wander 라 벽을 통과했음.  TryStep 가 다음 셀이 막혔으면
                //  axis-slide(한 축만)→둘 다 막히면 정지 + 새 목표 재선택(끼임 방지).
                Vector2 step = dir.normalized * wanderSpeed * Time.deltaTime;
                if (!TryStep(me, step)) PickNewTarget();
            }
            else
            {
                if (Time.time > nextIdleEnd) PickNewTarget();
            }
        }

        // #202 — rb.MovePosition 기반 1 step 이동 시도.  목적 셀 walkable 이면 이동,
        //  아니면 X-only / Y-only axis-slide, 둘 다 막히면 정지(false).
        //  PawnMovement.IsBlockedAt = 물/바위, Grid.IsWalkable = 벽 구조물.
        //  Grid==null(headless V-scene) 이면 벽 체크 skip — 기존 wander 유지(회귀 없음).
        private bool TryStep(Vector2 me, Vector2 step)
        {
            Vector2 want = PawnMovement.ClampToWorld(me + step);
            if (!IsCellBlocked(want)) { rb.MovePosition(want); return true; }
            Vector2 slideX = PawnMovement.ClampToWorld(new Vector2(me.x + step.x, me.y));
            if (!Mathf.Approximately(slideX.x, me.x) && !IsCellBlocked(slideX))
            { rb.MovePosition(slideX); return true; }
            Vector2 slideY = PawnMovement.ClampToWorld(new Vector2(me.x, me.y + step.y));
            if (!Mathf.Approximately(slideY.y, me.y) && !IsCellBlocked(slideY))
            { rb.MovePosition(slideY); return true; }
            return false;
        }

        private static bool IsCellBlocked(Vector2 worldPos)
        {
            if (PawnMovement.IsBlockedAt(worldPos)) return true;
            var grid = PawnMovement.Grid;
            if (grid != null && !grid.IsWalkable(
                    MelonS.GameProto.AI.PathGrid.WorldToCell(worldPos)))
                return true;
            return false;
        }

        private void PickNewTarget()
        {
            // Step 81: 맵 안쪽으로 clamp — 외곽으로 wander 못 나감
            target = PawnMovement.ClampToWorld(
                (Vector2)transform.position + Random.insideUnitCircle * wanderRadius);
            walking = true;
        }

        public void TakeDamage(int dmg, GameObject source = null)
        {
            if (IsDead) return;
            Hp = Mathf.Max(0, Hp - dmg);
            if (sr != null) { sr.color = Color.white; flashUntil = Time.time + 0.06f; }
            if (AudioBank.Instance != null) AudioBank.Instance.PlayHit();
            if (Hp <= 0)
            {
                // 운영자 fb v4 - 레퍼런스 콜로니심 정상 흐름: 즉시 +N 안 함. meat pile 만 drop.
                if (MeatPileEntity.SharedSprite != null)
                {
                    MeatPileEntity.Spawn(transform.position, foodDrop, MeatPileEntity.SharedSprite);
                }
                else
                {
                    if (ResourceManager.Instance != null) ResourceManager.Instance.AddFood(foodDrop);
                }
                Destroy(gameObject);
            }
        }
    }
}
