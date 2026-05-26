using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    public static class SceneSetup
    {
        private const string ScenesDir = "Assets/Scenes";
        private const string GamePath = "Assets/Scenes/Game.unity";
        private const string PrefabsDir = "Assets/Prefabs";

        private static readonly string[] SpritePaths = new[]
        {
            "Assets/Sprites/tile.png",
        };
        private static readonly string[] AudioPaths = new[]
        {
            "Assets/Audio/slide.wav",
            "Assets/Audio/merge.wav",
        };

        [MenuItem("G2048/Generate All")]
        public static void GenerateAll()
        {
            Debug.Log("[SceneSetup] Starting...");
            Directory.CreateDirectory(ScenesDir);
            Directory.CreateDirectory(PrefabsDir);
            ForceImportAllAssets();
            var tilePrefab = BuildTilePrefab();
            GenerateGame(tilePrefab);
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SceneSetup] Done.");
        }

        private static void ForceImportAllAssets()
        {
            foreach (var p in SpritePaths)
            {
                if (!File.Exists(p)) continue;
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                TextureImporter ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 64;
                ti.filterMode = FilterMode.Bilinear;
                ti.SaveAndReimport();
            }
            foreach (var p in AudioPaths)
                if (File.Exists(p)) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildTilePrefab()
        {
            var go = new GameObject("Tile");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/tile.png");
            sr.sortingOrder = 10;
            go.AddComponent<Tile>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabsDir}/Tile.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void GenerateGame(GameObject tilePrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera");
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.74f, 0.71f, 0.65f);
            cam.transform.position = new Vector3(0, 0, -10);
            camGO.tag = "MainCamera";
            camGO.AddComponent<AudioListener>();

            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            var bgRoot = new GameObject("GridBackground");
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/tile.png");
            float cellSize = 1.2f;
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                {
                    float ox = -(3) / 2f * cellSize;
                    float oy =  (3) / 2f * cellSize;
                    var cell = new GameObject($"cell_{x}_{y}");
                    cell.transform.SetParent(bgRoot.transform, false);
                    cell.transform.position = new Vector3(ox + x * cellSize, oy - y * cellSize, 0);
                    var csr = cell.AddComponent<SpriteRenderer>();
                    csr.sprite = spr;
                    csr.color = new Color(0.80f, 0.76f, 0.71f);
                    csr.sortingOrder = 0;
                }

            var gridRoot = new GameObject("GridRoot");

            var gmGO = new GameObject("GameManager");
            var gm = gmGO.AddComponent<GameManager>();
            gm.SetRefs(tilePrefab, gridRoot.transform, cellSize);

            var icGO = new GameObject("InputController");
            icGO.AddComponent<InputController>();

            var aGO = new GameObject("AudioBank");
            aGO.AddComponent<AudioSource>();
            var ab = aGO.AddComponent<AudioBank>();
            ab.SetClips(
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/slide.wav"),
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/merge.wav")
            );

            var ssGO = new GameObject("AutoScreenshotter");
            ssGO.AddComponent<AutoScreenshotter>();

            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            var scoreGO = new GameObject("ScoreText");
            scoreGO.transform.SetParent(canvasGO.transform, false);
            var st = scoreGO.AddComponent<Text>();
            st.text = "Score: 0";
            st.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            st.fontSize = 40;
            st.color = new Color(0.95f, 0.95f, 0.95f);
            st.alignment = TextAnchor.UpperCenter;
            var srt = scoreGO.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.5f, 1f);
            srt.anchorMax = new Vector2(0.5f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0, -20);
            srt.sizeDelta = new Vector2(400, 60);
            scoreGO.AddComponent<ScoreUI>();

            var hintGO = new GameObject("HintText");
            hintGO.transform.SetParent(canvasGO.transform, false);
            var ht = hintGO.AddComponent<Text>();
            ht.text = "WASD or Arrow keys to slide tiles - same numbers merge";
            ht.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ht.fontSize = 22;
            ht.color = new Color(0.95f, 0.95f, 0.95f, 0.85f);
            ht.alignment = TextAnchor.LowerCenter;
            var hrt = hintGO.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 0f);
            hrt.anchorMax = new Vector2(1f, 0f);
            hrt.pivot = new Vector2(0.5f, 0f);
            hrt.anchoredPosition = new Vector2(0, 20);
            hrt.sizeDelta = new Vector2(-40, 36);

            var goGO = new GameObject("GameOverText");
            goGO.transform.SetParent(canvasGO.transform, false);
            var got = goGO.AddComponent<Text>();
            got.text = "GAME OVER";
            got.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            got.fontSize = 80;
            got.color = new Color(0.95f, 0.32f, 0.32f);
            got.alignment = TextAnchor.MiddleCenter;
            var gort = goGO.GetComponent<RectTransform>();
            gort.anchorMin = new Vector2(0.5f, 0.5f);
            gort.anchorMax = new Vector2(0.5f, 0.5f);
            gort.pivot = new Vector2(0.5f, 0.5f);
            gort.anchoredPosition = Vector2.zero;
            gort.sizeDelta = new Vector2(800, 120);
            goGO.AddComponent<GameOverUI>();

            EditorSceneManager.SaveScene(scene, GamePath);
            Debug.Log($"[SceneSetup] saved {GamePath}");
        }

        private static void RegisterBuildScenes()
        {
            var build = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(GamePath, true),
            };
            EditorBuildSettings.scenes = build.ToArray();
        }
    }
}
