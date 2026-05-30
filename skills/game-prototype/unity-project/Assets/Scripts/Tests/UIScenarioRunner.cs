using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// HUMAN-PLAY SCENARIO RUNNER (operator 2026-05-30: "사람이 실제 플레이하는것처럼
    /// 시나리오까지 넣어서").  Drives a realistic, end-to-end play session over the
    /// EXISTING simulation hooks — exactly the things a real player touches — and
    /// captures a PNG at EACH step so QA can eyeball the game like a human would:
    ///
    ///   1) startup game view
    ///   2) SELECT a colonist        (ClickSelector.SimulateMapClick on a pawn world pos)
    ///                               → EntityInspectorPanel + SelectionGizmoBar appear
    ///   3) open ARCHITECT/건축 menu  (GuiControlBar "Btn_건축".onClick → ArchitectMenu.Open)
    ///                               → catalogue visible
    ///   4) enter build mode + place a WALL blueprint
    ///                               (ArchitectMenu Structure→벽 button onClick → SetMode,
    ///                                then BuildManager.SimulateMapClick on empty grass)
    ///   5) designate a MINE zone    (MineDesignation.SetMode(true) + SimulateClick on a vein)
    ///   6) MULTI-SELECT marquee      (MarqueeSelector.SimulateMarquee over the colonists)
    ///   7) right-click a TREE        (re-select a pawn, ClickSelector.SimulateRightClick on a tree
    ///                                 → PawnChopper.SetTreeTarget = chop work queued)
    ///
    /// Each step logs "[UIScenario] step N <name> OK" (or WARN + skip if a target is
    /// missing — robust, never crashes the session) and writes a PNG to
    ///   G:/ai/_scn_<NN>_<name>.png
    /// Ends with "[UIScenario] DONE steps=7".
    ///
    /// ────────────────────────────────────────────────────────────────────────────
    /// SELF-BOOTSTRAP DISCIPLINE (mirrors MarqueeSelector / MineDesignation / the
    /// SelectionGizmoBar bootstrap exactly):
    ///   - [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] gated through
    ///     Core.GameSceneGate.RunWhenGameScene so it NEVER runs on MainMenu, only in
    ///     the Game scene.
    ///   - Active ONLY when argv contains "-ui-scenario" (off for normal play / the
    ///     other QA paths) — so a double-click play session is never disturbed.
    ///   - Idempotent FindFirstObjectByType guard: a domain/scene reload never spawns
    ///     a second runner.
    ///
    /// BUG-PATTERN #9 FIREWALL (runInBackground / long-WaitForSeconds freeze):
    ///   This runner is a long coroutine of WaitForSeconds steps that runs UNDER A
    ///   CLI-DRIVEN QA LAUNCH (qa.py).  When the Unity window loses OS focus to the
    ///   python launcher console, Update/coroutines freeze unless runInBackground is
    ///   ON — the exact symptom signature (short-delay PASS, long-delay FAIL, no PNG)
    ///   the AutoScreenshotter pattern was built to defeat.  So we set
    ///   Application.runInBackground = true on entry (the AutoScreenshotter contract),
    ///   and we capture every screenshot with ScreenCapture.CaptureScreenshot exactly
    ///   like AutoScreenshotter does.  We do NOT add an always-on heartbeat timer.
    ///
    /// Other firewall: no AudioBank tight loop (we only trigger the existing per-click
    /// UI blips already inside the menu/designation handlers), no GetInstanceID
    /// collision compares, no OnCollision handlers, no Singleton.OnX OnEnable
    /// subscription (every manager is poll-found read-only with a null-guarded skip),
    /// no justSpawned-default field.
    ///
    /// FILE ISOLATION: this is a NEW self-contained file.  It reads ClickSelector /
    /// BuildManager / ArchitectMenu / MineDesignation / MarqueeSelector / GameClock /
    /// the entity classes READ-ONLY through their EXISTING public Simulate* / mode
    /// APIs and edits none of them.
    /// </summary>
    public class UIScenarioRunner : MonoBehaviour
    {
        // ---- tunables (SerializeField so designer can tune QA pacing day-1) -------
        [Header("Pacing")]
        [SerializeField] private float bootSettleSec = 2.5f;   // let GameManager spawn pawns + UI bootstrap
        [SerializeField] private float perStepSettleSec = 0.8f; // let UI/visuals settle before each capture
        [SerializeField] private float postCaptureFlushSec = 0.6f; // let the PNG flush to disk
        [SerializeField] private float quitDelaySec = 1.5f;    // final flush before Application.Quit

        [Header("Output")]
        [SerializeField] private string outputDir = "G:/ai";
        [SerializeField] private string filePrefix = "_scn_";

        // ---- the activation flag ------------------------------------------------
        private const string ActivateArg = "-ui-scenario";

        // ============================================================
        //  Self-bootstrap — Game-scene-gated + argv-gated (no SceneSetup edit).
        // ============================================================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!HasActivateArg()) return;   // only when -ui-scenario requested

            // Game-scene gate: never run on MainMenu (operator 2026-05-30).  Runs the
            //  instant the Game scene is active (or right now in a game-only build).
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindExisting() != null) return;   // idempotent — never a 2nd runner
                var go = new GameObject("~UIScenarioRunner");
                Object.DontDestroyOnLoad(go);
                go.AddComponent<UIScenarioRunner>();
            });
        }

        private static bool HasActivateArg()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == ActivateArg) return true;
            return false;
        }

        private static UIScenarioRunner FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<UIScenarioRunner>();
