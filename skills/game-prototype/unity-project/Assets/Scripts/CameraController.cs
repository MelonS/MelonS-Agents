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
        [SerializeField] private float zoomStep = 1.2f;
        // #199 A1: default ortho 6→3.5 (pawn 1x1).  zoomMin 3→1.5 로 낮춰
        //  단일 1x1 pawn 근접 inspection 가능 (1.5 → pawn 이 화면 33% 차지).
        [SerializeField] private float zoomMin = 1.5f;
        // Day 40: 40x40 맵 — zoomMax 14 → 22 (전체 맵 한 화면에 보기 가능)
        [SerializeField] private float zoomMax = 48f;  // #108 60x60→32.  #235 90x90 전체 보기 → 48

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
                    cam.transform.position = Vector3.MoveTowards(cam.transform.position, target, maxStep);
                }
            }

            // Pan (WASD + Arrow)
            float h = 0f, v = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  h -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  v -= 1;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    v += 1;

            if (h != 0 || v != 0)
            {
                // 사용자 pan 입력 - auto-follow 즉시 종료
                followingPawn = null;

                // #199 A1: 기준 ortho 6 → 3.5 (pawn 1x1).  pan 속도 정규화 divisor 도 맞춤
                //  (default zoom 에서 pan = panSpeed 유지).
                float speed = panSpeed * (cam.orthographicSize / 3.5f);
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    speed *= fastPanMultiplier;

                Vector3 p = cam.transform.position;
                // #버그헌트: 줌-인식 경계로 clamp (뷰 사각형이 월드 밖 void 를 안 보이게).
                cam.transform.position = ClampCamPos(
                    p.x + h * speed * Time.unscaledDeltaTime,
                    p.y + v * speed * Time.unscaledDeltaTime, p.z);
            }

            // Zoom (mouse wheel)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                if (scroll > 0)
                    cam.orthographicSize = Mathf.Max(zoomMin, cam.orthographicSize / zoomStep);
                else
                    cam.orthographicSize = Mathf.Min(zoomMax, cam.orthographicSize * zoomStep);
                // #버그헌트: 줌 변경 후 경계 밖이면 재클램프(엣지에서 줌아웃 시 void 방지).
                Vector3 zp = cam.transform.position;
                cam.transform.position = ClampCamPos(zp.x, zp.y, zp.z);
            }
        }
    }
}
