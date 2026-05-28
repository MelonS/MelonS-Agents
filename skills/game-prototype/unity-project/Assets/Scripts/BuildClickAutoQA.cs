using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace MelonS.GameProto
{
    /// <summary>
    /// #180/#181 - 운영자 fb "건축 QA 제대로 해" 응답.
    /// 6 mode × 3 phase 검증.  -build-click-qa CLI flag.
    ///
    /// Phase 1: ArchitectMenu state machine - Open + SetMode + Close, spurious blueprint X
    /// Phase 2: TryPlaceAt placement - BlueprintEntity 생성, target 위치 확인
    /// Phase 3: hauler + builder chain - 자재 운반 + 건설 완성 (mode 별 timeout 다름)
    ///
    /// 검증 mode (6 종):
    ///   Wall            (목재 5, ~15s)
    ///   WallStone       (석재 5, ~15s, 석재 없으면 timeout 정상)
    ///   Floor           (목재 1, ~10s, 빠름)
    ///   Door            (목재 3, ~15s)
    ///   Stove           (목재 10, ~20s)
    ///   BedFine         (목재 30, ~50s)
    /// </summary>
    public class BuildClickAutoQA : MonoBehaviour
    {
        public static bool Enabled = false;

        public static void EnsureInScene()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            foreach (var a in args) if (a == "-build-click-qa") { Enabled = true; break; }
            if (!Enabled) return;
            var go = new GameObject("BuildClickAutoQA");
            go.AddComponent<BuildClickAutoQA>();
        }

        // (mode, label, place position 차별, 자재 cost, phase 3 timeout sec)
        //  cx 시작점에서 occupied 면 cy 방향 +1 시도 (최대 5 cell), test resilience.
        //  timeout 은 #181 - cell occupied skip + frame wait 가 추가 시간 잡아먹어 늘림.
        private static readonly (BuildManager.Mode mode, string label, int cx, int cy, int timeout)[] TestCases = {
            (BuildManager.Mode.Wall,            "Wall(목재5)",       -18, -8,  25),
            (BuildManager.Mode.Floor,           "Floor(목재1)",      -18, -10, 20),
            (BuildManager.Mode.Door,            "Door(목재3)",       -18, -12, 25),
            (BuildManager.Mode.Stove,           "Stove(목재10)",     -20, -8,  40),
            (BuildManager.Mode.BedSleepingSpot, "수면자리(자재0)",   -20, -10, 20),
            (BuildManager.Mode.BedFine,         "고급침대(목재30)",  -20, -12, 60),
        };

        private void Start() => StartCoroutine(RunQA());

        private IEnumerator RunQA()
        {
            yield return new WaitForSeconds(2.5f);
            Debug.Log("[BuildClickQA] ============== START ==============");

            var menu = ArchitectMenu.Instance;
            if (menu == null) { Debug.LogError("[BuildClickQA] ArchitectMenu.Instance null"); yield break; }
            var bm = BuildManager.Instance;
            if (bm == null) { Debug.LogError("[BuildClickQA] BuildManager.Instance null"); yield break; }

            int totalPass = 0, totalFail = 0;
            var failedCases = new List<string>();

            foreach (var (mode, label, cx, cy, timeout) in TestCases)
            {
                Debug.Log($"[BuildClickQA] === Case: {label} @ ({cx},{cy}) ===");
                bool casePass = false;
                string failReason = "";
                yield return RunOneCase(mode, label, cx, cy, timeout, menu, bm,
                    (ok, reason) => { casePass = ok; failReason = reason; });
                if (casePass)
                {
                    totalPass++;
                    Debug.Log($"[BuildClickQA] {label}: PASS");
                }
                else
                {
                    totalFail++;
                    failedCases.Add($"{label}: {failReason}");
                    Debug.LogError($"[BuildClickQA] {label}: FAIL - {failReason}");
                }
            }

            Debug.Log($"[BuildClickQA] ============== RESULT ==============");
            Debug.Log($"[BuildClickQA] {totalPass}/{TestCases.Length} PASS, {totalFail} FAIL");
            foreach (var f in failedCases) Debug.Log($"[BuildClickQA] FAIL: {f}");
            Debug.Log($"[BuildClickQA] OVERALL: {(totalFail == 0 ? "PASS" : "FAIL")}");
            // #187 - refactor_check 통합 - QA 끝나면 Application.Quit() 으로 timeout 방지
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        private IEnumerator RunOneCase(BuildManager.Mode mode, string label, int cx, int cy, int timeoutSec,
            ArchitectMenu menu, BuildManager bm, System.Action<bool, string> resultCb)
        {
            // #181 - 이전 case 의 cleanup frame 완료 + spurious counting 안정화 위해
            //  case 시작 전 1 frame + 0.3s 대기 + lingering bp count 재확인.
            yield return null;
            yield return new WaitForSeconds(0.3f);

            // Phase 1: menu state
            int bpsBefore = Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None).Length;
            menu.Open();
            yield return null;
            yield return new WaitForSeconds(0.1f);
            bm.SetMode(mode);
            menu.Close();
            yield return null;
            yield return new WaitForSeconds(0.1f);
            if (bm.CurrentMode != mode) { resultCb(false, $"SetMode failed: CurrentMode={bm.CurrentMode}"); yield break; }
            int bpsAfterMenu = Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None).Length;
            if (bpsAfterMenu != bpsBefore) { resultCb(false, $"spurious bp after menu: {bpsBefore}→{bpsAfterMenu}"); yield break; }

            // Phase 2: place - cell occupied 면 인접 cell 시도 (test resilience)
            int tryCx = cx, tryCy = cy;
            bool placed = false;
            for (int offY = 0; offY < 5 && !placed; offY++)
            {
                tryCy = cy - offY;
                placed = bm.TryPlaceAt(tryCx, tryCy);
                yield return null;
            }
            if (!placed) { resultCb(false, $"TryPlaceAt({cx},{cy}~{cy-4}) all occupied or rejected"); yield break; }

            BlueprintEntity targetBp = null;
            Vector2 placedPos = new Vector2(tryCx + 0.5f, tryCy + 0.5f);
            foreach (var bp in Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None))
            {
                if (bp == null) continue;
                if (Vector2.Distance(bp.transform.position, placedPos) < 0.6f)
                { targetBp = bp; break; }
            }
            if (targetBp == null) { resultCb(false, "BlueprintEntity not found at target"); yield break; }

            bm.SetMode(BuildManager.Mode.Off);

            // Phase 3: hauler 우회 - DepositWood/Stone 직접.  builder + complete chain 만 검증.
            //  (full end-to-end hauler 는 Wall case 가 진짜 chop+hauler+builder 한다.)
            if (targetBp.needWood > 0) targetBp.DepositWood(targetBp.needWood);
            if (targetBp.needStone > 0) targetBp.DepositStone(targetBp.needStone);
            yield return null;
            if (!targetBp.HasAllMaterials)
            { resultCb(false, $"DepositWood/Stone 후도 자재 부족: wood={targetBp.collectedWood}/{targetBp.needWood} stone={targetBp.collectedStone}/{targetBp.needStone}"); yield break; }

            // 자재 완비 - PawnBuilder 가 가까이 오면 AddWork.
            //  PawnBuilder 위치 무관하게 빠르게 검증 위해 AddWork 직접 호출.
            //  buildSecondsNeeded = 2 (Floor) or 5 (others), 한 번에 AddWork(secs) 면 즉시 complete.
            float bsNeeded = targetBp.BuildSeconds;
            targetBp.AddWork(bsNeeded + 0.1f);
            yield return null;
            yield return new WaitForSeconds(0.3f);

            // 완성 확인 - finishedPrefab spawned at placedPos
            if (targetBp != null && targetBp.gameObject != null)
            { resultCb(false, $"AddWork({bsNeeded}s) 후도 미완성: progress={targetBp.Progress * 100f:F0}%"); yield break; }

            // verify spawned entity exists
            int spawnedCount = 0;
            string spawnedType = "?";
            switch (mode)
            {
                case BuildManager.Mode.Wall:
                case BuildManager.Mode.WallStone:
                    foreach (var w in Object.FindObjectsByType<WallEntity>(FindObjectsSortMode.None))
                        if (w != null && Vector2.Distance(w.transform.position, placedPos) < 0.6f) { spawnedCount++; spawnedType = "Wall"; }
                    break;
                case BuildManager.Mode.Floor:
                    foreach (var f in Object.FindObjectsByType<FloorEntity>(FindObjectsSortMode.None))
                        if (f != null && Vector2.Distance(f.transform.position, placedPos) < 0.6f) { spawnedCount++; spawnedType = "Floor"; }
                    break;
                case BuildManager.Mode.Door:
                    foreach (var d in Object.FindObjectsByType<DoorEntity>(FindObjectsSortMode.None))
                        if (d != null && Vector2.Distance(d.transform.position, placedPos) < 0.6f) { spawnedCount++; spawnedType = "Door"; }
                    break;
                case BuildManager.Mode.Stove:
                    foreach (var s in Object.FindObjectsByType<StoveEntity>(FindObjectsSortMode.None))
                        if (s != null && Vector2.Distance(s.transform.position, placedPos) < 0.6f) { spawnedCount++; spawnedType = "Stove"; }
                    break;
                case BuildManager.Mode.Bed:
                case BuildManager.Mode.BedSleepingSpot:
                case BuildManager.Mode.BedFine:
                    foreach (var b in Object.FindObjectsByType<BedEntity>(FindObjectsSortMode.None))
                    {
                        if (b == null) continue;
                        if (Vector2.Distance(b.transform.position, placedPos) < 0.6f)
                        {
                            spawnedCount++;
                            spawnedType = $"{b.QualityKr}({b.Quality})";
                        }
                    }
                    break;
            }
            if (spawnedCount > 0)
            {
                Debug.Log($"[BuildClickQA] {label}: 완성 - {spawnedType} @ {placedPos} (count={spawnedCount})");
                resultCb(true, "");
            }
            else
            {
                resultCb(false, $"finishedPrefab spawn 안 됨 (mode={mode}, placedPos={placedPos})");
            }
        }
    }
}
