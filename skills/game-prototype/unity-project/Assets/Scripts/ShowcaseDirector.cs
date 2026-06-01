using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using MelonS.GameProto.AI;

namespace MelonS.GameProto
{
    /// <summary>
    /// #265 ShowcaseDirector — 녹화용 "사람이 직접 플레이하는 듯한" 연출 디렉터.
    ///
    /// 기존 GameplayRecorderTool 은 카메라 고정 + 1프레임에 집을 통째로 완성 → AI 시뮬
    /// 처럼 보였다.  운영자 요구: (1)사람이 플레이하는 느낌, (2)예쁜 집을 점진적으로 건설,
    /// (3)줌인·줌아웃 적절히, (4)림 행동이 잘 보이게.
    ///
    /// 이 디렉터는 play 진입 후 GameplayRecorderTool 이 Spawn 하며, 코루틴으로:
    ///   인트로(전경) → 건설 몽타주(바닥→벽→문→가구→조명 점진 + 카메라 팬/줌) →
    ///   림 행동 클로즈업(작업/이동 중인 림을 부드럽게 추적, 줌인) → 피날레(완성된 집 전경,
    ///   황혼/밤 조명 발광) 순으로 totalSeconds 동안 연출한다.  녹화는 RecorderTool 이 끊는다.
    /// </summary>
    public class ShowcaseDirector : MonoBehaviour
    {
        public static bool Enabled = false;
        public float totalSeconds = 150f;

        private Camera cam;
        private float camZ = -10f;

        public static void Spawn(float seconds)
        {
            Enabled = true;
            var go = new GameObject("ShowcaseDirector");
            go.AddComponent<ShowcaseDirector>().totalSeconds = seconds;
        }

        // ---- 예쁜 집 설계 (7x6 외벽 + 바닥재 + 침대3 + 화덕 + 테이블 + 조명2) ------
        const int X0 = 2, X1 = 8, Y0 = 2, Y1 = 7;                 // 외벽 링 (perimeter)
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

            // 카메라를 우리가 직접 제어 — 입력 기반 CameraController 끔.
            var cc = cam.GetComponent<CameraController>();
            if (cc != null) cc.enabled = false;

            if (TimeController.Instance != null) TimeController.Instance.SetScale(1f);
            TrySetClock(8f);   // 08:00 시작 → 몽타주=낮, 피날레=황혼/밤(조명 발광)
            SetupColonyWork(); // 림이 벌목·운반하는 모습이 보이도록 일거리 부여

            float T = totalSeconds;
            float introDur  = Mathf.Max(6f,  T * 0.06f);
            float buildDur  = T * 0.50f;
            float pawnsDur  = T * 0.24f;
            // 피날레는 남은 시간 (RecorderTool 이 T 에서 컷).

            // ── 인트로: 집터 전경에서 천천히 줌인 ──
            SnapCam(HouseCenter, 13f);
            yield return MoveCam(HouseCenter + new Vector2(0, 0.5f), 10.5f, introDur);

            // ── 건설 몽타주: 점진 건설 + 카메라 팬/줌 (병렬) ──
            yield return StartCoroutine(BuildMontage(buildDur));

            // ── 림 행동 클로즈업: 작업/이동 중인 림 추적 ──
            yield return StartCoroutine(PawnShowcase(pawnsDur));

            // ── 피날레: 완성된 집 + 콜로니 전경 (황혼/밤 조명) ──
            yield return MoveCam(HouseCenter + new Vector2(0.5f, 0.5f), 12.5f, 6f);
            // 남은 시간 동안 아주 느린 드리프트로 "구경하는" 느낌
            float t = 0f;
            while (t < 60f)
            {
                t += Time.deltaTime;
                cam.transform.position += new Vector3(Mathf.Sin(t * 0.2f) * 0.015f, 0.008f, 0f);
                yield return null;
            }
        }

        // ====================================================================
        //  건설 몽타주
        // ====================================================================
        private IEnumerator BuildMontage(float dur)
        {
            // 카메라 연출을 병렬로 (집 위를 팬하며 중간에 줌인 후 다시 줌아웃).
            StartCoroutine(MontageCamera(dur));

            var pieces = BuildOrder();
            if (pieces.Count == 0) { yield return Wait(dur); yield break; }
            float interval = dur / pieces.Count;

            foreach (var p in pieces)
            {
                PlaceAndComplete(p.mode, p.x, p.y);
                yield return Wait(interval);
            }
        }

        private IEnumerator MontageCamera(float dur)
        {
            Vector2 c = HouseCenter;
            yield return MoveCam(c + new Vector2(-1.0f, -0.5f), 7.0f, dur * 0.28f);   // 와이드 팬
            yield return MoveCam(c + new Vector2( 1.5f,  0.5f), 4.8f, dur * 0.24f);   // 줌인 (벽 올라가는 모습)
            yield return MoveCam(c + new Vector2(-0.5f,  1.0f), 5.4f, dur * 0.24f);   // 줌인 유지 팬
            yield return MoveCam(c,                              7.5f, dur * 0.24f);   // 줌아웃 전경
        }

