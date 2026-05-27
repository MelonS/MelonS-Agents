using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 우클릭 위치 visualization — 사용자가 어디 클릭했는지 0.6초 동안 X 마커 표시.
    /// 운영자 "어디 클릭했는지 모르겠음" 피드백 대응.
    /// 1초 자동 destroy.
    /// </summary>
    public class ClickEffect : MonoBehaviour
    {
        private float spawnTime;
        private float lifetime = 0.6f;
        private SpriteRenderer sr;

        private static Sprite _whiteSprite;
        private static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null)
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                    tex.Apply();
                    _whiteSprite = Sprite.Create(tex, new Rect(0,0,2,2), new Vector2(0.5f,0.5f), 2f);
                }
                return _whiteSprite;
            }
        }

        public static void Spawn(Vector3 worldPos, Color color)
        {
            // 작은 X 마커 - 두 줄 cross (가로/세로 별도 GameObject)
            float size = 0.6f;
            float thick = 0.08f;
            var parent = new GameObject("ClickEffect");
            parent.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
            parent.AddComponent<ClickEffect>();
            // 수평
            var hg = new GameObject("H");
            hg.transform.SetParent(parent.transform, false);
            hg.transform.localScale = new Vector3(size, thick, 1f);
            var hsr = hg.AddComponent<SpriteRenderer>();
            hsr.sprite = WhiteSprite; hsr.color = color; hsr.sortingOrder = 40;
            // 수직
            var vg = new GameObject("V");
            vg.transform.SetParent(parent.transform, false);
            vg.transform.localScale = new Vector3(thick, size, 1f);
            var vsr = vg.AddComponent<SpriteRenderer>();
            vsr.sprite = WhiteSprite; vsr.color = color; vsr.sortingOrder = 40;
        }

        private void Awake() { spawnTime = Time.time; }

        private void Update()
        {
            float t = (Time.time - spawnTime) / lifetime;
            if (t >= 1f) { Destroy(gameObject); return; }
            // fade out
            float alpha = Mathf.Lerp(1f, 0f, t);
            foreach (Transform child in transform)
            {
                var sr = child.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    var c = sr.color; c.a = alpha; sr.color = c;
                }
            }
            // grow slightly
            float scale = Mathf.Lerp(1f, 1.4f, t);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
