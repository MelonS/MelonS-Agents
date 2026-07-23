using UnityEngine;
using UnityEngine.SceneManagement;

namespace MelonS.GameProto
{
    /// <summary>
    /// 룩앤필 배치1-2 (2026-07-24 리서치 톱2): 구름 그림자 스크롤.
    /// 타일러블 펄린 노이즈(토러스 샘플) 알파 텍스처 2겹이 맵 위를 천천히 흘러
    /// 정지 화면에도 "세상이 살아있는" 미세 변화를 만든다.  낮 전용(밤엔 페이드).
    /// 셰이더 불요(SpriteRenderer 이동 + 주기 랩) — 스트립/WebGL 리스크 0.
    /// sortingOrder 24 = 전체 위·NightOverlay(25) 아래.
    /// </summary>
    public class CloudShadowDriver : MonoBehaviour
    {
        private const int TEX = 256;
        private const float WorldSize = 150f;          // 텍스처 1장이 덮는 월드 크기 (맵 90 + 여유)
        private const float MaxAlpha = 0.10f;          // 한낮 최대 그림자 농도 (은은)
        private struct Layer { public Transform[] pair; public Vector2 dir; public float speed; }
        private Layer[] layers;
        private SpriteRenderer[] all;

        private void Start()
        {
            var sp = MakeCloudSprite(9127);
            var sp2 = MakeCloudSprite(40211);
            layers = new[]
            {
                MakeLayer(sp,  new Vector2(1f, 0.35f).normalized, 0.9f),
                MakeLayer(sp2, new Vector2(1f, -0.15f).normalized, 1.5f),
            };
            var list = new System.Collections.Generic.List<SpriteRenderer>();
            foreach (var l in layers)
                foreach (var t in l.pair) list.Add(t.GetComponent<SpriteRenderer>());
            all = list.ToArray();
        }

        private Layer MakeLayer(Sprite sp, Vector2 dir, float speed)
        {
            var pair = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject($"CloudShadow_{sp.GetHashCode()}_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                sr.sortingOrder = 24;
                sr.color = new Color(0f, 0f, 0f, 0f);
                // 진행 방향으로 한 장 뒤에 세워 leapfrog — 랩 시 이음새 없음(타일러블).
                go.transform.position = (Vector3)(dir * WorldSize * -i);
            }
            return new Layer { pair = pair, dir = dir, speed = speed };
        }

        private void Update()
        {
            // 낮 전용: 일출(0.29)~일몰(0.77) 사이만, 가장자리 부드럽게.
            float t = GameClock.Instance != null ? GameClock.Instance.DayProgress : 0.5f;
            float day = Mathf.Clamp01(Mathf.Min(t - 0.27f, 0.79f - t) / 0.06f);
            float a = MaxAlpha * day;
            foreach (var sr in all)
            {
                var c = sr.color; c.a = a; sr.color = c;
            }
            if (a <= 0.001f) return;
            foreach (var l in layers)
            {
                foreach (var tr in l.pair)
                {
                    tr.position += (Vector3)(l.dir * l.speed * Time.deltaTime);
                    // 진행 방향 투영 거리가 반 주기를 넘으면 두 장 뒤로 랩.
                    if (Vector2.Dot(tr.position, l.dir) > WorldSize)
                        tr.position -= (Vector3)(l.dir * WorldSize * 2f);
                }
            }
        }

        /// <summary>토러스 좌표 4-혼합 펄린 = 타일러블 구름 알파 스프라이트.</summary>
        private static Sprite MakeCloudSprite(int seed)
        {
            var tex = new Texture2D(TEX, TEX, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            float o = seed * 0.013f;
            var px = new Color32[TEX * TEX];
            for (int y = 0; y < TEX; y++)
            {
                for (int x = 0; x < TEX; x++)
                {
                    float u = (float)x / TEX, v = (float)y / TEX;
                    // 토러스 4-혼합으로 이음새 제거
                    float n = TorusPerlin(u, v, 3f, o) * 0.65f + TorusPerlin(u, v, 7f, o + 31f) * 0.35f;
                    // 구름 덩어리만 남기고 바닥 컷 (하늘 60%는 그림자 없음)
                    float m = Mathf.Clamp01((n - 0.58f) / 0.42f);
                    m = m * m * (3f - 2f * m);   // smoothstep — 몽글한 가장자리
                    px[y * TEX + x] = new Color32(255, 255, 255, (byte)(m * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, TEX, TEX), new Vector2(0.5f, 0.5f), TEX / WorldSize);
        }

        private static float TorusPerlin(float u, float v, float freq, float o)
        {
            float x = u * freq, y = v * freq;
            float a = Mathf.PerlinNoise(x + o, y + o);
            float b = Mathf.PerlinNoise(x - freq + o, y + o);
            float c = Mathf.PerlinNoise(x + o, y - freq + o);
            float d = Mathf.PerlinNoise(x - freq + o, y - freq + o);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        // ── self-bootstrap (Game 씬 전용) ────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += (sc, _) => Ensure();
            Ensure();
        }

        private static void Ensure()
        {
            if (SceneManager.GetActiveScene().name != "Game") return;
            if (Object.FindFirstObjectByType<CloudShadowDriver>() == null)
                new GameObject("__CloudShadows__").AddComponent<CloudShadowDriver>();
        }
    }
}
