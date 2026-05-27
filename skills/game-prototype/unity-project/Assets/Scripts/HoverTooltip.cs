using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 피드백 2026-05-27 "프로토타입 수준도 안됨":
    /// 마우스 hover 시 그 entity 가 뭔지 + 우클릭 시 어떤 action 일어나는지 표시.
    /// 사용자가 "이게 뭐고 뭐 할 수 있지" 알 수 있게.
    ///
    /// 매 프레임 mouse pos → OverlapPoint → entity 종류 식별 → 짧은 한국어 텍스트.
    /// UI Canvas 위 작은 패널, mouse 옆 (offset 14px).
    /// </summary>
    public class HoverTooltip : MonoBehaviour
    {
        private static HoverTooltip _instance;
        private RectTransform rt;
        private Image bg;
        private Text label;
        private Camera mainCam;
        private Canvas canvas;

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("HoverTooltip");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<HoverTooltip>();
            _instance.canvas = canvas;
        }

        private void Awake()
        {
            mainCam = Camera.main;
            // panel
            rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(200, 32);
            bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.08f, 0.88f);
            bg.raycastTarget = false;
            // label
            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(transform, false);
            label = lblGo.AddComponent<Text>();
            label.text = "";
            label.font = LoadKoreanFont();
            label.fontSize = 14;
            label.color = new Color(0.95f, 0.95f, 0.9f, 1f);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.sizeDelta = new Vector2(-8, -4);
            lrt.anchoredPosition = Vector2.zero;
            // start hidden
            gameObject.SetActive(false);
        }

        private Font LoadKoreanFont()
        {
            string[] candidates = { "Malgun Gothic", "NanumGothic", "Gulim", "Dotum", "Arial Unicode MS" };
            foreach (var name in candidates)
            {
                var f = Font.CreateDynamicFontFromOSFont(name, 14);
                if (f != null) return f;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void Update()
        {
            // UI 위면 가림 (GUI 버튼 hover 시 tooltip 가리면 UX 나쁨)
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (overUI) { Hide(); return; }
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) { Hide(); return; }

            Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
            Collider2D hit = Physics2D.OverlapPoint(new Vector2(mouseWorld.x, mouseWorld.y));
            string text = DescribeHit(hit);
            if (string.IsNullOrEmpty(text)) { Hide(); return; }

            // Show + position
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            label.text = text;
            // resize bg by text width (approx 8px per Korean char + padding)
            float w = Mathf.Max(120, label.preferredWidth + 16);
            rt.sizeDelta = new Vector2(w, 32);
            // 위치: mouse 옆, 살짝 위 (Canvas Screen-Space-Overlay 라면 mouse pos 그대로)
            Vector2 pos = Input.mousePosition;
            // CanvasScaler 적용된 좌표 변환
            if (canvas != null)
            {
                var canvasRt = canvas.GetComponent<RectTransform>();
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, pos, null, out localPos);
                rt.anchoredPosition = new Vector2(localPos.x + 14, localPos.y + 18);
            }
        }

        private void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private string DescribeHit(Collider2D hit)
        {
            if (hit == null) return "";
            var pawn = hit.GetComponent<PawnEntity>();
            if (pawn != null)
            {
                string draftSuffix = pawn.IsDrafted ? " [징집됨]" : "";
                return $"콜로니스트 {pawn.PawnName}{draftSuffix}  (좌클릭=선택)";
            }
            var tree = hit.GetComponent<TreeEntity>();
            if (tree != null) return "나무  (선택 후 우클릭=벌목)";
            var bush = hit.GetComponent<BerryBushEntity>();
            if (bush != null) return "베리덤불  (자동 채집)";
            var crop = hit.GetComponent<CropEntity>();
            if (crop != null)
            {
                if (crop.IsRipe) return "익은 작물  (우클릭=수확 +5 식량)";
                return "자라는 중인 작물  (익을 때까지 대기)";
            }
            var animal = hit.GetComponent<AnimalEntity>();
            if (animal != null) return "사슴  (드래프트 후 우클릭=사냥 / 우클릭=길들이기)";
            var wolf = hit.GetComponent<WolfEnemy>();
            if (wolf != null) return "늑대 (위협!)  (드래프트 후 우클릭=공격)";
            var bandit = hit.GetComponent<BanditEnemy>();
            if (bandit != null) return "강도 (위협!)  (드래프트 후 우클릭=공격)";
            var trader = hit.GetComponent<TraderEntity>();
            if (trader != null) return "상인  (우클릭=교역)";
            var bench = hit.GetComponent<ResearchBench>();
            if (bench != null) return "연구대  (콜로니스트 옆에 있으면 자동 연구)";
            var stove = hit.GetComponent<StoveEntity>();
            if (stove != null) return "화덕  (콜로니스트가 자동 요리)";
            var wall = hit.GetComponent<WallEntity>();
            if (wall != null) return "벽";
            var door = hit.GetComponent<DoorEntity>();
            if (door != null) return "문 (콜로니스트 통과 가능)";
            var floor = hit.GetComponent<FloorEntity>();
            if (floor != null) return "바닥";
            return "";
        }
    }
}
