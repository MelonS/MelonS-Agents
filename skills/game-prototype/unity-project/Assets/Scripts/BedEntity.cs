using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 피드백 - 침대 만드는 기능 (#107).
    /// 림월드처럼 - pawn 이 침대 위에서 수면 시 회복 속도 1.6x.
    /// PawnNeeds.IsOnBed() 가 OverlapBox 로 검사.
    /// 24x24 (1.5x1 tile) - 일반 floor 보다 약간 큼.
    /// </summary>
    public class BedEntity : MonoBehaviour
    {
        // 단순 marker - PawnNeeds 가 OverlapBox 로 위치 검사.
        //  collider 는 trigger - pawn 통과 가능.
    }
}
