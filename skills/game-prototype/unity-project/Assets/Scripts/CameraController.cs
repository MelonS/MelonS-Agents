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
        [SerializeField] private float zoomMin = 3f;
        // Day 40: 40x40 맵 — zoomMax 14 → 22 (전체 맵 한 화면에 보기 가능)
        [SerializeField] private float zoomMax = 32f;  // #108 - 60x60 맵 전체 보기

        // Soft world bounds so camera can't pan into infinity.
        // #108: map ±30 + zoom 여유 → ±35
        [SerializeField] private Vector2 worldMin = new Vector2(-35, -35);
        [SerializeField] private Vector2 worldMax = new Vector2( 35,  35);

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

                float speed = panSpeed * (cam.orthographicSize / 6f);
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    speed *= fastPanMultiplier;

                Vector3 p = cam.transform.position;
                p.x = Mathf.Clamp(p.x + h * speed * Time.unscaledDeltaTime, worldMin.x, worldMax.x);
                p.y = Mathf.Clamp(p.y + v * speed * Time.unscaledDeltaTime, worldMin.y, worldMax.y);
                cam.transform.position = p;
            }

            // Zoom (mouse wheel)
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                if (scroll > 0)
                    cam.orthographicSize = Mathf.Max(zoomMin, cam.orthographicSize / zoomStep);
                else
                    cam.orthographicSize = Mathf.Min(zoomMax, cam.orthographicSize * zoomStep);
            }
        }
    }
}
