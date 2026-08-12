using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M4-04 Lane A — wiki Dimension 4 (건축) #19:
    ///   "Add torch/standing-lamp buildable (night light pool)."
    ///   Acceptance: a built lamp emits a light glow at night.
    ///
    /// The placed standing-lamp / torch.  A passive structure entity in the same
    /// family as StoveEntity / WallEntity: a sprite + a 1×1 collider footprint.
    /// It carries NO state of its own beyond "I am a lamp" — the night lighting it
    /// casts is drawn by two self-attached driver layers that both scan for this
    /// marker, so this file stays a thin marker component:
    ///   - LampGlowDriver.cs : warm amber floor pool UNDER the night overlay
    ///                         (sortingOrder 8) — ambience/colour on the ground.
    ///   - LampLight.cs      : soft additive light disc ABOVE the night overlay
    ///                         (sortingOrder 26) — actually lifts the darkness so
    ///                         the map reads as LIT around the lamp at night
    ///                         (operator fb "조명이 실제로 맵을 밝히는 효과 필요").
    /// The two layers together give colony-sim-style "light pushes back the dark".
    ///
    /// Why a marker, not a glow-owner:
    ///   NightLightPoolDriver.cs scans ONLY for StoveEntity and gates on
    ///   stove.MealsAvailable/CanCookOne — it cannot detect a lamp without an
    ///   edit, and the W-M4-04 lane contract forbids editing it.  So dedicated
    ///   LampGlowDriver / LampLight drivers scan for LampEntity instead, reusing
    ///   the identical procedural + day/night-alpha pattern.  This component is
    ///   the thing those drivers scan for; FlameHeightCells / IsLit are the only
    ///   surface they read.
    ///
    /// Lit model (폴리싱 2026-06-01 — 운영자 fb "램프가 낮에도 불꽃 ON"):
    ///   이전엔 IsLit => true 로 항상 켜져 있어 낮(audit 6AM)에도 불꽃/빛이 났다.
    ///   the reference sim 처럼 **어두울 때만** 켜지도록 GameClock 의 day/night 진행도를
    ///   읽어 NightFactor(밤 0→1) 가 임계값 이상일 때만 IsLit = true 가 된다.
    ///   이렇게 하면 IsLit 을 게이트로 쓰는 LampGlowDriver / LampLight 의 빛·풀이
    ///   모두 밤에만 켜지고, 낮엔 자동으로 꺼진다(드라이버 파일은 손대지 않음).
    ///
    ///   더해서, 램프 본체 스프라이트에 그려진 "불꽃 머리" 픽셀(BuildManager 의
    ///   FIRE_OR/FIRE_LT) 은 시간과 무관하게 항상 보이므로, LampEntity 가 직접
    ///   불꽃 영역 위에 어두운 소화(消火) 오버레이를 붙여 낮엔 페이드 인(불꽃을
    ///   덮어 꺼진 것처럼), 밤엔 페이드 아웃(불꽃을 드러냄) 시킨다.  본체 스프라이트
    ///   텍스처(BuildManager 소유)는 건드리지 않고 오버레이로만 처리.
    ///
    ///   향후 전력/연료 루프가 들어오면 NightFactor 게이트에 AND 로 엮으면 된다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class LampEntity : MonoBehaviour
    {
        // World-space height (in cells) of the flame head above the lamp's
        // transform origin.  The sprite is 16×16 = 1 cell; the flame sits in
        // the top ~quarter, so the glow centre is lifted toward it rather than
        // sitting on the foot.  LampGlowDriver reads this to centre the pool.
        public const float FlameHeightCells = 0.30f;

        /// <summary>이 조명이 밝히는 반경 (칸).  등잔 5.5 = 방 하나.
        ///
        /// 2026-08-02 석등 추가로 값이 하나가 아니게 됐다 — 석등은 마당 전체를 맡는
        ///  큰 조명(7.5)이라 등잔과 같은 컴포넌트를 쓰되 반경만 다르다.  하드코딩된
        ///  5.5 를 필드로 빼서, 새 조명이 생길 때마다 컴포넌트를 늘리지 않게 한다.</summary>
        [SerializeField] private float lightRadiusTiles = 5.5f;

        /// <summary>프리팹 조립 시 반경을 지정한다 (Awake 전에 호출돼야 반영된다).</summary>
        public void SetLightRadius(float tiles) => lightRadiusTiles = tiles;

        // 밤 게이트 임계값: NightFactor(0=한낮,1=한밤) 가 이 값 이상이면 점등.
        //  0.18 ~ 황혼/일출 어귀 — 살짝 어둑해지면 켜지고 밝아지면 꺼진다.
        private const float LitThreshold = 0.18f;

        // ------------------------------------------------------------------ //
        //  소화(消火) 오버레이 외형 튜너블 — 본체 16px 스프라이트 기준          //
        // ------------------------------------------------------------------ //

        // 불꽃 머리 영역(본체 상단 ~1/4)을 덮을 오버레이의 본체 대비 크기/위치.
        //  불꽃 픽셀은 본체 top quarter(FlameHeightCells 부근)에 모여 있다.
        //  2026-08-02 석등 추가로 값이 하나가 아니게 됐다.  등잔은 불꽃이 기둥 **위**
        //   (+0.30칸)에 있지만, 석등은 불꽃이 화사석(불집) 안 = 스프라이트 중앙에 있다.
        //   상수로 두면 석등에서는 오버레이가 불꽃이 아니라 **지붕돌**을 덮어, 낮에
        //   지붕에 검은 얼룩이 생기고 불꽃은 그대로 타는 그림이 된다.
        [SerializeField] private float coverScale  = 0.42f;            // 본체 한 칸(1.0) 대비
        [SerializeField] private float coverYCells = FlameHeightCells; // 불꽃 머리 높이

        /// <summary>불 끄기 오버레이의 위치·크기 (프리팹 조립 시, Awake 전).</summary>
        public void SetFlameCover(float yCells, float scale)
        { coverYCells = yCells; coverScale = scale; }
        private const int   CoverSortingOffset = 1;         // 본체 바로 위
        // 소화 색: 본체 목재 어두운 톤(나무 그림자색)에 맞춰 "불 꺼진 심지"처럼.
        private static readonly Color OffTone = new Color(0.30f, 0.22f, 0.14f, 1f);

        private SpriteRenderer _body;
        private SpriteRenderer _douse;     // 낮에 불꽃을 덮는 오버레이
        private static Sprite  _douseSprite;

        /// <summary>0 = 한낮, 1 = 한밤.  GameClock.DayProgress 를 밤 곡선으로 변환.</summary>
        public static float NightFactor()
        {
            if (GameClock.Instance == null) return 0f;
            return NightCurve.Sample(GameClock.Instance.DayProgress);
        }

        /// <summary>
        /// 어두울 때(밤)만 점등.  glow 드라이버들이 이 값을 게이트로 읽는다.
        /// </summary>
        public bool IsLit => NightFactor() >= LitThreshold;

        private void Awake()
        {
            _body = GetComponent<SpriteRenderer>();
            BuildDouseOverlay();

            // 밤 라이트맵에 스스로 등록한다 (2026-08-01).  NightOverlay 가 타입을
            //  나열하는 대신 LightSource 목록만 읽도록 바뀌었으므로, 등잔도 그 목록에
            //  들어가야 한다.  씬 재베이크 없이 코드가 붙인다.
            var ls = GetComponent<LightSource>() ?? gameObject.AddComponent<LightSource>();
            //  빛의 중심도 불꽃 자리다 — 소화 오버레이와 **같은 좌표**를 쓴다.
            //  상수를 따로 넘기면 석등에서 빛만 지붕돌 높이에서 나온다.
            ls.Configure(lightRadiusTiles, coverYCells);
        }

        // 불꽃 머리 위에 얹는 소화 오버레이 1회 생성(절차).  알파 0 으로 시작.
        private void BuildDouseOverlay()
        {
            if (_douseSprite == null) _douseSprite = BuildSoftSquareSprite();

            var go = new GameObject("LampDouseOverlay");
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = new Vector3(0f, coverYCells, -0.001f);
            go.transform.localScale = Vector3.one * coverScale;

            _douse = go.AddComponent<SpriteRenderer>();
            _douse.sprite = _douseSprite;
            if (_body != null)
            {
                _douse.sortingLayerID = _body.sortingLayerID;
                _douse.sortingOrder   = _body.sortingOrder + CoverSortingOffset;
            }
            else
            {
                _douse.sortingOrder = 6;
            }
            _douse.color = WithA(OffTone, 0f);
        }

        private void Update()
        {
            if (_douse == null) return;
            // 밤(NightFactor 1) → 오버레이 알파 0(불꽃 드러남).
            // 낮(NightFactor 0) → 오버레이 알파 ~1(불꽃 덮임 = 꺼진 심지).
            //  per-frame 곡선 1회 계산 후 색만 갱신 — 무거운 연산/alloc 없음.
            float douse = 1f - Mathf.Clamp01(NightFactor());
            _douse.color = WithA(OffTone, douse);
        }

        private static Color WithA(Color c, float a) { c.a = a; return c; }

        // 16x16 부드러운 사각 패치 — 가장자리가 살짝 페이드되어 본체와 자연스럽게
        // 섞인다(딱딱한 경계 X).  소화 오버레이가 불꽃 머리 영역만 덮도록 작은 크기.
        private static Sprite BuildSoftSquareSprite()
        {
            const int SIZE = 16;
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.name       = "LampDouse_Proc";

            float cx = SIZE * 0.5f, cy = SIZE * 0.5f, rMax = SIZE * 0.5f;
            var px = new Color32[SIZE * SIZE];
            for (int y = 0; y < SIZE; y++)
            for (int x = 0; x < SIZE; x++)
            {
                float dx = (x + 0.5f) - cx;
                float dy = (y + 0.5f) - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float t = Mathf.Clamp01(dist / rMax);
                float falloff = 0.5f * (1f + Mathf.Cos(Mathf.PI * t)); // 중심 1 → 가장자리 0
                px[y * SIZE + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(falloff * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(updateMipmaps: false);
            return Sprite.Create(tex, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 16f);
        }
    }

    /// <summary>
    /// 공용 밤 곡선 — DayProgress(0..1) → NightFactor(0 한낮 … 1 한밤).
    /// LampEntity / LampGlowDriver / LampLight 가 같은 곡선을 쓰도록 한 곳에 모은다
    /// (이전엔 드라이버마다 동일 스톱 배열을 복붙해 드리프트 위험이 있었다).
    /// NightOverlay 의 alpha 커브와 위상이 일치(황혼에 켜지고 새벽에 꺼짐).
    /// smoothstep 보간이라 켜짐/꺼짐이 부드럽다.
    /// </summary>
    internal static class NightCurve
    {
        private struct Stop { public float t; public float n; public Stop(float t, float n){ this.t=t; this.n=n; } }

        // NightFactor: 한낮 0, 한밤 1.  NightOverlay STOPS 와 시각 위상 동일.
        private static readonly Stop[] STOPS = new Stop[]
        {
            new Stop(0.00f, 1.00f),  // 00:00 한밤
            new Stop(0.17f, 1.00f),  // 04:00 한밤
            new Stop(0.23f, 0.62f),  // 05:30 새벽
            new Stop(0.27f, 0.16f),  // 06:30 일출
            new Stop(0.33f, 0.00f),  // 08:00 한낮
            new Stop(0.71f, 0.00f),  // 17:00 한낮
            new Stop(0.77f, 0.30f),  // 18:30 일몰
            new Stop(0.83f, 0.76f),  // 20:00 황혼
            new Stop(0.92f, 0.94f),  // 22:00 밤
            new Stop(1.00f, 1.00f),  // 24:00 한밤
        };

        public static float Sample(float dayProgress)
        {
            float t = Mathf.Repeat(dayProgress, 1f);
            for (int i = 0; i < STOPS.Length - 1; i++)
            {
                if (t >= STOPS[i].t && t <= STOPS[i + 1].t)
                {
                    float local = Mathf.InverseLerp(STOPS[i].t, STOPS[i + 1].t, t);
                    float s = local * local * (3f - 2f * local); // smoothstep
                    return Mathf.Lerp(STOPS[i].n, STOPS[i + 1].n, s);
                }
            }
            return STOPS[0].n;
        }
    }
}
