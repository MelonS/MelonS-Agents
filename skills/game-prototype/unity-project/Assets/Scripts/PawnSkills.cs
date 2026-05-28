using System;
using UnityEngine;

namespace MelonS.GameProto
{
    public enum SkillKind { Gather, Chop, Build, Combat }

    /// <summary>
    /// Day 19: per-pawn skill levels + XP.
    /// 4 skills (채집/벌목/건축/전투), levels 0-20 (RimWorld 풍이지만
    /// 보편 RPG 패턴 — 저작권 안전).  XP gain → Level via log curve:
    ///   xp_to_next(L) = 1000 * L^1.5   (#200 RimWorld ~1000 XP/level)
    /// Hook points: PawnGatherer (Gather), PawnChopper (Chop),
    /// BuildManager.TryPlace (Build via static helper), bandit hit
    /// (Combat via PawnEntity counter-attack).
    /// </summary>
    public class PawnSkills : MonoBehaviour
    {
        [Serializable] public class SkillEntry { public SkillKind kind; public int level; public float xp; }

        public SkillEntry[] entries = new SkillEntry[4];

        public event Action<SkillKind, int> OnLevelUp;

        // #196 - 운영자 fb "초기 build skill 높은 수준으로".
        //  Build 8 = +32% 작업 속도 (#177 +4%/lvl).  건축 빠르게 진행.
        public const int InitialBuildLevel = 8;
        public const int InitialChopLevel = 4;
        public const int InitialGatherLevel = 4;

        private void Awake()
        {
            entries[0] = new SkillEntry { kind = SkillKind.Gather, level = InitialGatherLevel, xp = 0f };
            entries[1] = new SkillEntry { kind = SkillKind.Chop,   level = InitialChopLevel,   xp = 0f };
            entries[2] = new SkillEntry { kind = SkillKind.Build,  level = InitialBuildLevel,  xp = 0f };
            entries[3] = new SkillEntry { kind = SkillKind.Combat, level = 0, xp = 0f };
        }

        /// <summary>#196 - 외부에서 skill level 강제 설정 (테스트/시작값).</summary>
        public void SetLevel(SkillKind k, int level)
        {
            entries[(int)k].level = Mathf.Clamp(level, 0, 20);
            entries[(int)k].xp = 0f;
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
            // #200 RimWorld fidelity: RimWorld costs ~1000 XP/level early on.
            //  Was 100*L^1.5 → L0→1 = 100 XP (~10x too cheap, pawns blew through
            //  low levels in seconds).  Base 100→1000 keeps the curve shape but
            //  makes early progression earned.  L0→1 = 1000, L1→2 = 2828, L4→5 = 11180.
            return 1000f * Mathf.Pow(level, 1.5f);
        }
    }
}
