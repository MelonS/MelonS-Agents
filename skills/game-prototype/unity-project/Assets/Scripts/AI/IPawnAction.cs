using UnityEngine;

namespace MelonS.GameProto.AI
{
    /// <summary>
    /// R5 - Pawn utility AI 의 단일 action 추상화.
    /// 각 action 은 자신이 가능한지 판단 (eligibility) + 시작 (execute) 둘 다 담당.
    /// PawnUtilityAI.Decide() 는 priority 순으로 IPawnAction 리스트를 돌며
    /// 첫 TryStart 가 true 를 반환할 때까지 시도.
    ///
    /// 이전: 285L if-else 사슬, 새 action = 함수 가운데 분기 추가
    /// 이후: 작은 class 6+ 개, 새 action = 새 class
    /// </summary>
    public interface IPawnAction
    {
        /// <summary>
        /// 이 action 을 시작할 조건이 충족되면 PawnContext 의 movement/chopper/etc.
        /// 에 target 을 박고 true 반환.  조건 미충족이면 false 반환 (다음 action 시도).
        /// </summary>
        bool TryStart(PawnContext ctx);

        /// <summary>한글 디버그 라벨 — 로그/UI 표시용.</summary>
        string DisplayName { get; }
    }
}
