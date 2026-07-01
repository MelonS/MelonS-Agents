using UnityEngine;
using UnityEngine.EventSystems;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R8: GenerateGame() 안의 큰 블록들을 helper 로 추출 - 첫 batch (Camera + Singletons).
    public static partial class SceneSetup
    {
        /// <summary>R8: Main Camera + AudioListener + CameraController + DayNightCycle</summary>
        private static Camera SetupCamera()
        {
            GameObject camGo = new GameObject("Main Camera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.18f, 0.13f, 1f);
            cam.orthographic = true;
            // 운영자 피드백 2026-05-27 "프로토타입 수준도 안됨":
            //  ortho 10 일 때 pawn 1 unit / 화면 20 unit = 5% — 너무 작음.
            // #199 A1: pawn 2x2 → 1x1 (the reference sim 동일) 로 줄면서 화면상 절반 크기.
            //  apparent size 유지/향상 위해 ortho 6 → 3.5.  1-unit pawn / 화면 7 unit = 28.6%
            //  (이전 2-unit pawn @ ortho6 = 16.7% 보다 오히려 큼 — 디테일 가독성 ↑).
            //  zoomMax(CameraController) 32 살아있어 wheel 로 60x60 전체 줌아웃 가능.
            // #게임필 배치3(2026-06-10 자율) — 3.5 는 화면 ~13x7 칸: 첫 화면이 '맵'이 아니라
            //  '확대경'이라 게임 컨텍스트(정착지+숲+림 3명)가 안 보였다 (격차 분석 갈래3).
            //  5.5 = ~20x11 칸, 림 ~18% — '림 너무 작음'(2026-05-27, ortho10 5%) 과
            //  '게임 같은 첫 화면' 사이 절충.  체감은 운영자 확인 항목.
            // #카메라파리티 (운영자 2026-06-11 "레퍼런스 콜로니심 기본 줌인아웃과 많이 달라, 훨씬
            //  확대되어서 보이고"): 5.5 → 8 (~29x16 칸).  ortho10(5/27 "림 너무 작음")
            //  까지는 안 가는 절충 — 그림자/눈동자/모션이 생겨 작아도 살아 보임.
            // #카메라파리티2 (운영자 2026-06-12, 레퍼런스 실플레이 스샷 대조): 레퍼런스
            //  기본 뷰는 세로 ~30타일('콜로니 전경')인데 8 은 세로 16타일('마당')이라
            //  캐릭터/벽/나무가 전부 2배 거대해 보였다.  15 = 세로 30칸(레퍼런스 실측
            //  ~33칸) — 보통 플레이 뷰.  줌인 하한 5.5(레퍼런스 최대 줌인 = 세로 ~11칸).
            //  5/27 '림 너무 작음'(ortho10)은 16px 무그림자 시절 — 32px+그림자+애니로
            //  작아도 식별된다.  체감은 운영자 확인 항목.
            cam.orthographicSize = 15f;
            camGo.tag = "MainCamera";
            // pawn 그룹 (-1.5/0.5/2.5, 0.5) 중심 + 정착지 살짝 위 → (0.5, 1.0)
            camGo.transform.position = new Vector3(0.5f, 1.0f, -10);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraController>();   // Day 8: WASD pan + 휠 zoom + Shift fast-pan
            camGo.AddComponent<DayNightCycle>();      // Day 9: time-of-day tint
            return cam;
        }

        /// <summary>R8: EventSystem / TimeController / GameClock / NightOverlay 4종 singleton</summary>
        private static void SetupCoreSingletons()
        {
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();

            GameObject tcGo = new GameObject("TimeController");
            tcGo.AddComponent<TimeController>();      // Day 8: 1x/2x/4x + Space pause

            GameObject gcGo = new GameObject("GameClock");
            gcGo.AddComponent<GameClock>();           // Day 9: day/hour/minute

            GameObject noGo = new GameObject("NightOverlay");
            noGo.AddComponent<NightOverlay>();        // Day 43: 야간 alpha overlay
        }
    }
}
