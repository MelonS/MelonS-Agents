using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// D1 몰입 레이어 (design-immersion-2026-06-11) — 움직이는 엔티티(림/동물/적)
    /// 발밑 blob 그림자.  나무/가구는 SceneSetup 의 shadow_tree.png 베이크 그림자가
    /// 이미 있고, 움직이는 엔티티만 비어 있어 "공중부양" 느낌의 주범이었다
    /// (운영자 2026-06-11 "디자인이 너무 별로야. 몰입감이 떨어져").
    ///
    /// 아트 자산 0: 32x16 타원 radial 그라데이션 텍스처를 런타임 1회 절차 생성,
    /// 전 인스턴스 공유.  부착은 각 엔티티 Awake 에서 한 줄:
    ///   BlobShadow.Attach(gameObject, 폭, y오프셋);
    /// 정렬: 본체 SpriteRenderer 와 같은 레이어, order-1 (나무 그림자와 동일 규약)
    /// → 본체 바로 밑, 지면/바닥타일(0~1) 위.
    /// </summary>
    public static class BlobShadow
    {
        private static Sprite cached;

        /// <summary>host 루트 밑에 그림자 child 생성.  width = 월드 단위 타원 폭
        /// (높이는 폭의 1/2 고정 — 콜로니심 관례 비율).  이미 있으면 no-op.</summary>
        public static void Attach(GameObject host, float width, float yOffset, float alpha = 0.35f)
        {
            if (host == null) return;
            var bodySr = host.GetComponent<SpriteRenderer>();
            if (bodySr == null) return;
            if (host.transform.Find("BlobShadow") != null) return;  // 클론/재호출 중복 방지

            var go = new GameObject("BlobShadow");
            go.transform.SetParent(host.transform, false);
            go.transform.localPosition = new Vector3(0f, yOffset, 0f);
            // 스프라이트 기본 크기 2x1 월드유닛 → 균일 스케일 width/2 로 폭:높이 = 2:1 유지.
            float s = width / 2f;
            go.transform.localScale = new Vector3(s, s, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSprite();
            sr.color = new Color(0f, 0f, 0f, alpha);
            sr.sortingLayerID = bodySr.sortingLayerID;
            sr.sortingOrder = bodySr.sortingOrder - 1;
        }

        private static Sprite GetSprite()
        {
            if (cached != null) return cached;
            const int W = 32, H = 16;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;   // 픽셀톤 세계에서도 그림자는 부드럽게
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f - W / 2f) / (W / 2f);
                    float dy = (y + 0.5f - H / 2f) / (H / 2f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a;                       // 가장자리 soft falloff
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            // 주의: makeNoLongerReadable=true 금지 — Sprite.Create 의 기본 Tight 메시는
            //  알파 지오메트리 생성에 읽기 가능 텍스처가 필요해 non-readable 이면 조용히
            //  빈 스프라이트가 된다 (D1 1차 빌드에서 그림자 전체 미표시 원인).
            //  그라데이션은 FullRect 쿼드가 정답이기도 하다 (tight 가 저알파 가장자리 클립).
            tex.Apply(false, false);
            // PPU 16 → 2x1 월드유닛 (지형/스프라이트 공통 PPU 와 일치).
            cached = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 16f,
                                   0, SpriteMeshType.FullRect);
            cached.name = "BlobShadowSprite";
            return cached;
        }
    }
}
