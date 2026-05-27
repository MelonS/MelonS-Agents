using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 피드백 #105 - 오브젝트 클릭 시 정보 패널.
    /// 트리/벽/화덕/연구대/침대/floor/door 등 비-pawn entity 좌클릭 시
    /// 우상단 (EventLog 아래) 작은 정보 패널 표시.
    /// PawnInfoPanel 은 pawn 전용이라 별도.
    ///
    /// Self-bootstrap (GameManager.EnsureInScene).  ClickSelector.CurrentInspect 폴링.
    /// </summary>
    public class EntityInspectorPanel : MonoBehaviour
    {
        private static EntityInspectorPanel _instance;
        private Text titleText, bodyText;
        private Image bg;
        private ClickSelector cachedCs;

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("EntityInspectorPanel");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<EntityInspectorPanel>();
        }

        private void Awake()
        {
            // 운영자 fb #117 - 패널 안 보였음. 우측 중앙으로 옮기고 키움 + 노란 outline 으로 강조.
            var rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(320, 160);
            rt.anchoredPosition = new Vector2(-12, 80);  // 화면 우측 중앙 약간 위

            bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.08f, 0.95f);

            // 제목 - 노란 강조 (림 inspect 느낌)
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(transform, false);
            titleText = titleGo.AddComponent<Text>();
            titleText.text = "";
            titleText.font = LoadKoreanFont(24);
            titleText.fontSize = 24;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(1.0f, 0.85f, 0.30f, 1f);  // 노란
            titleText.alignment = TextAnchor.UpperLeft;
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0.70f);
            trt.anchorMax = new Vector2(1, 1);
            trt.sizeDelta = new Vector2(-16, -4);
            trt.anchoredPosition = new Vector2(8, -4);

            // 본문 (info text)
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            bodyText = bodyGo.AddComponent<Text>();
            bodyText.text = "";
            bodyText.font = LoadKoreanFont(15);
            bodyText.fontSize = 15;
            bodyText.color = new Color(0.92f, 0.92f, 0.88f, 1f);
            bodyText.alignment = TextAnchor.UpperLeft;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            var brt = bodyGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(1, 0.65f);
            brt.sizeDelta = new Vector2(-16, -4);
            brt.anchoredPosition = new Vector2(8, 4);

            gameObject.SetActive(false);
        }

        private Font LoadKoreanFont(int sz)
        {
            string[] candidates = { "Malgun Gothic", "NanumGothic", "Gulim", "Dotum", "Arial Unicode MS" };
            foreach (var name in candidates)
            {
                var f = Font.CreateDynamicFontFromOSFont(name, sz);
                if (f != null) return f;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void Update()
        {
            if (cachedCs == null) cachedCs = Object.FindFirstObjectByType<ClickSelector>();
            if (cachedCs == null) return;
            GameObject inspect = cachedCs.CurrentInspect;
            if (inspect == null)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            (string title, string body) = Describe(inspect);
            if (titleText.text != title) titleText.text = title;
            if (bodyText.text != body) bodyText.text = body;
        }

        private (string, string) Describe(GameObject go)
        {
            var bp = go.GetComponent<BlueprintEntity>();
            if (bp != null) return ($"청사진 ({bp.Mode})",
                $"진행도: {bp.Progress * 100f:F0}%\n예약: {(bp.IsReserved ? "건설중" : "대기")}\npawn 이 와서 {bp.BuildSeconds:F0}초 건설");
            var pile = go.GetComponent<WoodPileEntity>();
            if (pile != null) return ("통나무 더미",
                $"목재 {pile.Wood}개\n예약: {(pile.IsReserved ? "운반중" : "대기")}\n2분 후 사라짐");
            var vein = go.GetComponent<StoneVeinEntity>();
            if (vein != null) return ("광맥 (석재)",
                $"HP 200, 채광 시 돌 1-3개\n선택 후 우클릭 = 채광 우선");
            var chunk = go.GetComponent<StoneChunkEntity>();
            if (chunk != null) return ("돌덩이",
                $"석재 {chunk.Stone}개\n예약: {(chunk.IsReserved ? "운반중" : "대기")}\n3분 후 사라짐");
            var sp = go.GetComponent<StockpileZoneEntity>();
            if (sp != null) return ("창고 영역",
                $"hauler 가 자원을 여기로 운반\n근처 자원 자동 수집");
            var tree = go.GetComponent<TreeEntity>();
            if (tree != null) return ("나무", $"위치: ({go.transform.position.x:F0}, {go.transform.position.y:F0})\n선택 후 우클릭 = 벌목 → 목재 +5\nHP 100, 25 dmg/sec");
            var bush = go.GetComponent<BerryBushEntity>();
            if (bush != null) return ("베리덤불", $"위치: ({go.transform.position.x:F0}, {go.transform.position.y:F0})\nfood<40 pawn 이 자동 채집\n베리 재생 ~30s");
            var crop = go.GetComponent<CropEntity>();
            if (crop != null)
            {
                string stage = crop.IsRipe ? "익음 (수확 가능)" : "성장 중";
                return ("작물 (벼)", $"상태: {stage}\n익으면 우클릭 = +5 식량\n3 stage 시각 변화");
            }
            var wall = go.GetComponent<WallEntity>();
            if (wall != null) return ("벽", $"목재 5, 충돌 collider 있음\npawn 통과 X");
            var door = go.GetComponent<DoorEntity>();
            if (door != null) return ("문", $"목재 3, trigger collider\npawn 통과 가능");
            var floor = go.GetComponent<FloorEntity>();
            if (floor != null) return ("바닥", $"목재 1, 실내 marker\n날씨 보호 효과");
            var stove = go.GetComponent<StoveEntity>();
            if (stove != null) return ("화덕", $"목재 10\npawn 자동 cook (food 5 → meal 1)");
            var bed = go.GetComponent<BedEntity>();
            if (bed != null) return ("침대", $"목재 8\n밤 수면 시 1.6x 회복 + mood +5/s");
            var bench = go.GetComponent<ResearchBench>();
            if (bench != null) return ("연구대", $"radius 1.5 안 pawn 시 연구 진행\n2 pt/sec/pawn");
            var wolf = go.GetComponent<WolfEnemy>();
            if (wolf != null) return ("늑대 [위협]", $"HP {wolf.Hp}/18, dmg 4\ndetect 5u, chase speed 2.5\n드래프트 후 우클릭 = 공격");
            var bandit = go.GetComponent<BanditEnemy>();
            if (bandit != null) return ("강도 [위협]", $"HP {bandit.Hp}/20, contact dmg\n드래프트 후 우클릭 = 공격");
            var animal = go.GetComponent<AnimalEntity>();
            if (animal != null) return ("사슴", $"길들이기 30% (food 1)\n사냥: 드래프트 후 우클릭 → food +5");
            var trader = go.GetComponent<TraderEntity>();
            if (trader != null) return ("상인", $"우클릭 = wood 5 → food 8 거래\n60s 머무름");
            return ("오브젝트", go.name);
        }
    }
}
