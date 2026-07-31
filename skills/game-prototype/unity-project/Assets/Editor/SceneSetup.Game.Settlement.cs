using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using MelonS.GameProto;

namespace MelonS.GameProto.EditorTools
{
    // R10 - SceneSetup.cs 의 GenerateGame 안의 Settlement (Day 57) 블록 extract.
    //   wall 5 + floor 6 + stove + bench + 12 crops + 2 lamps + 9 stockpile markers.
    //   원본 SceneSetup.cs L340-442 (100 LOC).
    public static partial class SceneSetup
    {
        // 집터 사각형 — GenerateStarterSettlement 이 채우고 ClearStarterHouseSite 가 읽는다.
        //  둘이 같은 값을 각자 들고 있으면 그림자 상수가 되므로 한 곳에만 둔다.
        //  2026-07-30 3차 이동: x −10..−5 → **−14..−9**.
        //  시작 집이 기존 시나리오(그리고 자연스러운 플레이어)의 건축 자리를 통째로 먹었다 —
        //  `p0-basics-4axis` 는 청사진 13개 중 11개만 놓였고(벽·바닥 자리가 집과 겹침),
        //  `p3-rotate-bed` 는 침대를 집의 벽 모서리 (−5,−4) 에 놓으려다 0개가 됐다.
        //  두 시나리오가 쓰는 셀의 **최소 x 가 −8** 이므로, 집을 그 왼쪽 밖으로 뺀다.
        //  스폰(0,0)에서 9~14칸 서쪽 — 기본 줌(ortho 15, 가로 반폭 ≈26)에서 충분히 보인다.
        // 2026-08-01 — 콜로니스트를 3 → 6 인으로 늘리면서 집도 6인 규모로.
        //  이전 x −14..−9 는 내부가 4칸이라 침대 3개가 한계였다.  절반이 바닥에서
        //  자면 기분이 깎이고, '지붕 아래 잠자리 6' 목표도 시작부터 불가능해진다.
        //  x −17..−9 로 넓혀 내부 7칸 — 침대 6개가 한 줄로 들어간다.
        private static int _houseX0 = -17, _houseX1 = -9, _houseY0 = -4, _houseY1 = 0;

        /// <summary>
        /// 집터 안의 잔재(나무·덤불·광맥·풀 스캐터)를 제거한다.  **생성 파이프라인의 끝**에서
        /// 호출해야 한다 — 스캐터가 시작 캠프보다 뒤에 생성되므로, 캠프 생성 시점에만 치우면
        /// 그 뒤에 다시 놓인다 (실제로 덤불 하나가 집 안에 남았다).
        /// </summary>
        //  정리 범위 = 집터가 아니라 **캠프 전체**(집 + 서쪽 광장 + 북쪽 밭).
        //  2026-07-30 운영자 "배치도 이쁘게" — 집만 치우고 광장·밭은 잡초·나무가 그대로면
        //  '정리된 정착지'가 아니라 '숲에 박힌 집'으로 보인다.  사람은 집을 지으면
        //  마당까지 치운다.  x −19..−9 · y −5..4 (전부 시나리오 구역 x ≥ −8 바깥).
        private const int CampX0 = -19, CampX1 = -9, CampY0 = -5, CampY1 = 4;

        public static void ClearStarterHouseSite()
        {
            int cleared = 0;
            foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (sr == null) continue;
                var go = sr.gameObject;
                if (go.name.StartsWith("Starter")) continue;                  // 집·밭 자체
                if (go.name.StartsWith("Campfire")) continue;                 // 광장 모닥불
                if (go.GetComponentInParent<PawnEntity>() != null) continue;  // 콜로니스트
                var pos = go.transform.position;
                if (pos.x < CampX0 || pos.x > CampX1 + 1
                    || pos.y < CampY0 || pos.y > CampY1 + 1) continue;
                Object.DestroyImmediate(go);
                cleared++;
            }
            if (cleared > 0) Debug.Log($"[StarterCamp] 최종 캠프 정리: {cleared}개 제거");
        }

