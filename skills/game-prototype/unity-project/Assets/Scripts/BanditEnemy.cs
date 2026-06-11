// BanditEnemy.cs — Day 13 raid hostile.
//
// Subclass of the enemy-entity template pattern (HP + TakeDamage + IsDead).
// Movement: each tick, find nearest active PawnEntity within the map and
// MoveTowards on transform.  On contact (distance < 1.0) apply contactDamage
// to the pawn — throttled to 1 hit/sec, NEVER per-frame (lesson #4: don't
// hammer per-frame state changes; lesson #6 doesn't apply here because we
// poll distance rather than rely on collider Enter/Stay).
//
// On death, the base Destroy(gameObject) call is enough; the raid spawn is
// transient so OnKilled stays trivial.

using UnityEngine;

namespace MelonS.GameProto
{
    [RequireComponent(typeof(Collider2D))]
    public class BanditEnemy : MonoBehaviour
    {
        [SerializeField] private int maxHp = 20;
        [SerializeField] private int contactDamage = 2;
        // #277 1.2→4.2: pawn(4.6)보다 약간 느리되 추격 가능해야 레이드가 위협이 됨
        //  (1.2 는 영원히 못 따라잡아 레이드 무력화 — 2차 감사 rank8, 운영자 승인).
        [SerializeField] private float moveSpeed = 4.2f;
        // #277 적 기본 방어 — pawn 만 armor 감면받고 적은 full 피해받던 비대칭 보정(rank12).
        [SerializeField] private float baseArmor = 0.12f;
        [SerializeField] private float contactRange = 1.0f;
        [SerializeField] private float hitFlashSeconds = 0.06f;
        [SerializeField] private float damageInterval = 1.0f;

        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;
        // #135 - downed 면 capture 시도 가능.  #277 임계 25%→30% 로 PawnHealth(머리 30%)와
        //  정합(rank3, 운영자 승인).  정수 hp 보정 FloorToInt.
        public bool IsDowned => Hp > 0 && Hp <= Mathf.FloorToInt(maxHp * 0.3f);

        /// <summary>운영자 우클릭 capture - 50% 확률 colonist 합류.</summary>
        public bool TryCapture()
        {
            if (!IsDowned) return false;  // downed 상태만 capture
            if (Random.value < 0.5f)
            {
                // 성공 - 가까운 PawnEntity sprite + name 복제 (간단 spawn)
                var existingPawn = Object.FindFirstObjectByType<PawnEntity>();
                if (existingPawn != null)
                {
                    var go = new GameObject($"포로_{System.DateTime.Now.Ticks % 1000}");
                    go.transform.position = transform.position;
                    var sr2 = go.AddComponent<SpriteRenderer>();
                    var srcSr = existingPawn.GetComponent<SpriteRenderer>();
                    if (srcSr != null) { sr2.sprite = srcSr.sprite; sr2.sortingOrder = 10; }
                    go.AddComponent<BoxCollider2D>().size = new Vector2(2f, 2f);
                    go.AddComponent<PawnEntity>();
                    go.AddComponent<PawnMovement>();
                    go.AddComponent<PawnNeeds>();
                    go.AddComponent<PawnChopper>();
                    go.AddComponent<PawnGatherer>();
                    go.AddComponent<PawnHunter>();
                    go.AddComponent<PawnCook>();
                    go.AddComponent<PawnHauler>();
                    go.AddComponent<PawnSkills>();
                    go.AddComponent<PawnHealth>();
                    go.AddComponent<PawnWorkSettings>();
                    go.AddComponent<PawnUtilityAI>();
                    Debug.Log($"[Capture] 강도 포섭 성공!");
                }
                Destroy(gameObject);
                return true;
            }
            else
            {
                Debug.Log($"[Capture] 강도 포섭 실패, 도망");
                Destroy(gameObject);  // 실패 = 도망
                return false;
            }
        }

        private SpriteRenderer sr;
        private Color baseColor = Color.white;
        private float flashUntil = -1f;
        private float nextHitTime = -1f;

        // Target cache — refresh at most every 0.25s instead of every frame.
        // Lesson #4: FindObjectsOfType is O(n) and hammering it per-Update on
        // every BanditEnemy was the suspected cause of the Day 3 06:00 stall.
        private PawnEntity cachedTarget;
        private float nextTargetSearchTime = -1f;
        private const float TargetSearchInterval = 0.25f;

        private void Awake()
        {
            Hp = maxHp;
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
            BlobShadow.Attach(gameObject, 0.8f, -0.42f);  // D1 몰입 — 발밑 접지 그림자
            if (GetComponent<MotionFx>() == null) gameObject.AddComponent<MotionFx>();  // D2 걷기 모션
        }

