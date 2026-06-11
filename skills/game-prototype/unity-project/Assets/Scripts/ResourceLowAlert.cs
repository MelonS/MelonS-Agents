using UnityEngine;
using UnityEngine.UI;
using MelonS.GameProto.Core;

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
            font = UITheme.LoadKoreanFont(18);   // #ui백로그 7.7 — 사본 제거, 공용 로더
            rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(280, 64);
            rt.anchoredPosition = new Vector2(-12, -462);  // #ui백로그 7.0 밴드 계약: ...EventLog(-404)>Toast(-454) 아래

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

        private float lastCheck = -10f;

        private void Update()
        {
            // #ui백로그 7.6/#1.3 — 조건 판정만 1s 스로틀.  펄스가 게이트 안에 있으면
            //  sin(주기 ~2.09s)을 1Hz 로 샘플링해 에일리어싱 점프(스텝 깜빡임)가 됐다.
            if (Time.unscaledTime - lastCheck >= 1.0f)
            {
                lastCheck = Time.unscaledTime;
                var rm = ResourceManager.Instance;
                if (rm != null)
                {
                    string msg = null;
                    int totalFood = rm.food + rm.meals * 3 + rm.fineMeals * 5;
                    // r2-J (2026-06-12) — 식량 임계 인구 연동: 고정 5 는 3림 기준 반나절치도
                    //  안 돼 경보가 너무 늦었다 (소비 ~9유닛/림·일).  림 수 x 4.5 = 반일치.
                    int alive = 0;
                    foreach (var pe in UnityEngine.Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
                        if (pe != null && !pe.IsDead) alive++;
                    int foodFloor = Mathf.Max(5, Mathf.RoundToInt(alive * 4.5f));
                    if (rm.wood < 5) msg = "⚠ 목재 부족 (벌목 필요)";
                    else if (totalFood < foodFloor) msg = $"⚠ 식량 부족 — 반나절치 미만 (사냥/채집/요리)";

                    if (msg != null)
                    {
                        if (label.text != msg) label.text = msg;
                        if (!gameObject.activeSelf) gameObject.SetActive(true);
                    }
                    else if (gameObject.activeSelf) gameObject.SetActive(false);
                }
            }

            // 감사 rank7: 전체 밝기+알파 동반 펄스(또렷한 맥동) — 매 프레임, 게이트 밖.
            if (gameObject.activeSelf)
            {
                float s = (Mathf.Sin(Time.unscaledTime * 3f) + 1f) * 0.5f;   // 0..1
                float p = Mathf.Lerp(0.80f, 1.10f, s);
                bg.color = new Color(0.55f * p, 0.13f * p, 0.13f * p, Mathf.Lerp(0.80f, 0.96f, s));
            }
        }
    }
}
