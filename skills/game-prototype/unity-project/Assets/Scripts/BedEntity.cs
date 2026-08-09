using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>#151 - 침대 quality (wiki: sleeping spot 0.8 / wood bed 1.0 / fine 1.4).</summary>
    public enum BedQuality { SleepingSpot, Wood, Fine }

    /// <summary>
    /// 운영자 fb - 침대 (#107) + quality 시스템 (#151).
    /// 레퍼런스 콜로니심: quality 별 rest multiplier 다름.
    ///   SleepingSpot 0.8x (자재 0)
    ///   Wood        1.0x (목재 8) - default
    ///   Fine        1.4x (목재 30 + 건축 skill 5+)
    /// PawnNeeds.IsOnBed() 가 OverlapBox 검사 후 RestMul() 사용.
    /// </summary>
    public class BedEntity : MonoBehaviour
    {
        [SerializeField] private BedQuality quality = BedQuality.Wood;

        // #198 D4-1 - quality 별 sprite swap.  SceneSetup 가 prefab 빌드 시 두 ref 주입.
        //   Fine → fineSprite (royal-blue/gold), Wood/SleepingSpot → woodSprite.
        //  serialized 라 prefab 에 baked → 완성 path(Instantiate)에서도 ref 살아있음
        //  (BuildManager 의존 없이 entity 가 자체 resolve → bm.BedSpriteRef plumbing 과 독립적으로 견고).
        [SerializeField] private Sprite woodSprite;
        [SerializeField] private Sprite fineSprite;

        /// <summary>SceneSetup.GenerateBuildPrefabs 가 prefab 빌드 시 호출 (두 quality sprite 주입).</summary>
        public void SetSpriteRefs(Sprite wood, Sprite fine)
        {
            woodSprite = wood;
            fineSprite = fine;
        }

        public BedQuality Quality => quality;
        public string QualityKr => quality switch
        {
            BedQuality.SleepingSpot => "수면 자리",
            BedQuality.Wood => "목재 침대",
            BedQuality.Fine => "고급 침대",
            _ => "침대",
        };

        public static readonly (float restMul, float moodBonus, Color tint)[] QualityStats = {
            (0.80f, 0f, new Color(0.65f, 0.55f, 0.45f, 1f)),
            (1.00f, 3f, new Color(0.95f, 0.95f, 0.95f, 1f)),
            (1.40f, 8f, new Color(1.00f, 0.95f, 0.70f, 1f)),
        };

        public float RestMul => QualityStats[(int)quality].restMul;
        public float MoodBonus => QualityStats[(int)quality].moodBonus;

        // #193 - vanilla colony-sim 침대 크기.  wiki:
        //   Sleeping spot  1x1  (자재 0)
        //   Wood bed       1x2  (목재 45 wiki / 우리 8 단순화)
        //   Fine bed       1x2  (목재 + quality)
        //   Royal bed      2x2  (현재는 Fine 으로 단순화)
        public static Vector2Int SizeFor(BedQuality q) => q switch
        {
            BedQuality.SleepingSpot => new Vector2Int(1, 1),
            BedQuality.Wood         => new Vector2Int(1, 2),
            BedQuality.Fine         => new Vector2Int(1, 2),
            _ => new Vector2Int(1, 1),
        };

        public Vector2Int Size => SizeFor(quality);

        public void SetQuality(BedQuality q)
        {
            quality = q;
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                // #198 D4-1 - quality 별 sprite swap.
                //  Fine → 전용 fine sprite (royal-blue/gold) + Color.white (sprite 자체가 고급 표현).
                //    기존 Fine tint (1.00,0.95,0.70) 은 파란 sprite 위에서 충돌 → white 로 교체.
                //  Wood/SleepingSpot → wood sprite + 기존 material tint.
                if (q == BedQuality.Fine && fineSprite != null)
                {
                    sr.sprite = fineSprite;
                    sr.color = Color.white;
                }
                else
                {
                    if (woodSprite != null) sr.sprite = woodSprite;
                    sr.color = QualityStats[(int)q].tint;
                }
            }
            // #193 - 1x2 침대 → sprite 가 16x32 가 아니어도 transform.localScale 로 강제 (PPU 자동 detect 안 될 시 fallback)
            //  bed_wood.png 가 16x32 면 PPU 16 자동 = world 1x2.  16x16 PNG 면 scale (1,2) 보정.
            ApplyVisualSize();
        }

        private void ApplyVisualSize()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;
            // sprite.bounds.size = world unit (PPU 반영).  target = Size (Vector2Int).
            //  ratio 로 transform.localScale 보정 → 결과 world bound = target.
            Vector2 worldSize = sr.sprite.bounds.size;
            if (worldSize.x < 0.01f || worldSize.y < 0.01f) return;
            transform.localScale = new Vector3(Size.x / worldSize.x, Size.y / worldSize.y, 1f);
        }

        private SpriteRenderer quiltSr;
        private float nextQuiltPoll;

        private void Start()
        {
            ApplyVisualSize();
            EnsureQuiltOverlay();
        }

        private void Update()
        {
            if (quiltSr == null) return;
            // 0.4s 폴링 — 매 프레임 겹침 검사는 낭비다 (침대는 몇 개 안 되지만
            //  이 규약은 이 레포 전반의 '매 프레임 FindObjects 금지' 와 같다).
            if (Time.unscaledTime < nextQuiltPoll) return;
            nextQuiltPoll = Time.unscaledTime + 0.4f;

            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;
            float footY = sr.bounds.min.y;
            var sleeper = SleeperRenderer();
            // 자는 사람이 있으면 **그 사람보다 딱 1 위**.  침대 y 기준으로 +8 을 주던
            //  이전 방식은 침대 앞을 지나가는 **다른 주민까지 덮었다** — 그게 운영자가
            //  세 번 지적한 "캐릭터가 침대 밑으로 들어감" 의 정체다.
            //  기준을 침대가 아니라 **자는 사람 본인**으로 바꾸면, 더 앞(화면 아래)에
            //  선 주민은 자기 정렬값이 더 크므로 이불 위로 그대로 지나간다.
            // 운영자 2026-08-09: "침대에 누웠을때 얼굴은 보여야 하는데".
            //  이불을 자는 사람 **위**에 두는 것이 원래 의도였다(하반신만 덮게
            //  하단 45% 만 그린다).  그런데 실제 화면은 **여섯 침대에 Z 표시만
            //  뜨고 사람이 하나도 안 보였다** — 이불 밑에 사람이 있다는 것이
            //  전혀 읽히지 않는다.  누가 어디서 자는지가 이 게임의 그림인데
            //  그게 사라졌다.
            //
            //  그래서 순서를 뒤집는다: **이불이 사람 아래**.  덮인 느낌은 줄지만
            //  자는 사람이 확실히 보인다.  둘 중 하나를 골라야 한다면 보이는 쪽이다.
            quiltSr.sortingOrder = sleeper != null
                ? sleeper.sortingOrder - 1
                : Core.YSort.OrderForFlat(footY) + 1;     // 빈 침대는 그대로
        }

        /// <summary>이 침대 위에서 실제로 **자고 있는** 주민이 있는가.
        ///  단순히 서 있는 것과 구분해야 한다 — 지나가는 사람을 덮으면 안 된다.</summary>
        private SpriteRenderer SleeperRenderer()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) return null;
            var b = sr.bounds;
            var hits = Physics2D.OverlapBoxAll(b.center, b.size * 0.9f, 0f);
            foreach (var h in hits)
            {
                if (h == null) continue;
                var needs = h.GetComponent<PawnNeeds>();
                if (needs != null && needs.IsSleeping)
                    return h.GetComponent<SpriteRenderer>()
                           ?? h.GetComponentInChildren<SpriteRenderer>();
            }
            return null;
        }

        // ── 이불 오버레이 ────────────────────────────────────────────────────
        //
        // 운영자 2026-08-01: "침대에서 잘 때는 모션이 달라야하고 침대의 이불에서
        //  자야하는데 머리에서 자고 있음."
        //
        // 위치는 PawnActions.BedStandPos 가 담요 칸으로 고정해 해결했지만, 그것만으로는
        //  주민 스프라이트가 침대 전체를 덮어 **침대 위에 서 있는** 그림이 됐다.
        //  레퍼런스 콜로니심의 문법은 '이불이 하반신을 덮는다' 이다 — 그러려면 담요가
        //  주민보다 **위에** 그려져야 한다.  침대 본체(주민 아래) 위에 담요만 뽑은
        //  층을 하나 더 얹는다.  담요 픽셀은 본체와 동일한 생성 함수에서 잘라낸
        //  것이라(`_gen_struct32.sprite_bed_quilt`) 이음매가 생기지 않고, 침대가
        //  비어 있을 때의 그림은 이전과 완전히 같다.
        // 이불의 정렬은 **BedEntity 가 직접 소유**한다 (2026-08-01 2차).
        //  운영자 "사람이 걸어갈때 침대 밑으로 들어감" 을 고치며 침대를 평면 밴드로
        //  내렸는데, 이불만 그대로 두면 이번엔 **지나가는 사람을 이불이 덮는다.**
        //  이불은 '자는 사람을 덮는' 층이지 '모든 사람 위' 가 아니다.
        //  · 아무도 안 자면 → 침대 바로 위 (평면 밴드.  지나가는 사람이 위로 지난다)
        //  · 누가 자면     → 그 사람 위 (입체 밴드.  하반신이 이불에 들어간다)
        //  YSortManager 가 건드리지 않도록 초기값을 관리 대역(110+)으로 준다.
        private const int QuiltSortingOrder = 900;
        private const string QuiltChildName = "BedQuilt";
        /// <summary>침대 스프라이트 중 주민 위로 덮을 하단 비율.
        ///
        /// 1.0 (담요 전체)로 하면 주민이 거의 다 가려 '이불 밑에 사람이 있다'가 아니라
        /// '빈 침대'로 보인다(실측).  가슴 높이까지만 덮어 상반신과 얼굴을 남긴다 —
        /// 레퍼런스 콜로니심에서도 자는 주민의 머리·어깨는 이불 위로 나와 있다.</summary>
        private const float QuiltHeightFrac = 0.45f;

        private void EnsureQuiltOverlay()
        {
            // 1×1 잠자리는 담요가 없다 (맨바닥에 눕는 자리).
            if (Size.y <= Size.x) return;
            if (transform.Find(QuiltChildName) != null) return;

            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;

            // 이불 층은 **이 침대가 실제로 쓰는 스프라이트에서 잘라 쓴다.**
            //  1차 구현은 별도 PNG 를 만들어 Resources 에서 읽었는데, 씬의 침대가
            //  그 PNG 와 다른 스프라이트(붉은 담요)를 쓰고 있어서 파란 이불이
            //  붉은 침대 위에 덮이는 색 불일치가 났다.  스프라이트 자산이 여러
            //  경로로 배정되는 구조에서는 '같은 그림을 두 번 만드는' 방식이
            //  언젠가 반드시 갈라진다.  같은 텍스처의 하단 영역을 참조하면
            //  어떤 스프라이트가 배정되든 색·주름이 자동으로 일치한다.
            //  (Sprite.Create 는 텍스처 rect 참조라 Read/Write 설정이 필요 없다.)
            var baseSp = sr.sprite;
            var r = baseSp.rect;
            float h = r.height * QuiltHeightFrac;
            var quiltSprite = Sprite.Create(
                baseSp.texture,
                new Rect(r.x, r.y, r.width, h),          // 텍스처 좌표는 아래가 0 — 하단부가 이불
                new Vector2(0.5f, 0.5f),
                baseSp.pixelsPerUnit);
            quiltSprite.name = baseSp.name + "_quilt";

            var go = new GameObject(QuiltChildName);
            go.transform.SetParent(transform, false);
            // 잘라낸 조각은 원본보다 짧다.  둘 다 중앙 피벗이므로 그대로 두면
            //  이불이 침대 한가운데로 떠오른다 — 아래 끝을 맞춰 내린다.
            //  로컬 단위는 부모 스케일(ApplyVisualSize) 적용 전 스프라이트 단위다.
            float baseH = baseSp.bounds.size.y;
            go.transform.localPosition = new Vector3(0f, -(baseH * (1f - QuiltHeightFrac)) * 0.5f, 0f);
            go.transform.localScale = Vector3.one;   // 부모 스케일 상속 (ApplyVisualSize)
            var qsr = go.AddComponent<SpriteRenderer>();
            qsr.sprite = quiltSprite;
            qsr.sortingOrder = QuiltSortingOrder;
            quiltSr = qsr;
            qsr.color = sr.color;                    // 품질 tint 동기 (wood 계열)
            qsr.sortingLayerID = sr.sortingLayerID;
        }
    }
}
