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
        // #200 genre fidelity: food (0-100) should empty over ~2.5-3 in-game
        //  days like the reference sim (hunger ~1.6 nutrition/day).  1 in-game day = 240
        //  real seconds (GameClock), so a 3-day budget = 100 / (3*240) = 0.139/s.
        //  Was 0.5/s → emptied in 200s = 0.83 day (~3.5x too fast).  0.14 ≈ 2.97 days.
        // #228 운영자 fb "배고픔·수면 게이지가 정상동작 안 하는 느낌" — 시계 fix(rate 6,
        //  하루=240s) 후 needs(실시간 decay)가 하루 주기와 분리돼 게이지가 며칠에 걸쳐 찔끔
        //  움직였다(food~3일/sleep~1.4일).  하루 주기로 재튜닝: 매일 먹고/밤마다 자도록.
        //  food 0.14→0.2(eat≈1회/일), sleep 0.3→0.4(sleep 100→0 ≈ 1게임일 → 밤마다 졸림).
        //  #234 the reference sim 시계(1x=하루1000초)에 맞춰 decay 를 비례 축소(×0.24)해 게임-하루
        //  리듬은 동일 유지: foodDecay 0.2→0.048, sleepDecay 0.4→0.096, moodDecay 0.2→0.048.
        //  (game-day 당 food≈48/sleep≈96 으로 rate-6 때와 동일 — 둘 다 timeScale 곱이라 정합.)
        //  #247 운영자 fb "식량·수면 감소가 없어 식사·잠 불필요 (심각). the reference sim는 빨리 내려감"
        //  — #234 의 느린 시계(1x=16.7분/일)로 needs 가 실시간 4× 느려져 체감상 '안 내려감'.
        //  식사·잠이 실제로 필요하도록 상향: food 0.048→0.13(게임일당 ~130 → 하루 2~3회 식사),
        //  sleep 0.096→0.18(~180/일 → 밤마다 + 부족시 졸림).  생존은 meals/berry/사냥+밤잠으로 유지.
        // 운영자 2026-06-02 "수면/식량 게이지가 왜 안 줄어드냐" — decay 는 돌고 있었으나 너무
        //  느려 체감이 안 됐다.  눈에 보이게 ~1.6x 상향(perceptible).  여전히 game-day 단위 생존.
        // #thrash-fix(2026-06-10) sleep 0.29 는 기상 3.7실분(게임 오전11시) 만에 탈진(<15) →
        //  WantsAutoSleep 영구 발화 → (침대 없으면) PawnUtilityAI 수면게이트 1.5s 마다 전 작업
        //  해제 = 운영자 "림 작업마비/벌목-떠도는중 번갈이"의 공범 (재현 p0-remote-chop).
        //  0.12 = 탈진 ~19시 도착(레퍼런스 콜로니심 리듬: 저녁 졸림→밤잠), #247 의 '체감 가능'
        //  의도(0.18 1차 조정)는 유지하되 마지막 1.6x 과조정만 되돌림.  food 0.13 = #247 1차
        //  값 복원(하루 2~3회 식사).  ※ 운영자 속도 체감 보고 받기 (확인 라운드 항목).
        [SerializeField] private float foodDecay = 0.13f;
        [SerializeField] private float sleepDecay = 0.12f;
        [SerializeField] private float moodDecay = 0.048f;  // #234 the reference sim 시계 비례 축소

        [Header("Day 9+: sleep regen when sleeping at night")]
        // 백로그 #8 (2026-06-11): 회복 8/s = 밤잠이 하루의 ~10초 → 수면이 시간 비용이
        //  아니었다.  3/s 로 — 침대 RestMul(0.8/1.0/1.4)이 처음으로 노동시간 경제에
        //  닿는다.  씬에 8 이 직렬화 → Awake clamp (#QR지면 교훈).
        [SerializeField] private float sleepRegenAtNight = 3f;
        private const float MaxSleepRegen = 3f;

        [Header("Day 11: eat-from-stockpile")]
        // #202 SURVIVAL-LOOP FIX — eat at 50 (was 40).  With the harvest loop now
        //  feeding the cook→meal supply, pawns can afford to eat sooner; a higher
        //  threshold narrows the food sawtooth (band ~50-80 instead of ~45-80),
        //  giving more starvation margin AND keeping the colony's food trend STABLE
        //  rather than swinging wide enough that quarter-window sampling reads a
        //  phantom decline.  the reference sim pawns eat around the "Hungry" (~ half) mark.
        [SerializeField] private float eatThreshold = 50f;
        // 게임루프 백로그 #3 (2026-06-11): 단일 eatRestore 30 은 베리·생식·조리식 전원
        //  동일 영양 = 요리 3raw→1meal 이 -66% 가치 파괴 (조리가 손해).  위계 분리로
        //  조리 사슬이 처음으로 흑자가 된다: raw 20 / meal 40 / fine 55.
        [SerializeField] private float eatRestoreRaw = 20f;
        [SerializeField] private float eatRestoreMeal = 40f;
        [SerializeField] private float eatRestoreFine = 55f;

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
        // 첫사이클 T15 (2026-06-12) — 식사가 1프레임('뿅')이라 '먹는 중'이 화면에
        //  없던 것: Eating 단계(3스케일초 + 주기 '냠' FX) 신설.
        private enum EatState { None, Walking, Eating }
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
        public bool IsEating => eatState != EatState.None;

        public bool IsSleeping { get; private set; }

        // ── rcfix: the reference sim 우클릭 "Rest" / "Sleep" 명령 ─────────────────────────
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
        // #침대도달불가 race(2026-06-11): 제자리 취침이 걷기와 같은 임계(35)면 PawnNeeds
        //  (매 프레임)가 sleep<35 교차 프레임에 즉시 IsSleeping=true → PawnUtilityAI 의
        //  IsSleeping 블록이 Decide(1.5s 주기)보다 항상 먼저 return → GoSleepAction(침대
        //  걷기)이 영원히 실행 기회를 못 얻는다.  걷기보다 낮은 별도 임계 — 35~30 구간
        //  (decay 0.12/s ≈ 42 실초)이 AI 가 침대를 잡아 걸어갈 유예 창.  침대로 출발한
        //  뒤(HasAutoSleepOrder)는 위 전용 블록이 처리하므로 이 게이트와 무관.
        [SerializeField] private float inPlaceSleepThreshold = 30f;  // 바닥 취침 — 걷기(35)보다 낮게
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
        // 아사 틱 시각 (Time.time 스케일초) + 표시 스로틀 (실초)
        //  4.5스케일초/3dmg: 부위 랜덤 분산 + TotalHpRatio(부위 평균) 둔감성 기준
        //  공복→사망 ~0.22게임일, 만복부터 총 ~1게임일.
        private const float StarvTickGameSec = 4.5f;
        private float nextSleepHealT = -1f;   // 백로그 #5 수면 치유 틱
        // #QA플레이 F3 (2026-06-11): '빈 침대 없음' 힌트 — GoSleepAction 실패가 매
        //  decision tick(1.5s)마다 찍어줌.  스케줄 밤잠인데 침대가 없으면 림월드처럼
        //  바닥에서 잔다 (이전엔 밤새 배회 — thrash-fix 가 '침대 없으면 계속 일'로만
        //  굴려 침대 1개 콜로니의 나머지 림이 영원히 안 잤다).
        //  ⚠ 단발 null 에 잠그면 안 된다.  F5 클로즈(2026-06-12, 타임스탬프 재현):
        //  '일시 null'의 정체는 시작 직후 잔여 Sleep 슬롯(아침 hour=6 틱)의 마크가
        //  밤 스트릭으로 새던 것 — 침대 스폰 전 아침 마크 3개가 23h 진입 즉시 바닥취침을
        //  잠갔다.  밤/슬롯이 아니면 스트릭을 리셋(세션 단위)하므로 임계 3틱으로 충분.
        private float lastNoBedMark = -999f;
        private int noBedStreak;
        //  마크는 실제 밤(IsNightTime)에서만 적립 — 아침 잔여 Sleep 슬롯(hour 6 에지)
        //  마크가 밤으로 새던 누수의 최종 차단 (시각 재현 t=0.5/2.0/3.5 hour=6).
        public void MarkNoBedTonight()
        {
            if (!IsNightTime()) return;
            noBedStreak++;
            lastNoBedMark = Time.time;
        }
        public void ClearNoBedHint() { noBedStreak = 0; }
        public bool NoBedSustained => noBedStreak >= 3 && Time.time - lastNoBedMark < 6f;
        private float starvNextTickGs = -1f;
        private float lastStarvText = -99f;
        private static float _lastNoFoodAlert = -99f;   // r2 #5 — 콜로니 단위 스로틀
        // 운영자 피드백 #14 (2026-06-12 AM): 자는지 식별 불가 — 수면 중 주기 zZZ.
        private float _nextSleepFx = -1f;
        // T15 — 식사 점유 시간 / FX 스로틀
        private float eatingUntil = -1f;
        private float _nextEatFx = -1f;
        // 첫사이클 T5 — 침대 무드 '초당' 가산 제거의 짝: 기상 시 품질별 1회 thought.
        private BedEntity lastSleepBed;
        private void GrantWakeMood(BedEntity b)
        {
            lastSleepBed = null;
            if (b == null) return;
            var th = GetComponent<PawnThoughts>();
            if (th == null) return;
            if (b.RestMul >= 1.3f)      th.AddThought("고급 침대에서 잠", +6f, 600f);
            else if (b.RestMul >= 0.95f) th.AddThought("침대에서 잠", +3f, 600f);
            else                          th.AddThought("잠자리에서 잠", +1f, 600f);
        }
        private bool starveWarned;                       // r2-E — 임박 경고 1회 게이트
        [SerializeField] private float autoSleepRetryCooldown = 20f;
        public bool HasAutoSleepOrder => autoRestTarget != null;
        public BedEntity AutoRestTarget => autoRestTarget;
        /// <summary>밤에 졸려서 자율로 침대를 찾아가야 하는가 (GoSleepAction 의 eligibility).
        ///  도달 실패 직후엔 쿨다운 동안 false (제자리 취침으로 안정 회복하게).</summary>
        // #228 - 평소엔 밤에 졸리면(sleep<35 && 밤) 침대로.  단 ★탈진(sleep<15)이면 낮에도
        //  자러 간다 — 과거엔 밤 게이트 때문에 낮에 sleep 이 20까지 떨어져도 안 자고 게이지가
        //  바닥에 정체돼 "수면 게이지가 정상동작 안 함"처럼 보였다.
        private const float ExhaustedSleepLevel = 15f;
        // 첫사이클 T4 (2026-06-12) — 걷기/실신 임계가 동일(15)이라 같은 프레임 발화 →
        //  1일차 저녁 전 림이 빈 침대 옆에서도 길바닥 동시 실신.  걷기 트리거를 20으로
        //  올려 15~20 을 '침대로 걸어갈 유예 창'으로 (실신은 15 유지).
        private const float ExhaustedWalkLevel = 20f;
        public bool WantsAutoSleep =>
            (sleep < ExhaustedWalkLevel || (sleep < autoSleepThreshold && IsNightTime()) || ScheduledSleepNow)
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

        // #버그헌트4(2026-06-05): save/load 후 GameSaveButtons 가 호출.  복원된 mood 에 트레잇
        //  moodBaselineBonus 가 이미 포함돼 있으므로(저장 당시 1회 가산분) Update 의 baseline 재가산을
        //  막되(traitsApplied=true), moodDecay 의 Stoic swingMul 스케일은 fresh 인스턴스라 여기서
        //  재적용한다.  안 하면 매 로드마다 Cheerful +10 / Lazy -8 baseline 이 누적 표류했다.
        public void MarkTraitsApplied()
        {
            if (traitsApplied) return;
            var tr = GetComponent<PawnTraits>();
            if (tr != null) moodDecay *= tr.moodSwingMul;   // decay 스케일만(baseline 재가산 X)
            traitsApplied = true;
        }

        [Header("Day 20: mood break")]
        [SerializeField] private float moodBreakThreshold = 20f;
        [SerializeField] private float moodBreakRecoverAt = 35f;
        [SerializeField] private float moodBreakDuration = 30f;
        private bool isBreaking = false;
        private float breakUntil = -10f;
        public bool IsBreaking => isBreaking;

        private PawnSchedule schedule;   // #269 스케줄 수면 연동

        private void Awake()
        {
            movement = GetComponent<PawnMovement>();
            pawnEntity = GetComponent<PawnEntity>();
            if (sleepRegenAtNight > MaxSleepRegen) sleepRegenAtNight = MaxSleepRegen;  // 백로그 #8 직렬화 무력화
            schedule = GetComponent<PawnSchedule>();
        }

        // #269 the reference sim 스케줄: 현재 시간대가 Sleep 슬롯이면 졸리지 않아도 침대로 가 자고,
        //  슬롯 동안 풀충전돼도 안 깬다(Anything 은 피곤할 때만 — 기존 동작).
        //  운영자 fb "스케줄에 수면이 있는데 왜 안 따르냐" + reference-simwiki Schedule.
        public bool ScheduledSleepNow => schedule != null
            && schedule.GetCurrentSlot() == TimeSlot.Sleep;

        private void Update()
        {
            float dt = Time.deltaTime;
            bool night = IsNightTime();

            // F5 — 바닥취침 스트릭은 '밤 세션' 단위: 밤/Sleep 슬롯이 아니면 리셋
            //  (아침 마크가 밤으로 새는 누수 차단).
            if (!(night || ScheduledSleepNow)) noBedStreak = 0;

            // 백로그 #5 (2026-06-11) — 수면 자연 치유: 1게임시간(≈41.7스케일초)마다
            //  침대 2 / 바닥 1.  부상 회복이 의사 tend 뿐이라 '다리 5/20 림 영구 불구'
            //  였던 것 — 침대의 회복 가치가 처음으로 생긴다 (HealAll 재사용).
            if (IsSleeping)
            {
                if (nextSleepHealT < 0f) nextSleepHealT = Time.time + 41.7f;
                else if (Time.time >= nextSleepHealT)
                {
                    nextSleepHealT = Time.time + 41.7f;
                    var hh = GetComponent<PawnHealth>();
                    if (hh != null) hh.HealAll(GetBedUnderPawn() != null ? 2 : 1);
                }
            }
            else nextSleepHealT = -1f;

            // #164 - PawnTraits.moodBaselineBonus 시작 시 mood 에 1회 가산.
            //  Awake 가 traits 보다 먼저 fire 될 수 있어 Update 첫 frame 에서 적용.
            if (!traitsApplied)
            {
                var tr = GetComponent<PawnTraits>();
                if (tr != null)
                {
                    mood = Mathf.Clamp(mood + tr.moodBaselineBonus, 0f, 100f);
                    // #obj-audit #13 — Stoic(무던하다) moodSwingMul(÷2) 실배선.  그동안
                    //  PawnTraits 가 값만 계산하고 소비처가 없어 'mood swing ÷2' 가 무효과
                    //  dead-trait 였다.  의도("안정적") 그대로, mood 자연 감소율을 1회 스케일
                    //  (기본 1.0 → 무효, Stoic 0.5 → 절반 속도로 기분 하락).  moodDecay 를
                    //  쓰는 모든 분기에 자동 반영(추가 임의 밸런스 없음).
                    moodDecay *= tr.moodSwingMul;
                }
                traitsApplied = true;
            }

            // ── rcfix: 강제 휴식 명령 처리 (night/tired 게이트보다 우선) ──────────
            //  사용자가 침대 우클릭 → SetRestTarget.  pawn 이 그 침대 위에 도착하면
            //  졸리지 않아도/낮이어도 눕는다 (the reference sim 우클릭 Rest).
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
                        EmitSleepFx();   // grader 6차 F — 침대 수면 분기에 zZ 누락
                        float restMul = restTarget.RestMul;
                        // T5 — 무드는 기상 시 1회 thought (초당 가산은 하룻밤 +999~ 로
                        //  thought 경제를 매일 리셋시키던 운영자 승인 모델 위반 잔재).
                        sleep = Mathf.Min(100f, sleep + sleepRegenAtNight * restMul * dt);
                        food = Mathf.Max(0f, food - foodDecay * 0.5f * dt);
                        mood = Mathf.Max(0f, mood - moodDecay * 0.5f * dt);
                        // 충분히 쉬었으면 명령 종료 (자동 기상).
                        if (sleep >= forcedWakeSleepLevel)
                        {
                            IsSleeping = false;
                            GrantWakeMood(restTarget);
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
                        EmitSleepFx();   // grader 6차 F — 침대 수면 분기에 zZ 누락
                        float restMul = autoRestTarget.RestMul;
                        sleep = Mathf.Min(100f, sleep + sleepRegenAtNight * restMul * dt);
                        food = Mathf.Max(0f, food - foodDecay * 0.5f * dt);
                        mood = Mathf.Max(0f, mood - moodDecay * 0.5f * dt);
                        // 충분히 잤으면(80) 자율 취침 종료.  단 #269 스케줄 Sleep 슬롯 중엔
                        //  풀충전돼도 계속 잔다(슬롯 끝날 때까지) — the reference sim Sleep 동작.
                        if (sleep >= autoWakeSleepLevel && !ScheduledSleepNow)
                        {
                            IsSleeping = false;
                            // 백로그 #4b + T5 — 품질별 1회 thought 로 통일.
                            GrantWakeMood(autoRestTarget);
                            var thw = GetComponent<PawnThoughts>();
                            if (thw != null && sleep >= 95f) thw.AddThought("푹 잠");
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
                        float bedDist = Vector2.Distance(transform.position, autoRestTarget.transform.position);
                        ClearAutoSleepTarget();
                        autoSleepSuppressUntil = Time.time + autoSleepRetryCooldown;
                        Debug.Log($"[AutoSleep] {(pawnEntity != null ? pawnEntity.PawnName : name)} bed unreachable (timeout {autoSleepArriveTimeout}s, 남은거리 {bedDist:F1}) → 제자리 취침 fallback, sleep={sleep:F0}");
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
            // #269 주의: ScheduledSleepNow 를 여기(제자리 취침)에 넣으면 침대로 가기 전에
            //  그 자리서 자버린다.  스케줄 수면은 WantsAutoSleep → GoSleepAction(침대 이동)
            //  으로 처리하고, 여긴 기존 조건(졸림/밤/탈진)만 — 침대 도달 실패 시의 fallback.
            // #버그헌트2(2026-06-04): 징집(수동제어) 중에는 자율/제자리 취침 금지.  이전엔 drafted
            //  게이트가 없어 밤·저수면 시 징집 폰이 군사통제 중 제자리에서 잠들어(IsSleeping=true),
            //  HandleDraftedCombat 의 전투 이동과 충돌하는 모순 상태가 됐다(섭취는 이미 drafted 게이트).
            if (pawnEntity != null && pawnEntity.IsDrafted)
            {
                if (IsSleeping) IsSleeping = false;
            }
            // #침대도달불가 race fix: 평시 바닥 취침은 inPlaceSleepThreshold(30) — 걷기
            //  임계(35)와 분리해 35~30 구간을 GoSleepAction 의 침대 걷기 유예 창으로 비운다.
            //  단 침대 도달 실패 직후(suppress 쿨다운 중)는 기존대로 30~35 에서도 곧장 눕는다
            //  (9ecfaab robust fallback — 림이 멀뚱히 서서 sleep 만 까먹지 않게).
            else if (sleep < ExhaustedSleepLevel
                     || (night && sleep < autoSleepThreshold && Time.time < autoSleepSuppressUntil)
                     || (night && sleep < inPlaceSleepThreshold)
                     || (night && ScheduledSleepNow && NoBedSustained))
            {
                IsSleeping = true;
                EmitSleepFx();
                var bed = GetBedUnderPawn();
                float restMul = bed != null ? bed.RestMul : 0.6f;
                if (bed != null) lastSleepBed = bed;   // T5 — 기상 1회 thought 용
                sleep = Mathf.Min(100f, sleep + sleepRegenAtNight * restMul * dt);
                food  = Mathf.Max(0f, food  - foodDecay * 0.5f * dt);
                mood  = Mathf.Max(0f, mood  - moodDecay * 0.5f * dt);
                return;
            }
            // Wake up when sleep refilled past 80 — 단 #269 스케줄 Sleep 슬롯 중엔 계속 잔다.
            if (IsSleeping && sleep >= 80f && !ScheduledSleepNow)
            {
                IsSleeping = false;
                GrantWakeMood(lastSleepBed);   // T5 — 바닥이면 no-op
            }
            if (IsSleeping)
            {
                EmitSleepFx();
                // 첫사이클 T6 — 이 분기만 RestMul 풀레이트라 '바닥 취침(3.0/s)이
                //  수면자리(2.4/s)보다 빨리 회복'하는 경제 역전이 있었다.
                var contBed = GetBedUnderPawn();
                if (contBed != null) lastSleepBed = contBed;
                sleep = Mathf.Min(100f, sleep + sleepRegenAtNight * (contBed != null ? contBed.RestMul : 0.6f) * dt);
                // #audit2 #18 — 회복/스케줄 수면 분기에도 food/mood 0.5x decay.  이전엔 sleep 만
                //  채우고 return 해서, sleep 이 회복 임계(~35)를 넘긴 뒤 80 까지 자는 동안(그리고
                //  스케줄 수면 내내) 허기·기분이 전혀 줄지 않는 '무한 무허기' 버그였다.  정상
                //  취침 분기(위)와 동일한 0.5x 감소로 통일 — RimWorld 도 수면 중 허기 진행.
                food = Mathf.Max(0f, food - foodDecay * 0.5f * dt);
                mood = Mathf.Max(0f, mood - moodDecay * 0.5f * dt);
                return;
            }

            // Day 22→#폭풍fix(2026-06-10): 폭풍 직접 드레인(3f*dt) 제거.  -3/스케일초 × 폭풍
            //  60스케일초 = -180 으로 바닥타일 없는 림 전원이 폭풍 1회에 무조건 정신붕괴였다
            //  (longplay-2026-06-10-cycle2 3일차 자정 전원 동시 mood 0, 재현 p1-storm-mood).
            //  운영자 승인 mood 모델(decay+thought 델타 합산)대로 '야외 폭풍' thought(-6)로
            //  일원화 — 배선은 PawnThoughts.Update (배고픔/수면부족과 동일 패턴).

            food = Mathf.Max(0f, food - foodDecay * dt);
            sleep = Mathf.Max(0f, sleep - sleepDecay * dt);
            mood = Mathf.Max(0f, mood - moodDecay * dt);

            // ── 아사 (운영자 2026-06-11 '가만히 냅둬도 게임오버가 되지 않음') ──
            //  감쇠율은 림월드 패리티(#234)였으나 food 0 의 '결과'가 없었다 — 굶주림이
            //  죽음에 이르지 않으면 생존게임 축이 통째로 장식이 된다.
            //  food 0 지속: 12 게임초당 1 HP (만복→공복 ~1게임일 + 공복→사망 ~0.4게임일
            //  ≈ 총 1.4게임일 — 림월드 영양실조보다 압축, 프로토 페이싱).
            //  시간 기준은 Time.time(스케일 시계) — 니즈 decay·WaitForSeconds 와 동일
            //  기반이라 배속/프레임 케이스에서 표류하지 않는다 (dt 누적·GameClock 기반
            //  1·2차 구현은 측정 시계와 어긋나 틱이 6~12배 느리게 보였다).
            //  6스케일초/틱 ≈ 518게임초 — PawnHealth 부위 풀(~137) 기준 공복 후 사망까지
            //  ~0.25게임일, 만복부터 총 ~1.25게임일.
            // r2-E (감사 자동승격) — 아사 임박 개인 경고: food 15 하향 교차 1회.
            if (food < 15f && !starveWarned && pawnEntity != null && !pawnEntity.IsDead)
            {
                starveWarned = true;
                AlertStackUI.Notify($"{pawnEntity.PawnName} 굶주림 위험", 2);
                FloatingText.Spawn(transform.position + Vector3.up * 0.6f,
                                   "아사 임박!", new Color(0.95f, 0.55f, 0.25f, 1f));
            }
            else if (food > 25f) starveWarned = false;

            if (food <= 0f && pawnEntity != null && !pawnEntity.IsDead)
            {
                float gs = Time.time;
                if (starvNextTickGs < 0f) starvNextTickGs = gs + StarvTickGameSec;
                if (gs >= starvNextTickGs)
                {
                    starvNextTickGs = gs + StarvTickGameSec;
                    pawnEntity.TakeTrueDamage(3);   // 방어구 무시 — 굶주림은 못 막는다
                    Debug.Log($"[Starv] {(pawnEntity != null ? pawnEntity.PawnName : name)} 아사 틱 t={Time.time:F1}");
                    if (Time.unscaledTime - lastStarvText > 3f)   // 표시는 실초 스로틀
                    {
                        lastStarvText = Time.unscaledTime;
                        FloatingText.Spawn(transform.position + Vector3.up * 0.6f,
                                           "굶주림!", new Color(0.92f, 0.35f, 0.25f, 1f));
                    }
                }
            }
            else
            {
                if (starvNextTickGs >= 0f)
                    Debug.Log($"[Starv] {(pawnEntity != null ? pawnEntity.PawnName : name)} 아사 중단 — food={food:F1} t={Time.time:F1}");
                starvNextTickGs = -1f;
            }

            // Day 20: mood break — when mood drops below threshold, pawn
            // enters a "break" for moodBreakDuration.  Recovery only when
            // mood climbs back to recoverAt.  PawnUtilityAI checks IsBreaking
            // and skips picking new work tasks (pawn wanders aimlessly).
            if (!isBreaking && mood < moodBreakThreshold)
            {
                isBreaking = true;
                breakUntil = Time.time + moodBreakDuration;
                // r2 #3 — 붕괴는 (섭취 금지와 결합해) 아사 나선의 입구인데 무음이었다.
                //  +관측성 (2026-06-12): 화면 신호뿐이라 로그 grep 으로 붕괴를 못 찾던 것.
                Debug.Log($"[Mood] {(pawnEntity != null ? pawnEntity.PawnName : name)} 정신붕괴 진입 mood={mood:F0} t={Time.time:F1}");
                AlertStackUI.Notify($"{(pawnEntity != null ? pawnEntity.PawnName : name)} 정신붕괴!", 1);
                FloatingText.Spawn(transform.position + Vector3.up * 0.6f,
                                   "정신붕괴!", new Color(0.85f, 0.45f, 0.85f, 1f));
            }
            else if (isBreaking && Time.time > breakUntil)
            {
                // 카타르시스 복귀 (2026-06-12, grader 4차 W-1) — 이전 조건은 '시간 만료
                //  AND mood>35'라 침대 없는 콜로니는 mood 가 35 를 영영 못 넘어 영구
                //  붕괴(마지막 1게임일 콜로니 전체 노동 정지)였다.  레퍼런스 멘탈
                //  브레이크는 시간 만료로 끝나고 카타르시스 무드 보너스가 붙는다 —
                //  복귀 시 mood 를 recoverAt+10 까지 회복시켜 즉시 재붕괴 루프를 차단.
                //  붕괴 자체의 비용(지속 시간 동안 노동 정지·배회)은 그대로다.
                isBreaking = false;
                mood = Mathf.Max(mood, moodBreakRecoverAt + 10f);
                var thCat = GetComponent<PawnThoughts>();
                if (thCat != null) thCat.AddThought("후련함", +8f, 600f);
                Debug.Log($"[Mood] {(pawnEntity != null ? pawnEntity.PawnName : name)} 붕괴 복귀(카타르시스) mood={mood:F0} t={Time.time:F1}");
                FloatingText.Spawn(transform.position + Vector3.up * 0.6f,
                                   "진정함", new Color(0.55f, 0.8f, 0.55f, 1f));
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
        // 운영자 피드백 #14 + grader 7차 — 순간 플로팅(수명~1s/주기 4s)은 임의 샷에
        //  안 잡힌다(야간 3샷 전부 미스).  레퍼런스처럼 수면 내내 떠 있는 '상시 zZ
        //  마커'(자식 TextMesh, MineMark 패턴)로 교체.  ASCII 라 폰트 글리프 무관.
        private GameObject _sleepMarker;
        private void EmitSleepFx()
        {
            if (_sleepMarker == null)
            {
                _sleepMarker = new GameObject("SleepZz");
                _sleepMarker.transform.SetParent(transform, false);
                _sleepMarker.transform.localPosition = new Vector3(0.35f, 0.8f, 0f);
                var tm = _sleepMarker.AddComponent<TextMesh>();
                tm.text = "zZ";
                tm.fontSize = 40;
                tm.characterSize = 0.07f;
                tm.anchor = TextAnchor.LowerCenter;
                tm.color = new Color(0.78f, 0.88f, 1f, 0.95f);
                var mrz = _sleepMarker.GetComponent<MeshRenderer>();
                if (mrz != null) mrz.sortingOrder = 31;   // NightOverlay(25)·바(30) 위
            }
            if (!_sleepMarker.activeSelf) _sleepMarker.SetActive(true);
            _sleepFxSeen = Time.time;
        }
        private float _sleepFxSeen = -1f;
        private void LateUpdate()
        {
            // 수면 분기가 이번 프레임 EmitSleepFx 를 안 불렀으면(기상/이동) 마커 끔.
            if (_sleepMarker != null && _sleepMarker.activeSelf
                && (!IsSleeping || Time.time - _sleepFxSeen > 0.5f))
                _sleepMarker.SetActive(false);
        }

        private void TryPhysicalEatTick()
        {
            // 사용자 수동 제어/징집 중에는 자율 섭취 보류 (명령 우선).
            if (pawnEntity != null && (pawnEntity.IsDrafted || pawnEntity.IsUnderManualControl))
            {
                if (eatState == EatState.Walking) ClearEatTask();
                return;
            }
            // 게임루프 백로그 #4 (2026-06-11): 정신붕괴 중 자율 섭취 금지 — 이전엔 mood 0
            //  콜로니도 붕괴 상태로 영구 생존해 기분이 생사 수치로 전이되지 않았다.
            //  붕괴 방치 → 섭취 정지 → food 0 → 아사 = 죽음 나선 완성.  회복 경로는
            //  보존: 침대 MoodBonus/양성 thought 로 mood 가 recoverAt 을 넘으면 정상화.
            if (isBreaking)
            {
                // 생존 본능 보정 (2026-06-12, 재현 _diag 6차) — 붕괴-섭취금지가 '식량
                //  688 비축에 원년 전원 아사'를 만들었다: 침대 없는 콜로니는 mood 가
                //  recoverAt(35)을 영영 못 넘어 회복 경로가 닫힌 트랩 (방랑자만 생존,
                //  원년 림 [Eat] 0건으로 확정).  레퍼런스도 대부분의 멘탈 브레이크에서
                //  섭취는 유지한다.  아사 임계(25) 미만이면 붕괴 중에도 먹는다 — mood
                //  나선(작업 정지·배회)은 유지, '식량 고갈' 전멸 경로는 불변.
                if (food >= 25f)
                {
                    if (eatState == EatState.Walking) ClearEatTask();
                    return;
                }
            }

            // T15 — 식사 중: 주기 '냠' FX, 시간 차면 실제 소비.
            if (eatState == EatState.Eating)
            {
                if (Time.unscaledTime >= _nextEatFx)
                {
                    _nextEatFx = Time.unscaledTime + 1.1f;
                    FloatingText.Spawn(transform.position + new Vector3(0.3f, 0.6f, 0f),
                                       "냠", new Color(0.95f, 0.85f, 0.6f, 0.95f));
                }
                if (Time.time >= eatingUntil)
                {
                    float before = food;
                    ConsumeAtSource();
                    Debug.Log($"[Eat] {name} 도착·섭취 food {before:F0}→{food:F0} t={Time.time:F1}");   // 관측성
                    ClearEatTask();
                }
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
            // 첫사이클 T14 — 순수 거리 + '<=' 라 동률(저장 셀 위 더미)이면 생고기가
            //  이겨 조리 동기가 붕괴했다.  조리식(meal/fine)이 있으면 저장고 우선
            //  (레퍼런스: 조리식 우선, 생식은 마지못해).  생식 부정 thought 는 결정 D6.
            var rmPick = ResourceManager.Instance;
            if (bestStock != null && rmPick != null && (rmPick.meals > 0 || rmPick.fineMeals > 0))
                stockD = 0f;

            if (meatD == float.MaxValue && stockD == float.MaxValue && bushD == float.MaxValue)
            {
                // r2 #5 — '배고픈데 먹을 게 없다'는 아사 ~1게임일 전 마지막 개입 시점인데
                //  무음이었다.  콜로니 단위 30실초 스로틀로 경보 + 본인 플로팅.
                if (Time.unscaledTime - _lastNoFoodAlert > 30f)
                {
                    _lastNoFoodAlert = Time.unscaledTime;
                    AlertStackUI.Notify("식량 전무 — 굶는 림 발생", 2);
                }
                FloatingText.Spawn(transform.position + Vector3.up * 0.6f,
                                   "먹을 것 없음!", new Color(0.95f, 0.65f, 0.3f, 1f));
                Debug.Log($"[Eat] {name} 음식원 전무 food={food:F0}");   // 관측성 (아사 진단)
                return;  // 물리 음식원 전무 → 순간이동 금지, 굶주림 유지
            }

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
            // 관측성 (2026-06-12 아사 진단) — '식량 632 두고 전원 아사'에서 섭취 선택이
            //  무로그라 어느 단계가 멈췄는지 특정 불가했다.  선택 1줄.
            Debug.Log($"[Eat] {name} 출발 → {(eatMeatTarget != null ? "더미" : eatStockTarget != null ? "저장고" : "덤불")} d={Vector2.Distance(transform.position, eatDestWorld):F1} food={food:F0} t={Time.time:F1}");
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
                // T15 — 도착 즉시 소비('뿅') 대신 3스케일초 식사 단계.
                eatState = EatState.Eating;
                eatingUntil = Time.time + 3f;
                Debug.Log($"[Eat] {name} 식사 시작 t={Time.time:F1}");
                if (movement != null) movement.ClearTarget();
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
                // 첫사이클 T3 (2026-06-12) — 간식 1회가 더미 '전량' 증발 + 카운터 전액
                //  차감이던 것 (회복은 +20뿐): 모아둔 식량이 식사마다 녹아 비축 루프
                //  불신의 직접 원인.  레퍼런스처럼 1단위만 차감, 잔량 보존.
                bool counted = eatMeatTarget.InStockpile;
                int left = eatMeatTarget.Food - 1;
                if (left <= 0) Object.Destroy(eatMeatTarget.gameObject);
                else eatMeatTarget.SetFood(left);
                if (counted && rm != null) rm.AddFood(-1);
                AudioBank.Instance?.PlayEat();   // #게임필2 — 식사가 들린다 (0.5s 스로틀)
                food = Mathf.Min(100f, food + eatRestoreRaw);
                var th = GetComponent<PawnThoughts>();
                if (th != null) th.AddThought("배부름");
                ApplyDiningBonus();
                return;
            }

            // 2) 저장고 도착: 보관된 조리식/식량을 한 단위 꺼내 먹는다(우선순위 fine>meal>raw).
            //  #회귀가드: eatMeat/eatBush 와 동일하게 gameObject 파괴 방어(저장존 erase 레이스).
            if (eatStockTarget != null && eatStockTarget.gameObject != null && rm != null)
            {
                if (rm.fineMeals > 0)
                {
                    rm.AddFineMeals(-1);
                    AudioBank.Instance?.PlayEat();   // #게임필2 — 식사가 들린다 (0.5s 스로틀)
                food = Mathf.Min(100f, food + eatRestoreFine);
                    mood = Mathf.Min(100f, mood + 20f + traitMealBonus);
                    var th2 = GetComponent<PawnThoughts>();
                    if (th2 != null) th2.AddThought("최고의 식사", +12f, 800f);
                    ApplyDiningBonus();
                    return;
                }
                if (rm.meals > 0)
                {
                    rm.AddMeals(-1);
                    AudioBank.Instance?.PlayEat();   // #게임필2 — 식사가 들린다 (0.5s 스로틀)
                food = Mathf.Min(100f, food + eatRestoreMeal);
                    mood = Mathf.Min(100f, mood + 10f + traitMealBonus);
                    var th = GetComponent<PawnThoughts>();
                    if (th != null) th.AddThought("맛있는 식사");
                    ApplyDiningBonus();
                    return;
                }
                if (rm.food > 0)
                {
                    // #자원모델 단일화(2026-06-04): 저장고 raw 식량 1 을 카운터 + 물리 InStockpile
                    //  MeatPile 에서 함께 소비(이전엔 카운터만 −1 → 물리 더미 잔존 #2: '카운터 0인데
                    //  화면엔 식량 더미').
                    rm.SpendStockpiledFood(1);
                    AudioBank.Instance?.PlayEat();   // #게임필2 — 식사가 들린다 (0.5s 스로틀)
                food = Mathf.Min(100f, food + eatRestoreRaw);
                    var th = GetComponent<PawnThoughts>();
                    if (th != null) th.AddThought("배부름");
                    ApplyDiningBonus();
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
                    AudioBank.Instance?.PlayEat();   // #게임필2 — 식사가 들린다 (0.5s 스로틀)
                food = Mathf.Min(100f, food + eatRestoreRaw);
                    var th = GetComponent<PawnThoughts>();
                    if (th != null) th.AddThought("배부름");
                    ApplyDiningBonus();
                }
            }
        }

        // 게임루프 차순위 (2026-06-12) — 식탁 배선.  TableEntity.EatSpotAt/ChairEntity.ChairAt
        //  프로브가 게임플레이 호출자 0 인 채 식탁 툴팁만 보너스를 약속하던 거짓 UI 교정.
        //  새 디스패치/경로탐색 없음: 림이 '먹는 칸'에 식탁이 있으면 보너스 — 침대
        //  GetBedUnderPawn 과 같은 모양.  양성만 배선('식탁 없이 식사' 페널티는 밸런스
        //  결정이라 운영자 유보).  직접 mood + AddThought 병행은 '최고의 식사' 기존 패턴.
        private void ApplyDiningBonus()
        {
            var table = TableEntity.EatSpotAt(transform.position);
            if (table == null) return;
            mood = Mathf.Min(100f, mood + table.AteAtTableMoodBonus);
            var th = GetComponent<PawnThoughts>();
            if (th != null) th.AddThought("식탁에서 식사", +table.AteAtTableMoodBonus,
                                          table.AteAtTableThoughtDur);
            var chair = ChairEntity.ChairAt(transform.position);
            if (chair != null) mood = Mathf.Min(100f, mood + chair.ChairComfortMoodBonus);
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

        // #폭풍fix(2026-06-10) public — PawnThoughts 가 '야외 폭풍' 노출 판정에 사용.
        public bool IsOnFloor()
        {
            // T7 — 물리 쿼리(콜라이더 부재로 영구 false) → 셀 레지스트리.
            return FloorEntity.HasFloorAt(transform.position);
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
