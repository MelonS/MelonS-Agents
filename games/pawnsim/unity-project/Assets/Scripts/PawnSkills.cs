using System;
using UnityEngine;

namespace MelonS.GameProto
{
    public enum SkillKind { Gather, Chop, Build, Combat }

    /// <summary>
    /// Day 19: per-pawn skill levels + XP.
    /// 4 skills (채집/벌목/건축/전투), levels 0-20 (콜로니심풍이지만
    /// 보편 RPG 패턴 — 저작권 안전).  XP gain → Level via log curve:
    ///   xp_to_next(L) = 1000 * L^1.5   (#200 the reference sim ~1000 XP/level)
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

        /// <summary>시작 스킬 개인차 (게임루프 차순위 2026-06-12) — 전 림이 4/4/8/0
        ///  동일 시작이라 '누가 뭘 잘하나'가 없던 것.  PawnTraits.ReRollFromName 과
        ///  같은 이름-시드 결정론(같은 이름 = 같은 시작 스킬).  스폰/합류 지점에서
        ///  호출 — 로드 경로는 호출하지 않으며 skillLevels 복원이 항상 우선한다.</summary>
        public void ReRollFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            int h = name.GetHashCode();
            for (int i = 0; i < entries.Length; i++)
            {
                int delta = ((h >> (i * 5)) & 7) - 2;   // 스킬별 결정론 변동 -2..+5
                entries[i].level = Mathf.Clamp(entries[i].level + delta, 0, 12);
                entries[i].xp = 0f;
            }
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
            // #버그헌트4(2026-06-05): 전투 XP 에 PawnTraits.combatXpMul(Bloodthirsty/호전적 +50%) 반영.
            //  이전엔 combatXpMul 이 계산만 되고 어떤 AddXP 호출도 소비 안 하는 dead trait effect 였다.
            //  Combat XP 의 단일 통로인 여기서 1회 적용해 7개 호출처(PawnEntity/Hunter/UtilityAI) 전부 커버.
            if (k == SkillKind.Combat)
            {
                var tr = GetComponent<PawnTraits>();
                if (tr != null) amount *= tr.combatXpMul;
            }
            var e = entries[(int)k];
            e.xp += amount;
            int safety = 0;
            while (e.xp >= XPToLevel(e.level + 1) && e.level < 20 && safety++ < 20)
            {
                e.xp -= XPToLevel(e.level + 1);
                e.level++;
                OnLevelUp?.Invoke(k, e.level);
                // 백로그 #7 — 레벨업이 보인다 (OnLevelUp 구독자 0 이던 것의 최소 배선).
                FloatingText.Spawn(transform.position + UnityEngine.Vector3.up * 0.7f,
                    $"{KindKr(k)} Lv{e.level}!", new UnityEngine.Color(0.55f, 0.85f, 1f, 1f));
            }
        }

        private static float XPToLevel(int level)
        {
            // #200 genre fidelity: the reference sim costs ~1000 XP/level early on.
            //  Was 100*L^1.5 → L0→1 = 100 XP (~10x too cheap, pawns blew through
            //  low levels in seconds).  Base 100→1000 keeps the curve shape but
            //  makes early progression earned.  L0→1 = 1000, L1→2 = 2828, L4→5 = 11180.
            // 게임루프 백로그 #7 (2026-06-11): 1000 커브는 L8→9 건축 90분 연속 = 한
            //  세션 레벨업 0회 — 성장이 결정에 닿지 않았다.  200 = 1세션 1~2렙 목표.
            return 200f * Mathf.Pow(level, 1.5f);
        }

        private static string KindKr(SkillKind k) => k switch
        {
            SkillKind.Gather => "채집",
            SkillKind.Chop   => "벌목",
            SkillKind.Build  => "건축",
            SkillKind.Combat => "전투",
            _ => k.ToString(),
        };
    }
}
