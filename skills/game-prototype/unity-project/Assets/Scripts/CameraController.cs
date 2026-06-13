using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 8 polish: WASD/Arrow pan + mouse-wheel zoom (orthographic).
    /// Pan speed scales with zoom level (zoomed-out = bigger steps).
    /// Hold Shift for fast pan.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private float panSpeed = 8f;
        [SerializeField] private float fastPanMultiplier = 2.5f;
        // #매직마우스(운영자 2026-06-14): 실측 진단 — 매직마우스 한 플릭 = 60Hz 로 28~165
        //  개의 스크롤 이벤트(raw 0.005~0.86)를 쏟아낸다.  이전 모델들이 '휙휙' 슬램한 건
        //  이벤트마다 줌을 곱했기 때문.  최종 모델: 스크롤을 pendingZoom(누적 목표 변화,
        //  log 공간)에 비례 적립 후 매 프레임 maxZoomRate×dt 만큼만 소비 → (a) 부드러운
        //  스크롤=부드러운 줌(비례), (b) 빠른/관성 버스트도 초당 maxZoomRate 를 넘지 못함
        //  (슬램 불가), (c) pending 적립이라 60Hz 희소 입력이 1000fps 에서 안 버려진다.
        [SerializeField] private float zoomSensitivity = 0.33f;  // raw 스크롤 1당 ln(ortho) 변화 (강도). 값↑=빠름
        [SerializeField] private float maxZoomRate = 2.5f;       // 줌 속도 상한 ln(ortho)/초 (슬램 방지). 값↓=느림
        // #카메라파리티 (운영자 2026-06-11 "림월드 기본 줌인아웃과 많이 달라"):
        //  zoomMin 1.5(픽셀 깨지는 초근접) → 3, zoomMax 48(맵보다 큰 void 뷰) → 26.
        // #카메라파리티2 (2026-06-12 레퍼런스 스샷 2장 실측) — 기본 줌 15(보통 뷰
        //  세로 30칸) + 줌인 하한 5.5(레퍼런스 최대 줌인 = 세로 ~11칸; 3 은 레퍼런스보다
        //  2배 깊은 픽셀 깨짐 구간) + 줌아웃 상한 32(콜로니+주변 전경).
        [SerializeField] private float zoomMin = 5.5f;
        [SerializeField] private float zoomMax = 32f;
        // #카메라파리티 — 림월드 무빙 질감: 줌은 목표값으로 지수 수렴(즉시 점프 X),
        //  팬은 관성(가속 빠르게/release 후 ~0.3s 드리프트 정지), 줌인은 커서 방향.
        [SerializeField] private float zoomLerpSpeed = 9f;   // 줌 수렴 속도 (1/s)
        [SerializeField] private float panAccel = 14f;       // 입력 → 목표속도 수렴 (1/s)
        [SerializeField] private float panDecay = 6f;        // release 후 감속 (1/s)

        private float targetOrtho = -1f;     // <0 = 줌 입력 전 (스무딩 비활성)
        private float pendingZoom;           // 미소비 줌(log 공간) — maxZoomRate 로 소비
        private bool zoomTowardCursor;       // 줌인일 때만 커서 앵커
        private Vector2 panVel;              // 관성 팬 속도 (world units/s)
        private Vector3 dragAnchorWorld;     // 미들마우스 드래그 월드 anchor
        private bool dragging;

        // Soft world bounds so camera can't pan into infinity.
        // #108: map ±30 → ±35.  #235 map ±45 + zoom 여유 → ±50
        [SerializeField] private Vector2 worldMin = new Vector2(-50, -50);
        [SerializeField] private Vector2 worldMax = new Vector2( 50,  50);

        private Camera cam;

        // 운영자 피드백 2026-05-27: pawn 이 AI 로 wander 하면서 camera 밖으로 나감.
        //  → ClickSelector.Select 시 cs.CurrentSelection 위치로 부드럽게 pan.
        //  다른 작업 (WASD/마우스휠) 시작하면 follow 종료.
        private PawnEntity followingPawn;
        private float followStartTime = -10f;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
        }

        public void RequestFocus(PawnEntity p)
        {
            followingPawn = p;
            followStartTime = Time.unscaledTime;
        }

        // ColonistBar (콜로니스트 바) 가 entry 클릭 시 즉시 camera 를 그 pawn 으로 중앙 정렬할 때 호출.
        //  RequestFocus(부드러운 0.6s pan) 와 달리 이건 즉시 점프 — bar 클릭은 "지금 바로 보여달라"는
        //  의도라 deterministic 한 즉시 이동이 적절(batchmode 에서도 한 frame 에 반영).  worldMin/Max
        //  로 clamp 해 맵 밖으로 안 나가게.  진행 중인 auto-follow 가 다시 덮어쓰지 않도록 해제.
        public void FocusOn(Vector2 worldPos)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            followingPawn = null;  // bar 클릭 = 명시적 jump, auto-follow 종료
            cam.transform.position = ClampCamPos(worldPos.x, worldPos.y, cam.transform.position.z);
        }

        // #버그헌트(2026-06-03): 줌-인식 카메라 경계.  이전엔 worldMin/Max 로 '카메라 중심'만
        //  clamp 해, 줌아웃(orthographicSize 큼) 시 카메라가 월드 밖(검은 void)을 렌더했다.
        //  현재 ortho size + 아스펙트로 '뷰 사각형'이 월드 안에 들어오게 중심을 clamp 한다.
        //  뷰가 월드보다 크면(과도 줌아웃) 월드 중앙 고정(min>max 역전 방지).
        private Vector3 ClampCamPos(float x, float y, float z)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            float minX = worldMin.x + halfW, maxX = worldMax.x - halfW;
            float minY = worldMin.y + halfH, maxY = worldMax.y - halfH;
            float cx = (minX <= maxX) ? Mathf.Clamp(x, minX, maxX) : (worldMin.x + worldMax.x) * 0.5f;
            float cy = (minY <= maxY) ? Mathf.Clamp(y, minY, maxY) : (worldMin.y + worldMax.y) * 0.5f;
            return new Vector3(cx, cy, z);
        }

        private void Update()
        {
            if (cam == null) return;

            // 자동 follow - 0.6s 동안 부드럽게 pan, 그 후 정지 (auto-follow X)
            //  WASD 입력 들어오면 즉시 종료.
            //  batchmode 에서 unscaledDeltaTime 이 매우 작아 Lerp 가 안 수렴하는 문제 →
            //  MoveTowards (고정 거리/초) 가 deterministic.  속도 ~30 unit/s.
            if (followingPawn != null)
            {
                float elapsed = Time.unscaledTime - followStartTime;
                if (elapsed > 0.6f || followingPawn.IsDead)
                {
                    followingPawn = null;
                }
                else
                {
                    Vector3 target = followingPawn.transform.position;
                    target.z = cam.transform.position.z;
                    // batchmode 에서 unscaledDeltaTime 이 매우 작아 Lerp 가 안 수렴하는 문제 →
                    //  MoveTowards 고정 거리/초.  속도 ~30 unit/s, 최소 0.5/frame (batchmode 보호).
                    float maxStep = 30f * Time.unscaledDeltaTime;
                    if (maxStep < 0.5f) maxStep = 0.5f;
                    // #버그헌트3(2026-06-04): 자동 follow 도 월드 경계 clamp.  이전엔 raw 림 위치로
                    //  MoveTowards 만 해 맵 가장자리 림 선택 시 카메라가 경계를 넘어 void 가 보였다
                    //  (pan/zoom/FocusOn 은 이미 ClampCamPos 적용 — 이 경로만 누락).
                    Vector3 np = Vector3.MoveTowards(cam.transform.position, target, maxStep);
                    cam.transform.position = ClampCamPos(np.x, np.y, np.z);
                }
            }

            float dt = Time.unscaledDeltaTime;

            // ── Pan (WASD + Arrow) — #카메라파리티: 관성 모델 ──────────────────
            //  입력은 '목표 속도'만 정하고 실제 속도(panVel)는 지수 수렴.  키를 떼면
            //  panDecay 로 ~0.3s 드리프트 후 정지 — 림월드의 부드러운 무빙 질감.
            //  하네스/배치모드: 키 입력이 없으면 panVel 은 0 유지 → 드리프트 오염 없음.
            float h = 0f, v = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  h -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  v -= 1;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    v += 1;

            if (h != 0 || v != 0)
            {
                followingPawn = null;   // 사용자 pan 입력 - auto-follow 즉시 종료
                float speed = panSpeed * (cam.orthographicSize / 3.5f);
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    speed *= fastPanMultiplier;
                Vector2 desired = new Vector2(h, v).normalized * speed;
                panVel = Vector2.Lerp(panVel, desired, 1f - Mathf.Exp(-panAccel * dt));
            }
            else
            {
                panVel = Vector2.Lerp(panVel, Vector2.zero, 1f - Mathf.Exp(-panDecay * dt));
                if (panVel.sqrMagnitude < 0.0025f) panVel = Vector2.zero;
            }

            // ── 미들마우스 드래그 팬 (림월드 동작) — 놓으면 panVel 모멘텀으로 이어짐 ──
            if (Input.GetMouseButtonDown(2))
            {
                dragging = true;
                followingPawn = null;
                dragAnchorWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            }
            if (dragging && Input.GetMouseButton(2))
            {
                Vector3 cur = cam.ScreenToWorldPoint(Input.mousePosition);
                Vector3 delta = dragAnchorWorld - cur;
                Vector3 p0 = cam.transform.position;
                cam.transform.position = ClampCamPos(p0.x + delta.x, p0.y + delta.y, p0.z);
                if (dt > 0.0001f)   // release 모멘텀용 속도 기록 (스무딩)
                    panVel = Vector2.Lerp(panVel, new Vector2(delta.x, delta.y) / dt,
                                          1f - Mathf.Exp(-panAccel * dt));
            }
            if (Input.GetMouseButtonUp(2)) dragging = false;

            if (!dragging && panVel.sqrMagnitude > 0.0001f)
            {
                Vector3 p = cam.transform.position;
                cam.transform.position = ClampCamPos(
                    p.x + panVel.x * dt, p.y + panVel.y * dt, p.z);
            }

            // ── Zoom (mouse wheel / 매직마우스 / 트랙패드) — #카메라파리티: 스무스 + 줌인 시 커서 방향 ──
            // #매직마우스(운영자 2026-06-14 "휙휙 넘어가는 버그"): 실측 — 한 플릭이 60Hz 로
            //  28~165개 이벤트.  스크롤을 pendingZoom 에 비례 적립 → 매 프레임 maxZoomRate×dt
            //  만큼만 소비.  슬램 불가 + 1000fps 에서도 희소 입력 보존(round-3 dt 캡 버그 수정).
            float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                if (targetOrtho < 0f) targetOrtho = cam.orthographicSize;
                pendingZoom += -scroll * zoomSensitivity;   // 비례 적립 (scroll>0=줌인=음수)
                zoomTowardCursor = scroll > 0;              // 줌인만 커서 앵커 (줌아웃은 중앙 유지)
            }
            if (Mathf.Abs(pendingZoom) > 0.0001f)
            {
                if (targetOrtho < 0f) targetOrtho = cam.orthographicSize;
                float step = Mathf.Clamp(pendingZoom, -maxZoomRate * dt, maxZoomRate * dt);
                targetOrtho = Mathf.Clamp(targetOrtho * Mathf.Exp(step), zoomMin, zoomMax);
                pendingZoom -= step;
                // 줌 한계 도달 시 같은 방향 잔여 pending 폐기 — 불필요한 관성 꼬리 제거.
                if (targetOrtho <= zoomMin + 1e-4f && pendingZoom < 0f) pendingZoom = 0f;
                if (targetOrtho >= zoomMax - 1e-4f && pendingZoom > 0f) pendingZoom = 0f;
            }
            if (targetOrtho > 0f && Mathf.Abs(cam.orthographicSize - targetOrtho) > 0.005f)
            {
                Vector3 before = zoomTowardCursor
                    ? cam.ScreenToWorldPoint(Input.mousePosition) : Vector3.zero;
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetOrtho,
                                                  1f - Mathf.Exp(-zoomLerpSpeed * dt));
                if (zoomTowardCursor)
                {
                    // 커서 밑 월드 지점이 제자리에 머물게 — 줌인이 커서로 빨려 들어간다.
                    Vector3 after = cam.ScreenToWorldPoint(Input.mousePosition);
                    Vector3 p = cam.transform.position + (before - after);
                    cam.transform.position = ClampCamPos(p.x, p.y, p.z);
                }
                else
                {
                    Vector3 zp = cam.transform.position;
                    cam.transform.position = ClampCamPos(zp.x, zp.y, zp.z);
                }
            }
        }
    }
}
