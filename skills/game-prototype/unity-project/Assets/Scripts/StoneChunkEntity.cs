using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #119 - 채광 후 바닥에 떨어지는 돌덩이 (WoodPile 패턴 동일).
    /// PawnHauler 가 운반 → ResourceManager.AddStone.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class StoneChunkEntity : MonoBehaviour
    {
        [SerializeField] private int stone = 1;
        public bool InStockpile = false;  // stockpile stack 보존

        public int Stone => stone;
        public GameObject ReservedBy { get; set; }
        public bool IsReserved => ReservedBy != null;

        // 폴리싱(#61): 양에 비례해 더미가 커 보이도록(WoodPile 공용 스케일). spawn·shrink 동기.
        public void SetStone(int v)
        {
            stone = v;
            transform.localScale = Vector3.one * WoodPileEntity.PileScale(v);
        }

        // 운영자 2026-06-02: "돌덩이가 너무 빨리 사라짐".  돌은 부패하지 않는 자원이므로
        //  lifetimeSec 통째 Destroy(180s)를 제거 — 운반될 때까지 바닥에 영구히 남는다.
        //  (Update 부패 로직 없음 = 자연스러운 광물 거동.)

        // #214 운영자 fb: 즉시-credit/teleport 제거.  과거 Pickup() 은 줍는 즉시
        //  ResourceManager.AddStone + Destroy = 순간이동.  운반은 PawnHauler(물리 carry)
        //  전담이므로 이 즉시-적립 경로는 완전히 제거한다.  외부 호출처 없음(확인됨).

        // #215 운영자 fb "캐면 순간이동" — StoneVein 이 sprite null 일 때 즉시 AddStone
        //  하던 텔레포트를 없애려면, chunk 가 sprite 없이도 항상 보여야 한다.  WoodPile
        //  의 EnsureSprite 패턴을 그대로 복제 — null 이면 코드에서 회색 돌덩이를 생성.
        private static Sprite _fallbackSprite;
        public static Sprite EnsureSprite(Sprite candidate)
        {
            if (candidate != null) return candidate;
            if (_fallbackSprite != null) return _fallbackSprite;
            var loaded = Resources.Load<Sprite>("Sprites/stone_chunk");
            if (loaded != null) { _fallbackSprite = loaded; return _fallbackSprite; }
            const int W = 14, H = 10;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var clear = new Color(0f, 0f, 0f, 0f);
            var rock    = new Color(0.50f, 0.50f, 0.54f, 1f);   // 회색 돌
            var rockLit = new Color(0.66f, 0.66f, 0.70f, 1f);   // 윗면 하이라이트
            var rockDk  = new Color(0.34f, 0.34f, 0.38f, 1f);   // 그림자
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            void Blob(int cx, int cy, int rx, int ry)
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry;
                        if (dx * dx + dy * dy > 1f) continue;
                        bool top = y >= cy + ry - 1;
                        bool bot = y <= cy - ry + 1;
                        px[y * W + x] = top ? rockLit : (bot ? rockDk : rock);
                    }
            }
            Blob(5, 4, 4, 3);
            Blob(9, 5, 4, 3);
            tex.SetPixels(px);
            tex.Apply();
            _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 16f);
            _fallbackSprite.name = "StoneChunk_RuntimeFallback";
            return _fallbackSprite;
        }

        public static StoneChunkEntity Spawn(Vector3 pos, int amount, Sprite sprite)
        {
            var go = new GameObject($"StoneChunk_{amount}");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EnsureSprite(sprite);  // #215 - null 이면 코드 기본 sprite (항상 가시)
            sr.sortingOrder = 7;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.6f);
            col.isTrigger = true;
            var chunk = go.AddComponent<StoneChunkEntity>();
            chunk.SetStone(amount);   // 양에 비례한 시각 스케일 포함(#61)
            return chunk;
        }
    }
}
