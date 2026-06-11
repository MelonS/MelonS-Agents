using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// WORKFLOW-V2 규칙 1 — "재현 없으면 수정 없음"의 실행 장치.
    ///
    /// 운영자 버그 리포트를 JSON 시나리오(클릭 시퀀스 + 관찰 가능한 상태 assert)로
    /// 옮겨 적고, 이 컴포넌트가 빌드 안에서 운영자와 **같은 입력 경로**로 실행한다:
    ///   - 월드 클릭: SimInput 주입 → ClickSelector.Update 의 진짜 경로
    ///     (ScreenToWorldPoint → PickEntityAt → 메뉴/지정) 그대로 통과
    ///   - UI 클릭: GraphicRaycaster 레이캐스트 + ExecuteEvents (ArchitectClickAutoQA 패턴)
    ///   - assert: 운영자가 화면에서 보는 것(머리위 라벨, 메뉴 열림, 림 위치)을 검사
    ///
    /// 시나리오 파일 = 그 버그의 회귀 테스트. PASS 한 시나리오는 영구 보존.
    ///
    /// CLI:  PawnSim.exe -repro <scenario.json> [-repro-report <out.json>]
    ///                   [-repro-shotdir <dir>]
    ///   그래픽 빌드 필요 (-nographics 금지: GraphicRaycaster + 스크린샷).
    ///
    /// 시나리오 형식 (steps 순차 실행):
    ///   {"op":"wait","sec":2.5}
    ///   {"op":"worldclick","target":"tree"}          // 좌클릭 — target 리졸버 참조
    ///   {"op":"worldright","target":"pawn:철수"}      // 우클릭
    ///   {"op":"worldclick","x":12,"y":7}             // 절대 월드좌표도 가능
    ///   {"op":"clickui","name":"Btn_건축"}            // GO 이름 (또는 "label:벌목" 라벨 검색)
    ///   {"op":"key","name":"R"}
    ///   {"op":"shot","name":"01_after_click"}
    ///   {"op":"assert","probe":"contextMenuOpen","expect":"true"}
    ///   {"op":"assert","probe":"selection","expect":"철수"}        // "none"/"any"/이름
    ///   {"op":"assert","probe":"pawnMoved","pawn":"철수","min":1.0,"withinSec":6}
    ///   {"op":"assert","probe":"pawnNear","pawn":"철수","target":"tree","min":1.6,"withinSec":15}
    ///   {"op":"assert","probe":"pawnActivity","pawn":"철수","contains":"벌목","withinSec":10}
    ///   {"op":"assert","probe":"chopDesignations","min":1}
    ///   {"op":"assert","probe":"treeCountBelow","min":0,"withinSec":30}  // min=시작대비 감소 수
    ///
    /// target 리졸버: "tree"/"vein"/"berry" = 화면중심에서 가장 가까운 해당 엔티티,
    ///   "pawn" = 첫 림, "pawn:이름" = 이름 매칭 림.  맵이 절차생성이라 고정좌표 대신 사용.
    /// </summary>
    public class ReproHarness : MonoBehaviour
    {
        public static bool Enabled = false;
        private string scenarioPath;
        private string reportPath = "G:/ai/_repro_report.json";
        private string shotDir = "G:/ai/_repro_shots";

        [System.Serializable] public class Step
        {
            public string op;
            public string target;
            public string name;
            public string probe;
            public string expect;
            public string pawn;
            public string contains;
            public float x; public float y;
            public float dx; public float dy;   // 타깃 리졸브 후 월드 오프셋
            public float sec;
            public float min;
            public float withinSec;
        }
        [System.Serializable] public class Scenario { public string name; public Step[] steps; }
        [System.Serializable] public class StepResult
        { public int index; public string op; public bool passed; public string detail; }
        [System.Serializable] public class Report
        {
            public string scenario; public bool overallPass;
            public List<StepResult> results = new List<StepResult>();
        }

        private readonly Report report = new Report();
        private Scenario scenario;
        // 직전 시뮬 클릭의 월드 좌표 — selectedNearClick/activityNearClick 프로브가 참조
        private Vector3 lastLeftClickWorld, lastRightClickWorld;

        public static void EnsureInScene()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string path = null, rep = null, shots = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-repro" && i + 1 < args.Length) { Enabled = true; path = args[i + 1]; }
                if (args[i] == "-repro-report" && i + 1 < args.Length) rep = args[i + 1];
                if (args[i] == "-repro-shotdir" && i + 1 < args.Length) shots = args[i + 1];
            }
            if (!Enabled || path == null) return;
            var go = new GameObject("ReproHarness");
            var h = go.AddComponent<ReproHarness>();
            h.scenarioPath = path;
            if (rep != null) h.reportPath = rep;
            if (shots != null) h.shotDir = shots;
        }

        private void Start()
        {
            // bug-pattern #9 firewall — CLI 실행 시 포커스 잃어도 coroutine 정지 금지.
            Application.runInBackground = true;
            if (!Directory.Exists(shotDir)) Directory.CreateDirectory(shotDir);
            try
            {
                scenario = JsonUtility.FromJson<Scenario>(File.ReadAllText(scenarioPath));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ReproHarness] scenario load FAIL: {scenarioPath} — {e.Message}");
                report.scenario = scenarioPath; report.overallPass = false;
                Finish(); return;
            }
            report.scenario = scenario.name;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Debug.Log($"[ReproHarness] ===== START '{scenario.name}' ({scenario.steps.Length} steps) =====");
            yield return new WaitForSeconds(2.0f);   // 씬 부트스트랩 대기
            SimInput.BeginSim();

            for (int i = 0; i < scenario.steps.Length; i++)
            {
                var s = scenario.steps[i];
                var r = new StepResult { index = i, op = s.op + (s.probe != null ? ":" + s.probe : "") };
                yield return RunStep(s, r);
                report.results.Add(r);
                Debug.Log($"[ReproHarness] step {i} {r.op}: {(r.passed ? "PASS" : "FAIL")} — {r.detail}");
            }

            SimInput.EndSim();
            report.overallPass = report.results.TrueForAll(x => x.passed);
            Finish();
        }

        private IEnumerator RunStep(Step s, StepResult r)
        {
            switch (s.op)
            {
                case "wait":
                    yield return new WaitForSeconds(s.sec);
                    r.passed = true; r.detail = $"waited {s.sec}s"; break;

                case "worldclick":
                case "worldright":
                {
                    // #38 하네스 결함(2026-06-10) — 직전 선택의 카메라 포커스 팬 중에 screen
                    //  좌표를 계산하면, 주입이 소비되는 다음 프레임의 ScreenToWorldPoint 가
                    //  다른 월드점으로 풀려 클릭이 무음 미스됐다(p1-chop-selected-only
                    //  designations=0, 18:00/19:43 런).  카메라가 1프레임 정지할 때까지
                    //  대기(최대 ~2s) 후 리졸브·계산한다.
                    for (int settle = 0; settle < 120; settle++)
                    {
                        Vector3 c0 = Camera.main.transform.position;
                        yield return null;
                        if ((Camera.main.transform.position - c0).sqrMagnitude < 0.0001f) break;
                    }
                    Vector3 world;
                    if (!ResolveWorld(s, out world)) { r.passed = false; r.detail = $"target '{s.target}' not found"; break; }
                    // #38 — 타깃이 화면 가장자리/UI 점유 밴드(하단바 등)에 있으면 overUI 게이트에
                    //  막혀 클릭이 무음 무효화된다(-far designations=0, screen y=77 사례).  실제
                    //  유저처럼 카메라를 타깃으로 즉시 점프(FocusOn) 후 클릭한다.
                    Vector3 pre = Camera.main.WorldToScreenPoint(world);
                    // 기하 밴드(하단바)에 더해 실제 UI 레이캐스트로 차단 검사 — 콜로니스트
                    //  초상화/알림 카드 등 동적 UI 뒤에 타깃이 깔린 케이스(110px-from-top
                    //  무음 미스)는 밴드 추정으로 못 잡는다.  막혀 있으면 화면 중앙으로 점프.
                    if (pre.x < Screen.width * 0.05f || pre.x > Screen.width * 0.95f
                        || pre.y < Screen.height * 0.16f || pre.y > Screen.height * 0.92f
                        || UiBlockedAt(pre))
                    {
                        var cc = Object.FindFirstObjectByType<CameraController>();
                        if (cc != null) { cc.FocusOn(new Vector2(world.x, world.y)); yield return null; }
                    }
                    Vector3 screen = Camera.main.WorldToScreenPoint(world);   // detail 표기용
                    int btn = s.op == "worldclick" ? 0 : 1;
                    if (btn == 0) lastLeftClickWorld = world; else lastRightClickWorld = world;
                    // #38 — 월드좌표 주입: screen 도출을 소비 프레임으로 미뤄 settle 후에도
                    //  남는 잔여 팬(포커스 이징 시작 지연)에 의한 재투영 미스를 원천 차단.
                    SimInput.FrameMouseDownWorld(btn, world);
                    yield return null;                 // 다음 프레임 Update 들이 이 입력을 본다
                    SimInput.ClearFrame();
                    yield return null;
                    // 진단 — 클릭 지점에 뭐가 있었는지 기록 (이동인 줄 알았는데 엔티티 클릭이었던 케이스 가시화)
                    var hitsAt = Physics2D.OverlapPointAll(world);
                    string atWhat = hitsAt.Length == 0 ? "empty"
                        : string.Join("+", System.Array.ConvertAll(hitsAt, h => h.name));
                    r.passed = true;
                    r.detail = $"{(btn == 0 ? "L" : "R")}click world({world.x:F1},{world.y:F1}) screen({screen.x:F0},{screen.y:F0}) at={atWhat}";
                    break;
                }

                case "spawnBed":
                {
                    // #침대도달불가 회귀가드 — 침대를 직접 스폰 (건설 경로 우회, BuildManager
                    //  prefab 재사용).  s.x/s.y = 월드 좌표.
                    if (BuildManager.Instance == null || BuildManager.Instance.BedPrefabRef == null)
                    { r.passed = false; r.detail = "no BuildManager/bedPrefab"; break; }
                    var bedGo = Object.Instantiate(BuildManager.Instance.BedPrefabRef,
                        new Vector3(Mathf.Floor(s.x) + 0.5f, Mathf.Floor(s.y) + 0.5f, 0f), Quaternion.identity);
                    bedGo.SetActive(true);
                    yield return null;
                    r.passed = bedGo.GetComponent<BedEntity>() != null
                               || bedGo.GetComponentInChildren<BedEntity>() != null;
                    r.detail = $"bed spawned @ ({s.x:F1},{s.y:F1})";
                    break;
                }

                case "clearFood":
                {
                    // #생존압박(2026-06-11) — 아사 검증용: 물리 음식원 전부 제거 (고기/간편식
                    //  더미 + 베리 덤불).  섭취 레이스 차단으로 굶주림→사망 경로를 결정화.
                    int removed = 0;
                    foreach (var m in Object.FindObjectsByType<MeatPileEntity>(FindObjectsSortMode.None))
                    { if (m != null) { Object.Destroy(m.gameObject); removed++; } }
                    foreach (var b in Object.FindObjectsByType<BerryBushEntity>(FindObjectsSortMode.None))
                    { if (b != null) { Object.Destroy(b.gameObject); removed++; } }
                    // 작물도 — 수확이 고기더미를 새로 만들어 아사 검증을 오염시킨다.
                    foreach (var c in Object.FindObjectsByType<CropEntity>(FindObjectsSortMode.None))
                    { if (c != null) { Object.Destroy(c.gameObject); removed++; } }
                    // 야생동물도 — 식량<5 자동 사냥이 고기를 공급해 림을 구출한다
                    //  (게임 루프는 정상 — 격리 검증에서만 제거).
                    foreach (var a in Object.FindObjectsByType<AnimalEntity>(FindObjectsSortMode.None))
                    { if (a != null) { Object.Destroy(a.gameObject); removed++; } }
                    yield return null;
                    r.passed = true;
                    r.detail = $"food sources removed: {removed}";
                    break;
                }

                case "setHour":
                {
                    // 게임 시각 강제 (테스트 스캐폴딩) — 자율취침 밤 게이트(h>=22) 등 시간
                    //  조건을 결정적으로 만든다.  s.x = 시(0-23).  일수는 보존.
                    var clk = GameClock.Instance;
                    if (clk == null) { r.passed = false; r.detail = "no GameClock"; break; }
                    float daysBase = Mathf.Floor(clk.GameSeconds / 86400f) * 86400f;
                    clk.SetGameSeconds(daysBase + Mathf.Clamp(s.x, 0f, 23.99f) * 3600f);
                    r.passed = true; r.detail = $"hour={clk.Hour}"; break;
                }

                case "boxselect":
                {
                    // #38 마키 재현 — 운영자의 박스선택 경로.  단일 클릭 선택(ClickSelector.
                    //  currentSelection)과 명령 소유권이 다르다(MarqueeOwnsCommand) — 그 차이가
                    //  실플레이에서만 #38 을 재발시켰으므로 하네스도 이 경로를 타야 한다.
                    Vector3 world;
                    if (!ResolveWorld(s, out world)) { r.passed = false; r.detail = $"target '{s.target}' not found"; break; }
                    var mq = Object.FindFirstObjectByType<MarqueeSelector>();
                    if (mq == null) { r.passed = false; r.detail = "no MarqueeSelector"; break; }
                    int nSel = mq.SimulateBoxSelect(new Vector2(world.x - 0.6f, world.y - 0.6f),
                                                    new Vector2(world.x + 0.6f, world.y + 0.6f));
                    yield return null;
                    r.passed = nSel >= 1;
                    r.detail = $"box-selected {nSel} pawn(s) around ({world.x:F1},{world.y:F1})";
                    break;
                }

                case "key":
                {
                    KeyCode k;
                    if (!System.Enum.TryParse(s.name, true, out k)) { r.passed = false; r.detail = $"bad key '{s.name}'"; break; }
                    SimInput.FrameKeyDown(k);
                    yield return null;
                    SimInput.ClearFrame();
                    r.passed = true; r.detail = $"key {k}"; break;
                }

                case "clickui":
                    r.passed = RealClickUI(s.name, out var uiDetail);
                    r.detail = uiDetail;
                    yield return null; yield return new WaitForSeconds(0.3f);
                    break;

                case "shot":
                {
                    string p = Path.Combine(shotDir, s.name + ".png");
                    ScreenCapture.CaptureScreenshot(p);
                    yield return new WaitForSeconds(0.4f);   // 캡처 flush
                    r.passed = true; r.detail = p; break;
                }

                case "setNeed":
                {
                    // 테스트 스캐폴딩 (검증 대상 아님) — 림 need 를 직접 세팅해 조건을 빠르게
                    //  만든다 (레퍼런스 콜로니심 데브모드와 동일).  이후의 thought 발생/mood 반영은
                    //  게임 시스템(PawnThoughts tick)이 자연 수행 — 검증 체인은 입력경로 그대로.
                    var pn = FindPawn(s.pawn);
                    var nds = pn != null ? pn.GetComponent<PawnNeeds>() : null;
                    if (nds == null) { r.passed = false; r.detail = "no PawnNeeds"; break; }
                    if (s.name == "food") nds.food = s.x;
                    else if (s.name == "sleep") nds.sleep = s.x;
                    else nds.mood = s.x;
                    r.passed = true; r.detail = $"setNeed {s.name}={s.x} ({pn.PawnName})"; break;
                }

                case "speed":
                {
                    // 테스트 스캐폴딩 (검증 대상 아님) — 장시간 sim 을 견딜 수 있게 배속.
                    //  주의: 이후 wait/withinSec 는 '게임 초' 단위가 된다 (WaitForSeconds 는 scaled).
                    if (TimeController.Instance != null) { TimeController.Instance.SetScale(s.x); r.passed = true; }
                    else { Time.timeScale = s.x; r.passed = true; }
                    r.detail = $"timeScale={s.x}"; break;
                }

                case "setWeather":
                {
                    // 테스트 스캐폴딩 (검증 대상 아님) — 폭풍을 즉시 시작/종료 (AIDirector
                    //  storm_warning 이벤트와 동일 상태 전이).  이후 mood 반응은 게임 시스템이 수행.
                    var wc = WeatherController.Instance;
                    if (wc == null) { r.passed = false; r.detail = "no WeatherController"; break; }
                    if (s.name == "storm") wc.ForceStorm(); else wc.ForceClear();
                    r.passed = true; r.detail = $"weather={s.name}"; break;
                }

                case "assert":
                    yield return RunAssert(s, r);
                    break;

                default:
                    r.passed = false; r.detail = $"unknown op '{s.op}'"; break;
            }
        }

        // ── assert 프로브 — 전부 "운영자가 화면에서 보는 것" 기준 ───────────
        private IEnumerator RunAssert(Step s, StepResult r)
        {
            switch (s.probe)
            {
                case "contextMenuOpen":
                {
                    yield return null;
                    bool open = ContextMenuUI.Instance != null && ContextMenuUI.Instance.IsOpen;
                    bool want = s.expect == "true";
                    r.passed = open == want;
                    r.detail = $"contextMenu open={open} (expect {want})"; break;
                }
                case "selection":
                {
                    var sel = Object.FindFirstObjectByType<ClickSelector>()?.CurrentSelection;
                    string got = sel != null ? sel.PawnName : "none";
                    r.passed = s.expect == "any" ? sel != null : got == s.expect;
                    r.detail = $"selection={got} (expect {s.expect})"; break;
                }
                case "pawnMoved":
                {
                    var pawn = FindPawn(s.pawn);
                    if (pawn == null) { r.passed = false; r.detail = $"pawn '{s.pawn}' not found"; break; }
                    Vector3 start = pawn.transform.position;
                    float t = 0, best = 0;
                    while (t < s.withinSec)
                    {
                        yield return new WaitForSeconds(0.25f); t += 0.25f;
                        best = Mathf.Max(best, Vector3.Distance(start, pawn.transform.position));
                        if (best >= s.min) break;
                    }
                    r.passed = best >= s.min;
                    r.detail = $"moved {best:F2} (need ≥{s.min}) in {t:F1}s"; break;
                }
                case "pawnNear":
                {
                    var pawn = FindPawn(s.pawn);
                    Vector3 world;
                    if (pawn == null) { r.passed = false; r.detail = $"pawn '{s.pawn}' not found"; break; }
                    if (!ResolveWorld(s, out world)) { r.passed = false; r.detail = $"target '{s.target}' not found"; break; }
                    float t = 0, best = float.MaxValue;
                    while (t < s.withinSec)
                    {
                        yield return new WaitForSeconds(0.25f); t += 0.25f;
                        best = Mathf.Min(best, Vector3.Distance(world, pawn.transform.position));
                        if (best <= s.min) break;
                    }
                    r.passed = best <= s.min;
                    r.detail = $"closest {best:F2} (need ≤{s.min}) in {t:F1}s"; break;
                }
                case "pawnOnBed":
                {
                    // #침대도달불가(2026-06-11) — '림이 어떤 침대 위에서 자고 있는가' 게임 진실 단언.
                    //  pawnNear+target:"bed" 는 카메라중심 최근접 침대 1개 고정이라, 침대 여러 개
                    //  스폰 시 림이 다른 침대를 잡으면 가짜 FAIL → 림 기준 최근접 침대를 매 샘플
                    //  재계산하고 IsSleeping(실제 수면 진입)까지 함께 단언한다.
                    var pawn = FindPawn(s.pawn);
                    if (pawn == null) { r.passed = false; r.detail = $"pawn '{s.pawn}' not found"; break; }
                    var needsC = pawn.GetComponent<PawnNeeds>();
                    float t = 0, best = float.MaxValue; bool sleeping = false;
                    while (t < s.withinSec)
                    {
                        yield return new WaitForSeconds(0.25f); t += 0.25f;
                        float d = float.MaxValue;
                        foreach (var b in Object.FindObjectsByType<BedEntity>(FindObjectsSortMode.None))
                            if (b != null) d = Mathf.Min(d, Vector3.Distance(b.transform.position, pawn.transform.position));
                        best = Mathf.Min(best, d);
                        sleeping = needsC != null && needsC.IsSleeping;
                        if (d <= s.min && sleeping) break;
                    }
                    r.passed = best <= s.min && sleeping;
                    r.detail = $"nearestBed {best:F2} (need ≤{s.min}) sleeping={sleeping} in {t:F1}s"; break;
                }
                case "pawnHpBelow":
                {
                    // #생존압박(2026-06-11) — 굶주림이 HP 를 깎는가 (아사 경로 게임 진실).
                    var pawn = FindPawn(s.pawn);
                    if (pawn == null) { r.passed = false; r.detail = $"pawn '{s.pawn}' not found"; break; }
                    float t = 0; int hp = pawn.Hp;
                    while (t < s.withinSec && hp > (int)s.min)
                    { yield return new WaitForSeconds(0.25f); t += 0.25f; hp = pawn.Hp; }
                    r.passed = hp <= (int)s.min;
                    r.detail = $"hp={hp} (need ≤{(int)s.min}) in {t:F0}s(scaled)"; break;
                }
                case "pawnActivity":
                {
                    var pawn = FindPawn(s.pawn);
                    if (pawn == null) { r.passed = false; r.detail = $"pawn '{s.pawn}' not found"; break; }
                    var label = pawn.GetComponentInChildren<PawnNameLabel>();
                    string last = ""; float t = 0;
                    while (t < s.withinSec)
                    {
                        last = label != null ? label.CurrentActivity : "<no label>";
                        if (last.Contains(s.contains)) break;
                        yield return new WaitForSeconds(0.25f); t += 0.25f;
                    }
                    r.passed = last.Contains(s.contains);
                    r.detail = $"activity='{last}' (want contains '{s.contains}') in {t:F1}s"; break;
                }
                case "chopDesignations":
                {
                    yield return null;
                    int n = TreeChopDesignation.Instance != null
                        ? TreeChopDesignation.Instance.GetMarkedTreePositions().Count : 0;
                    r.passed = n >= (int)s.min;
                    r.detail = $"chop designations={n} (need ≥{(int)s.min})"; break;
                }
                case "treeCountBelow":
                {
                    int start = Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None).Length;
                    float t = 0; int now = start;
                    while (t < s.withinSec)
                    {
                        yield return new WaitForSeconds(0.5f); t += 0.5f;
                        now = Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None).Length;
                        if (start - now >= s.min) break;
                    }
                    r.passed = start - now >= s.min;
                    r.detail = $"trees {start}→{now} (need -{(int)s.min}) in {t:F1}s"; break;
                }
                case "selectedNearClick":
                {
                    // 운영자 P0 "림 기본 이동" — 선택 림이 직전 우클릭 지점에 도달하는가.
                    var sel = Object.FindFirstObjectByType<ClickSelector>()?.CurrentSelection;
                    if (sel == null) { r.passed = false; r.detail = "no selection"; break; }
                    float t = 0, best = float.MaxValue;
                    while (t < s.withinSec)
                    {
                        yield return new WaitForSeconds(0.25f); t += 0.25f;
                        best = Mathf.Min(best, Vector3.Distance(lastRightClickWorld, sel.transform.position));
                        if (best <= s.min) break;
                    }
                    r.passed = best <= s.min;
                    r.detail = $"selected '{sel.PawnName}' closest {best:F2} to rclick({lastRightClickWorld.x:F1},{lastRightClickWorld.y:F1}) (need ≤{s.min}) in {t:F1}s";
                    break;
                }
                case "anyPawnActivity":
                {
                    // 아무 림이나 머리위 라벨에 contains 가 뜨는가 (배정 자체의 확인).
                    float t = 0; string who = "", last = "";
                    while (t < s.withinSec)
                    {
                        foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
                        {
                            var lbl = p.GetComponentInChildren<PawnNameLabel>();
                            if (lbl == null) continue;
                            last = lbl.CurrentActivity;
                            if (last.Contains(s.contains)) { who = p.PawnName; break; }
                        }
                        if (who != "") break;
                        yield return new WaitForSeconds(0.25f); t += 0.25f;
                    }
                    r.passed = who != "";
                    r.detail = who != "" ? $"'{who}' activity contains '{s.contains}' at {t:F1}s"
                                         : $"no pawn activity contains '{s.contains}' in {t:F1}s";
                    break;
                }
                case "activityNearClick":
                {
                    // 운영자 P0 "원거리 벌목" — 라벨이 contains(예: 벌목)인 림이 그 순간
                    //  직전 우클릭 지점(나무)에서 s.min 이내에 있는가.  작업 라벨이 떠 있는데
                    //  내내 멀리 있으면 FAIL = 제자리 벌목 재현.
                    float t = 0, bestWhileActive = float.MaxValue; bool seenActive = false;
                    while (t < s.withinSec)
                    {
                        foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
                        {
                            var lbl = p.GetComponentInChildren<PawnNameLabel>();
                            if (lbl == null || !lbl.CurrentActivity.Contains(s.contains)) continue;
                            seenActive = true;
                            float d = Vector3.Distance(lastRightClickWorld, p.transform.position);
                            bestWhileActive = Mathf.Min(bestWhileActive, d);
                        }
                        if (bestWhileActive <= s.min) break;
                        yield return new WaitForSeconds(0.25f); t += 0.25f;
                    }
                    r.passed = seenActive && bestWhileActive <= s.min;
                    r.detail = !seenActive
                        ? $"no pawn ever showed '{s.contains}' in {t:F1}s"
                        : $"closest-while-'{s.contains}' {bestWhileActive:F2} to rclick (need ≤{s.min}) in {t:F1}s";
                    break;
                }
                case "needBelow":
                {
                    // s.name = food|sleep|mood, s.min = 임계값.  내려갈 때까지 폴링.
                    var pawn = FindPawn(s.pawn);
                    var nd = pawn != null ? pawn.GetComponent<PawnNeeds>() : null;
                    if (nd == null) { r.passed = false; r.detail = "no PawnNeeds"; break; }
                    float t = 0, v = GetNeed(nd, s.name);
                    while (t < s.withinSec && v > s.min)
                    { yield return new WaitForSeconds(0.25f); t += 0.25f; v = GetNeed(nd, s.name); }
                    r.passed = v <= s.min;
                    r.detail = $"{s.name}={v:F1} (need ≤{s.min}) in {t:F0}s(scaled)"; break;
                }
                case "needDrops":
                {
                    // 시작값 대비 s.min 이상 하락하는가 — '기분이 나빠지긴 하는가' 류 end-to-end.
                    var pawn = FindPawn(s.pawn);
                    var nd = pawn != null ? pawn.GetComponent<PawnNeeds>() : null;
                    if (nd == null) { r.passed = false; r.detail = "no PawnNeeds"; break; }
                    float start = GetNeed(nd, s.name), t = 0, v = start;
                    while (t < s.withinSec && start - v < s.min)
                    { yield return new WaitForSeconds(0.25f); t += 0.25f; v = GetNeed(nd, s.name); }
                    r.passed = start - v >= s.min;
                    r.detail = $"{s.name} {start:F1}→{v:F1} (drop {start - v:F1}, need ≥{s.min}) in {t:F0}s(scaled)"; break;
                }
                case "hasThought":
                {
                    var pawn = FindPawn(s.pawn);
                    var th = pawn != null ? pawn.GetComponent<PawnThoughts>() : null;
                    if (th == null) { r.passed = false; r.detail = "no PawnThoughts"; break; }
                    float t = 0; bool found2 = false; string labels = "";
                    while (t < s.withinSec && !found2)
                    {
                        labels = "";
                        foreach (var thought in th.active)
                        {
                            labels += thought.label + ",";
                            if (thought.label.Contains(s.contains)) { found2 = true; break; }
                        }
                        if (!found2) { yield return new WaitForSeconds(0.25f); t += 0.25f; }
                    }
                    r.passed = found2;
                    r.detail = found2 ? $"thought '{s.contains}' present at {t:F0}s" : $"no '{s.contains}' in {t:F0}s (active: {labels})";
                    break;
                }
                case "pileDurabilityDrops":
                {
                    // 운영자 "통나무 내구도만 닳아 사라지게 — 안 됐다" 재현용.
                    //  옥외(!InStockpile) 더미만 닳는 설계 — 옥외 최다수량 더미(시작 50-더미)를 추적.
                    //  s.min = 요구 하락폭.  더미 Destroy 는 내구도가 거의 소진된 상태였을 때만
                    //  '끝까지 닳음' PASS — 높은 내구도에서 소멸하면 운반/병합 의심으로 FAIL.
                    WoodPileEntity tgt = null;
                    foreach (var w in FindObjectsOfType<WoodPileEntity>())
                        if (!w.InStockpile && (tgt == null || w.Wood > tgt.Wood)) tgt = w;
                    if (tgt == null) { r.passed = false; r.detail = "옥외 WoodPile 없음"; break; }
                    float start = tgt.Durability, t = 0, lastSeen = start;
                    while (t < s.withinSec)
                    {
                        if (tgt == null) break;
                        lastSeen = tgt.Durability;
                        if (start - lastSeen >= s.min) break;
                        yield return new WaitForSeconds(0.5f); t += 0.5f;
                    }
                    if (tgt == null)
                    {
                        bool wornOut = lastSeen < 15f;
                        r.passed = wornOut || start - lastSeen >= s.min;
                        r.detail = $"pile destroyed at {t:F0}s(scaled), durability {start:F1}→{lastSeen:F1}"
                                   + (wornOut ? " (소진 소멸)" : " (조기 소멸 — 운반/병합 의심)");
                    }
                    else
                    {
                        r.passed = start - lastSeen >= s.min;
                        r.detail = $"durability {start:F1}→{lastSeen:F1} (drop {start - lastSeen:F1}, need ≥{s.min}) in {t:F0}s(scaled)";
                    }
                    break;
                }
                case "selectedOnlyActivity":
                {
                    // #38 "선택 림만 벌목" 회귀가드 — withinSec 전 구간 감시: 선택 림은 s.contains
                    //  활동을 보여야 하고(언젠가), 다른 림이 그 활동을 보이면 즉시 FAIL
                    //  (우클릭 명령 = 선택 림 전용 배타 예약, f29b10f).  조기 PASS 없음 — 윈도
                    //  전체를 봐야 "타 림이 늦게 합류"하는 회귀도 잡는다.
                    var selPawn = ResolveSelectedPawn();
                    if (selPawn == null) { r.passed = false; r.detail = "선택 림 없음"; break; }
                    string selName = selPawn.PawnName;
                    bool selDid = false; string offender = "";
                    float t = 0;
                    while (t < s.withinSec && offender == "")
                    {
                        foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
                        {
                            var lbl = p.GetComponentInChildren<PawnNameLabel>();
                            if (lbl == null || lbl.CurrentActivity == null
                                || !lbl.CurrentActivity.Contains(s.contains)) continue;
                            if (p.PawnName == selName) selDid = true;
                            else offender = p.PawnName;
                        }
                        yield return new WaitForSeconds(0.25f); t += 0.25f;
                    }
                    r.passed = selDid && offender == "";
                    r.detail = offender != ""
                        ? $"위반: 비선택 림 '{offender}' 도 '{s.contains}' (선택={selName}, {t:F1}s)"
                        : selDid ? $"선택 림 '{selName}' 만 '{s.contains}' ({t:F1}s 전구간 감시)"
                                 : $"선택 림 '{selName}' 의 '{s.contains}' 미관측 in {t:F1}s";
                    break;
                }
                case "selectedChopAssigned":
                {
                    // #38 즉시-인과 probe — 우클릭 직접명령은 클릭 프레임에 동기 배정되고,
                    //  자율 race 는 최소 다음 Decide 틱(1.5s, PawnUtilityAI)이다.  1s 내
                    //  '선택 림이 벌목 task 를 쥐었는가'로 명령 발행 주체를 결정적으로 구분 —
                    //  결과 라벨만 보던 selectedOnlyActivity 의 race-운 가짜 PASS 를 인과로 차단.
                    var selC = ResolveSelectedPawn();
                    if (selC == null) { r.passed = false; r.detail = "선택 림 없음"; break; }
                    var chp = selC.GetComponent<PawnChopper>();
                    float win = s.withinSec > 0 ? s.withinSec : 1.0f;
                    float tc = 0; bool got = false;
                    while (tc < win)
                    {
                        if (chp != null && chp.HasTask) { got = true; break; }
                        yield return new WaitForSeconds(0.1f); tc += 0.1f;
                    }
                    r.passed = got;
                    r.detail = got
                        ? $"선택 림 '{selC.PawnName}' 벌목 task 보유 ({tc:F1}s — 직접명령 인과 확인)"
                        : $"선택 림 '{selC.PawnName}' 벌목 task 미보유 in {win:F1}s — 직접명령 미발행";
                    break;
                }

                case "needDropsAtMost":
                {
                    // 상한 가드 — withinSec(스케일초) 동안 s.name 하락폭이 s.min "미만"이어야 PASS.
                    //  (폭풍 직접드레인 같은 폭주 페널티의 회귀가드.  하락폭이 min 에 도달하면
                    //  남은 시간을 기다릴 필요 없이 즉시 FAIL 확정.)
                    var pawn = FindPawn(s.pawn);
                    var nd = pawn != null ? pawn.GetComponent<PawnNeeds>() : null;
                    if (nd == null) { r.passed = false; r.detail = "no PawnNeeds"; break; }
                    float start = GetNeed(nd, s.name), t = 0, v = start;
                    while (t < s.withinSec && start - v < s.min)
                    { yield return new WaitForSeconds(0.25f); t += 0.25f; v = GetNeed(nd, s.name); }
                    r.passed = start - v < s.min;
                    r.detail = $"{s.name} {start:F1}→{v:F1} (drop {start - v:F1}, 허용 <{s.min}) in {t:F0}s(scaled)"; break;
                }
                default:
                    r.passed = false; r.detail = $"unknown probe '{s.probe}'"; break;
            }
        }

        private static float GetNeed(PawnNeeds nd, string name)
            => name == "food" ? nd.food : name == "sleep" ? nd.sleep : nd.mood;

        /// <summary>주입 클릭 좌표가 UI 그래픽에 막혀 있는지 — SimInput.IsPointerOverUI 와
        ///  동일 의미의 사전 검사 (클릭 전에 가림 여부를 알아야 FocusOn 으로 회피 가능).</summary>
        private static bool UiBlockedAt(Vector2 screen)
        {
            if (UnityEngine.EventSystems.EventSystem.current == null) return false;
            var ped = new UnityEngine.EventSystems.PointerEventData(
                UnityEngine.EventSystems.EventSystem.current) { position = screen };
            var res = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(ped, res);
            return res.Count > 0;
        }

        /// <summary>#38 — 단일(ClickSelector)/마키(MarqueeSelector) 선택을 동일 의미로 해석.
        ///  박스선택 시 ClickSelector.CurrentSelection 은 비어 있고 마키가 선택을 소유한다.</summary>
        private PawnEntity ResolveSelectedPawn()
        {
            var selector = Object.FindFirstObjectByType<ClickSelector>();
            var sp = selector != null ? selector.CurrentSelection : null;
            if (sp == null)
            {
                var mq = Object.FindFirstObjectByType<MarqueeSelector>();
                if (mq != null && mq.HasMultiSelection) sp = mq.CurrentMultiSelection[0];
            }
            return sp;
        }

        // ── target 리졸버 — 절차생성 맵이라 고정좌표 대신 엔티티 검색 ────────
        private bool ResolveWorld(Step s, out Vector3 world)
        {
            world = new Vector3(s.x, s.y, 0f);
            if (string.IsNullOrEmpty(s.target)) return true;   // 절대좌표 모드

            Vector3 camC = Camera.main.transform.position; camC.z = 0;
            Component found = null;
            if (s.target == "tree") found = Nearest<TreeEntity>(camC);
            else if (s.target == "vein") found = Nearest<StoneVeinEntity>(camC);
            else if (s.target == "berry") found = Nearest<BerryBushEntity>(camC);
            else if (s.target == "bed") found = Nearest<BedEntity>(camC);   // #침대도달불가 가드용
            else if (s.target == "pawn") found = Nearest<PawnEntity>(camC);
            else if (s.target == "pawn:far")
            {
                // #38 적대 조건(2026-06-10) — '나무에서 가장 먼 림'을 선택.  기존 "pawn"(화면중심
                //  최근접)은 자율 디스패치의 최근접 선택과 우연히 일치해 가짜 PASS 를 만들었다
                //  (운영자 실플레이: 민지 선택→서연 배정 FAIL, 하네스: 최근접 선택→PASS).
                //  worldright 가 잡을 나무(화면중심 최근접 tree) 기준 최원거리 림으로 그 우연을 제거.
                var refTree = Nearest<TreeEntity>(camC);
                Vector3 rp = refTree != null ? refTree.transform.position : camC;
                PawnEntity farP = null; float farSq = -1f;
                foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
                {
                    if (p == null || p.IsDead) continue;
                    float d = (p.transform.position - rp).sqrMagnitude;
                    if (d > farSq) { farSq = d; farP = p; }
                }
                found = farP;
            }
            else if (s.target == "@selected")
                found = Object.FindFirstObjectByType<ClickSelector>()?.CurrentSelection;
            else if (s.target == "empty@selected")
            {
                // 선택 림 주변에서 콜라이더 없는 빈 칸 탐색 (dx/dy 무시) — 순수 이동 테스트용.
                var sel2 = Object.FindFirstObjectByType<ClickSelector>()?.CurrentSelection;
                if (sel2 == null) return false;
                Vector3 from = sel2.transform.position;
                for (float rr = 2f; rr <= 4f; rr += 1f)
                    foreach (var dir in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down,
                                                new Vector3(1,1,0)*0.707f, new Vector3(-1,1,0)*0.707f,
                                                new Vector3(1,-1,0)*0.707f, new Vector3(-1,-1,0)*0.707f })
                    {
                        Vector3 cand = from + dir * rr; cand.z = 0;
                        if (Physics2D.OverlapPointAll(cand).Length == 0) { world = cand; return true; }
                    }
                return false;
            }
            else if (s.target.StartsWith("pawn:")) found = FindPawn(s.target.Substring(5));
            if (found == null) return false;
            world = found.transform.position; world.z = 0;
            world += new Vector3(s.dx, s.dy, 0f);   // 오프셋 (예: 선택 림 기준 3칸 오른쪽)
            return true;
        }

        private T Nearest<T>(Vector3 from) where T : Component
        {
            T best = null; float bestSq = float.MaxValue;
            foreach (var e in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                float d = (e.transform.position - from).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = e; }
            }
            return best;
        }

        private PawnEntity FindPawn(string name)
        {
            var pawns = Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
            if (string.IsNullOrEmpty(name)) return pawns.Length > 0 ? pawns[0] : null;
            foreach (var p in pawns) if (p.PawnName == name) return p;
            return null;
        }

        // ── UI 클릭 — ArchitectClickAutoQA 의 검증된 REAL-click 패턴 ─────────
        //  "label:벌목" = 라벨 텍스트 검색, 그 외 = GO 이름 매칭.
        private bool RealClickUI(string spec, out string detail)
        {
            Button targetBtn = null;
            bool byLabel = spec.StartsWith("label:");
            string key = byLabel ? spec.Substring(6) : spec;
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (b == null) continue;
                if (byLabel)
                {
                    var txt = b.GetComponentInChildren<Text>(true);
                    if (txt != null && txt.text.Contains(key)) { targetBtn = b; break; }
                }
                else if (b.gameObject.name == key) { targetBtn = b; break; }
            }
            if (targetBtn == null) { detail = $"button '{spec}' not found"; return false; }
            if (EventSystem.current == null) { detail = "no EventSystem"; return false; }

            var rt = targetBtn.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 center = new Vector2((corners[0].x + corners[2].x) * 0.5f,
                                         (corners[0].y + corners[2].y) * 0.5f);
            var ped = new PointerEventData(EventSystem.current) { position = center };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);
            GameObject topHit = results.Count > 0 ? results[0].gameObject : null;
            bool hit = false;
            foreach (var rr in results)
            {
                if (rr.gameObject == null) continue;
                if (rr.gameObject == targetBtn.gameObject
                    || rr.gameObject.transform.IsChildOf(targetBtn.transform)
                    || targetBtn.transform.IsChildOf(rr.gameObject.transform)) { hit = true; break; }
            }
            if (!hit)
            {
                detail = $"raycast at {center} did NOT reach '{spec}' (topHit={(topHit != null ? topHit.name : "<none>")})";
                return false;
            }
            ped.pointerPress = topHit;
            ped.pointerCurrentRaycast = results[0];
            ped.pressPosition = center;
            ExecuteEvents.ExecuteHierarchy(topHit, ped, ExecuteEvents.pointerClickHandler);
            detail = $"REAL click '{spec}' @ {center}";
            return true;
        }

        private void Finish()
        {
            try { File.WriteAllText(reportPath, JsonUtility.ToJson(report, true)); }
            catch (System.Exception e) { Debug.LogError($"[ReproHarness] report write fail: {e.Message}"); }
            Debug.Log($"[ReproHarness] ===== OVERALL: {(report.overallPass ? "PASS" : "FAIL")} → {reportPath} =====");
            StartCoroutine(QuitSoon());
        }

        private IEnumerator QuitSoon()
        {
            yield return new WaitForSeconds(1.0f);
            Application.Quit();
        }
    }
}
