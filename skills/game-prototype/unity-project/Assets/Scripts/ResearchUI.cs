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

        private void Update()
        {
            if (ResearchManager.Instance == null) return;
            // N 키 = picker 토글
            if (Input.GetKeyDown(KeyCode.N))
            {
                pickerOpen = !pickerOpen;
                if (pickerPanel != null) pickerPanel.gameObject.SetActive(pickerOpen);
                if (pickerOpen) RefreshPicker();
            }
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
            var active = rm.activeTech;
            if (active == null)
            {
                statusText.text = "연구: 없음 (N=선택)";
                if (progressBar != null) progressBar.fillAmount = 0f;
                return;
            }
            statusText.text = $"연구: {active.nameKr} {active.currentPoints}/{active.requiredPoints}";
            if (progressBar != null)
                progressBar.fillAmount = Mathf.Clamp01((float)active.currentPoints / active.requiredPoints);
        }

        private void RefreshPicker()
        {
            if (pickerText == null) return;
            var rm = ResearchManager.Instance;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("== 연구 선택 (숫자키, N으로 닫기) ==");
            for (int i = 0; i < rm.techs.Count; i++)
            {
                var t = rm.techs[i];
                string status = t.completed ? " ✓" :
                                !rm.CanStart(t) ? " ✗" :
                                (rm.activeTech == t ? " ▶" : "");
                sb.AppendLine($"{i+1}. {t.nameKr}{status}  ({t.requiredPoints}pt)  {t.descKr}");
            }
            pickerText.text = sb.ToString();
        }

        /// <summary>운영자 피드백 — N 키 대신 GUI 버튼에서 picker 토글</summary>
        public void TogglePicker()
        {
            pickerOpen = !pickerOpen;
            if (pickerPanel != null) pickerPanel.gameObject.SetActive(pickerOpen);
            if (pickerOpen) RefreshPicker();
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
