using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 초기화면 모션 (2026-07-25 운영자 "초기화면이 좀 움직였으면 — 너무 이미지만").
    /// 베이크된 키아트 위에 런타임으로:
    ///  ① 켄 번스 — 배경 슬로우 줌(1.07±0.065, 16s 주기) + 드리프트
    ///  ② 모닥불 불씨 — 키아트 캠프파이어 위치에서 피어오르는 앰버 파티클 14개
    ///  ③ 타이틀/부제 페이드인 — 로드 시 1.2s 정착
    /// 씬 재베이크 불요(기존 드라이버 패턴), 셰이더/에셋 불요 = WebGL 리스크 0.
    /// </summary>
    public class MainMenuMotion : MonoBehaviour
    {
        private RectTransform backdrop;
        private Text title, subtitle;
        private float t0;

        // 진폭 상향 (2026-07-29, 운영자 "타이틀 좀 움직이는 애니메이션 같은거
        //  넣어달라고 했던거 같은데").  실은 이미 들어 있었다 — 프레임 diff 로
        //  6초에 픽셀 7.66% 변화가 측정된다.  문제는 **너무 미세해서 안 보이는 것**
        //  이었다(평균차 2.7/255).  없는 기능을 만드는 게 아니라 체감되게 올린다.
        private const int EmberCount = 30;
        // 메뉴2_01 새벽능선 아트 (2026-07-25 운영자 픽): 모닥불이 없으므로
        //  파티클 컨셉을 '초원 위로 떠오르는 금빛 모트(꽃가루/빛입자)'로 —
        //  하단 초원 전폭에서 넓게 분산 스폰.
        private Vector2[] emberAnchor;
        private RectTransform[] embers;
        private float[] phase, speed, life, sway;

        private void Start()
        {
            t0 = Time.unscaledTime;
            var bgGo = GameObject.Find("Backdrop");
            if (bgGo != null) backdrop = bgGo.GetComponent<RectTransform>();
            var tGo = GameObject.Find("Title");
            if (tGo != null) title = tGo.GetComponent<Text>();
            var sGo = GameObject.Find("Subtitle");
            if (sGo != null) subtitle = sGo.GetComponent<Text>();

            if (backdrop != null) SpawnEmbers(backdrop.parent);
        }

        private void SpawnEmbers(Transform canvas)
        {
            var sp = MakeDotSprite();
            embers = new RectTransform[EmberCount];
            phase = new float[EmberCount];
            speed = new float[EmberCount];
            life = new float[EmberCount];
            sway = new float[EmberCount];
            var root = new GameObject("Embers", typeof(RectTransform));
            var rrt = (RectTransform)root.transform;
            rrt.SetParent(canvas, false);
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
            // 딤 위·타이틀 아래 (Backdrop=0, Dim=1 다음)
            root.transform.SetSiblingIndex(Mathf.Min(2, canvas.childCount - 1));
            var rng = new System.Random(7411);
            emberAnchor = new Vector2[EmberCount];
            for (int i = 0; i < EmberCount; i++)
            {
                var go = new GameObject("ember", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(root.transform, false);
                emberAnchor[i] = new Vector2(
                    0.08f + (float)rng.NextDouble() * 0.84f,
                    0.10f + (float)rng.NextDouble() * 0.22f);
                rt.anchorMin = rt.anchorMax = emberAnchor[i];
                float s = 4f + (float)rng.NextDouble() * 5f;
                rt.sizeDelta = new Vector2(s, s);
                var img = go.GetComponent<Image>();
                img.sprite = sp;
                img.raycastTarget = false;
                embers[i] = rt;
                phase[i] = (float)rng.NextDouble() * 8f;
                speed[i] = 22f + (float)rng.NextDouble() * 30f;    // 모트 = 불씨보다 느긋하게
                life[i] = 5f + (float)rng.NextDouble() * 4f;
                sway[i] = 18f + (float)rng.NextDouble() * 30f;
            }
        }

        private void Update()
        {
            float t = Time.unscaledTime - t0;
            // ① 켄 번스
            if (backdrop != null)
            {
                float z = 1.075f + 0.065f * Mathf.Sin(t * (2f * Mathf.PI / 16f));
                backdrop.localScale = new Vector3(z, z, 1f);
                backdrop.anchoredPosition = new Vector2(
                    Mathf.Sin(t * 0.17f) * 22f, Mathf.Cos(t * 0.11f) * 14f);
            }
            // ③ 타이틀 페이드인 (+부제 0.35s 지연)
            if (title != null) SetAlpha(title, Mathf.Clamp01(t / 1.2f));
            if (subtitle != null) SetAlpha(subtitle, Mathf.Clamp01((t - 0.35f) / 1.2f));
            // ② 불씨 — 위상 루프: 상승 + 사인 스웨이 + 후반 페이드
            if (embers != null)
            {
                for (int i = 0; i < EmberCount; i++)
                {
                    float u = ((t + phase[i]) % life[i]) / life[i];   // 0~1 수명 진행
                    float rise = u * speed[i] * life[i] * 0.55f;
                    float sx = Mathf.Sin((t + phase[i]) * 2.1f + i) * sway[i] * u;
                    embers[i].anchoredPosition = new Vector2(sx, rise);
                    float a = Mathf.Clamp01(u * 5f) * (1f - Mathf.SmoothStep(0.45f, 1f, u));
                    var img = embers[i].GetComponent<Image>();
                    // 금빛 모트 — 새벽 역광 꽃가루 느낌 (식는 색 변화 없음)
                    img.color = new Color(1f, 0.88f, 0.55f, 0.65f * a);
                    float sc = 1f - 0.45f * u;
                    embers[i].localScale = new Vector3(sc, sc, 1f);
                }
            }
        }

        private static void SetAlpha(Text txt, float a)
        {
            var c = txt.color; c.a = a; txt.color = c;
            var sh = txt.GetComponent<Shadow>();
            if (sh != null) { var ec = sh.effectColor; ec.a = 0.85f * a; sh.effectColor = ec; }
        }

        /// <summary>6x6 라디얼 도트 — 불씨용 코드 생성 스프라이트.</summary>
        private static Sprite MakeDotSprite()
        {
            var tex = new Texture2D(6, 6, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 6; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(2.5f, 2.5f)) / 2.6f;
                    byte a = (byte)(255f * Mathf.Clamp01(1f - d));
                    tex.SetPixel(x, y, new Color32(255, 255, 255, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 6, 6), new Vector2(0.5f, 0.5f), 6f);
        }

        // ── self-bootstrap (MainMenu 씬 전용) ────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += (sc, _) => Ensure();
            Ensure();
        }

        private static void Ensure()
        {
            if (SceneManager.GetActiveScene().name != "MainMenu") return;
            if (Object.FindFirstObjectByType<MainMenuMotion>() == null)
                new GameObject("__MenuMotion__").AddComponent<MainMenuMotion>();
        }
    }
}
