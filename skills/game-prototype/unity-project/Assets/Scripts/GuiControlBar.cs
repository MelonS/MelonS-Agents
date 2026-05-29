using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 피드백 (2026-05-27): "ui들 그냥 표시만해주는 키보드 의존도 너무 높음. gui가 전혀 되질 않음."
    /// → 화면 하단 중앙에 클릭 가능한 버튼 바.  모든 기존 hotkey 와 동일 효과.
    /// 키보드 의존 제거.  Self-bootstrapping (Canvas 찾아서 자기가 UI 생성).
    ///
    /// 버튼 라인업 (왼쪽 → 오른쪽):
    ///   [ ⏸ 멈춤 ] [ 1x ] [ 2x ] [ 4x ]   [ 징집(R) ]   [ 벽(B) ] [ 바닥(F) ] [ 문(G) ] [ 화덕(T) ]   [ 연구(N) ]
    ///
    /// 각 버튼 60x56, gap 4.  active mode 인 build 버튼은 노란 highlight.
    /// </summary>
    public class GuiControlBar : MonoBehaviour
    {
        private const float BtnW = 76f;
        private const float BtnH = 56f;
        private const float Gap = 4f;
        private const float GroupGap = 16f;

        private Button pauseBtn, speed1Btn, speed2Btn, speed4Btn;
        private Button draftBtn;
        private Button workBtn;       // #114 - Work tab 열기 (F1)
        private Button scheduleBtn;   // #126 - Schedule tab (F4)
        private Button architectBtn;  // #110 - 5 build btn 대체 (Architect 메뉴 열기)
        private Button researchBtn;

        // #185 - UITheme 통일 (Kenney warm tone 매치)
        private static readonly Color InactiveBg = MelonS.GameProto.Core.UITheme.BtnInactiveBg;
        private static readonly Color ActiveBg   = MelonS.GameProto.Core.UITheme.BtnActiveBg;
        private static readonly Color TextNormal = MelonS.GameProto.Core.UITheme.TextPrimary;
        private static readonly Color TextActive = MelonS.GameProto.Core.UITheme.TextDark;

        private Font font;
        // Lesson #4 - FindFirstObjectByType per-Update 비쌈.
        private ClickSelector cachedCs;

        // wiki Dim2 acceptance #5: clicking any UI button plays the blip.
        //  Route every button onClick through the EXISTING AudioBank.PlaySelect()
        //  (the same soft-blip pawn-select uses; 0.0s throttle = per-click ok).
        //  Find-once + cache, mirroring the ClickSelector cached-reference pattern;
        //  null no-op if the bank is absent (clips/sources may be unassigned).
        private AudioBank cachedAudio;
        private bool audioResolved;

        private void PlayClickBlip()
        {
            if (!audioResolved)
            {
                cachedAudio = AudioBank.Instance != null
                    ? AudioBank.Instance
                    : Object.FindFirstObjectByType<AudioBank>();
                audioResolved = true;
            }
            if (cachedAudio != null) cachedAudio.PlaySelect();
        }

        private static GuiControlBar _instance;

        public static void EnsureInScene()
        {
            if (_instance != null) return;
            // Find Canvas
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[GuiControlBar] Canvas 없음 - skip");
                return;
            }
            var go = new GameObject("GuiControlBar");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<GuiControlBar>();
        }

        private void Start()
        {
            // #UI-restyle U9 — route through UITheme (kill the per-script fallback drift).
            font = MelonS.GameProto.Core.UITheme.LoadKoreanFont(18);
            BuildLayout();
            HideOldHintIfPresent();
        }

        private void HideOldHintIfPresent()
        {
            // SceneSetup 의 ControlHint 텍스트가 키보드 의존 안내라서, 버튼 바가 있으면 가림.
            // (사용자가 옛 hint 와 새 bar 중복 보면 혼란)
            var hint = GameObject.Find("ControlHint");
            if (hint != null)
            {
                var t = hint.GetComponent<Text>();
                if (t != null) t.text = "🖱 좌클릭=선택 · 우클릭=이동/작업 · ESC=빌드취소";
            }
        }

        private RectTransform contentRt;  // bordered-panel inner content (buttons + dividers live here)

        private void BuildLayout()
        {
            // 부모 panel — 하단 중앙
            var rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            // 9 buttons (#126 - 일정 추가)
            //  layout: [멈춤][1x][2x][4x] | [징집] | [직업][일정] | [건축] | [연구]
            float totalW = 9 * BtnW + 4 * Gap + 4 * GroupGap;
            rt.anchoredPosition = new Vector2(0, 40);  // 화면 하단에서 40px

            // #UI-restyle U1 — ONE bordered panel (warm brown + 2px lighter border),
            //   not a borderless flat rectangle.  Pad the frame so buttons breathe.
            float padX = MelonS.GameProto.Core.UITheme.PadOuter;
            float padY = 8f;
            rt.sizeDelta = new Vector2(totalW + padX * 2f, BtnH + padY * 2f);
            // MakeBorderedPanel returns inner content RT (inside border+fill); we lay buttons there.
            contentRt = MelonS.GameProto.Core.UITheme.MakeBorderedPanel(rt);

            float x = -totalW * 0.5f;
            float dividerH = BtnH + 4f;

            // Speed group: [멈춤][1x][2x][4x]
            pauseBtn  = MakeBtn("멈춤", "(Space)",   x, ()=>{ if (TimeController.Instance!=null) TimeController.Instance.TogglePause(); }); x += BtnW + Gap;
            speed1Btn = MakeBtn("1x",  "(1)",       x, ()=>{ if (TimeController.Instance!=null) TimeController.Instance.SetScale(1f); });   x += BtnW + Gap;
            speed2Btn = MakeBtn("2x",  "(2)",       x, ()=>{ if (TimeController.Instance!=null) TimeController.Instance.SetScale(2f); });   x += BtnW + Gap;
            speed4Btn = MakeBtn("4x",  "(3)",       x, ()=>{ if (TimeController.Instance!=null) TimeController.Instance.SetScale(4f); });   x += BtnW;
            MelonS.GameProto.Core.UITheme.MakeVDivider(contentRt, x + GroupGap * 0.5f, dividerH); x += GroupGap;

            // Draft group: [징집]
            draftBtn  = MakeBtn("징집",  "(R)",      x, ToggleDraft);                                                                       x += BtnW;
            MelonS.GameProto.Core.UITheme.MakeVDivider(contentRt, x + GroupGap * 0.5f, dividerH); x += GroupGap;

            // Tabs group: [직업][일정] (#114 F1 / #126 F4)
            workBtn = MakeBtn("직업", "(F1)", x, () => { if (WorkTabUI.Instance != null) WorkTabUI.Instance.Toggle(); }); x += BtnW + GroupGap;
            scheduleBtn = MakeBtn("일정", "(F4)", x, () => { if (ScheduleUI.Instance != null) ScheduleUI.Instance.Toggle(); }); x += BtnW;
            MelonS.GameProto.Core.UITheme.MakeVDivider(contentRt, x + GroupGap * 0.5f, dividerH); x += GroupGap;

            // Build group: [건축]
            architectBtn = MakeBtn("건축", "(F8)", x, () => { if (ArchitectMenu.Instance != null) ArchitectMenu.Instance.Toggle(); }); x += BtnW;
            MelonS.GameProto.Core.UITheme.MakeVDivider(contentRt, x + GroupGap * 0.5f, dividerH); x += GroupGap;

            // Research group: [연구]
            researchBtn = MakeBtn("연구", "(N)",     x, OpenResearchPicker);
        }

        private Button MakeBtn(string label, string hint, float x, System.Action onClick)
        {
            // NOTE: button MUST stay a DIRECT child of the bar root — IntegrationTestRunner
            //   does bar.transform.Find("Btn_멈춤") (depth-1).  Don't reparent into Content.
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(BtnW, BtnH);
            rt.anchoredPosition = new Vector2(x, 0);

            // #UI-restyle U1 — each button gets its own 2px Divider border (root Image)
            //   + an inset fill child, matching the global panel system → reads as a real button.
            var img = go.AddComponent<Image>();
            img.color = MelonS.GameProto.Core.UITheme.Divider;   // border edge
            var fillRt = MelonS.GameProto.Core.UITheme.MakeBorderedPanel(rt, 2f, InactiveBg);
            // The button targets the FILL image so hover/pressed tints the body, not the border.
            var fillImg = fillRt.parent.GetComponent<Image>();   // PanelFill image
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = fillImg;
            var cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);  // hover lighten
            cb.pressedColor     = new Color(0.72f, 0.72f, 0.72f, 1f);  // pressed darken
            cb.selectedColor    = Color.white;
            cb.fadeDuration     = 0.06f;
            btn.colors = cb;
            // wiki #5 — central chokepoint: every bar button blips on click.
            btn.onClick.AddListener(()=>{ PlayClickBlip(); onClick?.Invoke(); });

            // 주 라벨
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var lbl = labelGo.AddComponent<Text>();
            lbl.text = label;
            lbl.font = font;
            lbl.fontSize = 20;
            lbl.fontStyle = FontStyle.Bold;
            lbl.color = TextNormal;
            lbl.alignment = TextAnchor.MiddleCenter;
            var lrt = lbl.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0.4f);
            lrt.anchorMax = new Vector2(1, 1);
            lrt.sizeDelta = Vector2.zero;
            lrt.anchoredPosition = Vector2.zero;

            // hint (작은 글씨)
            var hintGo = new GameObject("Hint");
            hintGo.transform.SetParent(go.transform, false);
            var ht = hintGo.AddComponent<Text>();
            ht.text = hint;
            ht.font = font;
            ht.fontSize = 11;
            ht.color = new Color(0.75f, 0.75f, 0.7f, 0.85f);
            ht.alignment = TextAnchor.MiddleCenter;
            var hrt = ht.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0, 0);
            hrt.anchorMax = new Vector2(1, 0.4f);
            hrt.sizeDelta = Vector2.zero;
            hrt.anchoredPosition = Vector2.zero;

            return btn;
        }

        private void ToggleDraft()
        {
            var cs = Object.FindFirstObjectByType<ClickSelector>();
            if (cs == null || cs.CurrentSelection == null)
            {
                Debug.Log("[Gui] 징집 - 선택된 콜로니스트 없음");
                return;
            }
            cs.CurrentSelection.SetDrafted(!cs.CurrentSelection.IsDrafted);
            Debug.Log($"[Gui] {cs.CurrentSelection.PawnName} 징집 → {cs.CurrentSelection.IsDrafted}");
        }

        private void SetBuildMode(BuildManager.Mode m)
        {
            if (BuildManager.Instance == null) return;
            // 같은 mode 누르면 off (toggle) — Update 의 hotkey 와 동일 동작
            var newMode = (BuildManager.Instance.CurrentMode == m) ? BuildManager.Mode.Off : m;
            BuildManager.Instance.SetMode(newMode);
        }

        private void OpenResearchPicker()
        {
            var ru = Object.FindFirstObjectByType<ResearchUI>();
            if (ru != null) ru.TogglePicker();
        }

        private void Update()
        {
            // 매 프레임 active build mode 따라 button 색 갱신 (가벼움 - 5개 비교)
            if (BuildManager.Instance != null)
            {
                // Architect 버튼 active highlight: 어떤 build mode 든 활성 시 노란.
                RefreshBuildHighlight(architectBtn, BuildManager.Instance.BuildModeActive);
            }
            // Speed highlight
            if (TimeController.Instance != null)
            {
                float s = TimeController.Instance.CurrentScale;
                RefreshBuildHighlight(pauseBtn,  s == 0f);
                RefreshBuildHighlight(speed1Btn, Mathf.Approximately(s, 1f));
                RefreshBuildHighlight(speed2Btn, Mathf.Approximately(s, 2f));
                RefreshBuildHighlight(speed4Btn, Mathf.Approximately(s, 4f));
            }
            // Draft highlight (selection 의 IsDrafted) - cachedCs lesson #4
            if (draftBtn != null)
            {
                if (cachedCs == null) cachedCs = Object.FindFirstObjectByType<ClickSelector>();
                bool drafted = cachedCs != null && cachedCs.CurrentSelection != null && cachedCs.CurrentSelection.IsDrafted;
                RefreshBuildHighlight(draftBtn, drafted);
            }
        }

        private void RefreshBuildHighlight(Button btn, bool active)
        {
            if (btn == null) return;
            // #UI-restyle — tint the FILL graphic (Button.targetGraphic), not the border edge,
            //   so active build/speed buttons glow orange while the border stays consistent.
            var img = btn.targetGraphic as Image;
            if (img == null) img = btn.GetComponent<Image>();
            if (img == null) return;
            var targetBg = active ? ActiveBg : InactiveBg;
            if (img.color != targetBg) img.color = targetBg;
            // 텍스트 색 (label/hint 둘 다)
            foreach (var t in btn.GetComponentsInChildren<Text>())
            {
                t.color = active ? TextActive : (t.fontSize >= 18 ? TextNormal : new Color(0.75f, 0.75f, 0.7f, 0.85f));
            }
        }
    }
}
