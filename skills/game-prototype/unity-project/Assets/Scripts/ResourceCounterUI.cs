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

        // #130 - 자원 변화 시 텍스트 일시적 노란 flash (운영자가 "값이 안 늘어" 느낌 해소)
        private float woodFlashUntil = -10f;
        private float foodFlashUntil = -10f;
        private float mealsFlashUntil = -10f;
        private float stoneFlashUntil = -10f;
        private const float FlashDuration = 1.2f;
        private Color woodOriginalColor, foodOriginalColor, mealsOriginalColor, stoneOriginalColor;
        private bool colorsCaptured = false;

        private void Update()
        {
            if (ResourceManager.Instance == null) return;
            var rm = ResourceManager.Instance;
            // 첫 frame 에 원래 색 캡쳐
            if (!colorsCaptured)
            {
                if (woodText != null)  woodOriginalColor  = woodText.color;
                if (foodText != null)  foodOriginalColor  = foodText.color;
                if (mealsText != null) mealsOriginalColor = mealsText.color;
                if (stoneText != null) stoneOriginalColor = stoneText.color;
                colorsCaptured = true;
            }
            if (rm.wood != lastWood)
            {
                if (woodText != null) woodText.text = $"목재: {rm.wood}";
                if (lastWood >= 0) woodFlashUntil = Time.unscaledTime + FlashDuration;
                lastWood = rm.wood;
            }
            if (rm.food != lastFood)
            {
                if (foodText != null) foodText.text = $"식량: {rm.food}";
                if (lastFood >= 0) foodFlashUntil = Time.unscaledTime + FlashDuration;
                lastFood = rm.food;
            }
            if (rm.meals != lastMeals)
            {
                if (mealsText != null) mealsText.text = $"식사: {rm.meals}";
                if (lastMeals >= 0) mealsFlashUntil = Time.unscaledTime + FlashDuration;
                lastMeals = rm.meals;
            }
            if (rm.stone != lastStone)
            {
                if (stoneText != null) stoneText.text = $"석재: {rm.stone}";
                if (lastStone >= 0) stoneFlashUntil = Time.unscaledTime + FlashDuration;
                lastStone = rm.stone;
            }
            // flash 색 적용
            Color flashCol = new Color(1f, 0.95f, 0.35f, 1f);  // 밝은 노란
            if (woodText != null)  woodText.color  = Time.unscaledTime < woodFlashUntil  ? flashCol : woodOriginalColor;
            if (foodText != null)  foodText.color  = Time.unscaledTime < foodFlashUntil  ? flashCol : foodOriginalColor;
            if (mealsText != null) mealsText.color = Time.unscaledTime < mealsFlashUntil ? flashCol : mealsOriginalColor;
            if (stoneText != null) stoneText.color = Time.unscaledTime < stoneFlashUntil ? flashCol : stoneOriginalColor;
        }
    }
}
