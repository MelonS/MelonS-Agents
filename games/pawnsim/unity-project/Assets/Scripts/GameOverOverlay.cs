using UnityEngine;
using UnityEngine.UI;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// 정착지 전멸 게임오버 (운영자 2026-06-11 '가만히 냅둬도 게임오버가 되지 않음.
    /// 가장 큰 문제점').  생존게임 축의 종착점 — 콜로니스트 전원 사망 시 전체 화면
    /// 오버레이(생존 일수 + 다시 시작)와 일시정지.
    ///
    /// VignetteOverlay 와 같은 self-bootstrap 패턴 — SceneSetup 무수정, 파일 삭제로
    /// 완전 제거 가능.  2초 폴링(이벤트 구독 금지 — bug-pattern #7).
    /// 시작 직후 콜로니스트가 아직 스폰 전인 프레임을 '전멸'로 오인하지 않도록
    /// '한 번이라도 생존자를 본 뒤' 에만 발동한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameOverOverlay : MonoBehaviour
    {
        private bool sawColonists;
        private bool fired;
        private float nextPoll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (Object.FindFirstObjectByType<GameOverOverlay>() != null) return;
                var go = new GameObject("~GameOverOverlay");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<GameOverOverlay>();
            });
        }

        private void Update()
        {
            if (fired || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 2f;

            var pawns = Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
            int alive = 0, total = 0;
            foreach (var p in pawns)
            {
                if (p == null) continue;
                total++;
                if (!p.IsDead) alive++;
            }
            if (alive > 0) { sawColonists = true; return; }
            if (!sawColonists || total == 0) return;   // 스폰 전/씬 전환 프레임 보호

            fired = true;
            int days = GameClock.Instance != null ? GameClock.Instance.Day : 0;
            Debug.Log($"[GameOver] 정착지 전멸 — {days}일 생존");
            BuildOverlay(days);
            // 하네스(-repro) 중엔 일시정지 생략 — timeScale=0 이 scaled WaitForSeconds 를
            //  동결시켜 소크 시나리오의 후속 샷(전멸 화면·경보 카드 증거)이 전부 멈췄다.
            //  실플레이는 기존대로 정지.
            if (!ReproHarness.Enabled) Time.timeScale = 0f;
        }

        /// <summary>**승리 화면** — 정착 성공.  외부(ColonyObjectives)에서 호출.
        ///
        /// 2026-08-01 UX 리뷰가 잡은 비대칭: 패배는 34pt 전체 화면 오버레이인데
        /// 승리는 **15pt 주황 토스트** 하나였다.  그것도 약탈자 경보와 같은 색·같은 자리라
        /// 위험 신호로 읽히고, 카드 3장 제한에 밀려 사라질 수도 있었다.
        /// 제출 요강이 요구하는 '종료 조건'이 정작 화면에서 가장 안 보이는 상태였다.
        /// 패배와 **같은 무게**로 보여준다 — 같은 오버레이, 다른 문구.</summary>
        public static void ShowVictory(int days)
        {
            if (Object.FindFirstObjectByType<GameOverOverlay>() != null) return;   // 멱등
            var go = new GameObject("~VictoryOverlay");
            var ov = go.AddComponent<GameOverOverlay>();
            ov.isVictory = true;
            ov.BuildOverlay(days);
            Debug.Log($"[Victory] 승리 화면 표시 — {days}일차");
        }

        private bool isVictory;

        private void BuildOverlay(int days)
        {
            var canvasGo = new GameObject("GameOverCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;   // 모든 UI 위
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            // 어두운 백드롭 — 클릭 차단 (게임오버 뒤 월드 조작 방지)
            var dim = new GameObject("Dim");
            dim.transform.SetParent(canvasGo.transform, false);
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.72f);
            Stretch(dim.GetComponent<RectTransform>());

            // 중앙 패널
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            panelGo.AddComponent<Image>();
            var prt = panelGo.GetComponent<RectTransform>();
            prt.sizeDelta = new Vector2(520, 240);
            var content = UITheme.MakeBorderedPanel(prt, UITheme.BorderPx, UITheme.PanelBg, UITheme.PadOuter);

            var font = UITheme.LoadKoreanFont(30);
            MakeText(content, isVictory ? "정착 성공" : "정착지 전멸", font, 34,
                     UITheme.AccentGold, new Vector2(0, 55), new Vector2(480, 50));
            MakeText(content,
                     isVictory
                       ? $"{days}일 만에 네 가지 목표를 모두 이뤘습니다."
                       : $"{days}일 생존 — 모든 주민이 사망했습니다.",
                     font, 18, UITheme.TextPrimary, new Vector2(0, 8), new Vector2(480, 32));

            var btnGo = new GameObject("RestartBtn");
            btnGo.transform.SetParent(content, false);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = UITheme.BtnHover;
            var brt = btnGo.GetComponent<RectTransform>();
            brt.sizeDelta = new Vector2(200, 48);
            brt.anchoredPosition = new Vector2(0, -58);
            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                Time.timeScale = 1f;
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
            });
            MakeText(btnGo.GetComponent<RectTransform>(), "다시 시작", font, 20,
                     UITheme.TextPrimary, Vector2.zero, new Vector2(200, 48));

            // 퀵픽 '게임오버 관전' (2026-06-13) — 레퍼런스: 전멸 후에도 세계는 계속
            //  돌고 플레이어는 지켜볼 수 있다.  관전 = 차단막·패널 숨김 + 시간 재개,
            //  우상단 미니 배너(재시작 버튼 포함)만 남긴다.
            var specGo = new GameObject("SpectateBtn");
            specGo.transform.SetParent(content, false);
            var specImg = specGo.AddComponent<Image>();
            specImg.color = UITheme.PanelBgLight;
            var srt = specGo.GetComponent<RectTransform>();
            srt.sizeDelta = new Vector2(200, 36);
            srt.anchoredPosition = new Vector2(0, -106);
            var specBtn = specGo.AddComponent<Button>();
            var dimRef = dim; var panelRef = panelGo;
            specBtn.onClick.AddListener(() =>
            {
                Time.timeScale = 1f;
                dimRef.SetActive(false);
                panelRef.SetActive(false);
                BuildSpectateBanner(canvasGo.transform, font);
                Debug.Log("[GameOver] 관전 모드 진입");
            });
            MakeText(specGo.GetComponent<RectTransform>(), "관전", font, 17,
                     UITheme.TextSecondary, Vector2.zero, new Vector2(200, 36));
        }

        private void BuildSpectateBanner(Transform canvasRoot, Font font)
        {
            var bGo = new GameObject("SpectateBanner");
            bGo.transform.SetParent(canvasRoot, false);
            var img = bGo.AddComponent<Image>();
            img.color = new Color(0.10f, 0.085f, 0.07f, 0.85f);
            var rt = bGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(360, 34);
            rt.anchoredPosition = new Vector2(0, -6);
            MakeText(rt, "정착지 전멸 — 관전 중", font, 15, UITheme.TextDanger,
                     new Vector2(-50, 0), new Vector2(240, 30));
            var rGo = new GameObject("MiniRestart");
            rGo.transform.SetParent(bGo.transform, false);
            var rImg = rGo.AddComponent<Image>();
            rImg.color = UITheme.BtnHover;
            var rrt = rGo.GetComponent<RectTransform>();
            rrt.sizeDelta = new Vector2(90, 26);
            rrt.anchoredPosition = new Vector2(125, 0);
            var rBtn = rGo.AddComponent<Button>();
            rBtn.onClick.AddListener(() =>
            {
                Time.timeScale = 1f;
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
            });
            MakeText(rrt, "다시 시작", font, 13, UITheme.TextPrimary, Vector2.zero, new Vector2(90, 26));
        }

        private static void MakeText(RectTransform parent, string text, Font font, int size,
                                     Color color, Vector2 pos, Vector2 dims)
        {
            var go = new GameObject("Txt");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = dims;
            rt.anchoredPosition = pos;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
