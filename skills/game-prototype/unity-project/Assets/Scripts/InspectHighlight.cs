using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb #138 - 좌클릭 시 정보 패널은 떴지만 어느 entity 선택됐는지 시각 X.
    ///  ClickSelector.CurrentInspect 폴링하여 그 entity 위치에 노란 사각 outline 표시.
    ///  SelectionRing 와 별개 (SelectionRing = pawn 전용 원 발밑, InspectHighlight = 모든 entity 사각 outline).
    ///
    /// Self-bootstrap (GameManager.EnsureInScene).
    /// </summary>
    public class InspectHighlight : MonoBehaviour
    {
        private static InspectHighlight _instance;
        private SpriteRenderer sr;
        private ClickSelector cachedCs;

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            var go = new GameObject("InspectHighlight");
            _instance = go.AddComponent<InspectHighlight>();
        }

        private static Sprite _boxSpriteCache;
        private static Sprite MakeBoxSprite()
        {
            if (_boxSpriteCache != null) return _boxSpriteCache;
            int size = 64;
            int thick = 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool border = x < thick || x >= size - thick || y < thick || y >= size - thick;
                    pixels[y * size + x] = border ? new Color(1f, 1f, 1f, 1f) : new Color(0, 0, 0, 0);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _boxSpriteCache = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _boxSpriteCache;
        }

        private void Awake()
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = MakeBoxSprite();
            sr.color = new Color(1f, 0.85f, 0.30f, 0f);
            sr.sortingOrder = 25;  // 모든 entity 위
            transform.localScale = new Vector3(1.5f, 1.5f, 1f);
        }

        private void Update()
        {
            if (cachedCs == null) cachedCs = Object.FindFirstObjectByType<ClickSelector>();
            if (cachedCs == null) return;
            GameObject ins = cachedCs.CurrentInspect;
            if (ins == null)
            {
                if (sr.color.a > 0f)
                {
                    var c = sr.color; c.a = Mathf.MoveTowards(c.a, 0f, Time.deltaTime * 4f);
                    sr.color = c;
                }
                return;
            }
            // 따라가기
            transform.position = new Vector3(ins.transform.position.x, ins.transform.position.y, 0);
            // entity bounds 에 맞춰 scale (collider 우선, 없으면 sprite)
            float w = 1.2f, h = 1.2f;
            var c2d = ins.GetComponent<Collider2D>();
            if (c2d != null)
            {
                w = c2d.bounds.size.x + 0.4f;
                h = c2d.bounds.size.y + 0.4f;
            }
            transform.localScale = new Vector3(w, h, 1f);
            // 펄스
            float pulse = 0.65f + 0.30f * Mathf.Sin(Time.time * 4f);
            sr.color = new Color(1f, 0.85f, 0.30f, pulse);
        }
    }
}