        private static void GenerateStarterSettlement(
            Tilemap tm, TileBase dirtTile,
            GameObject wallPrefab, GameObject floorPrefab,
            GameObject stovePrefab, GameObject benchPrefab)
        {
            // Day 57: 시작 정착지 - 화면 중앙 좌측에 미리 짠 구조물 + 농장 + stockpile.
            //  보여주기용 (벽 5 + 바닥 6 + 화덕 1 + 연구대 1).  pawn 1명이 이 안에
            //  자리잡고 시작하는 vanilla colony-sim 첫 게임 느낌.
            //  좌표 기준: 정착지 중심 (-5, 0).  방 크기 4x3.
            //
            //   X X X X X
            //   . . . . .   <- floor 안쪽 (음수 y)
            //   . . . . .
            //   X . . . X   <- 좌우 벽 + 가운데 floor
            //
            // 벽 5개: (-7, 1)..(-3, 1) 위쪽 가로벽 + 좌우 양끝 (-7, 0) (-3, 0)
            Sprite cropSprite         = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/crop_rice.png");
            Sprite cropSeedlingSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/crop_rice_seedling.png");
            Sprite cropGrowingSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/crop_rice_growing.png");
            if (cropSeedlingSprite == null) Debug.LogWarning("[SceneSetup] crop_rice_seedling.png null — stage-sprite wiring skipped");
            if (cropGrowingSprite  == null) Debug.LogWarning("[SceneSetup] crop_rice_growing.png null — stage-sprite wiring skipped");
            Sprite stockSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/stockpile_marker.png");

            // #229 맨땅 시작 정합 — 맨땅 시작.  미리 지어진 베이스(벽/바닥/화덕/
            //  연구대/작물/램프)를 제거하고 플레이어가 직접 건설하게 한다.  저장 구역만 유지
            //  (the reference sim 도 stockpile zone 은 무료 지정).  코드는 플래그로 보존(되돌리기 쉽게).
            bool prebuiltBase = false;
            if (prebuiltBase) {
            // 벽 5개 - 북쪽 벽 라인 (y=1, x=-7..-3) + 좌우 벽 (x=-7, x=-3, y=0)
            //  운영자 피드백: tile 시각 center 가 (x+0.5, y+0.5) 라서 entity 도 +0.5 정렬.
            Vector2Int[] wallSpots = new[]
            {
                new Vector2Int(-7, 1), new Vector2Int(-6, 1), new Vector2Int(-5, 1),
                new Vector2Int(-4, 1), new Vector2Int(-3, 1),
            };
            foreach (var w in wallSpots)
            {
                GameObject wGo = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab);
                wGo.name = $"StarterWall_{w.x}_{w.y}";
                wGo.transform.position = new Vector3(w.x + 0.5f, w.y + 0.5f, 0f);
            }
            // 바닥 6타일 (인테리어)
            Vector2Int[] floorSpots = new[]
            {
                new Vector2Int(-6, 0), new Vector2Int(-5, 0), new Vector2Int(-4, 0),
                new Vector2Int(-6, -1), new Vector2Int(-5, -1), new Vector2Int(-4, -1),
            };
            foreach (var f in floorSpots)
            {
                GameObject fGo = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab);
                fGo.name = $"StarterFloor_{f.x}_{f.y}";
                fGo.transform.position = new Vector3(f.x + 0.5f, f.y + 0.5f, 0f);
            }
            // 시작 화덕 + 시작 연구대 (tile center 정렬)
            GameObject starterStove = (GameObject)PrefabUtility.InstantiatePrefab(stovePrefab);
            starterStove.name = "StarterStove";
            starterStove.transform.position = new Vector3(-4f + 0.5f, 0f + 0.5f, 0f);
            GameObject starterBench = (GameObject)PrefabUtility.InstantiatePrefab(benchPrefab);
            starterBench.name = "StarterResearchBench";
            starterBench.transform.position = new Vector3(-6f + 0.5f, 0f + 0.5f, 0f);

