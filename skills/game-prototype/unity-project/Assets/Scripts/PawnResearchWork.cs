using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>이 콜로니스트가 **지금 연구를 하고 있는가**를 나타내는 단일 정본.
    ///
    /// 계기 (2026-07-31 운영자): "동작 하나하나에 의미가 있어야 하는데 뭐 하고 있는건지
    /// 모르겠음."  머리 위 활동 라벨을 전원 상시 표시로 바꾸고 나서, 연구 진행도는
    /// 40/100 으로 오르는데 세 사람 모두 라벨은 "떠도는중"인 화면이 찍혔다.  파고 보니
    /// 두 곳이 서로 다른 것을 보고 있었다:
    ///
    ///   · 진행 판정 `ResearchBench.ResearcherSpeedSum()` — **반경 안의 살아있는 폰 전부**를
    ///     센다.  연구와 무관한 요리사가 집 안에 서 있어도 연구가 오른다.  시작 집 안에
    ///     연구대가 있으니 사실상 "실내에 있으면 연구 중"이었다.
    ///   · 라벨 판정 `PawnNameLabel.IsResearchingHere()` — 반경 + **정지 상태**를 요구한다.
    ///     연구자가 벤치 옆에서 한 발짝 움직이는 순간 "떠도는중"으로 떨어진다.
    ///
    /// 둘 다 실제 작업 배정과 무관한 **위치 기반 추정**이라, 화면과 숫자가 어긋났다.
    /// 그리고 더 나쁜 것은 직업 탭의 '연구' 우선순위가 실효를 잃는다는 점이다 — 누가
    /// 연구를 맡든 결과가 같으면 그 열은 장식이다.  이 게임이 내세우는 것이 간접 조작인데.
    ///
    /// 그래서 판정을 **작업 배정 자체**로 옮긴다.  `DoResearchAction` 이 이 컴포넌트에
    /// 도장을 찍고, 진행도와 라벨이 둘 다 그 도장 하나만 본다.  세 곳이 같은 사실을 말한다.
    ///
    /// 도장은 시각이고 유효기간이 있다.  유틸리티 AI 는 `decisionInterval`(1.5초)마다
    /// 결정하므로, 그보다 넉넉한 창을 둬야 결정과 결정 사이에 "연구 중 아님"으로 깜빡이지
    /// 않는다.  연구를 그만두면 도장이 갱신되지 않으니 창이 지나면서 자연히 꺼진다.</summary>
    public class PawnResearchWork : MonoBehaviour
    {
        // decisionInterval(1.5s)의 2배.  한 번의 결정 누락으로 깜빡이지 않을 만큼 길고,
        //  일을 바꾼 뒤에도 계속 "연구 중"으로 보일 만큼 길지는 않다.
        private const float FreshSec = 3f;

        private float lastMarked = -999f;

        /// <summary>이번 결정에서 연구를 작업으로 잡았다 (DoResearchAction 이 호출).</summary>
        public void Mark() => lastMarked = Time.time;

        /// <summary>지금 연구 중인가 — 진행도 적립과 머리위 라벨이 **함께** 보는 값.</summary>
        public bool IsResearching => Time.time - lastMarked <= FreshSec;
    }
}
