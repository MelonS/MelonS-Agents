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
            // 팔레트 시스템 (2026-07-25): 베이크는 항상 디폴트(크림) — 로컬 pref 가
            //  씬 해시에 새지 않게 (베이크 결정론).
            MelonS.GameProto.Core.UITheme.ForcePalette(0);
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
                "Assets/Sprites/pawn_blue.png",   // #199 A2 — colonist 0 셔츠 (muted denim blue)
                "Assets/Sprites/pawn_rust.png",   // #199 A2 — colonist 1 셔츠 (muted rust)
                "Assets/Sprites/pawn_olive.png",  // #199 A2 — colonist 2 셔츠 (muted olive)
                "Assets/Sprites/tile_grass.png",
                "Assets/Sprites/tile_dirt.png",
                "Assets/Sprites/tile_water.png",
                "Assets/Sprites/tile_rock.png",
                "Assets/Sprites/decor_flower.png",
                "Assets/Sprites/tree.png",
                "Assets/Sprites/struct32_wall_wood.png",
                "Assets/Sprites/struct32_floor_wood.png",
                "Assets/Sprites/struct32_door_wood.png",
                "Assets/Sprites/deer.png",
                "Assets/Sprites/struct32_stove.png",
                "Assets/Sprites/struct32_research_bench.png",  // Day 52
                "Assets/Sprites/crop_rice.png",       // Day 57
                "Assets/Sprites/stockpile_marker.png",// Day 57
                "Assets/Sprites/wolf.png",            // Day 64
                "Assets/Sprites/arrow.png",           // Day 50
                "Assets/Sprites/trader.png",          // Stretch — Trader
                "Assets/Sprites/struct32_lamp.png",            // Stretch — Lamp
                "Assets/Sprites/struct32_bed_wood.png",        // #107 — Wood bed
                "Assets/Sprites/struct32_bed_fine.png",        // #198 D4-1 — Fine bed (royal-blue/gold)
                "Assets/Sprites/berry_bush.png",      // Round 6/7 — dedicated berry-bush art (was tree reuse + neon tint)
                "Assets/Sprites/shadow_small.png",    // Polish v2 — pawn grounding shadow (16x8, alpha baked)
                "Assets/Sprites/shadow_tree.png",     // Polish v2 — tree trunk grounding shadow (20x6, alpha baked)
                "Assets/Sprites/decor_rock.png",         // Polish v3 C — world scatter rock (8x8)
                "Assets/Sprites/grass_tuft.png",         // Polish v3 C — world scatter grass tuft (8x8)
                "Assets/Sprites/wildflower1.png",        // Polish v3 C — world scatter wildflower cross (8x8)
                "Assets/Sprites/wildflower2.png",        // Polish v3 C — world scatter wildflower droop (8x8)
                "Assets/Sprites/crop_rice_seedling.png", // W-M1-02 Art — crop stage 0 (seedling, 16x16)
                "Assets/Sprites/crop_rice_growing.png",  // W-M1-02 Art — crop stage 1 (growing, 16x16)
                "Assets/Sprites/vignette.png",           // W-M1-03 Art — V8 optional vignette overlay (512x512)
                "Assets/Resources/scatter/stone_chunk_small.png",  // W-M4-03 Lane B — scatter variety: small rock chunk (10x8); moved to Resources/scatter/ so ScatterVarietyDriver Resources.Load resolves; QA-FLAG W-M4-06 Lane B
                "Assets/Resources/scatter/dead_leaves.png",         // W-M4-03 Lane B — scatter variety: dead leaf cluster (8x8); moved to Resources/scatter/; QA-FLAG W-M4-06 Lane B
                "Assets/Resources/scatter/pebble_scatter.png",      // W-M4-03 Lane B — scatter variety: pebble group (10x6); moved to Resources/scatter/; QA-FLAG W-M4-06 Lane B
                "Assets/Sprites/stone_floor.png",         // W-M4-05 Lane B — stone/paved floor tile (16x16, PPU 16)
                "Assets/Sprites/table_chair.png",         // W-M4-06 Lane A — table+chair furniture (16x16, PPU 16); procedural fallback exists but import ensures real sprite resolves
                "Assets/Sprites/struct32_fence.png",               // W-M6-02 Lane B3 — fence low-barrier (16x16, PPU 16); procedural fallback exists
                "Assets/Sprites/struct32_fence_gate.png",          // W-M6-02 Lane B3 — fence-gate variant (16x16, PPU 16); procedural fallback exists
                "Assets/Sprites/carry_bundle.png",        // W-M6-02 Lane B10 — carry-bundle overlay sprite (8x8, PPU 16); also copied to Resources/Sprites/ for Resources.Load path
                "Assets/Sprites/stone_vein.png",          // #119 — minable stone vein (16x16, PPU 16); ForceImport ensures Sprite type before SpawnStoneVeins LoadAssetAtPath
            };
            foreach (var p in paths)
            {
                ImportSprite(p, 16);
            }

            // Round 7 — top-bar resource ICONS (24x24).  PPU=24 so a 24px source
            //   maps 1:1 to the 24x24 UI slot when read at native size; UI Image
            //   in any case scales to its RectTransform, so this just keeps the
            //   .meta self-consistent with the slot size.
            string[] iconPaths = new[]
            {
                "Assets/Sprites/icon_stone.png",
                "Assets/Sprites/icon_wood.png",
                "Assets/Sprites/icon_meal.png",
                "Assets/Sprites/icon_food.png",
            };
            foreach (var p in iconPaths)
            {
                ImportSprite(p, 24);
            }

            // 구역 셀 아이콘 (2026-07-29) — 64x64, scripts/gen-ui-icons.py 산출.
            //  등록하지 않으면 텍스처 타입이 Sprite 로 확정되지 않아 ArchitectMenu.LoadIcon
            //  이 null 을 돌려받고, 셀은 조용히 옛 '첫 글자' 폴백으로 되돌아간다
            //  (빌드도 게이트도 통과하는 종류의 미반영 — 신규 png 임포트 누락은
            //   이 레포에서 반복된 함정이라 반드시 목록에 넣는다).
            foreach (var p in new[]
            {
                "Assets/Sprites/icon_zone_farm.png",
                "Assets/Sprites/icon_zone_stock.png",
                "Assets/Sprites/icon_zone_dump.png",
                "Assets/Sprites/icon_zone_roof.png",
                // Resources 사본도 **반드시** 함께 등록한다.  LoadIcon 은 에디터에선
                //  Assets/Sprites 를, **런타임 빌드에선 Resources/Sprites** 를 본다.
                //  1차 배선 때 Assets 쪽만 등록해 Resources 사본이 spriteMode:0 으로
                //  남았고, 빌드에서 Resources.Load<Sprite> 가 null → 셀이 조용히 옛
                //  '첫 글자' 폴백으로 돌아갔다 (에디터에선 멀쩡해 보여 더 헷갈린다).
                "Assets/Resources/Sprites/icon_zone_farm.png",
                "Assets/Resources/Sprites/icon_zone_stock.png",
                "Assets/Resources/Sprites/icon_zone_dump.png",
                "Assets/Resources/Sprites/icon_zone_roof.png",
            })
            {
                ImportSprite(p, 64);
            }

            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }

        // Round 7 — extracted from the inline loop so the icon set can opt into a
        //   different PPU (24) than the world sprites (16) while sharing the exact
        //   same Sprite/Point/no-compress import path.
        private static void ImportSprite(string p, int ppu)
        {
            if (!File.Exists(p)) { Debug.LogWarning($"[SceneSetup] missing: {p}"); return; }
            // Force re-import to ensure .meta exists + Sprite type
            AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            TextureImporter ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) { Debug.LogWarning($"[SceneSetup] no TextureImporter for {p}"); return; }
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ppu;
            ti.filterMode = FilterMode.Point;
            ti.SaveAndReimport();
            Debug.Log($"[SceneSetup] forced sprite import: {p} (PPU {ppu})");
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
            // ⚠️ 여기 있던 `const int MAP_HALF = 20;` 를 제거했다 (2026-07-29).
            //  TerrainLayout.MAP_HALF 를 **가리는 지역 상수**였다.  맵이 40x40 →
            //  60x60(#108) → 90x90(#235) 으로 두 번 커지는 동안 지형 쪽 상수만
            //  45 로 갱신되고 이 값은 20 에 남아, SpawnTrees 만 옛 40x40 범위에
            //  나무를 뿌리고 있었다.  실측: 나무 x -16~18 / y -18~18, 맵은 -45~45
            //  → 중심거리 30~45 구간(전체 면적의 56%)에 **0그루**.
            //  운영자 2026-07-29 "화면을 축소해서 맵 전체를 보면 나무가 없어".
            //  같은 이름의 지역 상수를 두지 않는다 — 아래는 전부 정본을 참조한다.
            SetupFlowerDecor(layout);
            // Polish v3 C — scatter rock/grass/wildflower decor
            SpawnScatterDecor(layout);

            // GameManager + spawn pawn via prefab
            GameObject gmGo = new GameObject("GameManager");
            GameManager gm = gmGo.AddComponent<GameManager>();
            GameObject pawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PawnPrefabPath);
            Sprite arrowSpriteRef = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/arrow.png");
            // #116 - wood pile sprite (벌목 후 바닥에 떨어지는 통나무 더미)
            // 아트 B2: TS 자원 더미 우선 (128px·그림자 베이크 → PPU160 = 0.8칸).
            //  stone 은 팩에 등가물 없음 — 구세대 유지 (정합 생성 후속).
            Sprite woodPileSpriteRef = ImportSpriteAt("Assets/Sprites/ts_wood_pile.png", 160f)
                                       ?? LoadOrSetupSprite("Assets/Sprites/wood_pile.png");
            Sprite stoneChunkSpriteRef = LoadOrSetupSprite("Assets/Sprites/stone_chunk.png");  // #119
            Sprite meatPileSpriteRef = ImportSpriteAt("Assets/Sprites/ts_meat_pile.png", 160f)
                                       ?? LoadOrSetupSprite("Assets/Sprites/meat_pile.png");   // #129
            // #199 A2 — per-colonist 셔츠 변형 sprite 3종 (blue/rust/olive).
            //  ForceImportAllSprites 가 이미 Sprite 타입 + PPU16 으로 import 함.
            // 아트 B2: 시작 3인방도 TS 폰 (blue/red/yellow — rust→red, olive→yellow 매핑)
            Sprite pawnBlueRef  = ImportSpriteAt("Assets/Sprites/ts_pawn_blue.png", 96f)
                                  ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/pawn_blue.png");
            Sprite pawnRustRef  = ImportSpriteAt("Assets/Sprites/ts_pawn_red.png", 96f)
                                  ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/pawn_rust.png");
            Sprite pawnOliveRef = ImportSpriteAt("Assets/Sprites/ts_pawn_yellow.png", 96f)
                                  ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/pawn_olive.png");
            if (pawnBlueRef == null)  Debug.LogWarning("[SceneSetup] pawn_blue.png sprite null");
            if (pawnRustRef == null)  Debug.LogWarning("[SceneSetup] pawn_rust.png sprite null");
            if (pawnOliveRef == null) Debug.LogWarning("[SceneSetup] pawn_olive.png sprite null");

            SerializedObject gmSo = new SerializedObject(gm);
            gmSo.FindProperty("pawnPrefab").objectReferenceValue = pawnPrefab;
            // #54 콜로니스트 외형 다양화: per-pawn 변형 8종(pawn_v0..v7) — 머리색/피부톤/옷색
            //  조합으로 개인 구분.  GameManager 가 colonist i 에 colonistVariantSprites[i] 배정.
            //  (PawnSpriteBob 이 root 변형 sprite 를 가시 자식에 mirror — 이전엔 color 만 mirror 라
            //   변형이 안 보였음, 같은 배치에서 수정.)
            var variantsProp = gmSo.FindProperty("colonistVariantSprites");
            const int NV = 8;
            variantsProp.arraySize = NV;
            // 2026-07-29 — ts_pawn_* 우선 배정을 되돌린다.  두 가지 이유:
            //
            //  (1) 실제로 화면에 안 나왔다.  가시 스프라이트의 주인은 PawnSpriteAnimator
            //      이고, 그건 루트 스프라이트 **이름**에서 `_v{digit}` 를 파싱해
            //      `Resources/pawn32/pawn32_v{n}` 시트를 고른다.  `ts_pawn_blue` 는
            //      파싱에 실패해 **이름 해시 폴백**으로 떨어졌고, 시작 콜로니스트 3인의
            //      이름이 공교롭게 전부 파랑 계열(v0/v1/v6)로 사상돼 셋이 똑같이 보였다.
            //      (라이브 캡처에서 발견 — 로그엔 경고가 없다. 폴백이 '성공'이라서.)
            //  (2) ts_pawn_* 은 팩의 탑다운 유닛이라 몸통 대비 머리가 압도적인 실루엣이다.
            //      우리 콜로니스트(인간형 16px)와 종이 다르게 읽힌다.
            //
            //  대신 pawn_v{i} 를 **옷색이 최대로 갈리는 순서**로 넣는다.  시작 3인이
            //  슬롯 0·1·2 를 가져가므로 그 셋이 파랑/러스트/리넨 — 팔레트가 정의한
            //  세 옷색과 정확히 일치한다.  '각자 다른 개인'이 이 게임의 주장이므로
            //  세 명이 한눈에 구분되는 것은 장식이 아니라 주장의 증거다.
            int[] variantOrder = { 0, 2, 4, 6, 1, 3, 5, 7 };
            for (int vi = 0; vi < NV; vi++)
            {
                int src = variantOrder[vi];
                var vs = LoadOrSetupSprite($"Assets/Sprites/pawn_v{src}.png");
                if (vs == null) Debug.LogWarning($"[SceneSetup] pawn_v{src}.png sprite null");
                variantsProp.GetArrayElementAtIndex(vi).objectReferenceValue = vs;
            }
            gmSo.FindProperty("arrowSpriteRuntime").objectReferenceValue = arrowSpriteRef;
            gmSo.FindProperty("woodPileSpriteRuntime").objectReferenceValue = woodPileSpriteRef;
            gmSo.FindProperty("stoneChunkSpriteRuntime").objectReferenceValue = stoneChunkSpriteRef;
            gmSo.FindProperty("meatPileSpriteRuntime").objectReferenceValue = meatPileSpriteRef;
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
            var treePositions = SpawnTrees(treePrefab, TerrainLayout.MAP_HALF,
                       lakeCenters, lakeRadii, rockClusterCenters, rockRadius);

            // #119 fix: StoneVeinEntity 8-14개 deterministic 배치 (rock cluster 근처 우선).
            //   R10 refactor 시 SetupWorldEntities 가 제거되면서 SpawnStoneVeins 호출이
            //   GenerateGame() 에서 누락됐던 것을 복구.
            //  ⚠ 복구 당시 **나무 목록 없는 오버로드**를 썼다.  광맥 배치에는 나무 회피
            //   로직(거리 1.8)이 이미 있는데 빈 목록을 받아 무력화됐고, 그래서 돌이 나무와
            //   겹쳐 스폰됐다 (운영자 2026-07-29 "돌이랑 나무 겹치는 문제").
            //   MAP_HALF 사고와 같은 계열 — 호출부가 정보를 잃어버린 것.
            SpawnStoneVeins(layout, treePositions);

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
            Color colPanel       = MelonS.GameProto.Core.UITheme.PanelBg;        // 팔레트 통일 (2026-07-25)
            Color colAccentWood  = new Color(0.659f, 0.459f, 0.263f, 1f);
            Color colAccentFood  = new Color(0.478f, 0.604f, 0.302f, 1f);
            Color colAccentWarn  = new Color(0.769f, 0.353f, 0.227f, 1f);
            Color colTextPrimary = MelonS.GameProto.Core.UITheme.TextPrimary;    // 팔레트 통일 (2026-07-25)
            Color colTextMuted   = MelonS.GameProto.Core.UITheme.TextSecondary;  // 팔레트 통일 (2026-07-25)
            // Korean font: try multiple OS fonts.  Malgun Gothic on Win10/11
            // is the safe default.  NanumGothic for Win7 installs that
            // grabbed the Nanum package separately.  Gulim is the legacy
            // shipped Korean font.  Final fallback = LegacyRuntime.ttf
            // (no Hangul → squares, but at least loads).
            // WebGL 근본수정 (2026-07-24): OS 폰트를 씬에 구우면 font data 없는 Font
            //  객체가 직렬화돼 WebGL 빌드가 거부된다("Need to include font data") —
            //  번들 Noto(OFL) 에셋 우선, OS 폰트는 에셋 부재 시 폴백.
            Font  uiFont         = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/GowunDodum.ttf")
                            ?? AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/NotoSansKR.ttf")
                                  ?? Font.CreateDynamicFontFromOSFont(
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
            string stovePath = "Assets/Sprites/struct32_stove.png";
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
