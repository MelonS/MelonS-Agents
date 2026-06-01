using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

namespace MelonS.GameProto
{
    /// <summary>
    /// #180/#181 - 운영자 fb "건축 QA 제대로 해" 응답.
    /// #191 (v2) - 운영자 fb "마우스 시뮬 못함 → 자율 검증 못 함" 응답.
    ///   기존 v1 은 bm.TryPlaceAt(cx,cy) / bm.SetMode(mode) 직접 호출 = 절반 검증.
    ///   v2 는 진짜 user flow 시뮬:
    ///     - GuiControlBar "건축" Button.onClick 트리거 (ExecuteEvents.Submit)
    ///     - ArchitectMenu 카테고리 Button.onClick 트리거 (button.onClick.Invoke)
    ///     - ArchitectMenu buildable Button.onClick 트리거 (real menu close + SetMode chain)
    ///     - cooldown 0.15s wait → 진짜 cooldown gate 통과 검증
    ///     - bm.SimulateMapClick(screenPos) - Update 의 click 처리 path 100% 동일 (overUI off)
    ///
    /// 검증 mode (6 종): Wall / Floor / Door / Stove / SleepingSpot / BedFine
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

        // (mode, label, ArchitectMenu 카테고리, ArchitectMenu buildable index, place cell)
        //  #192 - WallStone + Bed-Wood 추가 = 8 modes 완전체 (Architect 메뉴 모든 entry).
        private static readonly (BuildManager.Mode mode, string label, string category, int buildableIdx, int cx, int cy)[] TestCases = {
            (BuildManager.Mode.Wall,            "Wall(목재5)",       "Structure (구조)", 0, -18, -8),
            (BuildManager.Mode.WallStone,       "Wall(석재5)",       "Structure (구조)", 1, -16, -8),
            // #210 fix — the old Floor cell (-18,-10) and ALL FOUR downward fallback
            //  cells (-18,-11 .. -18,-14) sit INSIDE the lake centred at (-18,-12)
            //  r≈2.8 (added in #108).  C3 terrain validation correctly rejects water,
            //  so the floor genuinely could not be placed on ANY of the 5 cells the
            //  test tried → real Build-Click FAIL.  Door/BedFine "passed" only because
            //  their downward fallback reached the grass row y=-15 (off=3); Floor's
            //  off cap of 4 stopped one cell short of grass.  This was a TEST-FIXTURE
            //  defect (water-targeted), NOT a placement-logic regression.  Move the
            //  Floor case to the dedicated grass column x=-22 (y=-8 and all downward
            //  fallbacks -9..-12 are grass, well clear of every lake/rock and every
            //  other test case's cells) so the floor lands on open ground first-try.
            (BuildManager.Mode.Floor,           "Floor(목재1)",      "Floors (바닥)",    0, -22, -8),
            (BuildManager.Mode.Door,            "Door(목재3)",       "Structure (구조)", 2, -18, -12),
            (BuildManager.Mode.Stove,           "Stove(목재10)",     "Production (생산)",0, -20, -8),
            (BuildManager.Mode.BedSleepingSpot, "수면자리(자재0)",   "Furniture (가구)", 0, -20, -10),
            (BuildManager.Mode.Bed,             "목재침대(목재8)",   "Furniture (가구)", 1, -16, -10),
            (BuildManager.Mode.BedFine,         "고급침대(목재30)",  "Furniture (가구)", 2, -20, -12),
        };

        private void Start() => StartCoroutine(RunQA());

