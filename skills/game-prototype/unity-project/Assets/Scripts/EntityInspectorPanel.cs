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
            // 운영자 fb #128 - 운영자 패널 못 봄.
            //  원인 진단: (a) raycastTarget=true 가 클릭 차단 → 모두 false 로.
            //              (b) 패널 위치 작아서 못 봄 → 우측 stack 큰 패널.
            //              (c) hidden by default → 항상 보임 (없을 때는 hint text).
            var rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(360, 380);
            rt.anchoredPosition = new Vector2(-12, 0);  // 화면 우측 정확히 가운데

            bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.06f, 0.09f, 0.95f);
            bg.raycastTarget = false;  // #128 - 클릭 통과 (entity 클릭 안 막음)

            // 제목 - 노란 강조 (림 inspect 느낌)
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(transform, false);
            titleText = titleGo.AddComponent<Text>();
            titleText.text = "선택된 오브젝트 없음";
            titleText.font = LoadKoreanFont(22);
            titleText.fontSize = 22;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(1.0f, 0.85f, 0.30f, 1f);
            titleText.alignment = TextAnchor.UpperLeft;
            titleText.raycastTarget = false;
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0.85f);
            trt.anchorMax = new Vector2(1, 1);
            trt.sizeDelta = new Vector2(-16, -4);
            trt.anchoredPosition = new Vector2(8, -4);

            // 본문 (info text)
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            bodyText = bodyGo.AddComponent<Text>();
            bodyText.text = "💡 나무/벽/사슴/광맥 등을\n좌클릭하면 정보가 표시됩니다.";
            bodyText.font = LoadKoreanFont(15);
            bodyText.fontSize = 15;
            bodyText.color = new Color(0.85f, 0.85f, 0.78f, 1f);
            bodyText.alignment = TextAnchor.UpperLeft;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.raycastTarget = false;
            var brt = bodyGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(1, 0.85f);
            brt.sizeDelta = new Vector2(-16, -4);
            brt.anchoredPosition = new Vector2(8, 4);

            // 패널 항상 활성 (hint 가 보임).  SetActive(false) 제거.
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
            // #128 - 패널 항상 켜둠. inspect 없으면 hint 표시.
            if (inspect == null)
            {
                string hintTitle = "선택된 오브젝트 없음";
                string hintBody = "💡 나무/벽/사슴/광맥/콜로니스트 등을\n좌클릭하면 정보가 표시됩니다.\n\n📋 직업: F1\n🏛 건축: F8\n🔬 연구: N\n⏸ 멈춤: Space";
                if (titleText.text != hintTitle) titleText.text = hintTitle;
                if (bodyText.text != hintBody) bodyText.text = hintBody;
                return;
            }
            (string title, string body) = Describe(inspect);
            if (titleText.text != title) titleText.text = title;
            if (bodyText.text != body) bodyText.text = body;
        }

        private (string, string) Describe(GameObject go)
        {
            // #128 - pawn 도 패널에 표시 (좌측 PawnInfoPanel 와 동시 - 일관성)
            var pawn = go.GetComponent<PawnEntity>();
            if (pawn != null)
            {
                var needs = go.GetComponent<PawnNeeds>();
                var abil = go.GetComponent<PawnAbilities>();
                var traits = go.GetComponent<PawnTraits>();
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"위치: ({go.transform.position.x:F0}, {go.transform.position.y:F0})");
                if (traits != null) sb.AppendLine($"성격: {traits.SummaryKr()}");
                if (needs != null)
                {
                    sb.AppendLine($"식량: {needs.food:F0}/100");
                    sb.AppendLine($"수면: {needs.sleep:F0}/100");
                    sb.AppendLine($"기분: {needs.mood:F0}/100");
                }
                if (abil != null)
                {
                    sb.AppendLine($"\n능력:");
                    sb.AppendLine($"  이동 {abil.moveSpeedMul:F2}  벌목 {abil.chopMul:F2}");
                    sb.AppendLine($"  채광 {abil.miningMul:F2}  건설 {abil.constructionMul:F2}");
                }
                return ($"{pawn.PawnName}", sb.ToString());
            }
            var bp = go.GetComponent<BlueprintEntity>();
            if (bp != null)
            {
                string materials = "";
                if (bp.needWood > 0) materials += $"목재: {bp.collectedWood}/{bp.needWood}\n";
                if (bp.needStone > 0) materials += $"석재: {bp.collectedStone}/{bp.needStone}\n";
                string status = bp.HasAllMaterials
                    ? (bp.IsReserved ? "🔨 건설중" : "✓ 자재 완비, pawn 대기")
                    : "⏳ 자재 부족, hauler 운반 중";
                return ($"청사진 ({bp.Mode})",
                    $"{materials}진행도: {bp.Progress * 100f:F0}%\n{status}");
            }
            var pile = go.GetComponent<WoodPileEntity>();
            if (pile != null) return ("통나무 더미",
                $"목재 {pile.Wood}개\n예약: {(pile.IsReserved ? "운반중" : "대기")}\n2분 후 사라짐");
            var vein = go.GetComponent<StoneVeinEntity>();
            if (vein != null) return ($"광맥 ({vein.TypeKr})",
                $"종류: {vein.TypeKr}\n채광 시 돌 1-3개\n선택 후 우클릭 = 채광 우선\n(화강암 가장 단단, 사암 약함)");
            var chunk = go.GetComponent<StoneChunkEntity>();
            if (chunk != null) return ("돌덩이",
                $"석재 {chunk.Stone}개\n예약: {(chunk.IsReserved ? "운반중" : "대기")}\n3분 후 사라짐");
            var meat = go.GetComponent<MeatPileEntity>();
            if (meat != null) return ("고기",
                $"식량 {meat.Food}개\n예약: {(meat.IsReserved ? "운반중" : "대기")}\n1.5분 후 상함");
            var sp = go.GetComponent<StockpileZoneEntity>();
            if (sp != null) return ("창고 영역",
                $"hauler 가 자원을 여기로 운반\n근처 자원 자동 수집");
            var tree = go.GetComponent<TreeEntity>();
            if (tree != null) return ($"나무 ({tree.SpeciesKr})",
                $"위치: ({go.transform.position.x:F0}, {go.transform.position.y:F0})\n" +
                $"종류: {tree.SpeciesKr}\n" +
                "선택 후 우클릭 = 벌목 → 목재 떨어짐\n" +
                "(Oak 단단 7목재, Pine 빠름 4, Birch 5)");
            var bush = go.GetComponent<BerryBushEntity>();
            if (bush != null) return ("베리덤불", $"위치: ({go.transform.position.x:F0}, {go.transform.position.y:F0})\nfood<40 pawn 이 자동 채집\n베리 재생 ~30s");
            var crop = go.GetComponent<CropEntity>();
            if (crop != null)
            {
                string stage = crop.IsRipe ? "익음 (수확 가능)" : "성장 중";
                return ("작물 (벼)", $"상태: {stage}\n익으면 우클릭 = +5 식량\n3 stage 시각 변화");
            }
            var wall = go.GetComponent<WallEntity>();
            if (wall != null) return ($"{wall.MaterialKr}",
                $"HP: {wall.Hp:F0}/{wall.MaxHp:F0}\n자재: {wall.MaterialKr}\n충돌 collider (pawn 통과 X)\n(wood 100 / stone 280 / steel 300)");
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
            if (animal != null) return (animal.SpeciesKr,
                $"HP {animal.Hp}\n사냥 시 고기 drop\n길들이기 가능 (식량 1 소모)\n{(animal.IsTamed ? "✓ 길들여짐" : "야생")}");
            var trader = go.GetComponent<TraderEntity>();
            if (trader != null) return ("상인", $"우클릭 = wood 5 → food 8 거래\n60s 머무름");
            return ("오브젝트", go.name);
        }
    }
}
