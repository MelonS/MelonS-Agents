using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 피드백 2026-05-27 "프로토타입 수준도 안됨":
    /// 마우스 hover 시 그 entity 가 뭔지 + 우클릭 시 어떤 action 일어나는지 표시.
    /// 사용자가 "이게 뭐고 뭐 할 수 있지" 알 수 있게.
    ///
    /// 매 프레임 mouse pos → OverlapPoint → entity 종류 식별 → 짧은 한국어 텍스트.
    /// UI Canvas 위 작은 패널, mouse 옆 (offset 14px).
    /// </summary>
    public class HoverTooltip : MonoBehaviour
    {
        private static HoverTooltip _instance;
        public static HoverTooltip Instance => _instance;
        private RectTransform rt;
        private Image bg;
        private Text label;
        private Camera mainCam;
        private Canvas canvas;

        // ── UI-driven override (v3 audit: Architect menu rows) ──────────────
        //   The world-hover path below early-returns + Hide()s whenever the
        //   pointer is over a UI GameObject (so a world tooltip never covers a
        //   button).  But menu ROWS are themselves UI — they need a tooltip too
        //   (자재/비용).  ArchitectMenu pushes its row text here via ShowText();
        //   while an override string is set, Update() renders THAT (honored even
        //   over-UI) instead of the world OverlapPoint result.  ClearUIOverride()
        //   on PointerExit returns to the normal world-hover behaviour.
        //   No singleton OnEnable subscription (#7): the caller polls Instance.
        private string uiOverrideText;
        private Vector2 uiOverrideScreenPos;

        /// <summary>UI panel (e.g. ArchitectMenu row) requests the tooltip show
        /// <paramref name="text"/> anchored near <paramref name="screenPos"/>.
        /// Takes precedence over the world-hover path, even while over UI.</summary>
        public void ShowText(string text, Vector2 screenPos)
        {
            uiOverrideText = text;
            uiOverrideScreenPos = screenPos;
        }

        /// <summary>Clears a previously-set UI override (PointerExit).  Only clears
        /// if the live override still matches, so a stale exit can't wipe a newer
        /// row's tooltip set by a near-simultaneous enter.</summary>
        public void ClearUIOverride(string text)
        {
            if (uiOverrideText == text) uiOverrideText = null;
        }

        /// <summary>Force-clear any UI override regardless of which row set it.
        /// Called when the owning panel rebuilds/closes its rows so a destroyed
        /// row (whose PointerExit may never fire) can't leave a tooltip stuck on.</summary>
        public void ClearUIOverride()
        {
            uiOverrideText = null;
        }

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var go = new GameObject("HoverTooltip");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<HoverTooltip>();
            _instance.canvas = canvas;
        }

        private void Awake()
        {
            mainCam = Camera.main;
            // panel
            rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(200, 32);

            // #UI-restyle U6 — bordered warm-brown panel (root Image = border edge),
            //   matching control bar / inspector.  No more bare PanelBg rectangle.
            var content = MelonS.GameProto.Core.UITheme.MakeBorderedPanel(rt, 2f);
            bg = GetComponent<Image>();  // border image (kept for legacy ref)

            // label (parented INSIDE the bordered content with padding)
            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(content, false);
            label = lblGo.AddComponent<Text>();
            label.text = "";
            label.font = MelonS.GameProto.Core.UITheme.LoadKoreanFont(14);
            label.fontSize = 14;
            label.color = MelonS.GameProto.Core.UITheme.TextPrimary;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8, 4);
            lrt.offsetMax = new Vector2(-8, -4);
            lrt.anchoredPosition = Vector2.zero;
            // start hidden
            gameObject.SetActive(false);
        }

        private void Update()
        {
            // ── UI-driven override first (Architect menu rows) ──────────────
            //   A menu row that the pointer is over has pushed its 자재/비용 text;
            //   render it at the row's screen position and skip the world path.
            if (!string.IsNullOrEmpty(uiOverrideText))
            {
                ShowAt(uiOverrideText, uiOverrideScreenPos);
                return;
            }

            // UI 위면 가림 (GUI 버튼 hover 시 tooltip 가리면 UX 나쁨)
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (overUI) { Hide(); return; }
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) { Hide(); return; }

            Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
            Collider2D hit = Physics2D.OverlapPoint(new Vector2(mouseWorld.x, mouseWorld.y));
            string text = DescribeHit(hit);
            if (string.IsNullOrEmpty(text)) { Hide(); return; }

            ShowAt(text, Input.mousePosition);
        }

        /// <summary>Show + size + position the tooltip at a screen point.  Shared
        /// by the world-hover path and the UI override path so both render
        /// identically (UITheme-consistent bordered panel).</summary>
        private void ShowAt(string text, Vector2 screenPos)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            label.text = text;
            // resize bg by text width (approx 8px per Korean char + padding)
            float w = Mathf.Max(120, label.preferredWidth + 16);
            rt.sizeDelta = new Vector2(w, 32);
            // 위치: mouse/row 옆, 살짝 위 (Canvas Screen-Space-Overlay 라면 mouse pos 그대로)
            if (canvas != null)
            {
                var canvasRt = canvas.GetComponent<RectTransform>();
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPos, null, out localPos);
                // #p2_2 화면 가장자리 clamp (ContextMenuUI #275 패턴): pivot(0,0)이라 우/상으로
                //  확장 → 우측·상단이 캔버스 밖으로 잘리지 않게 보정.
                float px = localPos.x + 14, py = localPos.y + 18;
                Rect cr = canvasRt.rect;
                float pw = rt.sizeDelta.x, ph = rt.sizeDelta.y;
                px = Mathf.Clamp(px, cr.xMin, Mathf.Max(cr.xMin, cr.xMax - pw));
                py = Mathf.Clamp(py, cr.yMin, Mathf.Max(cr.yMin, cr.yMax - ph));
                rt.anchoredPosition = new Vector2(px, py);
            }
        }

        private void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private string DescribeHit(Collider2D hit)
        {
            if (hit == null) return "";
            // 운영자 피드백 #106 - drafted pawn 으로 적 hover 시 ⚔ 공격 아이콘 prefix
            bool draftedReady = false;
            var cs = Object.FindFirstObjectByType<ClickSelector>();
            if (cs != null && cs.CurrentSelection != null && cs.CurrentSelection.IsDrafted)
                draftedReady = true;

            var pawn = hit.GetComponent<PawnEntity>();
            if (pawn != null)
            {
                string draftSuffix = pawn.IsDrafted ? " [징집됨]" : "";
                // #rimworld-fidelity — RimWorld 는 콜로니스트 hover 시 현재 작업을 보여준다.
                //  PawnNameLabel.CurrentActivity(머리위 라벨과 동일 정보)를 덧붙인다.
                var nl = pawn.GetComponent<PawnNameLabel>();
                string act = (nl != null && !string.IsNullOrEmpty(nl.CurrentActivity))
                    ? $" · {nl.CurrentActivity}" : "";
                return $"콜로니스트 {pawn.PawnName}{draftSuffix}{act}  (좌클릭=선택)";
            }
            var tree = hit.GetComponent<TreeEntity>();
            if (tree != null) return "나무  (선택 후 우클릭=벌목)";
            var bush = hit.GetComponent<BerryBushEntity>();
            if (bush != null) return "베리덤불  (자동 채집)";
            var crop = hit.GetComponent<CropEntity>();
            if (crop != null)
            {
                if (crop.IsRipe) return "익은 작물  (우클릭=수확 +5 식량)";
                return "자라는 중인 작물  (익을 때까지 대기)";
            }
            var animal = hit.GetComponent<AnimalEntity>();
            if (animal != null)
            {
                if (draftedReady) return "⚔ 사슴  (우클릭=공격/사냥)";
                return "사슴  (드래프트 후 우클릭=사냥 / 우클릭=길들이기)";
            }
            var wolf = hit.GetComponent<WolfEnemy>();
            if (wolf != null)
            {
                if (draftedReady) return "⚔ 늑대 (위협!)  (우클릭=공격)";
                return "늑대 (위협!)  (드래프트 후 우클릭=공격)";
            }
            var bandit = hit.GetComponent<BanditEnemy>();
            if (bandit != null)
            {
                if (draftedReady) return "⚔ 강도 (위협!)  (우클릭=공격)";
                return "강도 (위협!)  (드래프트 후 우클릭=공격)";
            }
            var trader = hit.GetComponent<TraderEntity>();
            if (trader != null) return "상인  (우클릭=교역)";
            var bench = hit.GetComponent<ResearchBench>();
            if (bench != null) return "연구대  (콜로니스트 옆에 있으면 자동 연구)";
            var stove = hit.GetComponent<StoveEntity>();
            if (stove != null) return "화덕  (콜로니스트가 자동 요리)";
            var bed = hit.GetComponent<BedEntity>();
            if (bed != null) return $"{bed.QualityKr}  (수면 {bed.RestMul:F2}x · 기분 +{bed.MoodBonus:F0}/s)";
            var wall = hit.GetComponent<WallEntity>();
            if (wall != null) return "벽";
            // #obj-audit: AutodoorEntity 는 DoorEntity 를 상속 → 반드시 Door 체크보다 먼저
            //  검사해야 일반 문 설명에 가려지지 않는다(빠른 통과 특성 별도 안내).
            var adoor = hit.GetComponent<AutodoorEntity>();
            if (adoor != null) return "자동문 (콜로니스트 통과 빠름)";
            var door = hit.GetComponent<DoorEntity>();
            if (door != null) return "문 (콜로니스트 통과 가능)";
            var floor = hit.GetComponent<FloorEntity>();
            if (floor != null) return "바닥";
            // ── #obj-audit (멀티에이전트 감사 #6~11): 상호작용·운반 대상인데 hover 설명이
            //    없던 엔티티들.  문구는 ClickSelector 의 실제 우클릭 컨텍스트 메뉴 동작과
            //    1:1 로 맞춰 거짓 안내가 없게 한다(선택된 림 우클릭 시 뜨는 '우선' 명령).
            var vein = hit.GetComponent<StoneVeinEntity>();
            if (vein != null) return "광맥  (선택 후 우클릭=채광 우선)";
            var pile = hit.GetComponent<WoodPileEntity>();
            if (pile != null) return $"목재 더미 (×{pile.Wood})  (선택 후 우클릭=운반 우선)";
            var chunk = hit.GetComponent<StoneChunkEntity>();
            if (chunk != null) return $"돌덩이 (석재 ×{chunk.Stone})  (운반 대상)";
            var meat = hit.GetComponent<MeatPileEntity>();
            if (meat != null) return $"{meat.DisplayName} 더미 (식량 ×{meat.Food})  (운반 대상)";
            var stockpile = hit.GetComponent<StockpileZoneEntity>();
            if (stockpile != null) return $"저장 구역 (우선순위 {stockpile.PriorityKr})  (선택 후 우클릭=우선순위 변경)";
            var bp = hit.GetComponent<BlueprintEntity>();
            if (bp != null && !bp.IsComplete) return "청사진 (건설 대기)  (선택 후 우클릭=건설 우선)";
            // 상호작용 없는 건설 구조물 — 정체만 안내(무엇인지 모르겠다는 혼란 방지).
            var barricade = hit.GetComponent<BarricadeEntity>();
            if (barricade != null) return "바리케이드 (경로 차단·엄폐)";
            var fence = hit.GetComponent<FenceEntity>();
            if (fence != null) return "울타리 (동물 차단·통과 가능)";
            var lamp = hit.GetComponent<LampEntity>();
            if (lamp != null) return "램프 (주변 조명)";
            var table = hit.GetComponent<TableEntity>();
            if (table != null) return "탁자 (식사/휴식 장소)";
            var chair = hit.GetComponent<ChairEntity>();
            if (chair != null) return "의자 (앉는 자리)";
            return "";
        }
    }
}
