using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 밤 어둠 + 램프 조명을 하나의 동적 라이트맵 텍스처로 그린다.
    ///
    /// #268 운영자 fb "조명이 뿌옇게만 보임" 근본 수정:
    ///   이전 구조 = 균일한 어둠 한 장(이 오버레이) + 그 위에 LampLight 가 주황빛을
    ///   가산(additive).  가산 빛은 어둠 위에 "빛나는 안개"로만 보여서, falloff 를
    ///   아무리 바꿔도 뿌옇다.  the reference sim 의 조명은 반대다 — 램프 반경에서 어둠 자체를
    ///   걷어내(라이트맵) 실제 바닥/벽/림이 환히 드러나고, 거기에 따뜻한 색이 입혀진다.
    ///
    ///   그래서 이 오버레이를 카메라 뷰를 덮는 동적 텍스처(라이트맵)로 만든다.  매 프레임
    ///   각 텍셀의 월드 위치를 구해, 기본 밤 어둠 alpha 에서 켜진 램프 반경만큼 alpha 를
    ///   낮추고(=어둠을 걷어 바닥이 드러남) 따뜻한 촛불색을 입힌다.  감쇠는 the reference sim
    ///   토치램프 글로우 공식(중심 1/d² 집중 + 선형)을 따라 또렷한 코어 + 깔끔한 가장자리.
    ///
    ///   → LampLight(가산 안개) / LampGlowDriver(바닥 풀) 는 비활성(중복·뿌연 원인).
    ///
    /// Alpha curve (24h, smoothstep): 낮 0 … 한밤 0.72 (칠흑은 아님).  날씨 가중.
    /// 카메라 위치/줌을 매 프레임 추종해 뷰를 정확히 덮는다(맵과 따로 안 논다).
    /// Headless/-batchmode 안전(카메라 null 가드).
    /// </summary>
    public class NightOverlay : MonoBehaviour
    {
        // 라이트맵 해상도(정사각).  뷰를 덮는 정사각 영역에 매핑; 월드 거리로 reveal 계산
        //  하므로 텍셀이 비정방형으로 늘어나도 빛 원은 월드에서 정원으로 유지된다.
        private const int TEX = 160;

        // 램프 빛 반경(칸) — the reference sim 토치램프(lit ~6.5)를 참고한 값.  중심 또렷, ~6.5칸에서 0.
        private const float LightRadiusTiles = 6.5f;

        // reveal 1(램프 중심)에서 어둠 alpha 를 얼마나 걷을지(0.88 → 거의 다 걷어 바닥 보임).
        private const float MaxReveal = 0.9f;

        private static readonly Color32 WarmLight = new Color32(255, 156, 66, 255); // 촛불 오렌지

        // D4b 몰입 (design-immersion-2026-06-11) — 어둠 "색" 도 시간대를 탄다.
        //  (구 NightDark 상수(13,20,51)는 DARK_STOPS 의 한밤 구간으로 흡수.)
        //  기존엔 황혼/새벽에도 한밤과 같은 청색이라 해질녘 무드가 없었다.
        //  STOPS(alpha 커브)와 같은 타임라인: 일몰(18:30) 주황 → 황혼(20:00) 보라
        //  → 밤 청색, 역순으로 새벽 보라 → 일출(06:30) 주황.  alpha 0 구간(낮)의
        //  색은 무관하나 경계 보간이 자연스럽도록 웜톤을 이어 둔다.
        private struct CStop { public float t; public Color32 c; public CStop(float t, byte r, byte g, byte b){ this.t=t; this.c=new Color32(r,g,b,255); } }
        private static readonly CStop[] DARK_STOPS = new CStop[]
        {
            new CStop(0.00f, 13, 20, 51),
            new CStop(0.17f, 13, 20, 51),   // 04:00 한밤 청색
            new CStop(0.23f, 48, 26, 52),   // 05:30 새벽 보라
            new CStop(0.27f, 82, 42, 32),   // 06:30 일출 주황
            new CStop(0.33f, 82, 46, 30),   // 08:00 (alpha 0 — 보간용)
            new CStop(0.71f, 86, 46, 28),   // 17:00 (alpha 0 — 보간용)
            new CStop(0.77f, 86, 42, 26),   // 18:30 일몰 주황
            new CStop(0.83f, 52, 26, 56),   // 20:00 황혼 보라
            new CStop(0.92f, 16, 20, 52),   // 22:00 밤 청색 복귀
            new CStop(1.00f, 13, 20, 51),
        };

        private static Color32 SampleDarkStops(float t)
        {
            for (int i = 0; i < DARK_STOPS.Length - 1; i++)
            {
                if (t <= DARK_STOPS[i + 1].t)
                {
                    float local = Mathf.InverseLerp(DARK_STOPS[i].t, DARK_STOPS[i + 1].t, t);
                    float s = local * local * (3f - 2f * local);   // STOPS 와 동일 smoothstep
                    return Color32.Lerp(DARK_STOPS[i].c, DARK_STOPS[i + 1].c, s);
                }
            }
            return DARK_STOPS[DARK_STOPS.Length - 1].c;
        }

        private const float MaxNightAlpha = 0.82f;
        private const float CoverMargin   = 1.25f;

        /// <summary>현재 어둠 강도 [0,1] (한밤 0.72 기준 정규화).  라벨 감광(TOP-2)/
        ///  글로우 주간 감쇠(TOP-3) 등 다른 표현 레이어가 시간대 무드를 따라가는 단일 출처.</summary>
        public static float CurrentDarkness01 { get; private set; }

        private SpriteRenderer sr;
        private Camera cam;
        private Texture2D _tex;
        private Color32[] _px;

        // 램프 캐시 (1.5s 마다 재스캔)
        private LampEntity[] _lamps = System.Array.Empty<LampEntity>();
        private float _nextLampScan;

        // #268b 벽 가림(occlusion): 빛이 벽을 통과 못 하게.  벽 셀 집합 + 램프별 타일
        //  가시성 그리드(VD×VD)를 매 repaint 미리 계산해 텍셀은 O(1) 조회.
        private readonly System.Collections.Generic.HashSet<Vector2Int> _wallCells
            = new System.Collections.Generic.HashSet<Vector2Int>();
        private const int VR = 7;            // 가시성 반경(칸) ≥ LightRadiusTiles
        private const int VD = VR * 2 + 1;   // 15
        private bool[] _vis = new bool[32 * VD * VD];
        private readonly int[] lcxCache = new int[32];   // 램프 셀좌표(SampleVis 공유)
        private readonly int[] lcyCache = new int[32];

        // the reference sim 글로우 중심 정규화 상수 (dist0 → d=1)
        private static readonly float FCenter = ComputeFCenter();
        private static float ComputeFCenter()
        {
            float aL = 1f - 1f / LightRadiusTiles;
            return aL + (1f - aL) * 0.4f;
        }

        private struct Stop { public float t; public float a; public Stop(float t, float a){ this.t=t; this.a=a; } }
        private static readonly Stop[] STOPS = new Stop[]
        {
            new Stop(0.00f, 0.72f),
            new Stop(0.17f, 0.72f),
            new Stop(0.23f, 0.45f),
            new Stop(0.27f, 0.12f),
            new Stop(0.33f, 0.00f),
            new Stop(0.71f, 0.00f),
            new Stop(0.77f, 0.22f),
            new Stop(0.83f, 0.55f),
            new Stop(0.92f, 0.68f),
            new Stop(1.00f, 0.72f),
        };

        private void Awake()
        {
            AcquireCamera();
            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

            _tex = new Texture2D(TEX, TEX, TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Bilinear;   // 부드러운 빛 경계
            _tex.wrapMode   = TextureWrapMode.Clamp;
            _tex.name       = "NightLightmap";
            _px = new Color32[TEX * TEX];

            // 정사각 텍스처, ppu=TEX → 네이티브 1x1 유닛.  localScale 로 뷰에 맞춰 늘림.
            var spr = Sprite.Create(_tex, new Rect(0, 0, TEX, TEX), new Vector2(0.5f, 0.5f), TEX);
            spr.name = "NightLightmapSprite";
            sr.sprite = spr;
            sr.color  = Color.white;                 // 색/알파는 텍스처가 담는다
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 25;
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
            if (cam == null) AcquireCamera();

            float coverW = 100f, coverH = 100f;
            Vector3 center = transform.position;
            if (cam != null)
            {
                Vector3 p = cam.transform.position;
                center = new Vector3(p.x, p.y, 0f);
                transform.position = center;

                float halfH = cam.orthographic
                    ? cam.orthographicSize
                    : Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Abs(p.z);
                float aspect = cam.aspect > 0.01f ? cam.aspect : (16f / 9f);
                coverH = (halfH * 2f) * CoverMargin;
                coverW = coverH * aspect;
                // 정사각 텍스처를 뷰에 맞춰 비정방형으로 늘림(빛은 월드거리로 계산해 정원 유지).
                transform.localScale = new Vector3(coverW, coverH, 1f);
            }

            float t = GameClock.Instance != null ? GameClock.Instance.DayProgress : 0.5f;
            float baseA = SampleStops(t);
            if (WeatherController.Instance != null)
            {
                float m = WeatherController.Instance.ColorMultiplier();
                if (m < 1f) baseA = Mathf.Min(MaxNightAlpha, baseA + (1f - m) * 0.6f);
            }

            if (Time.unscaledTime >= _nextLampScan)
            {
                _nextLampScan = Time.unscaledTime + 1.5f;
                ScanLamps();
            }

            CurrentDarkness01 = Mathf.Clamp01(baseA / 0.72f);
            RepaintLightmap(center, coverW, coverH, baseA, SampleDarkStops(t));
        }

        private void ScanLamps()
        {
#if UNITY_2023_1_OR_NEWER
            _lamps = Object.FindObjectsByType<LampEntity>(FindObjectsSortMode.None);
            var walls = Object.FindObjectsByType<WallEntity>(FindObjectsSortMode.None);
#else
            _lamps = Object.FindObjectsOfType<LampEntity>();
            var walls = Object.FindObjectsOfType<WallEntity>();
#endif
            // 벽 셀 집합 갱신 (건축 중 벽이 늘어나므로 매 스캔 재구성).
            _wallCells.Clear();
            for (int i = 0; i < walls.Length; i++)
            {
                if (walls[i] == null) continue;
                Vector3 wp = walls[i].transform.position;
                _wallCells.Add(new Vector2Int(Mathf.FloorToInt(wp.x), Mathf.FloorToInt(wp.y)));
            }
            // #268d 문도 빛을 막는다(닫힌 문 = 벽).  the reference sim 정석: 닫힌 문은 빛 차단.
            //  문은 대부분 닫혀 있고, 상시 문틈 누광이 어색하다는 운영자 fb 반영.
#if UNITY_2023_1_OR_NEWER
            var doors = Object.FindObjectsByType<DoorEntity>(FindObjectsSortMode.None);
#else
            var doors = Object.FindObjectsOfType<DoorEntity>();
#endif
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] == null) continue;
                Vector3 dp = doors[i].transform.position;
                _wallCells.Add(new Vector2Int(Mathf.FloorToInt(dp.x), Mathf.FloorToInt(dp.y)));
            }
        }

        // 램프 k 의 타일 가시성 그리드를 (wx,wy) 위치에서 bilinear 보간 → [0,1].
        //  #268c 이진 타일 경계의 "딱딱한 그림자"를 ~1칸에 걸쳐 부드럽게 한다.
        private float SampleVis(int k, float wx, float wy)
        {
            float gx = wx - 0.5f - lcxCache[k];   // 타일 중심 dxc 가 gx=dxc 에 대응
            float gy = wy - 0.5f - lcyCache[k];
            int x0 = Mathf.FloorToInt(gx), y0 = Mathf.FloorToInt(gy);
            float fx = gx - x0, fy = gy - y0;
            float v00 = VisAt(k, x0,     y0);
            float v10 = VisAt(k, x0 + 1, y0);
            float v01 = VisAt(k, x0,     y0 + 1);
            float v11 = VisAt(k, x0 + 1, y0 + 1);
            float a = v00 + (v10 - v00) * fx;
            float b = v01 + (v11 - v01) * fx;
            return a + (b - a) * fy;
        }

        private float VisAt(int k, int dx, int dy)
        {
            if (dx < -VR || dx > VR || dy < -VR || dy > VR) return 0f;
            return _vis[k * VD * VD + (dy + VR) * VD + (dx + VR)] ? 1f : 0f;
        }

        // 램프→대상 사이에 벽이 있으면 true(빛 차단).  끝점 셀(램프 셀·대상 셀)은 제외
        //  — 벽 자체의 안쪽 면은 빛을 받고, 벽 너머만 그림자.
        private bool LosBlocked(float fx, float fy, float tx, float ty)
        {
            int fcx = Mathf.FloorToInt(fx), fcy = Mathf.FloorToInt(fy);
            int tcx = Mathf.FloorToInt(tx), tcy = Mathf.FloorToInt(ty);
            float dx = tx - fx, dy = ty - fy;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len * 2f));   // 0.5칸 스텝
            for (int i = 1; i < steps; i++)
            {
                float f = (float)i / steps;
                int cx = Mathf.FloorToInt(fx + dx * f);
                int cy = Mathf.FloorToInt(fy + dy * f);
                if ((cx == fcx && cy == fcy) || (cx == tcx && cy == tcy)) continue;
                if (_wallCells.Contains(new Vector2Int(cx, cy))) return true;
            }
            return false;
        }

        // 라이트맵 페인트: 각 텍셀의 월드 위치에서 기본 어둠 - 램프 reveal.
        private void RepaintLightmap(Vector3 center, float coverW, float coverH, float baseA, Color32 dark)
        {
            float left   = center.x - coverW * 0.5f;
            float bottom = center.y - coverH * 0.5f;
            float stepX  = coverW / TEX;
            float stepY  = coverH / TEX;
            float R = LightRadiusTiles;
            float flameY = LampEntity.FlameHeightCells;

            // 켜진 램프만 추려 (위치, y보정, 셀좌표) + 램프별 타일 가시성 그리드 선계산.
            int n = 0;
            System.Span<float> lx  = stackalloc float[32];
            System.Span<float> ly  = stackalloc float[32];
            for (int i = 0; i < _lamps.Length && n < 32; i++)
            {
                var lamp = _lamps[i];
                if (lamp == null || !lamp.IsLit) continue;
                float px = lamp.transform.position.x;
                float py = lamp.transform.position.y + flameY;
                lx[n] = px; ly[n] = py;
                int cx0 = Mathf.FloorToInt(px), cy0 = Mathf.FloorToInt(py);
                lcxCache[n] = cx0; lcyCache[n] = cy0;
                // 가시성 그리드: 램프 주변 VD×VD 타일에 대해 LOS 한 번씩.
                int baseIdx = n * VD * VD;
                for (int dy = -VR; dy <= VR; dy++)
                    for (int dx = -VR; dx <= VR; dx++)
                    {
                        float tcx = cx0 + dx + 0.5f;
                        float tcy = cy0 + dy + 0.5f;
                        bool vis = !LosBlocked(px, py, tcx, tcy);
                        _vis[baseIdx + (dy + VR) * VD + (dx + VR)] = vis;
                    }
                n++;
            }

            // 한낮(어둠 0) + 램프 무관 → 전부 투명 (싼 경로).
            if (baseA <= 0.001f && n == 0)
            {
                for (int i = 0; i < _px.Length; i++) _px[i] = new Color32(0, 0, 0, 0);
                _tex.SetPixels32(_px); _tex.Apply(false);
                return;
            }

            float R2 = R * R;
            for (int yy = 0; yy < TEX; yy++)
            {
                float wy = bottom + (yy + 0.5f) * stepY;
                int tileY = Mathf.FloorToInt(wy);
                int row = yy * TEX;
                for (int xx = 0; xx < TEX; xx++)
                {
                    float wx = left + (xx + 0.5f) * stepX;
                    int tileX = Mathf.FloorToInt(wx);

                    float reveal = 0f;
                    for (int k = 0; k < n; k++)
                    {
                        int dxc = tileX - lcxCache[k];
                        int dyc = tileY - lcyCache[k];
                        if (dxc < -VR || dxc > VR || dyc < -VR || dyc > VR) continue;
                        float dx = wx - lx[k];
                        float dy = wy - ly[k];
                        float sq = dx * dx + dy * dy;
                        if (sq >= R2) continue;
                        // 벽/문 가림을 bilinear 로 부드럽게 (딱딱한 타일 경계 완화).
                        float occ = SampleVis(k, wx, wy);
                        if (occ <= 0f) continue;
                        float dist = Mathf.Sqrt(sq);
                        float d  = dist + 1f;
                        float aL = 1f - d / R;
                        float bq = 1f / (d * d);
                        float f  = aL + (bq - aL) * 0.4f;
                        float g  = (f / FCenter) * occ;
                        if (g > reveal) reveal = g;
                    }
                    if (reveal > 1f) reveal = 1f;
                    else if (reveal < 0f) reveal = 0f;

                    // 어둠 alpha 를 reveal 만큼 걷어 바닥이 드러나게.  색은 reveal 따라
                    //  밤 어둠색 → 촛불 따뜻색으로 (드러난 곳에 따뜻한 빛이 입혀짐).
                    float a = baseA * (1f - reveal * MaxReveal);
                    byte cr = (byte)(dark.r + (WarmLight.r - dark.r) * reveal);
                    byte cg = (byte)(dark.g + (WarmLight.g - dark.g) * reveal);
                    byte cb = (byte)(dark.b + (WarmLight.b - dark.b) * reveal);
                    _px[row + xx] = new Color32(cr, cg, cb, (byte)(a * 255f));
                }
            }

            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        private static float SampleStops(float t)
        {
            t = Mathf.Repeat(t, 1f);
            for (int i = 0; i < STOPS.Length - 1; i++)
            {
                if (t >= STOPS[i].t && t <= STOPS[i + 1].t)
                {
                    float local = Mathf.InverseLerp(STOPS[i].t, STOPS[i + 1].t, t);
                    float s = local * local * (3f - 2f * local);
                    return Mathf.Lerp(STOPS[i].a, STOPS[i + 1].a, s);
                }
            }
            return STOPS[0].a;
        }
    }
}
