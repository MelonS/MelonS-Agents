using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 64 — Wolf predator.  Wanders by default; if a pawn enters detection
    /// radius (5 unit), charges + attacks at melee range.  Drops 8 food on death.
    /// Spawned by AIDirector when wolf_pack event fires (or pre-placed for demo).
    /// </summary>
    public class WolfEnemy : MonoBehaviour
    {
        [SerializeField] private int maxHp = 18;
        [SerializeField] private int attackDamage = 4;
        [SerializeField] private float attackRange = 0.9f;
        [SerializeField] private float attackInterval = 1.2f;
        // #277 5→7: ThreatAlertUI 경고(8)와 1유닛 버퍼 정합 + 위협 성립(rank8, 승인).
        [SerializeField] private float detectionRadius = 7.0f;
        [SerializeField] private float baseArmor = 0.05f;   // #277 적 방어 비대칭 보정(rank12)
        // #200 genre fidelity ripple: pawn moveSpeed rose 3.0→4.6, so wolf
        //  chase must be >= 4.6 or a predator can never catch a fleeing pawn.
        //  the reference sim timber wolf ~4.6 c/s; set 5.0 so wolves stay a credible threat.
        [SerializeField] private float chaseSpeed = 5.0f;
        [SerializeField] private float wanderSpeed = 0.8f;
        [SerializeField] private int dropFood = 8;

        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;

        private Vector3 wanderTarget;
        private float nextWanderPick = -1f;
        private float nextAttackTime = -1f;
        private float nextTargetSearch = -1f;
        private PawnEntity cachedTarget;
        // #163 - hit-flash 시각 피드백 (BanditEnemy 와 같은 패턴).
        //  이전: 늑대가 공격받아도 시각 변화 0 → 플레이어가 데미지 들어가는지 알 수 없음.
        private SpriteRenderer sr;
        private Color baseColor = Color.white;
        private float flashUntil = -1f;
        private const float HitFlashSeconds = 0.08f;

        private void Awake()
        {
            Hp = maxHp;
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
            BlobShadow.Attach(gameObject, 0.8f, -0.38f);  // D1 몰입 — 발밑 접지 그림자
            if (GetComponent<MotionFx>() == null) gameObject.AddComponent<MotionFx>();  // D2 걷기 모션
            if (GetComponent<AnimalAnim32>() == null) gameObject.AddComponent<AnimalAnim32>();  // 아트 v2
            // 크기 위계 (2026-07-29) — 늑대는 AnimalEntity.SpeciesStats 를 안 타므로
            //  스케일이 1.0 으로 남아 멧돼지(0.94)·사슴(0.88)과 폭이 거의 같았다.
            //  포식자가 사슴보다 살짝 작아야 위계가 읽힌다 → 0.88 (체감 폭 0.80칸).
            transform.localScale = new Vector3(0.88f, 0.88f, 1f);
            PickNewWanderTarget();
        }

        private void Update()
        {
            if (IsDead) return;
            // #163 - hit-flash 복원
            if (sr != null && flashUntil > 0 && Time.time > flashUntil)
            {
                sr.color = baseColor;
                flashUntil = -1f;
            }
            // Find target every 0.4s
            if (Time.time > nextTargetSearch)
            {
                cachedTarget = FindNearestPawn();
                nextTargetSearch = Time.time + 0.4f;
            }
            // Combat mode
            if (cachedTarget != null && !cachedTarget.IsDead)
            {
                Vector3 dir = cachedTarget.transform.position - transform.position;
                float dist = dir.magnitude;
                if (dist <= attackRange)
                {
                    if (Time.time > nextAttackTime)
                    {
                        nextAttackTime = Time.time + attackInterval;
                        cachedTarget.TakeDamage(attackDamage, gameObject);
                    }
                    return;
                }
                // Chase — #202: 벽/물/바위 존중.  pawn 은 #199 A* 그리드로 막힌 셀을
                //  돌아가지만 늑대는 직선 이동이라 벽을 통과했음.  TryStep 가 다음 셀이
                //  막혔으면 axis-slide(한 축만)→그래도 막히면 정지.  (늑대 비활성이나
                //  WolfEnemy/BanditEnemy/AnimalEntity 정합 위해 동일 패턴 적용.)
                Vector2 chaseStep = (Vector2)dir.normalized * chaseSpeed * Time.deltaTime;
                TryStep(chaseStep);
                return;
            }
            // Wander
            if (Time.time > nextWanderPick) PickNewWanderTarget();
            Vector3 d = wanderTarget - transform.position;
            if (d.magnitude < 0.1f) { PickNewWanderTarget(); return; }
            Vector2 wstep = (Vector2)d.normalized * wanderSpeed * Time.deltaTime;
            // #202: wander 도 벽 막히면 새 목표 재선택(끼임 방지).
            if (!TryStep(wstep)) PickNewWanderTarget();
        }

        // #202 — 한 step 만큼 이동 시도.  목적 셀이 walkable 이면 그대로 이동,
        //  아니면 X-only / Y-only axis-slide 로 벽을 따라 미끄러짐, 둘 다 막히면
        //  제자리(false 반환).  PawnMovement.IsBlockedAt = 물/바위 타일,
        //  PawnMovement.Grid.IsWalkable = 벽(구조물) 셀.  Grid==null(headless V-scene)
        //  이면 벽 체크 skip — 기존 clamp-only 동작 유지(회귀 없음).
        private bool TryStep(Vector2 step)
        {
            Vector3 cur = transform.position;
            Vector2 want = PawnMovement.ClampToWorld((Vector2)cur + step);
            if (!IsCellBlocked(want))
            {
                transform.position = new Vector3(want.x, want.y, cur.z);
                return true;
            }
            // axis-slide: X 만
            Vector2 slideX = PawnMovement.ClampToWorld(new Vector2(cur.x + step.x, cur.y));
            if (!Mathf.Approximately(slideX.x, cur.x) && !IsCellBlocked(slideX))
            {
                transform.position = new Vector3(slideX.x, slideX.y, cur.z);
                return true;
            }
            // axis-slide: Y 만
            Vector2 slideY = PawnMovement.ClampToWorld(new Vector2(cur.x, cur.y + step.y));
            if (!Mathf.Approximately(slideY.y, cur.y) && !IsCellBlocked(slideY))
            {
                transform.position = new Vector3(slideY.x, slideY.y, cur.z);
                return true;
            }
            return false;   // 둘 다 막힘 → 정지
        }

        // #202 — 물/바위(IsBlockedAt) 또는 벽 구조물(Grid !IsWalkable) 이면 막힘.
        private static bool IsCellBlocked(Vector2 worldPos)
        {
            if (PawnMovement.IsBlockedAt(worldPos)) return true;
            var grid = PawnMovement.Grid;
            if (grid != null && !grid.IsWalkable(
                    MelonS.GameProto.AI.PathGrid.WorldToCell(worldPos)))
                return true;
            return false;
        }

        private PawnEntity FindNearestPawn()
        {
            PawnEntity[] all = GameObject.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0) return null;
            PawnEntity best = null;
            float bestSq = detectionRadius * detectionRadius;
            Vector3 me = transform.position;
            foreach (var p in all)
            {
                if (p == null || p.IsDead) continue;
                float sq = (p.transform.position - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = p; }
            }
            return best;
        }

        private void PickNewWanderTarget()
        {
            float r = Random.Range(2f, 4f);
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector2 raw = (Vector2)transform.position + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            Vector2 clamped = PawnMovement.ClampToWorld(raw);  // Step 81
            wanderTarget = new Vector3(clamped.x, clamped.y, 0f);
            nextWanderPick = Time.time + Random.Range(3f, 6f);
        }

        public void TakeDamage(int dmg, GameObject source = null)
        {
            if (IsDead) return;
            int eff = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - baseArmor)));  // #277 적 방어 감면
            Hp = Mathf.Max(0, Hp - eff);
            // #163 - 시각 피드백
            if (sr != null)
            {
                sr.color = Color.white;
                flashUntil = Time.time + HitFlashSeconds;
            }
            if (Hp <= 0)
            {
                Debug.Log($"[Wolf] killed → +{dropFood} 고기 (pile drop)");
                // 운영자 fb v4 - 레퍼런스 콜로니심 정상 흐름: meat pile 만 drop.
                if (MeatPileEntity.SharedSprite != null)
                {
                    MeatPileEntity.Spawn(transform.position, dropFood, MeatPileEntity.SharedSprite);
                }
                else if (ResourceManager.Instance != null)
                {
                    ResourceManager.Instance.AddFood(dropFood);
                }
                Destroy(gameObject, 0.5f);
            }
        }
    }
}
