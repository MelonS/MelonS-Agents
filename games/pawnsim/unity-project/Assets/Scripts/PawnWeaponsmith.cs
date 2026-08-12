using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 작업대에서 **무기를 만든다**.
    ///
    /// 계기 (2026-07-30 운영자): "습격을 처리하려면 무기가 있어야함. 기본적인 상식으로는
    /// 물론 맨주먹 싸울수도 있지만…", "무기를 만들려면 해야할게 너무 많은데 넣긴해야함."
    ///
    /// 실측한 이전 상태:
    ///   · `PawnEquipment.Catalog` 에 무기 7종이 정의돼 있는데
    ///   · 게임플레이에서 `Equip()` 을 호출하는 곳은 **딱 한 군데** — 연구 `simple_bow`
    ///     완료 시 전원에게 활을 지급하는 훅뿐이었다.
    ///   · 즉 제작·거래·시작 무기가 전부 없고, 무기는 "연구가 끝나면 하늘에서 떨어지는 것"
    ///     이었다.  8게임일 검증에서 습격 격퇴는 됐지만(밴딧 1킬 확인) 그건 자동 지급된
    ///     활 덕이고 **플레이어가 무기를 만든 적은 없다.**
    ///
    /// 설계 원칙 — 최소 침습:
    ///   · **새 작업 종류를 만들지 않는다.**  `WorkKind.Build`(만들기)로 묶는다.
    ///     9직종 그리드는 이 게임의 차별점 화면이라 열을 늘리면 그 화면이 바뀐다.
    ///   · **새 작업대를 만들지 않는다.**  시작 캠프의 `ResearchBench` 를 쓴다 —
    ///     스프라이트에 이미 공구가 그려져 있고, 연구와 제작이 한 작업대를 공유하는 것은
    ///     프로토타입 규모에서 자연스럽다.
    ///   · 구조는 `PawnCook` 을 그대로 본뜬다(예약·스탠드셀·give-up·작업속도 배율) —
    ///     이미 검증된 워커 패턴에서 벗어나면 새 종류의 stuck 을 만든다.
    ///
    /// 루프:  무기 없는 콜로니스트가 있고 자재가 있으면 → 작업대로 이동 → 제작 →
    ///        완성 즉시 그 콜로니스트에게 장착.
    /// </summary>
    [RequireComponent(typeof(PawnMovement))]
    public class PawnWeaponsmith : MonoBehaviour
    {
        // 레시피 — 자재가 싼 것부터.  '맨주먹보다 낫다'가 목표이지 밸런스 게임이 아니다.
        public struct Recipe
        {
            public string weaponKr;
            public int wood;
            public int stone;
        }
        public static readonly Recipe[] Recipes = {
            new Recipe { weaponKr = "곤봉",   wood = 15, stone = 0 },   // 근접 +1
            new Recipe { weaponKr = "도끼",   wood = 20, stone = 5 },   // 근접 +2
            new Recipe { weaponKr = "롱소드", wood = 10, stone = 25 },  // 근접 +3
        };

        [SerializeField] private float craftRange = 1.5f;
        [SerializeField] private float craftSeconds = 6f;      // 요리(1.5s)보다 훨씬 무겁다

        private ResearchBench targetBench;
        private PawnMovement movement;
        private float workStartTime = -1f;
        private WorkGiveUp giveUp;
        private const float GiveUpAfterSec = 10f;
        private Vector2Int standCell = PawnMovement.INVALID_CELL;

        public bool HasTask => targetBench != null;

        private void Awake() { movement = GetComponent<PawnMovement>(); }

        /// <summary>지금 만들 수 있는 레시피(자재 충족) — 없으면 false.</summary>
        public static bool TryPickRecipe(out Recipe pick)
        {
            pick = default;
            var rm = ResourceManager.Instance;
            if (rm == null) return false;
            // 좋은 것부터 시도해 자재가 남으면 더 나은 무기를 만든다.
            for (int i = Recipes.Length - 1; i >= 0; i--)
            {
                if (rm.wood >= Recipes[i].wood && rm.stone >= Recipes[i].stone)
                { pick = Recipes[i]; return true; }
            }
            return false;
        }

        /// <summary>무기가 없거나 맨손인 콜로니스트 — 없으면 null.</summary>
        public static PawnEntity FindUnarmed()
        {
            foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
            {
                if (p == null || p.IsDead) continue;
                var eq = p.GetComponent<PawnEquipment>();
                if (eq == null) continue;
                var w = eq.GetEquipped(PawnEquipment.Slot.Weapon);
                // 맨손("주먹") 도 무장 안 된 것으로 본다 — 이름 대신 효과로 판정한다.
                if (w == null || (!w.rangedEnabled && w.meleeDamageAdd <= 0f)) return p;
            }
            return null;
        }

        public void SetBenchTarget(ResearchBench b)
        {
            if (targetBench != null && targetBench != b)
                ReservationManager.Release(targetBench, gameObject);
            ReleaseStandCell();
            targetBench = b;
            workStartTime = -1f;
            if (b != null)
            {
                ReservationManager.TryReserve(b, gameObject);
                giveUp.Reset(Time.time, Vector2.Distance(transform.position, b.transform.position));
                WalkToWork();
            }
        }

        private void WalkToWork()
        {
            if (targetBench == null) return;
            if (PawnMovement.TryReserveWorkStandPos(targetBench.transform.position,
                    new Vector2Int(1, 1), transform.position, gameObject, ref standCell, out Vector2 stand))
                movement.SetTarget(stand);
            else
                ClearTask();
        }

        private void ReleaseStandCell()
        {
            if (standCell.x != PawnMovement.INVALID_CELL.x)
            {
                ReservationManager.ReleaseCell(standCell, gameObject);
                standCell = PawnMovement.INVALID_CELL;
            }
        }

        public void ClearTask()
        {
            if (targetBench != null) ReservationManager.Release(targetBench, gameObject);
            ReleaseStandCell();
            targetBench = null;
            workStartTime = -1f;
            movement.ClearTarget();
        }

        private void Update()
        {
            if (targetBench == null) return;

            // 도중에 전제가 무너지면 즉시 정리 — 작업대 앞에 얼어붙지 않게 (PawnCook #275 교훈).
            if (FindUnarmed() == null || !TryPickRecipe(out var recipe)) { ClearTask(); return; }

            float dist = Vector2.Distance(transform.position, targetBench.transform.position);
            if (dist > craftRange
                && giveUp.ShouldGiveUp(Time.time, dist, movement.LastPathFailed, GiveUpAfterSec))
            { ClearTask(); return; }

            if (dist > craftRange && !movement.AtStandCell(standCell)) return;

            movement.ClearTarget();
            if (workStartTime < 0f) workStartTime = Time.time;

            // 특성·부상 작업속도 (PawnCook 과 동일 규약)
            var traits = GetComponent<PawnTraits>();
            float mul = traits != null ? traits.workSpeedMul : 1f;
            var hlt = GetComponent<PawnHealth>();
            if (hlt != null) mul *= hlt.WorkSpeedMultiplier();
            float need = craftSeconds / Mathf.Max(0.1f, mul);

            if (Time.time - workStartTime < need) return;

            // ── 완성 ────────────────────────────────────────────────────────
            var target = FindUnarmed();
            var item = System.Array.Find(PawnEquipment.Catalog,
                c => c.slot == PawnEquipment.Slot.Weapon && c.nameKr == recipe.weaponKr);
            if (target == null || item == null) { ClearTask(); return; }

            var rm = ResourceManager.Instance;
            // 자재는 **완성 순간에만** 차감한다.  중간에 차감하면 give-up/중단 시
            //  자재가 사라지는 조용한 손실이 된다 (건축이 청사진 단계에서 안 깎는 것과 같은 이유).
            if (rm != null)
            {
                if (rm.wood < recipe.wood || rm.stone < recipe.stone) { ClearTask(); return; }
                if (recipe.wood > 0) rm.SpendStockpiledWood(recipe.wood);
                if (recipe.stone > 0) rm.SpendStockpiledStone(recipe.stone);
            }

            var eq = target.GetComponent<PawnEquipment>();
            if (eq != null) eq.Equip(item);
            Debug.Log($"[Craft] {recipe.weaponKr} 제작 완료 → {target.PawnName} 장착 "
                      + $"(목재 {recipe.wood} 석재 {recipe.stone})");
            FloatingText.Spawn(target.transform.position + Vector3.up * 0.7f,
                               $"{recipe.weaponKr}!", new Color(0.95f, 0.85f, 0.5f, 1f));
            AlertStackUI.Notify($"{recipe.weaponKr} 제작 — {target.PawnName} 무장", 1);
            ClearTask();
        }
    }
}