            // Day 57: 농장 - 4x3 (12 타일) 작은 농장 (우측에 배치).  단순 데코로
            //  생장 시각 (lvl 1 sprout). Day 67-68에서 실제 농경 로직 추가 예정.
            //  좌표 (3..6, -2..-4).
            for (int cx = 3; cx <= 6; cx++)
            {
                for (int cy = -4; cy <= -2; cy++)
                {
                    GameObject cGo = new GameObject($"Crop_{cx}_{cy}");
                    cGo.transform.position = new Vector3(cx + 0.5f, cy + 0.5f, 0f);
                    var csr = cGo.AddComponent<SpriteRenderer>();
                    csr.sprite = cropSprite;
                    csr.sortingOrder = 3;
                    // Day 67-68: CropEntity component → 자라서 익으면 우클릭 수확
                    var ce = cGo.AddComponent<CropEntity>();
                    // Day 79: 시작 시 growth 0.4~0.85 무작위로 - 곧 수확 가능, 다양한 stage 시각.
                    var ceField = typeof(CropEntity).GetField("growth",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    if (ceField != null)
                    {
                        float startGrowth = 0.4f + ((cx + cy + 100) % 5) * 0.10f;  // deterministic
                        ceField.SetValue(ce, startGrowth);
                    }
                    // W-M1-02 Art: wire the three per-stage sprite refs via
                    // SerializedObject so [SerializeField] private fields persist
                    // into the scene.  When all three are assigned, CropEntity
                    // switches sprites at runtime instead of color-tinting.
                    if (cropSeedlingSprite != null && cropGrowingSprite != null && cropSprite != null)
                    {
                        var ceSo = new SerializedObject(ce);
                        ceSo.FindProperty("spriteSeedling").objectReferenceValue = cropSeedlingSprite;
                        ceSo.FindProperty("spriteGrowing").objectReferenceValue  = cropGrowingSprite;
                        ceSo.FindProperty("spriteRipe").objectReferenceValue     = cropSprite;
                        ceSo.ApplyModifiedPropertiesWithoutUndo();
                    }
                    // 농경지 dirt 타일 강제 부착 (배경)
                    tm.SetTile(new Vector3Int(cx, cy, 0), dirtTile);
                }
            }

            // Stretch: 가로등 2개 - 시작 정착지 북쪽 벽 위 (tile center 정렬)
            Sprite lampSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/struct32_lamp.png");
            //  tile (x, y) 시각 center = (x+0.5, y+0.5).  벽 위쪽 (y=2) 행에 배치.
            Vector2Int[] lampSpots = { new Vector2Int(-7, 2), new Vector2Int(-3, 2) };
            foreach (var lp in lampSpots)
            {
                GameObject lampGo = new GameObject($"Lamp_{lp.x}_{lp.y}");
                lampGo.transform.position = new Vector3(lp.x + 0.5f, lp.y + 0.5f, 0f);
                var lsr = lampGo.AddComponent<SpriteRenderer>();
                lsr.sprite = lampSprite;
                lsr.sortingOrder = 6;
            }

            } // #229 end prebuiltBase — 맨땅 시작이라 위 구조물은 스폰 안 함

