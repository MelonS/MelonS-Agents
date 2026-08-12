using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto.Core
{
    /// <summary>톱다운 2D 깊이 정렬 — **화면에서 아래에 있는 것이 앞에 그려진다**.
    ///
    /// 계기 (2026-08-01 운영자): "침대가 벽보다 위로 올라오네 레이어가" /
    /// "오브젝트들 레이어 처리 제대로 되고 있나?"
    ///
    /// 진단: 이 프로젝트에는 **Y 기반 정렬이 아예 없었다.**  모든 스프라이트가
    ///  종류별 고정 sortingOrder 를 썼다 — 바닥 1 · 침대 4 · 화덕/나무 5 · 문 6 ·
    ///  벽 7 · 주민 10.  그래서 앞뒤 관계가 **위치와 무관하게 고정**된다:
    ///   · 2칸짜리 침대가 남쪽 벽을 덮는다 (침대 4 < 벽 7 인데도 침대 스프라이트가
    ///     아래 칸까지 뻗어 벽 타일 위로 올라온다 — 운영자가 본 그 증상).
    ///   · 주민(10)이 벽(7)보다 항상 위라, 벽 **뒤**에 서 있어도 벽 위에 그려진다.
    ///  종류별 고정값은 '무엇이 무엇보다 위냐'만 말할 수 있고 '지금 누가 앞에
    ///  있느냐'는 말하지 못한다.  톱다운 게임에서 그 둘은 다른 질문이다.
    ///
    /// 해법: 스프라이트의 **발밑 y**(월드 AABB 하단)로 순서를 매긴다.  아래에
    ///  있을수록 큰 값 = 앞.  타입별 오프셋을 손으로 관리하지 않아도 되고,
    ///  1×2 침대처럼 키가 다른 물건도 자동으로 맞는다.
    ///
    /// 기존 값은 버리지 않고 **동순위 보정치**로 재활용한다 — 발밑 y 가 같을 때
    ///  (예: 침대 위에 누운 주민) 손으로 맞춰 둔 상대 순서가 그대로 유지된다.
    /// </summary>
    public static class YSort
    {
        /// <summary>월드 밴드 중앙.  아래 Min/Max 안에 들어오도록 잡는다.</summary>
        private const int BaseOrder = 850;
        /// <summary>월드 1유닛당 정렬 단계.
        ///
        /// **한 칸(1유닛) 차이가 어떤 보정치 차이보다도 커야 한다.**  보정치는 기존
        /// 고정값에서 오므로 최대 11(=15-4)이다.  단계가 4 였을 때는 한 칸 = 4단계라
        /// 보정치가 위치를 이길 수 있었다 — 예컨대 이불(보정 7)이 한 칸 남쪽 벽(보정 3)을
        /// 덮는 식으로, 고치려던 증상이 다른 모습으로 되살아난다.
        /// 16 이면 한 칸 = 16단계 > 11 이라 **위치가 항상 우선**하고, 보정치는 발밑 y 가
        /// 같을 때(침대 위에 누운 주민 등)만 순서를 가른다 — 원래 의도한 역할.</summary>
        private const int StepsPerUnit = 16;
        /// <summary>맵은 ±45 → ±720 단계.  오버레이 밴드(2000+)와 겹치지 않게 잡는다.</summary>
        public const int BandMin = 110;
        public const int BandMax = 1600;

        /// <summary>발밑 월드 y → sortingOrder (입체 오브젝트 밴드).</summary>
        public static int OrderFor(float bottomY)
            => Mathf.Clamp(BaseOrder - Mathf.RoundToInt(bottomY * StepsPerUnit), BandMin, BandMax);

        /// <summary>평면 가구용 — 입체 밴드(110~1600) **아래**의 독립 밴드(4~99).
        ///
        /// 같은 규칙으로 자기들끼리는 Y 정렬되므로 침대끼리의 앞뒤는 유지되고,
        /// 사람·벽·나무보다는 무조건 아래에 그려진다.
        /// (지면 0~3 보다는 위 — 바닥·구역 위에 놓여야 한다.)</summary>
        public static int OrderForFlat(float bottomY)
            => Mathf.Clamp(50 - Mathf.RoundToInt(bottomY * 0.5f), 4, 99);

        // ── 기존 값의 해석 규약 ────────────────────────────────────────────
        //  0..3   지면 (바닥·구역·풀·꽃).  항상 맨 아래 — 건드리지 않는다.
        //  4      **평면 가구** (침대·잠자리).  Y 정렬하되 서 있는 것들보다 항상 아래.
        //  5..15  **입체 오브젝트** (화덕·나무·문·벽·주민…).  자기들끼리 Y 정렬.
        //
        // 왜 둘로 나누나 (2026-08-01 운영자 "사람이 걸어갈때 침대 밑으로 들어감"):
        //  침대는 1×2 라 발밑이 아래 칸 바닥이다.  주민이 **위쪽 칸(베개 자리)**을
        //  지나가면 주민 발밑이 침대 발밑보다 높아 침대가 앞에 그려졌다.
        //  Y 정렬 자체는 맞게 동작한 것이고, 전제가 틀렸다 — 바닥에 깔린 가구는
        //  '뒤에 설 수 있는 물건' 이 아니다.  벽·나무 뒤에는 설 수 있어도
        //  요·이불 뒤에는 못 선다.  그러니 평면 가구는 사람과 **경쟁시키지 않는다.**
        //  16..109 화면 오버레이 (선택 링 위 UI·화살·구름그림자·체력바·플로팅텍스트).
        //         월드 밴드(110~1600)보다 위로 올려야 하므로 +2000 한 번.
        //  110+   이미 관리 중 (월드 밴드 결과·오버레이 상향분·부모 파생) — 건드리지 않는다.
        public const int GroundMax = 3;
        public const int WorldMin = 4;
        public const int WorldMax = 15;
        /// <summary>이 값 이하는 평면 가구 — 별도 하위 밴드로 내린다.</summary>
        public const int FlatMax = 4;
        /// <summary>평면 밴드 폭.  입체 밴드와 겹치지 않게 아래에 붙인다.</summary>
        public const int FlatBandMin = 2000;   // 실제 출력은 아래 OrderForFlat 참조
        public const int OverlayLift = 2000;   // 월드 밴드 최대(1600)보다 확실히 위

        /// <summary>이 렌더러를 Y 정렬 대상으로 볼 것인가.</summary>
        public static bool IsWorldBand(int order) => order >= WorldMin && order <= WorldMax;
    }

    /// <summary>씬의 스프라이트를 훑어 Y 정렬을 적용하는 단일 관리자.
    ///
    /// 컴포넌트를 오브젝트마다 붙이지 않는 이유: 렌더러가 수백 개라 LateUpdate
    /// 호출 수백 번보다 리스트 한 번 순회가 싸다.  자가 부팅이라 씬 재베이크가
    /// 필요 없다 (이 레포의 다른 드라이버들과 같은 방식).</summary>
    public sealed class YSortManager : MonoBehaviour
    {
        private const float RescanInterval = 1.0f;

        private readonly List<SpriteRenderer> tracked = new List<SpriteRenderer>(256);
        private readonly List<int> bias = new List<int>(256);
        private readonly List<float> lastY = new List<float>(256);
        private readonly List<bool> flat = new List<bool>(256);
        // 1초 스윕마다 전량 갱신 — 위치는 그대로인데 스프라이트가 바뀐 경우 보정.
        private bool fullPass = true;
        private readonly HashSet<SpriteRenderer> known = new HashSet<SpriteRenderer>();
        private readonly HashSet<MeshRenderer> knownText = new HashSet<MeshRenderer>();
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindFirstObjectByType<YSortManager>() != null) return;
                var go = new GameObject("~YSortManager");
                go.AddComponent<YSortManager>();
                Debug.Log("[Boot] YSortManager 부착 — 톱다운 깊이 정렬 활성");
            });
        }

        private void Scan()
        {
            var all = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            int added = 0, lifted = 0;
            for (int i = 0; i < all.Length; i++)
            {
                var sr = all[i];
                if (sr == null || known.Contains(sr)) continue;
                int o = sr.sortingOrder;
                if (o <= YSort.GroundMax) { known.Add(sr); continue; }          // 지면
                if (YSort.IsWorldBand(o))
                {
                    known.Add(sr);
                    tracked.Add(sr);
                    bias.Add(o - YSort.WorldMin);
                    flat.Add(o <= YSort.FlatMax);        // 평면 가구인가
                    lastY.Add(float.NaN);   // 첫 프레임에 반드시 계산되도록
                    added++;
                    continue;
                }
                if (o < YSort.BandMin)                                           // 16..109 = 오버레이
                {
                    sr.sortingOrder = o + YSort.OverlayLift;
                    lifted++;
                }
                known.Add(sr);                                                   // 110+ 는 그대로
            }

            // 월드 텍스트(TextMesh)도 같은 밴드를 쓴다 — 주민 이름·zZ·벌목 표식·
            //  플로팅 텍스트가 28~50 이다.  스프라이트만 올리면 이 글자들이 월드
            //  밴드(110~1600) 아래로 내려가 **벽·침대 뒤로 숨는다**.  같은 규칙으로
            //  함께 올려야 지금까지의 상하 관계가 유지된다.
            var mrs = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            for (int i = 0; i < mrs.Length; i++)
            {
                var mr = mrs[i];
                if (mr == null || knownText.Contains(mr)) continue;
                int o = mr.sortingOrder;
                if (o >= YSort.WorldMin && o < YSort.BandMin)
                {
                    mr.sortingOrder = o + YSort.OverlayLift;
                    lifted++;
                }
                knownText.Add(mr);
            }

            if (added > 0 || lifted > 0)
                Debug.Log($"[YSort] 추적 +{added} (총 {tracked.Count}), 오버레이 상향 {lifted}");

        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + RescanInterval;
                Scan();
                fullPass = true;
            }
            // 추적 대상 900여 개 중 실제로 움직이는 것은 주민·동물 십수 개뿐이다.
            //  `sr.bounds` 는 행렬 계산이라 매 프레임 전부 돌리면 순손실이다 —
            //  **움직인 것만** 다시 계산하고, 나머지는 1초 스윕에서 한 번 갱신한다
            //  (스프라이트 교체처럼 위치는 그대로인데 높이가 바뀌는 경우를 위한 보정).
            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                var sr = tracked[i];
                if (sr == null)
                {
                    tracked.RemoveAt(i);
                    bias.RemoveAt(i);
                    lastY.RemoveAt(i);
                    flat.RemoveAt(i);
                    continue;
                }
                float y = sr.transform.position.y;
                if (!fullPass && Mathf.Approximately(y, lastY[i])) continue;
                lastY[i] = y;
                // 발밑 = 월드 AABB 하단.  스프라이트 높이가 달라도(1×2 침대, 나무)
                //  '땅에 닿는 지점' 이 자동으로 잡힌다.
                sr.sortingOrder = flat[i]
                    ? YSort.OrderForFlat(sr.bounds.min.y)
                    : YSort.OrderFor(sr.bounds.min.y) + bias[i];
            }
            fullPass = false;
        }
    }
}
