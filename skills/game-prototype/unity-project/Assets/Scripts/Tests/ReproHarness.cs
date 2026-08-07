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
            public int amount;   // spawnWoodPile 수량 (0 = 기본 20)
            public string kind;  // setWorkPriority 대상 WorkKind
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

            // 전역 난수 시드 고정 (2026-07-29) — **게이트 플레이크의 근본 원인**.
            //  게임플레이 코드가 UnityEngine.Random 을 57곳에서 쓰는데 Random.InitState
            //  호출은 0곳이었다.  유니티는 전역 RNG 를 시스템 엔트로피로 시드하므로
            //  같은 빌드·같은 시나리오를 두 번 돌려도 결과가 달랐다 (실측: 시작 식량이
            //  10 / 0 으로 갈림, p0-pawn-move 가 절반쯤 실패).
            //  p0-pawn-move 의 _note 가 "매 실행 폰 위치·일정이 다르다"로 진단하고
            //  clearStockpiles·타임아웃 완화로 대응했지만, 그건 증상이었다.
            //  ⚠ **재현 모드에서만** 고정한다.  일반 플레이는 매번 달라야 콜로니심답다.
            //  시드는 CLI 로 바꿀 수 있다 (-repro-seed N).  시드 자체에 의미는 없지만,
            //  고정하면 특정 시드가 만드는 상황(예: 더미 병합)에 시나리오가 걸릴 수
            //  있으므로 재빌드 없이 스윕할 수 있어야 한다.
            int seed = 20260729;
            var rargv = System.Environment.GetCommandLineArgs();
            for (int ri = 0; ri < rargv.Length - 1; ri++)
                if (rargv[ri] == "-repro-seed" && int.TryParse(rargv[ri + 1], out int rs)) seed = rs;
            UnityEngine.Random.InitState(seed);
            Debug.Log($"[ReproHarness] Random.InitState({seed})");
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
            // 퀵픽 '일시정지 시작'(2026-06-13) 가드 — 씬이 정지로 시작해도 하네스는
            //  동결되지 않는다: 부트스트랩 대기는 realtime, 시작 직전 1x 강제.
            yield return new WaitForSecondsRealtime(2.0f);
            if (TimeController.Instance != null) TimeController.Instance.SetScale(1f);
            else Time.timeScale = 1f;
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
                    // 일시정지(timeScale=0) 중 scaled wait 는 영원히 안 끝난다 — realtime
                    //  폴백으로 함정 제거 (정지 상태를 N초 '보여주는' 쇼케이스 비트도 가능해짐).
                    if (Time.timeScale == 0f)
                    {
                        Debug.Log($"[ReproHarness] wait {s.sec}s under pause → realtime fallback");
                        yield return new WaitForSecondsRealtime(s.sec);
                    }
                    else
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
                    //  2026-07-30 — 조건부 FocusOn 을 **무조건**으로 바꾼다.
                    //  기존은 "가장자리이거나 UI 에 막혔으면" 카메라를 옮겼는데, 판정 시점과
                    //  클릭 소비 시점 사이에 **알림 토스트가 새로 떠서** 그 자리를 덮으면
                    //  검사는 통과하고 클릭만 먹혔다 — `p2-first10min` 이 청사진 0개로 잡혔고
                    //  게임 로그에 `[Build] CLICK skip: overUI=true` 가 7줄 찍혀 있었다.
                    //  (오늘 토스트를 2줄로 키우면서 우상단 차단 영역이 넓어진 것이 계기다.)
                    //  타깃을 화면 중앙으로 가져오면 UI 위일 수가 없다 — 실제 플레이어도
                    //  뭔가를 클릭하려면 그쪽으로 화면을 옮긴다.  이 한 줄이 "UI 가 클릭을
                    //  먹었다" 계열 플레이크 전체를 없앤다.
                    _ = pre;   // (진단용 좌표 — 판정에는 더 이상 쓰지 않는다)
                    {
                        var cc = Object.FindFirstObjectByType<CameraController>();
                        if (cc != null)
                        {
                            cc.FocusOn(new Vector2(world.x, world.y));
                            yield return null;
                            for (int settle2 = 0; settle2 < 60; settle2++)
                            {
                                Vector3 c1 = Camera.main.transform.position;
                                yield return null;
                                if ((Camera.main.transform.position - c1).sqrMagnitude < 0.0001f) break;
                            }
                        }
                    }
                    // 2026-07-30 — **움직이는 대상은 클릭 직전에 다시 잡는다.**
                    //  `target:"pawn"` 은 정지물이 아니다.  예전에는 콜로니스트가 할 일이 없어
                    //  가만히 서 있었기 때문에 한 번 잡은 좌표가 계속 유효했는데, 시작 캠프가
                    //  생겨 실제로 일하러 걸어다니게 되자 리졸브~클릭 사이(카메라 정착·포커스
                    //  점프로 여러 프레임)에 그 자리를 떠나 **빈 땅을 클릭**하게 됐다
                    //  (`p0-pawn-move` 가 selection=none 으로 잡아냈다).
                    //  게임이 살아난 결과지 회귀가 아니므로, 테스트를 늦추는 게 아니라
                    //  하네스가 사람처럼 "지금 있는 곳"을 다시 보게 한다.
                    //  재리졸브만으론 부족했다 — 주입이 **소비되는 다음 프레임**에도 계속
                    //  움직여 3회 중 2회 실패했다.  그래서 클릭이 소비되는 그 몇 프레임 동안만
                    //  시간을 멈춘다 (`speed` 와 같은 테스트 스캐폴딩).  timeScale=0 에서도
                    //  Update 와 `yield return null` 은 정상 동작하므로 입력은 그대로 흐른다.
                    float savedScale = -1f;
                    bool movingTarget = !string.IsNullOrEmpty(s.target) && s.target.StartsWith("pawn");
                    if (movingTarget)
                    {
                        savedScale = Time.timeScale;
                        Time.timeScale = 0f;
                        yield return null;              // 정지가 반영된 프레임에서 다시 잡는다
                        Vector3 fresh;
                        if (ResolveWorld(s, out fresh)) world = fresh;
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
                    if (savedScale >= 0f) Time.timeScale = savedScale;   // 정지 해제 (움직이는 대상 클릭)
                    // 진단 — 클릭 지점에 뭐가 있었는지 기록 (이동인 줄 알았는데 엔티티 클릭이었던 케이스 가시화)
                    var hitsAt = Physics2D.OverlapPointAll(world);
                    string atWhat = hitsAt.Length == 0 ? "empty"
                        : string.Join("+", System.Array.ConvertAll(hitsAt, h => h.name));
                    r.passed = true;
                    r.detail = $"{(btn == 0 ? "L" : "R")}click world({world.x:F1},{world.y:F1}) screen({screen.x:F0},{screen.y:F0}) at={atWhat}";
                    break;
                }

                // 관측용 목재 더미를 **주민에게서 멀리** 떨어뜨린다 (2026-08-01).
                //  계기: 주민을 3 → 6 인으로 늘리자 `p1-wood-durability` 가 깨졌다 —
                //  바닥 더미를 1초 만에 주워 가서 내구도 감소를 관측할 대상이 사라졌다.
                //  clearStockpiles 만으로는 부족하다(청사진 건설 운반이 여전히 가져간다).
                //  시나리오가 **자기 관측 대상을 스스로 만들게** 한다 — 맵 구석에 놓으면
                //  운반 대상 선택(최근접)에서 한참 밀려 관측 시간이 확보된다.
                //  프로덕션 동작은 건드리지 않는다.
                case "spawnWoodPile":
                {
                    var pos = new Vector3(s.x, s.y, 0f);
                    int amt = s.amount > 0 ? s.amount : 20;
                    var wp = WoodPileEntity.Spawn(pos, amt, WoodPileEntity.EnsureSprite(null));
                    yield return null;
                    r.passed = wp != null;
                    r.detail = wp != null ? $"목재 더미 {amt} @ ({s.x:F1},{s.y:F1})" : "생성 실패";
                    break;
                }
                case "spawnStoneChunk":
                {
                    // 석재 더미 생성 (테스트 스캐폴딩) — s.amount 로 스택 단계가 바뀐다
                    //  (1개 / 5개 / 20개 = 파편 1 → 3 → 5).
                    //
                    // 왜 필요한가: 바닥에 떨어진 석재는 **광맥을 캐야만** 생기므로 시작
                    //  상태에 하나도 없고, 22개 시나리오 중 채광까지 가는 것이 없다.
                    //  2026-08-02 운영자 "석재 … 우리껀 너무 안보여" 로 아트를 전부 다시
                    //  그렸는데 실물로 확인할 경로가 없었다 — 그 공백을 메운다.
                    var spos = new Vector3(s.x, s.y, 0f);
                    int samt = s.amount > 0 ? s.amount : 20;
                    var sc = StoneChunkEntity.Spawn(spos, samt, StoneChunkEntity.EnsureSprite(null));
                    yield return null;
                    r.passed = sc != null;
                    r.detail = sc != null ? $"석재 더미 {samt} @ ({s.x:F1},{s.y:F1})" : "생성 실패";
                    break;
                }

                case "clearStockpiles":
                {
                    // 전제조건 세팅용 (2026-07-27).  시작 저장구역이 생기면서 바닥 자원이
                    //  3초 안에 전부 운반돼, '옥외 더미가 닳는가'를 보는 시나리오가 측정
                    //  대상을 잃었다(옥외 WoodPile 없음).  프로덕션 동작을 되돌리는 대신
                    //  **시나리오가 스스로 조건을 만들게** 한다 — 저장구역이 없는 상태는
                    //  플레이어가 구역을 지우면 실제로 도달하는 정상 게임 상태다.
                    var zones = Object.FindObjectsByType<StockpileZoneEntity>(FindObjectsSortMode.None);
                    int removed = 0;
                    foreach (var z in zones)
                    {
                        if (z == null) continue;
                        Object.Destroy(z.gameObject);
                        removed++;
                    }
                    yield return null;   // Destroy 반영 대기 (haul 예약 해제는 다음 폴에서)
                    r.passed = true;
                    r.detail = $"저장구역 {removed}개 제거";
                    break;
                }

                case "clearChopDesignations":
                {
                    // 전제조건 세팅용 (2026-07-27).  GameManager 가 첫 화면 생동감을 위해
                    //  시작 벌목 6그루를 미리 지정하는데, '선택된 림만 벌목한다'를 보는
                    //  시나리오에서는 **비선택 림이 그 시작 지정을 처리하는 것**이 위반으로
                    //  잡힌다(실측: 지훈이 시작 지정 나무를 벌목 → assert 실패).
                    //  프로덕션 동작을 되돌리는 대신 시나리오가 조건을 만들게 한다 —
                    //  지정이 없는 상태는 플레이어가 지정을 안 하면 도달하는 정상 상태다.
                    var marks = Object.FindObjectsByType<ChopTarget>(FindObjectsSortMode.None);
                    int cleared = 0;
                    foreach (var m in marks)
                    {
                        if (m == null) continue;
                        Object.Destroy(m.gameObject);
                        cleared++;
                    }
                    // 이미 잡을 들고 있는 림의 task 도 함께 해제 (다음 폴에서 새 잡을 고른다).
                    foreach (var pw in Object.FindObjectsByType<PawnChopper>(FindObjectsSortMode.None))
                        if (pw != null) pw.ClearTask();
                    yield return null;
                    r.passed = true;
                    r.detail = $"벌목 지정 {cleared}건 해제";
                    break;
                }

                case "spawnBed":
                {
                    // #침대도달불가 회귀가드 — 침대를 직접 스폰 (건설 경로 우회, BuildManager
                    //  prefab 재사용).  s.x/s.y = 월드 좌표.
                    if (BuildManager.Instance == null || BuildManager.Instance.BedPrefabRef == null)
                    { r.passed = false; r.detail = "no BuildManager/bedPrefab"; break; }
                    // 침대는 1×2 — 칸 중심이 아니라 발자국 중심에 놓는다
                    //  (BuildManager 규약.  칸 중심에 두면 스프라이트가 반 칸 처져
                    //   아래 칸을 침범한다 — 2026-08-01 배치 버그와 동일 원인).
                    var bedSz = BuildManager.SizeFor(BuildManager.Mode.Bed);
                    var bedGo = Object.Instantiate(BuildManager.Instance.BedPrefabRef,
                        new Vector3(Mathf.Floor(s.x) + bedSz.x * 0.5f,
                                    Mathf.Floor(s.y) + bedSz.y * 0.5f, 0f), Quaternion.identity);
                    bedGo.SetActive(true);
                    yield return null;
                    r.passed = bedGo.GetComponent<BedEntity>() != null
                               || bedGo.GetComponentInChildren<BedEntity>() != null;
                    r.detail = $"bed spawned @ ({s.x:F1},{s.y:F1})";
                    break;
                }

                // 전 주민의 특정 작업 우선순위를 일괄 설정 (격리 검증용, 2026-08-01).
                //  s.kind = WorkKind 이름, s.x = 0(비활성)~4.
                //  왜 필요한가: 주민이 6인이 되면서 **다른 주민의 정상 행동이 검증 대상을
                //  구출**하는 일이 생겼다 — 의사가 굶주리는 주민을 치료해 HP 가 오히려
                //  올라갔다.  게임 로직은 정상이므로 프로덕션을 바꾸지 않고,
                //  시나리오가 격리 조건을 만든다.
                case "setWorkPriority":
                {
                    if (!System.Enum.TryParse<WorkKind>(s.kind, out var wk))
                    { r.passed = false; r.detail = $"WorkKind '{s.kind}' 없음"; break; }
                    int n = 0;
                    foreach (var ws in Object.FindObjectsByType<PawnWorkSettings>(FindObjectsSortMode.None))
                    { if (ws != null) { ws.SetPriority(wk, Mathf.Clamp((int)s.x, 0, 4)); n++; } }
                    yield return null;
                    r.passed = n > 0; r.detail = $"{s.kind} 우선순위 {(int)s.x} → {n}명";
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
                    // 저장 카운터도 비운다 (2026-08-01).  물리 음식원만 지우면 창고에
                    //  적립된 식량/식사가 남아 **요리 → 섭취**로 아사를 막는다.
                    //  주민이 3 → 6 인이 되면서 요리가 훨씬 자주 돌아 이 경로가 드러났다:
                    //  HP 가 30 에서 멈추고 더 안 떨어졌다(임계 28).  게이트에서만 실패하는
                    //  flaky 의 진짜 원인이었다 — 개별 실행은 요리 타이밍이 우연히 안 맞았을 뿐.
                    var rm = ResourceManager.Instance;
                    if (rm != null) { rm.food = 0; rm.meals = 0; rm.fineMeals = 0; }
                    yield return null;
                    r.passed = true;
                    r.detail = $"food sources removed: {removed} (+저장 카운터 0)";
                    break;
                }

                case "roofrect":
                {
                    // 지붕 즉시 시공 (테스트 스캐폴딩) — s.x,s.y = 좌하단 셀,
                    //  s.dx,s.dy = 폭/높이(칸).  지정 후 노동을 한 번에 적립해 완공시킨다.
                    //
                    // 왜 필요한가: 지붕은 **플레이어가 지정해야만** 생기므로 시작 상태에는
                    //  단 한 칸도 없다.  그래서 지붕에 딸린 것들(실내 그늘 오버레이,
                    //  2026-08-02 추가한 건물 그림자, 비/온도 훅)이 자동 검증 대상에서
                    //  통째로 빠져 있었다 — 22개 시나리오 중 지붕을 만드는 것이 하나도 없다.
                    var rdz = RoofDesignation.Instance;
                    if (rdz == null) { r.passed = false; r.detail = "no RoofDesignation"; break; }
                    int w = Mathf.Max(1, Mathf.RoundToInt(s.dx));
                    int h = Mathf.Max(1, Mathf.RoundToInt(s.dy));
                    int x0 = Mathf.RoundToInt(s.x), y0 = Mathf.RoundToInt(s.y);
                    int made = 0;
                    for (int cy2 = y0; cy2 < y0 + h; cy2++)
                        for (int cx2 = x0; cx2 < x0 + w; cx2++)
                        {
                            var c = new Vector2Int(cx2, cy2);
                            rdz.DesignateCell(c);
                            rdz.TickRoofWork(c, 9999f, "harness");   // 노동 한 번에 적립 = 완공
                            if (rdz.IsRoofed(c)) made++;
                        }
                    r.passed = made == w * h;
                    r.detail = $"roofed {made}/{w * h}";
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
                    // UI 정착 대기 — UI 는 timeScale 무관하게 프레임 단위로 돌므로 realtime.
                    //  (일시정지 중 메뉴 클릭도 하네스가 동결되지 않게.)
                    yield return null; yield return new WaitForSecondsRealtime(0.3f);
                    break;

                case "shot":
                {
                    string p = Path.Combine(shotDir, s.name + ".png");
                    ScreenCapture.CaptureScreenshot(p);
                    // 사람-리듬 쇼케이스(2026-06-12) — 일시정지(timeScale=0) 중 샷에서
                    //  scaled flush 가 영원히 안 끝나 하네스가 동결됐다.  캡처 flush 는
                    //  실시간 I/O 이므로 realtime 이 맞다.
                    yield return new WaitForSecondsRealtime(0.4f);
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

                case "autoworkOff":
                {
                    // 테스트 스캐폴딩 — '한가한 콜로니' 를 전제하는 시나리오용.
                    ColonyAutoWork.Suspended = true;
                    r.passed = true; r.detail = "자동 일감 정지"; break;
                }

                case "directorOff":
                {
                    // 테스트 스캐폴딩 — 디렉터 사건을 멈춰 **측정 대상만 남긴다.**
                    //  기분처럼 여러 입력의 합인 값을 볼 때, 무작위 사건이 끼면
                    //  같은 조건에서 결과가 뒤집힌다(실측 1/3 실패).
                    AIDirector.EventsSuspended = true;
                    r.passed = true; r.detail = "디렉터 사건 정지"; break;
                }

                case "speed":
                {
                    // 테스트 스캐폴딩 (검증 대상 아님) — 장시간 sim 을 견딜 수 있게 배속.
                    //  주의: 이후 wait/withinSec 는 '게임 초' 단위가 된다 (WaitForSeconds 는 scaled).
                    if (TimeController.Instance != null) { TimeController.Instance.SetScale(s.x); r.passed = true; }
                    else { Time.timeScale = s.x; r.passed = true; }
                    r.detail = $"timeScale={s.x}"; break;
                }

                // 2026-07-31 — 카메라 줌 (테스트 스캐폴딩, `speed` 와 같은 성격).
                //  계기: 제출 영상에서 콜로니스트가 너무 작아 "무엇을 하는지" 가 안 읽혔다.
                //  게임의 기본 줌(ortho 15)은 운영자가 레퍼런스와 대조해 정한 값이라
                //  건드리지 않는다 — **영상의 화면 구성만** 시나리오가 정한다.
                //  플레이어도 보고 싶은 것이 있으면 휠로 당긴다.
                case "ortho":
                {
                    var cam = Camera.main;
                    if (cam == null) { r.passed = false; r.detail = "no camera"; break; }
                    cam.orthographicSize = Mathf.Clamp(s.x, 4f, 32f);
                    yield return null;
                    r.passed = true; r.detail = $"ortho={cam.orthographicSize:F1}";
                    break;
                }
                // 카메라를 특정 좌표(또는 대상)로 옮긴다 — **촬영 대본이 화면 구성을
                //  통제할 수 있게** (2026-07-31).
                //  계기: `worldright` 는 클릭 전에 카메라를 대상으로 FocusOn 하는데,
                //  지정을 연달아 하면 카메라가 그때마다 따라가 **누적으로 밀린다**.
                //  실측 영상 26초 지점에서 정착지가 화면 왼쪽 밖으로 나가 있었다.
                //  되돌릴 수단이 없어서 대본이 카메라를 포기하고 있었다.
                //  x/y 절대좌표 또는 target(tree/pawn/...)을 받는다.
                case "focus":
                {
                    var cc2 = Object.FindFirstObjectByType<CameraController>();
                    if (cc2 == null) { r.passed = false; r.detail = "CameraController 없음"; break; }
                    Vector3 fw;
                    if (!ResolveWorld(s, out fw))
                    { r.passed = false; r.detail = $"target '{s.target}' not found"; break; }
                    cc2.FocusOn(new Vector2(fw.x, fw.y));
                    // 팬이 멎을 때까지 (최대 ~2s) — 다음 스텝이 흔들리는 화면에서 시작하지 않게.
                    for (int i = 0; i < 120; i++)
                    {
                        Vector3 c0 = Camera.main.transform.position;
                        yield return null;
                        if ((Camera.main.transform.position - c0).sqrMagnitude < 0.0001f) break;
                    }
                    r.passed = true; r.detail = $"focus ({fw.x:F1},{fw.y:F1})";
                    break;
                }
                // 습격을 즉시 발생시킨다 — 시연 영상에 위기 장면을 넣기 위한 통로.
                //  실제 플레이와 같은 AIDirector.SpawnRaid 를 탄다(연출 위조 아님).
                case "raid":
                {
                    var dir = Object.FindFirstObjectByType<AIDirector>();
                    if (dir == null) { r.passed = false; r.detail = "AIDirector 없음"; break; }
                    dir.TriggerRaidNow();
                    yield return null;
                    int n = Object.FindObjectsByType<BanditEnemy>(FindObjectsSortMode.None).Length;
                    r.passed = n > 0; r.detail = $"습격 발생 — 밴딧 {n}";
                    break;
                }
                case "camdirector":
                {
                    // 테스트 스캐폴딩 — 무인 런 카메라 디렉터 토글 (x=1 켜기 / 0 끄기).
                    //  명령 구간에선 꺼 둘 것: worldclick 재투영이 팔로우 팬 중 흔들린다.
                    var cd = CameraDirector.EnsureInScene();
                    cd.SetActive(s.x > 0.5f);
                    r.passed = true; r.detail = $"camdirector={(s.x > 0.5f)}"; break;
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
                // 2026-07-30 — 정착 목표(ColonyObjectives) 검증용 프로브 2종.
                //  ① 화면에 그 문구가 실제로 있는가 (패널이 조용히 안 붙는 사고 방지 —
                //     1차 구현이 RuntimeInitializeOnLoadMethod 를 첫 씬에서만 실행해
                //     로그도 예외도 없이 미부착이었다)
                //  ② 달성 판정이 실제로 도는가 (굳어 있으면 목표는 장식이다)
                case "uiTextContains":
                {
                    float t2 = 0; bool found3 = false; string sample = "";
                    while (t2 < Mathf.Max(1f, s.withinSec) && !found3)
                    {
                        foreach (var txt in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None))
                        {
                            if (txt == null || string.IsNullOrEmpty(txt.text)) continue;
                            if (!txt.gameObject.activeInHierarchy) continue;
                            if (txt.text.Contains(s.contains)) { found3 = true; sample = txt.text; break; }
                        }
                        if (!found3) { yield return new WaitForSecondsRealtime(0.25f); t2 += 0.25f; }
                    }
                    r.passed = found3;
                    r.detail = found3 ? $"화면에서 '{s.contains}' 발견 — \"{sample}\""
                                      : $"화면에 '{s.contains}' 없음 in {t2:F1}s";
                    break;
                }
                //  ⑥ 동물이 실제로 이동하는가 (2026-07-31 운영자 "동물들 왜 그냥 서있기만해?").
                //     추측하지 않고 **위치를 두 번 재서 차분**으로 판정한다.  코드에 wander
                //     루프가 있다는 것과 화면에서 움직인다는 것은 다른 주장이다.
                case "animalsMoved":
                {
                    var first = new System.Collections.Generic.Dictionary<int, Vector2>();
                    foreach (var a in Object.FindObjectsByType<AnimalEntity>(FindObjectsSortMode.None))
                        if (a != null && !a.IsDead) first[a.GetInstanceID()] = a.transform.position;
                    float t6 = 0, window = Mathf.Max(1f, s.withinSec);
                    float moved = 0f; int movedCount = 0;
                    while (t6 < window)
                    {
                        yield return new WaitForSecondsRealtime(0.5f); t6 += 0.5f;
                        moved = 0f; movedCount = 0;
                        foreach (var a in Object.FindObjectsByType<AnimalEntity>(FindObjectsSortMode.None))
                        {
                            if (a == null || a.IsDead) continue;
                            if (!first.TryGetValue(a.GetInstanceID(), out var p0)) continue;
                            float d = Vector2.Distance(p0, a.transform.position);
                            if (d > moved) moved = d;
                            if (d > 0.5f) movedCount++;
                        }
                        if (movedCount >= (int)s.min) break;
                    }
                    r.passed = movedCount >= (int)s.min;
                    r.detail = $"0.5칸 이상 이동한 동물 {movedCount}마리 (최대 이동 {moved:F2}칸, "
                             + $"관측 {first.Count}마리, need ≥{(int)s.min}) in {t6:F1}s";
                    break;
                }
                //  ⑤ 호감도 최대값 — 잡담이 실제로 일어났는지의 증거 (G1 사회).
                //     thought 만 검사하면 부족하다: 감정은 붙었는데 관계가 안 쌓이면
                //     '살아있음의 증거'가 아니라 일회성 연출이다.
                case "socialOpinion":
                {
                    float t5 = 0; int best3 = 0;
                    while (t5 < Mathf.Max(1f, s.withinSec))
                    {
                        foreach (var so in Object.FindObjectsByType<PawnSocial>(FindObjectsSortMode.None))
                        {
                            if (so == null) continue;
                            if (so.TryGetBestFriend(out _, out int v) && v > best3) best3 = v;
                        }
                        if (best3 >= (int)s.min) break;
                        yield return new WaitForSecondsRealtime(0.5f); t5 += 0.5f;
                    }
                    r.passed = best3 >= (int)s.min;
                    r.detail = $"최대 호감도 {best3} (need ≥{(int)s.min}) in {t5:F1}s";
                    break;
                }
                //  ④ 무장한 콜로니스트 수 — '무기 제작' 루프가 실제로 도는지의 유일한 증거.
                //     맨손("주먹")은 무장으로 세지 않는다.  이름이 아니라 **효과**로 판정한다
                //     (카탈로그 이름이 바뀌어도 검사가 조용히 통과하지 않게).
                case "armedPawns":
                {
                    float t4 = 0; int best2 = 0;
                    while (t4 < Mathf.Max(1f, s.withinSec))
                    {
                        int armed = 0;
                        foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
                        {
                            if (p == null || p.IsDead) continue;
                            var eq = p.GetComponent<PawnEquipment>();
                            var w = eq != null ? eq.GetEquipped(PawnEquipment.Slot.Weapon) : null;
                            if (w != null && (w.rangedEnabled || w.meleeDamageAdd > 0f)) armed++;
                        }
                        if (armed > best2) best2 = armed;
                        if (best2 >= (int)s.min) break;
                        yield return new WaitForSecondsRealtime(0.5f); t4 += 0.5f;
                    }
                    r.passed = best2 >= (int)s.min;
                    r.detail = $"무장 {best2}명 (need ≥{(int)s.min}) in {t4:F1}s";
                    break;
                }
                //  ③ 콜로니가 살아 있는가 — 전멸하면 승리 경로 자체가 사라진다.
                case "pawnsAlive":
                {
                    int alive = 0, total = 0;
                    foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
                    {
                        if (p == null) continue;
                        total++;
                        if (!p.IsDead) alive++;
                    }
                    r.passed = alive >= (int)s.min;
                    r.detail = $"생존 {alive}/{total} (need ≥{(int)s.min})";
                    break;
                }
                case "objectivesDone":
                {
                    float t3 = 0; int best = -1;
                    while (t3 < Mathf.Max(1f, s.withinSec))
                    {
                        var co = ColonyObjectives.Instance;
                        int n = co != null ? co.CompletedCount : -1;
                        if (n > best) best = n;
                        if (n >= (int)s.min) break;
                        yield return new WaitForSecondsRealtime(0.25f); t3 += 0.25f;
                    }
                    r.passed = best >= (int)s.min;
                    r.detail = best < 0 ? "ColonyObjectives 없음"
                                        : $"목표 달성 {best} (need ≥{(int)s.min}) in {t3:F1}s";
                    break;
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
                        // 2026-07-30 — `contains` 에 '|' 로 **대안**을 쓸 수 있게 확장.
                        //  계기: 시작 캠프에 화덕이 생기면서 콜로니스트가 날것 대신 요리한
                        //  음식을 먹게 됐고, thought 이 '배부름' → '최고의 식사' 로 바뀌었다.
                        //  `p1-mood-negative` 는 '배부름' 을 박아 두고 있어서 **게임이
                        //  좋아진 것을 실패로 보고**했다.  어서션의 의도는 "먹었는가"이지
                        //  "어떤 경로로 먹었는가"가 아니므로 의도를 그대로 표현할 수단을 준다.
                        //  '|' 가 없으면 기존과 완전히 동일하게 동작한다(상위 호환).
                        string[] alts = s.contains != null ? s.contains.Split('|') : new string[0];
                        foreach (var thought in th.active)
                        {
                            labels += thought.label + ",";
                            foreach (var alt in alts)
                            {
                                if (alt.Length > 0 && thought.label.Contains(alt)) { found2 = true; break; }
                            }
                            if (found2) break;
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
                    var selSet2 = ResolveSelectedSet();
                    if (selSet2.Count == 0) { r.passed = false; r.detail = "선택 주민 없음"; break; }
                    var selNames = new System.Collections.Generic.HashSet<string>();
                    foreach (var sm in selSet2) selNames.Add(sm.PawnName);
                    string selName = string.Join(",", selNames);
                    bool selDid = false; string offender = "";
                    // 2026-07-30 — 실패 메시지가 "미관측"만 말해서 **무엇이 관측됐는지**를
                    //  알 수 없었다.  선택 림이 실제로 무슨 활동을 했는지 모으면 실패가
                    //  스스로를 설명한다 (기대와 실제의 차이가 곧 원인이다).
                    var seen = new System.Collections.Generic.HashSet<string>();
                    float t = 0;
                    while (t < s.withinSec && offender == "")
                    {
                        foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
                        {
                            var lbl = p.GetComponentInChildren<PawnNameLabel>();
                            if (lbl == null || lbl.CurrentActivity == null) continue;
                            if (selNames.Contains(p.PawnName))
                                seen.Add(lbl.CurrentActivity);
                            if (!lbl.CurrentActivity.Contains(s.contains)) continue;
                            if (selNames.Contains(p.PawnName)) selDid = true;
                            else offender = p.PawnName;
                        }
                        yield return new WaitForSeconds(0.25f); t += 0.25f;
                    }
                    string seenStr = seen.Count > 0 ? string.Join("/", seen) : "(없음)";
                    r.passed = selDid && offender == "";
                    r.detail = offender != ""
                        ? $"위반: 비선택 주민 '{offender}' 도 '{s.contains}' (선택={selName}, {t:F1}s)"
                        : selDid ? $"선택 주민 '{selName}' 만 '{s.contains}' ({t:F1}s 전구간 감시)"
                                 : $"선택 주민 '{selName}' 의 '{s.contains}' 미관측 in {t:F1}s — 실제 관측 활동: {seenStr}";
                    break;
                }
                case "selectedChopAssigned":
                {
                    // #38 즉시-인과 probe — 우클릭 직접명령은 클릭 프레임에 동기 배정되고,
                    //  자율 race 는 최소 다음 Decide 틱(1.5s, PawnUtilityAI)이다.  1s 내
                    //  '선택 림이 벌목 task 를 쥐었는가'로 명령 발행 주체를 결정적으로 구분 —
                    //  결과 라벨만 보던 selectedOnlyActivity 의 race-운 가짜 PASS 를 인과로 차단.
                    var selSet = ResolveSelectedSet();
                    if (selSet.Count == 0) { r.passed = false; r.detail = "선택 주민 없음"; break; }
                    float win = s.withinSec > 0 ? s.withinSec : 1.0f;
                    float tc = 0; bool got = false;
                    while (tc < win)
                    {
                        foreach (var sm in selSet)
                        {
                            var c2 = sm != null ? sm.GetComponent<PawnChopper>() : null;
                            if (c2 != null && c2.HasTask) { got = true; break; }
                        }
                        if (got) break;
                        yield return new WaitForSeconds(0.1f); tc += 0.1f;
                    }
                    r.passed = got;
                    string setNamesC = string.Join(",", selSet.ConvertAll(x => x.PawnName));
                    r.detail = got
                        ? $"선택 집합 [{setNamesC}] 중 벌목 task 보유 ({tc:F1}s — 직접명령 인과 확인)"
                        : $"선택 집합 [{setNamesC}] 벌목 task 미보유 in {win:F1}s — 직접명령 미발행";
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
                // ── 효과 어서션 7종 (WORKFLOW-V2 규칙 7, 2026-06-12) ──────────────
                //  '클릭 PASS = 입력 전달까지'의 사각을 메우는 probe — 셋업 클릭 뒤엔
                //  반드시 이들 중 하나로 '효과 발생'을 어서트한다 (이중 사각지대 사건의
                //  증류: 지시 클릭이 수십 소크에서 무음 무효였는데 아무도 못 잡았다).
                case "growZoneCells":
                {
                    yield return null;
                    int n = GrowZoneDesignation.Instance != null
                        ? GrowZoneDesignation.Instance.ZoneCellCount : 0;
                    r.passed = n >= (int)s.min;
                    r.detail = $"grow zone cells={n} (need ≥{(int)s.min})"; break;
                }
                case "mineMarks":
                {
                    yield return null;
                    int n = MineDesignation.Instance != null
                        ? MineDesignation.Instance.GetMarkedVeinPositions().Count : 0;
                    r.passed = n >= (int)s.min;
                    r.detail = $"mine marks={n} (need ≥{(int)s.min})"; break;
                }
                case "roofCells":
                {
                    yield return null;
                    int built = 0, pending = 0;
                    if (RoofDesignation.Instance != null)
                    {
                        built = RoofDesignation.Instance.RoofedCount
                              - RoofDesignation.Instance.PendingCount;
                        pending = RoofDesignation.Instance.PendingCount;
                    }
                    // min = 총 지정 수 (BUILT+pending) — 건설 완료 검증은 withinSec 로
                    //  BUILT 승급을 기다린다 (s.withinSec 0 이면 즉시 판정).
                    float t = 0;
                    while (t < s.withinSec && built < (int)s.min)
                    {
                        yield return new WaitForSeconds(0.5f); t += 0.5f;
                        if (RoofDesignation.Instance == null) break;
                        built = RoofDesignation.Instance.RoofedCount
                              - RoofDesignation.Instance.PendingCount;
                        pending = RoofDesignation.Instance.PendingCount;
                    }
                    int total = built + pending;
                    r.passed = (s.withinSec > 0f ? built : total) >= (int)s.min;
                    r.detail = $"roof built={built} pending={pending} (need ≥{(int)s.min}{(s.withinSec > 0 ? " BUILT" : "")})"; break;
                }
                case "blueprintCount":
                {
                    yield return null;
                    int n = Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None).Length;
                    r.passed = n >= (int)s.min;
                    r.detail = $"blueprints={n} (need ≥{(int)s.min})"; break;
                }
                case "structureCount":
                {
                    // s.name = wall|door|stove|bench|bed, withinSec 동안 도달 대기 (건설 완료 검증).
                    float t = 0; int n = 0;
                    while (true)
                    {
                        n = s.name switch
                        {
                            "wall"  => Object.FindObjectsByType<WallEntity>(FindObjectsSortMode.None).Length,
                            "door"  => Object.FindObjectsByType<DoorEntity>(FindObjectsSortMode.None).Length,
                            "stove" => Object.FindObjectsByType<StoveEntity>(FindObjectsSortMode.None).Length,
                            "bench" => Object.FindObjectsByType<ResearchBench>(FindObjectsSortMode.None).Length,
                            "bed"   => Object.FindObjectsByType<BedEntity>(FindObjectsSortMode.None).Length,
                            _ => -1,
                        };
                        if (n < 0) { r.passed = false; r.detail = $"unknown kind '{s.name}'"; yield break; }
                        if (n >= (int)s.min || t >= s.withinSec) break;
                        yield return new WaitForSeconds(0.5f); t += 0.5f;
                    }
                    r.passed = n >= (int)s.min;
                    r.detail = $"{s.name} count={n} (need ≥{(int)s.min}) in {t:F0}s"; break;
                }
                case "cropCount":
                {
                    float t = 0; int n = 0;
                    while (true)
                    {
                        n = Object.FindObjectsByType<CropEntity>(FindObjectsSortMode.None).Length;
                        if (n >= (int)s.min || t >= s.withinSec) break;
                        yield return new WaitForSeconds(0.5f); t += 0.5f;
                    }
                    r.passed = n >= (int)s.min;
                    r.detail = $"crops={n} (need ≥{(int)s.min}) in {t:F0}s"; break;
                }
                case "resourceAtLeast":
                {
                    // s.name = wood|stone|food|meals, withinSec 동안 도달 대기 (적립 검증).
                    var rm = ResourceManager.Instance;
                    if (rm == null) { r.passed = false; r.detail = "no ResourceManager"; break; }
                    float t = 0; int v = 0;
                    while (true)
                    {
                        v = s.name switch
                        {
                            "wood" => rm.wood, "stone" => rm.stone,
                            "food" => rm.food, "meals" => rm.meals, _ => -999,
                        };
                        if (v == -999) { r.passed = false; r.detail = $"unknown res '{s.name}'"; yield break; }
                        if (v >= (int)s.min || t >= s.withinSec) break;
                        yield return new WaitForSeconds(0.5f); t += 0.5f;
                    }
                    r.passed = v >= (int)s.min;
                    r.detail = $"{s.name}={v} (need ≥{(int)s.min}) in {t:F0}s"; break;
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
        // #마퀴플레이크(2026-06-12) — 선택 '집합' 진실: 마퀴 박스가 배회 위치에 따라
        //  2명이 아니라 3명을 잡으면 디스패치는 그 집합 내 최근접에게 가는 게 정당하다.
        //  프로브가 '집합의 첫 림' 고정 가정이라 정당 배정을 FAIL 로 읽던 플레이크 해소.
        private System.Collections.Generic.List<PawnEntity> ResolveSelectedSet()
        {
            var set = new System.Collections.Generic.List<PawnEntity>();
            var mq = Object.FindFirstObjectByType<MarqueeSelector>();
            if (mq != null && mq.HasMultiSelection)
                foreach (var m in mq.CurrentMultiSelection) if (m != null) set.Add(m);
            var selector = Object.FindFirstObjectByType<ClickSelector>();
            var sp = selector != null ? selector.CurrentSelection : null;
            if (sp != null && !set.Contains(sp)) set.Add(sp);
            return set;
        }

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
                        // 2026-07-31 — **셀 중심으로 스냅**한다.
                        //  이전에는 연속 좌표(from + dir*rr)를 그대로 클릭 지점으로 썼다.
                        //  그런데 폰은 셀 중심으로 이동하므로, 클릭 지점이 셀 안쪽 아무 데나면
                        //  도착점과 클릭점이 최대 반대각선(0.707칸)만큼 어긋난다.
                        //  `p0-pawn-move` 의 `selectedNearClick ≤0.8` 이 0.85 로 아슬하게
                        //  실패하는 플레이크가 여기서 나왔다 — 게임 버그가 아니라 **검사와
                        //  게임이 서로 다른 격자를 쓴 것**이다.  임계를 늘려 덮는 대신
                        //  두 좌표계를 맞춘다.
                        Vector3 cand = from + dir * rr;
                        cand = new Vector3(Mathf.Floor(cand.x) + 0.5f, Mathf.Floor(cand.y) + 0.5f, 0f);
                        // **도달 가능한** 빈 칸이어야 한다 (2026-08-07).
                        //  이전에는 '콜라이더 없음' 만 봤다.  그런데 물·바위 타일은
                        //  콜라이더가 없어도 통행 불가고, 벽으로 둘러싸인 안뜰도
                        //  '빈 칸' 으로 잡힌다.  그런 지점을 명령하면 폰이 최선을 다해
                        //  가까이 가다 2~3칸 앞에서 멈추고, 검사는 '이동이 안 된다' 로
                        //  읽는다 — 실제로 `p0-pawn-move` 가 그렇게 간헐 실패했다
                        //  (closest 2.45, need ≤0.8).  게임 결함이 아니라 **테스트가
                        //  갈 수 없는 곳을 시킨 것**이다.
                        if (Physics2D.OverlapPointAll(cand).Length != 0) continue;
                        if (PawnMovement.IsBlockedAt(cand)) continue;
                        // 목적지 주변 4방향 중 하나라도 통행 가능해야 한다 — 사방이
                        //  막힌 칸은 도달 경로가 없다.
                        int open = 0;
                        foreach (var nb in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down })
                        {
                            var q = cand + nb;
                            if (!PawnMovement.IsBlockedAt(q) && Physics2D.OverlapPointAll(q).Length == 0) open++;
                        }
                        if (open >= 2) { world = cand; return true; }
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
            // 기본기 진단 (2026-06-12) — 두 단계 수리:
            //  ① GetComponentInChildren(첫 Text)만 보던 사각: 지시/구역 셀은 아이콘이
            //    없어 첫 Text 가 한 글자 글리프("벌")라 'label:벌목'이 영영 미매칭.
            //  ② Contains 첫-매칭 모호성: 'label:문'→"울타리 문"(FenceGate 오발),
            //    'label:경작'→"경작 영역 제거"(EraseMode no-op) — 격리 채점이 적발.
            //  → 전 버튼 순회 + 정확도 순위: 완전일치(0) > 시작일치(1) > 포함(2),
            //    동순위면 최단 텍스트(가장 특정적인 버튼)가 이긴다.
            int bestRank = int.MaxValue, bestLen = int.MaxValue;
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (b == null) continue;
                if (byLabel)
                {
                    foreach (var txt in b.GetComponentsInChildren<Text>(true))
                    {
                        if (txt == null || string.IsNullOrEmpty(txt.text)) continue;
                        string t = txt.text.Trim();
                        int rank = t == key ? 0 : t.StartsWith(key) ? 1 : t.Contains(key) ? 2 : -1;
                        if (rank < 0) continue;
                        if (rank < bestRank || (rank == bestRank && t.Length < bestLen))
                        { bestRank = rank; bestLen = t.Length; targetBtn = b; }
                    }
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