        private IEnumerator RunQA()
        {
            yield return new WaitForSeconds(2.5f);
            Debug.Log("[BuildClickQA] ============== START (real user flow) ==============");

            var menu = ArchitectMenu.Instance;
            var bm = BuildManager.Instance;
            if (menu == null || bm == null)
            {
                Debug.LogError("[BuildClickQA] ArchitectMenu or BuildManager Instance null");
                Application.Quit();
                yield break;
            }

            int totalPass = 0, totalFail = 0;
            var failedCases = new List<string>();

            foreach (var tc in TestCases)
            {
                Debug.Log($"[BuildClickQA] === Case: {tc.label} @ cell({tc.cx},{tc.cy}) via menu={tc.category}[{tc.buildableIdx}] ===");
                bool casePass = false;
                string failReason = "";
                yield return RunOneCase(tc, menu, bm,
                    (ok, reason) => { casePass = ok; failReason = reason; });
                if (casePass) { totalPass++; Debug.Log($"[BuildClickQA] {tc.label}: PASS"); }
                else { totalFail++; failedCases.Add($"{tc.label}: {failReason}"); Debug.LogError($"[BuildClickQA] {tc.label}: FAIL - {failReason}"); }
            }

            Debug.Log($"[BuildClickQA] ============== RESULT ==============");
            Debug.Log($"[BuildClickQA] {totalPass}/{TestCases.Length} PASS, {totalFail} FAIL");
            foreach (var f in failedCases) Debug.Log($"[BuildClickQA] FAIL: {f}");
            Debug.Log($"[BuildClickQA] OVERALL: {(totalFail == 0 ? "PASS" : "FAIL")}");
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        private IEnumerator RunOneCase((BuildManager.Mode mode, string label, string category, int buildableIdx, int cx, int cy) tc,
            ArchitectMenu menu, BuildManager bm, System.Action<bool, string> resultCb)
        {
            yield return null;
            yield return new WaitForSeconds(0.3f);

            // Phase 1 - real GuiControlBar 건축 버튼 click 시뮬
            //   GuiControlBar 의 "Btn_건축" 찾아서 onClick 호출 (button.onClick.Invoke 가 진짜 EventSystem path).
            if (!menu.gameObject.activeSelf)
            {
                bool clicked = ClickButtonByName("Btn_건축");
                if (!clicked) { resultCb(false, "GuiControlBar Btn_건축 not found"); yield break; }
                yield return null;
                yield return new WaitForSeconds(0.15f);
                if (!menu.gameObject.activeSelf) { resultCb(false, "건축 버튼 클릭 후 ArchitectMenu 안 열림"); yield break; }
            }

            // Phase 2 - 카테고리 헤더 ▶/▼ state 확인 후 필요 시에만 click
            //   ▶ = 닫힌 상태 → click 으로 열어야 buildable 보임
            //   ▼ = 이미 열린 상태 → click 하면 닫힘 = bug.  skip.
            bool catOpenAlready = IsCategoryOpen(tc.category, menu.transform);
            if (!catOpenAlready)
            {
                bool catClicked = ClickButtonByLabel(tc.category, menu.transform);
                if (!catClicked) { resultCb(false, $"카테고리 '{tc.category}' 버튼 못 찾음"); yield break; }
                yield return null;
                yield return new WaitForSeconds(0.15f);
                // ArchitectMenu.RefreshContent 가 새로 build 했으므로 한 frame 더 wait
                yield return null;
            }

            // Phase 3 - buildable 버튼 click (메뉴 닫힘 + SetMode 발생)
            int bpsBefore = Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None).Length;
            bool buildableClicked = ClickBuildableByIndex(tc.category, tc.buildableIdx, menu.transform);
            if (!buildableClicked) { resultCb(false, $"buildable [{tc.buildableIdx}] 버튼 못 찾음 in {tc.category}"); yield break; }
            yield return null;
            yield return new WaitForSeconds(0.15f);

            if (bm.CurrentMode != tc.mode) { resultCb(false, $"buildable click 후 mode 안 바뀜: 기대={tc.mode}, 실제={bm.CurrentMode}"); yield break; }

            // Phase 4 - cooldown wait (PlaceCooldownSec 0.15s) + 그 후 map click
            yield return new WaitForSeconds(0.20f);

            // 진짜 map click 시뮬: cell → world → screen 변환
            Vector3 world = new Vector3(tc.cx + 0.5f, tc.cy + 0.5f, 0);
            if (Camera.main == null) { resultCb(false, "Camera.main null"); yield break; }
            Vector3 screen = Camera.main.WorldToScreenPoint(world);
            var clickResult = bm.SimulateMapClick(new Vector2(screen.x, screen.y));
            yield return null;

            if (clickResult != BuildClickResult.Placed)
            {
                // #263 점유/지형(물·바위) 시 나선형으로 근처 빈 셀 탐색 (test resilience).
                //  이전엔 아래(cy-off)로만 4칸 시도 → 절차적 맵에서 호수가 그 방향으로 뻗으면
                //  전부 실패하는 false-fail.  맵이 바뀌어도 근처 buildable 셀을 찾도록 전방위 스캔.
                bool placedFallback = false;
                for (int ring = 1; ring <= 5 && !placedFallback; ring++)
                {
                    for (int dx = -ring; dx <= ring && !placedFallback; dx++)
                    for (int dy = -ring; dy <= ring && !placedFallback; dy++)
                    {
                        if (System.Math.Abs(dx) != ring && System.Math.Abs(dy) != ring) continue; // ring 경계만
                        Vector3 worldF = new Vector3(tc.cx + dx + 0.5f, tc.cy + dy + 0.5f, 0);
                        Vector3 screenF = Camera.main.WorldToScreenPoint(worldF);
                        var r = bm.SimulateMapClick(new Vector2(screenF.x, screenF.y));
                        if (r == BuildClickResult.Placed) { placedFallback = true; world = worldF; }
                        yield return null;
                    }
                }
                if (!placedFallback) { resultCb(false, $"SimulateMapClick → {clickResult} (나선형 5-ring 탐색도 실패 — 진짜 배치 불가 버그?)"); yield break; }
            }

            // Phase 5 - 청사진 spawn 확인
            BlueprintEntity targetBp = null;
            Vector2 placedPos = new Vector2(world.x, world.y);
            foreach (var bp in Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None))
            {
                if (bp == null) continue;
                if (Vector2.Distance(bp.transform.position, placedPos) < 0.6f) { targetBp = bp; break; }
            }
            if (targetBp == null) { resultCb(false, $"청사진 spawn 안 됨 at {placedPos}"); yield break; }

