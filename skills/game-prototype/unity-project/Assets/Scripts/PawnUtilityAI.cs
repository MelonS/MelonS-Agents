using System.Collections.Generic;
using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 4 utility AI + Day 11 food + Day 20 mood break + Day 24 hunt.
    /// </summary>
    [RequireComponent(typeof(PawnMovement))]
    [RequireComponent(typeof(PawnChopper))]
    public class PawnUtilityAI : MonoBehaviour
    {
        [SerializeField] private float decisionInterval = 1.5f;
        // 운영자 2026-06-02: 떠도는중 = 근처 타일 왕복.  반경 3→2 로 좁혀 "근처" 유지.
        //  non-serialized(=평범 private): 프리팹에 구워진 옛 값(3)이 덮어쓰지 않도록 —
        //  source 값이 항상 적용된다(거대 씬 regen 커밋 불필요).
        private float idleWanderRadius = 2f;
        // 떠도는중 hop 간격 — 운영자 2026-06-02 "2초에 1칸은 움직여야": 1.5s 마다 1칸+
        //  (2초 이내 최소 1칸 보장).  work-decision(1.5s)과 별개로 idle 림을 또박또박 이동.
        //  non-serialized: 프리팹 직렬화 override 방지(0.8 잔존값 → source 1.5 항상 적용).
        private float idleStepInterval = 1.5f;
        private float lastIdleStep = -999f;
        // 왕복(왔다갔다) 기준점 — idle 시작 시 고정, 그 주변 근처 타일을 오간다.
        //  실제 작업/필요 상태가 되면 해제 → 다음 idle 때 새 위치에 다시 잡음.
        private Vector2 idleAnchor;
        private bool hasIdleAnchor;
        [Header("Day 11: food gather priority")]
        [SerializeField] private float foodHungryThreshold = 40f;
        [Header("Day 24: hunt when stockpile food low")]
        [SerializeField] private float globalFoodLowThreshold = 10f;

        private PawnMovement movement;
        private PawnChopper chopper;
        private PawnGatherer gatherer;
        private PawnHunter hunter;
        private PawnCook cook;
        private PawnHauler hauler;  // #116 — wood pile pickup
        private PawnHarvester harvester;  // #202 — ripe crop harvest
        private PawnBuilder builder;  // #118 — blueprint 건설
        private PawnMiner miner;      // #119 — 채광
        private PawnDoctor doctor;    // #125 — 의료
        private PawnSchedule schedule; // #126 — 시간대별 행동
        private PawnNeeds needs;
        private PawnEntity entity;  // Day 48 — drafted state check
        private PawnWorkSettings workSettings;  // #114 — per-pawn work priority
        private float lastDecision = -999f;
        private float lastDraftAttackTime = -999f;
        private const float DraftAttackInterval = 0.8f;
        // Day 50: bow ranged attack
        [SerializeField] private Sprite arrowSprite;
        private const float RangedAttackRange = 5.0f;
        private const float RangedAttackInterval = 1.5f;
        private float lastRangedAttackTime = -999f;
        public void SetArrowSprite(Sprite s) { arrowSprite = s; }

        // R5: Strategy pattern — Decide() priority list + reusable context
        private PawnContext ctx;
        private List<IPawnAction> actions;
        // 자율 취침: 생존 행동이라 work-priority loop 보다 먼저 시도 (work settings 무관).
        private GoSleepAction goSleep;
        // 자율 취침으로 예약한 침대 — 기상/취소 시 ReservationManager 에서 해제하기 위해 추적.
        private BedEntity reservedSleepBed;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
            chopper = GetComponent<PawnChopper>();
            gatherer = GetComponent<PawnGatherer>();
            hunter = GetComponent<PawnHunter>();
            cook = GetComponent<PawnCook>();
            hauler = GetComponent<PawnHauler>();  // #116
            harvester = GetComponent<PawnHarvester>();  // #202
            builder = GetComponent<PawnBuilder>();  // #118
            miner = GetComponent<PawnMiner>();      // #119
            doctor = GetComponent<PawnDoctor>();    // #125
            schedule = GetComponent<PawnSchedule>();// #126
            needs = GetComponent<PawnNeeds>();
            entity = GetComponent<PawnEntity>();
            workSettings = GetComponent<PawnWorkSettings>();  // #114
            // R5: ctx + action priority list
            ctx = new PawnContext
            {
                entity = entity, movement = movement, chopper = chopper,
                gatherer = gatherer, hunter = hunter, cook = cook,
                hauler = hauler,
                harvester = harvester,  // #202
                builder = builder,
                miner = miner,
                doctor = doctor,
                needs = needs, skills = GetComponent<PawnSkills>(),
                transform = transform,
                idleWanderRadius = idleWanderRadius,
            };
            goSleep = new GoSleepAction();
            actions = new List<IPawnAction>
            {
                new TendPatientAction(),       // #125 - 부상 동료 치료 최우선
                new EatBerryAction   { foodThreshold = foodHungryThreshold },
                new HuntAnimalAction { globalFoodThreshold = globalFoodLowThreshold },
                // #137 운영자 fb fix: foodSurplus 5 → 15.  starter food=10 일 때
                //  모든 pawn 이 cook 만 → food 떨어지면 hunt → 다시 cook 무한 loop,
                //  ChopTree 영영 안 됨 = "목재 안 캐짐" 진짜 원인.
                new CookMealAction   { foodSurplus = 15f },
                // #202 SURVIVAL-LOOP FIX — harvest ripe crops into the food stockpile.
                //  Placed here (above build/haul/chop generic labor) so idle pawns
                //  prioritise sustenance work; this is the missing link that feeds the
                //  cook→eat chain.  WorkKind.Gather so it shares the gather priority slot.
                new HarvestCropAction(),
                new BuildBlueprintAction(),  // #118 - 청사진 건설 (chop 보다 우선)
                // ── HAUL loose ground piles FIRST (above raw extraction) ──
                //  운영자 fb #I4-regress: 떨어진 더미가 적립 전에 부패(옥외 30s/-1)하지
                //  않도록, 줍을 더미가 있으면 새 채집/벌목보다 운반을 먼저 한다.  세 종류를
                //  나란히 둬서 wood/stone/meat 어느 것이든 바닥에 있으면 즉시 stockpile 로
                //  운반→카운터 적립(물리 운반 유지, 순간이동 아님).
                new HaulWoodAction(),     // #116 - 벌목 후 떨어진 wood pile 운반
                new HaulStoneAction(),    // #119 - 채광 후 떨어진 stone chunk 운반
                new HaulMeatAction(),     // #129 - 사냥 후 떨어진 meat pile 운반
                // ── #230 장르 정합: 야생나무 벌목·광맥 채광은 *플레이어 지정제* ──
                //  the reference sim 에선 림이 아무 나무·바위나 자동으로 캐지 않는다 — 이게 우리 게임의
                //  '자원 무한 자동축적'(목재 40→180)의 원인이었다.  자동 ChopTree/MineStone 을
                //  auto-loop 에서 제거:
                //    채광 = MineDesignation(플레이어가 drag-마킹 → idle 광부 자동 디스패치)
                //    벌목 = 우클릭 '벌목 우선'(PawnChopper) 으로 플레이어가 지정
                //  → 자원이 플레이어 지정으로만 모이고 건설로 소비된다(the reference sim 경제).
                //  운반·요리·수확·사냥(생존)·경작 zone 은 the reference sim 처럼 자동 유지.
                // 운영자 2026-06-02: idle 배회는 리스트의 WanderAction(1.5s 간격, 긴 정지)
                //  대신 Update 의 anchored pacing(idleStepInterval 0.8s, 근처 타일 왕복)이
                //  전담한다.  WanderAction 이 리스트에 있으면 decision 프레임마다 폰을 멀리
                //  움직여 pacing 의 '정지 상태' 조건을 굶겼다(IdleHop 0회 버그) → 제거.
                //  (WanderAction 클래스 자체는 TestV41_WanderAction 이 직접 쓰므로 유지.)
            };
        }

        private void Update()
        {
            // Day 48: drafted pawn skip utility AI — manual control only.
            if (entity != null && entity.IsDrafted)
            {
                HandleDraftedCombat();
                return;
            }
            // 운영자 피드백: 우클릭 이동이 AI 에 즉시 override 됐던 문제 fix.
            //  ClickSelector 가 ManualMoveUntil 을 Time.time+5 로 설정하면
            //  그 동안 AI Decide skip (사용자 이동 명령 존중).
            if (entity != null && entity.IsUnderManualControl) return;

            // 자율 취침 예약 해제: 기상/취소(PawnNeeds 가 autoRestTarget 을 비움)나
            //  사용자 우클릭 휴식 명령이 끼어든 경우, 잡고 있던 침대 예약을 푼다.
            //  매 frame 검사 (이동 중에도) — 림이 깬 즉시 다른 림이 그 침대 쓸 수 있게.
            if (reservedSleepBed != null
                && (needs == null || !needs.HasAutoSleepOrder || needs.HasRestOrder))
            {
                AI.ReservationManager.Release(reservedSleepBed, gameObject);
                reservedSleepBed = null;
            }

            // rcfix: 사용자가 침대 우클릭으로 "쉬어" 명령(needs.HasRestOrder)을 내린 동안은
            //  AI 가 다른 work 를 집지 않는다.  pawn 이 침대로 이동 → 도착 후 PawnNeeds 가
            //  강제 수면(IsSleeping) 처리.  아직 침대로 가는 중(ManualMoveUntil 만료 후)에도
            //  AI 가 끼어들어 target 을 뺏지 않도록 여기서 모든 task 정리 + 조기 return.
            if (needs != null && needs.HasRestOrder && !needs.IsSleeping)
            {
                // 침대로 가는 중 — 잔여 work task 만 정리 (이동 target 은 ClickSelector 가 박음).
                chopper.ClearTask();
                if (gatherer != null) gatherer.ClearTask();
                if (hunter != null) hunter.ClearTask();
                if (cook != null) cook.ClearTask();
                if (hauler != null) hauler.ClearTask();
                if (builder != null) builder.ClearTask();
                if (miner != null) miner.ClearTask();
                if (doctor != null) doctor.ClearTask();
                // 침대로 가는 이동 target 이 풀렸으면(도착 못 했는데 멈춤) 다시 박아준다.
                if (!movement.IsMoving && needs.RestTarget != null)
                    movement.SetTarget(needs.RestTarget.transform.position);
                lastDecision = Time.timeSinceLevelLoad;
                return;
            }

            // 자율 취침: 침대로 가는 중(HasAutoSleepOrder && !IsSleeping).  사용자
            //  forcedResting 과 동일하게 잔여 work task 정리 + 이동 target 유지.  도착 시
            //  PawnNeeds 가 IsSleeping 처리 (아래 IsSleeping 블록으로 넘어감).
            if (needs != null && needs.HasAutoSleepOrder && !needs.IsSleeping && !needs.HasRestOrder)
            {
                // 침대가 파괴됐으면 자율 취침 취소 (예약 해제는 위 블록이 다음 frame 처리).
                if (needs.AutoRestTarget == null)
                {
                    needs.ClearAutoSleepTarget();
                }
                else
                {
                    chopper.ClearTask();
                    if (gatherer != null) gatherer.ClearTask();
                    if (hunter != null) hunter.ClearTask();
                    if (cook != null) cook.ClearTask();
                    if (hauler != null) hauler.ClearTask();
                    if (builder != null) builder.ClearTask();
                    if (miner != null) miner.ClearTask();
                    if (doctor != null) doctor.ClearTask();
                    // 이동이 멈췄는데 아직 침대 위가 아니면 다시 침대 cell 위로 향하게.
                    //  GoSleepAction 과 동일하게 침대 footprint cell (옆이 아니라 위) 을 target.
                    if (!movement.IsMoving)
                        movement.SetTarget(
                            AI.GoSleepAction.BedStandPos(needs.AutoRestTarget, transform.position));
                    lastDecision = Time.timeSinceLevelLoad;
                    return;
                }
            }

            // work-decision 주기(1.5s)가 아직이면 → 그 사이 프레임엔 떠도는중 pacing 만.
            //  운영자 2026-06-02: 작업 없는 림은 idleAnchor 주변 근처 타일을 idleStepInterval
            //  (0.8s)마다 왕복 → 1.5s 대기 동안 가만히 서 있지 않고 또박또박 왔다갔다.
            //  decision 주기 프레임(아래)에서는 work 탐지/Decide 가 정상 수행돼 일감을 잡으므로
            //  pacing 이 일감 획득을 굶기지 않는다.  이동 중(hop 수행 중)엔 새 hop 안 함.
            if (Time.timeSinceLevelLoad - lastDecision < decisionInterval)
            {
                if (HasRealActivity()) hasIdleAnchor = false;
                else if (movement != null && !movement.IsMoving
                         && Time.timeSinceLevelLoad - lastIdleStep >= idleStepInterval)
                {
                    IssueWanderHop();
                    lastIdleStep = Time.timeSinceLevelLoad;
                }
                return;
            }

            if (needs != null && needs.IsSleeping)
            {
                movement.ClearTarget();
                chopper.ClearTask();
                if (gatherer != null) gatherer.ClearTask();
                if (hunter != null) hunter.ClearTask();
                if (hauler != null) hauler.ClearTask();
                if (builder != null) builder.ClearTask();
                if (miner != null) miner.ClearTask();
                if (doctor != null) doctor.ClearTask();
                lastDecision = Time.timeSinceLevelLoad;
                return;
            }
            if (needs != null && needs.IsBreaking)
            {
                chopper.ClearTask();
                if (gatherer != null) gatherer.ClearTask();
                if (hunter != null) hunter.ClearTask();
                if (hauler != null) hauler.ClearTask();
                if (builder != null) builder.ClearTask();
                if (miner != null) miner.ClearTask();
                if (doctor != null) doctor.ClearTask();
                if (!movement.IsMoving)
                {
                    Vector2 cur = transform.position;
                    movement.SetTarget(cur + Random.insideUnitCircle * idleWanderRadius);
                }
                lastDecision = Time.timeSinceLevelLoad;
                return;
            }

            // 자율 취침은 생존 우선 — 진행 중인 work 가 있어도 졸리고 밤이면 중단하고
            //  침대로.  busy-gate 보다 먼저: 현재 task 정리 후 Decide 로 GoSleep 시도.
            //  (work 가 없으면 어차피 아래 gate 를 통과해 Decide 가 GoSleep 을 잡는다.)
            if (needs != null && reservedSleepBed == null
                && needs.WantsAutoSleep && !needs.HasRestOrder
                && ctx != null && ctx.HasActiveTask())
            {
                chopper.ClearTask();
                if (gatherer != null) gatherer.ClearTask();
                if (hunter != null) hunter.ClearTask();
                if (cook != null) cook.ClearTask();
                if (hauler != null) hauler.ClearTask();
                if (builder != null) builder.ClearTask();
                if (miner != null) miner.ClearTask();
                if (doctor != null) doctor.ClearTask();
                lastDecision = Time.timeSinceLevelLoad;
                Decide();
                return;
            }

            if (movement.IsMoving || chopper.HasTask) return;
            if (gatherer != null && gatherer.HasTask) return;
            if (hunter != null && hunter.HasTask) return;
            if (cook != null && cook.HasTask) return;
            if (hauler != null && hauler.HasTask) return;
            if (builder != null && builder.HasTask) return;
            if (miner != null && miner.HasTask) return;
            if (doctor != null && doctor.HasTask) return;

            // #126 → 운영자 fb fix: Schedule slot 이 work 를 hard-block 하면
            //  startHour=6 (Sleep) 일 때 nothing 하는 회귀 발생.
            //  Schedule 은 UI 표시 + hint 만, work 막지 X.
            //  실제 휴식은 needs.IsSleeping (sleep<30 && night) 가 처리.

            lastDecision = Time.timeSinceLevelLoad;
            Decide();
        }

        private void Decide()
        {
            // 생존 pre-pass — 자율 취침은 work-priority loop 보다 먼저, work settings 와
            //  무관하게 시도 (졸리고 밤이면 일을 멈추고 침대로).  TryStart 가 true 면
            //  needs.SetAutoSleepTarget + 침대 예약이 끝난 상태 → 예약 침대 추적.
            if (goSleep != null && needs != null && reservedSleepBed == null
                && needs.WantsAutoSleep && !needs.HasRestOrder)
            {
                if (goSleep.TryStart(ctx))
                {
                    reservedSleepBed = needs.AutoRestTarget;
                    return;
                }
                // 빈 침대 없음/도달불가 → 제자리 취침은 PawnNeeds(sleep<30 && night) 가 처리.
            }

            // R5: Strategy pattern — priority list 순회.  첫 TryStart 가 true 반환 시 종료.
            // #114: PawnWorkSettings 가 disable 한 work 는 skip.  priority 1(highest) 부터.
            //  순서: 베리채집(Gather) → 사냥(Hunt) → 요리(Cook) → 벌목(Chop) → 어슬렁(fallback)
            //  새 action 추가 = AI/PawnActions.cs 에 class + Awake actions 리스트에 등록
            if (workSettings == null)
            {
                foreach (var action in actions)
                    if (action.TryStart(ctx)) return;
                return;
            }
            // priority 1 → 2 → 3 → 4 순서로 시도.  0 (disabled) 은 skip.
            for (int p = 1; p <= 4; p++)
            {
                foreach (var action in actions)
                {
                    if (workSettings.GetPriority(action.Kind) != p) continue;
                    if (action.TryStart(ctx)) return;
                }
            }
        }

        // 떠도는중 판정: 실제 작업 task / 생존 필요 / 징집·수동조작 중이면 "활동 중"
        //  → pacing 안 함 + anchor 해제.  하나도 없으면 idle → 근처 타일 왕복 대상.
        private bool HasRealActivity()
        {
            if (entity != null && (entity.IsDrafted || entity.IsDead || entity.IsUnderManualControl))
                return true;
            // NOTE: WantsAutoSleep 는 제외 — 침대가 없어 자려 해도 못 가는 림은 실제론
            //  서성이는 "떠도는중"(라벨도 동일).  침대가 있으면 위쪽 autosleep 블록이 먼저
            //  return 하므로 여기 도달 안 함 → 침대 가는 림을 배회시키지 않는다.
            //  HasRestOrder 도 위 rest 블록(146)이 먼저 return → 여기선 무관.
            if (needs != null && (needs.IsSleeping || needs.IsBreaking || needs.IsEating
                || needs.IsForcedResting))
                return true;
            if (chopper != null && chopper.HasTask) return true;
            if (gatherer != null && gatherer.HasTask) return true;
            if (hunter != null && hunter.HasTask) return true;
            if (cook != null && cook.HasTask) return true;
            if (hauler != null && hauler.HasTask) return true;
            if (harvester != null && harvester.HasTask) return true;
            if (builder != null && builder.HasTask) return true;
            if (miner != null && miner.HasTask) return true;
            if (doctor != null && doctor.HasTask) return true;
            return false;
        }

        // idleAnchor 주변 근처 타일(반경 idleWanderRadius) 하나로 hop.  tile-center 스냅
        //  → 격자 위를 또박또박 오가는 "왔다갔다" 느낌.  anchor 를 기준으로 픽하므로
        //  멀리 표류하지 않고 한 구역을 왕복한다.  movement 가 clamp/blocked 처리.
        private void IssueWanderHop()
        {
            if (movement == null) return;
            if (!hasIdleAnchor) { idleAnchor = transform.position; hasIdleAnchor = true; }
            Vector2 cur = transform.position;
            Vector2 curTile = new Vector2(Mathf.Floor(cur.x) + 0.5f, Mathf.Floor(cur.y) + 0.5f);
            Vector2 raw = idleAnchor + Random.insideUnitCircle * idleWanderRadius;
            Vector2 tile = new Vector2(Mathf.Floor(raw.x) + 0.5f, Mathf.Floor(raw.y) + 0.5f);
            // 최소 1칸 이동 보장(운영자: "2초에 1칸"): 현재 타일과 같으면 인접 칸으로 민다.
            if (Mathf.Approximately(tile.x, curTile.x) && Mathf.Approximately(tile.y, curTile.y))
            {
                Vector2[] card = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };
                tile = curTile + card[Random.Range(0, 4)];
            }
            movement.SetTarget(tile);
        }

        // 현재 active worker 의 작업 대상 월드 좌표 — 머리방향(PawnFacing)·작업 스윙
        //  (PawnPoseDriver) 연출의 단일 출처(운영자 2026-06-02 "일하는 게 보이게").
        //  우선순위는 라벨과 동일하게 채집계열보다 건설·채취를 앞에 둔다.  읽기 전용.
        public bool TryGetWorkTargetPos(out Vector3 pos)
        {
            pos = default;
            if (builder != null && builder.HasTask && builder.Target != null)
            { pos = builder.Target.transform.position; return true; }
            if (chopper != null && chopper.HasTask && chopper.Target != null)
            { pos = chopper.Target.transform.position; return true; }
            if (miner != null && miner.HasTask && miner.Target != null)
            { pos = miner.Target.transform.position; return true; }
            if (harvester != null && harvester.HasTask && harvester.Target != null)
            { pos = harvester.Target.transform.position; return true; }
            if (gatherer != null && gatherer.HasTask && gatherer.Target != null)
            { pos = gatherer.Target.transform.position; return true; }
            if (hunter != null && hunter.HasTask && hunter.Target != null)
            { pos = hunter.Target.transform.position; return true; }
            if (cook != null && cook.HasTask && cook.Target != null)
            { pos = cook.Target.transform.position; return true; }
            if (doctor != null && doctor.HasTask && doctor.Target != null)
            { pos = doctor.Target.transform.position; return true; }
            return false;
        }

        // R5: FindNearestStove/Animal/Bush/Tree moved to AI/PawnActions.cs (각 action 내부)

        // Day 48: drafted pawn 전투 처리.  manual move target는 이미
        //  ClickSelector 가 PawnMovement에 박았음.  여기선 attack/hunt
        //  target 가 있으면 추격 + 공격.
        private void HandleDraftedCombat()
        {
            if (entity == null) return;
            BanditEnemy bandit = entity.DraftedAttackTarget;
            AnimalEntity animal = entity.DraftedHuntTarget;
            WolfEnemy wolf = entity.DraftedWolfTarget;
            // Clean up dead/null targets
            if (bandit != null && bandit.IsDead) { entity.DraftedAttackTarget = null; bandit = null; }
            if (wolf != null && wolf.IsDead) { entity.DraftedWolfTarget = null; wolf = null; }
            if (animal != null && animal.gameObject == null)
            { entity.DraftedHuntTarget = null; animal = null; }
            if (bandit == null && animal == null && wolf == null) return;
            Vector2 me = transform.position;
            const float attackRange = 1.2f;
            // Day 50: 활 연구 완료 + arrow sprite 존재 시 ranged 시도 (melee보다 우선)
            bool canShoot = arrowSprite != null
                            && ResearchManager.Instance != null
                            && ResearchManager.Instance.IsUnlocked("simple_bow");
            Vector2 targetPos;
            bool inRange;
            if (canShoot)
            {
                Vector2 rPos = Vector2.zero; bool haveTarget = false; int rDmg = 4;
                if (bandit != null) { rPos = bandit.transform.position; haveTarget = true; rDmg = 4; }
                else if (wolf != null) { rPos = wolf.transform.position; haveTarget = true; rDmg = 5; }
                else if (animal != null) { rPos = animal.transform.position; haveTarget = true; rDmg = 3; }
                if (haveTarget)
                {
                    float d = Vector2.Distance(me, rPos);
                    if (d > attackRange && d <= RangedAttackRange)
                    {
                        movement.ClearTarget();
                        if (Time.time - lastRangedAttackTime > RangedAttackInterval)
                        {
                            lastRangedAttackTime = Time.time;
                            Vector2 dir = (rPos - me).normalized;
                            ArrowProjectile.SpawnArrow(new Vector3(me.x, me.y, 0f), dir, rDmg, gameObject, arrowSprite);
                            var skills = GetComponent<PawnSkills>();
                            if (skills != null) skills.AddXP(SkillKind.Combat, 12f);
                        }
                        return;
                    }
                }
            }
            if (bandit != null)
            {
                targetPos = bandit.transform.position;
                inRange = Vector2.Distance(me, targetPos) <= attackRange;
                if (!inRange) movement.SetTarget(targetPos);
                else
                {
                    movement.ClearTarget();
                    if (Time.time - lastDraftAttackTime > DraftAttackInterval)
                    {
                        lastDraftAttackTime = Time.time;
                        // #173 - 무기 dmg, #175 - meleeMul + Combat skill 가산.
                        var equip = GetComponent<PawnEquipment>();
                        var abil = GetComponent<PawnAbilities>();
                        var skills = GetComponent<PawnSkills>();
                        float wpn = equip != null ? equip.TotalMeleeDamageBonus() : 0f;
                        float ml = abil != null ? abil.meleeMul : 1f;
                        // Combat skill 1당 +3% 데미지 (lvl 10 = +30%).
                        float sk = skills != null ? (1f + skills.GetLevel(SkillKind.Combat) * 0.03f) : 1f;
                        int dmg = Mathf.Max(1, Mathf.RoundToInt((2f + wpn) * ml * sk));
                        bandit.TakeDamage(dmg, gameObject);
                        GetComponent<PawnEntity>()?.RegisterMeleeHit(targetPos, melee: true);  // #276 lunge 시각(auto만 호출하던 누락)
                        if (skills != null) skills.AddXP(SkillKind.Combat, 8f);
                    }
                }
            }
            else if (wolf != null)
            {
                targetPos = wolf.transform.position;
                inRange = Vector2.Distance(me, targetPos) <= attackRange;
                if (!inRange) movement.SetTarget(targetPos);
                else
                {
                    movement.ClearTarget();
                    if (Time.time - lastDraftAttackTime > DraftAttackInterval)
                    {
                        lastDraftAttackTime = Time.time;
                        // #173/#175 - 무기 + meleeMul + Combat skill 가산
                        var equip = GetComponent<PawnEquipment>();
                        var abil = GetComponent<PawnAbilities>();
                        var skills = GetComponent<PawnSkills>();
                        float wpn = equip != null ? equip.TotalMeleeDamageBonus() : 0f;
                        float ml = abil != null ? abil.meleeMul : 1f;
                        float sk = skills != null ? (1f + skills.GetLevel(SkillKind.Combat) * 0.03f) : 1f;
                        int dmg = Mathf.Max(1, Mathf.RoundToInt((3f + wpn) * ml * sk));
                        wolf.TakeDamage(dmg, gameObject);
                        GetComponent<PawnEntity>()?.RegisterMeleeHit(targetPos, melee: true);  // #276 lunge 시각
                        if (skills != null) skills.AddXP(SkillKind.Combat, 10f);
                    }
                }
            }
            else if (animal != null)
            {
                targetPos = animal.transform.position;
                inRange = Vector2.Distance(me, targetPos) <= attackRange;
                if (!inRange) movement.SetTarget(targetPos);
                else
                {
                    movement.ClearTarget();
                    if (Time.time - lastDraftAttackTime > DraftAttackInterval)
                    {
                        lastDraftAttackTime = Time.time;
                        animal.TakeDamage(2);
                        GetComponent<PawnEntity>()?.RegisterMeleeHit(targetPos, melee: true);  // #276 lunge 시각
                        var skills = GetComponent<PawnSkills>();
                        if (skills != null) skills.AddXP(SkillKind.Combat, 5f);
                    }
                }
            }
        }
    }
}
