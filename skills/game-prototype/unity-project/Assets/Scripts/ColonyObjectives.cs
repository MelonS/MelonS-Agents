using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 정착 목표 — 화면에 상시 표시되는 4단계 체크리스트와 **승리 조건**.
    ///
    /// 계기 (2026-07-30 운영자): "이게 머하는 게임인지 전혀 파악이 안됨",
    /// "게임인지 아닌지도 모르겠어".
    ///
    /// 진단해 보니 세 가지가 동시에 비어 있었다:
    ///   ① 세계가 스스로 돌지 않았다      → 시작 캠프·기분 모델로 해결
    ///   ② 사람처럼 짓지 못했다            → 자동 정리·동선 배치로 해결
    ///   ③ **무엇을 목표로 하는지 없었다**  ← 이 파일
    ///
    /// 콜로니 심은 열린 장르지만, **처음 3분의 플레이어에게는 방향이 필요하다.**
    /// 그리고 제출 요강이 "종료 조건"을 명시적으로 요구한다 — 지금까지는 패배(전멸)만
    /// 있고 승리가 없어서 "무한 운영형"이라고 적을 수밖에 없었다.
    ///
    /// 목표는 **튜토리얼이 가르치는 순서와 같은 축**으로 잡는다.  따로 놀면 두 개의
    /// 지시가 서로 경쟁한다:
    ///   1. 목재 200 비축      — 벌목을 지정해야 는다 (튜토리얼 ①)
    ///   2. 지붕 아래 잠자리 3 — 방을 만들고 지붕을 지정해야 한다 (튜토리얼 ②)
    ///   3. 연구 1개 완료      — 연구대에 사람을 붙여야 한다
    ///   4. 첫 습격 격퇴       — **승리**
    ///
    /// 1~3 은 시작 캠프 덕에 '가능하지만 저절로는 안 되는' 지점에 걸려 있다:
    /// 목재는 초기 300 중 창고 적립분만 세므로 벌목이 필요하고, 침대는 있지만 지붕이
    /// 없으며, 연구대는 있지만 우선순위를 올려야 진행된다.
    ///
    /// 배선은 자가 부팅([RuntimeInitializeOnLoadMethod]) — 씬 재베이크 없이 붙는다.
    /// </summary>
    public class ColonyObjectives : MonoBehaviour
    {
        public static ColonyObjectives Instance { get; private set; }

        private sealed class Objective
        {
            public string label;
            public System.Func<bool> done;
            public System.Func<string> progress;   // null 이면 진행도 표기 없음
            public bool completed;
            public Text row;
        }

        private readonly List<Objective> objectives = new List<Objective>();
        private Text headerText;
        private bool victoryFired;
        private float nextEval;

        // 2026-07-31 재조정 — 데모 스틸 6장을 시간순으로 보고 잡은 결함.
        //  기존 200 은 **시작 저장구역 적립분만으로 첫 프레임에 충족**됐다(d00 에 이미 ☑).
        //  그 결과 4분짜리 영상 내내 패널이 1/4 로 굳어 있었다 — 하나는 공짜로 켜져 있고
        //  나머지 셋은 아래 사유로 도달 불가라, "목표가 있는데 아무것도 안 움직인다"는
        //  최악의 조합이었다.  400 은 벌목 지정 → 운반 → 적립 루프를 한 바퀴 이상 돌려야
        //  닿는다(시작 200 + 나무 한 그루 27~50).  시작 화면은 200/400 = 절반 찬 상태로
        //  열리므로, 카운터가 살아 있다는 것도 첫 프레임부터 읽힌다.
        private const int WoodGoal = 400;

        // 잠자리 목표도 같은 이유로 3 → 6.  오늘 시작 집에 자동 지붕이 붙으면서(레퍼런스
        //  동작 정합 — RoofDesignation.NotifyWallBuilt 주석 참조) 3 은 시작과 동시에
        //  충족돼 버린다.  6 은 방을 하나 더 닫고 침대를 놓아야 하므로 **건축 루프 전체**를
        //  요구한다 — 벽·문·지붕·가구가 한 번씩 다 등장한다.  시작 3/6.
        private const int BedGoal = 6;

        // 패널 타이포 (2026-07-31) — 헤더와 행이 같은 크기다.  강조는 색(AccentGold)과
        //  위치로 준다(1차 구현이 헤더만 크게 만들었다가 아예 안 그려진 전례가 있다).
        // 18 → 20 (2026-08-01).  자원 패널을 32 → 24 로 낮추면서 함께 올려 단차를 만든다.
        //  목표는 '무엇을 해야 하나' 라 자원보다 작을 이유가 없다.
        private const int RowFontSize = 20;
        private const float RowStep = 29f;   // 글자 18 + 여백.  좁히면 받침이 윗줄에 닿는다.

        // ⚠ `AfterSceneLoad` 는 **첫 씬이 로드된 직후 한 번만** 실행된다.  이 빌드의 첫 씬은
        //  MainMenu 라, 여기서 `scene.name != "Game"` 으로 걸러 버리면 나중에 Game 씬이
        //  열려도 다시 불리지 않는다 — 1차 구현이 정확히 그래서 패널이 화면에 없었고
        //  로그도 예외도 남지 않았다(조용한 미실행).
        //  그래서 부팅 시 **sceneLoaded 를 구독**해 Game 이 열릴 때마다 붙인다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            // 이미 Game 씬에서 시작한 경우(에디터 재생·게임 단독 씬 빌드)도 즉시 처리.
            TrySpawn(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s,
                                          UnityEngine.SceneManagement.LoadSceneMode m)
            => TrySpawn(s.name);

        private static void TrySpawn(string sceneName)
        {
            if (sceneName != "Game") return;
            if (FindFirstObjectByType<ColonyObjectives>() != null) return;
            var go = new GameObject("ColonyObjectives");
            go.AddComponent<ColonyObjectives>();
            Debug.Log("[Objectives] 정착 목표 패널 부착");
        }

        private void Awake()
        {
            Instance = this;
            BuildObjectives();
            BuildUI();
        }

        private void BuildObjectives()
        {
            objectives.Add(new Objective {
                label = $"목재 {WoodGoal} 비축",
                done = () => ResourceManager.Instance != null && ResourceManager.Instance.wood >= WoodGoal,
                progress = () => ResourceManager.Instance != null
                                 ? $"{Mathf.Min(ResourceManager.Instance.wood, WoodGoal)}/{WoodGoal}" : "",
            });
            objectives.Add(new Objective {
                label = $"지붕 아래 잠자리 {BedGoal}",
                done = () => RoofedBedCount() >= BedGoal,
                progress = () => $"{Mathf.Min(RoofedBedCount(), BedGoal)}/{BedGoal}",
            });
            objectives.Add(new Objective {
                label = "연구 1개 완료",
                done = () => CompletedResearchCount() >= 1,
                progress = () => $"{Mathf.Min(CompletedResearchCount(), 1)}/1",
            });
            objectives.Add(new Objective {
                label = "첫 습격 격퇴",
                done = () => RaidRepelled(),
                progress = () => RaidHappened() ? (LiveBandits() == 0 ? "1/1" : "교전 중") : "0/1",
            });
        }

        // ── 판정 ────────────────────────────────────────────────────────────
        private static int RoofedBedCount()
        {
            var rd = RoofDesignation.Instance;
            if (rd == null) return 0;
            int n = 0;
            foreach (var b in Object.FindObjectsByType<BedEntity>(FindObjectsSortMode.None))
            {
                if (b == null) continue;
                var c = new Vector2Int(Mathf.FloorToInt(b.transform.position.x),
                                       Mathf.FloorToInt(b.transform.position.y));
                if (rd.IsRoofed(c)) n++;
            }
            return n;
        }

        private static int CompletedResearchCount()
        {
            var rm = ResearchManager.Instance;
            if (rm == null) return 0;
            // 완료 판정은 ResearchManager 가 이미 노출하는 IsUnlocked 로만 한다 —
            //  내부 목록 형태에 의존하면 연구 목록이 바뀔 때 조용히 틀린다.
            string[] techs = { "simple_bow", "irrigation", "better_stove", "stone_walls" };
            int n = 0;
            foreach (var t in techs) if (rm.IsUnlocked(t)) n++;
            return n;
        }

        private static int LiveBandits()
        {
            int n = 0;
            foreach (var b in Object.FindObjectsByType<BanditEnemy>(FindObjectsSortMode.None))
                if (b != null && b.isActiveAndEnabled) n++;
            return n;
        }

        //  AIDirector 는 싱글턴 프로퍼티가 없다 — AlertStackUI 와 같은 방식으로 찾는다.
        //  0.5s 폴링 안에서만 호출되므로 매 프레임 FindObjects 금지 규약에 걸리지 않는다.
        private static bool RaidHappened()
        {
            var d = Object.FindFirstObjectByType<AIDirector>();
            return d != null && d.RaidCount > 0;
        }

        // 이번 습격에서 **실제로 밴딧을 본 적이 있는가**.  아래 주석 참조.
        private static bool sawBandit;

        private static bool RaidRepelled()
        {
            if (!RaidHappened()) return false;
            // 2026-07-31 실측 버그: 교전이 한 번도 없었는데 '첫 습격 격퇴' 가 달성됐다.
            //  원인은 이 판정이 '밴딧을 물리쳤는가' 가 아니라 **'지금 살아 있는 밴딧이
            //  0인가'** 였다는 것.  습격 직후 밴딧이 맵 외곽에서 접근하는 동안
            //  isActiveAndEnabled 가 아직 false 인 프레임이 있으면 그 순간 곧바로
            //  '격퇴'로 판정됐다.  화면에는 밴딧이 오는 중인데 우상단엔 '습격 격퇴!' 가
            //  떴다 — 목표가 거짓말을 한다.
            //  이제 **밴딧을 실제로 본 뒤에** 0이 되어야 격퇴로 친다.
            if (LiveBandits() > 0) { sawBandit = true; return false; }
            if (!sawBandit) return false;
            foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
                if (p != null && !p.IsDead) return true;
            return false;
        }

        // ── UI ──────────────────────────────────────────────────────────────
        private void BuildUI()
        {
            var canvasGo = new GameObject("ObjectivesCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;           // 알림(200) 아래, 월드 UI 위
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var font = MelonS.GameProto.Core.UITheme.LoadKoreanFont(16);

            // 좌상단 자원 패널 아래.  자원(4행 × ~42px + 패딩) ≈ 190px 를 피한다.
            var panel = new GameObject("ObjectivesPanel", typeof(RectTransform), typeof(Image));
            var prt = (RectTransform)panel.transform;
            prt.SetParent(canvasGo.transform, false);
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0f, 1f);
            // 2026-07-31 — 패널을 키운다.  실측 스크린샷(1600x900 브라우저)에서 이 패널이
            //  **화면에서 가장 작은 글씨**였다.  바로 위 자원 표시(식량/목재)의 절반 크기인데,
            //  자원은 "지금 얼마나 있나"이고 목표는 "무엇을 해야 하나"다 — 위계가 뒤집혀 있었다.
            //  글자 15→18 에 맞춰 행 간격·패널 폭/높이를 함께 올린다(하나만 바꾸면 겹치거나 잘린다).
            // 2026-08-01 — 자원 패널 아래로 내리는 오프셋을 실측에 맞춰 재조정.
            //  자원 줄이 4행 → 5행('마을 가치' 를 별도 줄로 분리)이 되면서 자원 패널이
            //  아래로 자라 **목표 패널을 덮었다** — 확대 실측에서 목표 패널이 흰 상자로만
            //  보였다(글자는 자원 패널 뒤에 깔림).
            //  자원 패널 실측 높이(상단 여백 76 + 5행) 를 반영해 250 으로 내린다.
            prt.anchoredPosition = new Vector2(8f, -(76f + 8f + 250f));
            prt.sizeDelta = new Vector2(272f, 34f + objectives.Count * RowStep + 12f);
            var pimg = panel.GetComponent<Image>();
            pimg.color = MelonS.GameProto.Core.UITheme.PanelBg;
            pimg.raycastTarget = false;

            // 헤더도 **행과 완전히 같은 방식**으로 만든다.  1차 구현은 크기 17 · AccentGold 로
            //  따로 만들었는데 화면에 아무것도 안 나왔다(픽셀 샘플링으로 확인 — 패널 배경색만
            //  균일).  행과 다른 점을 없애 변수를 제거한다.  강조는 색이 아니라 위치로 준다.
            headerText = MakeRow(prt, font, 6f, "정착 목표", MelonS.GameProto.Core.UITheme.AccentGold, RowFontSize);
            for (int i = 0; i < objectives.Count; i++)
                objectives[i].row = MakeRow(prt, font, 34f + i * RowStep, "", MelonS.GameProto.Core.UITheme.TextPrimary, RowFontSize);

            Refresh(force: true);
        }

        private static Text MakeRow(RectTransform parent, Font font, float top, string txt, Color col, int size)
        {
            var go = new GameObject("row", typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(8f, 0f); rt.offsetMax = new Vector2(-8f, 0f);
            rt.anchoredPosition = new Vector2(8f, -top);
            // 행 높이는 **글자 크기에서 유도**한다 (2026-08-01).
            //  26px 고정이었는데 글자를 18 → 20 으로 올리자 한글이 세로로 잘려
            //  **패널이 통째로 비어 보였다**(흰 상자만 남음).  숫자를 하드코딩하면
            //  다음에 크기를 바꿀 때 같은 사고가 반복된다.
            rt.sizeDelta = new Vector2(0f, size * 1.6f);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = size; t.color = col;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = txt;
            return t;
        }

        private void Update()
        {
            // 매 프레임 FindObjects 금지 (프로젝트 규약) — 0.5s 폴링.
            if (Time.unscaledTime < nextEval) return;
            nextEval = Time.unscaledTime + 0.5f;
            Refresh(force: false);
        }

        private void Refresh(bool force)
        {
            int doneCount = 0;
            for (int i = 0; i < objectives.Count; i++)
            {
                var o = objectives[i];
                bool now = false;
                try { now = o.done(); } catch { now = o.completed; }
                if (now && !o.completed)
                {
                    o.completed = true;
                    AlertStackUI.Notify($"목표 달성 — {o.label}", 1);
                }
                if (o.completed) doneCount++;
                if (o.row != null)
                {
                    string prog = o.progress != null ? o.progress() : "";
                    // ☑/☐ (U+2611/U+2610) 은 **번들 폰트 3종 어디에도 없다** —
                    //  WebGL 에서 빈칸(tofu)으로 나온다.  Windows 에서만 OS 폰트가
                    //  대신 그려 줘서 개발 중엔 멀쩡해 보였다.
                    //  (2026-07-31 `check-font-coverage.py` 가 잡았다.  이 검사는
                    //   ✕ U+2715 사고 뒤에 만들어졌는데, 목표 패널을 추가할 때
                    //   아무도 돌리지 않아 같은 유형이 다시 들어왔다.)
                    //  ■/□ (U+25A0/U+25A1) 은 3종 모두에 있다.  달성 행은 녹색이므로
                    //  '채워진 사각 + 녹색'이 체크 표시 역할을 한다.
                    o.row.text = (o.completed ? "■ " : "□ ") + o.label
                                 + (o.completed || string.IsNullOrEmpty(prog) ? "" : $"   {prog}");
                    o.row.color = o.completed
                        ? new Color(0.55f, 0.78f, 0.45f, 1f)          // 달성 = 녹색
                        : MelonS.GameProto.Core.UITheme.TextPrimary;
                }
            }
            if (headerText != null) headerText.text = $"정착 목표  {doneCount}/{objectives.Count}";

            if (!victoryFired && doneCount >= objectives.Count)
            {
                victoryFired = true;
                int days = GameClock.Instance != null ? GameClock.Instance.Day : 0;
                Debug.Log($"[Victory] 정착 성공 — {days}일차, 목표 {objectives.Count}/{objectives.Count}");
                AlertStackUI.Notify("정착 성공! 모든 목표를 달성했습니다", 2);
            }
        }

        /// <summary>QA / 하네스용 — 달성 개수.</summary>
        public int CompletedCount
        {
            get { int n = 0; foreach (var o in objectives) if (o.completed) n++; return n; }
        }

        /// <summary>QA / 하네스용 — 승리 발생 여부.</summary>
        public bool VictoryReached => victoryFired;
    }
}
