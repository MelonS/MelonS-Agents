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
        // WoodGoal / BedGoal 상수는 제거됐다 (2026-08-01).  위 주석이 기록한 대로
        //  이 두 수치는 시작 정착지가 바뀔 때마다 조용히 공짜가 되어 세 번 뒤쫓아
        //  올려야 했다.  이제 목표는 시작 기준선 + Delta 로 파생된다 —
        //  아래 `UpdateBaseline` / `WoodTarget` / `BedTarget` 참조.

        // 잠자리 목표도 같은 이유로 3 → 6.  오늘 시작 집에 자동 지붕이 붙으면서(레퍼런스
        //  동작 정합 — RoofDesignation.NotifyWallBuilt 주석 참조) 3 은 시작과 동시에
        //  충족돼 버린다.  6 은 방을 하나 더 닫고 침대를 놓아야 하므로 **건축 루프 전체**를
        //  요구한다 — 벽·문·지붕·가구가 한 번씩 다 등장한다.  시작 3/6.
        // 6 → 9 (2026-08-01).  같은 함정을 두 번째 밟았다.
        //  7-31: 시작 집에 자동 지붕이 붙으면서 잠자리 3 이 시작과 동시에 충족 → 6 으로 상향.
        //  8-01: 주민을 6인으로 늘리며 집을 넓히고 침대를 6개 준 순간 **다시 공짜**가 됐다.
        //        게임 1일차에 '목표 달성 — 지붕 아래 잠자리 6' 알림이 뜬다(실측).
        //  교훈: 시작 정착지를 바꿀 때마다 목표 수치를 함께 봐야 한다 — 목표는 시작
        //        상태 대비 **증분**이어야 의미가 있다.  9 = 시작 6 + 방 하나 증축(3).

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
                label = $"목재 {WoodTarget} 비축",
                done = () => CurrentWood() >= WoodTarget,
                progress = () => $"{Mathf.Min(CurrentWood(), WoodTarget)}/{WoodTarget}",
            });
            objectives.Add(new Objective {
                label = $"지붕 아래 잠자리 {BedTarget}",
                done = () => RoofedBedCount() >= BedTarget,
                progress = () => $"{Mathf.Min(RoofedBedCount(), BedTarget)}/{BedTarget}",
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

        // ── 시작 상태 기준선 ────────────────────────────────────────────────
        //
        // 왜 상수가 아니라 기준선인가 (2026-08-01, 정합성 리뷰 #2):
        //  위 주석들이 "목표는 시작 상태 대비 **증분**이어야 의미가 있다"는 교훈을
        //  이미 두 번 적어 놨는데, 코드는 계속 절대값이었다.  그래서 시작 정착지를
        //  건드릴 때마다 목표가 조용히 공짜가 됐다 — 잠자리는 3→6→9 로 두 번,
        //  목재는 200→400 으로 한 번 뒤늦게 올렸다.  세 번째를 막으려면 수치가
        //  아니라 **구조**를 바꿔야 한다.  목표는 "받아 든 상태에서 얼마나 더" 로 잰다.
        //
        // 언제 찍나:
        //  게임은 일시정지 상태로 부팅한다(사람이 맵을 둘러본 뒤 직접 시작).  그 동안은
        //  아직 스폰·자동 지붕이 진행 중일 수 있으므로 기준선을 **계속 다시 찍고**,
        //  플레이어가 시간을 흘리기 시작하는 순간 얼린다.  즉 기준선은 정확히
        //  "조작을 넘겨받은 시점의 정착지" 다.  하네스 실행은 일시정지가 없으므로
        //  첫 평가 틱에 바로 얼린다 — 어느 경로든 '시작 상태' 라는 의미는 같다.
        private int startWood, startBeds;
        private bool baselineFrozen;

        /// <summary>목재 목표 = 시작 보유분 + 이만큼 더.  시작 더미 운반만으로는 못 채운다.</summary>
        private const int WoodDelta = 250;
        /// <summary>잠자리 목표 = 시작 잠자리 + 이만큼 더.  방 하나 증축 분량.</summary>
        private const int BedDelta = 3;

        private int WoodTarget => startWood + WoodDelta;
        private int BedTarget => startBeds + BedDelta;

        /// <summary>비축 목재 — **바닥 더미를 포함**한다.
        ///
        /// 카운터만 보면 시작값이 0 이라 '아직 아무것도 없다' 로 읽히지만, 실제로는
        /// 바닥에 300 이 널려 있고 운반만 하면 그대로 적립된다.  기준선을 카운터로만
        /// 잡으면 '운반 = 목표 달성' 이 되어 목표가 다시 공짜가 된다.</summary>
        private static int CurrentWood()
        {
            var rm = ResourceManager.Instance;
            int n = rm != null ? rm.wood : 0;
            foreach (var p in Object.FindObjectsByType<WoodPileEntity>(FindObjectsSortMode.None))
                if (p != null && !p.InStockpile) n += p.Wood;
            return n;
        }

        /// <summary>일시정지가 풀리기 전까지 기준선을 최신 상태로 따라가게 하고,
        /// 시간이 흐르기 시작하면 그대로 얼린다.  평가 틱에서 호출.</summary>
        private void UpdateBaseline()
        {
            if (baselineFrozen) return;
            startWood = CurrentWood();
            startBeds = RoofedBedCount();
            // 라벨의 숫자도 기준선에서 파생시킨다 — 상수를 그대로 쓰면 화면에 적힌
            //  목표와 실제 판정 수치가 어긋난다(플레이어가 볼 수 있는 종류의 거짓말).
            if (objectives.Count >= 2)
            {
                objectives[0].label = $"목재 {WoodTarget} 비축";
                objectives[1].label = $"지붕 아래 잠자리 {BedTarget}";
            }
            if (Time.timeScale > 0f)
            {
                baselineFrozen = true;
                Debug.Log($"[Objectives] 시작 기준선 확정 — 목재 {startWood} (목표 {WoodTarget}), "
                          + $"잠자리 {startBeds} (목표 {BedTarget})");
            }
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
            // 바로 위 주석이 "연구 목록이 바뀌면 조용히 틀린다"고 경고해 놓고, 정작
            //  아래에 tech id 를 하드코딩하고 있었다 (2026-08-01 정합성 리뷰 #11).
            //  그 사이 `stone_walls` 는 트리에서 삭제됐고(ResearchManager 주석 참조)
            //  `masonry` 가 추가됐다.  결과: 삭제된 tech 는 **영원히 false**, 석공술을
            //  연구해도 목표에 크레딧이 없어 4개 중 3개가 상한이었다.
            //  경고를 주석이 아니라 **구조**로 지킨다 — 트리 자체에 물어본다.
            int n = 0;
            foreach (var t in rm.techs)
                if (t != null && rm.IsUnlocked(t.id)) n++;
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
            UpdateBaseline();   // 일시정지 중엔 따라가고, 시간이 흐르면 얼린다 (위 주석)
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
                // 토스트가 아니라 **전용 화면**으로 (2026-08-01 UX 리뷰).
                //  기존 tier 2 알림은 약탈자 경보와 같은 주황·같은 자리라 위험 신호로
                //  읽혔고, 카드 3장 제한에 밀려 사라질 수도 있었다.  제출 요강이 요구하는
                //  '종료 조건' 이 화면에서 가장 안 보이는 역설이었다.
                GameOverOverlay.ShowVictory(days);
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
