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
                    ? (bp.IsReserved ? "짓는 중" : "자재 준비됨 — 일할 주민을 기다리는 중")
                    : "자재가 부족해 운반 중";
                // `bp.Mode` 를 그대로 찍으면 "청사진 (WallStone)" 처럼 **내부 enum 이름**이
                //  한글 UI 한복판에 나온다 (2026-08-01 UX 리뷰).  한글 이름으로 옮긴다.
                return ($"{ModeKr(bp.Mode)} 예정지",
                    $"{materials}진행도: {bp.Progress * 100f:F0}%\n{status}");
            }
            var pile = go.GetComponent<WoodPileEntity>();
            if (pile != null) return ("통나무 더미",
                $"목재 {pile.Wood}개\n{(pile.IsReserved ? "운반하러 오는 중" : "아직 아무도 안 옴")}\n2분 안에 옮기지 않으면 사라진다");
            var vein = go.GetComponent<StoneVeinEntity>();
            if (vein != null) return ($"광맥 ({vein.TypeKr})",
                $"종류: {vein.TypeKr}\n채광 시 돌 1-3개\n선택 후 우클릭 = 채광 우선\n(화강암 가장 단단, 사암 약함)");
            var chunk = go.GetComponent<StoneChunkEntity>();
            if (chunk != null) return ("돌덩이",
                $"석재 {chunk.Stone}개\n{(chunk.IsReserved ? "운반하러 오는 중" : "아직 아무도 안 옴")}\n3분 안에 옮기지 않으면 사라진다");
            var meat = go.GetComponent<MeatPileEntity>();
            if (meat != null) return (meat.DisplayName,   // #219 - 고기/농작물/베리 구분
                $"식량 {meat.Food}개\n{(meat.IsReserved ? "운반하러 오는 중" : "아직 아무도 안 옴")}\n1분 30초 안에 옮기지 않으면 상한다");
            var sp = go.GetComponent<StockpileZoneEntity>();
            if (sp != null) return ($"창고 영역 [{sp.PriorityKr}]",
                $"우선순위: {sp.PriorityKr}\n운반 담당 주민이 우선순위 높은 곳부터 채운다\n우클릭하면 우선순위가 바뀐다\n(긴급 > 중요 > 우선 > 보통 > 낮음)");
            var tree = go.GetComponent<TreeEntity>();
            if (tree != null) return ($"나무 ({tree.SpeciesKr})",
                $"위치: ({go.transform.position.x:F0}, {go.transform.position.y:F0})\n" +
                $"종류: {tree.SpeciesKr}\n" +
                "선택 후 우클릭 = 벌목 → 목재 떨어짐\n" +
                "(참나무 단단 7목재, 소나무 빠름 4, 자작나무 5)");   // QA F2(2026-06-14) — 한글 UI 속 영문 수종명 제거
            var bush = go.GetComponent<BerryBushEntity>();
            if (bush != null) return ("베리덤불", $"배고픈 주민이 알아서 따 먹는다\n딴 자리는 30초쯤 뒤 다시 열린다");
            var crop = go.GetComponent<CropEntity>();
            if (crop != null)
            {
                string stage = crop.IsRipe ? "익음 (수확 가능)" : "성장 중";
                return ("벼", $"상태: {stage}\n다 익은 뒤 우클릭하면 수확 — 식량 +5\n세 단계에 걸쳐 자란다");
            }
            var wall = go.GetComponent<WallEntity>();
            if (wall != null) return ($"{wall.MaterialKr}",
                $"내구도: {wall.Hp:F0}/{wall.MaxHp:F0}\n자재: {wall.MaterialKr}\n주민도 짐승도 지나갈 수 없다\n(나무 100 · 돌 280 · 강철 300)");
            var door = go.GetComponent<DoorEntity>();
            if (door != null) return ("문", $"{CostKr(BuildManager.Mode.Door)}\n주민은 지나갈 수 있고, 닫혀 있으면 벽처럼 열기를 막는다");
            var floor = go.GetComponent<FloorEntity>();
            if (floor != null) return ("바닥", $"{CostKr(BuildManager.Mode.Floor)}\n깔아 두면 실내로 쳐서 비와 바람을 막아 준다");
            var stove = go.GetComponent<StoveEntity>();
            if (stove != null) return ("화덕", $"{CostKr(BuildManager.Mode.Stove)}\n요리 담당 주민이 식량 5 로 식사 1 을 만든다");
            var bed = go.GetComponent<BedEntity>();
            if (bed != null) return ($"{bed.QualityKr}",
                $"품질: {bed.QualityKr}\n수면 회복: {bed.RestMul:F2}배\n기분: 자는 동안 +{bed.MoodBonus:F0}\n(맨바닥 잠자리 0.8배 · 나무 침대 1.0배 · 고급 침대 1.4배)");
            var bench = go.GetComponent<ResearchBench>();
            if (bench != null) return ("연구대",
                $"연구 담당 주민이 앞에 서 있는 동안 진행된다\n"
                + $"초당 {(ResearchManager.Instance != null ? ResearchManager.Instance.pointsPerSecondPerBench : 0f):0.##} 점");
            var wolf = go.GetComponent<WolfEnemy>();
            if (wolf != null) return ("늑대 [위협]", $"체력 {wolf.Hp}/18 · 물면 4 피해\n5칸 안의 주민을 발견하면 쫓아온다 (주민보다 빠르다)\n징집한 뒤 우클릭하면 공격");
            var bandit = go.GetComponent<BanditEnemy>();
            if (bandit != null) return ("약탈자 [위협]", $"체력 {bandit.Hp}/20 · 닿으면 피해를 준다\n징집한 뒤 우클릭하면 공격");
            var animal = go.GetComponent<AnimalEntity>();
            if (animal != null) return (animal.SpeciesKr,
                $"체력 {animal.Hp}\n사냥하면 고기를 남긴다\n길들이기 가능 (식량 1 소모)\n{(animal.IsTamed ? "길들여짐" : "야생")}");
            var trader = go.GetComponent<TraderEntity>();
            if (trader != null) return ("상인", $"우클릭하면 목재 5 를 식량 8 로 바꿔 준다\n60초 뒤 떠난다");
            var grave = go.GetComponent<GraveEntity>();
            if (grave != null) return (grave.Occupied ? "무덤" : "빈 무덤", grave.Description);
            return ("오브젝트", go.name);
        }

        /// <summary>건축 종류의 한글 이름.  내부 enum 이름이 한글 UI 에 새어 나오는 것을 막는다.</summary>
        private static string ModeKr(BuildManager.Mode m) => m switch
        {
            BuildManager.Mode.Wall            => "나무 벽",
            BuildManager.Mode.WallStone       => "돌 벽",
            BuildManager.Mode.Floor           => "나무 바닥",
            BuildManager.Mode.FloorStone      => "돌 바닥",
            BuildManager.Mode.Door            => "문",
            BuildManager.Mode.Autodoor        => "자동문",
            BuildManager.Mode.Stove           => "화덕",
            BuildManager.Mode.Bed             => "침대",
            BuildManager.Mode.BedFine         => "고급 침대",
            BuildManager.Mode.BedSleepingSpot => "잠자리",
            BuildManager.Mode.Lamp            => "등불",
            BuildManager.Mode.TableChair       => "식탁",
            BuildManager.Mode.Fence           => "울타리",
            BuildManager.Mode.FenceGate       => "울타리 문",
            BuildManager.Mode.Barricade       => "바리케이드",
            BuildManager.Mode.ResearchBench   => "연구대",
            BuildManager.Mode.Grave           => "무덤",
            _                                  => "건축물",
        };

        /// <summary>정본에서 읽은 목재 비용 문구.
        ///
        /// 2026-08-01 UX 리뷰 — 이 패널은 비용을 **손으로 적고** 있었고, 그 숫자가
        ///  실제와 3~8배 어긋나 있었다 (문 3 ↔ 실제 25, 화덕 10 ↔ 실제 80,
        ///  바닥 1 ↔ 실제 3).  플레이어가 이 패널을 읽고 계획하면 반드시 틀린다.
        ///  `BuildManager.LiveCostFor` 는 바로 이 드리프트를 막으려고 이미 만들어져
        ///  있었는데(그 함수 주석에 "드리프트 방지" 라고 적혀 있다) 여기서 안 쓰고
        ///  있었다 — 이 레포에서 반복되는 '고칠 방법이 이미 있는데 안 쓴' 유형.</summary>
        private static string CostKr(BuildManager.Mode m)
        {
            var bm = BuildManager.Instance;
            return bm != null ? $"목재 {bm.LiveCostFor(m)}" : "";
        }
    }
}
