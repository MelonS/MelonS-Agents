using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #버그헌트2(2026-06-04): 빌드로 완성된 구조물에 빌드 Mode 를 스탬프한다.  save/load 가
    /// reverse-mapping(엔티티 타입→Mode 역추론) 없이 (mode, position) 만으로 모든 플레이어
    /// 구조물을 BuildManager.SpawnFinished 로 동일하게 재구성하게 해, 게임 재시작 후 로드 시
    /// 벽/침대/문/화덕/램프/울타리 등이 소실되던 CRITICAL 버그를 해결한다.
    /// BuildManager.SpawnFinished 가 부착(빌드 완료·save 재구성 양쪽 단일 경로).
    /// </summary>
    public class StructureTag : MonoBehaviour
    {
        public int modeInt = -1;   // (int)BuildManager.Mode
    }
}
