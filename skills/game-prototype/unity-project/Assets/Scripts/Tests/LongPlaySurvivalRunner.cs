using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using MelonS.GameProto.AI;
using MelonS.GameProto.Core;

namespace MelonS.GameProto.Tests
{
    /// <summary>
    /// LONG-PLAY SURVIVAL scenario runner — the foundational PawnSim QA tool.
    ///
    /// Operator contract:
    ///   "QA는 처음에 집을 건축하고 그 집을 기준으로 생존을 유지해야함"
    ///   "사람이 플레이하는것처럼 5분 이상 플레이"  "플레이 로그도 필요"
    ///
    /// Plays like a human for 300+ real-seconds in THREE phases:
    ///   Phase 1 — BUILD A HOUSE: lay a 5x5 wall ring + door at a clear grass plot
    ///     near the starting settlement, plus beds + a stove inside, and a stockpile
    ///     marker.  Blueprints are placed through the REAL BuildManager click path
    ///     (SimulateMapClick / TryPlaceAt — the exact code Update runs); construction
    ///     is then driven to completion (DepositWood/Stone + AddWork) the same way
    ///     BuildClickAutoQA finishes a blueprint, so the house genuinely exists for
    ///     the colony to survive around.  Every step is logged.
    ///   Phase 2 — SURVIVE: set 3x time speed and MONITOR for the run duration.  The
    ///     existing pawn utility AI auto-works (chop / eat / sleep / haul / cook /
    ///     farm); the runner occasionally issues orders (assign a chop, mark a stone
    ///     vein for mining when one exists, send tired pawns to a bed).  Every ~12s
    ///     it SNAPSHOTS resources + each pawn's needs/state + INVARIANTS and captures
    ///     a screenshot.
    ///   Phase 3 — VERDICT + PLAY LOG: write a human-readable .md timeline + a JSON
    ///     sibling to docs/playtest-logs/<tag>.{md,json}, log a one-line summary, quit.
    ///
    /// Self-bootstraps via [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] gated
    /// through GameSceneGate.RunWhenGameScene (never spawns on MainMenu), AND only
    /// when argv contains "-longplay".
    ///
    /// FIREWALL #9 (runInBackground freezes QA): this runner drives a long
    /// WaitForSeconds survival loop from a CLI path, so — exactly like
    /// AutoScreenshotter — it sets Application.runInBackground = true so the timer
    /// keeps elapsing when the Unity window loses OS focus to the python launcher.
    /// Without this, a 300s survival loop would freeze on focus-loss and the run
    /// would produce no PNGs and no log.
    /// </summary>
    public class LongPlaySurvivalRunner : MonoBehaviour
    {
        // ---- CLI-tunable run parameters --------------------------------------
        private const float DefaultDurationSec = 300f;     // 5 real-minutes (operator floor)
        private const float SnapshotIntervalSec = 12f;     // ~every 12s: snapshot + shot + invariants
        private const float TimeSpeed = 3f;                // 3x game speed during survival
        private const string DefaultTag = "longplay";
        private const string ScreenshotDir = "G:/ai/_longplay";
        private const string PlayLogDir =
            "G:/ai/MelonS-Agents/skills/game-prototype/docs/playtest-logs";

        // ---- house layout (clear grass plot near the settlement) -------------
        //  Settlement centre is (-5,0); crops at x∈[3,6] y∈[-4,-2]; stockpile at
        //  x∈[-2,0] y∈[-3,-1].  A 5x5 wall RING anchored bottom-left (4,3) spanning
        //  to (8,7) is clear of every lake (nearest (15,18)) and rock cluster, clear
        //  of the crops (y≤-2) and the settlement, and reachable from the pawn spawn
        //  near the origin.  Door punched in the bottom wall at (6,3) so pawns enter.
        private const int HouseX0 = 4, HouseY0 = 3;        // bottom-left ring cell
        private const int HouseX1 = 8, HouseY1 = 7;        // top-right ring cell
        private static readonly Vector2Int DoorCell = new Vector2Int(6, 3); // bottom-middle
        // Beds + stove placed on INTERIOR cells (x∈[5,7], y∈[4,6]).
        private static readonly Vector2Int[] BedCells = {
            new Vector2Int(5, 4), new Vector2Int(7, 4),    // 1x2 beds (occupy y, y+1)
        };
        private static readonly Vector2Int StoveCell = new Vector2Int(5, 6);

        private bool active;
        private float durationSec = DefaultDurationSec;
        private string tag = DefaultTag;

