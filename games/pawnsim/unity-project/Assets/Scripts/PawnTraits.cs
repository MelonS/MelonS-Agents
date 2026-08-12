using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 56 — Pawn personality traits (림 바닐라 vibe).
    /// Each pawn gets 1-2 random traits at spawn that affect:
    ///   - mood baseline (additive 0-100 cap)
    ///   - movement speed multiplier
    ///   - work speed multiplier (gather/chop/build/cook)
    ///   - combat XP gain multiplier
    ///   - max HP multiplier (applied at Awake on PawnHealth parts)
    ///   - meal mood bonus (+/-)
    /// Traits are visible in PawnInfoPanel (concatenated Korean labels).
    /// Deterministic per-pawn via name hash so save/load is stable.
    /// </summary>
    public class PawnTraits : MonoBehaviour
    {
        public enum Trait
        {
            Cheerful,      // 활기차다 — mood +10 baseline, move +20%
            Lazy,          // 게으르다 — work -25%, mood -8
            Industrious,   // 부지런하다 — work +30%, mood +5
            Bloodthirsty,  // 호전적이다 — combat XP +50%, kill mood +5
            Frail,         // 약골 — HP max -25%
            Tough,         // 강골 — HP max +35%
            Gourmand,      // 미식가 — meal +15 mood (배선됨: PawnNeeds mealMoodBonus)
            Stoic,         // 무던하다 — mood swing ÷2 (안정적)
        }

        private static readonly string[] TraitLabels = new[]
        {
            "활기차다", "게으르다", "부지런하다", "호전적이다",
            "약골", "강골", "미식가", "무던하다",
        };

        public List<Trait> ActiveTraits { get; private set; } = new List<Trait>();

        public float moveSpeedMul = 1f;
        public float workSpeedMul = 1f;
        public float combatXpMul = 1f;
        public float maxHpMul = 1f;
        public float moodBaselineBonus = 0f;
        public float mealMoodBonus = 0f;
        public float moodSwingMul = 1f;

        private void Awake()
        {
            RollTraits(gameObject.name);
            ApplyToHealth();
        }

        // #트레잇 결정성(2026-06-03): 시드 키를 인자로 받는다.  이전엔 항상 gameObject.name
        //  ("Pawn(Clone)" — 모든 클론 동일)으로 굴려 전(全) 콜로니스트가 똑같은 트레잇이었다
        //  (변종성 결여, 클래스 doc "각 pawn 1-2 랜덤 트레잇" 의도 위반).  GameManager/load 가
        //  이름 설정 직후 ReRollFromName(고유 이름)으로 재시드 → 변종성 + save/load 결정성.
        //  Awake 는 기본(gameObject.name) 유지 — 테스트/직접 생성 호환.
        private void RollTraits(string seedKey)
        {
            int seed = 0;
            foreach (char ch in seedKey) seed = unchecked(seed * 31 + ch);
            System.Random rng = new System.Random(seed);
            // 50% chance of 2 traits, else 1.
            int count = (rng.NextDouble() < 0.5) ? 2 : 1;
            var pool = new List<Trait>((Trait[])System.Enum.GetValues(typeof(Trait)));
            for (int i = 0; i < count; i++)
            {
                if (pool.Count == 0) break;
                int idx = rng.Next(pool.Count);
                Trait t = pool[idx];
                pool.RemoveAt(idx);
                ActiveTraits.Add(t);
                Apply(t);
            }
        }

        private void Apply(Trait t)
        {
            switch (t)
            {
                case Trait.Cheerful:
                    moodBaselineBonus += 10f;
                    moveSpeedMul *= 1.20f;
                    break;
                case Trait.Lazy:
                    workSpeedMul *= 0.75f;
                    moodBaselineBonus -= 8f;
                    break;
                case Trait.Industrious:
                    workSpeedMul *= 1.30f;
                    moodBaselineBonus += 5f;
                    break;
                case Trait.Bloodthirsty:
                    combatXpMul *= 1.5f;
                    break;
                case Trait.Frail:
                    maxHpMul *= 0.75f;
                    break;
                case Trait.Tough:
                    maxHpMul *= 1.35f;
                    break;
                case Trait.Gourmand:
                    mealMoodBonus += 15f;
                    break;
                case Trait.Stoic:
                    moodSwingMul *= 0.5f;
                    break;
            }
        }

        private void ApplyToHealth()
        {
            // #트레잇 결정성: base(config 기본)에서 maxHpMul 을 멱등 적용 — 재roll 시 이중 스케일 없음.
            var health = GetComponent<PawnHealth>();
            if (health != null) health.ApplyMaxHpMul(maxHpMul);
        }

        // #트레잇 결정성: 트레잇 효과/목록을 기본값으로 리셋(재roll 전).
        private void ResetTraitEffects()
        {
            ActiveTraits.Clear();
            moveSpeedMul = 1f; workSpeedMul = 1f; combatXpMul = 1f; maxHpMul = 1f;
            moodBaselineBonus = 0f; mealMoodBonus = 0f; moodSwingMul = 1f;
        }

        /// <summary>#트레잇 결정성 — 스폰/로드 시 pawn '이름'으로 트레잇을 재시드.  Awake 의 기본
        /// roll(gameObject.name, 전원 동일)을 덮어써 콜로니스트마다 다른 트레잇 + 같은 이름이면
        /// save/load 후에도 동일(결정성).  HP 는 base 에서 멱등 재적용이라 이중 스케일 없음.
        /// GameManager/GameSaveButtons 가 pawnName 설정 직후 호출.</summary>
        public void ReRollFromName(string nameKey)
        {
            if (string.IsNullOrEmpty(nameKey)) return;
            ResetTraitEffects();
            RollTraits(nameKey);
            ApplyToHealth();
        }

        public string SummaryKr()
        {
            if (ActiveTraits.Count == 0) return "";
            var labels = new List<string>();
            foreach (var t in ActiveTraits) labels.Add(TraitLabels[(int)t]);
            return string.Join(", ", labels);
        }
    }
}
