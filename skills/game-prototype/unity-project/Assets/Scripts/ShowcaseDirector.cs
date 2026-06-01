using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #265/#266 ShowcaseDirector — 녹화용 "사람이 직접 플레이하는 듯한" 연출 디렉터.
    ///
    /// 운영자 요구: (1)사람이 플레이하는 느낌, (2)예쁜 집, (3)줌인·줌아웃 적절히,
    /// (4)림 행동이 잘 보이게, (5)#266 건물은 "청사진만 올리고 건축은 림이" 직접 해야 함
    /// (이전엔 청사진을 즉시 강제완성 → 림 노동이 안 보였음).
    ///
    /// 따라서: 청사진을 자재 충당된 상태로 배치만 하고, 림(PawnBuilder)이 직접 걸어가
    /// 건축한다.  카메라는 건축 중인 림을 따라가 클로즈업(림 행동·인접 노동이 잘 보이게).
    /// 시간 내 못 끝낸 잔여 청사진만 피날레 직전에 마무리(자연스러운 마감).
    /// </summary>
    public class ShowcaseDirector : MonoBehaviour
    {
        public static bool Enabled = false;
        public float totalSeconds = 150f;

        private Camera cam;
        private float camZ = -10f;
        private readonly List<BlueprintEntity> _placed = new List<BlueprintEntity>();

        public static void Spawn(float seconds)
        {
            Enabled = true;
            var go = new GameObject("ShowcaseDirector");
            go.AddComponent<ShowcaseDirector>().totalSeconds = seconds;
        }

        // ---- 예쁜 집 설계 (7x6 외벽 + 바닥재 + 침대3 + 화덕 + 테이블 + 조명2) ------
        const int X0 = 2, X1 = 8, Y0 = 2, Y1 = 7;                 // 외벽 링
        static readonly Vector2Int Door  = new Vector2Int(5, 2);  // 하단 중앙 문
        static readonly Vector2Int[] Beds = {                      // 1x2, anchor+위칸 점유
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
            TrySetClock(8f);   // 08:00 시작 → 건축=낮, 피날레=황혼/밤(조명 발광)

            float T = totalSeconds;
            float introDur = Mathf.Max(6f, T * 0.06f);
            float placeDur = T * 0.16f;             // 청사진 배치(고스트가 차례로 뜸)
            float buildWatchDur = T * 0.58f;        // 림이 건축하는 모습 추적
            // 피날레는 남은 시간.

            // ── 인트로: 집터 전경에서 천천히 줌인 ──
            SnapCam(HouseCenter, 13f);
            yield return MoveCam(HouseCenter + new Vector2(0, 0.5f), 9.5f, introDur);

            // ── 청사진 배치 (자재 충당, 강제완성 X) — 림이 지을 거리만 깔아줌 ──
            yield return StartCoroutine(PlaceBlueprints(placeDur));

            // ── 림이 직접 건축하는 모습 — 건축 중인 림을 따라가며 줌인 ──
            yield return StartCoroutine(WatchPawnsBuild(buildWatchDur));

            // ── 잔여 청사진 마감 (시간 내 못 끝낸 것만 자연 완성) ──
            FinishRemaining();

            // ── 피날레: 완성된 집 + 콜로니 전경 (황혼/밤 조명) ──
            yield return MoveCam(HouseCenter + new Vector2(0.5f, 0.5f), 12.5f, 6f);
            float t = 0f;
            while (t < 60f)
            {
                t += Time.deltaTime;
                cam.transform.position += new Vector3(Mathf.Sin(t * 0.2f) * 0.015f, 0.008f, 0f);
                yield return null;
            }
        }

        // ====================================================================
        //  청사진 배치 (자재 충당만, 완성은 림이)
        // ====================================================================
        private IEnumerator PlaceBlueprints(float dur)
        {
            StartCoroutine(MontageCamera(dur));   // 집터 위를 와이드 팬 + 살짝 줌인

            var pieces = BuildOrder();
            if (pieces.Count == 0) { yield return Wait(dur); yield break; }
            float interval = dur / pieces.Count;
            foreach (var p in pieces)
            {
                PlaceFunded(p.mode, p.x, p.y);
                yield return Wait(interval);
            }
        }

        private IEnumerator MontageCamera(float dur)
        {
            Vector2 c = HouseCenter;
            yield return MoveCam(c + new Vector2(-1.0f, -0.5f), 7.5f, dur * 0.5f);
            yield return MoveCam(c + new Vector2( 1.0f,  0.5f), 6.5f, dur * 0.5f);
        }

        // BuildManager 로 청사진 배치 + 자재 충당(DepositWood/Stone).  AddWork 하지 않음
        //  → 림(PawnBuilder)이 걸어가 직접 건축한다.
        private void PlaceFunded(BuildManager.Mode mode, int cx, int cy)
        {
            var bm = BuildManager.Instance;
            if (bm == null) return;
            bm.SetMode(mode);
            bool placed = bm.TryPlaceAt(cx, cy);
            bm.SetMode(BuildManager.Mode.Off);
            if (!placed) return;

            var bp = FindBlueprintNear(new Vector2(cx + 0.5f, cy + 0.5f));
            if (bp == null) return;
            if (bp.needWood > 0) bp.DepositWood(bp.needWood);     // 자재는 "현장 도착" 상태
            if (bp.needStone > 0) bp.DepositStone(bp.needStone);  // → 림은 운반 없이 건축만
            _placed.Add(bp);
        }

        private static BlueprintEntity FindBlueprintNear(Vector2 p)
        {
            BlueprintEntity best = null; float bestSq = 2.5f * 2.5f;
            foreach (var b in Object.FindObjectsByType<BlueprintEntity>(FindObjectsSortMode.None))
            {
                if (b == null) continue;
                float sq = ((Vector2)b.transform.position - p).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = b; }
            }
            return best;
        }

        // 건설 순서: 바닥(가구칸 제외) → 외벽 → 문 → 가구 → 조명.
        private struct Piece { public BuildManager.Mode mode; public int x, y; }
        private List<Piece> BuildOrder()
        {
            var list = new List<Piece>();
            var occupied = new HashSet<Vector2Int>();
            foreach (var b in Beds) { occupied.Add(b); occupied.Add(new Vector2Int(b.x, b.y + 1)); }
            occupied.Add(Stove); occupied.Add(Table);
            foreach (var l in Lamps) occupied.Add(l);

            for (int x = X0 + 1; x <= X1 - 1; x++)
                for (int y = Y0 + 1; y <= Y1 - 1; y++)
                    if (!occupied.Contains(new Vector2Int(x, y)))
                        list.Add(new Piece { mode = BuildManager.Mode.Floor, x = x, y = y });

            for (int x = X0; x <= X1; x++)
                for (int y = Y0; y <= Y1; y++)
                {
                    bool perim = (x == X0 || x == X1 || y == Y0 || y == Y1);
                    if (!perim) continue;
                    if (x == Door.x && y == Door.y) continue;
                    list.Add(new Piece { mode = BuildManager.Mode.Wall, x = x, y = y });
                }

            list.Add(new Piece { mode = BuildManager.Mode.Door, x = Door.x, y = Door.y });
            foreach (var b in Beds) list.Add(new Piece { mode = BuildManager.Mode.Bed, x = b.x, y = b.y });
            list.Add(new Piece { mode = BuildManager.Mode.Stove, x = Stove.x, y = Stove.y });
            list.Add(new Piece { mode = BuildManager.Mode.TableChair, x = Table.x, y = Table.y });
            foreach (var l in Lamps) list.Add(new Piece { mode = BuildManager.Mode.Lamp, x = l.x, y = l.y });
            return list;
        }

        // ====================================================================
        //  림이 건축하는 모습 추적 (림 행동·인접 노동이 잘 보이게)
        // ====================================================================
        private IEnumerator WatchPawnsBuild(float dur)
        {
            float elapsed = 0f;
            while (elapsed < dur)
            {
                // 지금 청사진 근처에서 건축 중인 림을 골라 ~7초 따라간다.
                PawnEntity p = PickBuildingPawn();
                float seg = Mathf.Min(7f, dur - elapsed);
                if (p == null)
                {
                    // 건축 중인 림이 없으면 집 전경 유지(줌 약간 아웃).
                    yield return MoveCam(HouseCenter, 6.5f, seg);
                }
                else
                {
                    yield return FollowPawn(p, 4.4f, seg);
                }
                elapsed += seg;
            }
        }

        // 살아있는 청사진에 가장 가까운(=건축 중인) 림을 고른다.
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

        // 시간 내 림이 못 끝낸 잔여 청사진만 자연 완성(피날레에 집이 온전하도록).
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
