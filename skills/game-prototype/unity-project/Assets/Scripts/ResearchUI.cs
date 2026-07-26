using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 54 — minimal research UI strip in bottom-right.
    /// Shows active tech name + (current/required) points and a thin progress bar.
    /// N key opens a popup list of available techs to start (no need for fancy tree visuals — text picker).
    /// </summary>
    public class ResearchUI : MonoBehaviour
    {
        [SerializeField] private Text statusText;
        [SerializeField] private Image progressBar;
        [SerializeField] private RectTransform pickerPanel;
        [SerializeField] private Text pickerText;

        private float lastUpdate = -10f;
        private bool pickerOpen = false;

        // wiki Dim2 acceptance #5: clicking any UI button plays the blip.
        //  ResearchUI has no Button objects of its own (picker is keyboard/number-
        //  key driven and opened from the GuiControlBar 연구 button), so the
        //  user-action surfaces here are the picker toggle + a tech selection.
        //  Route both through the EXISTING AudioBank.PlaySelect() (same soft-blip
        //  pawn-select uses). Find-once + cache (ClickSelector cached-reference
        //  pattern); null no-op if the bank is absent.
        private AudioBank cachedAudio;
        private bool audioResolved;

        private void PlayClickBlip()
        {
            if (!audioResolved)
            {
                cachedAudio = AudioBank.Instance != null
                    ? AudioBank.Instance
                    : Object.FindFirstObjectByType<AudioBank>();
                audioResolved = true;
            }
            if (cachedAudio != null) cachedAudio.PlaySelect();
        }

        private void Update()
        {
            if (_ruInstance == null) _ruInstance = this;   // P1-5 lazy
            if (ResearchManager.Instance == null) return;
            // N 키 = picker 토글
            // P1-5 — 설정 모달 열림 중 N/숫자키가 backdrop 을 관통하던 것 가드.
            if (SettingsMenu.AnyOpen) return;
            if (Input.GetKeyDown(KeyCode.N))
            {
                pickerOpen = !pickerOpen;
                if (pickerPanel != null) pickerPanel.gameObject.SetActive(pickerOpen);
                if (pickerOpen) { CloseSiblingPanels(); RefreshPicker(); }   // #ui백로그 5.3
            }
            // #ui백로그 5.3 — ESC 닫기: designation 모드 7곳의 'ESC=취소' 관례와 정합.
            //  SimInput 경유 (규칙 2).
            if (pickerOpen && SimInput.GetKeyDown(KeyCode.Escape)) ClosePicker();
            // Number keys 1-5 in picker mode = start that tech
            if (pickerOpen)
            {
                for (KeyCode k = KeyCode.Alpha1; k <= KeyCode.Alpha5; k++)
                {
                    if (Input.GetKeyDown(k))
                    {
                        int idx = k - KeyCode.Alpha1;
                        if (idx < ResearchManager.Instance.techs.Count)
                        {
                            var t = ResearchManager.Instance.techs[idx];
                            ResearchManager.Instance.SetActive(t);
                            PlayClickBlip();   // wiki #5 — tech-pick is a UI action
                            RefreshPicker();
                        }
                    }
                }
            }

            // Status update — once per 0.25s
            if (Time.time - lastUpdate < 0.25f) return;
            lastUpdate = Time.time;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (statusText == null) return;
            var rm = ResearchManager.Instance;
            if (rm == null) return;  // #버그헌트: Instance null deref 방어
            var active = rm.activeTech;
            if (active == null)
            {
                statusText.text = "연구: 없음 (N=선택)";
                if (progressBar != null) progressBar.fillAmount = 0f;
                FitStripToText();
                return;
            }
            // #게임필 배치4 — 정지 원인 병기: '0/100' 이 왜 안 오르는지 화면이 말해준다.
            string stall = rm.StallReason;
            statusText.text = stall != null
                ? $"연구: {active.nameKr} {active.currentPoints}/{active.requiredPoints} — {stall}"
                : $"연구: {active.nameKr} {active.currentPoints}/{active.requiredPoints}";
            if (progressBar != null)
                progressBar.fillAmount = Mathf.Clamp01((float)active.currentPoints / active.requiredPoints);
            FitStripToText();
        }

        /// <summary>스트립 배경을 텍스트 높이에 맞춘다.
        ///
        /// 운영자 fb (2026-07-27, 스크린샷): "연구: 원시 활 0/100 — 작업대 필요 (건축 F8)" 이
        /// 두 줄로 감기면서 **두 번째 줄이 배경 밖 잔디 위에 그대로 찍혔다**.
        /// 스트립 높이가 36 고정인데 statusText 는 verticalOverflow=Overflow 라 넘친 줄이
        /// 잘리지 않고 패널 바깥에 렌더된 것.  정지 사유(StallReason)가 붙으면 길이가
        /// 늘어나므로 문구를 줄이는 대신 **배경이 내용을 따라가게** 한다.
        /// 스트립은 pivot(1,0) 우하단 고정이라 위로 자란다 — 아래 시계 클러스터와 안 겹친다.</summary>
        private void FitStripToText()
        {
            if (statusText == null) return;
            var stripRt = statusText.rectTransform.parent as RectTransform;
            if (stripRt == null) return;
            // statusText 는 스트립에 stretch 로 물려 좌우 12 / 상하 8 만큼 안쪽이다.
            float needed = statusText.preferredHeight + 8f;
            float h = Mathf.Max(StripMinHeight, needed);
            if (!Mathf.Approximately(stripRt.sizeDelta.y, h))
                stripRt.sizeDelta = new Vector2(stripRt.sizeDelta.x, h);
        }

        private const float StripMinHeight = 36f;

        private void RefreshPicker()
        {
            if (pickerText == null) return;
            var rm = ResearchManager.Instance;
            if (rm == null) return;  // #버그헌트: Instance null deref 방어
            var sb = new System.Text.StringBuilder();
            // UI가독성 (운영자 2026-06-11) — '==' 디버그 콘솔 헤더 → 골드 타이틀 +
            //  보조 힌트 (richText 는 bake 의 Text 기본 supportRichText=true).
            sb.AppendLine("<color=#F4D78A><b>연구 선택</b></color>  <color=#BBAA94><size=17>숫자키로 시작 · N 닫기</size></color>");
            sb.AppendLine();
            // 디자인폴리시(2026-06-14) — 평면 목록에 상태별 위계 부여(레이아웃 불변, richText만):
            //  완료=차분한 녹·○ / 진행=오렌지·▶ / 잠김=흐린 회갈·× / 가능=크림(숫자키).
            //  비용은 골드, 설명은 들여쓴 흐린 소형 — 이름>비용>설명 위계.
            for (int i = 0; i < rm.techs.Count; i++)
            {
                var t = rm.techs[i];
                string nameCol, mark;
                // 대비 재설정 (2026-07-27).  평가자 다수가 "2·3번 연구는 이름이 아예 없다"고
                //  읽었고, UX 실측에서 그 제목의 대비가 **1.00:1** — 배경색과 완전 동일로 나왔다.
                //  범인은 잠김색이 아니라 **선택가능 색 #F2E4D0**(크림)이었다: 패널 배경이
                //  크림(#ECE1CE)이라 "가능" 상태 항목이 배경에 그대로 녹아 사라졌다.
                //  즉 가장 눌러야 할 항목이 가장 안 보이는 정반대 위계였다.
                //  이 패널은 밝은 배경이므로 **어두운 글자**가 기본이어야 한다.
                if (t.completed)               { mark = " ○"; nameCol = "#4A6B3A"; }
                else if (rm.activeTech == t)   { mark = " ▶"; nameCol = "#B4741E"; }
                else if (!rm.CanStart(t))      { mark = " ×"; nameCol = "#6B5A45"; }
                else                           { mark = "";   nameCol = "#3A2E22"; }
                sb.AppendLine($"<color={nameCol}><b>{i+1}. {t.nameKr}</b>{mark}</color>  <color=#C8963C>{t.requiredPoints}pt</color>");
                sb.AppendLine($"     <color=#A89A86><size=16>{t.descKr}</size></color>");
            }
            pickerText.text = sb.ToString();
        }

        /// <summary>#ui백로그 5.3 — 중앙 팝업 3종 상호배타용 공개 API.</summary>
        public bool PickerOpen => pickerOpen;

        public void ClosePicker()
        {
            if (!pickerOpen) return;
            pickerOpen = false;
            if (pickerPanel != null) pickerPanel.gameObject.SetActive(false);
        }

        /// <summary>#5.3 — picker 가 열릴 때 같은 중앙 자리를 쓰는 직업/일정 탭을 닫는다.</summary>
        /// <summary>P1-5 — SettingsMenu 가 호출하는 정적 픽커 닫기 (인스턴스 경유).</summary>
        private static ResearchUI _ruInstance;
        public static void ClosePickerStatic()
        {
            if (_ruInstance != null && _ruInstance.pickerOpen) _ruInstance.ClosePicker();
        }

        private static void CloseSiblingPanels()
        {
            HotkeyCheatSheet.CloseStatic();   // P2-4 상호배타
            if (WorkTabUI.Instance != null && WorkTabUI.Instance.IsOpen) WorkTabUI.Instance.Close();
            if (ScheduleUI.Instance != null && ScheduleUI.Instance.IsOpen) ScheduleUI.Instance.Close();
        }

        /// <summary>운영자 피드백 — N 키 대신 GUI 버튼에서 picker 토글</summary>
        public void TogglePicker()
        {
            // NOTE: the only caller is GuiControlBar's 연구 button, which already
            //  blips via its own onClick chokepoint — do NOT blip here too or the
            //  research button double-fires PlaySelect in one frame.
            pickerOpen = !pickerOpen;
            if (pickerPanel != null) pickerPanel.gameObject.SetActive(pickerOpen);
            if (pickerOpen) { CloseSiblingPanels(); RefreshPicker(); }   // #ui백로그 5.3
        }

        public void SetRefs(Text status, Image progress, RectTransform picker, Text pText)
        {
            statusText = status;
            progressBar = progress;
            pickerPanel = picker;
            pickerText = pText;
            if (pickerPanel != null) pickerPanel.gameObject.SetActive(false);
        }
    }
}
