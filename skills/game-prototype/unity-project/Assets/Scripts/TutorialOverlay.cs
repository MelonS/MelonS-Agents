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
        };

        [SerializeField] private Image bg;
        [SerializeField] private Text tipText;

        private int currentTipIdx = -1;
        private float currentTipFadeTime;
        private bool currentVisible;
        private float skipUntil = -1f;

        private void Start()
        {
            if (bg != null) { var c = bg.color; c.a = 0f; bg.color = c; }
            if (tipText != null) { var c = tipText.color; c.a = 0f; tipText.color = c; }
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

        private void FadeIn()  { currentVisible = true; }
        private void FadeOut() { currentVisible = false; }

        public void SetRefs(Image bgImg, Text txt)
        {
            bg = bgImg;
            tipText = txt;
        }
    }
}
