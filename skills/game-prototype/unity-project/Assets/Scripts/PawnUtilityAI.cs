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
        // 운영자 피드백 #1 (2026-06-12 AM): '떠도는중이 지금보다 느리게' — 유휴 배회가
        //  작업 이동과 비슷한 존재감이라 부산스러움.  홉 빈도 1.5→2.5s.
        private float idleStepInterval = 2.5f;
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
        private PawnWeaponsmith smith;   // 2026-07-30 무기 제작
        private PawnHauler hauler;  // #116 — wood pile pickup
        private PawnHarvester harvester;  // #202 — ripe crop harvest
        private PawnBuilder builder;  // #118 — blueprint 건설
        private PawnMiner miner;      // #119 — 채광
        private PawnDoctor doctor;    // #125 — 의료
        private PawnBurier burier;    // GDD G3 — 매장
        private PawnSchedule schedule; // #126 — 시간대별 행동
        private PawnNeeds needs;
        private PawnEntity entity;  // Day 48 — drafted state check
        private PawnHealth health;  // #버그헌트(2026-06-04) — 다운(의식불명) 게이트용
        private PawnWorkSettings workSettings;  // #114 — per-pawn work priority
        private float lastDecision = -999f;
        private float lastDraftAttackTime = -999f;
        private const float DraftAttackInterval = 0.8f;
        // Day 50: bow ranged attack
        [SerializeField] private Sprite arrowSprite;
        // 밸런스 A2(2026-07-24): 단궁 파리티 — 사거리 5→10타일, 간격 1.5→3.0s.
        private const float RangedAttackRange = 10.0f;
        private const float RangedAttackInterval = 3.0f;
        private float lastRangedAttackTime = -999f;
        public void SetArrowSprite(Sprite s) { arrowSprite = s; }

        // R5: Strategy pattern — Decide() priority list + reusable context
        private PawnContext ctx;
        private List<IPawnAction> actions;
        // 자율 취침: 생존 행동이라 work-priority loop 보다 먼저 시도 (work settings 무관).
        private GoSleepAction goSleep;
        private RestWhenBleedingAction restWhenBleeding;
        private EatBerryAction eatSurvival;
        // 자율 취침으로 예약한 침대 — 기상/취소 시 ReservationManager 에서 해제하기 위해 추적.
        private BedEntity reservedSleepBed;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
            chopper = GetComponent<PawnChopper>();
            gatherer = GetComponent<PawnGatherer>();
            hunter = GetComponent<PawnHunter>();
            cook = GetComponent<PawnCook>();
            // 2026-07-30 — 무기 제작 워커.  프리팹에 없어도 붙게 한다(씬 재베이크 불필요).
            smith = GetComponent<PawnWeaponsmith>() ?? gameObject.AddComponent<PawnWeaponsmith>();
            hauler = GetComponent<PawnHauler>();  // #116
            harvester = GetComponent<PawnHarvester>();  // #202
            builder = GetComponent<PawnBuilder>();  // #118
            miner = GetComponent<PawnMiner>();      // #119
            doctor = GetComponent<PawnDoctor>();    // #125
            burier = GetComponent<PawnBurier>();    // GDD G3 — 매장
            schedule = GetComponent<PawnSchedule>();// #126
            needs = GetComponent<PawnNeeds>();
            entity = GetComponent<PawnEntity>();
            health = GetComponent<PawnHealth>();  // #버그헌트(2026-06-04)
            workSettings = GetComponent<PawnWorkSettings>();  // #114
            // R5: ctx + action priority list
            ctx = new PawnContext
            {
                entity = entity, movement = movement, chopper = chopper,
                gatherer = gatherer, hunter = hunter, cook = cook,
                smith = smith,
                hauler = hauler,
                harvester = harvester,  // #202
                builder = builder,
                miner = miner,
                doctor = doctor,
                burier = burier,   // GDD G3
                needs = needs, skills = GetComponent<PawnSkills>(),
                transform = transform,
                idleWanderRadius = idleWanderRadius,
            };
            goSleep = new GoSleepAction();
            restWhenBleeding = new RestWhenBleedingAction();   // TOP-10 환자 행동
            eatSurvival = new EatBerryAction { foodThreshold = 25f };   // 퀵픽 기아 게이트 해제
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
                // 2026-07-30 — 무기 제작.  청사진 건설 **아래**: 집이 없는데 무기를
                //  먼저 만들면 곤란하고, 습격은 4~6일차라 급하지 않다.
                new CraftWeaponAction(),
                new BuildRoofAction(),       // 소크 r2 #1 - 지붕 노동 시공 (골조 다음)
                // ── HAUL loose ground piles FIRST (above raw extraction) ──
                //  운영자 fb #I4-regress: 떨어진 더미가 적립 전에 부패(옥외 30s/-1)하지
                //  않도록, 줍을 더미가 있으면 새 채집/벌목보다 운반을 먼저 한다.  세 종류를
                //  나란히 둬서 wood/stone/meat 어느 것이든 바닥에 있으면 즉시 stockpile 로
                //  운반→카운터 적립(물리 운반 유지, 순간이동 아님).
                new BuryCorpseAction(),   // GDD G3 - 시신 매장 (Haul 최상단 — 시체 > 더미)
                new HaulWoodAction(),     // #116 - 벌목 후 떨어진 wood pile 운반
                new HaulStoneAction(),    // #119 - 채광 후 떨어진 stone chunk 운반
                new HaulMeatAction(),     // #129 - 사냥 후 떨어진 meat pile 운반
                // ── #작업배정-단일화(2026-06-03) CRITICAL 회귀 수정 ──
                //  #230 에선 ChopTree/MineStone 을 auto-loop 에서 빼고 TreeChopDesignation/
                //  MineDesignation 의 DispatchToIdleX 가 마킹된 것만 idle 림에 배정했다.  오늘
                //  단일화로 그 별도 dispatch 를 retire 하면서 ChopTreeAction/MineStoneAction 을
                //  FindNearestTree/Vein 에서 IsMarked 게이트(지정된 것만)로 바꿨다 — 그런데 이
                //  actions 리스트에 추가하는 것을 빠뜨려, 지정해도 아무도 자율 벌목/채광을 안 하는
                //  CRITICAL 회귀가 났다.  이제 리스트에 추가해 단일 경로를 완성한다(여전히 '지정된'
                //  나무/광맥만 — IsMarked 게이트 덕에 '아무거나 자동 채집' 회귀는 없음).
                new ChopTreeAction(),     // 지정(마킹)된 나무만 자율 벌목 (IsMarked 게이트)
                new MineStoneAction(),    // 지정(마킹)된 광맥만 자율 채광 (IsMarked 게이트)
                // 연구 디스패치 (2026-06-12) — 최하단: 다른 일감이 없을 때만 벤치 옆에
                //  머물며 연구.  직업 탭 '연구' 우선순위가 처음으로 실효를 가진다.
                new DoResearchAction(),
                //  운반·요리·수확·사냥(생존)·경작 zone 은 the reference sim 처럼 자동 유지.
                // 운영자 2026-06-02: idle 배회는 리스트의 WanderAction(1.5s 간격, 긴 정지)
                //  대신 Update 의 anchored pacing(idleStepInterval 0.8s, 근처 타일 왕복)이
                //  전담한다.  WanderAction 이 리스트에 있으면 decision 프레임마다 폰을 멀리
                //  움직여 pacing 의 '정지 상태' 조건을 굶겼다(IdleHop 0회 버그) → 제거.
                //  (WanderAction 클래스 자체는 TestV41_WanderAction 이 직접 쓰므로 유지.)
            };
        }

        /// <summary>#회귀가드 — Decide actions 리스트에 특정 action 타입이 등록됐는지(테스트용).
        /// 오늘 ChopTreeAction/MineStoneAction 을 리스트에 빠뜨려 '지정해도 아무도 안 벰'
        /// CRITICAL 회귀가 났는데 기존 테스트(TryStart 직접 호출)가 못 잡았다 → 이 조회로 가드.</summary>
        public bool HasRegisteredAction(System.Type actionType)
            => actions != null && actionType != null && actions.Exists(a => a.GetType() == actionType);

        private void Update()
        {
            // Day 48: drafted pawn skip utility AI — manual control only.
            if (entity != null && entity.IsDrafted)
            {
                // 회귀 fix(2026-06-02): 징집/수동 이동도 정상 속도여야 함.  MoveSpeedScale 을
                //  pacing 블록(아래)에서만 set 하면 여기서 early-return 돼 idle 의 0.5 가 끼인다.
                if (movement != null) movement.MoveSpeedScale = 1f;
                HandleDraftedCombat();
                return;
            }
            // 운영자 피드백: 우클릭 이동이 AI 에 즉시 override 됐던 문제 fix.
            //  ClickSelector 가 ManualMoveUntil 을 설정하면 그 동안 AI Decide skip(사용자 이동 존중).
            if (entity != null && entity.IsUnderManualControl)
            {
                // 회귀 fix: 수동 이동(우클릭 이동/벌목 지정)은 항상 정상 속도.  (이전: idle 의
                //  0.5 가 여기 early-return 으로 안 풀려 수동 이동이 절반 속도로 기어가던 버그.)
                if (movement != null) movement.MoveSpeedScale = 1f;
                return;
            }

            // #버그헌트(2026-06-04): 다운(의식불명) 상태면 모든 행동 정지.  설계(PawnHealth 주석
            //  'head HP<30% → downed (consciousness loss)')대로 의식 잃은 폰은 이동·작업·전투를
            //  멈춰야 한다.  이전엔 IsDowned 게이트가 없어 다운된 폰이 계속 걷고 일하고 적을
            //  자동공격했다(누운 tint 인데 실제로는 활동 → 의사가 환자를 못 따라잡는 문제도 유발).
            //  회복(치료로 머리 HP>30%)되면 IsDowned=false 가 돼 자동 재개.
            if (health != null && health.IsDowned)
            {
                chopper?.ClearTask();
                gatherer?.ClearTask(); hunter?.ClearTask(); cook?.ClearTask();
                hauler?.ClearTask(); builder?.ClearTask(); miner?.ClearTask();
                doctor?.ClearTask(); harvester?.ClearTask();
                movement?.ClearTarget();
                return;
            }

            // MoveSpeedScale 단일 출처: 작업/필요 활동 중 = 1.0, 떠도는중(idle) = 0.5.
            //  매 프레임 계산해 decision 윈도우 밖에서도 작업 이동이 0.5 로 끼이지 않게 한다.
            // 백로그 #8 (2026-06-11): 저수면(<25)이면 휘청거린다 — 수면이 처음으로
            //  '실패 가능한' 수치가 된다 (페널티 훅 0개였음).
            float drowsy = (needs != null && needs.sleep < 25f) ? 0.5f : 1f;
            // 운영자 피드백 #1 (2026-06-12 AM): idle 0.5 → 0.35 — 어슬렁은 어슬렁답게.
            if (movement != null) movement.MoveSpeedScale = (HasRealActivity() ? 1f : 0.35f) * drowsy;

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
                // #침대도달불가(2026-06-11, 운영자 보고) — HasTask 게이트 필수: 이 블록은
                //  매 프레임 도는데 워커 ClearTask 는 task 가 없어도 movement.ClearTarget()
                //  을 불러 A* 경로를 매 프레임 파괴했다.  경로 인덱스가 0 으로 리셋돼 림이
                //  자기 칸 중심까지만 표류 후 영구 동결 → 12s timeout → '제자리 취침' 루프.
                if (chopper.HasTask) chopper.ClearTask();
                if (gatherer != null && gatherer.HasTask) gatherer.ClearTask();
                if (hunter != null && hunter.HasTask) hunter.ClearTask();
                if (cook != null && cook.HasTask) cook.ClearTask();
                if (hauler != null && hauler.HasTask) hauler.ClearTask();
                if (builder != null && builder.HasTask) builder.ClearTask();
                if (miner != null && miner.HasTask) miner.ClearTask();
                if (doctor != null && doctor.HasTask) doctor.ClearTask();
                if (harvester != null && harvester.HasTask) harvester.ClearTask();  // 감사 rank1
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
                    // #침대도달불가 — 위 HasRestOrder 블록과 동일한 HasTask 게이트 (매 프레임
                    //  무조건 ClearTask → ClearTarget 이 A* 경로를 파괴하던 동결 버그의 본체).
                    if (chopper.HasTask) chopper.ClearTask();
                    if (gatherer != null && gatherer.HasTask) gatherer.ClearTask();
                    if (hunter != null && hunter.HasTask) hunter.ClearTask();
                    if (cook != null && cook.HasTask) cook.ClearTask();
                    if (hauler != null && hauler.HasTask) hauler.ClearTask();
                    if (builder != null && builder.HasTask) builder.ClearTask();
                    if (miner != null && miner.HasTask) miner.ClearTask();
                    if (doctor != null && doctor.HasTask) doctor.ClearTask();
                    if (harvester != null && harvester.HasTask) harvester.ClearTask();  // 감사 rank1
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
                // MoveSpeedScale 은 위 단일 출처에서 이미 설정됨(작업1.0/idle0.5).
                if (HasRealActivity()) hasIdleAnchor = false;
                else if (movement != null && !movement.IsMoving
                         && Time.timeSinceLevelLoad - lastIdleStep >= idleStepInterval)
                {
                    IssueWanderHop();
                    lastIdleStep = Time.timeSinceLevelLoad;
                    // A1 — 모닥불 2.5셀 내 idle 체류 = '모닥불 곁' +2 환류.  JoyAction 주석에만
                    //  있고 미배선이던 dead feature 배선.  동일 라벨은 갱신이라 스팸 없음.
                    var fireNear = JoyAction.FindCampfire();
                    if (fireNear != null && ((Vector2)transform.position
                            - (Vector2)fireNear.transform.position).sqrMagnitude <= 6.25f)
                        GetComponent<PawnThoughts>()?.AddThought("모닥불 곁", +2f, 240f);
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
                if (harvester != null) harvester.ClearTask();  // 감사 rank1: 누락→작물 예약 영구점유 fix
                if (cook != null) cook.ClearTask();  // #회귀가드: cook 누락→수면 중 화덕 예약 점유
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
                if (harvester != null) harvester.ClearTask();  // 감사 rank1: 누락→작물 예약 영구점유 fix
                if (cook != null) cook.ClearTask();  // #회귀가드: cook 누락→정신붕괴 중 화덕 예약 점유
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
            // #thrash-fix(2026-06-10) 운영자 P0 "벌목-떠도는중 번갈이/림 작업마비" 근본원인:
            //  탈진 림이 이 게이트에서 1.5s 마다 전 작업 해제 → Decide → GoSleepAction 이
            //  침대 없음으로 실패 → 다시 작업 배정 → 무한 thrash (재현 p0-remote-chop:
            //  '벌목 시작 → ClearTask by=PawnUtilityAI.Update' 루프, 나무 44→44).
            //  → 침대를 실제로 얻을 수 있을 때만 작업을 중단한다.  침대가 없으면 하던 일
            //  계속 (탈진 제자리취침 fallback 은 PawnNeeds 가 밤에 처리).
            // 갭 TOP-7 ※ — 수동 task 소진 시 보호 해제 (완료가 보호 단위).
            if (entity != null && entity.HasManualTask
                && (ctx == null || !ctx.HasActiveTask()))
                entity.HasManualTask = false;
            if (needs != null && reservedSleepBed == null
                && needs.WantsAutoSleep && !needs.HasRestOrder
                && ctx != null && ctx.HasActiveTask()
                // TOP-7 — 수동 배정 task 는 탈진(<20) 전까지 취침이 끊지 않는다
                //  ('시킨 일 하다 말고 잔다' 수정).  탈진이면 생존 우선 그대로.
                && !(entity != null && entity.HasManualTask && needs.sleep >= 20f)
                && ctx.FindNearestFreeBed() != null)
            {
                chopper.ClearTask();
                if (gatherer != null) gatherer.ClearTask();
                if (hunter != null) hunter.ClearTask();
                if (cook != null) cook.ClearTask();
                if (hauler != null) hauler.ClearTask();
                if (builder != null) builder.ClearTask();
                if (miner != null) miner.ClearTask();
                if (doctor != null) doctor.ClearTask();
                if (harvester != null) harvester.ClearTask();  // 감사 rank1: 누락→작물 예약 영구점유 fix
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
            if (harvester != null && harvester.HasTask) return;  // 감사 rank2: 누락→수확 중 우왕좌왕 fix

            // #126 → 운영자 fb fix: Schedule slot 이 work 를 hard-block 하면
            //  startHour=6 (Sleep) 일 때 nothing 하는 회귀 발생.
            //  Schedule 은 UI 표시 + hint 만, work 막지 X.
            //  실제 휴식은 needs.IsSleeping (sleep<30 && night) 가 처리.

            lastDecision = Time.timeSinceLevelLoad;
            Decide();
        }

        private void Decide()
        {
            // 생존 pre-pass(0) — 퀵픽 '기아 채집 게이트 해제' (2026-06-13): 채집
            //  priority 0 인 림도 굶으면(food<25) 직업 설정 무관하게 먹는다 —
            //  생존 > 직업 설정 (spec §6 위반 잔재 청산).
            if (needs != null && needs.food < 25f && eatSurvival != null
                && eatSurvival.TryStart(ctx)) return;
            // 생존 pre-pass(2) — TOP-10 환자 행동: 출혈이면 침대로 가 치료 대기.
            //  취침보다 먼저 (출혈이 졸림보다 긴급).
            if (restWhenBleeding != null && needs != null && !needs.HasRestOrder
                && restWhenBleeding.TryStart(ctx)) return;
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
            //  서성이는 "떠도는중"(라벨도 동일).
            //  단 HasAutoSleepOrder(침대로 걷는 중)는 포함해야 한다 — MoveSpeedScale 설정은
            //  Update 최상단(autosleep 블록 return 보다 먼저)에서 매 프레임 일어나므로,
            //  여기서 빠지면 침대 걷기가 배회 속도(0.5x)로 기어가 12s 도달 timeout 을
            //  상습 초과한다 (#침대도달불가 2026-06-11 잔존 원인 2/2).
            if (needs != null && (needs.IsSleeping || needs.IsBreaking || needs.IsEating
                || needs.IsForcedResting || needs.HasAutoSleepOrder))
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
            if (!hasIdleAnchor)
            {
                idleAnchor = transform.position;
                // A1 생산적 idle (2026-07-24 운영자 "자동플레이가 멍청함"): 일감 없는 림은
                //  제자리 배회 대신 모닥불 곁에 모여 쉰다 — Joy 슬롯 밖 idle 에도 적용.
                //  30셀 밖이면 기존 제자리 배회 유지(맵 반대편까지 강제 귀환은 부자연).
                var fire = JoyAction.FindCampfire();
                if (fire != null)
                {
                    Vector2 c = fire.transform.position;
                    if (((Vector2)transform.position - c).sqrMagnitude < 900f)
                        idleAnchor = PawnMovement.ClampToWorld(
                            c + Random.insideUnitCircle.normalized * Random.Range(1.2f, 2.2f));
                }
                hasIdleAnchor = true;
            }
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
            // #버그헌트4(2026-06-05): 원거리 공격은 활/석궁 '장착' 시에만.  이전엔 연구(simple_bow)+
            //  arrowSprite 만 보고 맨손/근접무기 폰도 허공에서 화살을 쐈다(HasRangedWeapon/rangedEnabled
            //  가 dead code).  장비 검사 추가로 활 장착 폰만 사격, 나머지는 근접으로.
            var eqShoot = GetComponent<PawnEquipment>();
            bool canShoot = arrowSprite != null
                            && ResearchManager.Instance != null
                            && ResearchManager.Instance.IsUnlocked("simple_bow")
                            && eqShoot != null && eqShoot.HasRangedWeapon();
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
