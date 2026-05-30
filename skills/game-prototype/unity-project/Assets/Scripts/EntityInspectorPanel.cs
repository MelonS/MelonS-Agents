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

        private const float HeaderH = 38f;  // header strip height

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

            // #UI-restyle U5 — ONE bordered panel (warm brown + 2px border) matching
            //   control bar / tooltip.  MakeBorderedPanel returns the inner content RT.
            var content = MelonS.GameProto.Core.UITheme.MakeBorderedPanel(rt, 2f);
            bg = GetComponent<Image>();  // border image (legacy ref; raycast already off)

            float pad = MelonS.GameProto.Core.UITheme.PadOuter;

            // ── Header strip (HeaderBg + gold bold title) ────────────────────
            var headerGo = new GameObject("Header");
            headerGo.transform.SetParent(content, false);
            var hImg = headerGo.AddComponent<Image>();
            hImg.color = MelonS.GameProto.Core.UITheme.HeaderBg;
            hImg.raycastTarget = false;
            var hrt = headerGo.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0, 1);
            hrt.anchorMax = new Vector2(1, 1);
            hrt.pivot = new Vector2(0.5f, 1);
            hrt.sizeDelta = new Vector2(0, HeaderH);
            hrt.anchoredPosition = Vector2.zero;
            // thin Divider under the header to separate it from body
            var hRule = new GameObject("HeaderRule");
            hRule.transform.SetParent(content, false);
            var hrImg = hRule.AddComponent<Image>();
            hrImg.color = MelonS.GameProto.Core.UITheme.Divider;
            hrImg.raycastTarget = false;
            var hrRt = hRule.GetComponent<RectTransform>();
            hrRt.anchorMin = new Vector2(0, 1);
            hrRt.anchorMax = new Vector2(1, 1);
            hrRt.pivot = new Vector2(0.5f, 1);
            hrRt.sizeDelta = new Vector2(0, 1f);
            hrRt.anchoredPosition = new Vector2(0, -HeaderH);

            // 제목 - 노란 강조 (림 inspect 느낌) — inside header
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(headerGo.transform, false);
            titleText = titleGo.AddComponent<Text>();
            titleText.text = "선택된 오브젝트 없음";
            titleText.font = MelonS.GameProto.Core.UITheme.LoadKoreanFont(20);
            titleText.fontSize = 20;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = MelonS.GameProto.Core.UITheme.AccentGold;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.raycastTarget = false;
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(pad, 0);
            trt.offsetMax = new Vector2(-pad, 0);

            // 본문 (info text) — below header, padding rhythm
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(content, false);
            bodyText = bodyGo.AddComponent<Text>();
            bodyText.text = "💡 나무/벽/사슴/광맥 등을\n좌클릭하면 정보가 표시됩니다.";
            bodyText.font = MelonS.GameProto.Core.UITheme.LoadKoreanFont(15);
            bodyText.fontSize = 15;
            bodyText.color = MelonS.GameProto.Core.UITheme.TextPrimary;
            bodyText.alignment = TextAnchor.UpperLeft;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.raycastTarget = false;
            var brt = bodyGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(1, 1);
            brt.offsetMin = new Vector2(pad, pad);
            brt.offsetMax = new Vector2(-pad, -(HeaderH + MelonS.GameProto.Core.UITheme.RowGap));

            // 패널 항상 활성 (hint 가 보임).  SetActive(false) 제거.
        }

        private void Update()
        {
            if (cachedCs == null) cachedCs = Object.FindFirstObjectByType<ClickSelector>();
            if (cachedCs == null) return;
            GameObject inspect = cachedCs.CurrentInspect;
            // ui-audit §3.4 (P5) — pawns belong to the bottom-LEFT PawnInfoPanel
            //   ONLY.  The right entity panel no longer mirrors pawn data (that
            //   was the #128 double-panel clutter).  A pawn selection is treated
            //   here as "nothing for this panel" → show the hint, so only ONE
            //   inspector lights up per selection.
            bool isPawn = inspect != null && inspect.GetComponent<PawnEntity>() != null;
            // #128 - 패널 항상 켜둠. inspect 없으면 (or pawn) hint 표시.
            if (inspect == null || isPawn)
            {
                string hintTitle = "선택된 오브젝트 없음";
                string hintBody = "💡 나무/벽/사슴/광맥 등을\n좌클릭하면 정보가 표시됩니다.\n(콜로니스트 정보는 좌측 하단 패널)\n\n📋 직업: F1\n🏛 건축: F8\n🔬 연구: N\n⏸ 멈춤: Space";
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
            // ui-audit §3.4 (P5) — pawns are handled BEFORE this method (Update
            //   short-circuits to the hint for a pawn selection).  The right
            //   entity panel describes NON-pawn entities only; the duplicate
            //   #128 pawn branch was removed to kill the double-inspector.
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
            if (sp != null) return ($"창고 영역 [{sp.PriorityKr}]",
                $"우선순위: {sp.PriorityKr}\nhauler 는 높은 우선순위 zone 우선 운반\n우클릭 → 우선순위 순환\n(긴급 > 중요 > 우선 > 보통 > 낮음)");
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
            if (bed != null) return ($"{bed.QualityKr}",
                $"품질: {bed.QualityKr}\n수면 회복: {bed.RestMul:F2}x\n기분: +{bed.MoodBonus:F0}/s\n(sleeping spot 0.8x / wood 1.0x / fine 1.4x)");
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
