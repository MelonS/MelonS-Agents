using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 타이틀 화면을 **살아 있는 디오라마**로 만든다.
    ///
    /// 계기 (2026-07-30 운영자):
    ///   "타이틀에 애니메이션이 있어야해 머라도 움직여야"
    ///   "줌만 하지말고 레이어를 나눠서 돌린다던지 먼가 화려해야"
    ///   "타이틀에서 밤낮이 바뀌고 사람이 왓다갓다 하고 머가 생겼다가 없어지고 그정도는 되야지"
    ///
    /// 이전 타이틀은 켄 번스(느린 줌·이동)뿐이었고, 주기 16초·진폭 ±22px 이라
    /// 3초 흘끗 보는 동안 몇 픽셀만 움직여 **사실상 정지 화면**이었다.
    ///
    /// 이 컴포넌트는 없는 그림을 새로 그리지 않는다.  **게임이 이미 가진 것**으로
    /// 타이틀에서 게임의 핵심 루프를 보여준다:
    ///   ① 밤낮 순환 — 게임 안의 시간대 색조를 24초 루프로 압축
    ///   ② 걸어다니는 콜로니스트 — 실제 보행 시트(pawn32_v*)를 그대로 사용
    ///   ③ 밤에 켜지는 불빛 + 별 — "머가 생겼다가 없어지고"
    /// 즉 타이틀 자체가 "이건 사람들이 사는 콜로니 심"이라는 설명이 된다.
    ///
    /// 배선: MainMenu 씬에서 [RuntimeInitializeOnLoadMethod] 로 자가 부팅한다
    /// (SceneSetup 재베이크 없이 붙는다 — MainMenuMotion 과 같은 규약).
    /// </summary>
    public class MainMenuDiorama : MonoBehaviour
    {
        // ── 시트 규약 (PawnSpriteAnimator 와 동일 — 같은 자산을 읽으므로 같아야 한다) ──
        private const int COLS = 9, ROWS = 3, CELL = 32;
        private const int COL_IDLE = 0, COL_WALK0 = 1;
        private const int WALK_FRAMES = 6;
        private const int ROW_E = 1;                 // 측면 보행(동쪽) — 좌우 이동엔 이 행

        private const float DayLoopSec = 24f;        // 한 바퀴 = 24초 (게임 하루의 압축)
        private const int WalkerCount = 4;
        private const int StarCount = 60;

        private RectTransform root;
        private Image nightTint;                     // 전면 밤 색조
        private RectTransform[] walkers;
        private Image[] walkerImg;
        private float[] walkerSpeed, walkerPhase, walkerY, walkerScale;
        private int[] walkerVariant;
        private Sprite[][] walkFrames;               // [variant][frame]
        private Image[] stars;
        private Image[] lamps;                       // 밤에 켜지는 창/모닥불 빛
        private float t0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "MainMenu") return;
            if (FindFirstObjectByType<MainMenuDiorama>() != null) return;
            var go = new GameObject("MainMenuDiorama");
            go.AddComponent<MainMenuDiorama>();
        }

        private void Start()
        {
            t0 = Time.unscaledTime;
            var bg = GameObject.Find("Backdrop");
            if (bg == null) { enabled = false; return; }
            var canvas = bg.transform.parent as RectTransform;
            if (canvas == null) { enabled = false; return; }

            // 레이어 순서: Backdrop(0) → 이 디오라마 → Dim/타이틀.
            //  타이틀 글자 위로 사람이 지나가면 가독성이 깨지므로 반드시 아래에 둔다.
            root = NewRect("DioramaLayer", canvas);
            root.SetSiblingIndex(Mathf.Min(1, canvas.childCount - 1));

            BuildStars();
            BuildLamps();
            BuildWalkers();

            // 밤 색조는 디오라마 **전체** 위에 덮여야 사람/불빛도 같이 어두워진다.
            var tintGo = new GameObject("NightTint", typeof(RectTransform), typeof(Image));
            var trt = (RectTransform)tintGo.transform;
            trt.SetParent(root, false);
            Stretch(trt);
            nightTint = tintGo.GetComponent<Image>();
            nightTint.raycastTarget = false;
            trt.SetAsLastSibling();
        }

        // ─────────────────────────────────────────────────────────────────────
        private RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static Sprite _dot;
        private static Sprite Dot()
        {
            if (_dot != null) return _dot;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var px = new Color[4];
            for (int i = 0; i < 4; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            tex.filterMode = FilterMode.Point;
            _dot = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            return _dot;
        }

        // ── ① 별 — 밤에만 보인다 ("머가 생겼다가 없어지고") ──────────────────
        private void BuildStars()
        {
            var layer = NewRect("Stars", root);
            stars = new Image[StarCount];
            var rng = new System.Random(9931);
            for (int i = 0; i < StarCount; i++)
            {
                var go = new GameObject("star", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(layer, false);
                // 하늘 영역(위 45%)에만.  지면에 별이 뜨면 즉시 가짜로 보인다.
                float ax = 0.02f + (float)rng.NextDouble() * 0.96f;
                float ay = 0.55f + (float)rng.NextDouble() * 0.43f;
                rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
                rt.anchoredPosition = Vector2.zero;
                float s = 2f + (float)rng.NextDouble() * 2.5f;
                rt.sizeDelta = new Vector2(s, s);
                var img = go.GetComponent<Image>();
                img.sprite = Dot();
                img.raycastTarget = false;
                stars[i] = img;
            }
        }

        // ── ② 밤에 켜지는 불빛 ───────────────────────────────────────────────
        private void BuildLamps()
        {
            var layer = NewRect("Lamps", root);
            // 지평선 아래 초원에 흩어진 따뜻한 광원 — 멀리 있는 정착지의 창불.
            float[] xs = { 0.22f, 0.31f, 0.62f, 0.71f, 0.78f };
            float[] ys = { 0.34f, 0.30f, 0.33f, 0.29f, 0.35f };
            lamps = new Image[xs.Length];
            for (int i = 0; i < xs.Length; i++)
            {
                var go = new GameObject("lamp", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(layer, false);
                rt.anchorMin = rt.anchorMax = new Vector2(xs[i], ys[i]);
                rt.anchoredPosition = Vector2.zero;
                float s = 14f + i * 3f;
                rt.sizeDelta = new Vector2(s, s);
                var img = go.GetComponent<Image>();
                img.sprite = Dot();
                img.raycastTarget = false;
                lamps[i] = img;
            }
        }

        // ── ③ 걸어다니는 콜로니스트 ──────────────────────────────────────────
        private void BuildWalkers()
        {
            var layer = NewRect("Walkers", root);
            walkers = new RectTransform[WalkerCount];
            walkerImg = new Image[WalkerCount];
            walkerSpeed = new float[WalkerCount];
            walkerPhase = new float[WalkerCount];
            walkerY = new float[WalkerCount];
            walkerScale = new float[WalkerCount];
            walkerVariant = new int[WalkerCount];
            walkFrames = new Sprite[8][];

            var rng = new System.Random(4242);
            for (int i = 0; i < WalkerCount; i++)
            {
                int variant = i * 2 % 8;                 // 옷색이 갈리는 순서 (게임과 동일 규약)
                walkerVariant[i] = variant;
                EnsureFrames(variant);

                var go = new GameObject($"walker{i}", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(layer, false);
                // 앞쪽(아래)일수록 크고 빠르게 — 깊이가 다른 레이어가 각자 속도로 움직인다.
                float depth = (float)i / Mathf.Max(1, WalkerCount - 1);   // 0=멀리 1=가까이
                walkerY[i] = Mathf.Lerp(0.30f, 0.11f, depth);
                // 실측 캡처에서 앞쪽 인물도 작아 '점'에 가까웠다 — 앞줄을 키워 원근을 세운다.
                walkerScale[i] = Mathf.Lerp(1.8f, 5.0f, depth);
                walkerSpeed[i] = Mathf.Lerp(0.020f, 0.055f, depth)
                                 * (rng.Next(2) == 0 ? 1f : -1f);
                walkerPhase[i] = (float)rng.NextDouble();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, walkerY[i]);
                rt.sizeDelta = new Vector2(CELL * walkerScale[i], CELL * walkerScale[i]);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.preserveAspect = true;
                walkerImg[i] = img;
                walkers[i] = rt;
            }
        }

        private void EnsureFrames(int variant)
        {
            if (walkFrames[variant] != null) return;
            var tex = Resources.Load<Texture2D>($"pawn32/pawn32_v{variant}");
            if (tex == null || tex.width < COLS * CELL || tex.height < ROWS * CELL)
            {
                walkFrames[variant] = new Sprite[0];
                return;
            }
            var arr = new Sprite[WALK_FRAMES];
            for (int f = 0; f < WALK_FRAMES; f++)
            {
                int c = COL_WALK0 + f;
                // row 0(S) 이 시트 최상단 — 텍스처 좌표는 아래가 0 (PawnSpriteAnimator 와 동일).
                var rect = new Rect(c * CELL, tex.height - (ROW_E + 1) * CELL, CELL, CELL);
                arr[f] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 32f,
                                       0, SpriteMeshType.FullRect);
            }
            walkFrames[variant] = arr;
        }

        /// <summary>워커 i 의 시각 t 에서의 화면 가로 위치(앵커 0~1, 화면 밖 여유 포함).
        /// 방향 판정이 이 함수를 두 번 호출해 차분으로 구하므로, 위치와 방향이 어긋날 수 없다.</summary>
        private float WalkerX(int i, float t)
        {
            float u = Mathf.PingPong(t * Mathf.Abs(walkerSpeed[i]) + walkerPhase[i] * 2f, 1f);
            if (walkerSpeed[i] < 0f) u = 1f - u;      // 출발 방향만 좌우로 갈라 준다
            return Mathf.Lerp(-0.08f, 1.08f, u);
        }

        // ─────────────────────────────────────────────────────────────────────
        private void Update()
        {
            float t = Time.unscaledTime - t0;
            float day = Mathf.Repeat(t / DayLoopSec, 1f);     // 0=새벽 0.25=한낮 0.5=해질녘 0.75=한밤

            // 밤 정도: 낮 0 → 밤 1.  코사인이라 전환이 부드럽다.
            float night = Mathf.Clamp01(0.5f - 0.5f * Mathf.Cos((day - 0.5f) * 2f * Mathf.PI));
            // 노을: 해질녘/새벽에 잠깐 붉어진다.
            float dusk = Mathf.Clamp01(1f - Mathf.Abs(day - 0.5f) * 6f)
                       + Mathf.Clamp01(1f - Mathf.Abs(day - 0.0f) * 6f)
                       + Mathf.Clamp01(1f - Mathf.Abs(day - 1.0f) * 6f);
            dusk = Mathf.Clamp01(dusk);

            if (nightTint != null)
            {
                // 밤 = 짙은 남색, 노을 = 주황.  둘을 섞어 하루를 만든다.
                var nightCol = new Color(0.10f, 0.13f, 0.30f, 0.62f * night);
                var duskCol  = new Color(0.85f, 0.42f, 0.18f, 0.22f * dusk * (1f - night));
                nightTint.color = new Color(
                    Mathf.Lerp(duskCol.r, nightCol.r, night),
                    Mathf.Lerp(duskCol.g, nightCol.g, night),
                    Mathf.Lerp(duskCol.b, nightCol.b, night),
                    nightCol.a + duskCol.a);
            }

            if (stars != null)
            {
                float sa = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((night - 0.35f) / 0.5f));
                for (int i = 0; i < stars.Length; i++)
                {
                    if (stars[i] == null) continue;
                    // 별마다 다른 속도로 반짝여야 '점 무늬'가 아니라 하늘로 읽힌다.
                    float tw = 0.65f + 0.35f * Mathf.Sin(t * (1.1f + i * 0.037f) + i);
                    stars[i].color = new Color(1f, 0.97f, 0.88f, sa * tw);
                }
            }

            if (lamps != null)
            {
                float la = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((night - 0.25f) / 0.45f));
                for (int i = 0; i < lamps.Length; i++)
                {
                    if (lamps[i] == null) continue;
                    float flick = 0.82f + 0.18f * Mathf.Sin(t * (3.1f + i * 0.7f) + i * 2.3f);
                    lamps[i].color = new Color(1f, 0.78f, 0.42f, 0.55f * la * flick);
                }
            }

            if (walkers != null)
            {
                for (int i = 0; i < walkers.Length; i++)
                {
                    if (walkers[i] == null) continue;
                    // 2026-07-30 운영자: "타이틀이 캐릭터들이 거꾸로 걷는데?"
                    //  1차 구현은 PingPong 의 진행 방향을 부호 조합으로 역산했는데, 왕복
                    //  구간에서 뒤집혀 **걷는 방향과 보는 방향이 반대**가 됐다.
                    //  역산하지 말고 **실제 위치를 두 시점에서 구해 그 차이로 방향을 정한다** —
                    //  정의상 틀릴 수 없고, 왕복 지점에서도 자동으로 맞는다.
                    float ax     = WalkerX(i, t);
                    float axNext = WalkerX(i, t + 0.08f);
                    bool facingRight = axNext >= ax;

                    walkers[i].anchorMin = walkers[i].anchorMax = new Vector2(ax, walkerY[i]);
                    walkers[i].anchoredPosition = Vector2.zero;

                    var frames = walkFrames[walkerVariant[i]];
                    if (frames != null && frames.Length > 0)
                    {
                        int f = Mathf.FloorToInt(t * 8f + i * 2f) % frames.Length;
                        walkerImg[i].sprite = frames[f];
                        walkerImg[i].enabled = true;
                    }
                    else walkerImg[i].enabled = false;

                    // 시트의 측면 보행은 동쪽(오른쪽) 기준 — 왼쪽으로 갈 땐 X 반전.
                    float sx = facingRight ? 1f : -1f;
                    walkers[i].localScale = new Vector3(sx, 1f, 1f);
                }
            }
        }
    }
}
