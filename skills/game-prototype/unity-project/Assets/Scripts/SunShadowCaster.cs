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
            public SpriteRenderer hostSr;   // 원본 렌더러 — 스프라이트가 바뀌면 따라간다
            public Transform sprite;        // 그림자 스프라이트 노드 (피벗의 자식)
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

            // ── 전단(shear) 방식 (2026-07-31 4차, 리서치 반영) ────────────────
            //  회전으로 눕히던 것을 버린다.  2D 탑다운 그림자의 표준은 **전단**이다:
            //   · 밑변은 고정 — 발/밑동에 붙어 있어야 한다
            //   · 윗변만 광원 반대쪽으로 밀어낸다
            //  회전은 밑변까지 돌려 물체가 '쓰러진' 모양이 되고 접지점이 떨어진다.
            //  그래서 운영자가 세 번 "축이 안 맞는다"고 지적했다.
            //  Unity Transform 은 전단을 지원하지 않으므로 버텍스 셰이더로 한다.
            var go = new GameObject("SunShadow");
            go.transform.SetParent(host.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = body.sprite;
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = body.sortingOrder - 2;

            var sh = Shader.Find("MelonS/SpriteShadowShear");
            if (sh != null)
            {
                // 인스턴스 머티리얼 — 오브젝트마다 전단량이 달라 공유할 수 없다.
                sr.material = new Material(sh);
                sr.material.SetFloat("_PivotY", -body.sprite.bounds.extents.y);
                sr.material.SetFloat("_Height", body.sprite.bounds.size.y);
            }
            sr.color = new Color(0f, 0f, 0f, alpha);

            _list.Add(new Entry { t = go.transform, sr = sr, host = host.transform,
                                  hostSr = body, sprite = go.transform,
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