            // ── 최소 시작 캠프 (2026-07-30) ───────────────────────────────────
            //  #229 의 '맨땅 시작'은 유지한다 — 벽·바닥·램프는 여전히 플레이어 몫이다.
            //  다만 실측 결과 맨땅 상태에는 **상시 작업이 0** 이었다.  심사자 3분
            //  시뮬레이션에서 지정한 나무 한 그루를 벤 뒤로 2 게임일 동안 목재 333 ·
            //  석재 0 · 가치 428 이 **한 자리도 안 움직였다**.  콜로니 심에서 세계가
            //  멈춰 보이면 그건 연출 문제가 아니라 시뮬레이션이 관측 불가능한 것이다.
            //
            //  그래서 '보여주기용 완성된 기지'가 아니라 **자립 루프의 최소 종자**만 놓는다:
            //    · 침대 3 — 수면 회복(RestMul)과 기상 감정(+5)의 유일한 공급원.
            //               침대가 없으면 기분 목표가 구조적으로 낮게 고정된다.
            //    · 화덕   — 요리 작업이 생겨야 식량→식사 전환이 상시 작업이 된다.
            //    · 연구대 — "작업대 필요"로 막혀 있던 연구가 진행 가능해진다.
            //    · 경작지 6칸 — 수확·재파종이 매일 되풀이되는 작업을 만든다.
            //  이 넷이 있으면 플레이어가 아무 지시를 안 해도 콜로니스트가 자고·먹고·
            //  요리하고·수확한다 = '일감만 지정하면 스스로 판단한다'는 주장이 화면에서
            //  성립한다.  벽·창고·확장은 그대로 플레이어의 일이다.
            //
            //  ── 배치 규칙 (어기면 실제로 사고가 났다) ─────────────────────────
            //  (1) 스폰 지점을 피한다 — GameManager.spawnPositions = (-1.5,1.5) (1.5,0.5)
            //      (-0.5,-1.5), 모닥불 (0.5,-1.5).  1차 배치는 침대 하나가 (-0.5,-1.5) 로
            //      나와 콜로니스트 스폰 지점과 정확히 겹쳤다.
            //  (2) **스폰 남쪽 3×2 블록 (x −2..0, y −3..−2)** 을 피한다 — 플레이어가 처음
            //      경작지를 그리는 자리다.  2차 배치의 침대 (-2,-3) 이 이 블록을 물어
            //      `p0-basics-4axis` 의 '경작 6칸 지정'이 5칸만 등록되며 FAIL 했다.
            //      테스트만의 문제가 아니다 — 실제 플레이어도 같은 자리에서 막힌다.
            //
            //  ── 왜 '집' 이어야 하는가 (2026-07-30 운영자) ──────────────────────
            //  1·2차는 들판에 침대 3개와 화덕을 흩어 놓았다.  운영자 지적:
            //  "집안에 침대랑 도구들을 만들지 않는다", "집을 만드는게 사람의 플레이와는
            //  완전 다름".  맞는 말이다 — 사람은 방을 먼저 만들고 그 **안에** 가구를 넣는다.
            //  가구가 허허벌판에 떠 있으면 그 자체가 "이 게임은 사람처럼 안 논다"는
            //  신호가 된다.  그래서 벽으로 둘러싼 방을 만들고 침대·화덕·연구대를 그 안에 둔다.
            //
            //  ⚠ 지붕은 여기서 못 만든다.  RoofDesignation 은 런타임 싱글턴이고 지붕은
            //  빌더 노동으로만 승격된다(타이머 자동 승격은 폐기됨).  그래서 방은 벽·바닥까지만
            //  주고, 지붕 지정은 플레이어의 첫 과제로 남긴다 (튜토리얼 ②가 이미 가르친다).
            {
                GameObject bedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bed.prefab");
                if (bedPrefab == null) Debug.LogWarning("[StarterCamp] Bed.prefab 없음 — 침대 생략");

                // 집: 바깥 벽 x −10..−5, y −4..0 (6×5).  내부 x −9..−6, y −3..−1 (4×3).
                //  오른쪽 벽 (−5,−2) 한 칸은 **문간**으로 비운다 (문 프리팹 배선 없이 통행 확보).
                int hx0 = _houseX0, hx1 = _houseX1, hy0 = _houseY0, hy1 = _houseY1;
                // 2026-07-30 운영자: "동선도 생각해서 길을 내면서 지어야함. 길이라는 요소가
                //  없지만 가상의 길을 생각해서 배치를 할것.  그리고 배치도 이쁘게."
                //
                //  이전 배치는 문이 **동쪽**(hx1)에 있었는데 그쪽은 9칸 빈 잔디였고,
                //  밭은 집 북서쪽에 따로, 모닥불은 스폰(0,0)에 따로 떨어져 있었다.
                //  세 덩어리가 서로 아무 관계가 없어 '배치'가 아니라 '흩뿌림'이었다.
                //
                //  문을 **서쪽**(hx0)으로 돌려 하나의 축을 만든다:
                //
                //        밭 (−18..−16, 1..2)
                //          ↘
                //     모닥불 광장 ── 길 ── [문] 집 내부 통로 ──────→
                //       (−16,−2)   (−15,−2)      (−13..−10, −2)
                //
                //  · 문 바로 앞이 광장이라 나오자마자 사람이 모이는 곳이 보인다.
                //  · 집 내부의 빈 가운데 줄(y=−2)이 문과 **같은 높이**라, 문에서
                //    안쪽까지 한 직선으로 통한다 (침대는 아래 줄, 화덕·연구대는 위 줄).
                //  · 밭은 광장에서 북쪽으로 한 번 꺾으면 닿는다.
                //  전부 x ≤ −9 라 시나리오/플레이어의 건축 구역(x ≥ −8)을 침범하지 않는다.
                var doorGap = new Vector2Int(hx0, -2);

                // 2026-07-30 운영자: "집안에 풀이 있고 나무가 있고 그러네."
                //  집을 세우기만 하고 **그 자리에 원래 있던 것들을 안 치웠다** — 지형 생성이
                //  먼저 돌면서 깔아 둔 나무·덤불·광맥·풀 스캐터가 벽 안에 그대로 남는다.
                //  사람이 집을 지으면 그 자리를 먼저 정리한다.  집터 전체를 비우고,
                //  바닥도 잔디에서 흙으로 바꿔 실내가 실외처럼 보이지 않게 한다.
                //  (런타임 건축은 BuildManager.AutoClearObstacles 가 같은 일을 한다 —
                //   여기는 씬 베이크 시점의 같은 규칙이다.)
                int cleared = 0;
                for (int x = hx0; x <= hx1; x++)
                {
                    for (int y = hy0; y <= hy1; y++)
                    {
                        foreach (var hit in Physics2D.OverlapBoxAll(
                                     new Vector2(x + 0.5f, y + 0.5f), Vector2.one * 0.45f, 0f))
                        {
                            if (hit == null) continue;
                            var go = hit.gameObject;
                            if (go.GetComponent<TreeEntity>() != null
                                || go.GetComponent<BerryBushEntity>() != null
                                || go.GetComponent<StoneVeinEntity>() != null)
                            { Object.DestroyImmediate(go); cleared++; }
                        }
                        // 잔디 → 흙.  실내 바닥이 바깥 잔디와 같으면 '집 안'으로 안 읽힌다.
                        tm.SetTile(new Vector3Int(x, y, 0), dirtTile);
                    }
                }
                // 스캐터(풀·꽃·덤불 장식)는 콜라이더가 없어 OverlapBox 로 안 잡힌다.
                //  1차에는 이름 접두어("Grass"/"Scatter"/"Bush"…)로 지웠는데 **덤불 2개가
                //  집 안에 그대로 남았다** — 이름 규약이 제각각이라 열거로는 못 덮는다.
                //  그래서 이름이 아니라 **위치**로 쓴다: 집터 안에 있는 스프라이트 중
                //  집을 이루는 것(Starter*)과 콜로니스트만 남기고 전부 제거.
                //  ⚠ 타일맵은 SpriteRenderer 가 아니라 TilemapRenderer 라 영향 없다.
                foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                {
                    if (sr == null) continue;
                    var go = sr.gameObject;
                    if (go.name.StartsWith("Starter")) continue;              // 집 자체
                    if (go.GetComponentInParent<PawnEntity>() != null) continue;  // 콜로니스트
                    var pos = go.transform.position;
                    if (pos.x < hx0 || pos.x > hx1 + 1 || pos.y < hy0 || pos.y > hy1 + 1) continue;
                    Object.DestroyImmediate(go);
                    cleared++;
                }
                Debug.Log($"[StarterCamp] 집터 정리: {cleared}개 제거, 바닥 흙으로 교체");
                if (wallPrefab != null)
                {
                    for (int x = hx0; x <= hx1; x++)
                    {
                        for (int y = hy0; y <= hy1; y++)
                        {
                            bool onEdge = (x == hx0 || x == hx1 || y == hy0 || y == hy1);
                            if (!onEdge) continue;
                            if (x == doorGap.x && y == doorGap.y) continue;   // 문간
                            var wGo = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab);
                            wGo.name = $"StarterWall_{x}_{y}";
                            wGo.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0f);
                        }
                    }
                    // 2026-07-31 — 문간에 **실제 문**을 놓는다.
                    //  여기는 여태 벽을 건너뛰기만 하고(위 continue) 아무것도 놓지 않아서,
                    //  시작 집은 벽에 구멍이 뚫린 채였다.  두 가지가 동시에 잘못됐다:
                    //    · 보이는 것: 사람이 사는 집에 문이 없다
                    //    · 안 보이는 것: 방이 밀폐가 아니라 자동 지붕이 안 붙는다 →
                    //      정착 목표가 "지붕 아래 잠자리 0/6" 으로 굳는다.  실측 로그:
                    //      `[Roof] 밀폐 아님 (-12,-3) — 벽셀=False 불통과=False`.
                    //  문은 벽처럼 방을 닫으면서 사람은 통과시킨다(RoofDesignation.IsWallCell
                    //  이 DoorEntity 를 경계로 세는 이유).  침대 프리팹과 같은 로드 방식.
                    var doorPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Door.prefab");
                    if (doorPrefabAsset != null)
                    {
                        var dGo = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefabAsset);
                        dGo.name = $"StarterDoor_{doorGap.x}_{doorGap.y}";
                        dGo.transform.position = new Vector3(doorGap.x + 0.5f, doorGap.y + 0.5f, 0f);
                    }
                    else Debug.LogWarning("[StarterCamp] Door.prefab 없음 — 문간이 뚫린 채로 남는다(지붕 미형성)");
                }
                if (floorPrefab != null)
                {
                    for (int x = hx0 + 1; x < hx1; x++)
                        for (int y = hy0 + 1; y < hy1; y++)
                        {
                            var fGo = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab);
                            fGo.name = $"StarterFloor_{x}_{y}";
                            fGo.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0f);
                        }
                }

                // 가구는 **방 안에**.  침대는 안쪽 벽에 붙여 한 줄로, 도구는 반대편에.
                // 침대 6개 — 콜로니스트 수와 같게 (내부 x −16..−10 중 6칸 사용).
                Vector2Int[] bedSpots = {
                    new Vector2Int(-16, -3), new Vector2Int(-15, -3), new Vector2Int(-14, -3),
                    new Vector2Int(-13, -3), new Vector2Int(-12, -3), new Vector2Int(-11, -3),
                };
                foreach (var b in bedSpots)
                {
                    if (bedPrefab == null) break;
                    var bGo = (GameObject)PrefabUtility.InstantiatePrefab(bedPrefab);
                    bGo.name = $"StarterBed_{b.x}_{b.y}";
                    bGo.transform.position = new Vector3(b.x + 0.5f, b.y + 0.5f, 0f);
                }

                if (stovePrefab != null)
                {
                    var sGo = (GameObject)PrefabUtility.InstantiatePrefab(stovePrefab);
                    sGo.name = "StarterCampStove";
                    sGo.transform.position = new Vector3(-13f + 0.5f, -1f + 0.5f, 0f);
                }
                if (benchPrefab != null)
                {
                    var rGo = (GameObject)PrefabUtility.InstantiatePrefab(benchPrefab);
                    rGo.name = "StarterCampBench";
                    rGo.transform.position = new Vector3(-11f + 0.5f, -1f + 0.5f, 0f);
                }

                // 경작지 6칸 — 성장 단계를 흩어 놓아 첫날부터 수확이 하나씩 나온다.
                //  (전부 같은 단계면 하루 몰아치고 그 뒤 다시 정지한다.)
                Sprite cSeed = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/crop_rice_seedling.png");
                Sprite cGrow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/crop_rice_growing.png");
                Sprite cRipe = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/crop_rice.png");
                int ci = 0;
                //  경작지도 스폰 (-1.5,1.5) 를 비켜 x = -5..-3 으로.
                //  2026-07-30 운영자 "집이랑 농장이랑 겹쳐 짓는 이유가 머야?"
                //  좌표상으론 안 겹쳤다 — 집 윗벽이 y=0, 농장이 y=1..2 로 **맞붙어** 있었다.
                //  그런데 작물 스프라이트는 한 칸보다 커서 위아래로 삐져나오고, 그래서
                //  밭의 나무 테두리가 벽 위에 그려져 '겹쳐 지은 것'으로 보였다.
                //  타일 단위로 안 겹치는 것과 **화면에서 안 겹치는 것은 다르다** —
                //  한 칸(y=1)을 비워 통로 겸 시각적 간격으로 둔다.
                for (int cx = -18; cx <= -16; cx++)
                {
                    for (int cy = 2; cy <= 3; cy++)
                    {
                        var cGo = new GameObject($"StarterCrop_{cx}_{cy}");
                        cGo.transform.position = new Vector3(cx + 0.5f, cy + 0.5f, 0f);
                        var csr = cGo.AddComponent<SpriteRenderer>();
                        csr.sprite = cRipe;
                        csr.sortingOrder = 3;
                        var ce = cGo.AddComponent<CropEntity>();
                        var gf = typeof(CropEntity).GetField("growth",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        //  0.35 / 0.50 / 0.65 / 0.80 / 0.95 / 1.00 로 계단식 (결정론)
                        if (gf != null) gf.SetValue(ce, 0.35f + ci * 0.13f);
                        if (cSeed != null && cGrow != null && cRipe != null)
                        {
                            var so = new SerializedObject(ce);
                            so.FindProperty("spriteSeedling").objectReferenceValue = cSeed;
                            so.FindProperty("spriteGrowing").objectReferenceValue  = cGrow;
                            so.FindProperty("spriteRipe").objectReferenceValue     = cRipe;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                        tm.SetTile(new Vector3Int(cx, cy, 0), dirtTile);
                        ci++;
                    }
                }
                // 광장 — 문(−14,−2) 앞 3×3 을 흙바닥으로 깐다.
                //  1차 배치에서 광장이 그냥 잔디라 "여기가 마당"이라는 신호가 전혀 없었다.
                //  주변과 같은 지면이면 사람은 그것을 공간으로 인식하지 못한다 —
                //  바닥이 바뀌어야 '다져진 마당'으로 읽히고, 그게 곧 가상의 길이 된다.
                //  집 안(흙) ↔ 문간 ↔ 광장(흙) 이 한 색으로 이어져 동선이 눈에 보인다.
                int plaza = 0;
                for (int px = -17; px <= -15; px++)
                {
                    for (int py = -3; py <= -1; py++)
                    {
                        tm.SetTile(new Vector3Int(px, py, 0), dirtTile);
                        plaza++;
                    }
                }
                // 문 앞 한 칸(−15,−2)은 위 루프에 포함 — 문에서 광장까지 흙이 끊기지 않는다.

                Debug.Log($"[StarterCamp] 침대 {bedSpots.Length} · 화덕 1 · 연구대 1 · 경작지 {ci}칸 · 광장 {plaza}칸");
            }

            // #246 운영자 fb "게임 시작 시 창고영역 세팅 안 되어 있고 유저가 정하게" —
            //  시작 시 stockpile zone 자동 스폰 안 함.  플레이어가 구상>지시>저장(O)로 직접
            //  지정 (the reference sim: 시작 시 저장구역 없음).
            bool autoStockpile = false;
            if (autoStockpile)
            for (int sx = -2; sx <= 0; sx++)
            {
                for (int sy = -3; sy <= -1; sy++)
                {
                    StockpileZoneEntity.Spawn(new Vector3(sx, sy, 0f), stockSprite);
                }
            }

            // #게임필 배치3(2026-06-10 자율) — 스폰 중심 모닥불 1개: 맨땅 시작(#229)이라
            //  밤 화면에 광원이 0 — NightOverlay 가 어둡기만 하고 '야영지' 라는 정체성이
            //  없었다.  FlickerLight 는 비-Stove 객체에선 상시 은은 점멸(IsLit 폴백 true).
            //  콜라이더 없음(통행 무방해), 빌드/세이브 시스템 불간섭(순수 비주얼).
            Sprite fireSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/glow_fire_pool.png");
            if (fireSprite != null)
            {
                GameObject fireGo = new GameObject("Campfire_Spawn");
                //  광장 = 문(−14,−2) 바로 서쪽 두 칸을 비우고 그 끝에 모닥불.
                //  스폰(0,0)에 홀로 있던 것을 캠프의 중심으로 옮긴다 (2026-07-30 동선 정리).
                // 2026-08-01 — 집을 x −17 까지 넓히면서 이 자리(−16,−2)가 **집 안**이 됐다.
                //  모닥불은 야외 광장 장치다(여가 시간에 모이는 자리).  광장 남쪽으로 옮긴다.
                fireGo.transform.position = new Vector3(-15f + 0.5f, -7f + 0.5f, 0f);
                var fsr = fireGo.AddComponent<SpriteRenderer>();
                fsr.sprite = fireSprite;
                fsr.sortingOrder = 6;
                // 광장의 초점이 되려면 보여야 한다 — 1차 배치에선 희미한 주황 점이라
                //  '마당 한가운데 모닥불'로 안 읽혔다.  2.2배로 키워 3×3 광장을 채운다.
                fireGo.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
                fireGo.AddComponent<FlickerLight>();
            }
        }
    }
}
