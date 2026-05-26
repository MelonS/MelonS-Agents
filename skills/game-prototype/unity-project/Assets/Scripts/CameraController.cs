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
        [SerializeField] private float zoomMax = 14f;

        // Soft world bounds so camera can't pan into infinity.
        [SerializeField] private Vector2 worldMin = new Vector2(-30, -30);
        [SerializeField] private Vector2 worldMax = new Vector2( 30,  30);

        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
        }

        private void Update()
        {
            if (cam == null) return;

            // Pan (WASD + Arrow)
            float h = 0f, v = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  h -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  v -= 1;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    v += 1;

            if (h != 0 || v != 0)
            {
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
