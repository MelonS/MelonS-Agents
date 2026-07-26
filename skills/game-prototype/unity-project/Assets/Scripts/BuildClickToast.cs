using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// #190 - 운영자 "청사진 설치 안 됨" 진단 도구.
    ///   QA 는 6/6 PASS, 운영자는 manual click FAIL → discrepancy 원인 모름 (Player.log 없음).
    ///   해결: BuildManager 가 click 처리할 때마다 화면 우상단에 결과 토스트 표시.
    ///   운영자가 클릭하고 "왜 안 됨?" 할 때 바로 원인 보임.
    ///
    /// 표시 메시지:
    ///   "○ 청사진 배치 - 벽 @ (5,3)"
    ///   "× 셀 점유됨 @ (5,3)"
    ///   "× 쿨다운 중 (0.07s)"
    ///   "× UI 위 클릭"
    ///   "× 카메라 null"
    /// </summary>
    public class BuildClickToast : MonoBehaviour
    {
        private static BuildClickToast _instance;
        public static BuildClickToast Instance => _instance;

        private RectTransform rt;
        private Image bg;
        private Text label;
        private float hideAt = -1f;

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("BuildClickToast");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<BuildClickToast>();
        }

        private void Awake()
        {
            rt = gameObject.AddComponent<RectTransform>();
            // #ui백로그 4.1 — 빌드 클릭 피드백은 알림 구역(우상단)이 아니라 클릭 지점과
            //  시선이 가까운 하단 중앙 탭바(GuiControlBar 밴드 상단 96) 위로.  우상단
            //  -416 은 화면 구석이라 피드백 인지 불가 + 알림 밴드 한 칸을 점유했다.
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(360, 38);
            rt.anchoredPosition = new Vector2(0, 110);
            bg = gameObject.AddComponent<Image>();
            bg.color = MelonS.GameProto.Core.UITheme.PanelBg;
            bg.raycastTarget = false;

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(transform, false);
            label = lblGo.AddComponent<Text>();
            label.text = "";
            label.font = MelonS.GameProto.Core.UITheme.LoadKoreanFont(15);   // #7.7 사본 제거
            label.fontSize = 15;
            label.color = MelonS.GameProto.Core.UITheme.TextPrimary;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.sizeDelta = new Vector2(-12, -4);
            lrt.anchoredPosition = Vector2.zero;

            gameObject.SetActive(false);
        }

        public void ShowSuccess(string msg) { Show(msg, MelonS.GameProto.Core.UITheme.AccentGold, 2.0f); }
        public void ShowFail(string msg)    { Show(msg, new Color(0.95f, 0.45f, 0.40f, 1f), 2.5f); }

        private void Show(string msg, Color textCol, float duration)
        {
            if (label == null) return;
            label.text = msg;
            label.color = textCol;
            gameObject.SetActive(true);
            hideAt = Time.unscaledTime + duration;
        }

        private void Update()
        {
            if (hideAt > 0 && Time.unscaledTime >= hideAt)
            {
                gameObject.SetActive(false);
                hideAt = -1f;
            }
        }
    }
}
