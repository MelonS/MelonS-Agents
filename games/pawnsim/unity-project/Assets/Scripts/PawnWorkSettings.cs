using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb #7 + #114 - 레퍼런스 콜로니심 Work tab priority 시스템.
    ///   각 pawn × work type 마다 우선순위 0(disabled)~4(highest).
    ///   PawnUtilityAI 가 Decide 시 priority 높은 work 부터 시도.
    ///
    /// per-pawn 컴포넌트.  WorkTabUI 가 외부에서 setting 변경.
    /// </summary>
    // #작업종류확장(2026-06-03, 다 콜로니심식): the reference sim 처럼 건축/채광/운반/의료를 별도 work
    //  type 으로 분리(이전엔 전부 Chop/Research 슬롯에 묶여 우선순위 개별 조정 불가).  enum 은
    //  반드시 끝에 append — 기존 정수 인덱스 보존(우선순위 dict/저장 호환).  기본 우선순위 1
    //  이라 동작은 보존되고, Work tab 에 건축/채광/운반/의료 열이 추가돼 개별 조정 가능.
    public enum WorkKind { Chop, Gather, Hunt, Cook, Research, Build, Mine, Haul, Doctor }

    public class PawnWorkSettings : MonoBehaviour
    {
        // priority 0 = disabled, 1-4 = 우선순위 (1 highest).  default 모두 1.
        private Dictionary<WorkKind, int> priorities = new Dictionary<WorkKind, int>();

        public static readonly WorkKind[] AllKinds = (WorkKind[])System.Enum.GetValues(typeof(WorkKind));
        // enum WorkKind 와 1:1 순서 일치 필수 (WorkTabUI 열 라벨).
        public static readonly string[] KoreanNames = new[] { "벌목", "채집", "사냥", "요리", "연구", "건축", "채광", "운반", "의료" };

        private void Awake()
        {
            foreach (var k in AllKinds) priorities[k] = DefaultBase;
        }

        // ── 기본 우선순위 (2026-07-29) ──────────────────────────────────────
        //  이전엔 전 칸이 1 이었다.  동작은 됐지만 **화면에서 죽어 있었다** — 작업 탭을
        //  열면 3인 × 9직종 27칸이 전부 같은 숫자라, 이 게임의 핵심인 "플레이어는
        //  정책만 정하고 판단은 콜로니스트가 한다"가 아무것도 보여주지 못했다.
        //
        //  임의로 숫자를 흩뿌리지 않는다.  스킬은 이미 이름 시드로 개인차가 있으므로
        //  (PawnSkills.ReRollFromName) **그 데이터를 그대로 반영**한다 — "잘하는 사람이
        //  먼저 맡는다"는 장르 관례이자, 우리가 내세우는 간접 조작의 근거이기도 하다.
        //
        //  동작 안전성: PawnUtilityAI 는 우선순위 순으로 시도하고 없으면 다음으로
        //  내려간다.  0(비활성)은 만들지 않으므로 **모든 일은 여전히 처리된다** —
        //  바뀌는 것은 순서뿐이다.
        private const int DefaultBase = 3;

        /// <summary>WorkKind → 이 일을 대표하는 스킬.  대응 스킬이 없는 일(운반·요리
        ///  ·연구·의료)은 null 로 두고 아래에서 별도 규칙을 쓴다.</summary>
        private static SkillKind? SkillFor(WorkKind k)
        {
            switch (k)
            {
                case WorkKind.Chop:   return SkillKind.Chop;
                case WorkKind.Gather: return SkillKind.Gather;
                case WorkKind.Build:  return SkillKind.Build;
                case WorkKind.Mine:   return SkillKind.Build;   // 채광도 건축 계열 숙련
                case WorkKind.Hunt:   return SkillKind.Combat;
                default:              return null;
            }
        }

        /// <summary>스킬 기반 기본 우선순위 배정.  스폰 직후 1회 호출 (세이브 로드
        ///  경로는 저장된 값이 항상 우선하므로 호출하지 않는다).</summary>
        public void ApplyDefaultsFromSkills()
        {
            var skills = GetComponent<PawnSkills>();
            foreach (var k in AllKinds)
            {
                int p;
                var sk = SkillFor(k);
                if (sk.HasValue && skills != null)
                {
                    int lv = skills.GetLevel(sk.Value);
                    // 숙련 → 먼저 맡는다.  1 이 가장 높다.
                    p = lv >= 8 ? 1 : lv >= 5 ? 2 : lv >= 3 ? 3 : 4;
                }
                else if (k == WorkKind.Doctor) p = 1;   // 치료는 항상 최우선 (응급)
                else if (k == WorkKind.Cook)   p = 2;   // 식량은 콜로니 생존선
                else if (k == WorkKind.Haul)   p = 3;   // 무숙련 상시 작업
                else                           p = 4;   // 연구 — 급하지 않다
                priorities[k] = p;
            }
        }

        public int GetPriority(WorkKind k) => priorities.TryGetValue(k, out int p) ? p : 1;
        public void SetPriority(WorkKind k, int v) => priorities[k] = Mathf.Clamp(v, 0, 4);
        public bool IsEnabled(WorkKind k) => GetPriority(k) > 0;
    }
}