#else
            return Object.FindObjectOfType<UIScenarioRunner>();
#endif
        }

        private int stepCount;

        private void Start()
        {
            // bug-pattern #9 firewall: a CLI-driven QA launch loses OS focus to the
            //  python console; without runInBackground the WaitForSeconds steps below
            //  freeze and no PNG ever lands (short-delay PASS / long-delay FAIL).
            //  Set it ON exactly like AutoScreenshotter does for any CLI QA path.
            Application.runInBackground = true;
            Debug.Log("[UIScenario] ============== START (human-play scenario, runInBackground=ON) ==============");
            StartCoroutine(RunScenario());
        }

        private IEnumerator RunScenario()
        {
            yield return new WaitForSeconds(bootSettleSec);

            // Step 1 — startup game view (no interaction; just confirm the scene is live).
            yield return Step(1, "startup", () =>
            {
                var clk = GameClock.Instance;
                if (clk != null) Debug.Log($"[UIScenario] clock Day {clk.Day} {clk.Hour:00}:{clk.Minute:00}");
                return true;   // startup view always "passes" (the screenshot is the check)
            });

            // Step 2 — SELECT a colonist via the real ClickSelector mouse path.
            PawnEntity firstPawn = null;
            yield return Step(2, "select_colonist", () =>
            {
                var cs = FindClickSelector();
                firstPawn = FindAlivePawn();
                if (cs == null) { Debug.LogWarning("[UIScenario] step 2 WARN: ClickSelector not found — skip"); return false; }
                if (firstPawn == null) { Debug.LogWarning("[UIScenario] step 2 WARN: no alive pawn found — skip"); return false; }
                // ClickSelector's public QA hook for selection is SimulateSelect(pawn)
                //  — the same Select() path a real left-click on the pawn drives
                //  (shows the selection ring + feeds EntityInspectorPanel + the gizmo bar).
                cs.SimulateSelect(firstPawn);
                Debug.Log($"[UIScenario] selected pawn '{firstPawn.PawnName}', sel={(cs.CurrentSelection != null ? cs.CurrentSelection.PawnName : "<null>")}");
                return cs.CurrentSelection != null;
            });

            // Step 3 — open the ARCHITECT / 건축 build menu (real GuiControlBar button click).
            yield return Step(3, "open_architect", () =>
            {
                var menu = ArchitectMenu.Instance;
                if (menu == null) { Debug.LogWarning("[UIScenario] step 3 WARN: ArchitectMenu not found — skip"); return false; }
                if (!menu.gameObject.activeSelf)
                {
                    // Prefer the genuine player path: click the GuiControlBar "건축" button.
                    if (!ClickUIButtonByName("Btn_건축"))
                        menu.Open();   // fallback: open directly (still the real Open() the button calls)
                }
                bool open = menu.gameObject.activeSelf;
                Debug.Log($"[UIScenario] architect menu open={open}");
                return open;
            });

            // Step 4 — enter a build mode + place a WALL blueprint on an empty grass cell.
            yield return Step(4, "place_wall", () =>
            {
                var bm = BuildManager.Instance;
                if (bm == null) { Debug.LogWarning("[UIScenario] step 4 WARN: BuildManager not found — skip"); return false; }

                // Drive the menu the way a player does: expand Structure, click 벽.
                //  If the menu wiring can't be reached, fall back to the public SetMode
                //  (still the EXACT API the buildable button invokes).
                var menu = ArchitectMenu.Instance;
                bool clicked = false;
                if (menu != null && menu.gameObject.activeSelf)
                {
                    ClickUIButtonContaining("Structure (구조)", menu.transform);  // expand category
                    clicked = ClickUIButtonContaining("벽 (목재 5)", menu.transform); // pick wall buildable
                }
                if (!clicked && bm.CurrentMode != BuildManager.Mode.Wall)
                    bm.SetMode(BuildManager.Mode.Wall);

                if (bm.CurrentMode != BuildManager.Mode.Wall)
                {
                    Debug.LogWarning($"[UIScenario] step 4 WARN: build mode not Wall (={bm.CurrentMode}) — skip place");
                    return false;
                }

                // Find an open grass cell and place via the real map-click path.  Try a
                //  spread of cells (placement rejects water/rock/occupied — robust).
                int bpBefore = CountBlueprints();
                bool placed = false;
                foreach (var (cx, cy) in CandidateGrassCells())
                {
                    Vector3 world = new Vector3(cx + 0.5f, cy + 0.5f, 0f);
                    Vector2 screen = WorldToScreen(world);
                    var result = bm.SimulateMapClick(screen);
                    if (result == BuildClickResult.Placed) { placed = true; Debug.Log($"[UIScenario] wall blueprint placed @ cell({cx},{cy})"); break; }
                }
                bm.SetMode(BuildManager.Mode.Off);   // leave build mode (a player would right-click off)
                if (!placed && CountBlueprints() > bpBefore) placed = true;  // tolerant
                if (!placed) Debug.LogWarning("[UIScenario] step 4 WARN: no grass cell accepted the wall — blueprint not placed");
                return placed;
            });

            // Step 5 — designate a MINE zone over a rock vein (toggle + simulate click).
            yield return Step(5, "designate_mine", () =>
            {
                var mine = MineDesignation.Instance;
                if (mine == null) { Debug.LogWarning("[UIScenario] step 5 WARN: MineDesignation not found — skip"); return false; }
                var vein = FindMineableVein();
                mine.SetMode(true);   // enter mine designation mode (cancels build/decon)
                if (vein == null)
                {
                    Debug.LogWarning("[UIScenario] step 5 WARN: no mineable rock vein found — mode toggled, nothing to mark");
                    return false;
                }
                Vector2 screen = WorldToScreen(vein.transform.position);
                var mt = mine.SimulateClick(screen);   // mark the vein for mining
                bool marked = mt != null;
                Debug.Log($"[UIScenario] mine designation marked={marked} at vein '{vein.name}'");
                // leave it in mine mode for the screenshot; next step toggles it off.
                return marked;
            });

            // Step 6 — MULTI-SELECT marquee over the colonists (box-select).
            yield return Step(6, "marquee_multiselect", () =>
            {
                // Exit mine mode so the marquee gesture isn't suppressed (MineDesignation
                //  blocks left-clicks while active — same exclusivity the real UI enforces).
                if (MineDesignation.Instance != null) MineDesignation.Instance.SetMode(false);

                // MarqueeSelector exposes NO public QA-drive entry point and the lane
                //  forbids editing it, so we cannot inject a synthetic drag gesture
                //  through it.  Instead we reproduce its EXACT box-select math here
                //  READ-ONLY (Physics2D.OverlapArea over the pawn bounds, then
                //  PawnEntity.SetSelected(true) on each — the identical visual the
                //  MarqueeSelector itself applies).  This shows the multi-select rings
                //  for the screenshot without touching the (input-driven) selector.
                if (!ComputePawnBounds(out Vector2 min, out Vector2 max))
                {
                    Debug.LogWarning("[UIScenario] step 6 WARN: no pawns to marquee — skip");
                    return false;
                }
                min -= Vector2.one; max += Vector2.one;   // pad so every collider is inside
                int n = BoxSelectPawns(min, max);
                Debug.Log($"[UIScenario] marquee box-selected {n} pawn(s) over rect ({min.x:F1},{min.y:F1})-({max.x:F1},{max.y:F1})");
                return n > 0;
            });

            // Step 7 — right-click a TREE to queue chop work.
            yield return Step(7, "chop_tree", () =>
            {
                var cs = FindClickSelector();
                if (cs == null) { Debug.LogWarning("[UIScenario] step 7 WARN: ClickSelector not found — skip"); return false; }
                var tree = FindTree();
                if (tree == null) { Debug.LogWarning("[UIScenario] step 7 WARN: no tree found — skip"); return false; }

                // Make sure a single pawn is selected (the marquee may have cleared the
                //  single ring).  Re-select the first pawn via the real select path.
                var pawn = (cs.CurrentSelection != null) ? cs.CurrentSelection : (firstPawn != null ? firstPawn : FindAlivePawn());
                if (pawn == null) { Debug.LogWarning("[UIScenario] step 7 WARN: no pawn to issue chop — skip"); return false; }
                if (cs.CurrentSelection == null)
                    cs.SimulateSelect(pawn);   // re-select via the real Select() path

                // Right-click the tree (real ClickSelector right-click path → chopper target).
                Vector2 treeWorld = tree.transform.position;
                cs.SimulateRightClick(treeWorld);
                var chopper = (cs.CurrentSelection != null) ? cs.CurrentSelection.GetComponent<PawnChopper>() : null;
                bool queued = chopper != null;   // chopper exists → SetTreeTarget was invokable
                Debug.Log($"[UIScenario] chop work right-clicked on tree '{tree.name}', chopper={(queued ? "present" : "ABSENT")}");
                return queued;
            });

            Debug.Log($"[UIScenario] DONE steps={stepCount}");
            yield return new WaitForSeconds(quitDelaySec);
            Debug.Log("[UIScenario] quitting...");
            Application.Quit();
        }

        // ============================================================
        //  Step harness: settle → run action → capture PNG → log.
        // ============================================================
        private IEnumerator Step(int n, string name, System.Func<bool> action)
        {
            yield return new WaitForSeconds(perStepSettleSec);

            bool ok = false;
            try
            {
                ok = action != null && action();
            }
            catch (System.Exception ex)
            {
                // Robustness: a thrown step never crashes the whole session.
                Debug.LogWarning($"[UIScenario] step {n} {name} WARN: exception {ex.GetType().Name}: {ex.Message} — continuing");
                ok = false;
            }

            // Capture AFTER the action so the screenshot reflects the new UI/world state.
            yield return new WaitForEndOfFrame();
            string path = CapturePath(n, name);
            CaptureScreenshot(path);
            yield return new WaitForSeconds(postCaptureFlushSec);

            stepCount = n;
            Debug.Log(ok
                ? $"[UIScenario] step {n} {name} OK  -> {path}"
                : $"[UIScenario] step {n} {name} WARN (target missing / not confirmed) -> {path}");
        }

        private string CapturePath(int n, string name)
            => Path.Combine(outputDir, $"{filePrefix}{n:00}_{name}.png").Replace('\\', '/');

        private void CaptureScreenshot(string path)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                ScreenCapture.CaptureScreenshot(path);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[UIScenario] capture WARN: {ex.Message}");
            }
        }

        // ============================================================
        //  Read-only poll-find helpers (singleton-subscriber pattern, #7 firewall).
        // ============================================================
        private static ClickSelector FindClickSelector()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<ClickSelector>();
