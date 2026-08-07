using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>할 일이 떨어지면 마을이 **스스로 다음 일감을 정한다.**
    ///
    /// 계기 (2026-08-02, 손대지 않은 10분 실측):
    /// ```
    /// 시각   식량  식사  목재   가치   목표
    /// 0분     0     0   300    435   0/4
    /// 1분    21    26   285    770   0/4
    /// 3분    43    30   285    784   0/4
    /// 10분   39    30   285    784   1/4     ← 9분간 목재·가치 완전 고정
    /// ```
    /// 첫 1분에 초기 운반이 끝나면 그 뒤로는 아무 일도 일어나지 않았다.  원인은
    /// 명확하다 — 벌목·채광이 **플레이어 지정을 전제**로 하므로, 지정하지 않으면
    /// 영원히 할 일이 없다.  콜로니 심에서 사람이 서 있는 화면은 "AI가 죽었다"로
    /// 읽히고, 심사자가 보는 창(5~15분)이 통째로 그 화면이 된다.
    ///
    /// ── 왜 새 '자동 벌목 액션' 이 아니라 '자동 지정' 인가 ──────────────────
    /// 자율 벌목 액션을 따로 만들면 `ChopTreeAction` 의 IsMarked 게이트를 우회하는
    /// 두 번째 경로가 생긴다.  그 경로는 이 레포에서 이미 한 번 사고를 냈다 —
    /// 지정과 무관하게 맵의 아무 나무나 고르던 시절의 "다른 림이 벌목한다",
    /// "다같이 몰려간다"(#38).  그래서 **경로를 늘리지 않고 입력을 준다**:
    /// 플레이어가 드래그로 지정하는 것과 **같은 함수**(`SimulateDragRect`)를 호출해
    /// 나무를 마킹하고, 그 뒤는 기존 파이프라인이 그대로 처리한다.
    ///
    /// 부수 효과가 오히려 이득이다 — 자동으로 찍힌 나무에도 지정 표시가 뜨므로
    /// 화면에서 **"마을이 저 나무를 베기로 정했다"** 가 읽힌다.  플레이어는 그
    /// 지정을 지울 수도 있다(의도가 항상 먼저다).
    ///
    /// ── 언제 개입하는가 ────────────────────────────────────────────────
    ///  · 플레이어 지정이 **하나라도 남아 있으면 개입하지 않는다.**
    ///  · 자원이 정착 목표선 아래일 때만.  목표를 채우면 스스로 멈춘다
    ///    (무한 벌목으로 맵을 밀어버리지 않는다).
    ///  · 한 번에 소수만 찍는다 — 플레이어가 끼어들 여지를 남긴다.
    /// </summary>
    public class ColonyAutoWork : MonoBehaviour
    {
        private const float Interval = 2.0f;     // 실시간 (일시정지 중엔 의미 없음)
        private const int TreeBatch = 3;         // 한 번에 찍는 나무 수
        private const int VeinBatch = 2;
        private const float SearchRadius = 26f;  // 마을 중심 기준 (너무 멀면 왕복만 한다)

        /// <summary>시작 후 이 시간(실초)까지는 개입하지 않는다.
        ///
        /// 두 가지 이유가 겹친다.
        ///  · **게임적으로**: 시작 직후엔 바닥에 깔린 자원을 나르는 일이 이미 있다.
        ///    실측에서 첫 1분은 가치 435→770 으로 활발했다 — 그 위에 자동 벌목까지
        ///    얹으면 오히려 정신없고, "할 일이 없을 때만 스스로 정한다"는 규칙과도 어긋난다.
        ///  · **검증상**: 재현 시나리오 넷(`p1-chop-selected-only*`, `p0-remote-chop`)이
        ///    "지정한 나무만 베는가"를 본다.  시작하자마자 자동 지정이 끼면 그 전제가
        ///    깨진다.  시나리오는 앞 몇 초 안에 지정을 마치므로 유예가 이를 보호한다.
        ///    (하네스에서만 끄는 방식은 쓰지 않는다 — 게이트가 출시본과 다른 것을
        ///     검증하게 되고, 그게 이 레포에서 7주간 안전장치가 꺼져 있던 방식이다.)</summary>
        private const float StartGraceSeconds = 45f;

        private float next;
        private float firstSeen = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindFirstObjectByType<ColonyAutoWork>() != null) return;
                var go = new GameObject("~ColonyAutoWork");
                go.AddComponent<ColonyAutoWork>();
                Debug.Log("[Boot] ColonyAutoWork 부착 — 할 일이 없으면 스스로 정한다");
            });
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;               // 일시정지 중엔 결정하지 않는다
            // 쇼케이스(제출 영상 녹화) 중에는 개입하지 않는다.  영상은 55초인데 이
            //  유예가 45초라, 하필 마지막 10초에 나무 지정 표식이 우수수 뜬다 —
            //  연출이 짜 놓은 화면에 설명 없는 마커가 끼어드는 셈이다.
            //  쇼케이스는 스스로 지정·건축을 연출하므로 자동 일감이 할 일도 없다.
            if (ShowcaseDirector.Enabled) return;
            // 게임이 실제로 도는 시간만 센다 — 일시정지로 부팅하므로 실시간으로 세면
            //  플레이어가 둘러보는 동안 유예가 소진된다(튜토리얼 게이트에서 같은 실수를 했다).
            if (firstSeen < 0f) firstSeen = Time.unscaledTime;
            if (Time.unscaledTime - firstSeen < StartGraceSeconds) return;
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + Interval;

            Vector2 center = ColonyCenter();
            TryAutoChop(center);
            TryAutoMine(center);
        }

        // ── 마을 중심 = 주민들의 평균 위치.  건물 좌표를 박지 않는 이유는
        //    시작 정착지가 옮겨지거나 세이브에서 복원돼도 따라오게 하기 위해서다.
        private static Vector2 ColonyCenter()
        {
            var pawns = FindObjectsByType<PawnNeeds>(FindObjectsSortMode.None);
            if (pawns == null || pawns.Length == 0) return Vector2.zero;
            Vector2 sum = Vector2.zero;
            int n = 0;
            for (int i = 0; i < pawns.Length; i++)
            {
                if (pawns[i] == null) continue;
                sum += (Vector2)pawns[i].transform.position;
                n++;
            }
            return n > 0 ? sum / n : Vector2.zero;
        }

        private void TryAutoChop(Vector2 center)
        {
            var design = TreeChopDesignation.Instance;
            var res = ResourceManager.Instance;
            if (design == null || res == null) return;
            if (design.MarkedCount > 0) return;                 // 플레이어 지시가 남아 있다
            if (res.wood >= ColonyObjectives.WoodTarget_Public) return; // 목표를 채웠으면 멈춘다

            int marked = 0;
            var trees = FindObjectsByType<TreeEntity>(FindObjectsSortMode.None);
            // 가까운 것부터 — 왕복 시간이 짧아야 화면에서 진행이 보인다.
            System.Array.Sort(trees, (a, b) =>
            {
                if (a == null) return 1;
                if (b == null) return -1;
                float da = ((Vector2)a.transform.position - center).sqrMagnitude;
                float db = ((Vector2)b.transform.position - center).sqrMagnitude;
                return da.CompareTo(db);
            });
            foreach (var t in trees)
            {
                if (marked >= TreeBatch) break;
                if (t == null || t.IsDestroyed) continue;
                Vector2 p = t.transform.position;
                if ((p - center).sqrMagnitude > SearchRadius * SearchRadius) break;  // 정렬돼 있으므로 종료
                // 플레이어 드래그와 **같은 경로**로 찍는다 (한 칸짜리 사각형).
                if (design.SimulateDragRect(p - Vector2.one * 0.35f, p + Vector2.one * 0.35f) > 0)
                    marked++;
            }
            if (marked > 0)
                Debug.Log($"[AutoWork] 목재 {res.wood}/{ColonyObjectives.WoodTarget_Public} — 나무 {marked}그루 자동 지정");
        }

        private void TryAutoMine(Vector2 center)
        {
            var design = MineDesignation.Instance;
            var res = ResourceManager.Instance;
            if (design == null || res == null) return;
            if (design.MarkedCount > 0) return;
            if (res.stone >= ColonyObjectives.StoneSoftTarget) return;

            int marked = 0;
            var veins = FindObjectsByType<StoneVeinEntity>(FindObjectsSortMode.None);
            System.Array.Sort(veins, (a, b) =>
            {
                if (a == null) return 1;
                if (b == null) return -1;
                float da = ((Vector2)a.transform.position - center).sqrMagnitude;
                float db = ((Vector2)b.transform.position - center).sqrMagnitude;
                return da.CompareTo(db);
            });
            foreach (var v in veins)
            {
                if (marked >= VeinBatch) break;
                if (v == null) continue;
                Vector2 p = v.transform.position;
                if ((p - center).sqrMagnitude > SearchRadius * SearchRadius) break;
                if (design.SimulateDragRect(p - Vector2.one * 0.35f, p + Vector2.one * 0.35f) > 0)
                    marked++;
            }
            if (marked > 0)
                Debug.Log($"[AutoWork] 석재 {res.stone}/{ColonyObjectives.StoneSoftTarget} — 광맥 {marked}개 자동 지정");
        }
    }
}
