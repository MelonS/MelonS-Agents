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
        private int _groundFood;   // 바닥에 있는 식량 더미 합계 (식량 줄에 병기)
        private float foodFlashUntil = -10f;
        private float mealsFlashUntil = -10f;
        private float stoneFlashUntil = -10f;
        private const float FlashDuration = 1.2f;

        // 플래시 최소 변화폭 (2026-08-01).
        //  운영자 영상 실측: 좌상단 식량·식사 줄이 **뭉개져 읽을 수 없었다**.  원인은
        //  폰트가 아니라 **노란 플래시가 거의 항상 켜져 있던 것** — 값이 1 만 바뀌어도
        //  1.2초 켜지는데, 주민이 3 → 6 인이 되면서 식량·식사가 끊임없이 오르내려
        //  플래시가 겹쳐 끊기지 않았다.  정상 색인 목재·석재 줄과 나란히 놓고 보면 명확하다.
        //  '값이 늘었다'를 알리는 장치가 '글자를 못 읽게' 만들면 순손실이다.
        //  ±3 이상 — 한 번의 운반·요리 단위 변화에만 반응한다.
        private const int FlashMinDelta = 3;
        private Color woodOriginalColor, foodOriginalColor, mealsOriginalColor, stoneOriginalColor;
        private bool colorsCaptured = false;

        /// <summary>크림색 패널 위에서 읽히도록 **명도 상한**을 건다 (색상·채도는 유지).
        ///
        /// 석재 칩은 "갈색 계열과 분리"하려고 밝은 청회색(0.70,0.76,0.84)으로 잡혀
        ///  있었는데, 그 밝기는 패널 배경과 거의 같아 여섯 프레임 내내 안 읽혔다.
        ///  색으로 구분하는 것과 배경에서 떠오르는 것은 다른 문제다 — 구분은 색상이
        ///  하고, 가독은 명도가 한다.  씬에 굳은 값을 고치는 대신 런타임에서 눌러
        ///  `SceneSetup` 을 다시 돌려도 되살아나지 않게 한다.</summary>
        private static Color Readable(Color c)
        {
            float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            const float MaxLum = 0.58f;
            if (lum <= MaxLum) return c;
            float k = MaxLum / Mathf.Max(lum, 1e-4f);
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }

        private void Update()
        {
            if (ResourceManager.Instance == null) return;
            var rm = ResourceManager.Instance;
            // 첫 frame 에 원래 색 캡쳐
            if (!colorsCaptured)
            {
                if (woodText != null)  woodOriginalColor  = Readable(woodText.color);
                if (foodText != null)  foodOriginalColor  = Readable(foodText.color);
                if (mealsText != null) mealsOriginalColor = Readable(mealsText.color);
                if (stoneText != null) stoneOriginalColor = Readable(stoneText.color);
                if (woodText != null)  woodText.color  = woodOriginalColor;
                if (foodText != null)  foodText.color  = foodOriginalColor;
                if (mealsText != null) mealsText.color = mealsOriginalColor;
                if (stoneText != null) stoneText.color = stoneOriginalColor;
                colorsCaptured = true;
            }
            // #QA플레이 F4 (2026-06-12) — 시작 자원이 '바닥 더미'(물리)라 적립 카운터가
            //  0 이면 '자원 없음'처럼 읽혔다.  바닥 더미 합계를 2s 폴링해 병기:
            //  "목재: 0 (+300 바닥)" — 건축은 바닥분으로도 펀딩되므로 진실 표시.
            if (Time.unscaledTime >= _nextWealthPoll)
            {
                _nextWealthPoll = Time.unscaledTime + 5f;
                // 2026-07-31 — 라벨에 **왜 이 숫자가 있는지**를 붙인다.
                //  기존 "석재: 0   가치: 644" 는 자원 옆에 정체불명의 수치가 붙어 있어
                //  디버그 값처럼 읽혔다(플레이어는 점수로 오해하거나 그냥 무시한다).
                //  실제로는 습격 규모를 정하는 입력값이다 — 그 한 마디가 있으면
                //  "재산이 늘면 더 큰 습격이 온다"는 이 장르의 긴장이 화면에서 읽힌다.
                // 2026-08-01 — 한 줄에 두 값이 붙어 있으면 둘 다 안 읽힌다(확대 실측:
                //  '석재: 0   가치: 844 (습격 규모)').  줄바꿈으로 분리하고 괄호 설명을
                //  짧게 줄인다.  석재는 자원, 가치는 위협 지표라 성격이 다르다.
                // 줄바꿈은 쓰지 않는다 (2026-08-01 실측).  이 패널은 ContentSizeFitter +
                //  VerticalLayoutGroup 으로 **행 단위** 높이를 계산하는데, 한 Text 안에
                //  개행을 넣으면 레이아웃은 여전히 한 행으로 보고 둘째 줄이 패널 밖으로
                //  삐져나온다.  진짜 해법은 별도 행 추가지만 씬 재베이크가 필요하므로,
                //  같은 줄에 두되 **간격을 벌리고 라벨을 짧게** 해 읽히게 한다.
                string ws = $"    가치 {AIDirector.WealthSnapshot():N0}";
                if (ws != _wealthSuffix) { _wealthSuffix = ws; lastStone = -1; }
            }
            if (Time.unscaledTime >= _nextGroundPoll)
            {
                _nextGroundPoll = Time.unscaledTime + 2f;
                int g = 0;
                foreach (var wpile in Object.FindObjectsByType<WoodPileEntity>(FindObjectsSortMode.None))
                    if (wpile != null && !wpile.InStockpile) g += wpile.Wood;
                if (g != _groundWood) { _groundWood = g; lastWood = -1; }   // 갱신 강제
                int gf = 0;
                foreach (var mp in Object.FindObjectsByType<MeatPileEntity>(FindObjectsSortMode.None))
                    if (mp != null && !mp.InStockpile) gf += mp.Food;
                if (gf != _groundFood) { _groundFood = gf; lastFood = -1; }   // 식량 줄 갱신 강제
            }
            if (rm.wood != lastWood)
            {
                if (woodText != null) woodText.text = _groundWood > 0
                    ? $"목재: {rm.wood:N0} (+{_groundWood:N0} 바닥)"
                    : $"목재: {rm.wood:N0}";  // #audit3 #17 천단위 구분
                if (lastWood >= 0 && Mathf.Abs(rm.wood - lastWood) >= FlashMinDelta)
                    woodFlashUntil = Time.unscaledTime + FlashDuration;
                lastWood = rm.wood;
            }
            if (rm.food != lastFood)
            {
                // 2026-07-31 — 식량도 **바닥분을 병기**한다 (목재와 같은 어법).
                //  시작 시 간편식 6더미(60)가 실제로 바닥에 뿌려지는데 HUD 는 `식량: 0`
                //  만 보여줬다.  영상 내내 '식량 0 / 식사 0' 이라 굶는 콜로니로 읽혔는데,
                //  사실은 60이 널려 있고 아직 창고에 안 들어갔을 뿐이다.
                //  목재는 이미 `(+140 바닥)` 을 붙이고 있었다 — 같은 상태를 한쪽만
                //  숨기고 있었던 셈이다.
                if (foodText != null) foodText.text = _groundFood > 0
                    ? $"식량: {rm.food:N0} (+{_groundFood:N0} 바닥)"
                    : $"식량: {rm.food:N0}";
                if (lastFood >= 0 && Mathf.Abs(rm.food - lastFood) >= FlashMinDelta)
                    foodFlashUntil = Time.unscaledTime + FlashDuration;
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
                if (lastMeals >= 0 && Mathf.Abs(rm.meals - lastMeals) >= FlashMinDelta)
                    mealsFlashUntil = Time.unscaledTime + FlashDuration;
                lastMeals = rm.meals;
                lastFineMeals = rm.fineMeals;
            }
            if (rm.stone != lastStone)
            {
                if (stoneText != null) stoneText.text = $"석재: {rm.stone:N0}{_wealthSuffix}";
                if (lastStone >= 0 && Mathf.Abs(rm.stone - lastStone) >= FlashMinDelta)
                    stoneFlashUntil = Time.unscaledTime + FlashDuration;
                lastStone = rm.stone;
            }
            // flash 색 적용
            // #소개영상(2026-08-08) — flash 를 **밝은 노랑**으로 두면 크림색 패널 위에서
            //  그 줄이 1.2초간 통째로 사라진다.  촬영 프레임 여섯 장 중 다섯 장에서
            //  한 줄씩 읽히지 않았고, 그때마다 다른 줄이라 UI 버그로 보이지도 않았다.
            //  "값이 늘었다"를 밝기로 알리려던 장치가 밝은 배경에서 정반대로 작동한 것.
            //  강조는 **어둡고 진한 호박색**으로 준다 — 배경보다 어두우면 항상 읽힌다.
            Color flashCol = new Color(0.80f, 0.42f, 0.04f, 1f);
            if (woodText != null)  woodText.color  = Time.unscaledTime < woodFlashUntil  ? flashCol : woodOriginalColor;
            if (foodText != null)  foodText.color  = Time.unscaledTime < foodFlashUntil  ? flashCol : foodOriginalColor;
            if (mealsText != null) mealsText.color = Time.unscaledTime < mealsFlashUntil ? flashCol : mealsOriginalColor;
            if (stoneText != null) stoneText.color = Time.unscaledTime < stoneFlashUntil ? flashCol : stoneOriginalColor;
        }
    }
}
