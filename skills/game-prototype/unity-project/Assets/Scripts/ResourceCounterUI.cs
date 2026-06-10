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
        // #ui백로그 1.1 + #게임필4 — fineMeals 표시(미표시로 '값이 안 늘어' 재발) 와
        //  식량 ≈N일치(카운터가 카운트다운으로 읽혀야 농사/사냥을 '하고 싶어'짐).
        private int lastFineMeals = -1;
        private int cachedPawnCount = 0;
        private float nextPawnCountPoll = -1f;

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
                if (woodText != null) woodText.text = $"목재: {rm.wood:N0}";  // #audit3 #17 천단위 구분
                if (lastWood >= 0) woodFlashUntil = Time.unscaledTime + FlashDuration;
                lastWood = rm.wood;
            }
            if (rm.food != lastFood)
            {
                if (foodText != null) foodText.text = $"식량: {rm.food:N0}";
                if (lastFood >= 0) foodFlashUntil = Time.unscaledTime + FlashDuration;
                lastFood = rm.food;
            }
            // 림 수 2s 캐시 (lesson #4 — 매 프레임 FindObjects 금지)
            if (Time.unscaledTime >= nextPawnCountPoll)
            {
                nextPawnCountPoll = Time.unscaledTime + 2f;
                int n = 0;
                foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
                    if (p != null && !p.IsDead) n++;
                cachedPawnCount = n;
            }
            if (rm.meals != lastMeals || rm.fineMeals != lastFineMeals)
            {
                if (mealsText != null)
                {
                    // #1.1 — 고급식사 병기 (숙련 요리 산출이 화면에 안 보이던 것).
                    string fine = rm.fineMeals > 0 ? $" +고급{rm.fineMeals:N0}" : "";
                    // #게임필4 — ≈N일치: 1림 ≈ 하루 식사환산 1개(meal=3, fine=5 food단위).
                    string days = "";
                    if (cachedPawnCount > 0)
                    {
                        float foodUnits = rm.food + rm.meals * 3f + rm.fineMeals * 5f;
                        float d = foodUnits / (cachedPawnCount * 3f);
                        days = d < 10f ? $" (~{d:F0}일치)" : "";
                    }
                    mealsText.text = $"식사: {rm.meals:N0}{fine}{days}";
                }
                if (lastMeals >= 0) mealsFlashUntil = Time.unscaledTime + FlashDuration;
                lastMeals = rm.meals;
                lastFineMeals = rm.fineMeals;
            }
            if (rm.stone != lastStone)
            {
                if (stoneText != null) stoneText.text = $"석재: {rm.stone:N0}";
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
