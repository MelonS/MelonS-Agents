using System;
using UnityEngine;

namespace MelonS.GameProto
{
    public enum SkillKind { Gather, Chop, Build, Combat }

    /// <summary>
    /// Day 19: per-pawn skill levels + XP.
    /// 4 skills (채집/벌목/건축/전투), levels 0-20 (RimWorld 풍이지만
    /// 보편 RPG 패턴 — 저작권 안전).  XP gain → Level via log curve:
    ///   xp_to_next(L) = 100 * (L+1)^1.5
    /// Hook points: PawnGatherer (Gather), PawnChopper (Chop),
    /// BuildManager.TryPlace (Build via static helper), bandit hit
    /// (Combat via PawnEntity counter-attack).
    /// </summary>
    public class PawnSkills : MonoBehaviour
    {
        [Serializable] public class SkillEntry { public SkillKind kind; public int level; public float xp; }

        public SkillEntry[] entries = new SkillEntry[4];

        public event Action<SkillKind, int> OnLevelUp;

        private void Awake()
        {
            for (int i = 0; i < entries.Length; i++)
                entries[i] = new SkillEntry { kind = (SkillKind)i, level = 0, xp = 0f };
        }

        public int GetLevel(SkillKind k) => entries[(int)k].level;
        public float GetXP(SkillKind k) => entries[(int)k].xp;
        public float GetXPToNext(SkillKind k) => XPToLevel(entries[(int)k].level + 1);

        public void AddXP(SkillKind k, float amount)
        {
            if (amount <= 0) return;
            var e = entries[(int)k];
            e.xp += amount;
            int safety = 0;
            while (e.xp >= XPToLevel(e.level + 1) && e.level < 20 && safety++ < 20)
            {
                e.xp -= XPToLevel(e.level + 1);
                e.level++;
                OnLevelUp?.Invoke(k, e.level);
            }
        }

        private static float XPToLevel(int level)
        {
            return 100f * Mathf.Pow(level, 1.5f);
        }
    }
}
