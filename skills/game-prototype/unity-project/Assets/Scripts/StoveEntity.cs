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

        public bool CanCookOne()
        {
            return ResourceManager.Instance != null && ResourceManager.Instance.food >= 3;
        }

        public bool CookOne()
        {
            if (!CanCookOne()) return false;
            ResourceManager.Instance.AddFood(-3);
            ResourceManager.Instance.AddMeals(+1);   // Day 27: global meal counter
            MealsAvailable++;
            return true;
        }
    }
}
