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
        public enum Gate { Unpause, Stockpile, House, Farm, Done }

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
            new Tip { gate = Gate.Stockpile,
                      text = "① 저장공간:  건축(F8) → 구역 → 저장 \n자원을 모아둘 구역을 드래그로 지정하세요." },
            new Tip { gate = Gate.House,
                      text = "② 집:  건축 → 구조 → 목재 벽으로 방을 짓고 \n문·가구(침대)를 놓으세요." },
            new Tip { gate = Gate.Farm,
                      text = "③ 농장:  건축 → 구역 → 경작 \n농사 지을 땅을 지정하세요." },
            new Tip { gate = Gate.Done,
                      text = "좋습니다!  이제 콜로니스트가 알아서 일합니다. \n밤엔 침대에서 자고, 며칠 뒤 습격이 옵니다." },
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
                // UI 가림 다이어트 (2026-07-24): 같은 단계가 25초 지나면 0.35 로 딤 —
                //  안내는 남되 맵 하단 중앙을 계속 가리지 않는다.  단계 전환 시 원복.
                bool stale = Time.realtimeSinceStartup - currentTipFadeTime > 25f;
                float target = currentVisible ? (stale ? 0.35f : 0.92f) : 0f;
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
                    // 정지 시작(SetScale 0) 이후 1배속 이상으로 올렸는가.
                    return Time.timeScale > 0.01f && sawPause;
                case Gate.Stockpile:
                    return Object.FindObjectsByType<StockpileZoneEntity>(FindObjectsSortMode.None).Length > 0;
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
                group.alpha = Mathf.MoveTowards(group.alpha, currentVisible ? 0.92f : 0f, Time.unscaledDeltaTime * 2f);
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