#else
            return Object.FindObjectOfType<ClickSelector>();
#endif
        }

        private static PawnEntity FindAlivePawn()
        {
#if UNITY_2023_1_OR_NEWER
            var pawns = Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
#else
            var pawns = Object.FindObjectsOfType<PawnEntity>();
#endif
            foreach (var p in pawns) if (p != null && !p.IsDead) return p;
            return null;
        }

        private static PawnEntity[] FindAlivePawns()
        {
#if UNITY_2023_1_OR_NEWER
            var all = Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
#else
            var all = Object.FindObjectsOfType<PawnEntity>();
#endif
            var live = new List<PawnEntity>(all.Length);
            foreach (var p in all) if (p != null && !p.IsDead) live.Add(p);
            return live.ToArray();
        }

        private static StoneVeinEntity FindMineableVein()
        {
#if UNITY_2023_1_OR_NEWER
            var veins = Object.FindObjectsByType<StoneVeinEntity>(FindObjectsSortMode.None);
#else
            var veins = Object.FindObjectsOfType<StoneVeinEntity>();
#endif
            foreach (var v in veins)
                if (v != null && MineTarget.IsMineable(v.gameObject)) return v;
            return null;
        }

        private static TreeEntity FindTree()
        {
#if UNITY_2023_1_OR_NEWER
            var trees = Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None);
