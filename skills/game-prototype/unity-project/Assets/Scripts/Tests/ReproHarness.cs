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
                    Vector3 world;
                    if (!ResolveWorld(s, out world)) { r.passed = false; r.detail = $"target '{s.target}' not found"; break; }
                    Vector3 screen = Camera.main.WorldToScreenPoint(world);
                    int btn = s.op == "worldclick" ? 0 : 1;
                    SimInput.FrameMouseDown(btn, screen);
                    yield return null;                 // 다음 프레임 Update 들이 이 입력을 본다
                    SimInput.ClearFrame();
                    yield return null;
                    r.passed = true;
                    r.detail = $"{(btn == 0 ? "L" : "R")}click world({world.x:F1},{world.y:F1}) screen({screen.x:F0},{screen.y:F0})";
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
                default:
                    r.passed = false; r.detail = $"unknown probe '{s.probe}'"; break;
            }
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
            else if (s.target == "pawn") found = Nearest<PawnEntity>(camC);
            else if (s.target.StartsWith("pawn:")) found = FindPawn(s.target.Substring(5));
            if (found == null) return false;
            world = found.transform.position; world.z = 0;
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
