using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Day 21: cooking station.  Placeholder for Day 22+ where
    /// pawn brings food (berry) here + cooks into Meal (+50% mood when
    /// eaten).  Day 21 = just the entity + sprite + collider.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class StoveEntity : MonoBehaviour
    {
        public int MealsAvailable { get; private set; } = 0;

        // 부뚜막 불빛 (2026-08-01 "조명관련 오브젝트들 개선 및 추가해줘").
        //  아궁이에 불이 그려져 있는데 밤에 주변이 캄캄했다 — 광원 목록에 없었다.
        //  등잔보다 좁게, 그러나 상시(요리는 밤낮 없다) 켜 둔다.
        private void Awake()
        {
            var ls = GetComponent<LightSource>() ?? gameObject.AddComponent<LightSource>();
            ls.Configure(4.0f, 0.20f);
        }

        public bool CanCookOne()
        {
            return ResourceManager.Instance != null && ResourceManager.Instance.food >= 3;
        }

        public bool CookOne() => CookOne(fineMeal: false);

        /// <summary>#131 - fineMeal=true 면 (Build skill 5+ pawn) fineMeals 카운터 증가.</summary>
        public bool CookOne(bool fineMeal)
        {
            if (!CanCookOne()) return false;
            // #자원모델 단일화(2026-06-04): 조리 재료(raw 식량 3)를 카운터 + 물리 InStockpile
            //  MeatPile 에서 함께 소비(이전엔 카운터만 차감 → 물리 더미 잔존 #2).
            ResourceManager.Instance.SpendStockpiledFood(3);
            if (fineMeal) ResourceManager.Instance.AddFineMeals(+1);
            else ResourceManager.Instance.AddMeals(+1);
            MealsAvailable++;
            // r7 채점 관찰 #4 — 식사 원장: meals 보충 출처를 추적 가능하게.
            Debug.Log($"[Cook] +1 {(fineMeal ? "fine" : "meal")} (재료 food -3) @ ({transform.position.x:F1},{transform.position.y:F1})");
            AudioBank.Instance?.PlayCook();  // wiki B9: cook tick/complete SFX
            return true;
        }
    }
}
