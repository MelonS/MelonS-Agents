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
        private Button settingsBtn;   // 설정 통합 (SettingsMenu 열기 — gear)

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
            // #ui백로그 0.2 — 텍스트 교체만 하던 종전 구현은 y14 의 hint 가 탭바(y24~96)
            //  뒤로 아랫부분만 비져나와 '잘린 글자 띠'로 보였다.  같은 안내는 TutorialOverlay
            //  가 담당하므로 이름값대로 실제로 숨긴다.
            var hint = GameObject.Find("ControlHint");
            if (hint != null) hint.SetActive(false);
        }

        private RectTransform contentRt;  // bordered-panel inner content (buttons + dividers live here)
        private RectTransform speedPanelRt;  // 우하단 속도/멈춤 클러스터 (the reference sim 레이아웃)

        // 우하단 속도 클러스터 배치 상수 (root=full-screen stretch 기준 anchor (1,0) 좌표 계산용).
        private const float PadX = 12f;   // = UITheme.PadOuter
        private const float PadY = 8f;
        private const float SpRight = 16f;  // 화면 우측 모서리 여백
        private const float SpBottom = 24f; // 화면 바닥 여백

        private void BuildLayout()
        {
            // === ROOT: 전체 화면 stretch ===
            // 핵심 fix (시각버그): 이전엔 root RectTransform 이 "하단 중앙" 작은 rect (anchor 0.5,0)
            //   였다.  그 안에서 속도 버튼이 anchor (1,0) 을 써도 (1,0) 은 "작은 중앙 바의 우측 끝"
            //   을 가리켜서, 속도 버튼이 화면 우하단이 아니라 중앙 바 안에 인라인으로 박혔다.
            //   → root 를 전체 화면 stretch 로 바꿔야 자식의 anchor (1,0) 이 "화면 우하단" 을 가리킨다.
            //   탭 바(하단 중앙)는 별도 child 컨테이너(tabBarRt, anchor 0.5,0)로 분리한다.
            //   속도 버튼 4개는 여전히 GuiControlBar root 의 DIRECT child (IntegrationTestRunner
            //   가 bar.transform.Find("Btn_멈춤") depth-1 검사 + Button 총 >=9 검사) → 위치만 우하단.
            var rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            float padX = PadX;
            float padY = PadY;

            // 6 tab buttons: [징집] | [직업][일정] | [건축] | [연구] | [설정]
            float totalW = 6 * BtnW + 5 * GroupGap;  // #ui백로그 0.3 — 버튼 사이 갭은 5개 (4개로 계산해 ⚙설정이 패널 테두리를 4px 침범했었음)

            // === 하단 중앙 탭 바 컨테이너 (징집/직업/일정/건축/연구/설정) ===
            //   root 가 full-screen stretch 이므로, 중앙 바는 자체 컨테이너로 anchor (0.5,0) 고정.
            var tabBarGo = new GameObject("TabBar");
            tabBarGo.transform.SetParent(transform, false);
            var tabBarRt = tabBarGo.AddComponent<RectTransform>();
            tabBarRt.anchorMin = new Vector2(0.5f, 0f);
            tabBarRt.anchorMax = new Vector2(0.5f, 0f);
            tabBarRt.pivot = new Vector2(0.5f, 0f);
            // ui-audit §3.2 Band A — main command bar baseline y=24.
            tabBarRt.anchoredPosition = new Vector2(0, 24);
            tabBarRt.sizeDelta = new Vector2(totalW + padX * 2f, BtnH + padY * 2f);
            // #UI-restyle U1 — ONE bordered panel (warm brown + 2px lighter border).
            //   MakeBorderedPanel returns inner content RT; dividers live there.
            contentRt = MelonS.GameProto.Core.UITheme.MakeBorderedPanel(tabBarRt);

            // === 우하단 속도 패널 (the reference sim 의 우하단 시계/속도 클러스터 위치) ===
            //   root=full-screen 이므로 anchor (1,0) 이 화면 우하단 모서리를 가리킨다.
            //   자체 bordered panel 을 배경으로 만들되, 속도 버튼들은 여전히 GuiControlBar root
            //   의 direct child 로 두고 speedPanelRt 는 시각 배경으로만 사용 (버튼 부모붙이지 않음).
            var spGo = new GameObject("SpeedPanel");
            spGo.transform.SetParent(transform, false);
            speedPanelRt = spGo.AddComponent<RectTransform>();
            speedPanelRt.anchorMin = new Vector2(1f, 0f);
            speedPanelRt.anchorMax = new Vector2(1f, 0f);
            speedPanelRt.pivot = new Vector2(1f, 0f);
            float speedW = 4 * BtnW + 3 * Gap;
            speedPanelRt.sizeDelta = new Vector2(speedW + padX * 2f, BtnH + padY * 2f);
            speedPanelRt.anchoredPosition = new Vector2(-SpRight, SpBottom);  // 우하단 모서리 16px, 바닥 24px
            MelonS.GameProto.Core.UITheme.MakeBorderedPanel(speedPanelRt);

            float dividerH = BtnH + 4f;

            // Speed group: [⏸ 멈춤][1x][2x][4x] — root anchor (1,0)=화면 우하단 기준 배치.
            // ui-audit P1 (운영자 fb "없음(0)") — the speed cluster's LEFTMOST slot is ALWAYS the
            //   pause/speed-0 control, never a build-mode readout.  No mode-indicator Text anywhere.
            //   The GameObject name MUST stay "Btn_멈춤" (IntegrationTestRunner depth-1 Find).
            //   anchor (1,0): 화면 우하단 모서리 기준, x 는 버튼 left edge (음수, left 로 갈수록 더 음수).
            //   speedPanelRt 내부 패딩(padX)만큼 안쪽에서 시작 → 패널 배경과 버튼 정렬.
            float sx = -SpRight - padX - BtnW;  // 가장 오른쪽 버튼(4x)의 left edge
            speed4Btn = MakeBtn("4x",  "(3)", sx, ()=>{ if (TimeController.Instance!=null) TimeController.Instance.SetScale(4f); }, anchorBottomRight:true); sx -= (BtnW + Gap);
            speed2Btn = MakeBtn("2x",  "(2)", sx, ()=>{ if (TimeController.Instance!=null) TimeController.Instance.SetScale(2f); }, anchorBottomRight:true); sx -= (BtnW + Gap);
            speed1Btn = MakeBtn("1x",  "(1)", sx, ()=>{ if (TimeController.Instance!=null) TimeController.Instance.SetScale(1f); }, anchorBottomRight:true); sx -= (BtnW + Gap);
            pauseBtn  = MakeBtn("멈춤", "(Space)", sx, ()=>{ if (TimeController.Instance!=null) TimeController.Instance.TogglePause(); }, displayLabel:"⏸ 멈춤", anchorBottomRight:true);

            // === 하단 중앙: 탭 버튼들 (tabBarRt 컨테이너 기준 로컬 좌표) ===
            float x = -totalW * 0.5f;

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
            researchBtn = MakeBtn("연구", "(N)",     x, OpenResearchPicker); x += BtnW;
            MelonS.GameProto.Core.UITheme.MakeVDivider(contentRt, x + GroupGap * 0.5f, dividerH); x += GroupGap;

            // Settings group: [설정] — opens the unified SettingsMenu panel.
            // #ui백로그 0.4 — "(ESC)" 는 거짓 힌트(ESC 는 닫기/빌드취소에만 소비, 열기 바인딩 없음).
            settingsBtn = MakeBtn("설정", "", x, () => SettingsMenu.ToggleStatic(), displayLabel:"⚙ 설정");
        }

        private Button MakeBtn(string label, string hint, float x, System.Action onClick, string displayLabel = null, bool anchorBottomRight = false)
        {
            // NOTE: button MUST stay a DIRECT child of the bar root — IntegrationTestRunner
            //   does bar.transform.Find("Btn_멈춤") (depth-1).  Don't reparent into Content
            //   nor into SpeedPanel.  `label` drives the GameObject name (test-referenced);
            //   `displayLabel`, when given, drives ONLY the visible Text so we can show e.g.
            //   "⏸ 멈춤" without renaming the GO away from "Btn_멈춤".
            //   anchorBottomRight=true → 우하단 속도 클러스터용 (anchor (1,0), pivot left-bottom).
            //   둘 다 GuiControlBar root(=full-screen stretch)의 DIRECT child 로 유지 (test depth-1 Find).
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            if (anchorBottomRight)
            {
                // 우하단 anchor: root 가 full-screen 이므로 (1,0)=화면 우하단 모서리.
                //   x 는 화면 우측 모서리 기준 음수 오프셋(버튼 left edge).  y 는 화면 바닥 기준.
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(BtnW, BtnH);
                // SpeedPanel 바닥에서 padY 만큼 띄움 + 패널 자체가 y=SpBottom → 시각상 중앙 정렬.
                rt.anchoredPosition = new Vector2(x, SpBottom + PadY);
            }
            else
            {
                // 하단 중앙 탭 버튼: root 가 full-screen 이므로 (0.5,0)=화면 하단 중앙.
                //   TabBar 컨테이너(anchor 0.5,0, y=24, 높이 BtnH+padY*2)와 동일 위치에 겹치도록
                //   버튼 높이만큼 안쪽(y=SpBottom+padY=중앙)에 배치.  x 는 컨테이너 로컬과 동일한
                //   바 중앙 기준 오프셋(buttons 의 left edge, pivot left-bottom).
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(BtnW, BtnH);
                rt.anchoredPosition = new Vector2(x, SpBottom + PadY);
            }

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
            lbl.text = displayLabel ?? label;
            lbl.font = font;
            lbl.fontSize = 20;
            lbl.fontStyle = FontStyle.Bold;
            lbl.color = TextNormal;
            lbl.alignment = TextAnchor.MiddleCenter;
            // #65 운영자 "설정 버튼 라벨/아이콘 잘림": "⚙ 설정"·"⏸ 멈춤"(아이콘+텍스트)이 76px
            //  버튼 폭을 넘어 잘렸음.  best-fit 으로 버튼 안에 맞게 자동 축소(2자 라벨은 20 유지,
            //  넓은 라벨만 줄어듦) + 가로 overflow 는 자르지 않게 → 잘림 제거.
            lbl.resizeTextForBestFit = true;
            lbl.resizeTextMinSize = 12;
            lbl.resizeTextMaxSize = 20;
            lbl.horizontalOverflow = HorizontalWrapMode.Wrap;
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
