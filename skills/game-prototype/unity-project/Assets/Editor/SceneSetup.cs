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
            SerializedObject gmSo = new SerializedObject(gm);
            gmSo.FindProperty("pawnPrefab").objectReferenceValue = pawnPrefab;
            gmSo.FindProperty("arrowSpriteRuntime").objectReferenceValue = arrowSpriteRef;
            gmSo.ApplyModifiedProperties();

            // ResourceManager (singleton)
            GameObject rmGo = new GameObject("ResourceManager");
            rmGo.AddComponent<ResourceManager>();

            // AudioBank (Day 6 - wired Day 7)
            GameObject audioGo = new GameObject("AudioBank");
            AudioBank audioBank = audioGo.AddComponent<AudioBank>();
            AudioClip chopClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/chop.wav");
            AudioClip selClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/select.wav");
            // Day 33: ambient BGM (30s loop)
            if (File.Exists("Assets/Audio/bgm_ambient.wav"))
                AssetDatabase.ImportAsset("Assets/Audio/bgm_ambient.wav", ImportAssetOptions.ForceUpdate);
            AudioClip bgmClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bgm_ambient.wav");
            // Day 80: 새 SFX
            if (File.Exists("Assets/Audio/hit.wav"))
                AssetDatabase.ImportAsset("Assets/Audio/hit.wav", ImportAssetOptions.ForceUpdate);
            if (File.Exists("Assets/Audio/harvest.wav"))
                AssetDatabase.ImportAsset("Assets/Audio/harvest.wav", ImportAssetOptions.ForceUpdate);
            if (File.Exists("Assets/Audio/wolf_howl.wav"))
                AssetDatabase.ImportAsset("Assets/Audio/wolf_howl.wav", ImportAssetOptions.ForceUpdate);
            AudioClip hitClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/hit.wav");
            AudioClip harvestClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/harvest.wav");
            AudioClip howlClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/wolf_howl.wav");
            SerializedObject abSo = new SerializedObject(audioBank);
            if (chopClip != null) abSo.FindProperty("sfxChop").objectReferenceValue = chopClip;
            if (selClip != null) abSo.FindProperty("sfxSelect").objectReferenceValue = selClip;
            if (bgmClip != null) abSo.FindProperty("bgm").objectReferenceValue = bgmClip;
            if (hitClip != null) abSo.FindProperty("sfxHit").objectReferenceValue = hitClip;
            if (harvestClip != null) abSo.FindProperty("sfxHarvest").objectReferenceValue = harvestClip;
            if (howlClip != null) abSo.FindProperty("sfxWolfHowl").objectReferenceValue = howlClip;
            abSo.ApplyModifiedProperties();

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

            // Day 41: tree 위치 — 40x40 맵에 20그루 (분포 균등 + 호수·바위 회피).
            //  결정론적 (seed=24680).
            var treePositionsList = new System.Collections.Generic.List<Vector2>();
            {
                System.Random tr = new System.Random(24680);
                int tries = 0;
                while (treePositionsList.Count < 20 && tries < 400)
                {
                    tries++;
                    int tx = tr.Next(-(MAP_HALF-2), MAP_HALF-1);
                    int ty = tr.Next(-(MAP_HALF-2), MAP_HALF-1);
                    Vector2 tp = new Vector2(tx, ty);
                    bool skip = false;
                    // pawn spawn (-2,0)(0,0)(2,0) 근처 회피
                    if (Mathf.Abs(tx) < 4 && Mathf.Abs(ty) < 2) continue;
                    for (int li = 0; li < lakeCenters.Length; li++)
                        if ((tp - lakeCenters[li]).magnitude < lakeRadii[li] + 1.5f) { skip = true; break; }
                    if (skip) continue;
                    foreach (var rc in rockClusterCenters)
                        if ((tp - rc).magnitude < rockRadius + 0.8f) { skip = true; break; }
                    if (skip) continue;
                    // 다른 tree 와 최소 거리 2.0
                    foreach (var ex in treePositionsList)
                        if (Vector2.Distance(ex, tp) < 2.0f) { skip = true; break; }
                    if (skip) continue;
                    treePositionsList.Add(tp);
                }
            }
            Vector2[] treePositions = treePositionsList.ToArray();
            foreach (var pos in treePositions)
            {
                GameObject t = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab);
                t.name = $"Tree_{pos.x}_{pos.y}";
                t.transform.position = new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0);  // tile center 정렬
            }

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
                       wallSprite, floorSprite, doorSprite, stoveSprite, ghostSr);

            // Day 11: BerryBushes — 4 placed offset from trees so AI sees
            // both gather + chop choices.  Re-use tree sprite tinted greenish
            // until an operator-authored berry sprite exists (already imported
            // by ForceImportAllSprites, so no extra import call needed).
            // NOTE: BerryBushEntity.Awake() repaints SpriteRenderer.color
            // based on stock level (white -> grey), so any green tint we set
            // here is overwritten at runtime — that's intentional in the
            // component design and acceptable: white vs brown still reads
            // distinct from trees.  The scene-file color is still set per
            // handoff spec.
            // Day 41: berry bush 6개 (40x40 맵).
            Vector2[] bushPositions = new[]
            {
                new Vector2(-9f,  -2f),
                new Vector2(  6f,  -8f),
                new Vector2( 11f,   3f),
                new Vector2(-13f,  10f),
                new Vector2(  3f,  13f),
                new Vector2( -6f, -14f),
            };
            foreach (var pos in bushPositions)
            {
                GameObject b = new GameObject($"BerryBush_{pos.x}_{pos.y}");
                b.transform.position = new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0);  // tile center 정렬
                SpriteRenderer bsr = b.AddComponent<SpriteRenderer>();
                bsr.sprite = treeSprite;
                bsr.sortingOrder = 5;
                bsr.color = new Color(0.6f, 0.9f, 0.4f, 1f);
                BoxCollider2D bcol = b.AddComponent<BoxCollider2D>();
                bcol.size = Vector2.one;
                b.AddComponent<BerryBushEntity>();
            }

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

            // ---------- TopBar (full-width strip, 32px, panel color) ----------
            GameObject topBarGo = new GameObject("TopBar");
            topBarGo.transform.SetParent(canvasGo.transform, false);
            Image topBarBg = topBarGo.AddComponent<Image>();
            topBarBg.color = colPanel;
            RectTransform topRt = topBarGo.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            // Day 39: 1920 ref 기준 topbar 48px (이전 32은 800 ref 기준이라 너무 작음)
            // 운영자 피드백 polish: font 22→28 키움 → topbar 도 48→60 확보
            topRt.sizeDelta = new Vector2(0, 60);
            topRt.anchoredPosition = new Vector2(0, 0);

            // TopBar LEFT — ClockUI "Day 1 - 06:00"
            GameObject clockGo = new GameObject("ClockUI");
            clockGo.transform.SetParent(topBarGo.transform, false);
            Text clockText = clockGo.AddComponent<Text>();
            clockText.text = "Day 1 - 06:00";
            clockText.font = uiFont;
            clockText.fontSize = 28;
            clockText.color = colTextPrimary;
            clockText.alignment = TextAnchor.MiddleLeft;
            RectTransform clockRt = clockGo.GetComponent<RectTransform>();
            clockRt.anchorMin = new Vector2(0f, 0f);
            clockRt.anchorMax = new Vector2(0f, 1f);
            clockRt.pivot = new Vector2(0f, 0.5f);
            clockRt.sizeDelta = new Vector2(220, 0);
            clockRt.anchoredPosition = new Vector2(16, 0);
            clockGo.AddComponent<ClockUI>();

            // TopBar CENTER — TimeUI "▶ 1x"
            GameObject timeGo = new GameObject("TimeUI");
            timeGo.transform.SetParent(topBarGo.transform, false);
            Text timeText = timeGo.AddComponent<Text>();
            timeText.text = "▶ 1x";
            timeText.font = uiFont;
            timeText.fontSize = 28;
            timeText.color = colTextPrimary;
            timeText.alignment = TextAnchor.MiddleCenter;
            RectTransform timeRt = timeGo.GetComponent<RectTransform>();
            timeRt.anchorMin = new Vector2(0.5f, 0f);
            timeRt.anchorMax = new Vector2(0.5f, 1f);
            timeRt.pivot = new Vector2(0.5f, 0.5f);
            timeRt.sizeDelta = new Vector2(220, 0);
            timeRt.anchoredPosition = new Vector2(0, 0);
            timeGo.AddComponent<TimeUI>();

            // Day 38: 우측 리소스 영역 레이아웃 재설계 — overlap fix.
            //  기존 wood(-320)/meals(-160)/food(-20) + 140 width → mutual overlap.
            //  새 레이아웃: 우측 정렬, 안전한 spacing.
            //    [목재: N]  ·  [식사: N]  ·  [식량: N]                 16px padding from right
            //  각 텍스트 width 120, 점 width 16, 텍스트간 8px 간격.
            //  총 너비: 120*3 + 16*2 + 8*4 = 392 + 32 = 424 + padding 16 = 440.
            //  TimeUI 중앙(960±110)과의 ovrlap 방지 — 우측 클러스터 시작 x=1920-440=1480.

            // 식량 — 가장 오른쪽
            GameObject foodTextGo = new GameObject("FoodText");
            foodTextGo.transform.SetParent(topBarGo.transform, false);
            Text foodText = foodTextGo.AddComponent<Text>();
            foodText.text = "식량: 0";
            foodText.font = uiFont;
            foodText.fontSize = 28;
            foodText.color = colAccentFood;
            foodText.alignment = TextAnchor.MiddleRight;
            RectTransform foodTextRt = foodTextGo.GetComponent<RectTransform>();
            foodTextRt.anchorMin = new Vector2(1f, 0f);
            foodTextRt.anchorMax = new Vector2(1f, 1f);
            foodTextRt.pivot = new Vector2(1f, 0.5f);
            foodTextRt.sizeDelta = new Vector2(120, 0);
            foodTextRt.anchoredPosition = new Vector2(-16, 0);

            // 식량 — 식사 사이 점
            GameObject sep2Go = new GameObject("ResSep2");
            sep2Go.transform.SetParent(topBarGo.transform, false);
            Text sep2Text = sep2Go.AddComponent<Text>();
            sep2Text.text = "·";
            sep2Text.font = uiFont;
            sep2Text.fontSize = 28;
            sep2Text.color = colTextMuted;
            sep2Text.alignment = TextAnchor.MiddleCenter;
            RectTransform sep2Rt = sep2Go.GetComponent<RectTransform>();
            sep2Rt.anchorMin = new Vector2(1f, 0f);
            sep2Rt.anchorMax = new Vector2(1f, 1f);
            sep2Rt.pivot = new Vector2(1f, 0.5f);
            sep2Rt.sizeDelta = new Vector2(16, 0);
            sep2Rt.anchoredPosition = new Vector2(-144, 0);  // 16 + 120 + 8

            // 식사 — Day 29 meals counter
            GameObject mealsTextGo = new GameObject("MealsText");
            mealsTextGo.transform.SetParent(topBarGo.transform, false);
            Text mealsText = mealsTextGo.AddComponent<Text>();
            mealsText.text = "식사: 0";
            mealsText.font = uiFont;
            mealsText.fontSize = 28;
            mealsText.color = new Color(0.93f, 0.81f, 0.45f, 1f);  // amber/wheat
            mealsText.alignment = TextAnchor.MiddleRight;
            RectTransform mealsRt = mealsTextGo.GetComponent<RectTransform>();
            mealsRt.anchorMin = new Vector2(1f, 0f);
            mealsRt.anchorMax = new Vector2(1f, 1f);
            mealsRt.pivot = new Vector2(1f, 0.5f);
            mealsRt.sizeDelta = new Vector2(120, 0);
            mealsRt.anchoredPosition = new Vector2(-168, 0);  // 144 + 16 + 8

            // 식사 — 목재 사이 점
            GameObject sepGo = new GameObject("ResSep1");
            sepGo.transform.SetParent(topBarGo.transform, false);
            Text sepText = sepGo.AddComponent<Text>();
            sepText.text = "·";
            sepText.font = uiFont;
            sepText.fontSize = 28;
            sepText.color = colTextMuted;
            sepText.alignment = TextAnchor.MiddleCenter;
            RectTransform sepRt = sepGo.GetComponent<RectTransform>();
            sepRt.anchorMin = new Vector2(1f, 0f);
            sepRt.anchorMax = new Vector2(1f, 1f);
            sepRt.pivot = new Vector2(1f, 0.5f);
            sepRt.sizeDelta = new Vector2(16, 0);
            sepRt.anchoredPosition = new Vector2(-296, 0);  // 168 + 120 + 8

            // 목재 — 가장 왼쪽 (우측 클러스터 내)
            GameObject woodGo = new GameObject("WoodText");
            woodGo.transform.SetParent(topBarGo.transform, false);
            Text woodText = woodGo.AddComponent<Text>();
            woodText.text = "목재: 0";
            woodText.font = uiFont;
            woodText.fontSize = 28;
            woodText.color = colAccentWood;
            woodText.alignment = TextAnchor.MiddleRight;
            RectTransform woodRt = woodGo.GetComponent<RectTransform>();
            woodRt.anchorMin = new Vector2(1f, 0f);
            woodRt.anchorMax = new Vector2(1f, 1f);
            woodRt.pivot = new Vector2(1f, 0.5f);
            woodRt.sizeDelta = new Vector2(120, 0);
            woodRt.anchoredPosition = new Vector2(-320, 0);  // 296 + 16 + 8

            // ResourceCounterUI host (no longer has its own panel image; just script)
            GameObject resHostGo = new GameObject("ResourceCounter");
            resHostGo.transform.SetParent(canvasGo.transform, false);
            resHostGo.AddComponent<RectTransform>();
            ResourceCounterUI resCounter = resHostGo.AddComponent<ResourceCounterUI>();
            SerializedObject rcSo = new SerializedObject(resCounter);
            rcSo.FindProperty("woodText").objectReferenceValue = woodText;
            rcSo.FindProperty("foodText").objectReferenceValue = foodText;
            rcSo.FindProperty("mealsText").objectReferenceValue = mealsText;
            rcSo.ApplyModifiedProperties();

            // R10d: EventLog panel extract -> SceneSetup.Game.EventLog.cs
            GenerateEventLogPanel(canvasGo, colPanel, colTextPrimary, uiFont, director);

            // ---------- Day 54: Research strip (bottom-center) + popup picker ----------
            GameObject resStripGo = new GameObject("ResearchStrip");
            resStripGo.transform.SetParent(canvasGo.transform, false);
            Image resStripBg = resStripGo.AddComponent<Image>();
            resStripBg.color = colPanel;
            RectTransform resStripRt = resStripGo.GetComponent<RectTransform>();
            resStripRt.anchorMin = new Vector2(0.5f, 0f);
            resStripRt.anchorMax = new Vector2(0.5f, 0f);
            resStripRt.pivot = new Vector2(0.5f, 0f);
            resStripRt.sizeDelta = new Vector2(420, 36);
            resStripRt.anchoredPosition = new Vector2(0, 40);

            GameObject resStatusGo = new GameObject("ResearchStatusText");
            resStatusGo.transform.SetParent(resStripGo.transform, false);
            Text resStatus = resStatusGo.AddComponent<Text>();
            resStatus.text = "연구: 없음 (N=선택)";
            resStatus.font = uiFont;
            resStatus.fontSize = 22;
            resStatus.color = colTextPrimary;
            resStatus.alignment = TextAnchor.MiddleCenter;
            RectTransform resStatusRt = resStatusGo.GetComponent<RectTransform>();
            resStatusRt.anchorMin = Vector2.zero;
            resStatusRt.anchorMax = Vector2.one;
            resStatusRt.sizeDelta = new Vector2(-12, -8);
            resStatusRt.anchoredPosition = new Vector2(0, 2);

            // Progress bar background under strip
            GameObject resProgBgGo = new GameObject("ResearchProgressBg");
            resProgBgGo.transform.SetParent(resStripGo.transform, false);
            Image resProgBg = resProgBgGo.AddComponent<Image>();
            resProgBg.color = new Color(0.10f, 0.10f, 0.10f, 0.7f);
            RectTransform resProgBgRt = resProgBgGo.GetComponent<RectTransform>();
            resProgBgRt.anchorMin = new Vector2(0f, 0f);
            resProgBgRt.anchorMax = new Vector2(1f, 0f);
            resProgBgRt.pivot = new Vector2(0.5f, 0f);
            resProgBgRt.sizeDelta = new Vector2(-12, 4);
            resProgBgRt.anchoredPosition = new Vector2(0, 2);

            GameObject resProgGo = new GameObject("ResearchProgressFill");
            resProgGo.transform.SetParent(resProgBgGo.transform, false);
            Image resProg = resProgGo.AddComponent<Image>();
            resProg.color = new Color(0.45f, 0.85f, 0.50f, 1f);
            resProg.type = Image.Type.Filled;
            resProg.fillMethod = Image.FillMethod.Horizontal;
            resProg.fillOrigin = (int)Image.OriginHorizontal.Left;
            resProg.fillAmount = 0f;
            // Use a 2x2 white sprite (procedurally — Image needs Sprite even for solid fill)
            var rpTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            rpTex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            rpTex.Apply();
            resProg.sprite = Sprite.Create(rpTex, new Rect(0,0,2,2), new Vector2(0.5f,0.5f), 2f);
            RectTransform resProgRt = resProgGo.GetComponent<RectTransform>();
            resProgRt.anchorMin = Vector2.zero;
            resProgRt.anchorMax = Vector2.one;
            resProgRt.sizeDelta = Vector2.zero;
            // Also assign sprite to resProgBg so Unity Image renders fill
            resProgBg.sprite = resProg.sprite;

            // Popup picker — wide center panel, hidden by default
            GameObject pickerGo = new GameObject("ResearchPicker");
            pickerGo.transform.SetParent(canvasGo.transform, false);
            Image pickerBg = pickerGo.AddComponent<Image>();
            pickerBg.color = new Color(0.08f, 0.10f, 0.13f, 0.95f);
            pickerBg.sprite = resProg.sprite;
            RectTransform pickerRt = pickerGo.GetComponent<RectTransform>();
            pickerRt.anchorMin = new Vector2(0.5f, 0.5f);
            pickerRt.anchorMax = new Vector2(0.5f, 0.5f);
            pickerRt.pivot = new Vector2(0.5f, 0.5f);
            pickerRt.sizeDelta = new Vector2(680, 280);
            pickerRt.anchoredPosition = Vector2.zero;

            GameObject pickerTextGo = new GameObject("PickerText");
            pickerTextGo.transform.SetParent(pickerGo.transform, false);
            Text pickerText = pickerTextGo.AddComponent<Text>();
            pickerText.text = "";
            pickerText.font = uiFont;
            pickerText.fontSize = 22;
            pickerText.color = colTextPrimary;
            pickerText.alignment = TextAnchor.UpperLeft;
            pickerText.horizontalOverflow = HorizontalWrapMode.Wrap;
            pickerText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform pickerTextRt = pickerTextGo.GetComponent<RectTransform>();
            pickerTextRt.anchorMin = Vector2.zero;
            pickerTextRt.anchorMax = Vector2.one;
            pickerTextRt.sizeDelta = new Vector2(-24, -16);
            pickerTextRt.anchoredPosition = Vector2.zero;
            pickerGo.SetActive(false);

            GameObject rUIHost = new GameObject("ResearchUIHost");
            rUIHost.transform.SetParent(canvasGo.transform, false);
            rUIHost.AddComponent<RectTransform>();
            ResearchUI rUI = rUIHost.AddComponent<ResearchUI>();
            rUI.SetRefs(resStatus, resProg, pickerRt, pickerText);

            // R10e: Tutorial overlay extract -> SceneSetup.Game.Tutorial.cs
            GenerateTutorialOverlay(canvasGo, uiFont, resProg);

            // ---------- PawnInfoPanel (bottom-left, Day 55: 380x200 — health text 영역) ----------
            GameObject panelGo = new GameObject("PawnInfoPanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            Image panelBg = panelGo.AddComponent<Image>();
            panelBg.color = colPanel;
            RectTransform panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 0f);
            panelRt.anchorMax = new Vector2(0f, 0f);
            panelRt.pivot = new Vector2(0f, 0f);
            panelRt.sizeDelta = new Vector2(380, 200);
            panelRt.anchoredPosition = new Vector2(12, 64); // leave room for save/load row below

            // Title text (visible when pawn selected)
            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            Text title = titleGo.AddComponent<Text>();
            title.text = "Colonist";
            title.alignment = TextAnchor.UpperLeft;
            title.font = uiFont;
            title.fontSize = 22;
            title.color = colTextPrimary;
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.sizeDelta = new Vector2(-20, 26);
            titleRt.anchoredPosition = new Vector2(10, -8);

            // 3 need bars — accent palette
            Image foodBar  = CreateNeedBar(panelGo.transform, "식량",  new Vector2(10, 105), colAccentFood, uiFont, colTextPrimary);
            Image sleepBar = CreateNeedBar(panelGo.transform, "수면", new Vector2(10, 65),  new Color(0.4f, 0.6f, 0.9f, 1f), uiFont, colTextPrimary);
            Image moodBar  = CreateNeedBar(panelGo.transform, "기분",  new Vector2(10, 25),  colAccentWood, uiFont, colTextPrimary);

            // Empty-state hint — replaces the panel content when no pawn is selected.
            // PawnInfoPanel toggles emptyText visibility based on selection.
            GameObject emptyGo = new GameObject("EmptyText");
            emptyGo.transform.SetParent(panelGo.transform, false);
            Text empty = emptyGo.AddComponent<Text>();
            empty.text = "콜로니스트를 클릭하세요";
            empty.alignment = TextAnchor.MiddleCenter;
            empty.font = uiFont;
            empty.fontSize = 12;
            empty.color = colTextMuted;
            RectTransform emptyRt = emptyGo.GetComponent<RectTransform>();
            emptyRt.anchorMin = Vector2.zero;
            emptyRt.anchorMax = Vector2.one;
            emptyRt.sizeDelta = Vector2.zero;
            emptyRt.anchoredPosition = Vector2.zero;

            // ---------- SaveLoad buttons (bottom-left corner, 40x40 each) ----------
            GameObject saveBtnPanel = new GameObject("SaveLoadButtons");
            saveBtnPanel.transform.SetParent(canvasGo.transform, false);
            RectTransform sbpRt = saveBtnPanel.AddComponent<RectTransform>();
            sbpRt.anchorMin = new Vector2(0f, 0f);
            sbpRt.anchorMax = new Vector2(0f, 0f);
            sbpRt.pivot = new Vector2(0f, 0f);
            sbpRt.sizeDelta = new Vector2(92, 40);
            sbpRt.anchoredPosition = new Vector2(12, 12);

            GameObject saveBtnGo = CreateIconButton(saveBtnPanel.transform, "SaveBtn", "S", new Vector2(0, 0), colPanel, colTextPrimary, uiFont);
            GameObject loadBtnGo = CreateIconButton(saveBtnPanel.transform, "LoadBtn", "L", new Vector2(48, 0), colPanel, colTextPrimary, uiFont);

            GameSaveButtons gsb = saveBtnPanel.AddComponent<GameSaveButtons>();
            SerializedObject gsbSo = new SerializedObject(gsb);
            gsbSo.FindProperty("saveButton").objectReferenceValue = saveBtnGo.GetComponent<Button>();
            gsbSo.FindProperty("loadButton").objectReferenceValue = loadBtnGo.GetComponent<Button>();
            gsbSo.FindProperty("pawnPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(PawnPrefabPath);
            gsbSo.FindProperty("treeSprite").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/tree.png");
            gsbSo.ApplyModifiedProperties();

            // ---------- Control hint (bottom-center, muted) ----------
            GameObject hintGo = new GameObject("ControlHint");
            hintGo.transform.SetParent(canvasGo.transform, false);
            Text hintText = hintGo.AddComponent<Text>();
            hintText.text = "WASD/휠/123/Space · B:벽5 · F:바닥1 · G:문3 · T:화덕10 · 1일=4분(1x)";
            hintText.font = uiFont;
            hintText.fontSize = 18;
            Color hintCol = colTextMuted; hintCol.a = 0.75f;
            hintText.color = hintCol;
            hintText.alignment = TextAnchor.MiddleCenter;
            RectTransform hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0.5f, 0f);
            hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.sizeDelta = new Vector2(640, 20);
            hintRt.anchoredPosition = new Vector2(0, 14);

            // Day 55: 부위별 health text (panel 안쪽 좌측 영역)
            GameObject healthGo = new GameObject("HealthText");
            healthGo.transform.SetParent(panelGo.transform, false);
            Text healthText = healthGo.AddComponent<Text>();
            healthText.text = "";
            healthText.font = uiFont;
            healthText.fontSize = 13;
            healthText.color = colTextPrimary;
            healthText.alignment = TextAnchor.UpperLeft;
            healthText.supportRichText = true;
            healthText.horizontalOverflow = HorizontalWrapMode.Wrap;
            healthText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform healthRt = healthGo.GetComponent<RectTransform>();
            // 패널 width 240, 아래쪽 정보 영역 위에 가로 정렬 → use right half top
            healthRt.anchorMin = new Vector2(0.55f, 0f);
            healthRt.anchorMax = new Vector2(1f, 1f);
            healthRt.pivot = new Vector2(0.5f, 0.5f);
            healthRt.sizeDelta = new Vector2(-8, -38);
            healthRt.anchoredPosition = new Vector2(0, -8);

            // Controller wiring
            PawnInfoPanel panel = panelGo.AddComponent<PawnInfoPanel>();
            SerializedObject pso = new SerializedObject(panel);
            pso.FindProperty("selector").objectReferenceValue = cs;
            pso.FindProperty("titleText").objectReferenceValue = title;
            pso.FindProperty("foodBar").objectReferenceValue = foodBar;
            pso.FindProperty("sleepBar").objectReferenceValue = sleepBar;
            pso.FindProperty("moodBar").objectReferenceValue = moodBar;
            pso.FindProperty("emptyText").objectReferenceValue = empty;
            pso.FindProperty("panelBg").objectReferenceValue = panelBg;
            pso.FindProperty("healthText").objectReferenceValue = healthText;
            pso.ApplyModifiedProperties();

            // Day 21: SkillUI panel — to the RIGHT of PawnInfoPanel.
            GameObject skillPanelGo = new GameObject("SkillPanel");
            skillPanelGo.transform.SetParent(canvasGo.transform, false);
            Image skillBg = skillPanelGo.AddComponent<Image>();
            skillBg.color = colPanel;
            RectTransform skRt = skillPanelGo.GetComponent<RectTransform>();
            skRt.anchorMin = new Vector2(0f, 0f);
            skRt.anchorMax = new Vector2(0f, 0f);
            skRt.pivot = new Vector2(0f, 0f);
            skRt.sizeDelta = new Vector2(180, 180);
            skRt.anchoredPosition = new Vector2(260, 60);

            Text MkSkillRow(string label, float yOffset, Color accent)
            {
                GameObject g = new GameObject(label + "Text");
                g.transform.SetParent(skillPanelGo.transform, false);
                Text t = g.AddComponent<Text>();
                t.text = $"{label}: Lv 0";
                t.font = uiFont;
                t.fontSize = 20;
                t.color = colTextPrimary;
                t.alignment = TextAnchor.MiddleLeft;
                RectTransform rt = g.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(-16, 24);
                rt.anchoredPosition = new Vector2(8, -yOffset);
                return t;
            }
            Text gatherT = MkSkillRow("채집", 14, colAccentFood);
            Text chopT   = MkSkillRow("벌목", 42, colAccentWood);
            Text buildT  = MkSkillRow("건축", 70, colTextPrimary);
            Text combatT = MkSkillRow("전투", 98, colAccentWarn);

            SkillUI skUI = skillPanelGo.AddComponent<SkillUI>();
            SerializedObject skSo = new SerializedObject(skUI);
            skSo.FindProperty("selector").objectReferenceValue = cs;
            skSo.FindProperty("gatherText").objectReferenceValue = gatherT;
            skSo.FindProperty("chopText").objectReferenceValue = chopT;
            skSo.FindProperty("buildText").objectReferenceValue = buildT;
            skSo.FindProperty("combatText").objectReferenceValue = combatT;
            skSo.FindProperty("container").objectReferenceValue = skillPanelGo;
            skSo.ApplyModifiedProperties();
            skillPanelGo.SetActive(false);  // start hidden until pawn selected

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
