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
        [System.Serializable]
        public struct Tip
        {
            public float startTime;   // real seconds
            public float duration;    // hold seconds
            public string text;
        }

        // 운영자 피드백 2026-05-27: tip 9개 × 7초 = 72초 동안 화면 가림.  3개로 압축.
        // 키보드 안내는 GuiControlBar 의 버튼 hint 가 cover.
        public Tip[] tips = new Tip[]
        {
            new Tip { startTime = 1f,  duration = 5f,
                      text = "콜로니스트를 좌클릭하면 선택,\n빈 곳을 우클릭하면 이동합니다." },
            new Tip { startTime = 7f,  duration = 5f,
                      text = "나무·작물 우클릭 = 작업.\n적/늑대는 [징집] 후 우클릭 = 공격." },
            new Tip { startTime = 13f, duration = 5f,
                      text = "화면 하단 버튼으로 시간/빌드/연구 제어.\nSpace 로 팁 건너뛰기." },
            // 첫사이클 T1 (2026-06-12) — 저장구역 온보딩 0: 안 만들면 운반 3종이
            //  무음으로 죽고 바닥 더미가 썩는데 게임이 어디서도 말해주지 않았다.
            new Tip { startTime = 19f, duration = 6f,
                      text = "건축(F8) → 구역 → 저장:\n자원을 모아둘 저장 구역부터 지정하세요!" },
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
            // Skip current tip
            if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape)) && currentVisible)
            {
                skipUntil = Time.realtimeSinceStartup;
                if (currentTipIdx >= 0 && currentTipIdx < tips.Length)
                {
                    // jump to end of current tip
                    skipUntil = tips[currentTipIdx].startTime + tips[currentTipIdx].duration;
                    var t = tips[currentTipIdx];
                    t.startTime = Time.realtimeSinceStartup - tips[currentTipIdx].duration;
                    tips[currentTipIdx] = t;
                }
                FadeOut();
                return;
            }

            float t_now = Time.realtimeSinceStartup;
            int newIdx = -1;
            for (int i = 0; i < tips.Length; i++)
            {
                if (t_now >= tips[i].startTime && t_now <= tips[i].startTime + tips[i].duration)
                {
                    newIdx = i; break;
                }
            }
            if (newIdx != currentTipIdx)
            {
                currentTipIdx = newIdx;
                if (currentTipIdx >= 0)
                {
                    if (tipText != null) tipText.text = tips[currentTipIdx].text;
                    currentTipFadeTime = t_now;
                    FadeIn();
                }
                else FadeOut();
            }
            // Smooth fade
            if (group != null)
            {
                // whole bordered-panel fade (border + fill + text) — warm panel reads as one.
                float target = currentVisible ? 0.92f : 0f;
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
