using UnityEngine;

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
        [SerializeField] private float foodDecay = 0.14f;
        [SerializeField] private float sleepDecay = 0.3f;
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
        [SerializeField] private float eatTickInterval = 0.5f;
        private float lastEatTime = -999f;

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
        public bool HasAutoSleepOrder => autoRestTarget != null;
        public BedEntity AutoRestTarget => autoRestTarget;
        /// <summary>밤에 졸려서 자율로 침대를 찾아가야 하는가 (GoSleepAction 의 eligibility).</summary>
        public bool WantsAutoSleep => sleep < autoSleepThreshold && IsNightTime();

        /// <summary>GoSleepAction 이 빈 침대를 예약한 뒤 호출 — 이 침대로 가서 자라.</summary>
        public void SetAutoSleepTarget(BedEntity bed)
        {
            autoRestTarget = bed;
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
                    // 아직 침대로 이동 중 — 일반 decay 만 진행 (아래로 fall-through 하되
                    //  강제 수면은 아직 아님).  forcedResting 은 false 유지.
                    forcedResting = false;
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
                    // 아직 침대로 이동 중 — 제자리 취침에 빠지지 않도록 일반 decay 만 진행.
                    food = Mathf.Max(0f, food - foodDecay * dt);
                    sleep = Mathf.Max(0f, sleep - sleepDecay * dt);
                    mood = Mathf.Max(0f, mood - moodDecay * dt);
                    return;
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
            if (sleep < 30f && night)
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

            // Day 11: eat from food stockpile when hungry (only while awake).
            // We poll ResourceManager.Instance every frame instead of subscribing —
            // lesson #7 singleton-subscription-race avoidance: PawnNeeds.Awake
            // could fire before ResourceManager.Awake, and OnEnable subscription
            // would miss the eventual instance.  Poll-via-Update is the safe shape.
            TryEatTick();
        }

        private void TryEatTick()
        {
            if (food >= eatThreshold) return;
            if (Time.time - lastEatTime < eatTickInterval) return;
            var rm = ResourceManager.Instance;
            if (rm == null) return;

            // Day 27: prefer cooked meal — +eatRestore food AND +10 mood bonus.
            // #131 - fine meal 우선 (mood +20, "최고의 식사" thought)
            // #164 - PawnTraits.mealMoodBonus (Gourmand +15) 가산.
            var traits = GetComponent<PawnTraits>();
            float traitMealBonus = traits != null ? traits.mealMoodBonus : 0f;
            if (rm.fineMeals > 0)
            {
                rm.AddFineMeals(-1);
                food = Mathf.Min(100f, food + eatRestore);
                mood = Mathf.Min(100f, mood + 20f + traitMealBonus);
                lastEatTime = Time.time;
                var th2 = GetComponent<PawnThoughts>();
                if (th2 != null) th2.AddThought("최고의 식사", +12f, 800f);
                return;
            }
            if (rm.meals > 0)
            {
                rm.AddMeals(-1);
                food = Mathf.Min(100f, food + eatRestore);
                mood = Mathf.Min(100f, mood + 10f + traitMealBonus);
                lastEatTime = Time.time;
                // #122 - mood thought 추가
                var th = GetComponent<PawnThoughts>();
                if (th != null) th.AddThought("맛있는 식사");
                return;
            }
            if (rm.food > 0)
            {
                rm.AddFood(-1);
                food = Mathf.Min(100f, food + eatRestore);
                lastEatTime = Time.time;
                // #122 - raw food = 배부름 only (생식)
                var th = GetComponent<PawnThoughts>();
                if (th != null) th.AddThought("배부름");
            }
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
