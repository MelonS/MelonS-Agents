using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 74 — Tutorial overlay.  Shows a sequence of short tips during
    /// the first ~90 real seconds of play.  Each tip fades in 0.5s, holds,
    /// fades out 0.5s.  Player can skip current tip with Space or Esc.
    /// Tips are Korean strings hardcoded here.
    /// </summary>
    public class TutorialOverlay : MonoBehaviour
    {
        // 행동 기반 온보딩(2026-06-13) — 시간 대신 게임 상태로 단계 전진.
        //  일시정지 시작과 정합(시간 기반은 둘러보는 동안 만료됨) + 첫 사이클
        //  전체(저장→집→농장) 안내.  각 단계는 플레이어가 실제로 해야 넘어간다.
        public enum Gate { Unpause, Stockpile, Chop, House, Farm, Done }

        [System.Serializable]
        public struct Tip
        {
            public float startTime;   // (레거시 — 미사용, 직렬화 호환 유지)
            public float duration;    // (레거시)
            public string text;
            public Gate gate;
        }

        // 운영자 피드백 2026-05-27: tip 9개 × 7초 = 72초 동안 화면 가림.  3개로 압축.
        // 키보드 안내는 GuiControlBar 의 버튼 hint 가 cover.
        public Tip[] tips = new Tip[]
        {
            new Tip { gate = Gate.Unpause,
                      text = "일시정지 상태입니다.  맵을 둘러본 뒤 \nSpace(또는 ▶)로 시작하세요." },
            // ① 을 '저장구역 만들기' → '벌목 지정'으로 교체 (2026-07-27).
            //  시작 저장구역이 생기면서 Gate.Stockpile 이 게임 시작 즉시 충족돼, 이 단계가
            //  화면에 뜨자마자 건너뛰어졌다(평가자: "일시정지 상태인데 이미 ②번이 떠 있음").
            //  빈 단계를 남기는 대신, **생산이 멈추는 진짜 원인**을 가르치는 단계로 바꾼다:
            //  초기 목재 300 이 창고로 들어간 뒤에는 새 일감이 없어 콜로니가 정지하는데,
            //  그걸 푸는 첫 행동이 벌목 지정이다.
            new Tip { gate = Gate.Chop,
                      text = "① 일감 지정:  나무를 우클릭 → 벌목 \n지정한 만큼만 일합니다 — 여러 그루를 찍어보세요." },
            new Tip { gate = Gate.House,
                      text = "② 집:  건축 → 구조 → 목재 벽으로 방을 짓고 \n문·가구(침대)를 놓으세요." },
            new Tip { gate = Gate.Farm,
                      text = "③ 농장:  건축 → 구역 → 경작 \n농사 지을 땅을 지정하세요." },
            new Tip { gate = Gate.Done,
                      // 문구 정직화 (2026-07-27).  기존: "이제 콜로니스트가 알아서 일합니다."
                      //  → 화면에서는 아무도 일하지 않는 상태에서 이 문장이 떴다.  플레이어가
                      //  일감(벌목/채광/건설)을 지정하지 않으면 폰이 대기하는 건 장르 정상 동작이지만,
                      //  게임이 "알아서 한다"고 약속해 버리면 **게임이 자기 상태를 오인하는 것**으로
                      //  읽힌다 (가상 유저 평가에서 반복 지적: "게임이 자기 자신에 대해 거짓말").
                      //  약속 대신 다음 행동을 지시한다.
                      text = "이제 일감을 지정해 보세요.  나무를 우클릭 = 벌목.\n밤엔 침대에서 자고, 며칠 뒤 습격이 옵니다." },
        };

        [SerializeField] private Image bg;
        [SerializeField] private Text tipText;
        // #UI-restyle U8 — the banner is now a bordered panel (border Image + fill child +
        //   text).  We fade the whole subtree via a CanvasGroup instead of tweening one Image
        //   alpha, so the warm border/fill/text fade together as one panel.
        [SerializeField] private CanvasGroup group;

        private int currentTipIdx = -1;
        private float currentTipFadeTime;
        private bool currentVisible;
        private float skipUntil = -1f;

        private void Start()
        {
            if (group != null) group.alpha = 0f;
            // legacy single-image fade fallback (only if no CanvasGroup wired)
            if (group == null && bg != null) { var c = bg.color; c.a = 0f; bg.color = c; }
            if (group == null && tipText != null) { var c = tipText.color; c.a = 0f; tipText.color = c; }

            // #38 게이트 플레이키 진범(2026-06-10) — 튜토리얼 배너가 첫 ~18초 동안 상단
            //  ~230px 밴드의 월드 클릭을 통째로 먹었다.  CanvasGroup 알파 페이드는 raycast 를
            //  끄지 않아 'alpha 0 인데도 차단'(보이지 않는 차단자).  안내 배너는 어떤 입력도
            //  소비할 이유가 없으므로 서브트리 전체를 비차단으로 강제 (씬 베이크와 무관하게
            //  런타임 1회 sweep — 향후 재베이크에도 안전).
            if (group != null) { group.blocksRaycasts = false; group.interactable = false; }
            foreach (var g in GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = false;
        }

        private void Update()
        {
            if (Time.timeScale <= 0.01f) sawPause = true;   // 정지 시작 감지(Unpause gate 용)
            // Skip current tip
            // 행동 기반 진행: 현재 단계의 gate 가 충족되면 다음 단계로.  완료(stepIdx
            //  == tips.Length)면 영구 종료.  Space/ESC = 현재 단계 건너뛰기(수동 전진).
            if (stepIdx >= tips.Length) { FadeOut(); ApplyFade(); return; }
            bool manualSkip = (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)) && currentVisible;
            bool gateMet = GateSatisfied(tips[stepIdx].gate);
            // Done 단계는 표시 후 6초 뒤 자동 종료.
            if (tips[stepIdx].gate == Gate.Done && currentVisible
                && Time.realtimeSinceStartup - currentTipFadeTime > 6f) gateMet = true;
            if (manualSkip || gateMet)
            {
                stepIdx++;
                if (stepIdx >= tips.Length) { FadeOut(); ApplyFade(); return; }
            }
            if (stepIdx != currentTipIdx)
            {
                currentTipIdx = stepIdx;
                if (tipText != null) tipText.text = tips[currentTipIdx].text;
                currentTipFadeTime = Time.realtimeSinceStartup;
                FadeIn();
            }
            // Smooth fade
            if (group != null)
            {
                // whole bordered-panel fade (border + fill + text) — warm panel reads as one.
                // UI 가림 다이어트 (2026-07-24) → **판독성 우선으로 재조정 (2026-07-27)**.
                //  기존: 25초 후 알파 0.35.  의도는 "맵을 계속 가리지 않기"였으나, 초보자는
                //  한 단계에 25초를 쉽게 넘기므로 결과적으로 **안내가 대부분의 시간을 읽을 수
                //  없는 상태로** 떠 있었다.  가상 유저 평가에서 반복 지적:
                //   - 11세 페르소나: "글씨가 반투명이라 뒤에 나무가 비쳐요. 읽다가 포기했어요"
                //   - QA: 10장 중 4장이 판독 불가 상태로 포착 (게임내 약 40분 구간)
                //   - UX 실측: 불투명 시 대비 6.2:1 → 페이드 시 2.0:1
                //  튜토리얼은 이 게임의 **유일한 안내 수단**이라 가림보다 판독성이 우선이다.
                //  딤 자체는 유지하되(장시간 방치 시 시야 확보) 읽을 수 있는 선까지만.
                bool stale = Time.realtimeSinceStartup - currentTipFadeTime > 40f;
                float target = currentVisible ? (stale ? 0.80f : 0.96f) : 0f;
                group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 2f);
            }
            else
            {
                // legacy fallback (single Image + Text alpha) for any unmigrated wiring.
                if (bg != null)
                {
                    float target = currentVisible ? 0.78f : 0f;
                    var c = bg.color;
                    c.a = Mathf.MoveTowards(c.a, target, Time.unscaledDeltaTime * 2f);
                    bg.color = c;
                }
                if (tipText != null)
                {
                    float target = currentVisible ? 1f : 0f;
                    var c = tipText.color;
                    c.a = Mathf.MoveTowards(c.a, target, Time.unscaledDeltaTime * 2f);
                    tipText.color = c;
                }
            }
        }

        private void FadeIn()  { currentVisible = true; }
        private void FadeOut() { currentVisible = false; }

        private int stepIdx = 0;   // 행동 기반 현재 단계

        private bool GateSatisfied(Gate g)
        {
            switch (g)
            {
                case Gate.Unpause:
                    // 운영자 2026-07-24 "일시정지 아닌데 안내": sawPause 요구 제거 —
                    //  정지를 안 거친 세션(하네스 등)에서 sawPause 영원히 false 라
                    //  1배속 구동 중에도 '일시정지' 팁이 고정되던 버그.  시간이 흐르면
                    //  이 팁은 무조건 용무 종료.
                    return Time.timeScale > 0.01f;
                case Gate.Stockpile:
                    return Object.FindObjectsByType<StockpileZoneEntity>(FindObjectsSortMode.None).Length > 0;
                case Gate.Chop:
                    // 벌목 지정이 하나라도 찍히면 통과.  ReproHarness 의 chopDesignations
                    //  프로브와 **같은 소스**를 쓴다 — 게이트와 테스트가 다른 걸 세면
                    //  "테스트는 통과하는데 화면은 안 넘어간다"가 생긴다.
                    return TreeChopDesignation.Instance != null
                        && TreeChopDesignation.Instance.GetMarkedTreePositions().Count > 0;
                case Gate.House:
                    // 벽 청사진/완공 또는 가구 청사진 중 하나라도 = 집짓기 시작.
                    return Object.FindObjectsByType<WallEntity>(FindObjectsSortMode.None).Length > 0
                        || Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None).Length > 0;
                case Gate.Farm:
                    return GrowZoneDesignation.Instance != null
                        && GrowZoneDesignation.Instance.ZoneCellCount > 0;
                case Gate.Done:
                    return false;   // 시간 경과로만(위에서 처리)
            }
            return false;
        }

        private bool sawPause;

        private void ApplyFade()
        {
            if (group != null)
                group.alpha = Mathf.MoveTowards(group.alpha, currentVisible ? 0.96f : 0f, Time.unscaledDeltaTime * 2f);
        }

        public void SetRefs(Image bgImg, Text txt)
        {
            bg = bgImg;
            tipText = txt;
        }

        // #UI-restyle U8 overload — bordered-panel version (border Image + fill + CanvasGroup).
        public void SetRefs(Image bgImg, Text txt, CanvasGroup cg)
        {
            bg = bgImg;
            tipText = txt;
            group = cg;
        }
    }
}
