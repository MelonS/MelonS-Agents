using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 43 — Fullscreen dark overlay that intensifies at night.
    /// Renders a single large white sprite recolored at runtime.
    ///
    /// 폴리싱(2026-06-01): 기존엔 균일한 어두운 틴트라 "필터 한 장 씌운" 느낌이었다.
    /// 자연스러운 밤을 위해 두 가지를 바꿨다:
    ///   1) 스톱 사이를 선형이 아니라 smoothstep 으로 보간 — 황혼→자정 깊어지는
    ///      과정, 자정→새벽 밝아지는 과정이 부드러운 곡선을 그린다(딱딱한 직선 X).
    ///   2) 한밤 alpha 를 0.72 까지 올려 깊이감을 주되, 절대 칠흑(=1.0)으로는 가지
    ///      않게 ambient 최소 밝기(MaxNightAlpha 상한)를 유지 — 맵이 항상 읽힌다.
    /// Alpha curve (24h day, smoothstep 보간):
    ///   00:00–04:00  alpha 0.72 (deep night, but never pitch-black)
    ///   05:30        alpha 0.45
    ///   06:30        alpha 0.12
    ///   08:00–17:00  alpha 0.00 (full day)
    ///   18:30        alpha 0.22
    ///   20:00        alpha 0.55
    ///   22:00        alpha 0.68
    ///   24:00        alpha 0.72
    /// Plus weather multiplier (storm 1.4x darker).
    /// Auto-tracks camera position AND ortho size each LateUpdate so the overlay
    /// stays exactly centred on, and large enough to cover, the camera view at any
    /// zoom/pan — no edge ever shows, no drift between map and overlay (operator fb
    /// "밤이 맵 위에 레이어 덮은 것처럼 따로 논다").
    /// Sorting order high enough to render above tiles + entities, below UI canvas.
    /// Headless/-batchmode safe: every camera access is null-guarded.
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
            new Stop(0.00f, 0.72f),
            new Stop(0.17f, 0.72f),  // 04:00 깊은 밤(상한 = ambient 최소 밝기 유지)
            new Stop(0.23f, 0.45f),  // 05:30 새벽
            new Stop(0.27f, 0.12f),  // 06:30 일출
            new Stop(0.33f, 0.00f),  // 08:00 한낮
            new Stop(0.71f, 0.00f),  // 17:00 한낮
            new Stop(0.77f, 0.22f),  // 18:30 일몰
            new Stop(0.83f, 0.55f),  // 20:00 황혼
            new Stop(0.92f, 0.68f),  // 22:00 밤
            new Stop(1.00f, 0.72f),
        };

        // 한밤 ambient 상한 — 폭풍 가중치를 더해도 이 값을 넘지 않게 해 맵이 칠흑으로
        // 사라지지 않는다(RimWorld 도 한밤에 실루엣은 항상 읽힌다).
        private const float MaxNightAlpha = 0.82f;

        // 2x2 px white sprite → 1 world-unit wide at PPU 2.  localScale therefore
        // maps 1:1 to world units, so scale = view size in units covers the view.
        // Margin keeps a safety border so a fast pan/zoom can never reveal an edge
        // for a single frame.
        private const float CoverMargin = 1.25f;

        private void Awake()
        {
            AcquireCamera();
            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSprite;
            sr.color = new Color(0.05f, 0.08f, 0.20f, 0f);
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 25;  // above ground/tiles (0/1/5/7) below floating bars (30)
            // Initial fallback scale; LateUpdate re-fits to the live camera view.
            transform.localScale = new Vector3(100f, 100f, 1f);
        }

        private void AcquireCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var camObj = GameObject.FindFirstObjectByType<Camera>();
                if (camObj != null) cam = camObj;
            }
        }

        private void LateUpdate()
        {
            // Camera can be created after Awake (scene bootstrap order); re-acquire.
            if (cam == null) AcquireCamera();
            if (cam != null)
            {
                // Centre exactly on the camera (X/Y), pinned to z=0 so it sits on
                // the world plane regardless of camera depth.
                Vector3 p = cam.transform.position;
                transform.position = new Vector3(p.x, p.y, 0f);

                // Re-fit the cover quad to the live view every frame so zoom/pan
                // can never reveal an uncovered edge and the dark layer always
                // moves locked to the map.  orthographicSize = half-height in units.
                float halfH = cam.orthographic
                    ? cam.orthographicSize
                    : Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Abs(p.z);
                float aspect = cam.aspect > 0.01f ? cam.aspect : (16f / 9f);
                float coverH = (halfH * 2f) * CoverMargin;
                float coverW = coverH * aspect;
                transform.localScale = new Vector3(coverW, coverH, 1f);
            }
            float t = GameClock.Instance != null ? GameClock.Instance.DayProgress : 0.5f;
            float a = SampleStops(t);
            // Weather multiplier (storm = darker)
            if (WeatherController.Instance != null)
            {
                float m = WeatherController.Instance.ColorMultiplier();
                // ColorMultiplier 1.0 = clear, 0.55 = storm. Invert to add darkness.
                if (m < 1f) a = Mathf.Min(MaxNightAlpha, a + (1f - m) * 0.6f);
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
                    // smoothstep(3t²-2t³): 스톱 진입/이탈에서 기울기가 0 이라
                    // 황혼이 점점 깊어지고 새벽이 점점 밝아지는 곡선이 된다(직선 X).
                    float s = local * local * (3f - 2f * local);
                    return Mathf.Lerp(STOPS[i].a, STOPS[i + 1].a, s);
                }
            }
            return STOPS[0].a;
        }
    }
}
