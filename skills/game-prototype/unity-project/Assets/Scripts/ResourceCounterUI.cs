using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>Top-right resource counter UI.  Day 29 adds meals.</summary>
    public class ResourceCounterUI : MonoBehaviour
    {
        [SerializeField] private Text woodText;
        [SerializeField] private Text foodText;
        [SerializeField] private Text mealsText;
        [SerializeField] private Text stoneText;  // #119
        private int lastWood = -1, lastFood = -1, lastMeals = -1, lastStone = -1;

        public void SetStoneText(Text t) { stoneText = t; }  // SceneSetup binding

        private void Update()
        {
            if (ResourceManager.Instance == null) return;
            var rm = ResourceManager.Instance;
            if (rm.wood != lastWood)   { if (woodText  != null) woodText.text  = $"목재: {rm.wood}";   lastWood = rm.wood; }
            if (rm.food != lastFood)   { if (foodText  != null) foodText.text  = $"식량: {rm.food}";   lastFood = rm.food; }
            if (rm.meals != lastMeals) { if (mealsText != null) mealsText.text = $"식사: {rm.meals}";  lastMeals = rm.meals; }
            if (rm.stone != lastStone) { if (stoneText != null) stoneText.text = $"석재: {rm.stone}";  lastStone = rm.stone; }
        }
    }
}
