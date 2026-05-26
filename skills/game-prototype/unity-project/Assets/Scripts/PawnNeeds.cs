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
        [SerializeField] private float foodDecay = 0.5f;
        [SerializeField] private float sleepDecay = 0.3f;
        [SerializeField] private float moodDecay = 0.2f;

        [Header("Day 9+: sleep regen when sleeping at night")]
        [SerializeField] private float sleepRegenAtNight = 8f;

        [Header("Day 11: eat-from-stockpile")]
        [SerializeField] private float eatThreshold = 40f;
        [SerializeField] private float eatRestore = 30f;
        [SerializeField] private float eatTickInterval = 0.5f;
        private float lastEatTime = -999f;

        public bool IsSleeping { get; private set; }

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

            // Day 10: when sleep is low AND it's night, pawn sleeps in place.
            // Sleep regenerates fast, food + mood still decay (mildly).
            if (sleep < 30f && night)
            {
                IsSleeping = true;
                sleep = Mathf.Min(100f, sleep + sleepRegenAtNight * dt);
                food  = Mathf.Max(0f, food  - foodDecay * 0.5f * dt);
                mood  = Mathf.Max(0f, mood  - moodDecay * 0.5f * dt);
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
            if (rm == null || rm.food <= 0) return;

            // Spend 1 food unit from stockpile, restore eatRestore on this pawn (clamped to 100).
            rm.AddFood(-1);
            food = Mathf.Min(100f, food + eatRestore);
            lastEatTime = Time.time;
        }

        private bool IsOnFloor()
        {
            var hits = Physics2D.OverlapBoxAll(transform.position, Vector2.one * 0.3f, 0f);
            foreach (var h in hits)
                if (h != null && h.GetComponent<FloorEntity>() != null) return true;
            return false;
        }

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