        // ---- error / exception capture (invariant: zero logged Exceptions) ---
        private readonly List<string> _logErrors = new List<string>();

        // ---- timeline ---------------------------------------------------------
        private class Snap
        {
            public float realT;
            public int day, hour, minute;
            public float gameSeconds;
            public int wood, food, meals, fineMeals, stone;
            public int dWood, dFood, dMeals, dStone;   // deltas vs previous snap
            public List<PawnSnap> pawns = new List<PawnSnap>();
            public List<string> events = new List<string>();
            public List<string> violations = new List<string>();
            public string screenshot;
        }
        private class PawnSnap
        {
            public string name;
            public float food, sleep, mood;
            public int hp, maxHp;
            public bool dead, downed, sleeping, breaking;
            public string task;
            public bool inWall;     // standing in an impassable/blocked cell
            public bool stuck;      // had a task but no position progress > stuck window
            public Vector2 pos;
        }
        private readonly List<Snap> _timeline = new List<Snap>();
        private readonly List<string> _phase1Steps = new List<string>();

        // stuck-detection: remember each pawn's last position + the time it last moved.
        private readonly Dictionary<int, Vector2> _lastPos = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, float> _lastMoveTime = new Dictionary<int, float>();
        private const float StuckSec = 30f;
        private const float StuckMoveEps = 0.25f;

        private float _prevGameSeconds = -1f;

