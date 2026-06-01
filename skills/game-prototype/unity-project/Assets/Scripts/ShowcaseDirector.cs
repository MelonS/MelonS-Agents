using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace MelonS.GameProto
{
    /// <summary>
    /// #265~#268 ShowcaseDirector — 녹화용 "사람이 직접 플레이하는 듯한" 연출.
    ///
    /// 운영자 요구 누적:
    ///  - 사람이 플레이하는 느낌 / 예쁜 집 / 줌인·줌아웃 / 림 행동이 잘 보이게.
    ///  - 건축은 청사진만 올리고 림이 직접 짓기 (#266).
    ///  - #268f 건축 UI 를 "사람과 똑같이" 사용: 가짜 커서가 건축버튼→카테고리→
    ///    건축물 버튼→맵 셀을 실제로 클릭(메뉴가 진짜 반응)하며 청사진을 놓는다.
    ///  - 집터의 돌(StoneVein)은 보기 싫으니 제거.
    ///
    /// 흐름: 돌 정리 → 인트로 → (커서로 건축 UI 사용해 청사진 배치) → 림이 직접 건축
    ///       추적(줌인) → 밤 수면 피날레(촛불 조명).
    /// </summary>
    public class ShowcaseDirector : MonoBehaviour
    {
        public static bool Enabled = false;
        public float totalSeconds = 150f;

        private Camera cam;
        private float camZ = -10f;
        private readonly List<BlueprintEntity> _placed = new List<BlueprintEntity>();

        // 가짜 커서
        private RectTransform _cursor;

        public static void Spawn(float seconds)
        {
            Enabled = true;
            var go = new GameObject("ShowcaseDirector");
            go.AddComponent<ShowcaseDirector>().totalSeconds = seconds;
        }

        // ---- 예쁜 집 설계 (7x6 외벽 + 바닥재 + 침대3 + 화덕 + 테이블 + 조명2) ------
        const int X0 = 2, X1 = 8, Y0 = 2, Y1 = 7;
        static readonly Vector2Int Door  = new Vector2Int(5, 2);
        static readonly Vector2Int[] Beds = {
            new Vector2Int(3, 5), new Vector2Int(5, 5), new Vector2Int(7, 5),
        };
        static readonly Vector2Int Stove = new Vector2Int(7, 3);
        static readonly Vector2Int Table = new Vector2Int(5, 4);
        static readonly Vector2Int[] Lamps = { new Vector2Int(4, 3), new Vector2Int(6, 6) };

        private Vector2 HouseCenter => new Vector2((X0 + X1) / 2f + 0.5f, (Y0 + Y1) / 2f + 0.5f);

        private void Start() { StartCoroutine(Run()); }

        private IEnumerator Run()
        {
            cam = Camera.main;
            if (cam == null) yield break;
            camZ = cam.transform.position.z;

            var cc = cam.GetComponent<CameraController>();
            if (cc != null) cc.enabled = false;
            if (TimeController.Instance != null) TimeController.Instance.SetScale(1f);
            TrySetClock(13f);
            ClearHouseArea();     // 집터 돌 제거
            CreateCursor();

            float T = totalSeconds;
            float introDur = Mathf.Max(6f, T * 0.05f);
            float buildWatchDur = T * 0.18f;   // 밤→아침 시퀀스에 시간 양보

            // 인트로: 집터 전경 줌인
            SnapCam(HouseCenter, 13f);
            yield return MoveCam(HouseCenter + new Vector2(0f, 0.5f), 9.5f, introDur);

            // 건축 UI 를 사람처럼 사용해 청사진 배치 (커서 클릭)
            yield return StartCoroutine(HumanBuildPlacement());

            // 림이 직접 건축하는 모습 — 건축 중인 림 따라가며 줌인
            yield return StartCoroutine(WatchPawnsBuild(buildWatchDur));

            FinishRemaining();

            // 밤(자연스럽게 침대로 → 3배속 타임랩스) → 아침(기상 → 밥/농사)
            yield return StartCoroutine(NightSleepThenMorning());
        }

        // ====================================================================
        //  집터 돌 제거 (운영자 fb: 돌이 집에 겹쳐 보기 싫음)
        // ====================================================================
        private void ClearHouseArea()
        {
            const int pad = 2;
            float minX = X0 - pad, maxX = X1 + pad, minY = Y0 - pad, maxY = Y1 + pad;

            foreach (var v in Object.FindObjectsByType<StoneVeinEntity>(FindObjectsSortMode.None))
            {
                if (v == null) continue;
                Vector2 p = v.transform.position;
                if (p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY)
                {
                    var c = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
                    if (PawnMovement.Grid != null) PawnMovement.Grid.SetWalkable(c, true);
                    Destroy(v.gameObject);
                }
            }
            foreach (var ch in Object.FindObjectsByType<StoneChunkEntity>(FindObjectsSortMode.None))
            {
                if (ch == null) continue;
                Vector2 p = ch.transform.position;
                if (p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY)
                    Destroy(ch.gameObject);
            }
        }

        // ====================================================================
        //  사람처럼 건축 UI 사용: 커서가 건축버튼→카테고리→건축물→맵셀 클릭
        // ====================================================================
        // (모드, 카테고리 라벨, 카테고리 내 buildable index, 셀들)
        private struct Group { public BuildManager.Mode mode; public string cat; public int idx; public List<Vector2Int> cells; }

        private List<Group> BuildGroups()
        {
            // #268 운영자 fb "집안 바닥 다 깔아줘" — 가구/조명 밑에도 바닥(초록 풀 안 보이게).
            //  바닥을 먼저 전부 깔고(아래 그룹 순서상 첫번째) 그 위에 가구를 올린다.
            var floors = new List<Vector2Int>();
            for (int x = X0 + 1; x <= X1 - 1; x++)
                for (int y = Y0 + 1; y <= Y1 - 1; y++)
                    floors.Add(new Vector2Int(x, y));

            var walls = new List<Vector2Int>();
            for (int x = X0; x <= X1; x++)
                for (int y = Y0; y <= Y1; y++)
                {
                    bool perim = (x == X0 || x == X1 || y == Y0 || y == Y1);
                    if (!perim) continue;
                    if (x == Door.x && y == Door.y) continue;
                    walls.Add(new Vector2Int(x, y));
                }

            return new List<Group>
            {
                new Group { mode = BuildManager.Mode.Floor,      cat = "Floors (바닥)",    idx = 0, cells = floors },
                new Group { mode = BuildManager.Mode.Wall,       cat = "Structure (구조)", idx = 0, cells = walls },
                new Group { mode = BuildManager.Mode.Door,       cat = "Structure (구조)", idx = 2, cells = new List<Vector2Int>{ Door } },
                new Group { mode = BuildManager.Mode.Bed,        cat = "Furniture (가구)", idx = 1, cells = new List<Vector2Int>(Beds) },
                new Group { mode = BuildManager.Mode.Stove,      cat = "Production (생산)",idx = 0, cells = new List<Vector2Int>{ Stove } },
                new Group { mode = BuildManager.Mode.TableChair, cat = "Furniture (가구)", idx = 3, cells = new List<Vector2Int>{ Table } },
                new Group { mode = BuildManager.Mode.Lamp,       cat = "Lighting (조명)",  idx = 0, cells = new List<Vector2Int>(Lamps) },
            };
        }

        private IEnumerator HumanBuildPlacement()
        {
            // 메뉴가 좌측을 덮으므로 집을 화면 우측에 두도록 카메라를 살짝 좌측에 둔다.
            yield return MoveCam(HouseCenter + new Vector2(-3.5f, 0f), 8.0f, 1.5f);

            var bm = BuildManager.Instance;
            var menu = ArchitectMenu.Instance;
            if (bm == null || menu == null) { yield return Wait(2f); yield break; }

            foreach (var g in BuildGroups())
            {
                // 1) 메뉴 열기 (건축 버튼 클릭)
                if (!menu.gameObject.activeSelf)
                {
                    var ab = FindButtonByName("Btn_건축");
                    if (ab != null) yield return ClickButton(ab);
                    else menu.Toggle();
                    yield return Wait(0.35f);
                }
                // 2) 카테고리 펼치기 (닫혀 있으면 클릭)
                if (!IsCategoryOpen(g.cat, menu.transform))
                {
                    var cb = FindCategoryButton(g.cat, menu.transform);
                    if (cb != null) yield return ClickButton(cb);
                    yield return Wait(0.3f);
                }
                // 3) 건축물 버튼 클릭 → SetMode (메뉴는 닫힌다)
                var bb = FindBuildableButton(g.cat, g.idx, menu.transform);
                if (bb != null) yield return ClickButton(bb);
                else bm.SetMode(g.mode);     // fallback
                yield return Wait(0.3f);
                if (bm.CurrentMode != g.mode) bm.SetMode(g.mode);  // 보정

                // 4) 맵 셀들을 커서로 클릭해 청사진 배치 (림이 나중에 건축)
                foreach (var cell in g.cells)
                {
                    Vector3 world = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
                    Vector3 screen = cam.WorldToScreenPoint(world);
                    yield return MoveCursorTo(screen, 0.34f);   // #268 살짝 느리게
                    PlaceFundedAtScreen(bm, screen, cell);
                    yield return Wait(0.18f);
                }
                bm.SetMode(BuildManager.Mode.Off);
                yield return Wait(0.15f);
            }
        }

        // 화면 좌표로 맵 클릭(SimulateMapClick) → 청사진 + 자재 충당(완성은 림이).
        private void PlaceFundedAtScreen(BuildManager bm, Vector3 screen, Vector2Int cell)
        {
            bm.SimulateMapClick(new Vector2(screen.x, screen.y));
            var bp = FindBlueprintNear(new Vector2(cell.x + 0.5f, cell.y + 0.5f));
            if (bp == null) return;
            if (bp.needWood > 0) bp.DepositWood(bp.needWood);
            if (bp.needStone > 0) bp.DepositStone(bp.needStone);
            if (!_placed.Contains(bp)) _placed.Add(bp);
        }

        private static BlueprintEntity FindBlueprintNear(Vector2 p)
        {
            BlueprintEntity best = null; float bestSq = 1.2f * 1.2f;
            foreach (var b in Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None))
            {
                if (b == null) continue;
                float sq = ((Vector2)b.transform.position - p).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = b; }
            }
            return best;
        }

        // ---- UI 버튼 찾기 (BuildClickAutoQA 검증 패턴) ----------------------
        private static string KoreanOf(string s)
        {
            int a = s.IndexOf('('), b = s.IndexOf(')');
            return (a >= 0 && b > a) ? s.Substring(a + 1, b - a - 1).Trim() : s;
        }

        private Button FindButtonByName(string goName)
        {
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b != null && b.gameObject.name == goName) return b;
            return null;
        }

        private bool IsCategoryOpen(string cat, Transform parent)
        {
            string kr = KoreanOf(cat);
            foreach (var t in parent.GetComponentsInChildren<Text>(true))
            {
                if (t == null) continue;
                if (t.text.StartsWith("▼ ") && t.text.Contains(kr)) return true;
            }
            return false;
        }

        private Button FindCategoryButton(string cat, Transform parent)
        {
            string kr = KoreanOf(cat);
            foreach (var b in parent.GetComponentsInChildren<Button>(true))
            {
                if (b == null) continue;
                var t = b.GetComponentInChildren<Text>(true);
                if (t == null) continue;
                bool header = t.text.StartsWith("▼ ") || t.text.StartsWith("▶ ");
                if (header && t.text.Contains(kr)) return b;
            }
            return null;
        }

        private Button FindBuildableButton(string cat, int idx, Transform parent)
        {
            string kr = KoreanOf(cat);
            var buttons = parent.GetComponentsInChildren<Button>(true);
            int found = 0; bool hit = false;
            foreach (var b in buttons)
            {
                if (b == null) continue;
                var t = b.GetComponentInChildren<Text>(true);
                if (t == null) continue;
                bool header = t.text.StartsWith("▼ ") || t.text.StartsWith("▶ ");
                if (header && t.text.Contains(kr)) { hit = true; continue; }
                if (!hit) continue;
                if (header) break;             // 다음 카테고리 헤더
                if (found == idx) return b;
                found++;
            }
            return null;
        }

        // ====================================================================
        //  가짜 커서
        // ====================================================================
        private void CreateCursor()
        {
            var canGO = new GameObject("ShowcaseCursorCanvas");
            var canvas = canGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            canGO.AddComponent<UnityEngine.UI.CanvasScaler>();

            var curGO = new GameObject("ShowcaseCursor");
            curGO.transform.SetParent(canGO.transform, false);
            var img = curGO.AddComponent<Image>();
            img.sprite = BuildCursorSprite();
            img.raycastTarget = false;
            _cursor = curGO.GetComponent<RectTransform>();
            _cursor.sizeDelta = new Vector2(34, 34);
            _cursor.pivot = new Vector2(0.08f, 0.92f);   // 화살촉(좌상단)
            _cursor.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        }

        private static Sprite BuildCursorSprite()
        {
            const int S = 32;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[S * S];
            // 좌상단 꼭짓점에서 내려오는 화살표(흰색+검은 외곽).  tip=(2, S-2).
            Vector2 tip = new Vector2(2, S - 2);
            Vector2 a = new Vector2(2, S - 22);
            Vector2 b = new Vector2(15, S - 16);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    bool inside = InTri(p, tip, a, b);
                    // 외곽선: 삼각형을 살짝 키운 영역
                    bool border = InTri(p, tip + new Vector2(-1.4f, 1.4f), a + new Vector2(-1.6f, -1.6f), b + new Vector2(1.8f, 0.4f));
                    Color32 c = inside ? new Color32(255, 255, 255, 255)
                              : border ? new Color32(20, 20, 20, 255)
                                       : new Color32(0, 0, 0, 0);
                    px[y * S + x] = c;
                }
            tex.SetPixels32(px); tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.08f, 0.92f), S);
        }

        private static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
            bool neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool pos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(neg && pos);
        }
        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
            => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        private IEnumerator MoveCursorTo(Vector3 screenPos, float dur)
        {
            if (_cursor == null) yield break;
            Vector3 from = _cursor.position;
            Vector3 to = new Vector3(screenPos.x, screenPos.y, 0f);
            float t = 0f;
            if (dur <= 0f) { _cursor.position = to; yield break; }
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                _cursor.position = Vector3.Lerp(from, to, u);
                yield return null;
            }
            _cursor.position = to;
        }

        // 버튼의 실제 화면 중앙 (pivot 이 가장자리여도 정확히 가운데를 누르도록).
        private Vector3 ButtonCenterScreen(Button b)
        {
            var rt = b.transform as RectTransform;
            if (rt == null) return b.transform.position;
            Vector3[] c = new Vector3[4];
            rt.GetWorldCorners(c);                 // overlay canvas → 화면 px
            return (c[0] + c[2]) * 0.5f;           // bottom-left + top-right 중점
        }

        private IEnumerator ClickButton(Button b)
        {
            if (b == null) yield break;
            yield return MoveCursorTo(ButtonCenterScreen(b), 0.35f);
            yield return Wait(0.12f);
            // 클릭 펄스(살짝 줄었다 복귀) — 누르는 느낌
            if (_cursor != null) _cursor.localScale = Vector3.one * 0.8f;
            b.onClick.Invoke();
            yield return Wait(0.08f);
            if (_cursor != null) _cursor.localScale = Vector3.one;
        }

        // ====================================================================
        //  림이 건축하는 모습 추적
        // ====================================================================
        private IEnumerator WatchPawnsBuild(float dur)
        {
            float elapsed = 0f;
            while (elapsed < dur)
            {
                PawnEntity p = PickBuildingPawn();
                float seg = Mathf.Min(7f, dur - elapsed);
                if (p == null) yield return MoveCam(HouseCenter, 6.5f, seg);
                else yield return FollowPawn(p, 4.4f, seg);
                elapsed += seg;
            }
        }

        private PawnEntity PickBuildingPawn()
        {
            BlueprintEntity bp = null;
            foreach (var b in _placed) { if (b != null) { bp = b; break; } }
            if (bp == null)
                foreach (var b in Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None))
                    if (b != null) { bp = b; break; }

            PawnEntity best = null; float bestSq = float.MaxValue;
            Vector2 focus = bp != null ? (Vector2)bp.transform.position : HouseCenter;
            foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
            {
                if (p == null || p.IsDead) continue;
                float sq = ((Vector2)p.transform.position - focus).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = p; }
            }
            return best;
        }

        private IEnumerator FollowPawn(PawnEntity p, float ortho, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                float udt = Time.unscaledDeltaTime;   // 배속과 무관하게 영상 페이스 유지
                t += udt;
                if (p == null || p.IsDead) yield break;
                Vector3 want = new Vector3(p.transform.position.x, p.transform.position.y, camZ);
                cam.transform.position = Vector3.Lerp(cam.transform.position, want, udt * 2.5f);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, ortho, udt * 2.0f);
                yield return null;
            }
        }

        // 피날레: 림을 침대에 확실히 재운다.  AI 를 끄고(침대에서 안 끌려나가게) 빈 침대로
        //  안착시킨 뒤 SetRestTarget → forcedResting.  regen 을 낮춰(reflection) 피날레 동안
        //  풀충전돼 깨는 일이 없게 한다.
        private static FieldInfo RegenField =>
            typeof(PawnNeeds).GetField("sleepRegenAtNight", BindingFlags.Instance | BindingFlags.NonPublic);

        private BedEntity FindBedNear(Vector2 p)
        {
            BedEntity best = null; float bestSq = 2.5f * 2.5f;
            foreach (var b in Object.FindObjectsByType<BedEntity>(FindObjectsSortMode.None))
            {
                if (b == null) continue;
                float sq = ((Vector2)b.transform.position - p).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = b; }
            }
            return best;
        }

        // 밤: 림을 자연스럽게(텔레포트 X) 침대 "아래칸"(anchor)으로 걸어가게 해 재운다.
        //  AI 를 꺼 이동이 방해받지 않게 하고, regen 을 낮춰 자는 동안 안 깨게.
        private void StartNaturalSleep()
        {
            var regenField = RegenField;
            int i = 0;
            foreach (var pawn in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
            {
                if (pawn == null || pawn.IsDead || i >= Beds.Length) continue;
                Vector2Int anchor = Beds[i]; i++;                 // 아래칸 = anchor(작은 y)
                Vector2 bottomCell = new Vector2(anchor.x + 0.5f, anchor.y + 0.5f);
                var bed = FindBedNear(bottomCell);

                var ai = pawn.GetComponent<PawnUtilityAI>();
                if (ai != null) ai.enabled = false;              // 이동 방해 방지
                var needs = pawn.GetComponent<PawnNeeds>();
                if (needs != null)
                {
                    if (regenField != null) regenField.SetValue(needs, 0.04f);  // 안 깨게
                    needs.sleep = 16f;
                    if (bed != null) needs.SetRestTarget(bed);    // 아래칸 도착 시 forcedResting
                }
                var mv = pawn.GetComponent<PawnMovement>();
                if (mv != null) mv.SetTarget(bottomCell);         // 걸어서 침대 아래칸으로
            }
            Debug.Log("[Showcase] 자연 취침 시작 (침대 아래칸으로 도보).");
        }

        private PawnEntity _huntPawn;
        private PawnEntity _chopPawn;

        private TreeEntity FindTreeNear(Vector2 p, float maxDist)
        {
            TreeEntity best = null; float bestSq = maxDist * maxDist;
            foreach (var t in Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None))
            {
                if (t == null) continue;
                float sq = ((Vector2)t.transform.position - p).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = t; }
            }
            return best;
        }

        // 구역의 돌(StoneVein) 제거 + 그리드 unblock.
        private void ClearArea(float minX, float maxX, float minY, float maxY)
        {
            foreach (var v in Object.FindObjectsByType<StoneVeinEntity>(FindObjectsSortMode.None))
            {
                if (v == null) continue;
                Vector2 p = v.transform.position;
                if (p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY)
                {
                    var c = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
                    if (PawnMovement.Grid != null) PawnMovement.Grid.SetWalkable(c, true);
                    Destroy(v.gameObject);
                }
            }
        }

        private AnimalEntity FindHuntTarget()
        {
            AnimalEntity best = null; float bestSq = float.MaxValue; bool bestDeer = false;
            foreach (var a in Object.FindObjectsByType<AnimalEntity>(FindObjectsSortMode.None))
            {
                if (a == null || a.IsDead) continue;
                bool deer = a.Species == AnimalSpecies.Deer;
                float sq = ((Vector2)a.transform.position - HouseCenter).sqrMagnitude;
                // 사슴 우선, 그 안에서 가까운 것.
                if ((deer && !bestDeer) || (deer == bestDeer && sq < bestSq))
                { best = a; bestSq = sq; bestDeer = deer; }
            }
            return best;
        }

        // 아침: 림을 깨워 일상 재개 — 큰 농사 + 벌목 + 사슴 사냥.
        private void WakePawnsAndStartMorning()
        {
            var regenField = RegenField;
            var pawns = new List<PawnEntity>();
            foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
            {
                if (p == null || p.IsDead) continue;
                pawns.Add(p);
                var needs = p.GetComponent<PawnNeeds>();
                if (needs != null)
                {
                    needs.ClearRestTarget();
                    needs.sleep = 92f;
                    needs.food = 35f;                              // 아침 → 배고픔 → 밥 먹음
                    if (regenField != null) regenField.SetValue(needs, 8f);
                }
            }

            // 큰 농사 zone — 집 남쪽 grass (돌 정리 후 8x6 마킹).  운영자 "농사 더 크게".
            ClearArea(0.5f, 8.5f, -6.5f, -0.5f);
            var gz = GrowZoneDesignation.Instance;
            if (gz != null) gz.SimulateDragRect(new Vector2(1f, -6f), new Vector2(8f, -1f));

            // 벌목 지정 — 집 근처 나무 여러 그루 (림이 벌목).
            var chop = TreeChopDesignation.Instance;
            if (chop != null)
            {
                int marked = 0;
                foreach (var tree in Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None))
                {
                    if (tree == null || marked >= 6) continue;
                    Vector2 tp = tree.transform.position;
                    if ((tp - HouseCenter).sqrMagnitude < 20f * 20f) { chop.MarkWorld(tp); marked++; }
                }
            }

            // 림 명시 분담 — 뭉치지 않고 각자 다른 일을 하도록 직접 지정.
            //  [0] 사냥(사슴 추격), [1] 벌목(가까운 나무), [2+] 농사(grow zone, AI on).
            _huntPawn = null; _chopPawn = null;
            var deer = FindHuntTarget();
            for (int i = 0; i < pawns.Count; i++)
            {
                var pe = pawns[i];
                var ai = pe.GetComponent<PawnUtilityAI>();
                if (i == 0 && deer != null)
                {
                    if (ai != null) ai.enabled = false;       // AI off → PawnHunter 가 추격
                    var hunter = pe.GetComponent<PawnHunter>();
                    if (hunter != null) { hunter.SetAnimalTarget(deer); _huntPawn = pe; }
                }
                else if (i == 1)
                {
                    var tree = FindTreeNear(HouseCenter, 22f);
                    var chopper = pe.GetComponent<PawnChopper>();
                    if (tree != null && chopper != null)
                    {
                        if (ai != null) ai.enabled = false;   // AI off → PawnChopper 가 벌목
                        chopper.SetTreeTarget(tree); _chopPawn = pe;
                    }
                    else if (ai != null) ai.enabled = true;
                }
                else
                {
                    if (ai != null) ai.enabled = true;        // 농사(grow zone 가 배정)
                }
            }
            Debug.Log($"[Showcase] 아침 분담: 사냥={(_huntPawn != null)}, 벌목={(_chopPawn != null)}, 농사=나머지.");
        }

        private PawnEntity PickActivePawn()
        {
            // 움직이거나 일하는 림 우선 (집 근처).
            PawnEntity best = null; float bestSq = float.MaxValue;
            foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
            {
                if (p == null || p.IsDead) continue;
                float sq = ((Vector2)p.transform.position - HouseCenter).sqrMagnitude;
                var mv = p.GetComponent<PawnMovement>();
                if (mv != null && mv.IsMoving) sq -= 100f;        // 이동 중인 림 가산점
                if (sq < bestSq) { bestSq = sq; best = p; }
            }
            return best;
        }

        private void FinishRemaining()
        {
            int left = 0;
            foreach (var bp in _placed)
            {
                if (bp == null || bp.gameObject == null) continue;
                if (bp.needWood > 0) bp.DepositWood(bp.needWood);
                if (bp.needStone > 0) bp.DepositStone(bp.needStone);
                bp.AddWork(bp.BuildSeconds + 1f);
                left++;
            }
            Debug.Log($"[Showcase] 피날레 잔여 청사진 {left}개 마감.");
        }

        private IEnumerator NightSleepThenMorning()
        {
            if (_cursor != null) _cursor.gameObject.SetActive(false);
            var tc = TimeController.Instance;

            // ── 저녁 전경 (1배속) ──
            if (tc != null) tc.SetScale(1f);
            TrySetClock(20f);
            yield return MoveCam(HouseCenter, 10.5f, 3.5f);

            // ── 밤: 림이 침대로 걸어가 잔다 + 3배속 타임랩스 ──
            TrySetClock(22.5f);                       // 밤(조명 on)
            StartNaturalSleep();                       // 자연스럽게 도보 취침
            if (tc != null) tc.SetScale(3f);           // 3배속 — 자는 동안 빠르게
            Vector2 bedView = HouseCenter + new Vector2(0f, 0.6f);
            yield return MoveCam(bedView, 5.6f, 4f);   // 집 줌인(자는 모습 + 촛불)
            yield return Wait(18f);                    // 밤 경과 (3배속 타임랩스, unscaled 18s)

            // ── 아침: 기상 → 밥/농사 (2배속) ──
            if (tc != null) tc.SetScale(2f);
            TrySetClock(7.5f);                          // 아침(Work 슬롯, 조명 off)
            WakePawnsAndStartMorning();

            // ① 농사: 남쪽 밭 전경 → 파종하는 림 클로즈업
            yield return MoveCam(new Vector2((1f + 8f) / 2f, -3.5f), 8.0f, 4f);
            yield return Wait(6f);
            var planter = PickPawnExcept(_huntPawn, _chopPawn);
            if (planter != null) yield return FollowPawn(planter, 5.0f, 8f);
            else yield return Wait(8f);

            // ② 벌목: 나무 베는 림
            if (_chopPawn != null && !_chopPawn.IsDead)
                yield return FollowPawn(_chopPawn, 5.0f, 8f);

            // ③ 피날레: 사슴 사냥 추격을 따라가며 끝 (운영자: 사슴 잡다가 끝).
            if (_huntPawn != null && !_huntPawn.IsDead)
                yield return FollowPawn(_huntPawn, 5.2f, 13f);
            else
            {
                var p = PickActivePawn();
                if (p != null) yield return FollowPawn(p, 5.2f, 13f);
                else yield return Wait(13f);
            }
        }

        private PawnEntity PickPawnExcept(PawnEntity a, PawnEntity b)
        {
            PawnEntity best = null; float bestSq = float.MaxValue;
            foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
            {
                if (p == null || p.IsDead || p == a || p == b) continue;
                float sq = ((Vector2)p.transform.position - new Vector2(4.5f, -3.5f)).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = p; }
            }
            return best;
        }

        // ====================================================================
        //  카메라 헬퍼
        // ====================================================================
        private void SnapCam(Vector2 pos, float ortho)
        {
            cam.transform.position = new Vector3(pos.x, pos.y, camZ);
            cam.orthographicSize = ortho;
        }

        private IEnumerator MoveCam(Vector2 pos, float ortho, float dur)
        {
            Vector3 from = cam.transform.position;
            float fo = cam.orthographicSize;
            float t = 0f;
            if (dur <= 0f) { SnapCam(pos, ortho); yield break; }
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                cam.transform.position = new Vector3(Mathf.Lerp(from.x, pos.x, u),
                                                     Mathf.Lerp(from.y, pos.y, u), camZ);
                cam.orthographicSize = Mathf.Lerp(fo, ortho, u);
                yield return null;
            }
        }

        private IEnumerator Wait(float s)
        {
            float t = 0f;
            while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private void TrySetClock(float hour)
        {
            try
            {
                var gc = GameClock.Instance;
                if (gc == null) return;
                var fi = typeof(GameClock).GetField("<GameSeconds>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (fi != null) fi.SetValue(gc, hour * 3600f);
            }
            catch { }
        }
    }
}