        private void Update()
        {
            if (IsDead) return;

            // Hit-flash decay
            if (sr != null && flashUntil > 0 && Time.time > flashUntil)
            {
                sr.color = baseColor;
                flashUntil = -1f;
            }

            // Re-acquire target only periodically OR when cache is stale (target
            // destroyed / died).  Unity == null also catches "fake null" on
            // destroyed objects, so this is a safe staleness check.
            if (cachedTarget == null || cachedTarget.IsDead || Time.time >= nextTargetSearchTime)
            {
                cachedTarget = FindNearestPawn();
                nextTargetSearchTime = Time.time + TargetSearchInterval;
            }

            PawnEntity target = cachedTarget;
            if (target == null) return;
            // Defensive re-check: target may have been destroyed between the
            // search and now (race with another BanditEnemy on same frame).
            if (target.IsDead) return;

            Vector3 myPos = transform.position;
            Vector3 tgtPos = target.transform.position;
            float dist = Vector3.Distance(myPos, tgtPos);

            if (dist > contactRange)
            {
                // #202: 벽/물/바위 존중.  pawn 은 #199 A* 그리드로 막힌 셀을 돌아가지만
                //  강도는 직선 MoveTowards 라 벽을 통과했음.  TryStep 가 다음 셀이
                //  막혔으면 axis-slide(한 축만)→그래도 막히면 정지.
                Vector2 want = Vector2.MoveTowards(myPos, tgtPos, moveSpeed * Time.deltaTime);
                TryStep(want - (Vector2)myPos);
            }
            else
            {
                // In contact — throttled damage application (lesson #4 firewall:
                // never apply state changes every frame; gate by interval).
                if (Time.time >= nextHitTime)
                {
                    nextHitTime = Time.time + damageInterval;
                    target.TakeDamage(contactDamage, gameObject);
                }
            }
        }

        // #202 — 한 step 만큼 이동 시도.  목적 셀이 walkable 이면 이동, 아니면
        //  X-only / Y-only axis-slide, 둘 다 막히면 정지(false).  PawnMovement.IsBlockedAt
        //  = 물/바위, PawnMovement.Grid.IsWalkable = 벽 구조물.  Grid==null(headless
        //  V-scene) 이면 벽 체크 skip — 기존 직선 이동 유지(회귀 없음).
        // 게임루프 백로그 #10 (2026-06-11) — 막힌 강도의 벽 공격 상태.
        //  벽 한 겹 = 습격 영구 무효(WallEntity.TakeDamage 게임플레이 호출자 0)였다.
        //  1.5s 이상 3-슬라이드 전부 막히면 진행 방향 벽을 공격 — 벽이 '면제'에서
        //  '시간 벌기'가 되고, 목벽 100 vs 석벽 280 자재 선택이 처음으로 결정이 된다.
        private float blockedSince = -1f;
        private float nextStructHit = -1f;

        private bool TryStep(Vector2 step)
        {
            Vector3 cur = transform.position;
            Vector2 want = PawnMovement.ClampToWorld((Vector2)cur + step);
            if (!IsCellBlocked(want))
            {
                transform.position = new Vector3(want.x, want.y, cur.z);
                blockedSince = -1f;
                return true;
            }
            Vector2 slideX = PawnMovement.ClampToWorld(new Vector2(cur.x + step.x, cur.y));
            if (!Mathf.Approximately(slideX.x, cur.x) && !IsCellBlocked(slideX))
            {
                transform.position = new Vector3(slideX.x, slideX.y, cur.z);
                blockedSince = -1f;
                return true;
            }
            Vector2 slideY = PawnMovement.ClampToWorld(new Vector2(cur.x, cur.y + step.y));
            if (!Mathf.Approximately(slideY.y, cur.y) && !IsCellBlocked(slideY))
            {
                transform.position = new Vector3(slideY.x, slideY.y, cur.z);
                blockedSince = -1f;
                return true;
            }
            TryAttackBlockingWall(step);
            return false;
        }

        // 진행 방향 ~1칸 앞의 WallEntity 를 damageInterval 스로틀로 타격.
        //  (Door/Barricade 는 TakeDamage API 가 없어 후속 — 문은 의도된 진입로.)
        private void TryAttackBlockingWall(Vector2 step)
        {
            if (blockedSince < 0f) { blockedSince = Time.time; return; }
            if (Time.time - blockedSince < 1.5f) return;
            if (Time.time < nextStructHit) return;
            nextStructHit = Time.time + damageInterval;
            Vector2 cur = transform.position;
            Vector2 dir = step.sqrMagnitude > 1e-6f ? step.normalized : Vector2.right;
            foreach (var col in Physics2D.OverlapCircleAll(cur + dir * 0.9f, 0.55f))
            {
                var wall = col != null ? col.GetComponent<WallEntity>() : null;
                if (wall != null)
                {
                    wall.TakeDamage(contactDamage * 2f);   // HP tint 피드백은 WallEntity 내장
                    Debug.Log($"[Bandit] 벽 공격 -{contactDamage * 2f}");
                    break;
                }
            }
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

        private PawnEntity FindNearestPawn()
        {
            PawnEntity[] pawns = GameObject.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
            if (pawns == null || pawns.Length == 0) return null;
            PawnEntity nearest = null;
            float bestSq = float.MaxValue;
            Vector3 myPos = transform.position;
            foreach (var p in pawns)
            {
                if (p == null) continue;
                if (p.IsDead) continue;
                float sq = (p.transform.position - myPos).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; nearest = p; }
            }
            return nearest;
        }

        public void TakeDamage(int dmg, GameObject source = null)
        {
            if (IsDead) return;
            int eff = Mathf.Max(1, Mathf.RoundToInt(dmg * (1f - baseArmor)));  // #277 적 방어 감면
            Hp = Mathf.Max(0, Hp - eff);
            AudioBank.Instance?.PlayHit();   // #게임필2 — 백병전 타격이 들린다 (0.25s 스로틀 내장)
            if (sr != null)
            {
                sr.color = Color.white;
                flashUntil = Time.time + hitFlashSeconds;
            }
            if (Hp <= 0)
            {
                Debug.Log($"[BanditEnemy] killed by {(source != null ? source.name : "?")}");
                Destroy(gameObject);
            }
        }

        public int GetContactDamage() => contactDamage;
    }
}