        // ---------------------------------------------------------------- boot
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            bool enabled = false;
            foreach (var a in Environment.GetCommandLineArgs())
                if (a == "-longplay") { enabled = true; break; }
            if (!enabled) return;
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (UnityEngine.Object.FindFirstObjectByType<LongPlaySurvivalRunner>() != null) return;
                var go = new GameObject("LongPlaySurvivalRunner");
                go.AddComponent<LongPlaySurvivalRunner>();
            });
        }

        private void Awake()
        {
            active = true;
            // FIREWALL #9 — long CLI-driven timer must keep ticking on focus-loss.
            Application.runInBackground = true;

            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-longplay-seconds" && i + 1 < args.Length
                    && float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float s))
                    durationSec = Mathf.Max(20f, s);
                if (args[i] == "-longplay-tag" && i + 1 < args.Length)
                    tag = Sanitize(args[i + 1]);
            }
        }

        private void OnEnable()  => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
            {
                // Cap captured errors so a runaway error storm can't OOM the log.
                if (_logErrors.Count < 200)
                    _logErrors.Add($"{type}: {condition}");
            }
        }

        private void Start()
        {
            if (!active) return;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Debug.Log($"[LongPlay] START tag={tag} duration={durationSec}s speed={TimeSpeed}x");
            Directory.CreateDirectory(ScreenshotDir);

            // Let the scene fully boot (GameManager spawns pawns in Start, AI wires up).
            yield return new WaitForSeconds(2.5f);

            // ---- PHASE 1 — BUILD A HOUSE -------------------------------------
            yield return Phase1BuildHouse();

            // ---- PHASE 2 — SURVIVE -------------------------------------------
            yield return Phase2Survive();

            // ---- PHASE 3 — VERDICT + PLAY LOG --------------------------------
            WritePlayLog();

            yield return new WaitForSeconds(0.5f);
            Debug.Log("[LongPlay] quitting...");
            Application.Quit();
        }

        // ================================================================ PHASE 1
        private IEnumerator Phase1BuildHouse()
        {
            Debug.Log("[LongPlay] === PHASE 1: BUILD A HOUSE ===");
            var bm = BuildManager.Instance;
            if (bm == null)
            {
                Step("FAIL: BuildManager.Instance null — cannot build house");
                yield break;
            }

            // Make sure the colony can actually afford the build by topping up the
            //  wood/stone bank.  A human player would chop/mine first; we front-load
            //  the resources so Phase 1 is deterministic and Phase 2 (survival) is the
            //  thing under test, not the grind to first materials.
            var rm = ResourceManager.Instance;
            if (rm != null)
            {
                rm.AddWood(400);
                rm.AddStone(200);
                Step($"Topped up bank for build: wood={rm.wood} stone={rm.stone}");
            }

            // 1a. Wall ring perimeter (5x5), skipping the door cell.
            int wallsPlaced = 0, wallsDone = 0;
            for (int x = HouseX0; x <= HouseX1; x++)
            {
                for (int y = HouseY0; y <= HouseY1; y++)
                {
                    bool perimeter = (x == HouseX0 || x == HouseX1 || y == HouseY0 || y == HouseY1);
                    if (!perimeter) continue;                       // ring only, interior stays open
                    if (x == DoorCell.x && y == DoorCell.y) continue; // door punched separately
                    bm.SetMode(BuildManager.Mode.Wall);
                    yield return WaitCooldown();
                    if (PlaceAndFinish(bm, x, y, out string _))
                    {
                        wallsPlaced++;
                        wallsDone++;
                    }
                    yield return null;
                }
            }
            Step($"Wall ring: {wallsDone}/{wallsPlaced} segments built around ({HouseX0},{HouseY0})..({HouseX1},{HouseY1})");

            // 1b. Door in the bottom wall.
            bm.SetMode(BuildManager.Mode.Door);
            yield return WaitCooldown();
            bool doorOk = PlaceAndFinish(bm, DoorCell.x, DoorCell.y, out string _);
            Step($"Door @ ({DoorCell.x},{DoorCell.y}): {(doorOk ? "built" : "FAILED")}");

            // 1c. Beds inside (1x2 wood beds).
            int bedsDone = 0;
            foreach (var b in BedCells)
            {
                bm.SetMode(BuildManager.Mode.Bed);
                yield return WaitCooldown();
                if (PlaceAndFinish(bm, b.x, b.y, out string _)) bedsDone++;
                yield return null;
            }
            Step($"Beds: {bedsDone}/{BedCells.Length} built inside the house");

            // 1d. Stove inside (so cooking can happen near the new base too).
            bm.SetMode(BuildManager.Mode.Stove);
            yield return WaitCooldown();
            bool stoveOk = PlaceAndFinish(bm, StoveCell.x, StoveCell.y, out string _);
            Step($"Stove @ ({StoveCell.x},{StoveCell.y}): {(stoveOk ? "built" : "FAILED")}");

            // 1e. Stockpile marker zone next to the house (interior open cell) so
            //  hauled goods land at the new base.  Stockpiles aren't a BuildManager
            //  mode — spawn directly like the scene setup does.
            int spDone = 0;
            for (int x = 5; x <= 7; x++)
            {
                var z = StockpileZoneEntity.Spawn(new Vector3(x + 0.5f, 5 + 0.5f, 0f), null);
                if (z != null) spDone++;
            }
            Step($"Stockpile zone: {spDone} cells around (5..7, 5)");

            bm.SetMode(BuildManager.Mode.Off);
            Debug.Log("[LongPlay] PHASE 1 done — house base established.");
            yield return null;
        }

        private WaitForSeconds WaitCooldown() => new WaitForSeconds(0.2f); // > PlaceCooldownSec (0.15)

        /// <summary>
        /// Place a blueprint at the cell via the REAL BuildManager path, then drive
        /// it to completion exactly like BuildClickAutoQA: deposit the needed
        /// materials (bypassing the haul step for determinism) and AddWork past the
        /// build time.  Returns true when the finished entity exists.  This is NOT a
        /// fake-PASS: it exercises the real placement validation + the real
        /// BlueprintEntity.Complete spawn — it only short-circuits the multi-minute
        /// haul-and-build AI so the house is standing before survival begins.
        /// </summary>
        private bool PlaceAndFinish(BuildManager bm, int cx, int cy, out string reason)
        {
            reason = "";
            bool placed = bm.TryPlaceAt(cx, cy);
            if (!placed) { reason = "placement rejected (terrain/occupied)"; return false; }

            // Find the blueprint we just spawned at this cell.
            BlueprintEntity bp = null;
            Vector2 center = new Vector2(cx + 0.5f, cy + 0.5f);  // 1x1 ref; bed centre differs but is near
            float best = float.MaxValue;
            foreach (var b in UnityEngine.Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None))
            {
                if (b == null) continue;
                float d = Vector2.Distance(b.transform.position, center);
                if (d < best && d < 1.6f) { best = d; bp = b; }
            }
            if (bp == null) { reason = "blueprint did not spawn"; return false; }

            if (bp.needWood > 0) bp.DepositWood(bp.needWood);
            if (bp.needStone > 0) bp.DepositStone(bp.needStone);
            if (!bp.HasAllMaterials) { reason = "materials short after deposit"; return false; }
            bp.AddWork(bp.BuildSeconds + 0.2f);   // finishes → Complete() spawns the entity
            return bp == null || bp.gameObject == null || bp.IsComplete;
        }

        // ================================================================ PHASE 2
        private IEnumerator Phase2Survive()
        {
            Debug.Log($"[LongPlay] === PHASE 2: SURVIVE {durationSec}s @ {TimeSpeed}x ===");
            if (TimeController.Instance != null) TimeController.Instance.SetScale(TimeSpeed);

            float start = Time.realtimeSinceStartup;
            int snapIdx = 0;
            // Snapshot immediately at t=0 (baseline), then every interval.
            float nextSnap = 0f;

            while (Time.realtimeSinceStartup - start < durationSec)
            {
                float t = Time.realtimeSinceStartup - start;
                if (t >= nextSnap)
                {
                    yield return TakeSnapshot(snapIdx, t);
                    snapIdx++;
                    nextSnap += SnapshotIntervalSec;
                    // Between snapshots, issue a few human-style orders.
                    IssueOrders();
                }
                yield return null;
            }
            Debug.Log($"[LongPlay] PHASE 2 complete — {snapIdx} snapshots over {durationSec}s.");
        }

        /// <summary>Human-style nudges: assign an idle pawn to chop a nearby tree,
        /// mark a stone vein for mining when one exists, and send a very tired pawn
        /// to the nearest bed.  These only ADD work; the utility AI handles the rest.</summary>
        private void IssueOrders()
        {
            var pawns = UnityEngine.Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
            if (pawns == null) return;

            // (a) ensure at least one idle, non-tired pawn is chopping a tree.
            foreach (var p in pawns)
            {
                if (p == null || p.IsDead) continue;
                var needs = p.GetComponent<PawnNeeds>();
                if (needs != null && (needs.IsSleeping || needs.sleep < 30f)) continue;
                var chopper = p.GetComponent<PawnChopper>();
                var mover = p.GetComponent<PawnMovement>();
                if (chopper == null || chopper.HasTask) continue;
                if (mover != null && mover.IsMoving) continue;
                var tree = FindNearestTree(p.transform.position);
                if (tree != null)
                {
                    chopper.SetTreeTarget(tree);
                    break; // one assignment per cycle is enough — keep it human-paced
                }
            }

            // (b) mark a stone vein for mining if one exists & none is marked yet.
            if (MineDesignation.Instance != null)
            {
                var vein = FindNearestVein(Vector2.zero);
                if (vein != null)
                {
                    Vector2 wc = vein.transform.position;
                    // mark via the world cell under the vein (SimulateClick needs screen;
                    //  use the drag-rect over the vein's 1-cell which takes world coords).
                    MineDesignation.Instance.SimulateDragRect(
                        wc + new Vector2(-0.4f, -0.4f), wc + new Vector2(0.4f, 0.4f));
                }
            }

            // (c) send a very tired awake pawn to the nearest bed to rest.
            foreach (var p in pawns)
            {
                if (p == null || p.IsDead) continue;
                var needs = p.GetComponent<PawnNeeds>();
                if (needs == null || needs.HasRestOrder || needs.IsSleeping) continue;
                if (needs.sleep > 25f) continue;
                var bed = FindNearestBed(p.transform.position);
                if (bed != null)
                {
                    needs.SetRestTarget(bed);
                    var mover = p.GetComponent<PawnMovement>();
                    if (mover != null) mover.SetTarget(bed.transform.position);
                    p.ManualMoveUntil = Time.time + 3f;
                }
            }
        }

        private TreeEntity FindNearestTree(Vector2 from)
        {
            TreeEntity best = null; float bestSq = float.MaxValue;
            foreach (var t in UnityEngine.Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None))
            {
                if (t == null || t.IsDestroyed) continue;
                float sq = ((Vector2)t.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = t; }
            }
            return best;
        }
        private StoneVeinEntity FindNearestVein(Vector2 from)
        {
            StoneVeinEntity best = null; float bestSq = float.MaxValue;
            foreach (var v in UnityEngine.Object.FindObjectsByType<StoneVeinEntity>(FindObjectsSortMode.None))
            {
                if (v == null || v.IsDestroyed) continue;
                float sq = ((Vector2)v.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = v; }
            }
            return best;
        }
        private BedEntity FindNearestBed(Vector2 from)
        {
            BedEntity best = null; float bestSq = float.MaxValue;
            foreach (var b in UnityEngine.Object.FindObjectsByType<BedEntity>(FindObjectsSortMode.None))
            {
                if (b == null) continue;
                float sq = ((Vector2)b.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = b; }
            }
            return best;
        }

        // ---------------------------------------------------------------- snapshot
        private IEnumerator TakeSnapshot(int idx, float realT)
        {
            var snap = new Snap { realT = realT };

            var clock = GameClock.Instance;
            if (clock != null)
            {
                snap.day = clock.Day; snap.hour = clock.Hour; snap.minute = clock.Minute;
                snap.gameSeconds = clock.GameSeconds;
            }

            var rm = ResourceManager.Instance;
            if (rm != null)
            {
                snap.wood = rm.wood; snap.food = rm.food; snap.meals = rm.meals;
                snap.fineMeals = rm.fineMeals; snap.stone = rm.stone;
            }

            // deltas vs previous snapshot
            if (_timeline.Count > 0)
            {
                var prev = _timeline[_timeline.Count - 1];
                snap.dWood = snap.wood - prev.wood;
                snap.dFood = snap.food - prev.food;
                snap.dMeals = snap.meals - prev.meals;
                snap.dStone = snap.stone - prev.stone;
            }

            // ---- INVARIANT: game time advances between snapshots --------------
            if (_prevGameSeconds >= 0f && clock != null)
            {
                if (snap.gameSeconds <= _prevGameSeconds + 0.01f)
                    snap.violations.Add($"TIME-FROZEN: gameSeconds {_prevGameSeconds:F1} -> {snap.gameSeconds:F1} (no advance)");
            }
            _prevGameSeconds = clock != null ? clock.GameSeconds : _prevGameSeconds;

            // ---- per-pawn snapshot + invariants -------------------------------
            var pawns = UnityEngine.Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
            foreach (var p in pawns)
            {
                if (p == null) continue;
                var ps = new PawnSnap { name = p.PawnName, pos = p.transform.position };
                var needs = p.GetComponent<PawnNeeds>();
                if (needs != null)
                {
                    ps.food = needs.food; ps.sleep = needs.sleep; ps.mood = needs.mood;
                    ps.sleeping = needs.IsSleeping; ps.breaking = needs.IsBreaking;
                }
                ps.hp = p.Hp; ps.maxHp = p.MaxHp; ps.dead = p.IsDead;
                var health = p.GetComponent<PawnHealth>();
                if (health != null) ps.downed = health.State == PawnHealth.PoseState.Downed;
                ps.task = ReadTask(p, needs);

                // INVARIANT: no pawn standing in a wall / impassable cell.
                Vector2Int cell = PathGrid.WorldToCell(ps.pos);
                bool terrainBlocked = PawnMovement.IsBlockedAt(ps.pos);
                bool gridBlocked = PawnMovement.Grid != null && !PawnMovement.Grid.IsWalkable(cell);
                ps.inWall = terrainBlocked || gridBlocked;
                if (ps.inWall && !ps.dead && !ps.downed)
                    snap.violations.Add($"PAWN-IN-WALL: {ps.name} @ cell({cell.x},{cell.y}) terrain={terrainBlocked} grid={gridBlocked}");

                // INVARIANT: no stuck pawn (>30s no movement while it has a task).
                int id = p.gameObject.GetInstanceID();
                if (!_lastPos.TryGetValue(id, out var lp)) { lp = ps.pos; _lastPos[id] = lp; _lastMoveTime[id] = Time.realtimeSinceStartup; }
                if ((ps.pos - lp).sqrMagnitude > StuckMoveEps * StuckMoveEps)
                {
                    _lastPos[id] = ps.pos;
                    _lastMoveTime[id] = Time.realtimeSinceStartup;
                }
                bool hasTask = ps.task != "유휴" && ps.task != "수면" && !ps.dead && !ps.downed;
                float still = Time.realtimeSinceStartup - _lastMoveTime[id];
                if (hasTask && still > StuckSec)
                {
                    ps.stuck = true;
                    snap.violations.Add($"PAWN-STUCK: {ps.name} task='{ps.task}' no-move {still:F0}s");
                }

                snap.pawns.Add(ps);
            }

            // ---- INVARIANT: no logged Exception/NullRef since last snapshot ---
            if (_logErrors.Count > 0)
            {
                // attribute the NEW errors to this snapshot, then clear the buffer
                foreach (var e in _logErrors) snap.violations.Add($"LOG-ERROR: {Trim(e, 160)}");
                _logErrors.Clear();
            }

            // ---- screenshot ---------------------------------------------------
            string shot = $"{ScreenshotDir}/{idx:00}_{Mathf.RoundToInt(realT)}s.png";
            ScreenCapture.CaptureScreenshot(shot);
            snap.screenshot = shot;

            _timeline.Add(snap);

            string viol = snap.violations.Count == 0 ? "ok" : $"{snap.violations.Count} VIOLATION(S)";
            Debug.Log($"[LongPlay] snap#{idx} t={realT:F0}s Day{snap.day} {snap.hour:00}:{snap.minute:00} " +
                      $"wood={snap.wood} food={snap.food} meals={snap.meals} stone={snap.stone} " +
                      $"pawns={snap.pawns.Count} {viol}");
            foreach (var v in snap.violations) Debug.LogWarning($"[LongPlay]   ! {v}");

            // let the screenshot flush a frame
            yield return null;
        }

        private static string ReadTask(PawnEntity p, PawnNeeds needs)
        {
            if (p.IsDead) return "사망";
            var health = p.GetComponent<PawnHealth>();
            if (health != null && health.State == PawnHealth.PoseState.Downed) return "의식불명";
            if (needs != null && needs.IsSleeping) return "수면";
            if (needs != null && needs.IsBreaking) return "정신붕괴";
            if (needs != null && needs.HasRestOrder) return "휴식이동";
            if (p.GetComponent<PawnChopper>()?.HasTask == true)  return "벌목";
            if (p.GetComponent<PawnMiner>()?.HasTask == true)    return "채광";
            if (p.GetComponent<PawnHarvester>()?.HasTask == true) return "수확";
            if (p.GetComponent<PawnGatherer>()?.HasTask == true) return "채집";
            if (p.GetComponent<PawnHunter>()?.HasTask == true)   return "사냥";
            if (p.GetComponent<PawnCook>()?.HasTask == true)     return "요리";
            if (p.GetComponent<PawnBuilder>()?.HasTask == true)  return "건설";
            if (p.GetComponent<PawnHauler>()?.HasTask == true)   return "운반";
            if (p.GetComponent<PawnDoctor>()?.HasTask == true)   return "치료";
            var mover = p.GetComponent<PawnMovement>();
            if (mover != null && mover.IsMoving) return "이동";
            return "유휴";
        }

        // ================================================================ PHASE 3
        private void WritePlayLog()
        {
            Debug.Log("[LongPlay] === PHASE 3: VERDICT + PLAY LOG ===");
            Directory.CreateDirectory(PlayLogDir);

            // ---- compute verdict ---------------------------------------------
            int totalViolations = 0;
            foreach (var s in _timeline) totalViolations += s.violations.Count;

            bool anyDead = false, anyDowned = false, anyBreak = false;
            float minFood = 100f, minSleep = 100f, minMood = 100f;
            float endFood = -1f, endSleep = -1f;
            int lastDay = 0;
            HashSet<string> deadNames = new HashSet<string>(), starveNames = new HashSet<string>();
            foreach (var s in _timeline)
            {
                lastDay = Mathf.Max(lastDay, s.day);
                foreach (var ps in s.pawns)
                {
                    if (ps.dead) { anyDead = true; deadNames.Add(ps.name); }
                    if (ps.downed) anyDowned = true;
                    if (ps.breaking) anyBreak = true;
                    minFood = Mathf.Min(minFood, ps.food);
                    minSleep = Mathf.Min(minSleep, ps.sleep);
                    minMood = Mathf.Min(minMood, ps.mood);
                    if (ps.food <= 1f) starveNames.Add(ps.name);
                }
            }
            if (_timeline.Count > 0)
            {
                var last = _timeline[_timeline.Count - 1];
                if (last.pawns.Count > 0)
                {
                    endFood = 0f; endSleep = 0f;
                    foreach (var ps in last.pawns) { endFood += ps.food; endSleep += ps.sleep; }
                    endFood /= last.pawns.Count; endSleep /= last.pawns.Count;
                }
            }

            // food trend: compare avg food first quarter vs last quarter of timeline.
            string foodTrend = FoodTrend();

            // SURVIVAL verdict: survived iff nobody died AND food never bottomed to 0
            //  for the whole colony AND no time-frozen invariant tripped.
            bool timeFrozen = false;
            foreach (var s in _timeline) foreach (var v in s.violations) if (v.StartsWith("TIME-FROZEN")) timeFrozen = true;
            bool survived = !anyDead && starveNames.Count == 0 && !timeFrozen;

            var issues = new List<string>();
            if (anyDead) issues.Add($"DEATH: {string.Join(",", deadNames)} died during the run");
            if (starveNames.Count > 0) issues.Add($"STARVATION: {string.Join(",", starveNames)} hit food=0");
            if (anyDowned) issues.Add("a pawn was DOWNED at some point");
            if (anyBreak) issues.Add("a pawn had a MENTAL BREAK (mood crash)");
            if (timeFrozen) issues.Add("game TIME FROZEN between snapshots (clock not advancing)");
            if (minSleep <= 1f) issues.Add($"SLEEP-CRASH: a pawn hit sleep={minSleep:F0}");
            if (totalViolations > 0) issues.Add($"{totalViolations} invariant violation(s) across the timeline");

            WriteMarkdown(survived, lastDay, issues, foodTrend,
                minFood, minSleep, minMood, endFood, endSleep, totalViolations);
            WriteJson(survived, lastDay, issues, foodTrend, totalViolations);

            string mdPath = $"{PlayLogDir}/{tag}.md";
            Debug.Log($"[LongPlay] play log -> {mdPath}");
            Debug.Log($"[LongPlay] DONE survived={survived.ToString().ToLowerInvariant()} days={lastDay} issues={issues.Count}");
        }

        private string FoodTrend()
        {
            if (_timeline.Count < 4) return "insufficient-data";
            int q = Mathf.Max(1, _timeline.Count / 4);
            float early = AvgFood(0, q);
            float late = AvgFood(_timeline.Count - q, _timeline.Count);
            float diff = late - early;
            if (diff > 5f) return $"RISING (+{diff:F0})";
            if (diff < -5f) return $"FALLING ({diff:F0}) — sustainability risk";
            return $"STABLE ({diff:+0;-0;0})";
        }
        private float AvgFood(int lo, int hi)
        {
            float sum = 0f; int n = 0;
            for (int i = lo; i < hi && i < _timeline.Count; i++)
                foreach (var ps in _timeline[i].pawns) { sum += ps.food; n++; }
            return n > 0 ? sum / n : 0f;
        }

        private void WriteMarkdown(bool survived, int lastDay, List<string> issues, string foodTrend,
            float minFood, float minSleep, float minMood, float endFood, float endSleep, int totalViolations)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# PawnSim Long-Play Survival Log — `{tag}`");
            sb.AppendLine();
            sb.AppendLine($"- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- Run duration: {durationSec:F0}s real ({TimeSpeed}x game speed)");
            sb.AppendLine($"- Snapshots: {_timeline.Count} (every ~{SnapshotIntervalSec:F0}s)");
            sb.AppendLine($"- In-game days reached: {lastDay}");
            sb.AppendLine();

            sb.AppendLine("## SURVIVAL VERDICT");
            sb.AppendLine();
            sb.AppendLine($"**Colony survived: {(survived ? "YES" : "NO")}**");
            sb.AppendLine();
            sb.AppendLine($"- Food trend: {foodTrend}");
            sb.AppendLine($"- Min food across run: {minFood:F0} | Min sleep: {minSleep:F0} | Min mood: {minMood:F0}");
            sb.AppendLine($"- End-of-run avg food: {endFood:F0} | avg sleep: {endSleep:F0}");
            sb.AppendLine($"- Total invariant violations: {totalViolations}");
            sb.AppendLine();
            if (issues.Count == 0) sb.AppendLine("No issues found — colony sustained itself around the built house.");
            else { sb.AppendLine("### Issues found"); foreach (var i in issues) sb.AppendLine($"- {i}"); }
            sb.AppendLine();

            sb.AppendLine("## PHASE 1 — House construction");
            sb.AppendLine();
            foreach (var st in _phase1Steps) sb.AppendLine($"- {st}");
            sb.AppendLine();

            sb.AppendLine("## PHASE 2 — Survival timeline");
            sb.AppendLine();
            sb.AppendLine("| t(s) | Day HH:MM | wood(Δ) | food(Δ) | meals(Δ) | stone(Δ) | pawn needs (food/sleep/mood · task) | violations |");
            sb.AppendLine("|------|-----------|---------|---------|----------|----------|-------------------------------------|------------|");
            foreach (var s in _timeline)
            {
                var pawnCol = new StringBuilder();
                foreach (var ps in s.pawns)
                {
                    if (pawnCol.Length > 0) pawnCol.Append("<br>");
                    string flag = ps.dead ? " ☠" : ps.downed ? " ⬇" : ps.inWall ? " ⛔" : ps.stuck ? " ⏳" : "";
                    pawnCol.Append($"{ps.name}: {ps.food:F0}/{ps.sleep:F0}/{ps.mood:F0} · {ps.task}{flag}");
                }
                string vio = s.violations.Count == 0 ? "-" : string.Join("<br>", s.violations);
                sb.AppendLine($"| {s.realT:F0} | {s.day} {s.hour:00}:{s.minute:00} | {s.wood}({Sign(s.dWood)}) | " +
                              $"{s.food}({Sign(s.dFood)}) | {s.meals}({Sign(s.dMeals)}) | {s.stone}({Sign(s.dStone)}) | " +
                              $"{pawnCol} | {vio} |");
            }
            sb.AppendLine();
            sb.AppendLine("## Screenshots");
            sb.AppendLine();
            foreach (var s in _timeline) sb.AppendLine($"- t={s.realT:F0}s -> `{s.screenshot}`");

            File.WriteAllText($"{PlayLogDir}/{tag}.md", sb.ToString());
        }

        private void WriteJson(bool survived, int lastDay, List<string> issues, string foodTrend, int totalViolations)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"tag\": \"{Esc(tag)}\",\n");
            sb.Append($"  \"durationSec\": {durationSec.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"timeSpeed\": {TimeSpeed.ToString(CultureInfo.InvariantCulture)},\n");
            sb.Append($"  \"survived\": {survived.ToString().ToLowerInvariant()},\n");
            sb.Append($"  \"daysReached\": {lastDay},\n");
            sb.Append($"  \"foodTrend\": \"{Esc(foodTrend)}\",\n");
            sb.Append($"  \"totalViolations\": {totalViolations},\n");
            sb.Append("  \"issues\": [");
            for (int i = 0; i < issues.Count; i++)
                sb.Append($"{(i > 0 ? ", " : "")}\"{Esc(issues[i])}\"");
            sb.Append("],\n");
            sb.Append("  \"phase1\": [");
            for (int i = 0; i < _phase1Steps.Count; i++)
                sb.Append($"{(i > 0 ? ", " : "")}\"{Esc(_phase1Steps[i])}\"");
            sb.Append("],\n");
            sb.Append("  \"timeline\": [\n");
            for (int si = 0; si < _timeline.Count; si++)
            {
                var s = _timeline[si];
                sb.Append("    {");
                sb.Append($"\"t\": {s.realT.ToString("F1", CultureInfo.InvariantCulture)}, ");
                sb.Append($"\"day\": {s.day}, \"hour\": {s.hour}, \"minute\": {s.minute}, ");
                sb.Append($"\"wood\": {s.wood}, \"food\": {s.food}, \"meals\": {s.meals}, ");
                sb.Append($"\"fineMeals\": {s.fineMeals}, \"stone\": {s.stone}, ");
                sb.Append("\"pawns\": [");
                for (int pi = 0; pi < s.pawns.Count; pi++)
                {
                    var ps = s.pawns[pi];
                    if (pi > 0) sb.Append(", ");
                    sb.Append("{");
                    sb.Append($"\"name\": \"{Esc(ps.name)}\", ");
                    sb.Append($"\"food\": {ps.food.ToString("F1", CultureInfo.InvariantCulture)}, ");
                    sb.Append($"\"sleep\": {ps.sleep.ToString("F1", CultureInfo.InvariantCulture)}, ");
                    sb.Append($"\"mood\": {ps.mood.ToString("F1", CultureInfo.InvariantCulture)}, ");
                    sb.Append($"\"hp\": {ps.hp}, \"dead\": {ps.dead.ToString().ToLowerInvariant()}, ");
                    sb.Append($"\"downed\": {ps.downed.ToString().ToLowerInvariant()}, ");
                    sb.Append($"\"sleeping\": {ps.sleeping.ToString().ToLowerInvariant()}, ");
                    sb.Append($"\"inWall\": {ps.inWall.ToString().ToLowerInvariant()}, ");
                    sb.Append($"\"stuck\": {ps.stuck.ToString().ToLowerInvariant()}, ");
                    sb.Append($"\"task\": \"{Esc(ps.task)}\"");
                    sb.Append("}");
                }
                sb.Append("], ");
                sb.Append("\"violations\": [");
                for (int vi = 0; vi < s.violations.Count; vi++)
                    sb.Append($"{(vi > 0 ? ", " : "")}\"{Esc(s.violations[vi])}\"");
                sb.Append("]");
                sb.Append(si < _timeline.Count - 1 ? "},\n" : "}\n");
            }
            sb.Append("  ]\n");
            sb.Append("}\n");
            File.WriteAllText($"{PlayLogDir}/{tag}.json", sb.ToString());
        }

        // ---------------------------------------------------------------- helpers
        private void Step(string s) { _phase1Steps.Add(s); Debug.Log($"[LongPlay] P1: {s}"); }
        private static string Sign(int v) => v >= 0 ? $"+{v}" : v.ToString();
        private static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
        private static string Trim(string s, int n) => s == null ? "" : (s.Length <= n ? s : s.Substring(0, n));
        private static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return sb.Length == 0 ? DefaultTag : sb.ToString();
        }
    }
}
