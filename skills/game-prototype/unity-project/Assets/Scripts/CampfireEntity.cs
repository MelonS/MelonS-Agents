using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>광장 모닥불 — **클릭하면 무엇인지 알려주는** 최소 실체.
    ///
    /// 계기 (2026-08-01 운영자): "이 빛은 먼데? 눌러도 정보도 없고".
    ///
    /// 실측하니 모닥불은 `glow_fire_pool.png`(광원 이미지)만 얹은 빈 GameObject 였다.
    ///  · 실체가 없으니 잔디 위에 주황 빛만 떠 있어 무엇인지 알 수 없고,
    ///  · 콜라이더가 없으니 클릭해도 정보창이 뜨지 않는다.
    /// 화면에 있는 것은 전부 '무엇인지 물어볼 수 있어야' 한다 — 물어봐도 답이 없는
    /// 물체는 플레이어에게 '고장' 으로 읽힌다(같은 이유로 죽은 버튼을 고친 전례가 있다).
    ///
    /// 게임 규칙은 건드리지 않는다.  여가(Joy) 행동이 이미 이름으로 모닥불을 찾으므로
    /// (`PawnActions.FindCampfire` → FlickerLight + 이름), 이 컴포넌트는 순수하게
    /// '식별 가능한 물체' 만 담당한다.</summary>
    [DisallowMultipleComponent]
    public class CampfireEntity : MonoBehaviour
    {
        /// <summary>정보창 제목.</summary>
        public string DisplayName => "모닥불";

        /// <summary>정보창 본문 — 왜 여기 있는지, 무엇에 쓰는지.</summary>
        public string Description =>
            "마당 한가운데 피워 둔 불\n"
            + "여가 시간이 되면 주민들이 곁에 모여 쉰다\n"
            + "가까이 있으면 기분이 조금 오른다";
    }
}
