using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 피드백 - wolf/bandit 등장 시 EventLog 텍스트만 보임.  운영자 놓치기 쉬움.
    /// 화면 우상단에 큰 빨강 "⚠ 위협" 텍스트 + 2초 fade-in/out.
    ///
    /// auto-pause X, gameplay 변경 X, 시각 알림 only - "보수적 추가" 정책 부합.
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
        }

        private Font LoadKoreanFont()
        {
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
                            ShowAlert("⚠ 늑대 위협!");
                        }
                        return;
                    }
                }
            }

            foreach (var b in bandits)
            {
                if (b == null) continue;
                // BanditEnemy.IsDead 검사 - reflection 가벼움
                bool dead = b.GetType().GetProperty("IsDead")?.GetValue(b) as bool? ?? false;
                if (dead) continue;
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
                            ShowAlert("⚠ 강도 침입!");
                        }
                        return;
                    }
                }
            }
        }

        private void ShowAlert(string text)
        {
            if (alertText == null) return;
            alertText.text = text;
            alertShownTime = Time.time;
            Debug.Log($"[ThreatAlert] {text}");
        }
    }
}
