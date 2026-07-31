using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 운영자 fb #116 - 나무 캐면 즉시 inventory 들어가지 말고
    /// 바닥에 통나무 더미 entity 생성.  pawn 의 PawnHauler 가 줍어서 운반.
    ///
    /// 1차 단순화: stockpile 개념 없이 줍기만 (줍는 순간 inventory 차감).
    ///   추후 stockpile 영역 + 거기로 운반 의무 추가 가능.
    /// 다른 hauler 가 같은 pile 중복 reserve 안 하도록 reservedBy 필드.
    /// 60s 동안 안 줍히면 사라짐 (림 vanilla: deteriorate).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class WoodPileEntity : MonoBehaviour
    {
        [SerializeField] private int wood = 5;
        public bool InStockpile = false;

        // 운영자 2026-06-02: "통째로 빨리 사라질 게 아니라 개별 내구도가 조금씩 닳게".
        //  durability 0~100 이 옥외(!InStockpile)에서 천천히 감소(1 game day=24s 기준
        //  DurabilityPerDay/day) → 값에 비례해 스프라이트가 점점 흐려지고 0 에서 소멸.
        //  InStockpile 이면 감소 정지(보존).  기존 lifetimeSec(120s) 통째 Destroy +
        //  wood 수량 차감식 부패는 제거(수량 wood 는 그대로, 내구도만 닳는다).
        [SerializeField] private float durability = 100f;
        private const float DurabilityPerDay = 10f;   // 100/10 = 10 game-day ~ 240s

        public int Wood => wood;
        public float Durability => durability;   // #37 검증용(옥외 더미 내구도 fade 확인)
        public GameObject ReservedBy { get; set; }   // PawnHauler 가 set/clear
        public bool IsReserved => ReservedBy != null;

        private SpriteRenderer _sr;

        // 폴리싱(#61): 양 변경 시 시각 스케일도 갱신(spawn·shrink·merge 모두 동기).
        public void SetWood(int amount)
        {
            wood = amount;
            // 아트 v2: 스택양은 스프라이트 3단계로 (스케일 1 고정).  시트 없으면 폴백.
            var v2 = ItemArt32.Stage("wood", amount);
            if (v2 != null)
            {
                if (_sr == null) _sr = GetComponent<SpriteRenderer>();
                if (_sr != null) _sr.sprite = v2;
                transform.localScale = Vector3.one;
            }
            else transform.localScale = Vector3.one * PileScale(amount);
        }

        // 폴리싱(#61): 더미 양 → 시각 스케일 (모든 더미 종류 공용 — Stone/Meat 도 호출).
        //  운영자: 더미가 양과 무관하게 같은 크기로 보였음(5개=50개).  콜로니 심 관례대로
        //  쌓인 양이 많을수록 더미가 더 커 보이게.  1개=0.89, 5개~0.99, 25개~1.22,
        //  50개+=1.4 로 클램프.  sqrt 로 증가 둔화해 큰 더미도 타일을 크게 안 벗어남.
        public static float PileScale(int amount)
        {
            float t = Mathf.Clamp01(Mathf.Sqrt(Mathf.Max(1, amount)) / Mathf.Sqrt(50f));
            // #아이템스케일 (운영자 2026-06-11 "한칸을 다 차지하는 아이템은 별로 없음"):
            //  0.8~1.4(만재 시 1.4칸!) → 0.5~0.78.  레퍼런스 콜로니심처럼 아이템은 칸의 ~2/3 —
            //  '물건이 놓여 있다'로 읽히고 지면/그리드가 숨 쉰다.  양→크기 관례는 유지.
            // 2026-07-31 운영자 "아이템들 크기 너무 작음.  이거 레퍼런스랑 사이즈 맞춘거
            //  맞음??" (영상 10/100).  **실측으로 확인했다 — 안 맞는다.**
            //   · 콜로니스트: 32px 프레임 안 실제 내용 14x29 → 타일의 0.44 x 0.91
            //   · 목재 더미 : 32px 프레임 안 실제 내용 20x8  → 타일의 0.62 x 0.25
            //     여기에 아래 스케일 0.5~0.78 이 곱해져 최종 **0.13~0.20 타일 높이**.
            //   즉 바닥 아이템이 사람보다 5~6배 낮았다.  레퍼런스에서는 바닥 더미가
            //   사람 키의 절반쯤 된다.
            //
            //  0.5~0.78 은 2026-06-11 "한칸을 다 차지하는 아이템은 별로 없음" 피드백으로
            //  1.4 에서 내린 값인데, **스프라이트가 프레임을 꽉 채운다고 가정한 계산**이었다.
            //  실제 내용은 프레임의 4분의 1 높이라, 줄이면 보이지 않는 지경이 된다.
            //
            //  0.95~1.35 로 올린다 → 최종 0.59~0.84 타일 폭 / 0.24~0.34 타일 높이.
            //  폭이 한 칸을 넘지 않으므로 '칸을 다 차지한다'는 원래 지적도 지킨다.
            //  (스프라이트가 20x8 로 납작한 것 자체는 아트 쪽 후속 — 균일 스케일로는
            //   높이를 더 올리면 폭이 칸을 넘는다.)
            return Mathf.Lerp(0.95f, 1.35f, t);
        }

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            // 아트 B2: TS 더미 스프라이트는 그림자 베이크 — BlobShadow 중복 부착 안 함.
            //  (구세대 스프라이트 폴백 시에만 필요해지면 스프라이트명 검사로 복원)
        }

        // #214 운영자 fb "아이템이 뿅 이동" — 즉시-credit/teleport 완전 제거.
        //  과거 Pickup() 은 줍는 즉시 ResourceManager.AddWood + Destroy = 순간이동으로,
        //  stockpile 운반(물리)을 우회해 priority-haul 을 무력화했다.  현재 운반은
        //  PawnHauler 가 줍기→물리 carry→내려놓기로 전담하므로 이 경로를 삭제한다.
        //  외부 호출처 없음(확인됨).

        private void Update()
        {
            // 옥외 더미만 내구도 감소(저장구역에 들어가면 보존).  통째로 사라지지 않고
            //  천천히 닳다가 0 에서 소멸 — 운영자 fb.
            if (!InStockpile)
            {
                // 밸런스 A1(2026-07-24): 구 시계 잔재 /24f("24초=1일") → /1000f (1게임일=1000초
                //  정합).  이전엔 목재가 0.24일 만에 증발 — 바닐라 파리티 문서 §4 참조.
                durability -= DurabilityPerDay * (Time.deltaTime / 1000f);
                if (durability <= 0f) { Destroy(gameObject); return; }
            }
            // 내구도에 비례해 점점 흐려짐(통째로 뿅 사라지지 않게).
            if (_sr != null)
            {
                // #37 운영자 "내구도 안 됨": 바닥 0.35 라 거의 끝까지 멀쩡해 보이다 갑자기
                //  소멸 → 닳는 과정이 안 보였음.  바닥 0.12 로 낮춰 옥외 더미가 점점 옅어지다
                //  사라지는 게 눈에 보이게 한다(속도는 장르 정합 유지).
                var c = _sr.color;
                c.a = Mathf.Lerp(0.12f, 1f, Mathf.Clamp01(durability / 100f));
                _sr.color = c;
            }
        }

        // #213 운영자 fb — "목재가 나무 위치에 안 보이고 순간이동".  root cause (a):
        //  Spawn 이 넘겨받은 sprite 가 null 이면 (SceneSetup 미배선 / GameManager 가
        //  woodPileSpriteRuntime!=null 일 때만 static field 를 채움) pile 이 보이지
        //  않게 떨어진다 → 운영자에겐 "나무 쓰러졌는데 목재가 없음" → 나중에 hauler 가
        //  stockpile 에 새 pile 을 만들 때 비로소 보임 = "순간이동".
        //  해결: Spawn 이 null 을 받으면 코드에서 기본 sprite 를 보장한다.  먼저
        //  Resources 에 실 asset 이 있으면 그걸, 없으면 런타임 생성한 통나무더미
        //  텍스처를 쓴다.  이로써 pile 은 항상 "나무 쓰러진 자리"에 눈에 보이게 떨어진다.
        private static Sprite _fallbackSprite;
        public static Sprite EnsureSprite(Sprite candidate)
        {
            if (candidate != null) return candidate;
            if (_fallbackSprite != null) return _fallbackSprite;

            // 1) Resources 에 실제 asset 이 있으면 우선 사용 (디자이너 교체 가능).
            var loaded = Resources.Load<Sprite>("Sprites/wood_pile");
            if (loaded != null) { _fallbackSprite = loaded; return _fallbackSprite; }

            // 2) 없으면 런타임 생성 — 갈색 통나무 더미 (16x12, 4 stacked logs).
            const int W = 16, H = 12;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var clear = new Color(0f, 0f, 0f, 0f);
            var bark    = new Color(0.42f, 0.28f, 0.14f, 1f);   // 짙은 갈
            var barkLit = new Color(0.55f, 0.38f, 0.20f, 1f);   // 윗면 하이라이트
            var ringC   = new Color(0.78f, 0.64f, 0.42f, 1f);   // 단면 나이테
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            // 4개 통나무를 2단으로 쌓음 (가로 누운 원기둥 느낌).
            void Log(int x0, int y0, int w, int h)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int gx = x0 + x, gy = y0 + y;
                        if (gx < 0 || gx >= W || gy < 0 || gy >= H) continue;
                        bool top = y == h - 1;
                        bool endRing = x <= 1;          // 왼쪽 단면 = 나이테
                        px[gy * W + gx] = endRing ? ringC : (top ? barkLit : bark);
                    }
            }
            Log(1, 1, 13, 4);   // bottom row
            Log(3, 5, 11, 4);   // top row (살짝 안쪽)
            tex.SetPixels(px);
            tex.Apply();
            _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, W, H),
                new Vector2(0.5f, 0.5f), 16f);  // 16 ppu → 약 1 cell
            _fallbackSprite.name = "WoodPile_RuntimeFallback";
            return _fallbackSprite;
        }

        /// <summary>외부 (SceneSetup, TreeEntity) 에서 spawn 헬퍼.
        /// #213 - sprite 가 null 이면 EnsureSprite 가 기본 sprite 를 보장하므로
        ///  pile 은 항상 눈에 보인다.  pile 위치 = pos (벌목 시 = 나무 cell = 쓰러진 자리).
        ///  hauler 가 운반해야만 카운터에 반영된다.</summary>
        public static WoodPileEntity Spawn(Vector3 pos, int amount, Sprite sprite)
        {
            var go = new GameObject($"WoodPile_{amount}");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EnsureSprite(sprite);  // #213 - null 이면 코드 기본 sprite 보장 (항상 가시)
            sr.sortingOrder = 7;  // pawn(10)보다 아래, 타일보다 위
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.9f, 0.6f);
            col.isTrigger = true;  // 충돌 안 막음 - pawn 통과 가능
            var pile = go.AddComponent<WoodPileEntity>();
            pile.SetWood(amount);   // 양에 비례한 시각 스케일 포함(#61)
            return pile;
        }
    }
}
