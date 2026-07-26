using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MelonS.GameProto
{
    /// <summary>
    /// WORKFLOW-V2 규칙 2 — "검증은 운영자와 같은 레이어".
    ///
    /// 게임플레이 입력(ClickSelector 등)이 UnityEngine.Input 을 직접 읽으면
    /// 자동 재현 harness 가 운영자의 클릭 경로를 탈 수 없다 (OS 이벤트는 위조 불가).
    /// 이 정적 클래스가 그 한 겹의 추상화:
    ///   - 평시: UnityEngine.Input 패스스루 (zero behavior change)
    ///   - 시뮬 모드(ReproHarness 가 BeginFrame 주입): 주입된 좌표/버튼/키 반환
    ///
    /// 핵심 보장 — 시뮬 클릭은 ClickSelector 의 *진짜* 경로를 그대로 통과한다:
    /// ScreenToWorldPoint 변환 → PickEntityAt(OverlapPointAll+tie-break) → 메뉴/지정.
    /// API 직접 호출(crop.Harvest() 류)과 달리, 운영자가 보는 버그가 이 레이어에서
    /// 그대로 재현된다.
    ///
    /// overUI 체크: 실커서는 시뮬 좌표와 무관한 곳에 있을 수 있으므로, 시뮬 모드에선
    /// EventSystem.RaycastAll(시뮬 좌표) 로 판정한다 (Input module 과 동일 의미).
    ///
    /// 주입은 1프레임 유효 — ReproHarness 가 매 프레임 BeginFrame/EndFrame 호출.
    /// </summary>
    public static class SimInput
    {
        public static bool SimActive { get; private set; }

        private static Vector3 simMousePos;
        private static bool simMouseWorldMode;     // #38 — 월드좌표 주입 모드
        private static Vector3 simMouseWorld;
        private static readonly bool[] simMouseDown = new bool[3];
        // 기본기 진단 (2026-06-12) — designation 7종 SimInput 전환에 필요한 release 시뮬.
        //  주입 클릭의 자연 수명: down(프레임 N+1) → up(프레임 N+2, ClearFrame 이 스탬프).
        //  upFrame 비교 방식이라 그 프레임이 지나면 자동 소멸 — 매 프레임 release 스팸 없음.
        private static readonly int[] simMouseUpFrame = { -1, -1, -1 };
        private static readonly HashSet<KeyCode> simKeysDown = new HashSet<KeyCode>();

        // ── ReproHarness 전용 주입 API ─────────────────────────────────────
        public static void BeginSim() { SimActive = true; ClearFrame(); }
        public static void EndSim() { SimActive = false; ClearFrame(); }

        /// <summary>이번 프레임의 시뮬 입력 상태를 설정. 다음 BeginFrame 까지 유효.</summary>
        public static void FrameMouseDown(int button, Vector3 screenPos)
        {
            simMousePos = screenPos;
            if (button >= 0 && button < 3) simMouseDown[button] = true;
        }

        /// <summary>#38(2026-06-10) — 월드좌표 주입.  screen 좌표는 '소비 프레임'에
        ///  Camera 현재 상태로 도출하므로, 주입→소비 사이 카메라 포커스 팬이 끼어도
        ///  재투영이 절대 어긋나지 않는다 (p1-chop-selected-only designations=0 무음
        ///  미스의 근본 해결).  게임 코드는 여전히 mousePosition(screen)을 읽고
        ///  ScreenToWorldPoint 하므로 실경로 보장은 그대로다.</summary>
        public static void FrameMouseDownWorld(int button, Vector3 worldPos)
        {
            simMouseWorldMode = true;
            simMouseWorld = worldPos;
            if (button >= 0 && button < 3) simMouseDown[button] = true;
        }

        public static void FrameKeyDown(KeyCode k) { simKeysDown.Add(k); }
        public static void FrameMousePos(Vector3 screenPos) { simMousePos = screenPos; }
        public static void ClearFrame()
        {
            for (int i = 0; i < 3; i++)
            {
                // 직전 프레임에 down 이었던 버튼은 다음 프레임 1회 release —
                //  designation 의 GetMouseButtonUp(클릭 확정/드래그 종료) 분기가
                //  실플레이와 같은 순서(press→release)로 소비된다.
                if (simMouseDown[i]) simMouseUpFrame[i] = Time.frameCount + 1;
                simMouseDown[i] = false;
            }
            // simMouseWorldMode 는 유지 — release 프레임의 mousePosition(드래그 end
            //  좌표)이 press 와 같은 월드점으로 풀려야 '거리 0 = 클릭' 판정이 성립.
            //  다음 FrameMouseDown*/EndSim 이 자연 덮어쓴다.
            simKeysDown.Clear();
        }

        private static Vector3 CurrentSimScreenPos()
        {
            if (simMouseWorldMode && Camera.main != null)
                return Camera.main.WorldToScreenPoint(simMouseWorld);
            return simMousePos;
        }

        // ── 게임플레이 코드가 읽는 표면 (Input 대체) ───────────────────────
        public static bool GetMouseButtonDown(int button)
            => SimActive ? (button >= 0 && button < 3 && simMouseDown[button])
                         : Input.GetMouseButtonDown(button);

        /// <summary>시뮬 모드의 held 는 down 프레임과 동일(1프레임 클릭) — 드래그
        /// 승격 없이 release 분기의 '거리 0 = 단일 클릭' 경로로 떨어진다.</summary>
        public static bool GetMouseButton(int button)
            => SimActive ? (button >= 0 && button < 3 && simMouseDown[button])
                         : Input.GetMouseButton(button);

        public static bool GetMouseButtonUp(int button)
            => SimActive ? (button >= 0 && button < 3 && Time.frameCount == simMouseUpFrame[button])
                         : Input.GetMouseButtonUp(button);

        public static Vector3 mousePosition
            => SimActive ? CurrentSimScreenPos() : Input.mousePosition;

        public static bool GetKeyDown(KeyCode key)
            => SimActive ? simKeysDown.Contains(key) : Input.GetKeyDown(key);

        /// <summary>IsPointerOverGameObject 대체 — 시뮬 모드에선 시뮬 좌표 기준
        /// UI 레이캐스트 (StandaloneInputModule 의 판정과 동일 의미).</summary>
        public static bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            if (!SimActive) return EventSystem.current.IsPointerOverGameObject();
            var ped = new PointerEventData(EventSystem.current) { position = CurrentSimScreenPos() };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);
            // 진단(2026-06-10) — 주입 클릭이 overUI 로 무음 무효화될 때 차단자를 식별.
            //  버튼 주입 프레임에만 로그 (스팸 방지).  배치2 게이트에서 우클릭이 y~232-250
            //  밴드에서만 죽는 미스터리의 계측 장치 — 해결 후에도 회귀 진단용으로 유지.
            if (results.Count > 0 && (simMouseDown[0] || simMouseDown[1]))
            {
                var top = results[0].gameObject;
                string chain = top != null ? top.name : "?";
                var tr = top != null ? top.transform.parent : null;
                for (int d = 0; tr != null && d < 3; d++) { chain += "<" + tr.name; tr = tr.parent; }
                Debug.Log($"[SimInput] overUI 차단: {chain} (+{results.Count - 1}) at {ped.position}");
            }
            return results.Count > 0;
        }
    }
}
