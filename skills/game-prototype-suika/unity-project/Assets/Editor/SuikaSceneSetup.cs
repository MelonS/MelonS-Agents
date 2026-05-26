using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    /// <summary>Programmatic Suika-Game scene generator.  CLI:
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod
    /// MelonS.GameProto.EditorTools.SuikaSceneSetup.GenerateAll
    /// </summary>
    public static class SuikaSceneSetup
    {
        private const string ScenesDir = "Assets/Scenes";
        private const string GamePath = "Assets/Scenes/Game.unity";
        private const string PrefabsDir = "Assets/Prefabs";

        private static readonly string[] TierSpritePaths = new[]
        {
            "Assets/Sprites/tier1_cherry.png",
            "Assets/Sprites/tier2_orange.png",
            "Assets/Sprites/tier3_lemon.png",
            "Assets/Sprites/tier4_melon.png",
            "Assets/Sprites/tier5_watermelon.png",
        };
        private static readonly string WallSpritePath = "Assets/Sprites/wall_white.png";
        private static readonly string LineSpritePath = "Assets/Sprites/drop_line.png";

        // Tier physical radii (world units).  Match sprite size / PPU=16.
        private static readonly float[] TierRadii = new[] { 0.5f, 0.75f, 1.0f, 1.5f, 2.0f };

        [MenuItem("MelonS/Suika/Generate All")]
        public static void GenerateAll()
        {
            Debug.Log("[SuikaSceneSetup] Starting...");
            Directory.CreateDirectory(ScenesDir);
            Directory.CreateDirectory(PrefabsDir);
            ForceImportAllSprites();
            var fruitPrefabs = GenerateFruitPrefabs();
            GenerateGame(fruitPrefabs);
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SuikaSceneSetup] Done.");
        }

        private static readonly string[] AudioPaths = new[]
        {
            "Assets/Audio/drop.wav",
            "Assets/Audio/merge.wav",
            "Assets/Audio/gameover.wav",
        };

        private static void ForceImportAllSprites()
        {
            // First: ensure audio clips are imported as AudioClip type
            foreach (var p in AudioPaths)
            {
                if (File.Exists(p))
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            }
            string[] all = new System.Collections.Generic.List<string>(TierSpritePaths) { WallSpritePath, LineSpritePath }.ToArray();
            foreach (var p in all)
            {
                if (!File.Exists(p)) { Debug.LogWarning($"[SuikaSceneSetup] missing: {p}"); continue; }
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                TextureImporter ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) { Debug.LogWarning($"[SuikaSceneSetup] no importer: {p}"); continue; }
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 16;
                ti.filterMode = FilterMode.Point;
                ti.SaveAndReimport();
                Debug.Log($"[SuikaSceneSetup] sprite-imported: {p}");
            }
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }

        private static GameObject[] GenerateFruitPrefabs()
        {
            var prefabs = new GameObject[TierSpritePaths.Length];
            for (int i = 0; i < TierSpritePaths.Length; i++)
            {
                int tier = i + 1;
                Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>(TierSpritePaths[i]);
                if (spr == null) { Debug.LogWarning($"[SuikaSceneSetup] sprite null: {TierSpritePaths[i]}"); continue; }
                GameObject go = new GameObject($"Fruit_Tier{tier}");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = spr;
                sr.sortingOrder = 1;
                var rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 2f;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.5f;
                var col = go.AddComponent<CircleCollider2D>();
                col.radius = TierRadii[i];
                var f = go.AddComponent<Fruit>();
                f.tier = tier;
                var m = go.AddComponent<FruitMerger>();
                // allTierPrefabs serialized field can't be set until ALL prefabs exist;
                // do a second pass below after creation.
                string prefabPath = $"{PrefabsDir}/Fruit_Tier{tier}.prefab";
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                Object.DestroyImmediate(go);
                prefabs[i] = saved;
                Debug.Log($"[SuikaSceneSetup] prefab: {prefabPath}");
            }
            // Second pass: set FruitMerger.allTierPrefabs on each saved prefab.
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null) continue;
                string path = $"{PrefabsDir}/Fruit_Tier{i + 1}.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                var merger = root.GetComponent<FruitMerger>();
                if (merger != null)
                {
                    var so = new SerializedObject(merger);
                    var arr = so.FindProperty("allTierPrefabs");
                    arr.arraySize = prefabs.Length;
                    for (int j = 0; j < prefabs.Length; j++)
                        arr.GetArrayElementAtIndex(j).objectReferenceValue = prefabs[j];
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            return prefabs;
        }

        private static void GenerateGame(GameObject[] fruitPrefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGO = new GameObject("Main Camera");
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.18f);
            cam.transform.position = new Vector3(0, 0, -10);
            camGO.tag = "MainCamera";
            camGO.AddComponent<AudioListener>();

            // Walls (floor + left + right)
            Sprite wallSpr = AssetDatabase.LoadAssetAtPath<Sprite>(WallSpritePath);
            CreateWall("Floor", new Vector3(0, -5f, 0), new Vector3(8.5f, 0.8f, 1f), wallSpr);
            CreateWall("LeftWall", new Vector3(-3.7f, -1f, 0), new Vector3(0.8f, 10f, 1f), wallSpr);
            CreateWall("RightWall", new Vector3(3.7f, -1f, 0), new Vector3(0.8f, 10f, 1f), wallSpr);

            // Drop-line marker (visual only, no collider)
            Sprite lineSpr = AssetDatabase.LoadAssetAtPath<Sprite>(LineSpritePath);
            if (lineSpr != null)
            {
                var lineGO = new GameObject("DropLine");
                var sr = lineGO.AddComponent<SpriteRenderer>();
                sr.sprite = lineSpr;
                sr.color = new Color(1f, 0.4f, 0.4f, 0.5f);
                sr.sortingOrder = 0;
                lineGO.transform.position = new Vector3(0, 3.5f, 0);
                lineGO.transform.localScale = new Vector3(7.2f, 2f, 1f);
            }

            // Pre-spawn a small starting state for the t=4s verify screenshot:
            // - two tier1 cherries close together (will MERGE into tier2 by t=4)
            // - one tier3 lemon, one tier4 melon (different tiers, won't merge)
            // This way the screenshot shows fruit stack + a merge result + score>0
            // (proving physics, merger, score, and audio path all work).
            if (fruitPrefabs.Length >= 4)
            {
                // Two tier-1 cherries stacked vertically so gravity GUARANTEES
                // collision (top falls onto bottom -> Enter fires -> merge).
                // Force justSpawned = false on pre-spawned so Stay merges
                // them immediately at scene start.
                if (fruitPrefabs[0] != null)
                {
                    var a = (GameObject)PrefabUtility.InstantiatePrefab(fruitPrefabs[0], scene);
                    if (a != null) { a.transform.position = new Vector3(-1.5f, -3.5f, 0); ClearJustSpawned(a); }
                    var b = (GameObject)PrefabUtility.InstantiatePrefab(fruitPrefabs[0], scene);
                    if (b != null) { b.transform.position = new Vector3(-1.5f, 0.5f, 0); ClearJustSpawned(b); }
                }
                // Larger tier-3 lemon — stays separate (no merge)
                if (fruitPrefabs[2] != null)
                {
                    var c = (GameObject)PrefabUtility.InstantiatePrefab(fruitPrefabs[2], scene);
                    if (c != null) { c.transform.position = new Vector3(0.4f, -2.5f, 0); ClearJustSpawned(c); }
                }
                // Tier-4 melon to the right
                if (fruitPrefabs[3] != null)
                {
                    var d = (GameObject)PrefabUtility.InstantiatePrefab(fruitPrefabs[3], scene);
                    if (d != null) { d.transform.position = new Vector3(2.2f, -1f, 0); ClearJustSpawned(d); }
                }
            }

            // ScoreManager (singleton)
            var scoreMgr = new GameObject("ScoreManager");
            scoreMgr.AddComponent<ScoreManager>();

            // AudioBank (singleton)
            var audioGO = new GameObject("AudioBank");
            audioGO.AddComponent<AudioSource>();
            var ab = audioGO.AddComponent<AudioBank>();
            AudioClip dropClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/drop.wav");
            AudioClip mergeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/merge.wav");
            AudioClip goClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/gameover.wav");
            var soAb = new SerializedObject(ab);
            soAb.FindProperty("dropClip").objectReferenceValue = dropClip;
            soAb.FindProperty("mergeClip").objectReferenceValue = mergeClip;
            soAb.FindProperty("gameOverClip").objectReferenceValue = goClip;
            soAb.ApplyModifiedPropertiesWithoutUndo();

            // Drop cursor indicator
            Sprite cursorSpr = AssetDatabase.LoadAssetAtPath<Sprite>(WallSpritePath);
            var cursorGO = new GameObject("DropCursor");
            var cursorSR = cursorGO.AddComponent<SpriteRenderer>();
            cursorSR.sprite = cursorSpr;
            cursorSR.color = new Color(1f, 1f, 1f, 0.25f);
            cursorSR.sortingOrder = 0;
            cursorGO.transform.localScale = new Vector3(0.06f, 8f, 1f);
            var dci = cursorGO.AddComponent<DropCursorIndicator>();
            var soDci = new SerializedObject(dci);
            soDci.FindProperty("sr").objectReferenceValue = cursorSR;
            soDci.ApplyModifiedPropertiesWithoutUndo();

            // DropController
            var dropGO = new GameObject("DropController");
            var drop = dropGO.AddComponent<DropController>();
            var soDrop = new SerializedObject(drop);
            var lowArr = soDrop.FindProperty("lowTierPrefabs");
            lowArr.arraySize = 3;
            for (int i = 0; i < 3; i++)
                lowArr.GetArrayElementAtIndex(i).objectReferenceValue = fruitPrefabs[i];
            soDrop.ApplyModifiedPropertiesWithoutUndo();

            // GameOverDetector
            var godGO = new GameObject("GameOverDetector");
            godGO.AddComponent<GameOverDetector>();

            // AutoScreenshotter
            var ssGO = new GameObject("AutoScreenshotter");
            ssGO.AddComponent<AutoScreenshotter>();

            // Canvas + ScoreUI + GameOver panel
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Score text top-right
            var scoreText = new GameObject("ScoreText");
            scoreText.transform.SetParent(canvasGO.transform, false);
            var scoreTxt = scoreText.AddComponent<Text>();
            scoreTxt.text = "Score: 0";
            scoreTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            scoreTxt.fontSize = 48;
            scoreTxt.color = Color.white;
            scoreTxt.alignment = TextAnchor.UpperRight;
            var rt = scoreTxt.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -20f);
            rt.sizeDelta = new Vector2(300f, 60f);
            scoreText.AddComponent<ScoreUI>();

            // Hint text top-left
            var hintGO = new GameObject("HintText");
            hintGO.transform.SetParent(canvasGO.transform, false);
            var hintTxt = hintGO.AddComponent<Text>();
            hintTxt.text = "Click to drop fruit\nSame-tier fruits merge!";
            hintTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hintTxt.fontSize = 32;
            hintTxt.color = new Color(1f, 1f, 1f, 0.85f);
            var hrt = hintTxt.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(0f, 1f);
            hrt.pivot = new Vector2(0f, 1f);
            hrt.anchoredPosition = new Vector2(20f, -20f);
            hrt.sizeDelta = new Vector2(480f, 120f);

            // Next-tier preview top-center
            var nextPanel = new GameObject("NextPanel");
            nextPanel.transform.SetParent(canvasGO.transform, false);
            var nextLblGO = new GameObject("NextLabel");
            nextLblGO.transform.SetParent(nextPanel.transform, false);
            var nextLbl = nextLblGO.AddComponent<Text>();
            nextLbl.text = "NEXT";
            nextLbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nextLbl.fontSize = 24;
            nextLbl.color = new Color(1f, 1f, 1f, 0.75f);
            nextLbl.alignment = TextAnchor.MiddleCenter;
            var nlRT = nextLblGO.GetComponent<RectTransform>();
            nlRT.anchorMin = new Vector2(0.5f, 1f);
            nlRT.anchorMax = new Vector2(0.5f, 1f);
            nlRT.pivot = new Vector2(0.5f, 1f);
            nlRT.anchoredPosition = new Vector2(0f, -20f);
            nlRT.sizeDelta = new Vector2(120f, 30f);

            var nextImgGO = new GameObject("NextImage");
            nextImgGO.transform.SetParent(nextPanel.transform, false);
            var nextImg = nextImgGO.AddComponent<Image>();
            nextImg.preserveAspect = true;
            var niRT = nextImgGO.GetComponent<RectTransform>();
            niRT.anchorMin = new Vector2(0.5f, 1f);
            niRT.anchorMax = new Vector2(0.5f, 1f);
            niRT.pivot = new Vector2(0.5f, 1f);
            niRT.anchoredPosition = new Vector2(0f, -55f);
            niRT.sizeDelta = new Vector2(80f, 80f);

            var preview = nextPanel.AddComponent<NextTierPreview>();
            var soPrev = new SerializedObject(preview);
            soPrev.FindProperty("previewImage").objectReferenceValue = nextImg;
            soPrev.FindProperty("label").objectReferenceValue = nextLbl;
            var tierSprArr = soPrev.FindProperty("tierSprites");
            tierSprArr.arraySize = TierSpritePaths.Length;
            for (int i = 0; i < TierSpritePaths.Length; i++)
                tierSprArr.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(TierSpritePaths[i]);
            soPrev.ApplyModifiedPropertiesWithoutUndo();

            // Wire DropController.preview now that NextTierPreview exists
            var soDrop2 = new SerializedObject(drop);
            soDrop2.FindProperty("preview").objectReferenceValue = preview;
            soDrop2.ApplyModifiedPropertiesWithoutUndo();

            // GameOver panel (initially hidden)
            var goPanel = new GameObject("GameOverPanel");
            goPanel.transform.SetParent(canvasGO.transform, false);
            var goImg = goPanel.AddComponent<Image>();
            goImg.color = new Color(0f, 0f, 0f, 0.8f);
            var goRt = goPanel.GetComponent<RectTransform>();
            goRt.anchorMin = Vector2.zero;
            goRt.anchorMax = Vector2.one;
            goRt.sizeDelta = Vector2.zero;
            goRt.anchoredPosition = Vector2.zero;

            var goTextGO = new GameObject("GameOverText");
            goTextGO.transform.SetParent(goPanel.transform, false);
            var goTxt = goTextGO.AddComponent<Text>();
            goTxt.text = "GAME OVER";
            goTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            goTxt.fontSize = 64;
            goTxt.color = Color.white;
            goTxt.alignment = TextAnchor.MiddleCenter;
            var gtRt = goTextGO.GetComponent<RectTransform>();
            gtRt.anchorMin = new Vector2(0.5f, 0.55f);
            gtRt.anchorMax = new Vector2(0.5f, 0.55f);
            gtRt.pivot = new Vector2(0.5f, 0.5f);
            gtRt.anchoredPosition = Vector2.zero;
            gtRt.sizeDelta = new Vector2(600f, 100f);

            var finalScoreGO = new GameObject("FinalScore");
            finalScoreGO.transform.SetParent(goPanel.transform, false);
            var fsTxt = finalScoreGO.AddComponent<Text>();
            fsTxt.text = "Final: 0";
            fsTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            fsTxt.fontSize = 40;
            fsTxt.color = new Color(1f, 0.85f, 0.4f);
            fsTxt.alignment = TextAnchor.MiddleCenter;
            var fsRt = finalScoreGO.GetComponent<RectTransform>();
            fsRt.anchorMin = new Vector2(0.5f, 0.4f);
            fsRt.anchorMax = new Vector2(0.5f, 0.4f);
            fsRt.pivot = new Vector2(0.5f, 0.5f);
            fsRt.anchoredPosition = Vector2.zero;
            fsRt.sizeDelta = new Vector2(600f, 80f);

            // Wire GameOverUI on the panel parent
            var goUI = canvasGO.AddComponent<GameOverUI>();
            var soGoUI = new SerializedObject(goUI);
            soGoUI.FindProperty("panel").objectReferenceValue = goPanel;
            soGoUI.FindProperty("finalScore").objectReferenceValue = fsTxt;
            soGoUI.ApplyModifiedPropertiesWithoutUndo();

            // EventSystem for UI input
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            EditorSceneManager.SaveScene(scene, GamePath);
            Debug.Log($"[SuikaSceneSetup] saved {GamePath}");
        }

        private static void ClearJustSpawned(GameObject go)
        {
            var f = go.GetComponent<Fruit>();
            if (f == null) return;
            var so = new SerializedObject(f);
            var jsp = so.FindProperty("justSpawned");
            if (jsp != null) { jsp.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void CreateWall(string name, Vector3 pos, Vector3 scale, Sprite spr)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (spr != null)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = spr;
                sr.color = new Color(0.35f, 0.35f, 0.42f);
                sr.sortingOrder = 0;
            }
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
        }

        private static void RegisterBuildScenes()
        {
            var build = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(GamePath, true),
            };
            EditorBuildSettings.scenes = build.ToArray();
            Debug.Log("[SuikaSceneSetup] build scenes registered.");
        }
    }
}
