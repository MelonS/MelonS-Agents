using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>태양의 위치에 따라 접지 그림자를 움직인다.
    ///
    /// 계기 (2026-07-31 운영자): "태양의 움직임에 따른 그림자 효과를 구현해야 하지 않을까?"
    ///
    /// 그전까지 `BlobShadow` 는 발밑에 고정된 타원이었다 — 새벽이든 정오든 해질녘이든
    /// 방향도 길이도 같았다.  하루 주기가 핵심 루프인 장르에서 **시간의 흐름을 가장 싸게
    /// 보여주는 장치**를 놀리고 있었던 셈이다.  게다가 지금 시연 영상은 전부 낮이라
    /// 시간이 흐른다는 단서가 시계 숫자밖에 없었다.
    ///
    /// 모델 (2D 탑다운의 관례적 근사 — 물리적 정확도가 아니라 읽힘이 기준):
    ///   · 해는 동(+x)에서 떠서 서(−x)로 진다.  그림자는 **해의 반대쪽**으로 눕는다.
    ///     따라서 아침엔 서쪽(−x)으로 길게, 정오엔 발밑으로 짧게, 저녁엔 동쪽(+x)으로 길게.
    ///   · 길이는 태양 고도의 역수 성격이라 지평선 근처에서 급격히 길어진다 → cos 곡선.
    ///   · 밤에는 태양광이 없으므로 그림자를 지운다 (달빛 그림자는 과잉).
    ///
    /// 비용: 그림자는 `BlobShadow` 가 등록해 둔 목록으로만 접근한다(FindObjects 금지).
    /// 갱신은 0.2초 주기 — 그림자가 도는 속도는 게임 시계 기준 분 단위라 매 프레임일
    /// 이유가 없다.
    /// </summary>
    public class SunShadowDriver : MonoBehaviour
    {
        // 해 뜨는/지는 시각.  NightOverlay·PawnSchedule 과 같은 하루 감각을 쓴다.
        private const float SunriseH = 5.5f;
        private const float SunsetH = 19.5f;

        // 정오 대비 최대 길이 배수(일출·일몰 직전).  3배를 넘기면 스프라이트가 늘어나
        // 타원이 아니라 얼룩으로 보인다 — 실측 없이 크게 잡지 않는다.
        private const float MaxStretch = 2.6f;
        // 그림자가 발밑에서 최대로 밀려나는 거리(월드 유닛).
        private const float MaxOffset = 0.55f;

        private float nextTick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindFirstObjectByType<SunShadowDriver>() != null) return;
                var go = new GameObject("~SunShadowDriver");
                DontDestroyOnLoad(go);
                go.AddComponent<SunShadowDriver>();
            });
        }

        private void Update()
        {
            if (Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 0.2f;

            var clock = GameClock.Instance;
            if (clock == null) return;

            // 0 = 일출, 1 = 일몰.  범위 밖이면 밤.
            float h = clock.Hour + (Time.time % 60f) / 60f * 0f;   // 시 단위로 충분(분 보간 불필요)
            float t = Mathf.InverseLerp(SunriseH, SunsetH, h);
            bool day = h >= SunriseH && h <= SunsetH;

            // 태양 고도 sin(0..π): 일출/일몰 0, 정오 1.
            float alt = day ? Mathf.Sin(t * Mathf.PI) : 0f;
            // 그림자 방향: 아침(t≈0) 서쪽(−1) → 정오 0 → 저녁(t≈1) 동쪽(+1).
            float dirX = day ? Mathf.Lerp(-1f, 1f, t) : 0f;

            // 고도가 낮을수록 길고 옅다.  Mathf.Max 로 0 나눗셈 회피.
            float stretch = day ? Mathf.Lerp(MaxStretch, 1f, alt) : 1f;
            float offset = day ? dirX * MaxOffset * (1f - alt) : 0f;
            float alphaMul = day ? Mathf.Lerp(0.45f, 1f, alt) : 0f;

            var list = BlobShadow.Entries;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.t == null) continue;
                e.t.localPosition = e.baseLocalPos + new Vector3(offset, 0f, 0f);
                // 가로만 늘린다 — 세로까지 늘리면 발밑에서 떠 보인다.
                e.t.localScale = new Vector3(e.baseScale * stretch, e.baseScale, 1f);
                var sr = e.t.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    var c = sr.color;
                    c.a = e.baseAlpha * alphaMul;
                    sr.color = c;
                }
            }
        }
    }
}
