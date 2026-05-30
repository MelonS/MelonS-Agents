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

            // Title text
            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(canvasGo.transform, false);
            Text title = titleGo.AddComponent<Text>();
            title.text = "PAWN SIM\n(RimWorld-lite vertical slice)";
            title.alignment = TextAnchor.MiddleCenter;
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = 36;
            title.color = new Color(0.95f, 0.95f, 0.9f, 1f);
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 0.7f);
            titleRt.anchorMax = new Vector2(0.5f, 0.7f);
            titleRt.sizeDelta = new Vector2(800, 200);
            titleRt.anchoredPosition = Vector2.zero;

            // Start button
            GameObject startGo = CreateMenuButton(canvasGo.transform, "StartButton", "Start Game", new Vector2(0.5f, 0.45f));
            Button startBtn = startGo.GetComponent<Button>();

            // Quit button
            GameObject quitGo = CreateMenuButton(canvasGo.transform, "QuitButton", "Quit", new Vector2(0.5f, 0.30f));
            Button quitBtn = quitGo.GetComponent<Button>();

            // Controller
            GameObject controllerGo = new GameObject("MainMenuController");
            MainMenuController ctrl = controllerGo.AddComponent<MainMenuController>();
            SerializedObject so = new SerializedObject(ctrl);
            so.FindProperty("startButton").objectReferenceValue = startBtn;
            so.FindProperty("quitButton").objectReferenceValue = quitBtn;
            so.FindProperty("gameSceneName").stringValue = "Game";
            so.ApplyModifiedProperties();

            // Persistent button listeners (baked into scene file — fires even
            // if Awake-time runtime wiring fails for any reason)
            UnityAction startAction = new UnityAction(ctrl.OnStartClicked);
            UnityEventTools.AddPersistentListener(startBtn.onClick, startAction);
            UnityAction quitAction = new UnityAction(ctrl.OnQuitClicked);
            UnityEventTools.AddPersistentListener(quitBtn.onClick, quitAction);
            Debug.Log("[SceneSetup] MainMenu — persistent onClick listeners baked");

            // 운영자 fb 2026-05-30 (BUG1 검증): MainMenu 에도 AutoScreenshotter 를 둔다.
            //  -menushot -screenshot <path> -delay <t> 로 실행하면 MainMenuController 가
            //  autostart 를 억제하므로 게임씬 진입 전 메뉴 화면을 캡처할 수 있다.
            GameObject ssGo = new GameObject("AutoScreenshotter");
            ssGo.AddComponent<AutoScreenshotter>();

            EditorSceneManager.SaveScene(scene, MainMenuPath);
            Debug.Log($"[SceneSetup] MainMenu -> {MainMenuPath}");
        }

        private static GameObject CreateMenuButton(Transform parent, string name, string label, Vector2 anchor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            Image img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.5f, 0.8f, 1f);

            Button btn = go.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.2f, 0.5f, 0.8f, 1f);
            colors.highlightedColor = new Color(0.3f, 0.6f, 0.9f, 1f);
            colors.pressedColor = new Color(0.15f, 0.4f, 0.7f, 1f);
            btn.colors = colors;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(280, 70);
            rt.anchoredPosition = Vector2.zero;

            // Label
            GameObject txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            Text txt = txtGo.AddComponent<Text>();
            txt.text = label;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 22;
            txt.color = Color.white;
            RectTransform txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;
            txtRt.anchoredPosition = Vector2.zero;

            return go;
        }
    }
}
