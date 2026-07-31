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
        // 2026-07-31 운영자 "동물들 왜 그냥 서있기만해?" — 실측으로 확인했다:
        //  1배속 5초 동안 13마리 중 6마리만 0.5칸 이상 움직였고 **최대 이동이 0.76칸**이었다.
        //  즉 wander 는 돌고 있는데 **대기 2~5초 + 반경 4칸**이라 한 번의 이동이 짧고
        //  대부분의 시간을 서 있는다.  기본 줌(세로 30칸)에서는 그 정도 드리프트가
        //  '정지'로 읽힌다 — 콜로니스트가 맵을 가로지르는 것과 대비되면 더 그렇다.
        //  종별 속도(spd 0.4~0.9)는 **밸런스라 건드리지 않고**, 리듬만 고친다:
        //  대기를 절반으로, 반경을 1.75배로 → 한 번에 더 멀리, 더 자주 움직인다.
        [SerializeField] private float wanderMin = 1.0f;
        [SerializeField] private float wanderMax = 2.5f;
        [SerializeField] private float wanderRadius = 7f;
        private Vector2 stuckAnchor;   // 정체 감지 기준점 (2026-07-31)
        private float stuckSince;
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
        // 아트 v2 (2026-06-11): scale 은 구판(사슴 실루엣 공유)에서 종 크기를 내던
        //  보정값 — 32px 원화는 크기가 그림에 내장(닭 14px/사슴 28px)이라 과축소된다.
        //  닭/토끼만 살짝(0.85), 나머지 1.0 으로 정규화.
        // 크기 위계 재조정 (2026-07-29, 운영자 "동물들의 사이즈가 애매해 보인다").
        //  실측해 보니 위계가 사실상 없었다 — 화면에 그려지는 폭(칸):
        //    멧돼지 0.94 · 늑대 0.91 · 사슴 0.88 · 토끼 0.48 · 닭 0.37
        //  토끼가 사슴 높이의 79%(0.64 vs 0.81)라 "큰 토끼"로 보이고, 늑대·멧돼지·
        //  사슴이 서로 구분이 안 됐다.  콜로니스트는 0.44×0.91 이라 폭만 보면
        //  토끼와 비슷하다.
        //  32px 원화는 크기가 그림에 내장돼 있어(닭 12px/사슴 28px) 원화만으로는
        //  위계가 부족하다 — scale 로 보정한다.  체감 목표(폭):
        //    사슴 0.88 > 멧돼지 0.89 > 늑대 0.80 > 토끼 0.31 > 닭 0.30
        //  hp/food/spd/tame 은 밸런스라 건드리지 않는다 (크기만 조정).
        public static readonly (int hp, int food, float spd, float tame, float scale, Color tint)[] SpeciesStats = {
            (12, 5, 0.6f, 0.30f, 1.00f, new Color(1f, 1f, 1f, 1f)),              // Deer  가장 큼 (기준)
            (25, 8, 0.5f, 0.15f, 0.95f, new Color(0.55f, 0.45f, 0.40f, 1f)),     // Boar  갈회색, 길고 낮음
            (4,  2, 0.4f, 0.60f, 0.70f, new Color(1.0f, 0.95f, 0.85f, 1f)),      // Chicken 흰, 가장 작음
            (3,  1, 0.9f, 0.50f, 0.55f, new Color(0.80f, 0.75f, 0.72f, 1f)),     // Rabbit 옅은 회색, 닭과 비슷
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
            // (실루엣 캐스터는 2026-07-31 철회 — 사람·동물은 스프라이트가 작아
            //  눕혀도 본체에 가린 검은 얼룩이 되고 발밑 타원과 두 겹이 된다.
            //  레퍼런스도 사람에겐 단순 접지 그림자를 쓰고 긴 투영 그림자는
            //  큰 구조물에만 쓴다.  발밑 타원은 SunShadowDriver 가 태양을 따라
            //  방향·길이·농도를 이미 움직인다.)
            if (GetComponent<MotionFx>() == null) gameObject.AddComponent<MotionFx>();  // D2 걷기 모션
            if (GetComponent<AnimalAnim32>() == null) gameObject.AddComponent<AnimalAnim32>();  // 아트 v2
            PickNewTarget();
        }

        // 운영자 피드백 #12 (2026-06-12 AM) — 피격 무반응이던 동물에 레퍼런스 반응:
        //  초식(사슴/토끼/닭)은 공격자 반대편으로 도주 가속, 멧돼지는 반격 돌진.
        private float fleeUntil = -1f;
        private GameObject revengeTarget;
        private float revengeUntil = -1f;
        private float nextRevengeHit = -1f;
        // 갭 TOP-11 (2026-06-13) — 광기(manhunter): 반격 AI 의 '선공 모드' 변종.
        //  지속 시간 동안 대상이 죽거나 사라지면 가장 가까운 림을 재타겟.
        private float manhunterUntil = -1f;
        public bool IsManhunter => manhunterUntil > 0f && Time.time < manhunterUntil && !IsDead;

        public void ForceManhunter(float durationSec)
        {
            manhunterUntil = Time.time + durationSec;
            RetargetNearestPawn();
        }

        private void RetargetNearestPawn()
        {
            PawnEntity best = null; float bestSq = float.MaxValue;
            foreach (var pe in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
            {
                if (pe == null || pe.IsDead) continue;
                float sq = ((Vector2)pe.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = pe; }
            }
            if (best != null)
            {
                revengeTarget = best.gameObject;
                revengeUntil = manhunterUntil;   // 광기 지속 내내 추격
            }
        }

        private void Update()
        {
            if (IsDead) return;
            // TOP-11 광기 — 대상을 잃으면(사망/소멸) 다음 림으로 재타겟.
            if (IsManhunter && revengeTarget == null) RetargetNearestPawn();
            // #12 멧돼지 반격 — 공격자에게 돌진, 접촉 시 피해.  대상 사망/시간 만료 시 해제.
            if (revengeTarget != null)
            {
                var rpe = revengeTarget.GetComponent<PawnEntity>();
                if (rpe == null || rpe.IsDead || Time.time >= revengeUntil) revengeTarget = null;
                else
                {
                    Vector2 meR = rb.position;
                    Vector2 tpR = revengeTarget.transform.position;
                    if (Vector2.Distance(meR, tpR) > 0.9f)
                    {
                        Vector2 stepR = (tpR - meR).normalized * wanderSpeed * 2.2f * Time.deltaTime;
                        if (!TryStep(meR, stepR)) { revengeTarget = null; }
                    }
                    else if (Time.time >= nextRevengeHit)
                    {
                        nextRevengeHit = Time.time + 1.2f;
                        rpe.TakeDamage(3, gameObject);
                    }
                    if (sr != null && flashUntil > 0 && Time.time > flashUntil)
                    { sr.color = IsTamed ? new Color(0.75f, 0.85f, 1.0f, 1f) : baseColor; flashUntil = -1f; }
                    return;   // 반격 중엔 wander 스킵
                }
            }
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
                // #12 도주 중엔 2.5배 가속 (사냥이 '서서 맞아주기'가 아니게).
                float spdMul = Time.time < fleeUntil ? 2.5f : 1f;
                Vector2 step = dir.normalized * wanderSpeed * spdMul * Time.deltaTime;
                if (!TryStep(me, step)) PickNewTarget();

                // 2026-07-31 — **정체 감지.**  TryStep 이 성공(=목적 셀이 walkable)해도
                //  Rigidbody2D 가 나무·바위 콜라이더에 물리적으로 막히면 실제 위치는
                //  안 움직인다.  그러면 목표에 영원히 못 닿아 `dir.sqrMagnitude < 0.05`
                //  가 성립하지 않고, 동물은 장애물을 밀며 **그 자리에 서 있는다.**
                //  이것이 운영자가 본 "그냥 서있기만해"의 실체다 — wander 가 안 도는 게
                //  아니라 돌다가 걸려서 멈춰 있는 것.
                //  1.2초 동안 0.05칸도 못 나아갔으면 목표를 포기하고 다시 뽑는다.
                if ((rb.position - stuckAnchor).sqrMagnitude > 0.0025f)
                {
                    stuckAnchor = rb.position;
                    stuckSince = Time.time;
                }
                else if (Time.time - stuckSince > 1.2f)
                {
                    stuckAnchor = rb.position;
                    stuckSince = Time.time;
                    PickNewTarget();
                }
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
            //
            // 2026-07-31 — **목표가 갈 수 있는 곳인지 확인한다.**
            //  이전에는 반경 안의 아무 점이나 잡았다.  이 맵은 바위 지대와 호수가 넓어서
            //  상당수가 도달 불가였고, 그러면 몇 걸음 가다 TryStep 이 막혀 곧바로 새 목표를
            //  다시 뽑는다 → 제자리에서 흔들리기만 한다.
            //  실측(15초 고정 창): 13마리 중 10마리가 움직이긴 하는데 **최대 이동이 1.68칸**
            //  이었다.  콜로니스트가 맵을 가로지르는 것과 나란히 놓이면 '서 있는 것'으로 읽힌다.
            //  → 갈 수 있는 칸이 나올 때까지 최대 8번 다시 뽑는다.  전부 실패하면 예전처럼
            //    그냥 잡는다(막힌 곳에 갇혀도 영원히 얼어붙지는 않게).
            Vector2 me = transform.position;
            for (int i = 0; i < 8; i++)
            {
                Vector2 cand = PawnMovement.ClampToWorld(me + Random.insideUnitCircle * wanderRadius);
                if (PawnMovement.IsBlockedAt(cand)) continue;
                if (PawnMovement.Grid != null
                    && !PawnMovement.Grid.IsWalkable(MelonS.GameProto.AI.PathGrid.WorldToCell(cand)))
                    continue;
                target = cand; walking = true; return;
            }
            target = PawnMovement.ClampToWorld(me + Random.insideUnitCircle * wanderRadius);
            walking = true;
        }

        public void TakeDamage(int dmg, GameObject source = null)
        {
            if (IsDead) return;
            Hp = Mathf.Max(0, Hp - dmg);
            if (sr != null) { sr.color = Color.white; flashUntil = Time.time + 0.06f; }
            if (AudioBank.Instance != null) AudioBank.Instance.PlayHit();
            // #12 피격 반응 — 멧돼지: 반격 / 그 외: 공격자 반대편 도주 가속.
            if (source != null && Hp > 0 && !IsTamed)
            {
                if (species == AnimalSpecies.Boar)
                {
                    revengeTarget = source;
                    revengeUntil = Time.time + 8f;
                }
                else
                {
                    Vector2 away = ((Vector2)transform.position - (Vector2)source.transform.position);
                    if (away.sqrMagnitude < 0.01f) away = Random.insideUnitCircle;
                    target = PawnMovement.ClampToWorld((Vector2)transform.position + away.normalized * 9f);
                    walking = true;
                    fleeUntil = Time.time + 6f;
                }
            }
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
