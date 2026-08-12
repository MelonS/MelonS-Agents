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
            sr.sprite = MakeBracketSprite();
            sr.color = new Color(0.94f, 0.94f, 0.90f, 0.0f);  // 시작 invisible — TOP-6 화이트
            sr.sortingOrder = 12;  // 브래킷은 본체(10)를 '둘러싸는' 표식 — 위에 그린다
            transform.localScale = new Vector3(1.6f, 1.6f, 1f);
            spawnTime = Time.time;
        }

        private static Sprite _ringSpriteCache;
        /// <summary>#audit3 #0/#1 — 멀티선택(MultiSelectionRings)·인스펙트(InspectHighlight)가
        /// 같은 스프라이트를 재사용하도록 공개.  TOP-6 (visual-polish-backlog 2026-06-11):
        /// 타원 링/노란 박스/링 3종 혼재 → 콜로니심식 4코너 브래킷 1종으로 통일.</summary>
        public static Sprite SharedRingSprite() => MakeBracketSprite();
        private static Sprite MakeBracketSprite()
        {
            if (_ringSpriteCache != null) return _ringSpriteCache;
            const int size = 64;
            const int arm = 16;    // 코너 팔 길이
            const int thick = 5;   // 두께
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 각 축의 가장자리까지 거리 (코너 판정용)
                    int ex = Mathf.Min(x, size - 1 - x);
                    int ey = Mathf.Min(y, size - 1 - y);
                    bool inCorner = (ex < arm && ey < thick) || (ey < arm && ex < thick);
                    pixels[y * size + x] = inCorner
                        ? new Color(1f, 1f, 1f, 1f)
                        : new Color(0f, 0f, 0f, 0f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _ringSpriteCache = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            _ringSpriteCache.name = "SelectionBrackets";
            return _ringSpriteCache;
        }

        // Lesson #4 - FindFirstObjectByType per-Update 비쌈.  cache - ClickSelector 는 singleton-ish.
        private ClickSelector cachedCs;

        private void Update()
        {
            // 캐시 stale 검사 - Unity 의 fake null 잡음
            if (cachedCs == null) cachedCs = Object.FindFirstObjectByType<ClickSelector>();
            var cs = cachedCs;
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
            // TOP-6 — 브래킷은 몸 중심을 둘러싼다 (발밑 오프셋 제거).
            var pos = cs.CurrentSelection.transform.position;
            transform.position = new Vector3(pos.x, pos.y, pos.z);

            // 색: drafted 면 시안, 일반은 화이트 (노랑은 공격 타깃 전용 예약).
            Color baseCol = cs.CurrentSelection.IsDrafted
                ? new Color(0.4f, 0.85f, 1f)
                : new Color(0.94f, 0.94f, 0.90f);

            // 펄스 — 1Hz 잔잔하게 (기존 5rad/s 깜빡임은 산만).
            float pulse = 0.78f + 0.18f * Mathf.Sin(Time.time * Mathf.PI * 2f);
            baseCol.a = pulse;
            sr.color = baseCol;

            // 스케일 고정 (스케일 펄스 제거 — 브래킷이 숨쉬면 어지럽다)
            transform.localScale = new Vector3(1.5f, 1.5f, 1f);
        }
    }
}
