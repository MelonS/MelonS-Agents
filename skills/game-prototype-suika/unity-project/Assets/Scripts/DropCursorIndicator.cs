using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Vertical line at cursor X showing where the next fruit will land.</summary>
    public class DropCursorIndicator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sr;
        [SerializeField] private float dropY = 4f;
        [SerializeField] private float dropMinX = -2.5f;
        [SerializeField] private float dropMaxX = 2.5f;
        private Camera cam;

        private void Awake()
        {
            cam = Camera.main;
            if (sr == null) sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (GameOverDetector.IsGameOver) { if (sr != null) sr.enabled = false; return; }
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
            float x = Mathf.Clamp(mw.x, dropMinX, dropMaxX);
            transform.position = new Vector3(x, dropY * 0.5f, 0);
        }
    }
}
