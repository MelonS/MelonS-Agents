using UnityEngine;
using UnityEngine.UI;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Unified settings panel (2026-05-31, 운영자: "설정 메뉴 통합").
    ///
    /// ONE bordered panel (UITheme MakeBorderedPanel) that consolidates:
    ///   - SAVE / LOAD  : the exact logic that lived on the floating S/L corner
    ///                    buttons (GameSaveButtons.OnSave / OnLoad), moved inside
    ///                    this panel.  GameSaveButtons stays the canonical save/load
    ///                    implementation; this panel calls into it so there is ONE
    ///                    code path (no duplicated restore logic to drift).
    ///   - SFX 볼륨     : 0..1 slider → AudioBank.SetSfxVolume + PlayerPrefs.
    ///   - MUSIC 볼륨   : 0..1 slider → AudioBank.SetMusicVolume + PlayerPrefs.
    ///
    /// Opened from BOTH:
    ///   - In-game: GuiControlBar "설정" gear button (and ESC closes when open).
    ///   - Main menu: MainMenuController "Options/설정" button.
    ///
    /// NOT self-bootstrapping (no [RuntimeInitializeOnLoadMethod]).  It is created
    /// ON DEMAND the first time something calls EnsureInScene()/Toggle(), attached
    /// to whatever Canvas is in the active scene.  Because it never auto-spawns, it
    /// can never leak onto the MainMenu the way the gated always-on components could
    /// (operator 2026-05-30 menu-leak bug) — so the GameSceneGate gate is not needed
    /// here.  Works in both the MainMenu and Game scenes.
    ///
    /// Save/Load buttons are hidden when no GameSaveButtons exists in the scene
    /// (i.e. on the MainMenu, where there is nothing to save) so the panel degrades
    /// to a pure audio-options panel there.
    ///
    /// Every clickable button's FILL image has raycastTarget=true so the click
    /// always lands (operator: "버튼 fill.raycastTarget=true(클릭 보장)").
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        private static SettingsMenu _instance;

        private RectTransform rt;
        private RectTransform panelContent;
        private Font font;
        private bool isOpen;
        // UI겹침 P1-5 — ResearchUI 의 모달 상호배타/핫키 가드용 공개 표면.
        private static SettingsMenu _smInstance;
        public static bool AnyOpen => _smInstance != null && _smInstance.isOpen;

        private Button saveBtn;
        private Button loadBtn;
        private GameObject saveLoadRow;
        private Slider sfxSlider;
        private Slider musicSlider;
        private Text sfxValueText;     // #6.9 — 현재값 % 표시
        private Text musicValueText;
        private float lastSfxPreview = -10f;   // #6.9 — 드래그 중 청각 프리뷰 스로틀

        private AudioBank cachedAudio;
        private bool audioResolved;

        // Panel geometry — 운영자 #39 "설정 메뉴 깨짐": 콘텐츠에 맞춰 높이 동적 조정
        //  (저장행 유무에 따라).  과거엔 320 고정 + SFX y=-140 라 헤더 아래 죽은 공백이 생겨
        //  '깨진' 인상.  슬라이더를 헤더 바로 아래로 올리고 저장행을 하단에 둔다.
        private const float PanelW = 360f;
        private const float PanelH = 348f;          // 저장행+팔레트/복귀 행 포함
        private const float PanelHNoSave = 272f;    // 저장행 숨김 시 높이

        /// <summary>
        /// Create the panel (once) attached to the active scene's Canvas, if not
        /// already present.  Idempotent.  Returns the live instance (or null if no
        /// Canvas exists in the scene).
        /// </summary>
        public static SettingsMenu EnsureInScene()
        {
            if (_instance != null) return _instance;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[SettingsMenu] Canvas 없음 - skip");
                return null;
            }
            var go = new GameObject("SettingsMenu");
            go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<SettingsMenu>();
            return _instance;
        }

        /// <summary>Open the settings panel, creating it if needed.</summary>
        public static void Open()
        {
            var m = EnsureInScene();
            if (m != null) m.OpenInternal();
        }

        /// <summary>Toggle the settings panel, creating it if needed.</summary>
        public static void ToggleStatic()
        {
            var m = EnsureInScene();
            if (m != null) m.Toggle();
        }

        // #ui백로그 6.4 — 모달 백드롭: 패널 밖 클릭이 월드로 통과해 폰 명령/청사진
        //  배치가 일어나던 것 차단.  백드롭 클릭 = 닫기 (표준 모달 UX).
        private GameObject backdrop;

        private void Awake()
        {
            _smInstance = this;   // P1-5
            font = UITheme.LoadKoreanFont(18);

            // 백드롭은 같은 캔버스의 형제로 (패널보다 먼저 생성 → 아래 깔림).
            var bdGo = new GameObject("SettingsBackdrop");
            bdGo.transform.SetParent(transform.parent, false);
            var bdRt = bdGo.AddComponent<RectTransform>();
            bdRt.anchorMin = Vector2.zero; bdRt.anchorMax = Vector2.one;
            bdRt.offsetMin = Vector2.zero; bdRt.offsetMax = Vector2.zero;
            var bdImg = bdGo.AddComponent<Image>();
            bdImg.color = new Color(0f, 0f, 0f, 0.45f);
            bdImg.raycastTarget = true;
            bdGo.AddComponent<Button>().onClick.AddListener(Close);
            backdrop = bdGo;
            backdrop.SetActive(false);

            rt = gameObject.AddComponent<RectTransform>();
            // Screen-centered modal panel.
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(PanelW, PanelH);
            rt.anchoredPosition = Vector2.zero;
            // Render above the rest of the UI stack so sliders/buttons receive clicks.
            transform.SetAsLastSibling();

            panelContent = UITheme.MakeBorderedPanel(
                rt, UITheme.BorderPx, UITheme.PanelBg, UITheme.PadOuter);

            BuildPanel();
            gameObject.SetActive(false);
            isOpen = false;
        }

        private void BuildPanel()
        {
            // Header strip
            var headerGo = new GameObject("HeaderStrip");
            headerGo.transform.SetParent(panelContent, false);
            var hImg = headerGo.AddComponent<Image>();
            hImg.color = UITheme.HeaderBg;
            hImg.raycastTarget = false;
            var hrt = headerGo.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0, 1);
            hrt.anchorMax = new Vector2(1, 1);
            hrt.pivot = new Vector2(0.5f, 1);
            hrt.sizeDelta = new Vector2(0, 40);
            hrt.anchoredPosition = Vector2.zero;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(headerGo.transform, false);
            var title = titleGo.AddComponent<Text>();
            title.text = "설정";
            title.font = font;
            title.fontSize = 22;
            title.fontStyle = FontStyle.Bold;
            title.color = UITheme.AccentGold;
            title.alignment = TextAnchor.MiddleCenter;
            title.raycastTarget = false;
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
            trt.anchoredPosition = Vector2.zero;

            // Close (X) button — top-right of header.
            var closeBtn = MakeButton(headerGo.transform, "CloseBtn", "×",
                new Vector2(1f, 0.5f), new Vector2(-22, 0), new Vector2(32, 32), Close);
            // anchor close button to header right edge
            var crt = closeBtn.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 0.5f);
            crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-6, 0);

            // ---- SAVE / LOAD row (only meaningful in the Game scene) ----
            saveLoadRow = new GameObject("SaveLoadRow");
            saveLoadRow.transform.SetParent(panelContent, false);
            var slRt = saveLoadRow.AddComponent<RectTransform>();
            slRt.anchorMin = new Vector2(0.5f, 1f);
            slRt.anchorMax = new Vector2(0.5f, 1f);
            slRt.pivot = new Vector2(0.5f, 1f);
            slRt.sizeDelta = new Vector2(PanelW - UITheme.PadOuter * 2f, 56);
            slRt.anchoredPosition = new Vector2(0, -232);  // 팔레트 행 아래

            var slLabel = MakeLabel(saveLoadRow.transform, "저장 / 불러오기",
                new Vector2(0, 0.5f), new Vector2(0, 18), TextAnchor.MiddleLeft, 16, UITheme.TextSecondary);
            var slLabelRt = slLabel.GetComponent<RectTransform>();
            slLabelRt.anchorMin = new Vector2(0f, 1f);
            slLabelRt.anchorMax = new Vector2(1f, 1f);
            slLabelRt.pivot = new Vector2(0f, 1f);
            slLabelRt.sizeDelta = new Vector2(0, 18);
            slLabelRt.anchoredPosition = Vector2.zero;

            // NOTE: these panel buttons are named "Settings*" so they do NOT collide
            //  with the canonical SaveBtn/LoadBtn GameObjects (owned by the SaveHint
            //  lane) that the integration tests reference by name.  Those originals
            //  stay intact; this panel just invokes their wired onClick (see
            //  InvokeExistingButton).  Name preservation rule satisfied.
            // #ui백로그 6.1 — (S)/(L) 은 죽은 핫키 힌트 + 실키(카메라 S/램프 L)와 충돌.
            //  실제 핫키 F5/F9 로 교체.  GO 이름은 테스트 보존 규칙대로 유지.
            saveBtn = MakeButton(saveLoadRow.transform, "SettingsSaveBtn", "저장 (F5)",
                new Vector2(0f, 0f), new Vector2(0, 0), new Vector2(150, 34), OnSaveClicked);
            var saveRt = saveBtn.GetComponent<RectTransform>();
            saveRt.anchorMin = new Vector2(0f, 0f);
            saveRt.anchorMax = new Vector2(0f, 0f);
            saveRt.pivot = new Vector2(0f, 0f);
            saveRt.anchoredPosition = new Vector2(0, 0);

            loadBtn = MakeButton(saveLoadRow.transform, "SettingsLoadBtn", "불러오기 (F9)",
                new Vector2(0f, 0f), new Vector2(0, 0), new Vector2(160, 34), OnLoadClicked);
            var loadRt = loadBtn.GetComponent<RectTransform>();
            loadRt.anchorMin = new Vector2(1f, 0f);
            loadRt.anchorMax = new Vector2(1f, 0f);
            loadRt.pivot = new Vector2(1f, 0f);
            loadRt.anchoredPosition = new Vector2(0, 0);

            // ---- SFX volume slider ---- (헤더 바로 아래로 올림)
            float sfxY = -54;
            MakeLabel(panelContent, "SFX (사운드)", new Vector2(0f, 1f), new Vector2(0, sfxY),
                TextAnchor.MiddleLeft, 16, UITheme.TextPrimary, anchorTopLeft: true, height: 22);
            // #ui백로그 6.9 — 현재값 % 표시 (두 슬라이더 비교/현재 상태 가시화)
            sfxValueText = MakeLabel(panelContent, "100%", new Vector2(0f, 1f), new Vector2(0, sfxY),
                TextAnchor.MiddleRight, 14, UITheme.TextSecondary, anchorTopLeft: true, height: 22);
            sfxSlider = MakeSlider(panelContent, "SfxSlider", new Vector2(0, sfxY - 26),
                OnSfxChanged);

            // ---- MUSIC volume slider ----
            float musicY = -116;
            MakeLabel(panelContent, "MUSIC (음악)", new Vector2(0f, 1f), new Vector2(0, musicY),
                TextAnchor.MiddleLeft, 16, UITheme.TextPrimary, anchorTopLeft: true, height: 22);
            musicValueText = MakeLabel(panelContent, "100%", new Vector2(0f, 1f), new Vector2(0, musicY),
                TextAnchor.MiddleRight, 14, UITheme.TextSecondary, anchorTopLeft: true, height: 22);
            musicSlider = MakeSlider(panelContent, "MusicSlider", new Vector2(0, musicY - 26),
                OnMusicChanged);

            // ---- UI 팔레트 (2026-07-25 운영자 "옵션에서 변경") ----
            float palY = -176;
            MakeLabel(panelContent, "UI 팔레트", new Vector2(0f, 1f), new Vector2(0, palY),
                TextAnchor.MiddleLeft, 16, UITheme.TextPrimary, anchorTopLeft: true, height: 22);
            var palBtn = MakeButton(panelContent, "PaletteBtn",
                UITheme.PaletteNames[UITheme.CurrentPalette], new Vector2(0f, 1f),
                new Vector2(0, palY), new Vector2(120, 30), OnPaletteClicked);
            var palRt = palBtn.GetComponent<RectTransform>();
            palRt.anchorMin = new Vector2(1f, 1f);
            palRt.anchorMax = new Vector2(1f, 1f);
            palRt.pivot = new Vector2(1f, 1f);
            palRt.anchoredPosition = new Vector2(0, palY + 4);
            paletteValueText = palBtn.GetComponentInChildren<Text>();
            MakeLabel(panelContent, "일부 요소는 재입장 시 완전 적용", new Vector2(0f, 1f),
                new Vector2(0, palY - 24), TextAnchor.MiddleLeft, 12, UITheme.TextSecondary,
                anchorTopLeft: true, height: 16);

            // ---- 메인 메뉴로 (2026-07-25 운영자 "게임 안에서 초기 메뉴로 갈 방법") ----
            mainMenuBtn = MakeButton(panelContent, "MainMenuBtn", "메인 메뉴로 나가기",
                new Vector2(0.5f, 1f), new Vector2(0, -296), new Vector2(PanelW - UITheme.PadOuter * 4f, 36),
                OnMainMenuClicked);
            var mmRt = mainMenuBtn.GetComponent<RectTransform>();
            mmRt.anchorMin = new Vector2(0.5f, 1f);
            mmRt.anchorMax = new Vector2(0.5f, 1f);
            mmRt.pivot = new Vector2(0.5f, 1f);
            mmRt.anchoredPosition = new Vector2(0, -296);
        }

        private Text paletteValueText;
        private Button mainMenuBtn;

        private void OnPaletteClicked()
        {
            int next = (UITheme.CurrentPalette + 1) % UITheme.PaletteNames.Length;
            UITheme.SetPalette(next);
            if (paletteValueText != null) paletteValueText.text = UITheme.PaletteNames[next];
            AlertStackUI.Notify($"UI 팔레트: {UITheme.PaletteNames[next]} (재입장 시 완전 적용)", 1);
        }

        private void OnMainMenuClicked()
        {
            // 진행 상태는 저장 버튼으로 별도 — 여기선 시간/오디오 복구 후 메뉴로.
            Time.timeScale = 1f;
            AudioListener.pause = false;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        // ----------------------------------------------------------------------
        //  OPEN / CLOSE / TOGGLE
        // ----------------------------------------------------------------------

        public void Toggle() { if (isOpen) Close(); else OpenInternal(); }

        private void OpenInternal()
        {
            // Hide the redundant floating S/L corner buttons while the unified
            // panel is open (logic is mirrored inside the panel).  GameSaveButtons
            // still exists & functions (F5/F9 hotkeys); we only hide its visual GO.
            SyncSaveLoadAvailability();
            SyncSlidersFromAudio();
            // #6.4 — 백드롭 먼저 최상단으로, 그 위에 패널 (클릭 차단 + 패널 조작 가능)
            if (backdrop != null) { backdrop.SetActive(true); backdrop.transform.SetAsLastSibling(); }
            gameObject.SetActive(true);
            transform.SetAsLastSibling();   // ensure on top of late-spawned UI
            // P1-5 — 이중 모달 방지: 설정을 열면 연구 픽커/치트시트 닫는다.
            ResearchUI.ClosePickerStatic();
            HotkeyCheatSheet.CloseStatic();
            isOpen = true;
        }

        public void Close()
        {
            if (backdrop != null) backdrop.SetActive(false);
            gameObject.SetActive(false);
            isOpen = false;
            ResetLoadArm();   // #6.6 — 닫을 때 2-클릭 암 해제
        }

        private void Update()
        {
            // ESC closes the settings panel when it is open.  We deliberately only
            // consume ESC while open, so the in-game ESC=build-cancel binding (owned
            // by BuildManager / GuiControlBar lane) is untouched when settings closed.
            // #raw-Input 감사(2026-06-10) — SimInput 전환: 백로그 관찰 '설정 메뉴가
            //  harness ESC 주입으로 안 닫힘'의 원인이 이 raw 읽기였다 (WORKFLOW-V2 규칙 2).
            if (isOpen && SimInput.GetKeyDown(KeyCode.Escape))
                Close();
            // #6.6 — 암 시간 만료 시 라벨 원복
            if (loadArmUntil > 0f && Time.unscaledTime > loadArmUntil)
                ResetLoadArm();
        }

        /// <summary>
        /// Show the Save/Load row only when a GameSaveButtons exists in the scene
        /// (Game scene).  On the MainMenu there is nothing to save, so the row hides
        /// and the panel becomes a pure audio-options panel.
        /// </summary>
        private void SyncSaveLoadAvailability()
        {
            // #39 GameSaveButtons 의 GO 가 비활성(코너 S/L 버튼 숨김)이면 기본 Find 가 못 찾아
            //  저장행이 영영 안 떴음 → 비활성 포함으로 조회.
            bool canSave = Object.FindFirstObjectByType<GameSaveButtons>(FindObjectsInactive.Include) != null;
            if (saveLoadRow != null) saveLoadRow.SetActive(canSave);
            // #39 저장행 유무에 따라 패널 높이를 조정해 하단 죽은 공백 제거.
            if (rt != null) rt.sizeDelta = new Vector2(PanelW, canSave ? PanelH : PanelHNoSave);
        }

        private void SyncSlidersFromAudio()
        {
            var bank = ResolveAudio();
            if (bank == null)
            {
                // No AudioBank in scene (e.g. bare test scene): seed from PlayerPrefs.
                if (sfxSlider != null)   sfxSlider.SetValueWithoutNotify(
                    Mathf.Clamp01(PlayerPrefs.GetFloat(AudioBank.PrefSfxVolume, 1f)));
                if (musicSlider != null) musicSlider.SetValueWithoutNotify(
                    Mathf.Clamp01(PlayerPrefs.GetFloat(AudioBank.PrefMusicVolume, 1f)));
                return;
            }
            if (sfxSlider != null)   sfxSlider.SetValueWithoutNotify(bank.SfxVolume);
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(bank.MusicVolume);
            // #6.9 — 열 때 % 표시 동기화
            if (sfxValueText != null)   sfxValueText.text = $"{Mathf.RoundToInt(bank.SfxVolume * 100)}%";
            if (musicValueText != null) musicValueText.text = $"{Mathf.RoundToInt(bank.MusicVolume * 100)}%";
        }

        // ----------------------------------------------------------------------
        //  SAVE / LOAD  (delegate to the canonical GameSaveButtons implementation)
        // ----------------------------------------------------------------------

        private void OnSaveClicked()
        {
            PlayClickBlip();
            // #44 GameSaveButtons 는 이제 시각 버튼 없는 로직 전용 호스트 → 캐논 OnSave 를
            //  직접 호출(버튼 onClick 우회).  호스트 없으면 SaveLoadManager 직접 호출 fallback.
            var gsb = Object.FindFirstObjectByType<GameSaveButtons>(FindObjectsInactive.Include);
            if (gsb != null) gsb.OnSave();
            else SaveLoadManager.Save();
        }

        // #ui백로그 6.6 — 불러오기 2-클릭 암: 클릭 1회로 진행 중 콜로니가 통째로
        //  교체되던 것 방지 (저장 버튼 바로 옆이라 오클릭 위험).  3초 내 재클릭만 실행.
        private float loadArmUntil = -1f;

        private void OnLoadClicked()
        {
            PlayClickBlip();
            if (Time.unscaledTime > loadArmUntil)
            {
                loadArmUntil = Time.unscaledTime + 3f;
                SetLoadBtnLabel("! 재클릭=불러오기");
                return;
            }
            ResetLoadArm();
            // #44 호스트의 OnLoad 직접 호출(pawnPrefab/treeSprite ref 로 월드 복원).  복원 로직은
            //  GameSaveButtons 에만 있으므로 호스트 없으면 load 불가(경고).
            var gsb = Object.FindFirstObjectByType<GameSaveButtons>(FindObjectsInactive.Include);
            if (gsb != null) gsb.OnLoad();
            else Debug.LogWarning("[SettingsMenu] GameSaveButtons 호스트 없음 - load skip");
        }

        private void ResetLoadArm()
        {
            loadArmUntil = -1f;
            SetLoadBtnLabel("불러오기 (F9)");
        }

        private void SetLoadBtnLabel(string s)
        {
            if (loadBtn == null) return;
            var t = loadBtn.GetComponentInChildren<Text>();
            if (t != null) t.text = s;
        }

        // ----------------------------------------------------------------------
        //  VOLUME SLIDER CALLBACKS
        // ----------------------------------------------------------------------

        private void OnSfxChanged(float v)
        {
            var bank = ResolveAudio();
            if (bank != null) bank.SetSfxVolume(v);
            else { PlayerPrefs.SetFloat(AudioBank.PrefSfxVolume, Mathf.Clamp01(v)); PlayerPrefs.Save(); }
            if (sfxValueText != null) sfxValueText.text = $"{Mathf.RoundToInt(v * 100)}%";
            // #6.9 — 드래그 중 즉각 청각 프리뷰 (다음 효과음까지 변경 체감 불가하던 것)
            if (bank != null && Time.unscaledTime - lastSfxPreview > 0.15f)
            {
                lastSfxPreview = Time.unscaledTime;
                bank.PlaySelect();
            }
        }

        private void OnMusicChanged(float v)
        {
            var bank = ResolveAudio();
            if (bank != null) bank.SetMusicVolume(v);
            else { PlayerPrefs.SetFloat(AudioBank.PrefMusicVolume, Mathf.Clamp01(v)); PlayerPrefs.Save(); }
            if (musicValueText != null) musicValueText.text = $"{Mathf.RoundToInt(v * 100)}%";
        }

        private AudioBank ResolveAudio()
        {
            if (!audioResolved)
            {
                cachedAudio = AudioBank.Instance != null
                    ? AudioBank.Instance
                    : Object.FindFirstObjectByType<AudioBank>();
                audioResolved = true;
            }
            // If the cached one was destroyed (scene change), re-resolve once.
            if (cachedAudio == null)
                cachedAudio = Object.FindFirstObjectByType<AudioBank>();
            return cachedAudio;
        }

        private void PlayClickBlip()
        {
            var bank = ResolveAudio();
            if (bank != null) bank.PlaySelect();
        }

        // ----------------------------------------------------------------------
        //  UI BUILDERS
        // ----------------------------------------------------------------------

        private Button MakeButton(Transform parent, string name, string label,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size, System.Action onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var btnRt = go.AddComponent<RectTransform>();
            btnRt.anchorMin = anchor;
            btnRt.anchorMax = anchor;
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.sizeDelta = size;
            btnRt.anchoredPosition = anchoredPos;

            // border edge image (root) + inset fill child (matches control-bar look).
            var borderImg = go.AddComponent<Image>();
            borderImg.color = UITheme.Divider;
            borderImg.raycastTarget = false;

            var fillRt = UITheme.MakeBorderedPanel(btnRt, 2f, UITheme.BtnInactiveBg);
            var fillImg = fillRt.parent.GetComponent<Image>();   // PanelFill image
            // 운영자: 버튼 fill.raycastTarget=true (클릭 보장). MakeBorderedPanel sets
            //  raycastTarget=false on the fill for decorative panels; for an actual
            //  button the fill IS the targetGraphic, so it MUST receive raycasts.
            fillImg.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = fillImg;
            var cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cb.pressedColor     = new Color(0.72f, 0.72f, 0.72f, 1f);
            cb.selectedColor    = Color.white;
            cb.fadeDuration     = 0.06f;
            btn.colors = cb;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var txt = txtGo.AddComponent<Text>();
            txt.text = label;
            txt.font = font;
            txt.fontSize = 16;
            txt.fontStyle = FontStyle.Bold;
            txt.color = UITheme.TextPrimary;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;
            txtRt.anchoredPosition = Vector2.zero;

            return btn;
        }

        private Text MakeLabel(Transform parent, string text, Vector2 anchor, Vector2 anchoredPos,
            TextAnchor align, int size, Color color, bool anchorTopLeft = false, float height = 20f)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.font = font;
            txt.fontSize = size;
            txt.color = color;
            txt.alignment = align;
            txt.raycastTarget = false;
            var lrt = go.GetComponent<RectTransform>();
            if (anchorTopLeft)
            {
                lrt.anchorMin = new Vector2(0f, 1f);
                lrt.anchorMax = new Vector2(1f, 1f);
                lrt.pivot = new Vector2(0f, 1f);
                lrt.sizeDelta = new Vector2(-8, height);
                lrt.anchoredPosition = anchoredPos;
            }
            else
            {
                lrt.anchorMin = anchor;
                lrt.anchorMax = anchor;
                lrt.pivot = new Vector2(0f, 0.5f);
                lrt.sizeDelta = new Vector2(0, height);
                lrt.anchoredPosition = anchoredPos;
            }
            return txt;
        }

        /// <summary>
        /// Build a horizontal 0..1 Slider with a Background, Fill, and Handle.
        /// The handle and background images have raycastTarget=true so dragging
        /// the slider always registers (parity with the button fill rule).
        /// </summary>
        private Slider MakeSlider(Transform parent, string name, Vector2 anchoredPos,
            System.Action<float> onChanged)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var srt = go.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.offsetMin = new Vector2(0f, 0f);
            srt.offsetMax = new Vector2(0f, 0f);
            srt.sizeDelta = new Vector2(-8, 24);
            srt.anchoredPosition = anchoredPos;

            // Background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(go.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            // #39 저대비로 슬라이더가 가는 선처럼 보였음 → 어두운 트랙 색으로 또렷하게.
            bgImg.color = new Color(0.10f, 0.10f, 0.13f, 1f);
            bgImg.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            bgImg.type = Image.Type.Sliced;
            bgImg.raycastTarget = true;
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.18f);
            bgRt.anchorMax = new Vector2(1f, 0.82f);
            bgRt.sizeDelta = Vector2.zero;
            bgRt.anchoredPosition = Vector2.zero;

            // Fill Area → Fill
            var fillAreaGo = new GameObject("Fill Area");
            fillAreaGo.transform.SetParent(go.transform, false);
            var faRt = fillAreaGo.AddComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.25f);
            faRt.anchorMax = new Vector2(1f, 0.75f);
            faRt.offsetMin = new Vector2(8, 0);
            faRt.offsetMax = new Vector2(-8, 0);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = UITheme.AccentOrange;
            fillImg.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            fillImg.type = Image.Type.Sliced;
            fillImg.raycastTarget = false;
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.sizeDelta = new Vector2(10, 0);

            // Handle Slide Area → Handle
            var handleAreaGo = new GameObject("Handle Slide Area");
            handleAreaGo.transform.SetParent(go.transform, false);
            var haRt = handleAreaGo.AddComponent<RectTransform>();
            haRt.anchorMin = new Vector2(0f, 0f);
            haRt.anchorMax = new Vector2(1f, 1f);
            haRt.offsetMin = new Vector2(8, 0);
            haRt.offsetMax = new Vector2(-8, 0);

            var handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = UITheme.AccentGold;
            handleImg.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            handleImg.raycastTarget = true;
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.sizeDelta = new Vector2(20, 20);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 1f;
            slider.onValueChanged.AddListener((v) => onChanged?.Invoke(v));

            return slider;
        }
    }
}