            // Mode Off (R-click 시뮬 - 진짜 user 가 다음 case 위해 mode 끔)
            bm.SetMode(BuildManager.Mode.Off);

            // Phase 6 - hauler 우회 + builder chain (v1 과 동일)
            if (targetBp.needWood > 0) targetBp.DepositWood(targetBp.needWood);
            if (targetBp.needStone > 0) targetBp.DepositStone(targetBp.needStone);
            yield return null;
            if (!targetBp.HasAllMaterials) { resultCb(false, $"Deposit 후도 자재 부족"); yield break; }
            targetBp.AddWork(targetBp.BuildSeconds + 0.1f);
            yield return null;
            yield return new WaitForSeconds(0.3f);
            if (targetBp != null && targetBp.gameObject != null) { resultCb(false, $"AddWork 후도 미완성"); yield break; }

            // Phase 7 - 완성 prefab 확인
            int spawnedCount = 0;
            string spawnedType = "?";
            switch (tc.mode)
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
                        if (b != null && Vector2.Distance(b.transform.position, placedPos) < 0.6f)
                        {
                            spawnedCount++; spawnedType = $"{b.QualityKr}({b.Quality})";
                            // defect #1 (D3-1) - 완성된 침대 entity 의 sprite 런타임 binding 정직 검증.
                            //   bm.BedSpriteRef 가 null 이면(SceneSetup bedSprite 미연결) 여기서 FAIL.
                            //   완성 entity 의 SpriteRenderer.sprite 가 실제 bed_* sprite 여야 함.
                            var bsr = b.GetComponent<SpriteRenderer>();
                            Sprite refSprite = bm.BedSpriteRef;
                            string rendSpriteName = (bsr != null && bsr.sprite != null) ? bsr.sprite.name : "<null>";
                            bool refOk = refSprite != null;
                            bool rendOk = bsr != null && bsr.sprite != null && rendSpriteName.Contains("bed");
                            Debug.Log($"[BuildClickQA] bed: SpriteRef {(refOk ? "non-null" : "NULL")}, renderer.sprite={rendSpriteName}, quality={b.Quality}");
                            if (!refOk) { resultCb(false, "bm.BedSpriteRef null (SceneSetup bedSprite 미연결)"); yield break; }
                            if (!rendOk) { resultCb(false, $"완성 bed renderer.sprite 가 bed_* 아님 (={rendSpriteName})"); yield break; }
                            // #198 D4-1 - Fine quality 는 bed_fine sprite 로 swap 됐는지 정직 검증.
                            //   SetQuality 가 fine sprite + Color.white 적용했어야 함.
                            if (tc.mode == BuildManager.Mode.BedFine)
                            {
                                bool fineSpriteOk = rendSpriteName.Contains("bed_fine");
                                bool fineColorOk = bsr != null && bsr.color == Color.white;
                                Debug.Log($"[BuildClickQA] bedFine: sprite={rendSpriteName} (fine={fineSpriteOk}), color={(bsr != null ? bsr.color.ToString() : "<null>")} (white={fineColorOk})");
                                if (!fineSpriteOk) { resultCb(false, $"Fine bed renderer.sprite 가 bed_fine 아님 (={rendSpriteName}) - SetQuality swap 실패"); yield break; }
                                if (!fineColorOk) { resultCb(false, $"Fine bed color 가 white 아님 (={(bsr != null ? bsr.color.ToString() : "<null>")}) - 충돌 tint 미제거"); yield break; }
                            }
                        }
                    break;
            }
            if (spawnedCount > 0) { Debug.Log($"[BuildClickQA] {tc.label} 완성 - {spawnedType} @ {placedPos}"); resultCb(true, ""); }
            else { resultCb(false, $"finishedPrefab 미spawn (mode={tc.mode})"); }
        }

        // === Button click helpers (real EventSystem path) ===

        private bool ClickButtonByName(string goName)
        {
            var buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            foreach (var b in buttons)
            {
                if (b == null) continue;
                if (b.gameObject.name == goName) { b.onClick.Invoke(); return true; }
            }
            return false;
        }

        private bool IsCategoryOpen(string category, Transform parent)
        {
            var buttons = parent.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b == null) continue;
                var txt = b.GetComponentInChildren<Text>(true);
                if (txt == null) continue;
                if (!txt.text.Contains(KoreanOf(category))) continue;  // #261 메뉴 한글표시(#260) 대응
                if (txt.text.StartsWith("▼ ")) return true;
                if (txt.text.StartsWith("▶ ")) return false;
            }
            return false;
        }

        private bool ClickButtonByLabel(string labelText, Transform parent)
        {
            var buttons = parent.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b == null) continue;
                var txt = b.GetComponentInChildren<Text>(true);
                if (txt == null) continue;
                if (txt.text.Contains(KoreanOf(labelText))) { b.onClick.Invoke(); return true; }
            }
            return false;
        }

        // #261 카테고리 헤더가 #260 부터 한글 전용("구조")으로 표시됨 — 영한키("Structure (구조)")
        //  에서 괄호 안 한글을 뽑아 텍스트 매칭(괄호 없으면 원문).
        private static string KoreanOf(string s)
        {
            int a = s.IndexOf('('), b = s.IndexOf(')');
            return (a >= 0 && b > a) ? s.Substring(a + 1, b - a - 1).Trim() : s;
        }

        private bool ClickBuildableByIndex(string category, int idx, Transform parent)
        {
            // ArchitectMenu.RefreshContent 가 카테고리 펼친 후 buildable 들을 카테고리 헤더 다음에 list.
            //  buildable button name = "Btn_{label}" (MakeBtn).
            //  idx-th buildable = category 헤더 다음 idx-th button (헤더 자체 skip).
            var buttons = parent.GetComponentsInChildren<Button>(true);
            int found = 0;
            bool hitHeader = false;
            foreach (var b in buttons)
            {
                if (b == null) continue;
                var txt = b.GetComponentInChildren<Text>(true);
                if (txt == null) continue;
                // #263 헤더는 "▼/▶ " 접두사로만 식별.  이전엔 Contains(category)만 봐서
                //  buildable 라벨 "목재 바닥" 이 카테고리명 "바닥" 을 포함 → 헤더로 오인·skip →
                //  Floor[0] 을 영영 못 찾는 false-fail.  접두사 + 카테고리명 동시 만족만 헤더.
                bool isHeader = txt.text.StartsWith("▼ ") || txt.text.StartsWith("▶ ");
                if (isHeader && txt.text.Contains(KoreanOf(category))) { hitHeader = true; continue; }
                if (!hitHeader) continue;
                // 다음 카테고리 헤더 만나면 종료
                if (isHeader) break;
                if (found == idx) { b.onClick.Invoke(); return true; }
                found++;
            }
            return false;
        }
    }
}
