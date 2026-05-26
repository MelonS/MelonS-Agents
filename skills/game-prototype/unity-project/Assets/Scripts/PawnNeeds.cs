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

        private void Update()
        {
            float dt = Time.deltaTime;
            food = Mathf.Max(0f, food - foodDecay * dt);
            sleep = Mathf.Max(0f, sleep - sleepDecay * dt);
            mood = Mathf.Max(0f, mood - moodDecay * dt);
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
