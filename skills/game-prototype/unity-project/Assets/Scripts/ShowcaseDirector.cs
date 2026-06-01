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
            float buildWatchDur = T * 0.30f;

            // 인트로: 집터 전경 줌인
            SnapCam(HouseCenter, 13f);
            yield return MoveCam(HouseCenter + new Vector2(0f, 0.5f), 9.5f, introDur);

            // 건축 UI 를 사람처럼 사용해 청사진 배치 (커서 클릭)
            yield return StartCoroutine(HumanBuildPlacement());

            // 림이 직접 건축하는 모습 — 건축 중인 림 따라가며 줌인
            yield return StartCoroutine(WatchPawnsBuild(buildWatchDur));

            FinishRemaining();

            // 밤 수면 피날레 (촛불 조명)
            yield return StartCoroutine(NightSleepFinale());
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
                t += Time.deltaTime;
                if (p == null || p.IsDead) yield break;
                Vector3 want = new Vector3(p.transform.position.x, p.transform.position.y, camZ);
                cam.transform.position = Vector3.Lerp(cam.transform.position, want, Time.deltaTime * 2.5f);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, ortho, Time.deltaTime * 2.0f);
                yield return null;
            }
        }

        // 피날레: 림을 침대에 확실히 재운다.  AI 를 끄고(침대에서 안 끌려나가게) 빈 침대로
        //  안착시킨 뒤 SetRestTarget → forcedResting.  regen 을 낮춰(reflection) 피날레 동안
        //  풀충전돼 깨는 일이 없게 한다.
        private void ForcePawnsAsleepInBeds()
        {
            var beds = new List<BedEntity>();
            foreach (var b in Object.FindObjectsByType<BedEntity>(FindObjectsSortMode.None))
                if (b != null) beds.Add(b);

            var regenField = typeof(PawnNeeds).GetField("sleepRegenAtNight",
                BindingFlags.Instance | BindingFlags.NonPublic);

            int bi = 0;
            foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
            {
                if (p == null || p.IsDead || bi >= beds.Count) continue;
                var bed = beds[bi]; bi++;

                var ai = p.GetComponent<PawnUtilityAI>();
                if (ai != null) ai.enabled = false;          // 자율행동 정지 → 침대 고정
                var mv = p.GetComponent<PawnMovement>();
                if (mv != null) mv.SetTarget(bed.transform.position);  // 잔여 이동 정리

                // 침대 셀로 안착 (경로 실패와 무관하게 확실히 침대 위에).
                p.transform.position = new Vector3(
                    bed.transform.position.x, bed.transform.position.y, p.transform.position.z);

                var needs = p.GetComponent<PawnNeeds>();
                if (needs != null)
                {
                    if (regenField != null) regenField.SetValue(needs, 0.05f); // 거의 안 차게 → 안 깸
                    needs.sleep = 18f;                       // 졸린 상태로
                    needs.SetRestTarget(bed);                // 침대 위 → forcedResting=true
                }
            }
            Debug.Log($"[Showcase] 피날레 {bi}명 침대 수면 고정.");
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

        private IEnumerator NightSleepFinale()
        {
            // 커서 숨김 (피날레엔 UI 안 씀)
            if (_cursor != null) _cursor.gameObject.SetActive(false);

            // #269 게임 로직상 림은 Sleep 슬롯에 스스로 침대로 가지만, 공개 영상의 피날레는
            //  반드시 "침대에서 자는" 그림이 나와야 하므로 확실히 재운다(AI off + 침대 안착).
            ForcePawnsAsleepInBeds();

            Vector2 bedView = HouseCenter + new Vector2(0f, 1.0f);
            yield return MoveCam(bedView, 6.0f, 6f);
            float t = 0f;
            while (t < 120f)
            {
                t += Time.deltaTime;
                cam.orthographicSize = Mathf.Lerp(6.0f, 5.2f, Mathf.Clamp01(t / 45f));
                cam.transform.position = new Vector3(
                    bedView.x + Mathf.Sin(t * 0.13f) * 0.25f, bedView.y, camZ);
                yield return null;
            }
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
                t += Time.deltaTime;
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
            while (t < s) { t += Time.deltaTime; yield return null; }
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
