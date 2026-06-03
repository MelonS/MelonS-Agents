using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 진단 전용(-pawndiag): 림 행동을 실제 빌드에서 계측해 버그를 데이터로 검출한다.
    ///  추측 금지(observe-don't-speculate) + 실제 경로 검증(verify-the-real-path).
    ///
    /// Phase A (일반 행동): 매 2s 전 림의 pos/activity/이동/needs 로그 → 정지/진동/작업불가 검출.
    /// Phase B (선택-림 처리): 첫 림을 ClickSelector 로 선택 → 실제 우클릭 경로(DiagnosticCommandMove)
    ///   로 이동 명령 → 0.5s 간격 추적 → 명령 목표 도달 여부 검증("선택된 림 처리 확실하게").
    ///
    /// GameManager.Start 가 -pawndiag 인자에서 부착.  로그는 [PAWNDIAG] 접두.  ~40s 후 종료.
    /// </summary>
    public class PawnDiagnostics : MonoBehaviour
    {
        private void Start() { StartCoroutine(Run()); }

        private static string Act(PawnEntity p)
        {
            var lbl = p.GetComponent<PawnNameLabel>();
            return lbl != null ? lbl.CurrentActivity : "?";
        }

        private IEnumerator Run()
        {
            Application.runInBackground = true;
            yield return new WaitForSeconds(1.0f);

            var pawns = Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None);
            Debug.Log($"[PAWNDIAG] === START pawns={pawns.Length} ===");

            // ── Phase A: 일반 행동 계측 (0~20s, 매 2s) ──
            //  각 림의 직전 위치 대비 변위를 보고 '정지(움직여야 하는데 안 움직임)'를 검출.
            var lastPos = new Dictionary<int, Vector3>();
            var stuckSec = new Dictionary<int, float>();
            foreach (var p in pawns) { lastPos[p.GetInstanceID()] = p.transform.position; stuckSec[p.GetInstanceID()] = 0f; }

            for (int t = 0; t < 10; t++)
            {
                yield return new WaitForSeconds(2.0f);
                foreach (var p in pawns)
                {
                    if (p == null) continue;
                    int id = p.GetInstanceID();
                    Vector3 pos = p.transform.position;
                    float moved = (pos - lastPos[id]).magnitude;
                    lastPos[id] = pos;
                    var mv = p.GetComponent<PawnMovement>();
                    bool moving = mv != null && mv.IsMoving;
                    float spd = mv != null ? mv.MoveSpeedScale : -1f;
                    string act = Act(p);
                    // '움직이려는데(moving=target 있음) 변위 거의 0' = 정지 의심.
                    if (moving && moved < 0.05f) stuckSec[id] += 2f; else stuckSec[id] = 0f;
                    string flag = stuckSec[id] >= 4f ? "  <<STUCK?>>" : "";
                    Debug.Log($"[PAWNDIAG] A t={t*2+2}s {p.PawnName} pos=({pos.x:F1},{pos.y:F1}) move2s={moved:F2} target={moving} spd={spd:F2} act={act}{flag}");
                }
            }

            // ── Phase B: 선택-림 수동이동 명령 검증 ──
            var sel = Object.FindFirstObjectByType<ClickSelector>();
            if (sel == null) { Debug.LogError("[PAWNDIAG] ClickSelector 없음 — Phase B skip"); }
            else
            {
                sel.DiagnosticInspectFirstPawn();
                var p = sel.DiagnosticSelected;
                if (p == null) { Debug.LogError("[PAWNDIAG] 선택 실패 — Phase B skip"); }
                else
                {
                    Vector3 start = p.transform.position;
                    // 도달 가능 가까운 목표(+6,0 우측).  맵 중앙 근처라 walkable 추정.
                    Vector2 tgt = new Vector2(start.x + 6f, start.y);
                    bool issued = sel.DiagnosticCommandMove(tgt);
                    Debug.Log($"[PAWNDIAG] B CMD-MOVE {p.PawnName} start=({start.x:F1},{start.y:F1}) -> tgt=({tgt.x:F1},{tgt.y:F1}) issued={issued}");
                    float best = 999f;
                    for (int k = 0; k < 24; k++)   // 12s
                    {
                        yield return new WaitForSeconds(0.5f);
                        if (p == null) break;
                        Vector3 pos = p.transform.position;
                        float dist = ((Vector2)pos - tgt).magnitude;
                        best = Mathf.Min(best, dist);
                        var mv = p.GetComponent<PawnMovement>();
                        Debug.Log($"[PAWNDIAG] B t={k*0.5f:F1}s pos=({pos.x:F1},{pos.y:F1}) distToTgt={dist:F2} target={(mv!=null&&mv.IsMoving)} manual={p.IsUnderManualControl} act={Act(p)}");
                        if (dist < 1.2f) { Debug.Log($"[PAWNDIAG] B REACHED at t={k*0.5f:F1}s"); break; }
                    }
                    string verdict = best < 1.2f ? "PASS(도달)" : (best < 3f ? "PARTIAL(근접)" : "FAIL(미도달)");
                    Debug.Log($"[PAWNDIAG] B RESULT bestDist={best:F2} -> {verdict}");
                }
            }

            // ── Phase C: 선택-림 벌목 명령 검증 (#33 원거리 벌목 / #38 선택림 작업) ──
            //  실제 ClickSelector 선택-나무-우클릭 경로와 동일: chopper.SetTreeTarget +
            //  ClearAllWorkTasks + ManualMoveUntil.  나무 도달 + HP 감소를 계측.
            if (sel != null && sel.DiagnosticSelected != null)
            {
                var p = sel.DiagnosticSelected;
                // 가장 가까운 미파괴 나무.
                TreeEntity tree = null; float bestD = float.MaxValue;
                foreach (var tr in Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None))
                {
                    if (tr == null || tr.IsDestroyed) continue;
                    float d = (tr.transform.position - p.transform.position).sqrMagnitude;
                    if (d < bestD) { bestD = d; tree = tr; }
                }
                if (tree == null) { Debug.Log("[PAWNDIAG] C 나무 없음 — skip"); }
                else
                {
                    var chopper = p.GetComponent<PawnChopper>();
                    float hp0 = tree.HpRatio;
                    if (chopper != null) chopper.SetTreeTarget(tree);
                    p.ManualMoveUntil = Time.time + 18f;
                    Debug.Log($"[PAWNDIAG] C CHOP-CMD {p.PawnName} -> tree@({tree.transform.position.x:F1},{tree.transform.position.y:F1}) hp0={hp0:F2} hasChopper={chopper!=null}");
                    float minDist = 999f; bool destroyed = false;
                    for (int k = 0; k < 36; k++)   // 18s
                    {
                        yield return new WaitForSeconds(0.5f);
                        if (p == null || tree == null) { destroyed = (tree == null); break; }
                        float dist = (tree.transform.position - p.transform.position).magnitude;
                        minDist = Mathf.Min(minDist, dist);
                        bool ch = chopper != null && chopper.HasTask;
                        Debug.Log($"[PAWNDIAG] C t={k*0.5f:F1}s dist={dist:F2} hasTask={ch} treeHp={tree.HpRatio:F2} act={Act(p)}");
                        if (tree.IsDestroyed) { destroyed = true; Debug.Log($"[PAWNDIAG] C TREE FELLED at t={k*0.5f:F1}s"); break; }
                    }
                    bool reached = minDist < 2.0f;   // 대각 stand-cell(√2)+footprint 허용
                    bool damaged = destroyed || (tree != null && tree.HpRatio < hp0 - 0.01f);
                    // damaged=True 면 림이 유효 stand-cell 에서 실제로 벌목한 것 → 도달 성공으로 본다.
                    string verdict = damaged ? "PASS(도달+벌목)"
                        : reached ? "PARTIAL(도달했으나 HP감소 안보임)"
                        : "FAIL(나무 미도달)";
                    Debug.Log($"[PAWNDIAG] C RESULT minDist={minDist:F2} damaged={damaged} -> {verdict}");
                }
            }

            // ── Phase D: 작업 중 림에 새 명령(override) 검증 (#60 시나리오 2) ──
            //  벌목 명령 → 1.5s 후 이동 명령 → 작업을 버리고 이동 목표로 가는지.
            if (sel != null && sel.DiagnosticSelected != null)
            {
                var p = sel.DiagnosticSelected;
                TreeEntity tree = null; float bestD = float.MaxValue;
                foreach (var tr in Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None))
                {
                    if (tr == null || tr.IsDestroyed) continue;
                    float d = (tr.transform.position - p.transform.position).sqrMagnitude;
                    if (d < bestD) { bestD = d; tree = tr; }
                }
                var chopper = p.GetComponent<PawnChopper>();
                if (tree != null && chopper != null)
                {
                    chopper.SetTreeTarget(tree); p.ManualMoveUntil = Time.time + 18f;
                    yield return new WaitForSeconds(1.5f);
                    bool wasChopping = chopper.HasTask;
                    // 작업 중 새 이동 명령(실제 우클릭 빈땅 경로).
                    Vector3 mp = p.transform.position;
                    Vector2 moveTgt = new Vector2(mp.x - 5f, mp.y);
                    sel.DiagnosticCommandMove(moveTgt);
                    Debug.Log($"[PAWNDIAG] D OVERRIDE wasChopping={wasChopping} chopperCleared={!chopper.HasTask} -> moveTgt=({moveTgt.x:F1},{moveTgt.y:F1})");
                    float minD = 999f;
                    for (int k = 0; k < 20; k++)
                    {
                        yield return new WaitForSeconds(0.5f);
                        if (p == null) break;
                        float dist = ((Vector2)p.transform.position - moveTgt).magnitude;
                        minD = Mathf.Min(minD, dist);
                        if (dist < 1.2f) break;
                    }
                    string verdict = minD < 1.2f ? "PASS(작업버리고 이동 도달)" : "FAIL(이동 미도달 — 작업에 묶임?)";
                    Debug.Log($"[PAWNDIAG] D RESULT minDist={minD:F2} chopperHasTask={chopper.HasTask} -> {verdict}");
                }
            }

            // ── Phase E: 선택 지속 검증 (#60 시나리오 3) ──
            //  림 재선택 후 입력 없이 6s — 선택이 저절로 풀리는지(currentSelection/IsSelected).
            if (sel != null)
            {
                sel.DiagnosticInspectFirstPawn();
                var p = sel.DiagnosticSelected;
                bool dropped = false;
                for (int k = 0; k < 12; k++)
                {
                    yield return new WaitForSeconds(0.5f);
                    if (sel.DiagnosticSelected != p)
                    {
                        dropped = true;
                        var cur = sel.DiagnosticSelected;
                        string curName = cur == null ? "null" : cur.PawnName;
                        Debug.Log($"[PAWNDIAG] E 선택 풀림 t={k*0.5f:F1}s (cur={curName})");
                        break;
                    }
                }
                string eVerdict = dropped ? "FAIL(선택 저절로 풀림)" : "PASS(선택 6s 유지)";
                Debug.Log($"[PAWNDIAG] E RESULT {eVerdict}");
            }

            // ── Phase F: 원거리/장애물 너머 이동 경로 검증 (#60 시나리오 4) ──
            //  정착지(중앙 좌측 ~(-5,0)) 벽 너머 먼 지점으로 이동 명령 → 경로 탐색 도달 검증.
            if (sel != null && sel.DiagnosticSelected != null)
            {
                var p = sel.DiagnosticSelected;
                Vector3 sp = p.transform.position;
                Vector2 far = new Vector2(-9f, 0f);   // 정착지/벽 영역 너머 — 우회 필요 가능
                sel.DiagnosticCommandMove(far);
                Debug.Log($"[PAWNDIAG] F PATH-CMD {p.PawnName} ({sp.x:F1},{sp.y:F1}) -> far=({far.x:F1},{far.y:F1})");
                float minD = 999f; float lastD = 999f; float stallSec = 0f; bool stalled = false;
                for (int k = 0; k < 40; k++)   // 20s
                {
                    yield return new WaitForSeconds(0.5f);
                    if (p == null) break;
                    float dist = ((Vector2)p.transform.position - far).magnitude;
                    minD = Mathf.Min(minD, dist);
                    if (Mathf.Abs(dist - lastD) < 0.05f) stallSec += 0.5f; else stallSec = 0f;
                    lastD = dist;
                    if (stallSec >= 4f && dist > 1.5f) { stalled = true;
                        Debug.Log($"[PAWNDIAG] F STALL t={k*0.5f:F1}s dist={dist:F2} (4s 정지 — 경로막힘 의심)"); break; }
                    if (dist < 1.5f) break;
                }
                string verdict = minD < 1.5f ? "PASS(도달)" : (stalled ? "FAIL(중간 정지/경로막힘)" : (minD < 4f ? "PARTIAL(근접)" : "FAIL(미도달)"));
                Debug.Log($"[PAWNDIAG] F RESULT minDist={minD:F2} -> {verdict}");
            }

            Debug.Log("[PAWNDIAG] === DONE ===");
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }
    }
}
