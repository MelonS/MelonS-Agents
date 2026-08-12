using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace MelonS.GameProto.EditorTools
{
    /// <summary>
    /// MINIMAL diagnostic — Day 0 minimum.  Create a scene with just:
    ///   - Camera
    ///   - One SpriteRenderer with pawn_colonist.png visible at center
    ///   - One TilemapRenderer that fills the camera view with grass tile
    /// No scripts, no UI, no audio, no extra features.
    /// If THIS doesn't show pawn + grass on screen, the issue is asset
    /// import / fundamental scene setup, not gameplay code.
    ///
    /// Run: Unity.exe -batchmode -quit -executeMethod
    ///   MelonS.GameProto.EditorTools.MinimalSceneTest.GenerateMinimal
    /// </summary>
    public static class MinimalSceneTest
    {
        public static void GenerateMinimal()
        {
            Debug.Log("[MinimalSceneTest] starting");

            // STEP 1 — force-import all PNG sprites with explicit settings + refresh
            ForceImportSprites();
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            Debug.Log("[MinimalSceneTest] sprite import refresh complete");

            // STEP 2 — verify sprites loaded as Sprite type
            Sprite pawnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/pawn_colonist.png");
            Sprite grassSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/tile_grass.png");
            Sprite treeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/tree.png");

            Debug.Log($"[MinimalSceneTest] sprites loaded: pawn={pawnSprite!=null} grass={grassSprite!=null} tree={treeSprite!=null}");

            if (pawnSprite == null)
            {
                Debug.LogError("[MinimalSceneTest] FATAL: pawn sprite still null after force-import");
            }

            // STEP 3 — build a minimal scene
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            GameObject camGo = new GameObject("Main Camera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.05f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();
            Debug.Log("[MinimalSceneTest] camera created");

            // Grid + Tilemap
            GameObject gridGo = new GameObject("Grid");
            gridGo.AddComponent<Grid>();
            GameObject tmGo = new GameObject("Tilemap");
            tmGo.transform.SetParent(gridGo.transform);
            Tilemap tm = tmGo.AddComponent<Tilemap>();
            TilemapRenderer tmr = tmGo.AddComponent<TilemapRenderer>();
            tmr.sortingOrder = 0;

            if (grassSprite != null)
            {
                Directory.CreateDirectory("Assets/Tiles");
                Tile grassTile = ScriptableObject.CreateInstance<Tile>();
                grassTile.sprite = grassSprite;
                grassTile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(grassTile, "Assets/Tiles/Grass.asset");
                AssetDatabase.SaveAssets();

                // Fill a small 10x10 area near origin so it's clearly visible
                for (int x = -5; x < 5; x++)
                    for (int y = -5; y < 5; y++)
                        tm.SetTile(new Vector3Int(x, y, 0), grassTile);
                Debug.Log("[MinimalSceneTest] tilemap filled 10x10 grass tiles");
            }
            else
            {
                Debug.LogWarning("[MinimalSceneTest] grass sprite null — no tilemap content");
            }

            // Pawn (sprite renderer only — no scripts)
            GameObject pawnGo = new GameObject("Pawn");
            pawnGo.transform.position = new Vector3(0, 0, 0);
            SpriteRenderer psr = pawnGo.AddComponent<SpriteRenderer>();
            psr.sprite = pawnSprite;
            psr.sortingOrder = 10;
            psr.color = Color.white;
            string pawnSpriteName = psr.sprite != null ? psr.sprite.name : "NULL";
            Debug.Log($"[MinimalSceneTest] pawn at (0,0,0), sprite={pawnSpriteName}, order={psr.sortingOrder}");

            // Tree (one, to verify trees can render)
            if (treeSprite != null)
            {
                GameObject treeGo = new GameObject("Tree");
                treeGo.transform.position = new Vector3(3, 2, 0);
                SpriteRenderer tsr = treeGo.AddComponent<SpriteRenderer>();
                tsr.sprite = treeSprite;
                tsr.sortingOrder = 5;
                tsr.color = Color.white;
                string treeSpriteName = tsr.sprite != null ? tsr.sprite.name : "NULL";
                Debug.Log($"[MinimalSceneTest] tree at (3,2,0), sprite={treeSpriteName}");
            }

            // Auto-screenshotter (self-validates the build)
            GameObject ssGo = new GameObject("AutoScreenshotter");
            ssGo.AddComponent<AutoScreenshotter>();
            Debug.Log("[MinimalSceneTest] AutoScreenshotter added");

            // Save scene
            string scenePath = "Assets/Scenes/MinimalTest.unity";
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[MinimalSceneTest] saved -> {scenePath}");

            // Register as ONLY scene in build
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true),
            };

            AssetDatabase.SaveAssets();
            Debug.Log("[MinimalSceneTest] DONE — build with BuildScript to test");
        }

        private static void ForceImportSprites()
        {
            string[] paths = new[]
            {
                "Assets/Sprites/pawn_colonist.png",
                "Assets/Sprites/tile_grass.png",
                "Assets/Sprites/tree.png",
            };
            foreach (var p in paths)
            {
                TextureImporter ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null)
                {
                    Debug.LogWarning($"[MinimalSceneTest] no TextureImporter for {p}");
                    continue;
                }
                bool changed = false;
                if (ti.textureType != TextureImporterType.Sprite)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    changed = true;
                }
                if (ti.spriteImportMode != SpriteImportMode.Single)
                {
                    ti.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }
                if (ti.spritePixelsPerUnit != 16f)
                {
                    ti.spritePixelsPerUnit = 16f;
                    changed = true;
                }
                if (ti.filterMode != FilterMode.Point)
                {
                    ti.filterMode = FilterMode.Point;  // pixel-art crisp
                    changed = true;
                }
                if (changed)
                {
                    ti.SaveAndReimport();
                    Debug.Log($"[MinimalSceneTest] re-imported {p} as Sprite");
                }
            }
        }

        public static void BuildMinimal()
        {
            string outDir = "../builds/minimal-test";
            Directory.CreateDirectory(outDir);
            string outExe = Path.Combine(outDir, "MinimalTest.exe");

            var options = new UnityEditor.BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MinimalTest.unity" },
                locationPathName = outExe,
                target = BuildTarget.StandaloneWindows64,
                options = UnityEditor.BuildOptions.None,
            };
            var report = UnityEditor.BuildPipeline.BuildPlayer(options);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[MinimalSceneTest] BUILD OK -> {outExe}");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[MinimalSceneTest] BUILD FAIL: {report.summary.result}");
                EditorApplication.Exit(1);
            }
        }
    }
}
