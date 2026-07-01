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
            foreach (var k in AllKinds) priorities[k] = 1;
        }

        public int GetPriority(WorkKind k) => priorities.TryGetValue(k, out int p) ? p : 1;
        public void SetPriority(WorkKind k, int v) => priorities[k] = Mathf.Clamp(v, 0, 4);
        public bool IsEnabled(WorkKind k) => GetPriority(k) > 0;
    }
}
