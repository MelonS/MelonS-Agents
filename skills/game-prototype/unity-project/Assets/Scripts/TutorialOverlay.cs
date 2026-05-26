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

        public Tip[] tips = new Tip[]
        {
            new Tip { startTime = 1f,  duration = 7f,
                      text = "환영합니다, 콜로니스트.\n좌클릭으로 콜로니스트를 선택하세요." },
            new Tip { startTime = 9f,  duration = 7f,
                      text = "WASD 키로 카메라를 움직이고,\n마우스 휠로 줌인/줌아웃 합니다." },
            new Tip { startTime = 17f, duration = 7f,
                      text = "Space 키 = 일시정지.\n1/2/3 키 = 시간 가속 (1x/2x/4x)." },
            new Tip { startTime = 25f, duration = 7f,
                      text = "콜로니스트 선택 후 R 키 = 강제 동원 (draft).\n드래프트 상태에서 적/늑대 우클릭 = 공격." },
            new Tip { startTime = 33f, duration = 7f,
                      text = "B 키 = 벽 건설 모드 (목재 5)\nF = 바닥 1, G = 문 3, T = 화덕 10" },
            new Tip { startTime = 41f, duration = 7f,
                      text = "익은 작물(황금색)을 우클릭하면 수확.\n농장 우측 4x3 타일에서 자라고 있습니다." },
            new Tip { startTime = 49f, duration = 7f,
                      text = "연구대 옆에 콜로니스트를 두면 연구 진행.\nN 키 = 연구 선택." },
            new Tip { startTime = 57f, duration = 7f,
                      text = "위협도는 시간에 따라 증가합니다.\n외곽의 늑대와 강도단을 조심하세요." },
            new Tip { startTime = 65f, duration = 7f,
                      text = "행운을 빕니다.\nSpace/Esc 로 팁 건너뛰기." },
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
