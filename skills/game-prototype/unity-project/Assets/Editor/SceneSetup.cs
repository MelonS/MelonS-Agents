using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    /// <summary>
    /// One-shot scene generator — creates MainMenu.unity + Game.unity
    /// programmatically and registers them in EditorBuildSettings.
    ///
    /// Invoked from CLI:
    ///   Unity.exe -batchmode -quit -projectPath ... -executeMethod
    ///   MelonS.GameProto.EditorTools.SceneSetup.GenerateAll
    /// </summary>
    // R4: partial class - 메서드를 SceneSetup.Pawn/.Menu/.UI/.Terrain/.Game.cs 로 분할
    public static partial class SceneSetup
    {
        private const string ScenesDir = "Assets/Scenes";
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string GamePath = "Assets/Scenes/Game.unity";
        private const string PawnPrefabPath = "Assets/Prefabs/Pawn.prefab";
        private const string TreePrefabPath = "Assets/Prefabs/Tree.prefab";

        [MenuItem("MelonS/Generate All Scenes")]
        public static void GenerateAll()
        {
            Debug.Log("[SceneSetup] Starting generation...");
            Directory.CreateDirectory(ScenesDir);
            Directory.CreateDirectory("Assets/Prefabs");

            // CRITICAL: force-import all PNG sprites BEFORE anything tries
            // to load them via AssetDatabase.LoadAssetAtPath<Sprite>().
            // Without this, missing .meta files cause null sprite references.
            ForceImportAllSprites();

            GeneratePawnPrefab();
            GenerateMainMenu();
            GenerateGame();
            RegisterBuildScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SceneSetup] Done.");
        }

        private static void ForceImportAllSprites()
        {
            string[] paths = new[]
            {
                "Assets/Sprites/pawn_colonist.png",
                "Assets/Sprites/tile_grass.png",
                "Assets/Sprites/tile_dirt.png",
                "Assets/Sprites/tile_water.png",
                "Assets/Sprites/tile_rock.png",
                "Assets/Sprites/decor_flower.png",
                "Assets/Sprites/tree.png",
                "Assets/Sprites/wall_wood.png",
                "Assets/Sprites/floor_wood.png",
                "Assets/Sprites/door_wood.png",
                "Assets/Sprites/deer.png",
                "Assets/Sprites/stove.png",
                "Assets/Sprites/research_bench.png",  // Day 52
                "Assets/Sprites/crop_rice.png",       // Day 57
                "Assets/Sprites/stockpile_marker.png",// Day 57
                "Assets/Sprites/wolf.png",            // Day 64
                "Assets/Sprites/arrow.png",           // Day 50
                "Assets/Sprites/trader.png",          // Stretch — Trader
                "Assets/Sprites/lamp.png",            // Stretch — Lamp
            };
            foreach (var p in paths)
            {
                if (!File.Exists(p)) { Debug.LogWarning($"[SceneSetup] missing: {p}"); continue; }
                // Force re-import to ensure .meta exists + Sprite type
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                TextureImporter ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) { Debug.LogWarning($"[SceneSetup] no TextureImporter for {p}"); continue; }
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 16;
                ti.filterMode = FilterMode.Point;
                ti.SaveAndReimport();
                Debug.Log($"[SceneSetup] forced sprite import: {p}");
            }
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }

        // GeneratePawnPrefab() moved to SceneSetup.Pawn.cs (R4)

        // GenerateMainMenu + CreateMenuButton moved to SceneSetup.Menu.cs (R4)

        private static void GenerateGame()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // R8: 작은 helper 함수로 추출
            Camera cam = SetupCamera();
            SetupCoreSingletons();
            var layout = SetupTilemap();
            // legacy 변수명 호환 (후속 코드 변경 X)
            Tilemap tm = layout.tilemap;
            Tile grassTile = layout.grassTile, dirtTile = layout.dirtTile,
                 waterTile = layout.waterTile, rockTile = layout.rockTile;
            Vector2[] lakeCenters = layout.lakeCenters;
            float[] lakeRadii = layout.lakeRadii;
            Vector2[] rockClusterCenters = layout.rockClusterCenters;
            float rockRadius = layout.rockRadius;
            Vector2[] dirtCenters = layout.dirtCenters;
            float dirtRadius = layout.dirtRadius;
            const int MAP_HALF = 20;
            SetupFlowerDecor(layout);

            // GameManager + spawn pawn via prefab
            GameObject gmGo = new GameObject("GameManager");
            GameManager gm = gmGo.AddComponent<GameManager>();
            GameObject pawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PawnPrefabPath);
            Sprite arrowSpriteRef = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/arrow.png");
            // #116 - wood pile sprite (벌목 후 바닥에 떨어지는 통나무 더미)
            Sprite woodPileSpriteRef = LoadOrSetupSprite("Assets/Sprites/wood_pile.png");
            SerializedObject gmSo = new SerializedObject(gm);
            gmSo.FindProperty("pawnPrefab").objectReferenceValue = pawnPrefab;
            gmSo.FindProperty("arrowSpriteRuntime").objectReferenceValue = arrowSpriteRef;
            gmSo.FindProperty("woodPileSpriteRuntime").objectReferenceValue = woodPileSpriteRef;
            gmSo.ApplyModifiedProperties();

            // ResourceManager (singleton)
            GameObject rmGo = new GameObject("ResourceManager");
            rmGo.AddComponent<ResourceManager>();

            // R10k: AudioBank wiring extract -> SceneSetup.Game.Audio.cs
            GenerateAudioBank();

            // AI Director (Day 5)
            GameObject dirGo = new GameObject("AIDirector");
            AIDirector director = dirGo.AddComponent<AIDirector>();
            // Stretch: trader sprite 주입
            Sprite traderSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/trader.png");
            director.SetTraderSprite(traderSpr);

            // Day 22: WeatherController — subscribes to AIDirector storm event
            GameObject wcGo = new GameObject("WeatherController");
            WeatherController wc = wcGo.AddComponent<WeatherController>();
            wc.SetRefs(director);

            // R10: Wildlife (Day 64 wolves + Day 23/41 deer) extracted -> SceneSetup.Game.Wildlife.cs
            SpawnWildlife();

            // R10c: prefab 생성 (Tree/Wall/Floor/Door/Stove/ResearchBench) extract -> SceneSetup.Game.Prefabs.cs
            BuildPrefabSet prefabs = GenerateBuildPrefabs();
            GameObject treePrefab = prefabs.treePrefab;
            Sprite treeSprite = prefabs.treeSprite;

            // R10l: Tree spawn (20 tree) extract -> SceneSetup.Game.Trees.cs
            SpawnTrees(treePrefab, MAP_HALF, lakeCenters, lakeRadii, rockClusterCenters, rockRadius);

            // Day 12: RegrowthScheduler — drives bush regen + tree-to-sapling
            // chains.  Created AFTER treePrefab exists so we can wire it.
            // Lesson #7 firewall: callers poll Instance from Update, never
            // subscribe in OnEnable — singleton bind order isn't guaranteed.
            GameObject rsGo = new GameObject("RegrowthScheduler");
            RegrowthScheduler rs = rsGo.AddComponent<RegrowthScheduler>();
            rs.SetTreePrefab(treePrefab);

            // R10c: prefab refs from earlier GenerateBuildPrefabs()
            GameObject wallPrefab = prefabs.wallPrefab;
            GameObject floorPrefab = prefabs.floorPrefab;
            GameObject doorPrefab = prefabs.doorPrefab;
            GameObject stovePrefab = prefabs.stovePrefab;
            GameObject benchPrefab = prefabs.benchPrefab;
            Sprite wallSprite = prefabs.wallSprite;
            Sprite floorSprite = prefabs.floorSprite;
            Sprite doorSprite = prefabs.doorSprite;
            Sprite stoveSprite = prefabs.stoveSprite;
            // Day 52: ResearchManager singleton (prefab 은 GenerateBuildPrefabs 에서 처리)
            GameObject rmGo2 = new GameObject("ResearchManager");
            rmGo2.AddComponent<ResearchManager>();

            // R10: Settlement (Day 57) block extracted -> SceneSetup.Game.Settlement.cs
            //   wall 5 + floor 6 + stove + bench + 12 crops + 2 lamps + 9 stockpile markers
            GenerateStarterSettlement(tm, dirtTile, wallPrefab, floorPrefab, stovePrefab, benchPrefab);

            GameObject ghostGo = new GameObject("BuildGhost");
            var ghostSr = ghostGo.AddComponent<SpriteRenderer>();
            ghostSr.sprite = wallSprite;
            ghostSr.sortingOrder = 20;
            ghostSr.color = new Color(1f, 1f, 1f, 0.5f);
            ghostSr.enabled = false;

            GameObject bmGo = new GameObject("BuildManager");
            BuildManager bm = bmGo.AddComponent<BuildManager>();
            bm.SetRefs(wallPrefab, floorPrefab, doorPrefab, stovePrefab,
                       wallSprite, floorSprite, doorSprite, stoveSprite, ghostSr,
                       prefabs.bedPrefab, prefabs.bedSprite);

            // R10m: BerryBush spawn extract -> SceneSetup.Game.BerryBush.cs
            SpawnBerryBushes(treeSprite);

            // ClickSelector
            GameObject csGo = new GameObject("ClickSelector");
            ClickSelector cs = csGo.AddComponent<ClickSelector>();

            // AutoScreenshotter (self-validation harness)
            GameObject ssGo = new GameObject("AutoScreenshotter");
            ssGo.AddComponent<AutoScreenshotter>();

            // ================================================================
            // Day 14 UI polish — palette + layout refactor.
            // Palette (mirror in TimeUI.cs):
            //   panel        = rgba(0.10, 0.094, 0.078, 0.85)
            //   accent_wood  = #a87543  (0.659, 0.459, 0.263)
            //   accent_food  = #7a9a4d  (0.478, 0.604, 0.302)
            //   accent_warn  = #c45a3a  (0.769, 0.353, 0.227)
            //   text_primary = #e8dfd0  (0.910, 0.875, 0.816)
            //   text_muted   = #8a8170  (0.541, 0.506, 0.439)
            // ================================================================
            Color colPanel       = new Color(0.10f, 0.094f, 0.078f, 0.85f);
            Color colAccentWood  = new Color(0.659f, 0.459f, 0.263f, 1f);
            Color colAccentFood  = new Color(0.478f, 0.604f, 0.302f, 1f);
            Color colAccentWarn  = new Color(0.769f, 0.353f, 0.227f, 1f);
            Color colTextPrimary = new Color(0.910f, 0.875f, 0.816f, 1f);
            Color colTextMuted   = new Color(0.541f, 0.506f, 0.439f, 1f);
            // Korean font: try multiple OS fonts.  Malgun Gothic on Win10/11
            // is the safe default.  NanumGothic for Win7 installs that
            // grabbed the Nanum package separately.  Gulim is the legacy
            // shipped Korean font.  Final fallback = LegacyRuntime.ttf
            // (no Hangul → squares, but at least loads).
            Font  uiFont         = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "NanumGothic", "Gulim", "Dotum", "Arial Unicode MS" }, 22)
                                  ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Canvas (EventSystem already created earlier)
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

            // R10j: TopBar (Clock + Time + Wood/Food/Meals) extract -> SceneSetup.Game.TopBar.cs
            GenerateTopBar(canvasGo, colPanel, colTextPrimary, colTextMuted,
                           colAccentFood, colAccentWood, uiFont);

            // R10d: EventLog panel extract -> SceneSetup.Game.EventLog.cs
            GenerateEventLogPanel(canvasGo, colPanel, colTextPrimary, uiFont, director);

            // R10g: Research strip + picker extract -> SceneSetup.Game.Research.cs
            Image resProg = GenerateResearchStripAndPicker(canvasGo, colPanel, colTextPrimary, uiFont);

            // R10e: Tutorial overlay extract -> SceneSetup.Game.Tutorial.cs
            GenerateTutorialOverlay(canvasGo, uiFont, resProg);

            // R10h: PawnInfoPanel extract -> SceneSetup.Game.PawnInfo.cs
            GeneratePawnInfoPanel(canvasGo, cs, colPanel, colTextPrimary, colTextMuted,
                                  colAccentFood, colAccentWood, uiFont);

            // R10f: SaveLoad buttons + ControlHint extract -> SceneSetup.Game.SaveHint.cs
            GenerateSaveLoadButtons(canvasGo, colPanel, colTextPrimary, uiFont);
            GenerateControlHint(canvasGo, colTextMuted, uiFont);

            // R10i: SkillPanel extract -> SceneSetup.Game.SkillPanel.cs
            GenerateSkillPanel(canvasGo, cs, colPanel, colTextPrimary,
                               colAccentFood, colAccentWood, colAccentWarn, uiFont);

            // Day 21: import + add stove sprite (no scene-time placement;
            // future Day 22 will allow building Stove via BuildManager).
            // For now, just ensure sprite is imported.
            string stovePath = "Assets/Sprites/stove.png";
            if (File.Exists(stovePath))
            {
                AssetDatabase.ImportAsset(stovePath, ImportAssetOptions.ForceUpdate);
                TextureImporter ti = AssetImporter.GetAtPath(stovePath) as TextureImporter;
                if (ti != null)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.spritePixelsPerUnit = 16;
                    ti.filterMode = FilterMode.Point;
                    ti.SaveAndReimport();
                }
            }

            EditorSceneManager.SaveScene(scene, GamePath);
            Debug.Log($"[SceneSetup] Game -> {GamePath}");
        }

        // CreateNeedBar + CreateIconButton moved to SceneSetup.UI.cs (R4)

        private static void RegisterBuildScenes()
        {
            var menuGuid = AssetDatabase.AssetPathToGUID(MainMenuPath);
            var gameGuid = AssetDatabase.AssetPathToGUID(GamePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuPath, true),
                new EditorBuildSettingsScene(GamePath, true),
            };
            Debug.Log($"[SceneSetup] BuildScenes registered: MainMenu={menuGuid} Game={gameGuid}");
        }

        // Day 39 helpers — sprite import settings + Tile asset materialization
        // LoadOrSetupSprite + LoadOrCreateTile moved to SceneSetup.Terrain.cs (R4)
    }
}
