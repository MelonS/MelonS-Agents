using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R4: SceneSetup partial - MainMenu scene 생성 + CreateMenuButton.
    //  원본 SceneSetup.cs(95-207)에서 이동.
    public static partial class SceneSetup
    {
        private static void GenerateMainMenu()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            GameObject camGo = new GameObject("Main Camera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            // EventSystem (required for UI input)
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // Canvas
            GameObject canvasGo = new GameObject("Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Day 39 fix: referenceResolution 명시 — 기본 (800,600)은 1920-기반
            //  position 계산을 깨뜨림.
            var canvasScaler_ = canvasGo.AddComponent<CanvasScaler>();
            canvasScaler_.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler_.referenceResolution = new Vector2(1920, 1080);
            canvasScaler_.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // 초기화면 개선 (2026-07-24 운영자): FLUX 키아트 배경 + 한글 타이틀/버튼
            //  + UITheme 웜 톤 + 번들 Noto (LegacyRuntime 은 한글 글리프가 없어 tofu).
            // 2단 폰트: 타이틀=BitBit(디스플레이), 부제·버튼=고운돋움(본문)
            Font displayFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/DNFBitBit.otf")
                            ?? AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/GowunDodum.ttf");
            Font menuFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/GowunDodum.ttf")
                            ?? AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/NotoSansKR.ttf")
                            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var bgTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/UI/menu_bg.png");
            if (bgTex != null)
            {
                GameObject bgGo = new GameObject("Backdrop");
                bgGo.transform.SetParent(canvasGo.transform, false);
                var raw = bgGo.AddComponent<RawImage>();
                raw.texture = bgTex;
                raw.raycastTarget = false;
                var brt = raw.rectTransform;
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
                // 하단 딤 — 버튼 가독.  단색 박스는 상단 경계선이 보여서(1차 빌드 확인)
                //  세로 그라데이션 텍스처(위 투명 → 아래 0.55)로 굽는다.
                GameObject dimGo = new GameObject("BackdropDim");
                dimGo.transform.SetParent(canvasGo.transform, false);
                var dim = dimGo.AddComponent<RawImage>();
                var gradTex = new Texture2D(1, 64, TextureFormat.RGBA32, false);
                gradTex.wrapMode = TextureWrapMode.Clamp;   // Repeat 블렌드 가로줄 방지
                for (int gy = 0; gy < 64; gy++)
                {
                    float a = Mathf.SmoothStep(0.55f, 0f, gy / 63f);   // 아래(0) 진함 → 위 투명
                    gradTex.SetPixel(0, gy, new Color(0f, 0f, 0f, a));
                }
                gradTex.Apply();
                dim.texture = gradTex;
                dim.raycastTarget = false;
                var drt = dim.rectTransform;
                drt.anchorMin = Vector2.zero; drt.anchorMax = new Vector2(1f, 0.62f);
                drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            }

            // Title — 키아트 상단 하늘 영역, 앰버 + 그림자
            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(canvasGo.transform, false);
            Text title = titleGo.AddComponent<Text>();
            title.text = "PAWNSIM";
            title.alignment = TextAnchor.MiddleCenter;
            title.font = displayFont != null ? displayFont : menuFont;
            title.fontSize = 84;
            title.fontStyle = FontStyle.Normal /* BitBit 자체 볼드 — 중첩 금지 (2026-07-25) */;
            title.verticalOverflow = VerticalWrapMode.Overflow;   // BitBit 라인높이 함정
            title.color = new Color(0.985f, 0.87f, 0.62f, 1f);
            var tShadow = titleGo.AddComponent<Shadow>();
            tShadow.effectColor = new Color(0.10f, 0.06f, 0.03f, 0.85f);
            tShadow.effectDistance = new Vector2(3f, -3f);
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 0.80f);
            titleRt.anchorMax = new Vector2(0.5f, 0.80f);
            titleRt.sizeDelta = new Vector2(900, 120);
            titleRt.anchoredPosition = Vector2.zero;

            GameObject subGo = new GameObject("Subtitle");
            subGo.transform.SetParent(canvasGo.transform, false);
            Text sub = subGo.AddComponent<Text>();
            // 2026-08-01 UX 리뷰 — 이전 문구 "작은 정착지의 하루하루 — 콜로니 심
            //  프로토타입" 은 두 가지를 동시에 잘못했다.  (1) '콜로니 심' 은 장르를
            //  이미 아는 사람에게만 통하는 이름이고, (2) '프로토타입' 은 첫 화면에서
            //  스스로 미완성이라고 선언한다.  둘 다 **무엇을 하는 게임인지**는 끝내
            //  말해 주지 않는다 — 운영자가 반복해서 지적한 바로 그 결함이다.
            //  플레이어가 하는 일(일감을 지정 → 주민이 알아서 판단)을 한 줄로 적는다.
            sub.text = "일을 지시하면, 여섯 주민이 알아서 살아갑니다";
            sub.alignment = TextAnchor.MiddleCenter;
            sub.font = menuFont;
            sub.fontSize = 26;
            sub.verticalOverflow = VerticalWrapMode.Overflow;
            sub.color = new Color(0.96f, 0.93f, 0.86f, 0.92f);
            var sShadow = subGo.AddComponent<Shadow>();
            sShadow.effectColor = new Color(0.10f, 0.06f, 0.03f, 0.8f);
            sShadow.effectDistance = new Vector2(2f, -2f);
            RectTransform subRt = subGo.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 0.72f);
            subRt.anchorMax = new Vector2(0.5f, 0.72f);
            subRt.sizeDelta = new Vector2(900, 50);
            subRt.anchoredPosition = Vector2.zero;

            // Start button
            GameObject startGo = CreateMenuButton(canvasGo.transform, "StartButton", "게임 시작", new Vector2(0.5f, 0.40f), menuFont, true);
            Button startBtn = startGo.GetComponent<Button>();

            // Options button (설정 통합) — opens the unified SettingsMenu panel
            //  (audio sliders; no save/load on the menu where there's nothing to save).
            GameObject optionsGo = CreateMenuButton(canvasGo.transform, "OptionsButton", "설정", new Vector2(0.5f, 0.28f), menuFont, false);
            Button optionsBtn = optionsGo.GetComponent<Button>();

            // Quit button (moved down one slot to make room for Options)
            GameObject quitGo = CreateMenuButton(canvasGo.transform, "QuitButton", "종료", new Vector2(0.5f, 0.16f), menuFont, false);
            Button quitBtn = quitGo.GetComponent<Button>();

            // Controller
            GameObject controllerGo = new GameObject("MainMenuController");
            MainMenuController ctrl = controllerGo.AddComponent<MainMenuController>();
            SerializedObject so = new SerializedObject(ctrl);
            so.FindProperty("startButton").objectReferenceValue = startBtn;
            so.FindProperty("quitButton").objectReferenceValue = quitBtn;
            so.FindProperty("optionsButton").objectReferenceValue = optionsBtn;
            so.FindProperty("gameSceneName").stringValue = "Game";
            so.ApplyModifiedProperties();

            // Persistent button listeners (baked into scene file — fires even
            // if Awake-time runtime wiring fails for any reason)
            UnityAction startAction = new UnityAction(ctrl.OnStartClicked);
            UnityEventTools.AddPersistentListener(startBtn.onClick, startAction);
            UnityAction optionsAction = new UnityAction(ctrl.OnOptionsClicked);
            UnityEventTools.AddPersistentListener(optionsBtn.onClick, optionsAction);
            UnityAction quitAction = new UnityAction(ctrl.OnQuitClicked);
            UnityEventTools.AddPersistentListener(quitBtn.onClick, quitAction);
            Debug.Log("[SceneSetup] MainMenu — persistent onClick listeners baked (Start/Options/Quit)");

            // 운영자 fb 2026-05-30 (BUG1 검증): MainMenu 에도 AutoScreenshotter 를 둔다.
            //  -menushot -screenshot <path> -delay <t> 로 실행하면 MainMenuController 가
            //  autostart 를 억제하므로 게임씬 진입 전 메뉴 화면을 캡처할 수 있다.
            GameObject ssGo = new GameObject("AutoScreenshotter");
            ssGo.AddComponent<AutoScreenshotter>();

            EditorSceneManager.SaveScene(scene, MainMenuPath);
            Debug.Log($"[SceneSetup] MainMenu -> {MainMenuPath}");
        }

        private static GameObject CreateMenuButton(Transform parent, string name, string label, Vector2 anchor, Font font, bool primary)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            // UITheme 웜 톤 — 인게임 패널과 같은 계열 (기본 파란 버튼 탈피).
            Color normal = primary
                ? new Color(0.968f, 0.698f, 0.353f, 0.96f)    // 주 버튼 = 앰버
                : new Color(0.165f, 0.122f, 0.094f, 0.88f);   // 보조 = PanelBg 브라운
            Image img = go.AddComponent<Image>();
            img.color = normal;

            Button btn = go.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.08f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.80f, 1f);
            btn.colors = colors;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = primary
                ? new Color(0.36f, 0.22f, 0.10f, 0.9f)
                : new Color(0.427f, 0.310f, 0.216f, 0.9f);    // Divider 브라운
            outline.effectDistance = new Vector2(2f, -2f);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(300, 72);
            rt.anchoredPosition = Vector2.zero;

            // Label
            GameObject txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            Text txt = txtGo.AddComponent<Text>();
            txt.text = label;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = font;
            txt.fontSize = 26;
            txt.fontStyle = FontStyle.Normal /* BitBit 자체 볼드 — 중첩 금지 (2026-07-25) */;
            txt.color = primary
                ? new Color(0.118f, 0.086f, 0.063f, 1f)       // 앰버 위 다크 텍스트
                : new Color(0.94f, 0.90f, 0.84f, 1f);
            RectTransform txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;
            txtRt.anchoredPosition = Vector2.zero;

            return go;
        }
    }
}
