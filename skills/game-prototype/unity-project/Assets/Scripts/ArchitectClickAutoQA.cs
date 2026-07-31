using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// operator fb (2026-05-31) "건축 하위 메뉴가 아예 안 눌러짐" — REAL-CLICK QA.
    ///
    /// The existing BuildClickAutoQA drives the menu with button.onClick.Invoke(),
    /// which BYPASSES the GraphicRaycaster and therefore CANNOT detect the actual
    /// reported bug (clicks not REACHING the buttons because their targetGraphic was
    /// raycastTarget=false).  This harness fires GENUINE EventSystem pointer clicks:
    ///   1. GraphicRaycaster.Raycast a PointerEventData at the button's screen-space
    ///      center → assert the raycast HITS the button GO (proves it is clickable).
    ///   2. ExecuteEvents.Execute<IPointerClickHandler>(hitGO, ...) → fire the real
    ///      click handler chain.
    /// Then asserts the observable state the operator cares about:
    ///   - clicking "지시 (Orders)" header EXPANDS it (header glyph ▶ → ▼),
    ///   - clicking the 채광 item ENTERS MineDesignation mode (ModeActive==true),
    ///   - NO standalone 채광/경작/해체 buttons remain on any canvas,
    ///   - info panel HIDDEN when nothing is selected.
    /// Captures screenshots and logs a single OVERALL PASS/FAIL line the harness greps.
    ///
    /// Activated by the -architect-click-qa CLI flag.  Needs a GRAPHICS build (the
    /// GraphicRaycaster + ScreenCapture require a real screen) — run WITHOUT
    /// -nographics.  Sets Application.runInBackground=true (bug-pattern #9 firewall)
    /// so the coroutine's WaitForSeconds never freezes on focus loss during a CLI
    /// launch.
    /// </summary>
    public class ArchitectClickAutoQA : MonoBehaviour
    {
        public static bool Enabled = false;
        private string shotDir = "G:/ai/_archfix";

        public static void EnsureInScene()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string dir = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-architect-click-qa") Enabled = true;
                if (args[i] == "-archqa-shotdir" && i + 1 < args.Length) dir = args[i + 1];
            }
            if (!Enabled) return;
            var go = new GameObject("ArchitectClickAutoQA");
            var c = go.AddComponent<ArchitectClickAutoQA>();
            if (!string.IsNullOrEmpty(dir)) c.shotDir = dir;
        }

        private int pass, fail;
        private readonly List<string> fails = new List<string>();

        private void Assert(bool cond, string what)
        {
            if (cond) { pass++; Debug.Log($"[ArchitectClickQA] PASS: {what}"); }
            else { fail++; fails.Add(what); Debug.LogError($"[ArchitectClickQA] FAIL: {what}"); }
        }

        private void Start()
        {
            // firewall #9 — CLI QA path: keep ticking when the window loses OS focus.
            Application.runInBackground = true;
            if (!Directory.Exists(shotDir)) Directory.CreateDirectory(shotDir);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(2.5f);
            Debug.Log("[ArchitectClickQA] ============== START (REAL pointer clicks) ==============");

            // ── #3 info-panel hidden when nothing selected (no-selection frame) ──
            var infoPanel = Object.FindFirstObjectByType<PawnInfoPanel>();
            // The panelBg is a child Image; the panel root component is on the panel
            //  GO.  Read panelBg.enabled via reflection-free public surface: we check
            //  any Image named "...Bg/Panel" under the PawnInfoPanel that is enabled.
            yield return new WaitForSeconds(0.3f);
            bool infoVisibleAtRest = InfoPanelLooksVisible(infoPanel);
            Assert(!infoVisibleAtRest, "info panel HIDDEN when nothing selected");
            Shot("01_nosel_info_hidden");

            // ── no standalone 채광/경작/해체 buttons remain ──
            bool foundStandalone = false;
            string standaloneName = "";
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (b == null) continue;
                string n = b.gameObject.name;
                if (n == "Btn_채광" || n == "Btn_경작" || n == "Btn_해체")
                {
                    foundStandalone = true; standaloneName = n; break;
                }
            }
            Assert(!foundStandalone, $"no standalone designation buttons (found: {(foundStandalone ? standaloneName : "none")})");

            var menu = ArchitectMenu.Instance;
            if (menu == null) { Assert(false, "ArchitectMenu.Instance present"); Finish(); yield break; }

            // ── open the architect menu via a REAL click on GuiControlBar 건축 ──
            if (!menu.gameObject.activeSelf)
            {
                bool opened = RealClickByName("Btn_건축");
                Assert(opened, "REAL click hit Btn_건축 (architect open button)");
                yield return null;
                yield return new WaitForSeconds(0.4f);
            }
            Assert(menu.gameObject.activeSelf, "ArchitectMenu opened after real 건축 click");
            Shot("02_menu_open");

            // ── REAL click on "지시 (Orders)" header → expand ──
            // Ensure starting collapsed: header glyph should be ▶.
            bool ordersOpenBefore = HeaderExpanded("지시 (Orders)", menu.transform);
            if (ordersOpenBefore)
            {
                // collapse first so the test proves an EXPAND transition
                RealClickHeader("지시 (Orders)", menu.transform);
                yield return null; yield return new WaitForSeconds(0.3f); yield return null;
            }
            bool ordersHit = RealClickHeader("지시 (Orders)", menu.transform);
            Assert(ordersHit, "REAL click HIT '지시 (Orders)' category header");
            yield return null;
            yield return new WaitForSeconds(0.3f);
            yield return null; // RefreshContent rebuilt children
            bool ordersExpanded = HeaderExpanded("지시 (Orders)", menu.transform);
            Assert(ordersExpanded, "'지시 (Orders)' EXPANDED after real header click (▶ → ▼)");
            Shot("03_orders_expanded");

            // ── REAL click on the 채광 item → MineDesignation mode active ──
            // Make sure mine mode starts OFF so we prove a real activation.
            if (MineDesignation.Instance != null) MineDesignation.Instance.SetMode(false);
            yield return null;
            bool mineItemHit = RealClickItemContaining("채광", menu.transform);
            Assert(mineItemHit, "REAL click HIT the 채광 (Mine) order item");
            yield return null;
            yield return new WaitForSeconds(0.3f);
            bool mineActive = MineDesignation.Instance != null && MineDesignation.Instance.ModeActive;
            Assert(mineActive, "MineDesignation mode ACTIVE after real 채광 item click");
            Shot("04_mine_mode_active");

            Finish();
        }

        private void Finish()
        {
            Debug.Log("[ArchitectClickQA] ============== RESULT ==============");
            Debug.Log($"[ArchitectClickQA] {pass} PASS / {fail} FAIL");
            foreach (var f in fails) Debug.Log($"[ArchitectClickQA] FAIL: {f}");
            Debug.Log($"[ArchitectClickQA] OVERALL: {(fail == 0 ? "PASS" : "FAIL")}");
            StartCoroutine(QuitSoon());
        }

        private IEnumerator QuitSoon()
        {
            yield return new WaitForSeconds(1.2f);
            Application.Quit();
        }

        // ── info-panel visibility probe ──────────────────────────────────────
        //  "Visible" = the panel's background/border Images are enabled AND the GO
        //  active.  We look for an enabled Image on a descendant whose name hints at
        //  the panel frame; if any frame graphic is enabled, the panel is showing.
        private bool InfoPanelLooksVisible(PawnInfoPanel panel)
        {
            if (panel == null) return false;
            var imgs = panel.GetComponentsInChildren<Image>(false); // false = only enabled-GO subtree
            foreach (var img in imgs)
            {
                if (img == null) continue;
                if (!img.enabled) continue;
                if (!img.gameObject.activeInHierarchy) continue;
                // any enabled, on-screen frame/fill image counts as "visible"
                return true;
            }
            return false;
        }

        // ── REAL pointer-click helpers (GraphicRaycaster + ExecuteEvents) ─────
        private bool RealClickByName(string goName)
        {
            var buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            foreach (var b in buttons)
            {
                if (b == null) continue;
                if (b.gameObject.name == goName)
                    return RealClickRect(b.GetComponent<RectTransform>(), b.gameObject);
            }
            return false;
        }

        private bool RealClickHeader(string category, Transform menuRoot)
        {
            var btn = FindHeaderButton(category, menuRoot);
            if (btn == null) return false;
            return RealClickRect(btn.GetComponent<RectTransform>(), btn.gameObject);
        }

        // #콜로니심파리티(2026-06-11) — 메뉴가 아코디언(▶/▼ 텍스트 마커)에서 '카테고리
        //  2열 + 아이콘 셸프'로 바뀜.  헤더 = "Cat_" 버튼(텍스트 마커 없음), 항목 = 셸프
        //  "Cell_" 버튼(이름이 NameStrip 자식 Text 에 있음 — 글리프 Text 가 먼저 올 수
        //  있어 전체 Text 를 훑는다).  펼침 상태는 ArchitectMenu.ActiveCategory 로 판정.
        private bool RealClickItemContaining(string labelSub, Transform menuRoot)
        {
            var buttons = menuRoot.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b == null) continue;
                if (b.gameObject.name.StartsWith("Cat_")) continue;   // 카테고리 버튼 제외
                foreach (var txt in b.GetComponentsInChildren<Text>(true))
                {
                    if (txt != null && txt.text.Contains(labelSub))
                        return RealClickRect(b.GetComponent<RectTransform>(), b.gameObject);
                }
            }
            return false;
        }

        private Button FindHeaderButton(string category, Transform menuRoot)
        {
            // category 인자는 풀 키("구조 (Structure)") 또는 한글명 — 괄호 안 한글을 추출해
            //  카테고리 버튼 텍스트(한글 전용)와 대조.
            string kr = category;
            int a = category.IndexOf('('); int z = category.IndexOf(')');
            if (a >= 0 && z > a) kr = category.Substring(a + 1, z - a - 1).Trim();
            var buttons = menuRoot.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                if (b == null || !b.gameObject.name.StartsWith("Cat_")) continue;
                var txt = b.GetComponentInChildren<Text>(true);
                if (txt != null && txt.text.Contains(kr))
                    return b;
            }
            return null;
        }

        private bool HeaderExpanded(string category, Transform menuRoot)
        {
            return ArchitectMenu.Instance != null
                   && ArchitectMenu.Instance.ActiveCategory == category;
        }

        /// <summary>The load-bearing check: do a GENUINE GraphicRaycaster raycast at
        /// the target's screen-space center and confirm the raycast actually HITS the
        /// target's GO subtree (this is what proves raycastTarget/raycaster wiring is
        /// correct — a button with no raycastable graphic would NOT be hit).  Then
        /// fire the real pointerClick handler on the hit object.</summary>
        private bool RealClickRect(RectTransform rt, GameObject target)
        {
            if (rt == null || target == null) return false;
            if (EventSystem.current == null)
            {
                Debug.LogError("[ArchitectClickQA] no EventSystem in scene");
                return false;
            }

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            // ScreenSpaceOverlay: world corners ARE screen pixels.
            Vector2 center = new Vector2(
                (corners[0].x + corners[2].x) * 0.5f,
                (corners[0].y + corners[2].y) * 0.5f);

            var ped = new PointerEventData(EventSystem.current) { position = center };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);

            // Confirm the FIRST (topmost) raycast hit belongs to the target subtree —
            //  i.e. the click genuinely reaches the button, nothing overlays it.
            bool hitTarget = false;
            GameObject topHit = results.Count > 0 ? results[0].gameObject : null;
            foreach (var r in results)
            {
                if (r.gameObject == null) continue;
                if (r.gameObject == target || r.gameObject.transform.IsChildOf(target.transform)
                    || target.transform.IsChildOf(r.gameObject.transform))
                {
                    hitTarget = true;
                    break;
                }
            }
            if (!hitTarget)
            {
                Debug.LogError($"[ArchitectClickQA] raycast at {center} did NOT reach '{target.name}'. " +
                               $"topHit={(topHit != null ? topHit.name : "<none>")}, results={results.Count}");
                return false;
            }

            // Fire the genuine pointer-click handler chain.  ExecuteHierarchy walks
            //  UP from the hit object to the first ancestor implementing
            //  IPointerClickHandler — exactly what Unity's input module does when the
            //  raycast lands on a non-handler child (e.g. a Button's Label graphic).
            //  (Plain Execute on the leaf would NOT bubble to the Button.)
            ped.pointerPress = topHit;
            ped.pointerCurrentRaycast = results[0];
            ped.pressPosition = center;
            var handled = ExecuteEvents.ExecuteHierarchy(topHit, ped, ExecuteEvents.pointerClickHandler);
            Debug.Log($"[ArchitectClickQA] REAL click reached '{target.name}' (topHit '{topHit.name}', handler '{(handled != null ? handled.name : "<none>")}') @ {center}");
            return true;
        }

        private void Shot(string name)
        {
            try
            {
                string p = Path.Combine(shotDir, name + ".png");
                ScreenCapture.CaptureScreenshot(p);
                Debug.Log($"[ArchitectClickQA] shot -> {p}");
            }
            catch (System.Exception e) { Debug.LogWarning($"[ArchitectClickQA] shot fail: {e.Message}"); }
        }
    }
}
