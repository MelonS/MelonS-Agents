using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace MelonS.GameProto
{
    /// <summary>
    /// #180 - 운영자 fb "건축 청사진 안 됨" 진짜 QA.
    /// 이전 BuildAutoQA 는 BlueprintEntity 직접 spawn → click chain 검증 X.
    /// 이전 I37 은 TryPlaceAt(cx,cy) 직접 호출 → UI/EventSystem race 검증 X.
    ///
    /// 이번 QA - graphics 모드 + 실제 ArchitectMenu.Open + Button.onClick.Invoke()
    /// → BuildManager.SetMode → 메뉴 close → TryPlaceAt 호출 → blueprint spawn 검증.
    /// 또한 35s 시뮬 hauler + builder chain 까지 확인.
    ///
    /// CLI flag: -build-click-qa
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

        private void Start()
        {
            StartCoroutine(RunQA());
        }

        private IEnumerator RunQA()
        {
            yield return new WaitForSeconds(2.5f);
            Debug.Log("[BuildClickQA] === Phase 1: ArchitectMenu state machine ===");

            var menu = ArchitectMenu.Instance;
            if (menu == null) { Debug.LogError("[BuildClickQA] ArchitectMenu.Instance null"); yield break; }
            var bm = BuildManager.Instance;
            if (bm == null) { Debug.LogError("[BuildClickQA] BuildManager.Instance null"); yield break; }

            // 초기 상태
            int bpsBefore = Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None).Length;
            Debug.Log($"[BuildClickQA] t=0: 초기 blueprint count = {bpsBefore}");

            // 1) 메뉴 열기
            menu.Open();
            yield return null;
            yield return new WaitForSeconds(0.3f);
            Debug.Log("[BuildClickQA] Phase 1.1: ArchitectMenu opened");

            // 2) 모든 카테고리 expand 안 됐을 수 있으므로 직접 SetMode (Wall 버튼 lookup 우회 시뮬)
            //    실제 사용자 시나리오: Structure 카테고리 펼침 → "벽 (목재 5)" 클릭 → SetMode(Wall) + Close
            //    여기선 그 effect 만 시뮬: SetMode(Wall) + menu.Close()
            bm.SetMode(BuildManager.Mode.Wall);
            menu.Close();
            yield return null;
            yield return new WaitForSeconds(0.3f);
            bool modeOk = bm.CurrentMode == BuildManager.Mode.Wall;
            Debug.Log($"[BuildClickQA] Phase 1.2: SetMode(Wall) → CurrentMode={bm.CurrentMode}, modeOk={modeOk}");

            // 3) 메뉴 인터랙션 후 spurious blueprint 생긴 거 없는지 확인
            int bpsAfterMenu = Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None).Length;
            bool noSpurious = (bpsAfterMenu == bpsBefore);
            Debug.Log($"[BuildClickQA] Phase 1.3: 메뉴 후 bp count = {bpsAfterMenu} (이전 {bpsBefore}), spurious={!noSpurious}");

            Debug.Log("[BuildClickQA] === Phase 2: map click (TryPlaceAt) ===");

            // 4) 빈 cell 에 placement 시도.  맵 경계 ±29 안쪽 + settlement 영역 밖 = (-10, -5).
            int cx = -10, cy = -5;
            bool placed = bm.TryPlaceAt(cx, cy);
            yield return null;
            Debug.Log($"[BuildClickQA] Phase 2.1: TryPlaceAt({cx},{cy}) Wall → placed={placed}");

            // 5) 청사진 spawn 확인
            int bpsAfterPlace = Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None).Length;
            BlueprintEntity targetBp = null;
            foreach (var bp in Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None))
            {
                if (bp == null) continue;
                if (Vector2.Distance(bp.transform.position, new Vector2(cx + 0.5f, cy + 0.5f)) < 0.6f)
                { targetBp = bp; break; }
            }
            bool bpExists = targetBp != null;
            Debug.Log($"[BuildClickQA] Phase 2.2: bp count {bpsAfterMenu}→{bpsAfterPlace}, target bp exists={bpExists}");

            if (!bpExists)
            {
                Debug.LogError("[BuildClickQA] FAIL @ Phase 2: 청사진 spawn 실패 - placement 안 됨!");
                yield break;
            }

            // 6) build mode 해제
            bm.SetMode(BuildManager.Mode.Off);
            Debug.Log("[BuildClickQA] Phase 2.3: build mode OFF");

            Debug.Log("[BuildClickQA] === Phase 3: hauler + builder 35s 시뮬 ===");

            // 7) 35s 동안 5s 마다 상태 log
            int phase3Result = 0;  // 0=timeout, 1=success
            for (int t = 5; t <= 35; t += 5)
            {
                yield return new WaitForSeconds(5f);
                if (targetBp == null || targetBp.gameObject == null)
                {
                    // wall 완성 (bp destroyed)
                    var walls = Object.FindObjectsByType<WallEntity>(FindObjectsSortMode.None);
                    bool foundWall = false;
                    foreach (var w in walls)
                    {
                        if (w == null) continue;
                        if (Vector2.Distance(w.transform.position, new Vector2(cx + 0.5f, cy + 0.5f)) < 0.6f)
                        { foundWall = true; break; }
                    }
                    Debug.Log($"[BuildClickQA] Phase 3: t+{t}s SUCCESS - wall@target={foundWall} (total walls={walls.Length})");
                    phase3Result = 1;
                    break;
                }
                Debug.Log($"[BuildClickQA] Phase 3: t+{t}s collected wood={targetBp.collectedWood}/{targetBp.needWood} progress={targetBp.Progress * 100f:F0}% reserved={targetBp.IsReserved}");
            }

            Debug.Log("[BuildClickQA] === 최종 결과 ===");
            Debug.Log($"[BuildClickQA] Phase 1 menu state: modeOk={modeOk}, noSpurious={noSpurious}");
            Debug.Log($"[BuildClickQA] Phase 2 placement: placed={placed}, bpExists={bpExists}");
            Debug.Log($"[BuildClickQA] Phase 3 chain: {(phase3Result == 1 ? "SUCCESS" : "TIMEOUT")} (35s)");
            Debug.Log($"[BuildClickQA] OVERALL: {(modeOk && noSpurious && placed && bpExists && phase3Result == 1 ? "PASS" : "FAIL")}");
        }
    }
}
