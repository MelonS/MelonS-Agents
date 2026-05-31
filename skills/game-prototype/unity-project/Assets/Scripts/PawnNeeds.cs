using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Pawn needs (food / sleep / mood).  Day 2 = ticking decay only.
    /// Day 3+ will tie needs to actions (chop, eat, sleep) that restore.
    /// Day 4+ utility AI uses these to pick highest-priority action.
    /// </summary>
    public class PawnNeeds : MonoBehaviour
    {
        [Header("Need values (0-100)")]
        [Range(0f, 100f)] public float food = 80f;
        [Range(0f, 100f)] public float sleep = 80f;
        [Range(0f, 100f)] public float mood = 80f;

        [Header("Decay rates (units per second)")]
        // #200 RimWorld fidelity: food (0-100) should empty over ~2.5-3 in-game
        //  days like RimWorld (hunger ~1.6 nutrition/day).  1 in-game day = 240
        //  real seconds (GameClock), so a 3-day budget = 100 / (3*240) = 0.139/s.
        //  Was 0.5/s → emptied in 200s = 0.83 day (~3.5x too fast).  0.14 ≈ 2.97 days.
        // #228 운영자 fb "배고픔·수면 게이지가 정상동작 안 하는 느낌" — 시계 fix(rate 6,
        //  하루=240s) 후 needs(실시간 decay)가 하루 주기와 분리돼 게이지가 며칠에 걸쳐 찔끔
        //  움직였다(food~3일/sleep~1.4일).  하루 주기로 재튜닝: 매일 먹고/밤마다 자도록.
        //  food 0.14→0.2(eat≈1회/일), sleep 0.3→0.4(sleep 100→0 ≈ 1게임일 → 밤마다 졸림).
        //  느슨한 생존 방향이라 죽을 만큼은 아니고 '가시적 일일 리듬' 목적.
        [SerializeField] private float foodDecay = 0.2f;
        [SerializeField] private float sleepDecay = 0.4f;
        [SerializeField] private float moodDecay = 0.2f;

        [Header("Day 9+: sleep regen when sleeping at night")]
        [SerializeField] private float sleepRegenAtNight = 8f;

        [Header("Day 11: eat-from-stockpile")]
        // #202 SURVIVAL-LOOP FIX — eat at 50 (was 40).  With the harvest loop now
        //  feeding the cook→meal supply, pawns can afford to eat sooner; a higher
        //  threshold narrows the food sawtooth (band ~50-80 instead of ~45-80),
        //  giving more starvation margin AND keeping the colony's food trend STABLE
        //  rather than swinging wide enough that quarter-window sampling reads a
        //  phantom decline.  RimWorld pawns eat around the "Hungry" (~ half) mark.
        [SerializeField] private float eatThreshold = 50f;
        [SerializeField] private float eatRestore = 30f;

        // ── #214 운영자 fb: "아이템이 먹거나 하면 저장/건축공간으로 뿅 이동" ──────────
        //  ROOT CAUSE: 기존 TryEatTick 은 배고프면 ResourceManager 의 meals/food 카운터를
        //  그 자리에서 즉시 차감(= 음식이 림에게 순간이동/추상 섭취)했다.  림이 음식이 있는
        //  곳으로 걸어가는 행동이 전혀 없었다.  이건 운영자가 말한 "뿅 이동" 그 자체.
        //  FIX (물리 섭취): 배고프면 가장 가까운 *물리적 음식원* 으로 걸어가서 도착해야만
        //  먹는다.  음식원 우선순위:
        //    1) 바닥/저장고의 MeatPileEntity (진짜 entity — 줍어서 먹음, 카운터 동기 차감)
        //    2) 식량을 담는 StockpileZoneEntity (조리된 meal/food 카운터의 물리적 보관 위치
        //       — meal 은 entity 가 없으므로 저장고 cell 이 그 "장소" 역할.  도착해야 섭취)
        //    3) 익은 BerryBushEntity (덤불에서 직접 따 먹음)
        //  도착 전에는 카운터/덤불을 절대 건드리지 않는다 = 순간이동 제거.
        //  이동은 PawnMovement(읽기 전용 lane) 의 SetTarget 으로 박고, AI override 를
        //  막기 위해 PawnEntity.ManualMoveUntil 을 매 frame 밀어준다(auto-sleep 과 동일 정신).
        private enum EatState { None, Walking }
        private EatState eatState = EatState.None;
        private MeatPileEntity eatMeatTarget;       // 1) 물리 고기 더미
        private StockpileZoneEntity eatStockTarget; // 2) meal/food 카운터의 보관 장소
        private BerryBushEntity eatBushTarget;      // 3) 익은 베리 덤불
        private Vector2 eatDestWorld;
        private float eatStartTime = -999f;
        [SerializeField] private float eatReachRange = 1.3f;   // 음식원 인접 도달 판정
        [SerializeField] private float eatWalkTimeout = 12f;   // 도달 못 하면 포기(stuck 방지)
        [SerializeField] private float eatRetryCooldown = 4f;  // 포기 후 재시도 쿨다운
        private float eatSuppressUntil = -999f;
        private PawnMovement movement;
        private PawnEntity pawnEntity;
        public bool IsEating => eatState == EatState.Walking;

        public bool IsSleeping { get; private set; }

        // ── rcfix: RimWorld 우클릭 "Rest" / "Sleep" 명령 ─────────────────────────
        //  ClickSelector 가 침대 우클릭 시 SetRestTarget(bed) 호출.  pawn 은
        //  ClickSelector 가 박은 movement target + ManualMoveUntil 로 침대까지 이동.
        //  이 컴포넌트는 매 frame 침대 도착 여부만 검사 (이동은 PawnMovement 담당):
        //    - 아직 침대 위 아님  → 대기 (이동 중)
        //    - 침대 위 도착       → forcedResting=true → 졸리지 않아도/낮에도 잠.
        //  졸음(sleep) 이 wakeSleepLevel 이상으로 회복되면 자동 해제.
        //  사용자가 다른 명령(이동/작업) 내리면 ClearRestTarget 로 즉시 취소.
        //  forcedResting 동안 sleep 회복 = night 자동 수면과 동일 (bed.RestMul 반영).
        private BedEntity restTarget;
        private bool forcedResting = false;
        // #222 QA fix — 유저 rest 명령(restTarget)에도 도달 timeout (자율취침엔 이미 있음).
        //  LongPlay 로그: 민지 '휴식이동' no-move 60s = SetRestTarget 한 침대에 못 닿았는데
        //  restTarget 경로엔 timeout 이 없어 영구 stuck.  도달 못 하면 명령을 풀어 stuck 방지.
        private float restStartTime = -999f;
        [SerializeField] private float restArriveTimeout = 15f;
        [SerializeField] private float forcedWakeSleepLevel = 95f;  // 강제 휴식은 거의 만충까지
        public bool HasRestOrder => restTarget != null;
        public BedEntity RestTarget => restTarget;
        public bool IsForcedResting => forcedResting;

        // ── 자율 취침: 졸리고 밤이면 림이 스스로 침대로 걸어가서 잔다 ─────────────
        //  운영자 fb: "림이 그 자리에서 자고 침대를 안 씀."  기존엔 sleep<30 && night
        //  면 GetBedUnderPawn 으로 마침 발밑이 침대면 보너스, 아니면 제자리 취침뿐.
        //  자율로 침대를 찾아가는 행동이 없었다.  GoSleepAction(IPawnAction) 이
        //  비어있는 BedEntity 를 예약 후 SetAutoSleepTarget 호출 → PawnUtilityAI 가
        //  침대로 이동시킴 → 발밑에 그 침대 도착 시 여기서 IsSleeping=true.
        //  rcfix forcedResting(우클릭 침대) 와 별개의 경로 — 사용자 명령이 항상 우선:
        //   1) restTarget(우클릭)  : 졸음 무관/낮에도 강제 수면, wake @ 95.
        //   2) autoRestTarget(AI) : sleep 낮고 밤일 때만, wake @ 80 (일반 수면).
        //  도달 못 하거나 침대 없으면 기존 제자리 취침(아래 sleep<30 && night) fallback.
        private BedEntity autoRestTarget;
        [SerializeField] private float autoSleepThreshold = 35f;  // 졸음 — 침대로 가기 시작
        [SerializeField] private float autoWakeSleepLevel = 80f;  // 일반 기상 수준
        // ── 도달 timeout (robustness) ─────────────────────────────────────────────
        //  자율 취침으로 침대를 향해 출발한 시각.  이 시간 안에 침대 위에 도착 못 하면
        //  (door 막힘 / 경로 실패 / stand-cell 불일치) 제자리 취침으로 fallback 한다.
        //  운영자가 잡은 회귀: 림이 침대 옆에 멈춰 "휴식이동" 으로 영영 stuck → sleep 0.
        //  이제 어떤 경우에도 sleep 이 0 으로 추락하거나 stuck 되지 않는다.
        private float autoSleepStartTime = -999f;
        [SerializeField] private float autoSleepArriveTimeout = 12f;  // 침대 도달 제한시간
        // 도달 timeout 후 재시도 쿨다운 — 같은 침대로 다시 출발 → 다시 timeout 하는
        //  re-path loop (이것도 stuck 처럼 보임) 방지.  이 동안엔 WantsAutoSleep=false 라
        //  GoSleepAction 이 발동 안 하고 제자리 취침(sleep<30 && night)으로 안정 회복.
        private float autoSleepSuppressUntil = -999f;
        [SerializeField] private float autoSleepRetryCooldown = 20f;
        public bool HasAutoSleepOrder => autoRestTarget != null;
        public BedEntity AutoRestTarget => autoRestTarget;
        /// <summary>밤에 졸려서 자율로 침대를 찾아가야 하는가 (GoSleepAction 의 eligibility).
        ///  도달 실패 직후엔 쿨다운 동안 false (제자리 취침으로 안정 회복하게).</summary>
        // #228 - 평소엔 밤에 졸리면(sleep<35 && 밤) 침대로.  단 ★탈진(sleep<15)이면 낮에도
        //  자러 간다 — 과거엔 밤 게이트 때문에 낮에 sleep 이 20까지 떨어져도 안 자고 게이지가
        //  바닥에 정체돼 "수면 게이지가 정상동작 안 함"처럼 보였다.
        private const float ExhaustedSleepLevel = 15f;
        public bool WantsAutoSleep =>
            (sleep < ExhaustedSleepLevel || (sleep < autoSleepThreshold && IsNightTime()))
            && Time.time >= autoSleepSuppressUntil;

        /// <summary>GoSleepAction 이 빈 침대를 예약한 뒤 호출 — 이 침대로 가서 자라.</summary>
        public void SetAutoSleepTarget(BedEntity bed)
        {
            autoRestTarget = bed;
            autoSleepStartTime = Time.time;  // 도달 timeout 기준 시각 리셋
        }

        /// <summary>자율 취침 취소 (침대 파괴/도달불가/기상).  예약 해제는 호출측 책임.</summary>
        public void ClearAutoSleepTarget()
        {
            autoRestTarget = null;
        }

        /// <summary>ClickSelector 가 침대 우클릭 시 호출 — 이 침대로 가서 쉬라는 명령.</summary>
        public void SetRestTarget(BedEntity bed)
        {
            restTarget = bed;
            forcedResting = false;  // 도착 전까지는 이동 중 (아직 안 잠)
            restStartTime = Time.time;  // #222 도달 timeout 기준
        }

        /// <summary>사용자가 다른 명령을 내리거나 휴식이 끝나면 호출 — 강제 휴식 취소.</summary>
        public void ClearRestTarget()
        {
            restTarget = null;
            forcedResting = false;
        }

        // #164 - PawnTraits 효과 캐시 (Awake 시 traits.moodBaselineBonus 적용).
        private bool traitsApplied = false;

        [Header("Day 20: mood break")]
        [SerializeField] private float moodBreakThreshold = 20f;
        [SerializeField] private float moodBreakRecoverAt = 35f;
        [SerializeField] private float moodBreakDuration = 30f;
        private bool isBreaking = false;
        private float breakUntil = -10f;
        public bool IsBreaking => isBreaking;

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
            pawnEntity = GetComponent<PawnEntity>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool night = IsNightTime();

            // #164 - PawnTraits.moodBaselineBonus 시작 시 mood 에 1회 가산.
            //  Awake 가 traits 보다 먼저 fire 될 수 있어 Update 첫 frame 에서 적용.
            if (!traitsApplied)
            {
                var tr = GetComponent<PawnTraits>();
                if (tr != null)
                {
                    mood = Mathf.Clamp(mood + tr.moodBaselineBonus, 0f, 100f);
                }
                traitsApplied = true;
            }

            // ── rcfix: 강제 휴식 명령 처리 (night/tired 게이트보다 우선) ──────────
            //  사용자가 침대 우클릭 → SetRestTarget.  pawn 이 그 침대 위에 도착하면
            //  졸리지 않아도/낮이어도 눕는다 (RimWorld 우클릭 Rest).
            if (restTarget != null)
            {
                // target 침대가 파괴됐으면 명령 취소.
                if (restTarget == null || restTarget.gameObject == null)
                {
                    ClearRestTarget();
                }
                else
                {
                    var bedUnder = GetBedUnderPawn();
                    bool onTargetBed = bedUnder != null && bedUnder == restTarget;
                    if (onTargetBed)
                    {
                        // 침대 도착 → 강제 수면 진입/유지.
                        forcedResting = true;
                        IsSleeping = true;
                        float restMul = restTarget.RestMul;
                        float moodPerSec = restTarget.MoodBonus;
                        sleep = Mathf.Min(100f, sleep + sleepRegenAtNight * restMul * dt);
                        food = Mathf.Max(0f, food - foodDecay * 0.5f * dt);
                        mood = Mathf.Max(0f, mood - moodDecay * 0.5f * dt);
                        if (moodPerSec > 0f) mood = Mathf.Min(100f, mood + moodPerSec * dt);
                        // 충분히 쉬었으면 명령 종료 (자동 기상).
                        if (sleep >= forcedWakeSleepLevel)
                        {
                            IsSleeping = false;
                            ClearRestTarget();
                        }
                        return;
                    }
                    // #222 도달 timeout — 침대에 못 닿고 시간 초과면 명령을 풀어 stuck 방지
                    //  (경로 막힘/stand-cell 불일치/문 막힘).  자율취침과 동일한 robustness.
                    if (Time.time - restStartTime > restArriveTimeout)
                    {
                        Debug.Log($"[Rest] {name} 침대 도달 실패(timeout {restArriveTimeout}s) → 휴식 명령 해제, sleep={sleep:F0}");
                        ClearRestTarget();
                    }
                    // 아직 침대로 이동 중 — 일반 decay 만 진행 (아래로 fall-through 하되
                    //  강제 수면은 아직 아님).  forcedResting 은 false 유지.
                    else forcedResting = false;
                }
            }

            // ── 자율 취침 처리 (사용자 forcedResting 다음 우선, 제자리 취침보다 위) ──
            //  GoSleepAction 이 SetAutoSleepTarget 한 침대로 PawnUtilityAI 가 이동시킨다.
            //  여기서는 도착 여부만 검사:
            //    - 발밑에 그 침대 도착  → IsSleeping=true, bed.RestMul 로 회복.
            //    - 아직 이동 중          → 일반 decay 만 (아래로 fall-through, 아직 안 잠).
            //  자율 취침은 일반 기상(80)에서 종료 — 강제(95)보다 가볍게.
            if (autoRestTarget != null)
            {
                if (autoRestTarget == null || autoRestTarget.gameObject == null)
                {
                    ClearAutoSleepTarget();
                }
                else
                {
                    var bedUnder = GetBedUnderPawn();
                    bool onTargetBed = bedUnder != null && bedUnder == autoRestTarget;
                    if (onTargetBed)
                    {
                        IsSleeping = true;
                        float restMul = autoRestTarget.RestMul;
                        float moodPerSec = autoRestTarget.MoodBonus;
                        sleep = Mathf.Min(100f, sleep + sleepRegenAtNight * restMul * dt);
                        food = Mathf.Max(0f, food - foodDecay * 0.5f * dt);
                        mood = Mathf.Max(0f, mood - moodDecay * 0.5f * dt);
                        if (moodPerSec > 0f) mood = Mathf.Min(100f, mood + moodPerSec * dt);
                        // 충분히 잤으면(80) 자율 취침 종료.  예약 해제는 GoSleepAction/AI 가
                        //  HasAutoSleepOrder 가 풀린 걸 보고 처리.
                        if (sleep >= autoWakeSleepLevel)
                        {
                            IsSleeping = false;
                            ClearAutoSleepTarget();
                        }
                        return;
                    }
                    // ── ROBUST FALLBACK ──
                    //  아직 침대 위가 아니다.  제한시간 안에 도착했으면 이동 계속 (일반 decay),
                    //  하지만 timeout 을 넘겼는데도 침대에 못 닿았으면 (경로 실패 / door 막힘 /
                    //  stand-cell 불일치) 자율 취침 target 을 버리고 아래 제자리 취침으로 fall
                    //  through 한다.  이렇게 해서 림이 "휴식이동" 으로 stuck 되거나 sleep 이
                    //  0 으로 추락하는 회귀를 막는다 (9ecfaab 회귀의 핵심 수정).
                    bool arriveTimedOut = Time.time - autoSleepStartTime > autoSleepArriveTimeout;
                    if (arriveTimedOut)
                    {
                        // 침대 도달 포기 → 예약 해제는 PawnUtilityAI 가 HasAutoSleepOrder
                        //  풀린 걸 보고 다음 frame 처리.  쿨다운을 걸어 같은 침대로 재출발
                        //  → 또 timeout 하는 loop 를 막고, 그 동안 제자리 취침으로 회복한다.
                        ClearAutoSleepTarget();
                        autoSleepSuppressUntil = Time.time + autoSleepRetryCooldown;
                        Debug.Log($"[AutoSleep] {name} bed unreachable (timeout {autoSleepArriveTimeout}s) → 제자리 취침 fallback, sleep={sleep:F0}");
                        // (return 하지 않고 아래 sleep<30 && night 제자리 취침 블록으로 진행)
                    }
                    else
                    {
                        // 아직 이동 제한시간 내 — 일반 decay 만 진행 (도착 시 위에서 수면 진입).
                        food = Mathf.Max(0f, food - foodDecay * dt);
                        sleep = Mathf.Max(0f, sleep - sleepDecay * dt);
                        mood = Mathf.Max(0f, mood - moodDecay * dt);
                        return;
                    }
                }
            }

            // Day 10: when sleep is low AND it's night, pawn sleeps in place.
            // Sleep regenerates fast, food + mood still decay (mildly).
            //  운영자 #107 - 침대 위에서 자면 회복 보너스
            //  #153 - bed quality 별 RestMul/MoodBonus 적용 (#151 wiring).
            //    SleepingSpot 0.80x rest +0 mood/s
            //    Wood         1.00x rest +3 mood/s
            //    Fine         1.40x rest +8 mood/s
            //  bed 가 없으면 ground (rest 0.6x, mood 패널티 약간).
            // 제자리 취침 gate.  autoSleepThreshold(35) 와 동일하게 묶어, 침대 도달에
            //  실패해 fallback 으로 내려온 림이 (sleep≈30~35) 곧장 누워 회복하도록 한다.
            //  발밑이 침대면 자동으로 bed.RestMul 보너스 (위 cell 에 멈췄을 때 포함).
            //  #228 - 탈진(sleep<15)이면 낮에도 그 자리서 쓰러져 잔다(밤 게이트 무시).
            if (sleep < ExhaustedSleepLevel || (sleep < autoSleepThreshold && night))
            {
                IsSleeping = true;
                var bed = GetBedUnderPawn();
                float restMul = bed != null ? bed.RestMul : 0.6f;
                float moodPerSec = bed != null ? bed.MoodBonus : 0f;
                sleep = Mathf.Min(100f, sleep + sleepRegenAtNight * restMul * dt);
                food  = Mathf.Max(0f, food  - foodDecay * 0.5f * dt);
                mood  = Mathf.Max(0f, mood  - moodDecay * 0.5f * dt);
                if (moodPerSec > 0f) mood = Mathf.Min(100f, mood + moodPerSec * dt);
                return;
            }
            // Wake up when sleep refilled past 80, even if still night
            if (IsSleeping && sleep >= 80f) IsSleeping = false;
            if (IsSleeping)
            {
                sleep = Mathf.Min(100f, sleep + sleepRegenAtNight * dt);
                return;
            }

            // Day 22: weather mood penalty when outdoor + storm
            float weatherPenalty = 0f;
            if (WeatherController.Instance != null
                && WeatherController.Instance.Current == WeatherKind.Storm
                && !IsOnFloor())
            {
                weatherPenalty = 3f * dt;
            }

            food = Mathf.Max(0f, food - foodDecay * dt);
            sleep = Mathf.Max(0f, sleep - sleepDecay * dt);
            mood = Mathf.Max(0f, mood - moodDecay * dt - weatherPenalty);

            // Day 20: mood break — when mood drops below threshold, pawn
            // enters a "break" for moodBreakDuration.  Recovery only when
            // mood climbs back to recoverAt.  PawnUtilityAI checks IsBreaking
            // and skips picking new work tasks (pawn wanders aimlessly).
            if (!isBreaking && mood < moodBreakThreshold)
            {
                isBreaking = true;
                breakUntil = Time.time + moodBreakDuration;
            }
            else if (isBreaking && Time.time > breakUntil && mood > moodBreakRecoverAt)
            {
                isBreaking = false;
            }

            // #214 - 물리 섭취: 배고프면 음식원으로 걸어가서 도착 후에만 먹는다.
            //  (과거의 즉시-카운터-차감 = 순간이동 제거.)  poll-via-Update — lesson #7
            //  singleton-subscription-race 회피와 동일한 안전한 형태.
            TryPhysicalEatTick();
        }

        // ── #214 물리 섭취 상태기계 ──────────────────────────────────────────────────
        //  배고프면(food < eatThreshold) 음식원을 물색해 그쪽으로 이동시키고, 인접 도달
        //  시에만 1회 섭취.  drafted/manual/sleep/rest 중에는 발동하지 않는다(상위 생존/
        //  사용자 명령 우선 — 이 메서드는 그 블록들을 지난 awake 상태에서만 호출됨).
        private void TryPhysicalEatTick()
        {
            // 사용자 수동 제어/징집 중에는 자율 섭취 보류 (명령 우선).
            if (pawnEntity != null && (pawnEntity.IsDrafted || pawnEntity.IsUnderManualControl))
            {
                if (eatState == EatState.Walking) ClearEatTask();
                return;
            }

            // 진행 중인 섭취 이동이 있으면 그걸 우선 처리.
            if (eatState == EatState.Walking)
            {
                StepEatWalk();
                return;
            }

            if (food >= eatThreshold) return;
            if (Time.time < eatSuppressUntil) return;           // 직전 포기 후 쿨다운
            if (movement == null) return;
            // 이미 다른 이동/작업 중이면 이 frame 은 양보 — 작업이 끝나 idle 일 때 출발.
            //  (단, 너무 배고프면 식사가 생존이므로 작업보다 우선해야 하나, 작업 task 정리는
            //   PawnUtilityAI lane 이라 여기서 건드리지 않는다.  대신 ManualMoveUntil 로
            //   다음 AI tick 부터 식사 이동을 보호한다.)
            BeginEatWalk();
        }

        // 가장 가까운 물리 음식원을 골라 이동 시작.  못 찾으면 아무 것도 안 함(굶주림 유지 —
        //  순간이동으로 카운터에서 뽑아 먹지 않는다).
        private void BeginEatWalk()
        {
            Vector2 me = transform.position;
            var rm = ResourceManager.Instance;

            // 1) 물리 MeatPileEntity (바닥/저장고).  reserve 로 두 림이 한 더미에 안 몰리게.
            MeatPileEntity bestMeat = null;
            float bestSq = float.MaxValue;
            foreach (var m in Object.FindObjectsByType<MeatPileEntity>(FindObjectsSortMode.None))
            {
                if (m == null) continue;
                if (ReservationManager.IsReservedByOther(m, gameObject)) continue;
                float sq = ((Vector2)m.transform.position - me).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; bestMeat = m; }
            }

            // 2) meal/food 카운터가 있으면 그 보관 장소(식량 허용 stockpile)로 걸어가 먹는다.
            //  meal 은 별도 entity 가 없으므로 저장고 cell 이 "음식이 있는 위치" 역할을 한다.
            StockpileZoneEntity bestStock = null;
            if (rm != null && (rm.fineMeals > 0 || rm.meals > 0 || rm.food > 0))
            {
                bestStock = StockpileZoneEntity.FindBest(me, StockItemKind.Food);
            }

            // 3) 익은 베리 덤불에서 직접 따 먹기.
            BerryBushEntity bestBush = null;
            float bushSq = float.MaxValue;
            foreach (var b in Object.FindObjectsByType<BerryBushEntity>(FindObjectsSortMode.None))
            {
                if (b == null || b.IsDepleted) continue;
                if (ReservationManager.IsReservedByOther(b, gameObject)) continue;
                float sq = ((Vector2)b.transform.position - me).sqrMagnitude;
                if (sq < bushSq) { bushSq = sq; bestBush = b; }
            }

            // 가장 가까운 음식원 선택 (meat > stock > bush 동률시 거리 비교).
            //  meal 카운터를 가진 stockpile 이 더 가까우면 그쪽 우선 — 조리식이 mood 도 높음.
            float meatD = bestMeat != null ? bestSq : float.MaxValue;
            float stockD = bestStock != null
                ? ((Vector2)bestStock.transform.position - me).sqrMagnitude : float.MaxValue;
            float bushD = bestBush != null ? bushSq : float.MaxValue;

            if (meatD == float.MaxValue && stockD == float.MaxValue && bushD == float.MaxValue)
                return;  // 물리 음식원 전무 → 순간이동 금지, 굶주림 유지

            eatMeatTarget = null; eatStockTarget = null; eatBushTarget = null;
            if (meatD <= stockD && meatD <= bushD)
            {
                eatMeatTarget = bestMeat;
                eatDestWorld = bestMeat.transform.position;
                ReservationManager.TryReserve(bestMeat, gameObject);
            }
            else if (stockD <= bushD)
            {
                eatStockTarget = bestStock;
                eatDestWorld = bestStock.transform.position;
            }
            else
            {
                eatBushTarget = bestBush;
                eatDestWorld = bestBush.transform.position;
                ReservationManager.TryReserve(bestBush, gameObject);
            }

            eatState = EatState.Walking;
            eatStartTime = Time.time;
            movement.SetTarget(eatDestWorld);
            // AI override 는 PawnUtilityAI 의 busy-gate(movement.IsMoving) 가 막아준다 —
            //  이동 중엔 Decide 가 돌지 않아 work target 으로 가로채지 못한다.  ManualMoveUntil
            //  은 쓰지 않는다(그걸 쓰면 IsUnderManualControl 이 켜져 다음 frame 에 내 자신의
            //  TryPhysicalEatTick 가 "수동 제어 중"으로 오인해 eat 을 취소하는 자기상쇄 발생).
        }

        // 매 frame: 음식원 도달 여부 검사.  도달하면 섭취, 아니면 이동 유지(+timeout fallback).
        private void StepEatWalk()
        {
            // 목표 entity 가 사라졌으면(소비/부패) 재탐색.
            bool targetGone =
                (eatMeatTarget != null && eatMeatTarget.gameObject == null) ||
                (eatBushTarget != null && eatBushTarget.gameObject == null) ||
                (eatStockTarget != null && eatStockTarget.gameObject == null);
            if (targetGone) { ClearEatTask(); return; }

            float dist = Vector2.Distance(transform.position, eatDestWorld);
            if (dist <= eatReachRange)
            {
                ConsumeAtSource();
                ClearEatTask();
                return;
            }

            // 도달 timeout — 경로 실패/막힘이면 포기하고 쿨다운(stuck 방지, 순간이동 금지 유지).
            if (Time.time - eatStartTime > eatWalkTimeout)
            {
                Debug.Log($"[Eat] {name} 음식원 도달 실패(timeout) → 재시도 쿨다운, food={food:F0}");
                eatSuppressUntil = Time.time + eatRetryCooldown;
                ClearEatTask();
                return;
            }

            // 이동이 끊겼으면(도착 못 했는데 멈춤) 다시 음식원으로 향하게 — AI override 는
            //  busy-gate(movement.IsMoving) 가 막으므로 ManualMoveUntil 불필요.
            if (movement != null && !movement.IsMoving) movement.SetTarget(eatDestWorld);
        }

        // 음식원에 도착한 순간에만 1회 섭취 — 여기서만 카운터/덤불이 줄어든다(물리 도착 후).
        private void ConsumeAtSource()
        {
            var rm = ResourceManager.Instance;
            var traits = GetComponent<PawnTraits>();
            float traitMealBonus = traits != null ? traits.mealMoodBonus : 0f;

            // 1) 물리 고기 더미: 더미를 집어 먹는다.  더미가 저장고에 들어가 있던
            //  것(InStockpile)이면 그 양은 보관 카운터에 적립돼 있으므로 동기 차감한다.
            //  바닥에 흩어진(아직 미적립) 더미는 카운터를 건드리지 않는다(이중 차감 방지).
            if (eatMeatTarget != null && eatMeatTarget.gameObject != null)
            {
                int amount = eatMeatTarget.Food;
                bool counted = eatMeatTarget.InStockpile;
                Object.Destroy(eatMeatTarget.gameObject);
                if (counted && rm != null) rm.AddFood(-amount);
                food = Mathf.Min(100f, food + eatRestore);
                var th = GetComponent<PawnThoughts>();
                if (th != null) th.AddThought("배부름");
                return;
            }

            // 2) 저장고 도착: 보관된 조리식/식량을 한 단위 꺼내 먹는다(우선순위 fine>meal>raw).
            if (eatStockTarget != null && rm != null)
            {
                if (rm.fineMeals > 0)
                {
                    rm.AddFineMeals(-1);
                    food = Mathf.Min(100f, food + eatRestore);
                    mood = Mathf.Min(100f, mood + 20f + traitMealBonus);
                    var th2 = GetComponent<PawnThoughts>();
                    if (th2 != null) th2.AddThought("최고의 식사", +12f, 800f);
                    return;
                }
                if (rm.meals > 0)
                {
                    rm.AddMeals(-1);
                    food = Mathf.Min(100f, food + eatRestore);
                    mood = Mathf.Min(100f, mood + 10f + traitMealBonus);
                    var th = GetComponent<PawnThoughts>();
                    if (th != null) th.AddThought("맛있는 식사");
                    return;
                }
                if (rm.food > 0)
                {
                    rm.AddFood(-1);
                    food = Mathf.Min(100f, food + eatRestore);
                    var th = GetComponent<PawnThoughts>();
                    if (th != null) th.AddThought("배부름");
                    return;
                }
                // 도착했는데 보관 식량이 0 (다른 림이 먼저 먹음) → 그냥 종료, 다음 tick 재탐색.
                return;
            }

            // 3) 익은 베리 덤불: 덤불에서 직접 따 먹는다(카운터 경유 없이 직접 섭취).
            if (eatBushTarget != null && eatBushTarget.gameObject != null)
            {
                int got = eatBushTarget.TakeBerry();
                if (got > 0)
                {
                    food = Mathf.Min(100f, food + eatRestore);
                    var th = GetComponent<PawnThoughts>();
                    if (th != null) th.AddThought("배부름");
                }
            }
        }

        private void ClearEatTask()
        {
            if (eatMeatTarget != null) ReservationManager.Release(eatMeatTarget, gameObject);
            if (eatBushTarget != null) ReservationManager.Release(eatBushTarget, gameObject);
            eatMeatTarget = null;
            eatStockTarget = null;
            eatBushTarget = null;
            eatState = EatState.None;
            if (movement != null) movement.ClearTarget();
        }

        private bool IsOnFloor()
        {
            var hits = Physics2D.OverlapBoxAll(transform.position, Vector2.one * 0.3f, 0f);
            foreach (var h in hits)
                if (h != null && h.GetComponent<FloorEntity>() != null) return true;
            return false;
        }

        // 운영자 #107 - 침대 위에서 자면 보너스
        //  #153 - 단순 bool 대신 BedEntity 반환 (quality 별 RestMul/MoodBonus 접근).
        private BedEntity GetBedUnderPawn()
        {
            var hits = Physics2D.OverlapBoxAll(transform.position, Vector2.one * 0.4f, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                var bed = h.GetComponent<BedEntity>();
                if (bed != null) return bed;
            }
            return null;
        }
        public bool IsOnBed() => GetBedUnderPawn() != null;
        public BedEntity CurrentBed() => GetBedUnderPawn();

        private bool IsNightTime()
        {
            if (GameClock.Instance == null) return false;
            int h = GameClock.Instance.Hour;
            return h >= 22 || h < 6;
        }

        public float GetNormalized(NeedType n) => n switch
        {
            NeedType.Food => food / 100f,
            NeedType.Sleep => sleep / 100f,
            NeedType.Mood => mood / 100f,
            _ => 0f,
        };

        public NeedType LowestNeed()
        {
            NeedType worst = NeedType.Food;
            float worstVal = food;
            if (sleep < worstVal) { worst = NeedType.Sleep; worstVal = sleep; }
            if (mood < worstVal)  { worst = NeedType.Mood;  worstVal = mood;  }
            return worst;
        }
    }

    public enum NeedType { Food, Sleep, Mood }
}
