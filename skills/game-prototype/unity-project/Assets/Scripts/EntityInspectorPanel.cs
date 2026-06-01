using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 피드백 #105 - 오브젝트(비-pawn entity) 설명 텍스트 제공자.
    ///
    /// ── 2026-05-31 single-inspector 통합 (운영자 fb "DUAL INSPECTOR") ──
    /// 이전: 우측 가운데에 자체 패널을 그렸고 (pawn 선택 시 좌측 PawnInfoPanel 과
    ///   동시에 떠서 "오른쪽은 '없음' / 왼쪽은 pawn" 이중 패널 혼란).
    /// 지금: the reference sim 처럼 좌측 하단 PawnInfoPanel 하나만 보이는 selection-info 로
    ///   통합.  이 컴포넌트는 더 이상 화면에 패널을 그리지 않고, 비-pawn entity 의
    ///   설명 텍스트만 제공하는 "logic-only" 컴포넌트로 남는다.
    ///   PawnInfoPanel 이 entity 선택 시 EntityInspectorPanel.DescribeEntity() 를
    ///   호출해서 같은 좌측 패널에 표시한다 → 단일 inspector.
    ///
    /// 컴포넌트 자체는 그대로 둔다 (IntegrationTestRunner I25 가
    ///   FindFirstObjectByType<EntityInspectorPanel> + private Describe() reflection
    ///   으로 entity describe 로직을 검증하므로).  GameManager.EnsureInScene 도 유지.
    /// </summary>
    public class EntityInspectorPanel : MonoBehaviour
    {
        private static EntityInspectorPanel _instance;

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("EntityInspectorPanel");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<EntityInspectorPanel>();
        }

        public static EntityInspectorPanel Instance => _instance;

        private void Awake()
        {
            // logic-only: NO RectTransform / Image / Text built.  This component
            //   draws nothing — the single visible inspector is PawnInfoPanel
            //   (bottom-left).  We only expose entity description text.
            if (_instance == null) _instance = this;
        }

        /// <summary>비-pawn entity 의 (제목, 본문) 설명.  PawnInfoPanel 이 entity
        ///   선택 시 이 메서드로 텍스트를 받아 좌측 단일 패널에 표시한다.
        ///   pawn 이거나 null 이면 (null, null) 반환 (= "이 entity 패널이 표시할 것 없음").</summary>
        public (string, string) DescribeEntity(GameObject go)
        {
            if (go == null) return (null, null);
            if (go.GetComponent<PawnEntity>() != null) return (null, null);  // pawns → PawnInfoPanel own path
            return Describe(go);
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
            if (meat != null) return (meat.DisplayName,   // #219 - 고기/농작물/베리 구분
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
