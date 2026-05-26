using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 45 — RimWorld-style body part health system.
    /// 6 parts: head, torso, leftArm, rightArm, leftLeg, rightLeg.
    /// Each part has maxHp and currentHp.  Damage routes to a chosen part
    /// (random by default, weighted: torso 35%, legs 20%, arms 20%, head 5%).
    /// Effects:
    ///   - head HP=0  → death
    ///   - torso HP=0 → death
    ///   - head HP<30% → downed (consciousness loss)
    ///   - leg damage → movement speed multiplier (linear)
    ///   - arm damage → manipulation speed multiplier (linear)
    /// Bleeding: per-part bleed rate accumulates over time, drains overall.
    /// Bandaged parts stop bleeding.
    /// </summary>
    public class PawnHealth : MonoBehaviour
    {
        public enum PartId { Head=0, Torso=1, LeftArm=2, RightArm=3, LeftLeg=4, RightLeg=5 }

        [System.Serializable]
        public class BodyPart
        {
            public PartId id;
            public string nameKr;
            public int maxHp;
            public int hp;
            public float bleedRate;   // HP/sec drain when bleeding
            public bool bandaged;
            public bool isVital;       // head/torso — death if hp == 0
            public BodyPart(PartId id, string nameKr, int max, bool vital)
            {
                this.id = id; this.nameKr = nameKr;
                this.maxHp = max; this.hp = max;
                this.bleedRate = 0f; this.bandaged = false;
                this.isVital = vital;
            }
        }

        public BodyPart[] parts;
        public bool IsDead { get; private set; }
        public bool IsDowned { get; private set; }
        public float TotalHpRatio { get; private set; }   // 0..1 — for floating bar

        private float lastBleedTick = -10f;

        private void Awake()
        {
            parts = new BodyPart[]
            {
                new BodyPart(PartId.Head,     "머리",   10, true),
                new BodyPart(PartId.Torso,    "몸통",   30, true),
                new BodyPart(PartId.LeftArm,  "왼팔",   18, false),
                new BodyPart(PartId.RightArm, "오른팔", 18, false),
                new BodyPart(PartId.LeftLeg,  "왼다리", 20, false),
                new BodyPart(PartId.RightLeg, "오른다리", 20, false),
            };
            RecomputeAggregates();
        }

        private void Update()
        {
            if (IsDead) return;
            // Bleed ticks once per second
            if (Time.time - lastBleedTick > 1f)
            {
                lastBleedTick = Time.time;
                bool anyBleed = false;
                foreach (var p in parts)
                {
                    if (p.bandaged) continue;
                    if (p.bleedRate <= 0f) continue;
                    anyBleed = true;
                    // Spread bleed damage across vital parts proportionally —
                    //  bleed drains the source part itself first then random
                    //  redistribution to torso/head if part is below 30%.
                    p.hp = Mathf.Max(0, p.hp - Mathf.CeilToInt(p.bleedRate));
                    // Heal-rate slow: bleeding parts slowly stop bleeding over time
                    p.bleedRate = Mathf.Max(0f, p.bleedRate - 0.05f);
                }
                if (anyBleed) RecomputeAggregates();
                CheckDeath();
            }
        }

        /// <summary>
        /// Apply damage to a random body part (weighted) or to a specified one.
        /// Returns the affected part.  Sets bleedRate proportional to damage.
        /// </summary>
        public BodyPart TakeDamage(int dmg, PartId? preferPart = null)
        {
            if (IsDead || dmg <= 0) return null;
            BodyPart target = preferPart.HasValue ? GetPart(preferPart.Value) : PickRandomPart();
            target.hp = Mathf.Max(0, target.hp - dmg);
            // Wound bleed: bigger damage on smaller part = relatively worse bleed
            float baseBleed = dmg * 0.25f;
            if (target.id == PartId.Head || target.id == PartId.Torso) baseBleed *= 1.5f;
            target.bleedRate += baseBleed;
            // Cap bleed so a single mega-hit doesn't kill in seconds
            target.bleedRate = Mathf.Min(target.bleedRate, 3.0f);
            RecomputeAggregates();
            CheckDeath();
            return target;
        }

        public void Bandage(PartId id)
        {
            var p = GetPart(id);
            if (p == null) return;
            p.bleedRate = 0f;
            p.bandaged = true;
        }

        public void HealAll(int amount)
        {
            foreach (var p in parts)
            {
                p.hp = Mathf.Min(p.maxHp, p.hp + amount);
            }
            RecomputeAggregates();
        }

        private BodyPart PickRandomPart()
        {
            // Weighted: torso 35, legs 20+20, arms 10+10, head 5 (total 100)
            int roll = Random.Range(0, 100);
            if (roll < 5)   return parts[(int)PartId.Head];
            if (roll < 40)  return parts[(int)PartId.Torso];
            if (roll < 60)  return parts[(int)PartId.LeftLeg];
            if (roll < 80)  return parts[(int)PartId.RightLeg];
            if (roll < 90)  return parts[(int)PartId.LeftArm];
            return parts[(int)PartId.RightArm];
        }

        public BodyPart GetPart(PartId id)
        {
            return parts[(int)id];
        }

        public float MovementSpeedMultiplier()
        {
            // Each leg contributes 50%.  Lost leg = 50% speed.  Both legs 0 = crawl.
            float lf = parts[(int)PartId.LeftLeg].hp  / (float)parts[(int)PartId.LeftLeg].maxHp;
            float rf = parts[(int)PartId.RightLeg].hp / (float)parts[(int)PartId.RightLeg].maxHp;
            return Mathf.Max(0.10f, 0.5f * lf + 0.5f * rf);
        }

        public float WorkSpeedMultiplier()
        {
            // Each arm contributes 50%.  Both lost = 10% (other limbs / mouth).
            float la = parts[(int)PartId.LeftArm].hp  / (float)parts[(int)PartId.LeftArm].maxHp;
            float ra = parts[(int)PartId.RightArm].hp / (float)parts[(int)PartId.RightArm].maxHp;
            return Mathf.Max(0.10f, 0.5f * la + 0.5f * ra);
        }

        public string SummaryKr()
        {
            // Compact health summary for UI tooltips.
            var s = new System.Text.StringBuilder();
            foreach (var p in parts)
            {
                s.Append($"{p.nameKr}:{p.hp}/{p.maxHp}");
                if (p.bleedRate > 0f) s.Append("출혈");
                if (p.bandaged) s.Append("붕대");
                s.Append("  ");
            }
            return s.ToString().TrimEnd();
        }

        private void RecomputeAggregates()
        {
            int totalMax = 0; int totalCur = 0;
            foreach (var p in parts) { totalMax += p.maxHp; totalCur += p.hp; }
            TotalHpRatio = totalMax > 0 ? (float)totalCur / totalMax : 0f;
        }

        private void CheckDeath()
        {
            BodyPart head  = parts[(int)PartId.Head];
            BodyPart torso = parts[(int)PartId.Torso];
            if (head.hp <= 0 || torso.hp <= 0)
            {
                IsDead = true;
                IsDowned = true;
                Debug.Log($"[PawnHealth] {gameObject.name} 사망 — 머리={head.hp} 몸통={torso.hp}");
                // Disable AI components on death
                foreach (var mb in GetComponents<MonoBehaviour>())
                {
                    if (mb == this) continue;
                    if (mb is PawnFloatingBars) continue;
                    if (mb is SpriteRenderer) continue;
                    mb.enabled = false;
                }
                return;
            }
            // Downed: head <30% triggers unconsciousness
            IsDowned = (head.hp < head.maxHp * 0.3f);
        }
    }
}
