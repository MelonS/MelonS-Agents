using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 피드백 2026-05-27: "디자인 구리고 프로토타입 수준도 안되고" — 콜로니스트 선택 시
    /// 시각적 피드백이 살짝 색 변하는 것만 있었음. 명시적 ring 추가:
    ///   선택된 pawn 발밑에 노란 원 (펄스 애니메이션) 1개.
    /// ClickSelector.CurrentSelection 매 frame 폴링 (cheap).
    ///
    /// Self-bootstrapping — GameManager 가 EnsureInScene() 호출.
    /// </summary>
    public class SelectionRing : MonoBehaviour
    {
        private static SelectionRing _instance;
        private SpriteRenderer sr;
        private float spawnTime;

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            var go = new GameObject("SelectionRing");
            _instance = go.AddComponent<SelectionRing>();
        }

        private void Awake()
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = MakeRingSprite();
            sr.color = new Color(1f, 0.9f, 0.3f, 0.0f);  // 시작 invisible
            sr.sortingOrder = 6;  // pawn(10) 보다 낮음, ground(0) 보다 높음
            transform.localScale = new Vector3(1.6f, 1.6f, 1f);
            spawnTime = Time.time;
        }

        private static Sprite _ringSpriteCache;
        private static Sprite MakeRingSprite()
        {
            if (_ringSpriteCache != null) return _ringSpriteCache;
            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            float cx = size * 0.5f, cy = size * 0.5f;
            float outerR = size * 0.45f;
            float innerR = size * 0.36f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float d = Mathf.Sqrt(dx*dx + dy*dy);
                    if (d <= outerR && d >= innerR)
                    {
                        // soft edge
                        float aOuter = Mathf.SmoothStep(1f, 0f, (d - innerR - 2f) / Mathf.Max(1f, outerR - innerR - 2f));
                        float aInner = Mathf.SmoothStep(0f, 1f, (d - innerR) / 3f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Min(aOuter, aInner));
                    }
                    else
                    {
                        pixels[y * size + x] = new Color(0, 0, 0, 0);
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _ringSpriteCache = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _ringSpriteCache;
        }

        private void Update()
        {
            var cs = Object.FindFirstObjectByType<ClickSelector>();
            if (cs == null || cs.CurrentSelection == null || cs.CurrentSelection.IsDead)
            {
                // 페이드 아웃
                if (sr.color.a > 0f)
                {
                    var c = sr.color; c.a = Mathf.MoveTowards(c.a, 0f, Time.deltaTime * 4f);
                    sr.color = c;
                }
                return;
            }
            // 선택된 pawn 발밑 (살짝 아래 - 32x32 pawn 기준 -0.35)
            var pos = cs.CurrentSelection.transform.position;
            transform.position = new Vector3(pos.x, pos.y - 0.35f, pos.z);

            // 색: drafted 면 cyan, 일반은 노랑
            Color baseCol = cs.CurrentSelection.IsDrafted
                ? new Color(0.4f, 0.85f, 1f)
                : new Color(1f, 0.92f, 0.3f);

            // 펄스 (0.5초 주기, alpha 0.55 ~ 0.95)
            float pulse = 0.75f + 0.20f * Mathf.Sin(Time.time * 5f);
            baseCol.a = pulse;
            sr.color = baseCol;

            // 스케일 펄스 (subtle - 1.5 ~ 1.7)
            float scale = 1.6f + 0.10f * Mathf.Sin(Time.time * 5f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
