using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #122 - 레퍼런스 콜로니심 mood thoughts 시스템.
    ///  각 thought = (label, offset, expireSec).  현재 mood 는 base(50) + Σ thoughts.offset.
    ///  PawnNeeds.mood 는 이 값으로 매 frame 업데이트.
    ///  PawnInfoPanel 에 breakdown 표시 ("좋은 식사 +5", "추웠음 -3").
    ///
    /// AddThought("좋은 식사", +5, 600s) - 동일 label 있으면 expire 재설정.
    /// </summary>
    public class PawnThoughts : MonoBehaviour
    {
        [System.Serializable]
        public class Thought
        {
            public string label;
            public float offset;
            public float expireTime;  // Time.time 기준
        }

        public List<Thought> active = new List<Thought>();
        public float baseMood = 50f;

        // 일반적인 thought 카탈로그 (label, offset, durationSec)
        public static readonly (string label, float offset, float dur)[] Catalog = new[] {
            ("최고의 식사",   +12f, 800f),  // #131 - fine meal
            ("맛있는 식사",   +5f, 600f),
            ("배부름",        +3f, 300f),
            ("푹 잠",         +4f, 400f),
            ("침대에서 잠",   +2f, 600f),
            ("따뜻한 실내",   +2f, 300f),
            ("배고픔",        -4f, 120f),
            ("수면 부족",     -3f, 180f),
            ("야외 폭풍",     -6f, 120f),
            ("어두운 밤 작업",-2f,  60f),
            ("부상",          -5f, 600f),
            ("동료 사망",     -15f, 1800f),
            ("아름다운 환경", +3f, 300f),
        };

        /// <summary>
        /// label 의 thought 를 추가하거나 expireTime 갱신.  catalog 에 정의된 label 만 허용.
        /// </summary>
        public void AddThought(string label)
        {
            foreach (var (l, off, dur) in Catalog)
            {
                if (l == label)
                {
                    AddThought(label, off, dur);
                    return;
                }
            }
        }

        public void AddThought(string label, float offset, float durationSec)
        {
            foreach (var t in active)
            {
                if (t.label == label)
                {
                    t.expireTime = Time.time + durationSec;
                    return;
                }
            }
            active.Add(new Thought {
                label = label, offset = offset,
                expireTime = Time.time + durationSec
            });
        }

        public void RemoveThought(string label)
        {
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].label == label) active.RemoveAt(i);
        }

        public float CurrentMood
        {
            get
            {
                float sum = baseMood;
                foreach (var t in active) sum += t.offset;
                return Mathf.Clamp(sum, 0f, 100f);
            }
        }

        private float lastCullTime = -1f;

        private void Update()
        {
            // expire 정리 - 1초마다
            if (Time.time - lastCullTime < 1f) return;
            lastCullTime = Time.time;
            for (int i = active.Count - 1; i >= 0; i--)
                if (Time.time >= active[i].expireTime) active.RemoveAt(i);

            // PawnNeeds.mood 를 thought 합으로 lerp (즉시 set 하면 decay 무시됨 → 50% blend).
            //  단순화: thoughts 가 있으면 needs.mood = CurrentMood (override).
            var needs = GetComponent<PawnNeeds>();
            if (needs != null && active.Count > 0)
            {
                needs.mood = CurrentMood;
            }
        }
    }
}
