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
            // Day 40: 40x40 맵에 맞춰 ortho 10 (vertical ±10, horizontal ±17.8).
            //  맵 전체는 ±20이라 WASD pan 으로 봐야 — 림월드 vibe.
            cam.orthographicSize = 10f;
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0, 0, -10);
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
