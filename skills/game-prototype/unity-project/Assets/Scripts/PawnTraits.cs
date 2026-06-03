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
            RollTraits();
            ApplyToHealth();
        }

        private void RollTraits()
        {
            // Deterministic per-pawn-name hash so save/load gives same traits.
            string n = gameObject.name;
            int seed = 0;
            foreach (char ch in n) seed = unchecked(seed * 31 + ch);
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
            var health = GetComponent<PawnHealth>();
            if (health == null || health.parts == null) return;
            foreach (var p in health.parts)
            {
                int newMax = Mathf.Max(1, Mathf.RoundToInt(p.maxHp * maxHpMul));
                int newCur = Mathf.Max(1, Mathf.RoundToInt(p.hp * maxHpMul));
                p.maxHp = newMax;
                p.hp = Mathf.Min(newMax, newCur);
            }
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
