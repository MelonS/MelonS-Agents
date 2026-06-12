using UnityEngine;
using UnityEngine.UI;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 피드백 #7/#9 (2026-06-12 AM) — "침대 우클릭 메뉴가 나왔다 안 나왔다."
    /// 재현 결과 메뉴 자체는 100% 안정: 정체는 건축/지정 모드가 켜진 채의 우클릭이
    /// '모드 해제'로 소비되는 것 (레퍼런스와 같은 규칙).  문제는 모드가 켜져 있다는
    /// 시각 신호가 약한 것 — 레퍼런스는 모드 중 도구 커서+ESC 안내가 떠 있다.
    ///
    /// 화면 상단 중앙에 활성 모드 배너("벌목 지정 중 — 우클릭/ESC 해제")를 상시
    /// 표시한다.  self-bootstrap(GameOverOverlay 패턴), 0.25s 폴링(이벤트 구독 금지
    /// — bug-pattern #7), 모드 없으면 숨김.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModeBanner : MonoBehaviour
    {
        private Text label;
        private GameObject root;
        private float nextPoll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (Object.FindFirstObjectByType<ModeBanner>() != null) return;
                var go = new GameObject("~ModeBanner");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<ModeBanner>();
            });
        }

        private void Update()
        {
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 0.25f;

            string mode = ActiveModeLabel();
            if (mode == null)
            {
                if (root != null && root.activeSelf) root.SetActive(false);
                return;
            }
            EnsureUI();
            if (root == null) return;
            if (!root.activeSelf) root.SetActive(true);
            label.text = $"{mode} — 우클릭/ESC 해제";
        }

        private static string ActiveModeLabel()
        {
            if (BuildManager.Instance != null && BuildManager.Instance.BuildModeActive)
                return $"건설: {BuildManager.Instance.CurrentModeKr()}";
            if (TreeChopDesignation.Instance != null && TreeChopDesignation.Instance.ModeActive) return "벌목 지정 중";
            if (MineDesignation.Instance != null && MineDesignation.Instance.ModeActive) return "채광 지정 중";
            if (DeconstructDesignation.Instance != null && DeconstructDesignation.Instance.ModeActive) return "해체 지정 중";
            if (GrowZoneDesignation.Instance != null && GrowZoneDesignation.Instance.ModeActive)
                return GrowZoneDesignation.Instance.EraseMode ? "경작 영역 제거 중" : "경작 지정 중";
            if (StockpileDesignation.Instance != null && StockpileDesignation.Instance.ModeActive)
                return StockpileDesignation.Instance.DumpingMode ? "폐기 구역 지정 중" : "저장 구역 지정 중";
            if (RoofDesignation.Instance != null && RoofDesignation.Instance.ModeActive)
                return RoofDesignation.Instance.EraseMode ? "지붕 영역 제거 중" : "지붕 영역 지정 중";
            return null;
        }

        private void EnsureUI()
        {
            if (root != null) return;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            root = new GameObject("ModeBannerRoot");
            root.transform.SetParent(canvas.transform, false);
            var rt = root.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(360, 30);
            rt.anchoredPosition = new Vector2(0, -146);  // UI겹침 P1-1 — TopBar(76)+ColonistBar(84+54)+8 아래

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.07f, 0.06f, 0.82f);
            bg.raycastTarget = false;

            var tgo = new GameObject("Label");
            tgo.transform.SetParent(root.transform, false);
            label = tgo.AddComponent<Text>();
            label.font = UITheme.LoadKoreanFont(16);
            label.fontSize = 15;
            label.color = UITheme.AccentGold;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            root.transform.SetAsLastSibling();
        }
    }
}
