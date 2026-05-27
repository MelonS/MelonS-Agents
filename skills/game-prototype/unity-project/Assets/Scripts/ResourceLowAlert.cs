using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// #136 - 자원 부족 시 우상단 빨강 popup.
    ///  wood < 5, food + meals < 5, stone < 0 (0 이면 OK) 등 조건마다 텍스트.
    ///  자원 회복 시 자동 사라짐.  ThreatAlertUI 패턴 동일.
    /// </summary>
    public class ResourceLowAlert : MonoBehaviour
    {
        private static ResourceLowAlert _instance;
        private RectTransform rt;
        private Image bg;
        private Text label;
        private Font font;

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("ResourceLowAlert");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<ResourceLowAlert>();
        }

        private void Awake()
        {
            font = LoadKoreanFont();
            rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(280, 64);
            rt.anchoredPosition = new Vector2(-12, -100);  // TopBar 아래

            bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.45f, 0.10f, 0.10f, 0.92f);
            bg.raycastTarget = false;

            var lbl = new GameObject("Label");
            lbl.transform.SetParent(transform, false);
            label = lbl.AddComponent<Text>();
            label.font = font; label.fontSize = 18; label.fontStyle = FontStyle.Bold;
            label.color = new Color(1f, 0.95f, 0.85f, 1f);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            var lrt = lbl.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.sizeDelta = Vector2.zero; lrt.anchoredPosition = Vector2.zero;

            gameObject.SetActive(false);
        }

        private Font LoadKoreanFont()
        {
            string[] cand = { "Malgun Gothic", "NanumGothic", "Gulim", "Dotum", "Arial Unicode MS" };
            foreach (var n in cand)
            {
                var f = Font.CreateDynamicFontFromOSFont(n, 18);
                if (f != null) return f;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private float lastCheck = -10f;

        private void Update()
        {
            if (Time.unscaledTime - lastCheck < 1.0f) return;  // 1s 폴링
            lastCheck = Time.unscaledTime;
            var rm = ResourceManager.Instance;
            if (rm == null) return;

            string msg = null;
            int totalFood = rm.food + rm.meals * 3 + rm.fineMeals * 5;
            if (rm.wood < 5) msg = "⚠ 목재 부족 (벌목 필요)";
            else if (totalFood < 5) msg = "⚠ 식량 부족 (사냥/채집 필요)";

            if (msg != null)
            {
                if (label.text != msg) label.text = msg;
                if (!gameObject.activeSelf) gameObject.SetActive(true);
                // 펄스
                float pulse = 0.75f + 0.20f * Mathf.Sin(Time.unscaledTime * 3f);
                bg.color = new Color(0.45f * pulse, 0.10f, 0.10f, 0.92f);
            }
            else
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
            }
        }
    }
}