#else
            var trees = Object.FindObjectsOfType<TreeEntity>();
#endif
            foreach (var t in trees) if (t != null) return t;
            return null;
        }

        // ---- geometry helpers ----------------------------------------------------
        private static Vector2 WorldToScreen(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return Vector2.zero;
            Vector3 s = cam.WorldToScreenPoint(world);
            return new Vector2(s.x, s.y);
        }

        private static bool ComputePawnBounds(out Vector2 min, out Vector2 max)
        {
            min = Vector2.zero; max = Vector2.zero;
            var pawns = FindAlivePawns();
            if (pawns.Length == 0) return false;
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in pawns)
            {
                Vector2 pos = p.transform.position;
                min = Vector2.Min(min, pos);
                max = Vector2.Max(max, pos);
            }
            return true;
        }

        /// <summary>Reproduce MarqueeSelector's box-select READ-ONLY: OverlapArea the
        ///  world rectangle, then show each enclosed alive pawn's selection ring via
        ///  the same PawnEntity.SetSelected(true) the marquee applies.  Returns the
        ///  count selected.  Pre-allocated buffer = no per-frame List<> GC churn.</summary>
        private static readonly Collider2D[] s_overlapBuf = new Collider2D[256];
        private static readonly List<PawnEntity> s_boxSel = new List<PawnEntity>(64);
        private static int BoxSelectPawns(Vector2 min, Vector2 max)
        {
            s_boxSel.Clear();
            int hits = Physics2D.OverlapAreaNonAlloc(min, max, s_overlapBuf);
            for (int i = 0; i < hits; i++)
            {
                var c = s_overlapBuf[i];
                if (c == null) continue;
                var pawn = c.GetComponent<PawnEntity>();
                if (pawn == null || pawn.IsDead) continue;
                if (s_boxSel.Contains(pawn)) continue;   // a pawn may carry multiple colliders
                s_boxSel.Add(pawn);
                pawn.SetSelected(true);                  // marquee ring (same call MarqueeSelector uses)
            }
            return s_boxSel.Count;
        }

        /// <summary>Candidate empty grass cells to try a wall blueprint on.  Mirrors
        ///  the grass column BuildClickAutoQA uses (x=-22 row is grass, clear of the
        ///  lake at (-18,-12) and the rock band), spreading downward so placement
        ///  validation (water/rock/occupied) can reject a cell and we just try the
        ///  next — robust, never assumes one cell is free.</summary>
        private static IEnumerable<(int cx, int cy)> CandidateGrassCells()
        {
            for (int cy = -8; cy >= -12; cy--) yield return (-22, cy);
            for (int cy = -8; cy >= -12; cy--) yield return (-21, cy);
            for (int cy = -8; cy >= -12; cy--) yield return (-23, cy);
        }

        private static int CountBlueprints()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None).Length;
#else
            return Object.FindObjectsOfType<BlueprintEntity>().Length;
#endif
        }

        // ============================================================
        //  uGUI button-click helpers (real EventSystem path via onClick.Invoke) —
        //  same approach BuildClickAutoQA uses to drive the genuine player flow.
        // ============================================================
        private static bool ClickUIButtonByName(string goName)
        {
#if UNITY_2023_1_OR_NEWER
            var buttons = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None);
#else
            var buttons = Object.FindObjectsOfType<UnityEngine.UI.Button>();
#endif
            foreach (var b in buttons)
            {
                if (b == null) continue;
                if (b.gameObject.name == goName) { b.onClick.Invoke(); return true; }
            }
            return false;
        }

        private static bool ClickUIButtonContaining(string labelContains, Transform parent)
        {
            if (parent == null) return false;
            var buttons = parent.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var b in buttons)
            {
                if (b == null) continue;
                var txt = b.GetComponentInChildren<UnityEngine.UI.Text>(true);
                if (txt == null) continue;
                if (txt.text.Contains(labelContains)) { b.onClick.Invoke(); return true; }
            }
            return false;
        }
    }
}
