using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 피드백 - wolf/bandit 등장 시 EventLog 텍스트만 보임.  운영자 놓치기 쉬움.
    /// 화면 우상단에 큰 빨강 "! 위협" 텍스트 + 2초 fade-in/out.
    ///
    /// 사람-리듬 #7 (human-play-gap-2026-06-12.md): 레퍼런스는 위협 이벤트 시
    /// 엔진이 강제 감속 + 레터 클릭 = 현장 점프가 표준 리듬.  알림 발화 시
    /// 배속>1 이면 1x 로 강제(일시정지는 건드리지 않음) + 카메라를 위협
    /// 위치로 포커스.
    ///
    /// Self-bootstrap (GameManager.EnsureInScene 호출).
    /// 매 프레임 wolf/bandit 거리 폴링 (5 unit 이내 처음 detect 면 알림).
    /// </summary>
    public class ThreatAlertUI : MonoBehaviour
    {
        private static ThreatAlertUI _instance;
        private Text alertText;
        private float alertShownTime = -100f;
        private const float AlertDurationSec = 2.5f;
        private string currentAlertSubject = "";  // 같은 wolf 반복 알림 방지

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("ThreatAlertUI");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<ThreatAlertUI>();
        }

        private void Awake()
        {
            var rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(320, 64);
            rt.anchoredPosition = new Vector2(-12, -240);  // EventLog 아래 (EventLog -72 + 160 + gap)

            alertText = gameObject.AddComponent<Text>();
            alertText.text = "";
            alertText.font = LoadKoreanFont();
            alertText.fontSize = 32;
            alertText.fontStyle = FontStyle.Bold;
            alertText.color = new Color(1f, 0.25f, 0.20f, 0f);  // start hidden
            alertText.alignment = TextAnchor.MiddleRight;
            alertText.supportRichText = true;
            // 감사 rank17: 밝은 지형 위 빨강 텍스트 가독성 — 어두운 외곽선.
            var ol = gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
            ol.effectDistance = new Vector2(2f, -2f);
        }

        private Font LoadKoreanFont()
        {
            var bundled = Resources.Load<Font>("Fonts/DNFBitBit") ?? Resources.Load<Font>("Fonts/NotoSansKR");
            if (bundled != null) return bundled;
            string[] candidates = { "Malgun Gothic", "NanumGothic", "Gulim", "Dotum", "Arial Unicode MS" };
            foreach (var name in candidates)
            {
                var f = Font.CreateDynamicFontFromOSFont(name, 32);
                if (f != null) return f;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private float lastCheck = -10f;
        private void Update()
        {
            // 폴링 0.5s - 비싸므로 throttle
            if (Time.time - lastCheck > 0.5f)
            {
                lastCheck = Time.time;
                CheckThreats();
            }

            // fade in/out
            float elapsed = Time.time - alertShownTime;
            float alpha;
            if (elapsed < 0.3f)
                alpha = elapsed / 0.3f;  // fade in
            else if (elapsed < AlertDurationSec - 0.5f)
                alpha = 1f;  // 고정
            else if (elapsed < AlertDurationSec)
                alpha = 1f - (elapsed - (AlertDurationSec - 0.5f)) / 0.5f;
            else
                alpha = 0f;
            var c = alertText.color; c.a = alpha; alertText.color = c;
        }

        private void CheckThreats()
        {
            // pawn 와 가장 가까운 wolf/bandit 거리 - 8 unit 이내 면 위협.
            var pawns = Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
            if (pawns.Length == 0) return;
            var wolves = Object.FindObjectsByType<WolfEnemy>(FindObjectsSortMode.None);
            var bandits = Object.FindObjectsByType<BanditEnemy>(FindObjectsSortMode.None);

            // wolf 검사
            foreach (var w in wolves)
            {
                if (w == null || w.IsDead) continue;
                foreach (var p in pawns)
                {
                    if (p == null || p.IsDead) continue;
                    float d = Vector3.Distance(w.transform.position, p.transform.position);
                    if (d < 8f)
                    {
                        string key = $"wolf_{w.GetInstanceID()}";
                        if (currentAlertSubject != key)
                        {
                            currentAlertSubject = key;
                            ShowAlert("! 늑대 위협!", w.transform.position, true);
                        }
                        return;
                    }
                }
            }

            foreach (var b in bandits)
            {
                if (b == null || b.IsDead) continue;
                foreach (var p in pawns)
                {
                    if (p == null || p.IsDead) continue;
                    float d = Vector3.Distance(b.transform.position, p.transform.position);
                    if (d < 10f)
                    {
                        string key = $"bandit_{b.GetInstanceID()}";
                        if (currentAlertSubject != key)
                        {
                            currentAlertSubject = key;
                            ShowAlert("! 강도 침입!", b.transform.position, true);
                        }
                        return;
                    }
                }
            }
        }

        private void ShowAlert(string text) => ShowAlert(text, Vector3.zero, false);

        private void ShowAlert(string text, Vector3 focusPos, bool hasFocus)
        {
            if (alertText == null) return;
            alertText.text = text;
            alertShownTime = Time.time;
            Debug.Log($"[ThreatAlert] {text}");

            // 사람-리듬 #7: 위협 레터 = 강제 1x + 현장 점프.
            var tc = TimeController.Instance;
            if (tc != null && !tc.IsPaused && tc.CurrentScale > 1f)
            {
                tc.SetScale(1f);
                Debug.Log("[ThreatAlert] forced-slowdown to 1x");
            }
            if (hasFocus)
            {
                var camCtl = Object.FindFirstObjectByType<CameraController>();
                if (camCtl != null)
                {
                    camCtl.FocusOn(new Vector2(focusPos.x, focusPos.y));
                    Debug.Log($"[ThreatAlert] camera-focus ({focusPos.x:F1},{focusPos.y:F1})");
                }
            }
        }
    }
}
