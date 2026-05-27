using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 43 — Fullscreen dark overlay that intensifies at night.
    /// Renders a single large white sprite recolored at runtime.
    /// Alpha curve (24h day):
    ///   00:00–04:00  alpha 0.65 (deep night)
    ///   05:30        alpha 0.40
    ///   06:30        alpha 0.10
    ///   08:00–17:00  alpha 0.00 (full day)
    ///   18:30        alpha 0.20
    ///   20:00        alpha 0.50
    ///   22:00        alpha 0.62
    ///   24:00        alpha 0.65
    /// Plus weather multiplier (storm 1.4x darker).
    /// Auto-tracks camera position so the overlay is always centered.
    /// Sorting order high enough to render above tiles + entities, below UI canvas.
    /// </summary>
    public class NightOverlay : MonoBehaviour
    {
        private static Sprite _whiteSprite;
        private static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null)
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                    tex.filterMode = FilterMode.Bilinear;
                    tex.Apply();
                    _whiteSprite = Sprite.Create(tex, new Rect(0,0,2,2), new Vector2(0.5f,0.5f), 2f);
                    _whiteSprite.name = "NightOverlayWhite";
                }
                return _whiteSprite;
            }
        }

        private SpriteRenderer sr;
        private Camera cam;

        private struct Stop { public float t; public float a; public Stop(float t, float a){ this.t=t; this.a=a; } }
        private static readonly Stop[] STOPS = new Stop[]
        {
            new Stop(0.00f, 0.65f),
            new Stop(0.17f, 0.65f),  // 04:00
            new Stop(0.23f, 0.40f),  // 05:30
            new Stop(0.27f, 0.10f),  // 06:30
            new Stop(0.33f, 0.00f),  // 08:00
            new Stop(0.71f, 0.00f),  // 17:00
            new Stop(0.77f, 0.20f),  // 18:30
            new Stop(0.83f, 0.50f),  // 20:00
            new Stop(0.92f, 0.62f),  // 22:00
            new Stop(1.00f, 0.65f),
        };

        private void Awake()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var camObj = GameObject.FindFirstObjectByType<Camera>();
                if (camObj != null) cam = camObj;
            }
            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite;
            sr.color = new Color(0.05f, 0.08f, 0.20f, 0f);
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 25;  // above ground/tiles (0/1/5/7) below floating bars (30)
            // 50x50 unit cover — bigger than any reasonable camera ortho 22 ortho
            //  (camera vertical extent at zoomMax 22 = 44 unit, horizontal = 78).
            transform.localScale = new Vector3(100f, 100f, 1f);
        }

        private void LateUpdate()
        {
            if (cam != null)
            {
                Vector3 p = cam.transform.position;
                transform.position = new Vector3(p.x, p.y, 0f);
            }
            float t = GameClock.Instance != null ? GameClock.Instance.DayProgress : 0.5f;
            float a = SampleStops(t);
            // Weather multiplier (storm = darker)
            if (WeatherController.Instance != null)
            {
                float m = WeatherController.Instance.ColorMultiplier();
                // ColorMultiplier 1.0 = clear, 0.55 = storm. Invert to add darkness.
                if (m < 1f) a = Mathf.Min(0.85f, a + (1f - m) * 0.6f);
            }
            sr.color = new Color(0.05f, 0.08f, 0.20f, a);
        }

        private static float SampleStops(float t)
        {
            t = Mathf.Repeat(t, 1f);
            for (int i = 0; i < STOPS.Length - 1; i++)
            {
                if (t >= STOPS[i].t && t <= STOPS[i + 1].t)
                {
                    float local = Mathf.InverseLerp(STOPS[i].t, STOPS[i + 1].t, t);
                    return Mathf.Lerp(STOPS[i].a, STOPS[i + 1].a, local);
                }
            }
            return STOPS[0].a;
        }
    }
}
