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
        //  식량 ~N일치(카운터가 카운트다운으로 읽혀야 농사/사냥을 '하고 싶어'짐).
        private int lastFineMeals = -1;
        private int cachedPawnCount = 0;
        private float nextPawnCountPoll = -1f;

        public void SetStoneText(Text t) { stoneText = t; }  // SceneSetup binding

        // #130 - 자원 변화 시 텍스트 일시적 노란 flash (운영자가 "값이 안 늘어" 느낌 해소)
        private float woodFlashUntil = -10f;
        private float _nextGroundPoll;    // F4 — 바닥 더미 합계 폴링
        // G4 정착지 가치 (2026-07-24) — 습격 스케일 근거 수치를 석재 행에 병기.
        //  FindObjects 비용이라 5s 폴링, suffix 변화 시 스톤 행 강제 갱신.
        private float _nextWealthPoll;
        private string _wealthSuffix = "";
        private int lastFoodForDays = -1, lastPawnForDays = -1;   // r2 #6 stale 가드
        private int _groundWood;
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
            // #QA플레이 F4 (2026-06-12) — 시작 자원이 '바닥 더미'(물리)라 적립 카운터가
            //  0 이면 '자원 없음'처럼 읽혔다.  바닥 더미 합계를 2s 폴링해 병기:
            //  "목재: 0 (+300 바닥)" — 건축은 바닥분으로도 펀딩되므로 진실 표시.
            if (Time.unscaledTime >= _nextWealthPoll)
            {
                _nextWealthPoll = Time.unscaledTime + 5f;
                string ws = $"   가치: {AIDirector.WealthSnapshot():N0}";
                if (ws != _wealthSuffix) { _wealthSuffix = ws; lastStone = -1; }
            }
            if (Time.unscaledTime >= _nextGroundPoll)
            {
                _nextGroundPoll = Time.unscaledTime + 2f;
                int g = 0;
                foreach (var wpile in Object.FindObjectsByType<WoodPileEntity>(FindObjectsSortMode.None))
                    if (wpile != null && !wpile.InStockpile) g += wpile.Wood;
                if (g != _groundWood) { _groundWood = g; lastWood = -1; }   // 갱신 강제
            }
            if (rm.wood != lastWood)
            {
                if (woodText != null) woodText.text = _groundWood > 0
                    ? $"목재: {rm.wood:N0} (+{_groundWood:N0} 바닥)"
                    : $"목재: {rm.wood:N0}";  // #audit3 #17 천단위 구분
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
            // r2 #6 (2026-06-12) — '~N일치' 진실 보정: (a) rm.food/림 수 변화에도 재계산
            //  (이전엔 meals 변화 게이트 안이라 stale), (b) 소비율 — 림 1명 게임일당 need
            //  ~130 ÷ 영양위계(raw20/meal40) ~ 식량단위 9/일.  '3일치'로 읽히던 재고가
            //  실제 1일치이던 3배 과대평가 해소.
            if (rm.meals != lastMeals || rm.fineMeals != lastFineMeals
                || rm.food != lastFoodForDays || cachedPawnCount != lastPawnForDays)
            {
                lastFoodForDays = rm.food;
                lastPawnForDays = cachedPawnCount;
                if (mealsText != null)
                {
                    // #1.1 — 고급식사 병기 (숙련 요리 산출이 화면에 안 보이던 것).
                    string fine = rm.fineMeals > 0 ? $" +고급{rm.fineMeals:N0}" : "";
                    const float FoodUnitsPerPawnDay = 9f;
                    string days = "";
                    if (cachedPawnCount > 0)
                    {
                        float foodUnits = rm.food + rm.meals * 3f + rm.fineMeals * 5f;
                        // 소크 발견 (2026-06-12): rm.food 가 소수점 음수로 떨어지면
                        //  '~-0.0일치' 표기 — 잔여일은 0 미만이 없다.
                        float d = Mathf.Max(0f, foodUnits / (cachedPawnCount * FoodUnitsPerPawnDay));
                        days = d < 10f ? $" (~{d:F1}일치)" : "";
                    }
                    mealsText.text = $"식사: {rm.meals:N0}{fine}{days}";
                }
                if (lastMeals >= 0) mealsFlashUntil = Time.unscaledTime + FlashDuration;
                lastMeals = rm.meals;
                lastFineMeals = rm.fineMeals;
            }
            if (rm.stone != lastStone)
            {
                if (stoneText != null) stoneText.text = $"석재: {rm.stone:N0}{_wealthSuffix}";
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
