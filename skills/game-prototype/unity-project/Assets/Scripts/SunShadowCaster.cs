using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>구조물·나무·가구가 **자기 실루엣을 땅에 눕히는** 태양 그림자.
    ///
    /// 계기 (2026-07-31): 운영자 "태양의 움직임에 따른 그림자 효과" 요청으로 발밑 타원
    /// (`BlobShadow`)을 시각에 맞춰 움직이게 했는데, **스틸에서 변화를 눈으로 구분할 수
    /// 없었다**.  농도를 올려도 한계였다 — 대상이 콜로니스트 발밑 얼룩뿐이라 화면 전체의
    /// 인상이 바뀌지 않기 때문이다.
    ///
    /// 레퍼런스 리서치(원 개발자 devblog)가 그 차이를 정확히 짚는다:
    ///   "shadows fade in and out based on time of day, move across the ground,
    ///    **don't shade indoors**, come from people and **small objects as well as
    ///    large structures**, **project to varying heights**, and change color slightly
    ///    to complement the color of the sky"
    /// 즉 시간의 흐름이 읽히는 이유는 **건물이 땅에 길게 눕는 그림자**다.  발밑 타원은
    /// 접지감을 주는 보조 장치일 뿐 시간을 말하지 않는다.
    ///
    /// 구현: 호스트의 스프라이트를 그대로 복제해 검게 칠하고, 밑변을 축으로
    ///   · 해의 반대쪽으로 눕히고(가로 오프셋 + 기울기)
    ///   · 태양 고도가 낮을수록 길게(세로 스케일)
    ///   · 지붕 아래면 숨긴다
    /// 그림자는 순수 장식이다 — 레퍼런스도 작물 성장·태양광에 영향을 주지 않는다.
    ///
    /// 비용: 등록부 방식(FindObjects 금지).  갱신은 SunShadowDriver 가 0.2초 주기로
    /// 한 번에 돈다.
    /// </summary>
    public static class SunShadowCaster
    {
        public struct Entry
        {
            public Transform t;          // 그림자 트랜스폼
            public SpriteRenderer sr;
            public Transform host;       // 원본 (지붕 판정용 좌표)
            public float height;         // 오브젝트 '높이' — 투영 길이 배수
            public float baseAlpha;
        }

        private static readonly System.Collections.Generic.List<Entry> _list =
            new System.Collections.Generic.List<Entry>(256);

        /// <summary>host 아래 그림자 자식을 만든다.  height = 낮은 물건 0.4 ~ 나무 1.4.</summary>
        public static void Attach(GameObject host, float height, float alpha = 0.30f)
        {
            if (host == null) return;
            var body = host.GetComponent<SpriteRenderer>();
            if (body == null || body.sprite == null) return;
            if (host.transform.Find("SunShadow") != null) return;   // 멱등

            // ── 피벗 정렬 (2026-07-31 3차) ──────────────────────────────────
            //  운영자: "그림자가 옆으로 평행이동된 느낌.  줄기 중심과 그림자 중심이
            //  이어지지 않는다.  축이 틀리면 바로 어색함을 느낀다."  정확한 진단이다.
            //
            //  원인: 스프라이트 피벗이 중앙(0.5,0.5)이라 `localPosition = v` 로 옮기면
            //  **그림자 한가운데**가 그 지점으로 간다.  그래서 줄기 바닥과 그림자
            //  시작점이 끊기고, 회전도 중앙 기준이라 오브젝트마다 축이 미세하게 어긋난다.
            //
            //  해결: 그림자를 **피벗 노드의 자식**으로 둔다.
            //    pivotGo : 오브젝트의 밑변(발/줄기 바닥)에 고정 — 여기서 회전한다
            //    go      : 그 안에서 위로 half 만큼 올려 둔다 (스프라이트 아래 끝이 축)
            //  이러면 회전·신축이 모두 **밑변 한 점**을 기준으로 일어나므로,
            //  모든 그림자가 같은 각도로 같은 점에서 출발한다.
            float halfH = body.sprite.bounds.extents.y;
            var pivotGo = new GameObject("SunShadowPivot");
            pivotGo.transform.SetParent(host.transform, false);
            pivotGo.transform.localPosition = new Vector3(0f, -halfH, 0f);   // 밑변

            var go = new GameObject("SunShadow");
            go.transform.SetParent(pivotGo.transform, false);
            go.transform.localPosition = new Vector3(0f, halfH, 0f);         // 스프라이트 복원
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = body.sprite;
            sr.color = new Color(0f, 0f, 0f, alpha);
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = body.sortingOrder - 2;   // 본체와 접지 타원 아래
            //  드라이버는 **피벗 노드**를 돌린다 (그림자 노드가 아니라).
            _list.Add(new Entry { t = pivotGo.transform, sr = sr, host = host.transform,
                                  height = height, baseAlpha = alpha });
        }

        public static System.Collections.Generic.List<Entry> Entries
        {
            get
            {
                for (int i = _list.Count - 1; i >= 0; i--)
                    if (_list[i].t == null || _list[i].host == null) _list.RemoveAt(i);
                return _list;
            }
        }
    }
}
