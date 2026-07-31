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

            // ── 태양 위치와 그림자 축 (2026-07-31 2차 — 물리적으로 다시 세움) ──
            //
            //  운영자: "그림자가 축이 안 맞는데?  물리학적으로 말이 안 되게 그리고 있어."
            //  맞다.  1차 구현은 해가 동↔서로만 움직인다고 두고 그림자를 **가로로만**
            //  눕혔다(y 성분 0).  그러면 그림자가 지면에 누운 게 아니라 옆으로 미끄러지는
            //  모양이 된다 — 정오에도 그림자가 발밑이 아니라 좌우 어딘가에 있다.
            //
            //  실제 기하:
            //   · 해는 하늘을 반원으로 지난다.  방위각은 동(일출) → 남(정오) → 서(일몰),
            //     고도는 0 → 최대 → 0.
            //   · 그림자는 **해의 정반대 방향**으로, 길이는 1/tan(고도) 에 비례한다.
            //   · 북반구 탑다운 화면에서 해가 남쪽에 있으므로 그림자는 항상 **북(+y)
            //     성분**을 갖는다.  정오에는 짧게 북쪽, 아침엔 북서, 저녁엔 북동.
            //
            //  그래서 방위각 az 를 -90°(동) → 0°(남) → +90°(서) 로 두고,
            //  그림자 방향 = (-sin az, +cos az) · 길이.  y 성분이 늘 양수라 화면에서
            //  '오브젝트 뒤로 눕는다'가 성립한다.
            //  화면 좌표: +x = 동, +y = 북.  φ = t·π 로 하루를 반원으로 돈다.
            //   해의 수평 방향  sunDir = ( cos φ, −sin φ )
            //     φ=0   (일출) → (+1,  0)  동
            //     φ=π/2 (정오) → ( 0, −1)  남
            //     φ=π   (일몰) → (−1,  0)  서
            //   그림자는 그 정반대  dir = ( −cos φ, +sin φ )
            //     일출 → (−1, 0) 서쪽으로 길게
            //     정오 → ( 0,+1) 북쪽으로 짧게   ← y 성분이 항상 ≥0 이라 '뒤로 눕는다'
            //     일몰 → (+1, 0) 동쪽으로 길게
            //  고도는 sin φ (일출·일몰 0, 정오 1).  길이는 1/tan(고도) 성격이라
            //  지평선 근처에서 급격히 길어진다 → 아래에서 Lerp 로 유계 근사한다.
            float phi = t * Mathf.PI;
            float alt = day ? Mathf.Sin(phi) : 0f;
            float dirX = day ? -Mathf.Cos(phi) : 0f;
            float dirY = day ?  Mathf.Sin(phi) : 0f;

            // 고도가 낮을수록 길고 옅다.  Mathf.Max 로 0 나눗셈 회피.
            // 길이 계수 — 고도가 낮을수록 길다(1/tan 의 유계 근사).  방향과 분리해 둔다.
            float lenF = day ? Mathf.Lerp(1f, 0.18f, alt) : 0f;
            float stretch = day ? Mathf.Lerp(MaxStretch, 1f, alt) : 1f;
            Vector3 blobOff = new Vector3(dirX, dirY, 0f) * (MaxOffset * lenF);
            // 농도: 기본 알파(0.35)를 그대로 쓰면 잔디 위에서 **스틸로 확인이 안 된다**
            //  (2026-07-31 실측 — 7/12/18시를 정지 상태로 찍어 대조했는데 차이를 눈으로
            //   구분할 수 없었다).  스틸에서 안 보이면 심사자에게도 안 보인다.
            //  낮에는 기본보다 진하게(최대 1.9배) 깔고, 해가 낮을수록 옅어지는 관계는 유지.
            //  밤에는 0 (달빛 그림자는 과잉).
            float alphaMul = day ? Mathf.Lerp(1.1f, 1.9f, alt) : 0f;

            // 하늘색을 살짝 섞는다 — 레퍼런스: "change color slightly to complement the
            //  color of the sky".  순수 검정 그림자는 픽셀 세계에서 구멍처럼 보인다.
            //  낮에는 푸른 기, 노을엔 붉은 기가 아주 옅게 든다.
            Color tint = day
                ? Color.Lerp(new Color(0.10f, 0.07f, 0.05f), new Color(0.05f, 0.06f, 0.12f), alt)
                : new Color(0.04f, 0.05f, 0.10f);

            var list = BlobShadow.Entries;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.t == null) continue;
                e.t.localPosition = e.baseLocalPos + blobOff;
                // 가로만 늘린다 — 세로까지 늘리면 발밑에서 떠 보인다.
                e.t.localScale = new Vector3(e.baseScale * stretch, e.baseScale, 1f);
                var sr = e.t.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    var c = tint; c.a = e.baseAlpha * alphaMul;
                    sr.color = c;
                }
            }

            // ── 구조물 그림자 (실루엣이 땅에 눕는다) ────────────────────────────
            //  이게 화면 인상을 바꾸는 쪽이다 — 발밑 타원은 접지감만 준다.
            var cast = SunShadowCaster.Entries;
            for (int i = 0; i < cast.Count; i++)
            {
                var e = cast[i];
                if (e.t == null || e.host == null) continue;

                // 지붕 아래는 그림자를 만들지 않는다 (레퍼런스: don't shade indoors).
                //  오늘 만든 지붕 시스템을 그대로 재사용한다.
                bool indoors = false;
                var rd = RoofDesignation.Instance;
                if (rd != null)
                {
                    indoors = rd.IsRoofed(Mathf.FloorToInt(e.host.position.x),
                                          Mathf.FloorToInt(e.host.position.y));
                }
                bool show = day && !indoors;
                if (e.t.gameObject.activeSelf != show) e.t.gameObject.SetActive(show);
                if (!show) continue;

                // ── 밑변 회전 (2026-07-31 3차 — 운영자 '축이 안 맞는다') ──────
                //  이전엔 그림자를 통째로 평행이동(localPosition = v)했다.  스프라이트
                //  피벗이 중앙이라 **그림자 한가운데**가 그 지점으로 갔고, 그래서
                //  줄기 바닥과 그림자 시작점이 끊겨 '옆으로 밀린' 느낌이 났다.
                //  이제 t 는 밑변에 고정된 피벗 노드다 — 위치는 건드리지 않고
                //  **각도와 길이만** 준다.  모든 그림자가 같은 점에서 같은 각도로 출발한다.
                //
                //  각도: 그림자가 향하는 방위를 화면 각으로.  atan2(dirX, dirY) 는
                //   정오(북) 0°, 아침(서) −90°, 저녁(동) +90°.  스프라이트는 위로
                //   서 있으므로 그대로 이 각만큼 눕히면 된다(감쇠 없이 — 감쇠를 주면
                //   오브젝트마다 축이 어긋나 보인다).
                float angDeg = Mathf.Atan2(dirX, Mathf.Max(0.0001f, dirY)) * Mathf.Rad2Deg;
                //  탑다운 투영 보정: 화면 세로는 실제 거리보다 짧게 보이므로 각을 조금 벌린다.
                angDeg = Mathf.Clamp(angDeg * 1.15f, -82f, 82f);
                e.t.localPosition = Vector3.zero;
                e.t.localRotation = Quaternion.Euler(0f, 0f, -angDeg);

                //  길이 ∝ 물체 높이 (운영자: 사슴 그림자가 나무만큼 길면 안 된다).
                //   height 는 오브젝트 높이 배수 — 나무 1.4 / 동물 0.45 / 사람 0.5.
                //   lenF 는 태양 고도에 따른 공통 배수라 **모든 그림자가 같은 비율로**
                //   길어지고 짧아진다.  세로는 탑다운 투영이라 0.62 로 눌러 둔다.
                float len = e.height * Mathf.Lerp(0.35f, 1.55f, lenF) * 0.62f;
                e.t.localScale = new Vector3(1f, len, 1f);

                var c2 = tint; c2.a = e.baseAlpha * alphaMul;
                e.sr.color = c2;
            }
        }
    }
}
