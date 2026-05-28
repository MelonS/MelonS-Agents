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
            // #199 A1: pawn 2x2 → 1x1 (RimWorld 동일) 로 줄면서 화면상 절반 크기.
            //  apparent size 유지/향상 위해 ortho 6 → 3.5.  1-unit pawn / 화면 7 unit = 28.6%
            //  (이전 2-unit pawn @ ortho6 = 16.7% 보다 오히려 큼 — 디테일 가독성 ↑).
            //  zoomMax(CameraController) 32 살아있어 wheel 로 60x60 전체 줌아웃 가능.
            cam.orthographicSize = 3.5f;
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
