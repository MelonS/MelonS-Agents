using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb #7 + #114 - 림월드 Work tab priority 시스템.
    ///   각 pawn × work type 마다 우선순위 0(disabled)~4(highest).
    ///   PawnUtilityAI 가 Decide 시 priority 높은 work 부터 시도.
    ///
    /// per-pawn 컴포넌트.  WorkTabUI 가 외부에서 setting 변경.
    /// </summary>
    public enum WorkKind { Chop, Gather, Hunt, Cook, Research }

    public class PawnWorkSettings : MonoBehaviour
    {
        // priority 0 = disabled, 1-4 = 우선순위 (1 highest).  default 모두 1.
        private Dictionary<WorkKind, int> priorities = new Dictionary<WorkKind, int>();

        public static readonly WorkKind[] AllKinds = (WorkKind[])System.Enum.GetValues(typeof(WorkKind));
        public static readonly string[] KoreanNames = new[] { "벌목", "채집", "사냥", "요리", "연구" };

        private void Awake()
        {
            foreach (var k in AllKinds) priorities[k] = 1;
        }

        public int GetPriority(WorkKind k) => priorities.TryGetValue(k, out int p) ? p : 1;
        public void SetPriority(WorkKind k, int v) => priorities[k] = Mathf.Clamp(v, 0, 4);
        public bool IsEnabled(WorkKind k) => GetPriority(k) > 0;
    }
}