        // 건설 순서: 바닥(가구칸 제외) → 외벽 → 문 → 가구 → 조명.  진행이 눈에 보이게.
        private struct Piece { public BuildManager.Mode mode; public int x, y; }
        private List<Piece> BuildOrder()
        {
            var list = new List<Piece>();

            // 가구가 차지할 칸 — 바닥재를 깔지 않음(배치 충돌 회피).
            var occupied = new HashSet<Vector2Int>();
            foreach (var b in Beds) { occupied.Add(b); occupied.Add(new Vector2Int(b.x, b.y + 1)); } // 1x2
            occupied.Add(Stove); occupied.Add(Table);
            foreach (var l in Lamps) occupied.Add(l);

            // 1) 바닥재 (내부, 가구칸 제외)
            for (int x = X0 + 1; x <= X1 - 1; x++)
                for (int y = Y0 + 1; y <= Y1 - 1; y++)
                    if (!occupied.Contains(new Vector2Int(x, y)))
                        list.Add(new Piece { mode = BuildManager.Mode.Floor, x = x, y = y });

            // 2) 외벽 (문칸 제외)
            for (int x = X0; x <= X1; x++)
                for (int y = Y0; y <= Y1; y++)
                {
                    bool perim = (x == X0 || x == X1 || y == Y0 || y == Y1);
                    if (!perim) continue;
                    if (x == Door.x && y == Door.y) continue;
                    list.Add(new Piece { mode = BuildManager.Mode.Wall, x = x, y = y });
                }

            // 3) 문
            list.Add(new Piece { mode = BuildManager.Mode.Door, x = Door.x, y = Door.y });

            // 4) 가구: 침대 → 화덕 → 테이블
            foreach (var b in Beds) list.Add(new Piece { mode = BuildManager.Mode.Bed, x = b.x, y = b.y });
            list.Add(new Piece { mode = BuildManager.Mode.Stove, x = Stove.x, y = Stove.y });
            list.Add(new Piece { mode = BuildManager.Mode.TableChair, x = Table.x, y = Table.y });

            // 5) 조명 (밤에 발광)
            foreach (var l in Lamps) list.Add(new Piece { mode = BuildManager.Mode.Lamp, x = l.x, y = l.y });

            return list;
        }

        // BuildManager 로 청사진 배치 후 즉시 완성 (FeatureAuditQA 와 동일 경로).
        private void PlaceAndComplete(BuildManager.Mode mode, int cx, int cy)
        {
            var bm = BuildManager.Instance;
            if (bm == null) return;
            bm.SetMode(mode);
            bool placed = bm.TryPlaceAt(cx, cy);
            bm.SetMode(BuildManager.Mode.Off);
            if (!placed) { Debug.Log($"[Showcase] {mode} @({cx},{cy}) 배치 거부 — skip"); return; }

            BlueprintEntity bp = FindBlueprintNear(new Vector2(cx + 0.5f, cy + 0.5f));
            if (bp == null) return;
            if (bp.needWood > 0) bp.DepositWood(bp.needWood);
            if (bp.needStone > 0) bp.DepositStone(bp.needStone);
            bp.AddWork(bp.BuildSeconds + 1f);
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

        // ====================================================================
        //  림 행동 클로즈업
        // ====================================================================
        private IEnumerator PawnShowcase(float dur)
        {
            var pawns = new List<PawnEntity>();
            foreach (var p in Object.FindObjectsByType<PawnEntity>(FindObjectsSortMode.None))
                if (p != null && !p.IsDead) pawns.Add(p);

            if (pawns.Count == 0) { yield return Wait(dur); yield break; }

            // 2~3명을 번갈아 클로즈업 추적 (각자 작업/이동하는 모습).
            int n = Mathf.Min(3, pawns.Count);
            float each = dur / n;
            for (int i = 0; i < n; i++)
            {
                // 추적 대상으로 부드럽게 줌인 전환
                yield return FollowPawn(pawns[i], 4.6f, each);
            }
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

        // ====================================================================
        //  림 일거리 — 벌목 지정 + 창고 zone (림 행동이 잘 보이게)
        // ====================================================================
        private void SetupColonyWork()
        {
            // 창고 zone — 집 동쪽 3x2.  림이 흩어진 목재를 이리로 운반(haul)한다.
            for (int x = X1 + 2; x <= X1 + 4; x++)
                for (int y = Y0; y <= Y0 + 1; y++)
                    StockpileZoneEntity.Spawn(new Vector3(x + 0.5f, y + 0.5f, 0f), null);

            // 집 주변 나무 최대 5그루 벌목 지정 — 림이 벌목하러 가는 모습.
            var chop = TreeChopDesignation.Instance;
            if (chop == null) return;
            int marked = 0;
            foreach (var tree in Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None))
            {
                if (tree == null || marked >= 5) continue;
                Vector2 tp = tree.transform.position;
                if ((tp - HouseCenter).sqrMagnitude < 16f * 16f)
                {
                    chop.MarkWorld(tp);
                    marked++;
                }
            }
            Debug.Log($"[Showcase] 벌목 지정 {marked}그루 + 창고 zone 배치.");
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

        // GameClock.GameSeconds(auto-property) 를 reflection 으로 설정해 시작 시각 지정.
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
            catch { /* 시작 시각 설정 실패해도 영상은 정상 */ }
        }
    }
}
