using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// A choppable tree.  Day 3 = HP + on-chop damage + destroy + wood drop.
    /// </summary>
    /// <summary>#149 - 레퍼런스 콜로니심 wiki tree species (Oak 단단/yield 큼, Pine 빠름, Birch 중간).</summary>
    // L1 (2026-07-24 운영자 "나무 종류도 더"): Maple/Spruce 추가.  기존 3종 순서 불변
    //  (세이브 int 직렬화 호환 — 뒤에만 append).
    public enum TreeSpecies { Pine, Birch, Oak, Maple, Spruce }

    [RequireComponent(typeof(SpriteRenderer))]
    public class TreeEntity : MonoBehaviour
    {
        [SerializeField] private float maxHp = 100f;
        [SerializeField] private int woodDrop = 5;
        [SerializeField] private TreeSpecies species = TreeSpecies.Pine;

        public TreeSpecies Species => species;
        public string SpeciesKr => species switch
        {
            TreeSpecies.Pine => "소나무",
            TreeSpecies.Birch => "자작나무",
            TreeSpecies.Oak => "참나무",
            TreeSpecies.Maple => "단풍나무",
            TreeSpecies.Spruce => "가문비나무",
            _ => "나무",
        };

        // 종별 spec (HP, woodDrop, scale, tint)
        // 운영자 2026-06-02: 종별 스프라이트 추가(소나무만 → 자작·참나무).  sprite 가 색을
        //  가지므로 tint 는 흰색(런타임 HP-darken 의 base).  scale 로 크기 다양성 유지.
        // 운영자 피드백 #3 (2026-06-12 AM): 채집량 레퍼런스 동일 — 나무 1그루 ~25
        //  (Pine 20 / Birch 25 / Oak 30).  비용도 같은 라운드에서 파리티(문 25/침대 45
        //  /고급 100/화덕 80/연구대 75) — '나무 1그루 = 벽 4~6개' 비율이 핵심.
        public static readonly (float hp, int yield, float scale, Color tint)[] SpeciesStats = {
            (80f,  20, 0.95f, new Color(1f, 1f, 1f, 1f)),   // Pine  - 빠름 작음 (침엽 실루엣)
            (100f, 25, 1.00f, new Color(1f, 1f, 1f, 1f)),   // Birch - 중간 (키큰 로브)
            (150f, 30, 1.10f, new Color(1f, 1f, 1f, 1f)),   // Oak   - 단단 큼 (넓은 로브)
            // L1 신규 2종 — 실루엣이 정체성, 틴트는 은은한 보조 (원색 금지)
            (110f, 26, 1.00f, new Color(1.05f, 0.88f, 0.78f, 1f)),  // Maple  - 성긴 로브 + 담적
            (130f, 28, 1.15f, new Color(0.88f, 0.98f, 0.94f, 1f)),  // Spruce - 키큰 침엽 + 청록기
        };

        // 종별 스프라이트 [Pine,Birch,Oak] — SceneSetup(SpawnTrees)이 로딩 후 주입.
        public static Sprite[] SpeciesSprites;

        public void SetSpecies(TreeSpecies s)
        {
            species = s;
            var (h, y, sc, tint) = SpeciesStats[(int)s];
            maxHp = h;
            woodDrop = y;
            hp = h;
            transform.localScale = new Vector3(sc, sc, 1);
            // 캐시 spriteRenderer 는 edit-time(Awake 미실행)엔 null → fresh GetComponent.
            var sr = spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (SpeciesSprites != null && (int)s < SpeciesSprites.Length
                    && SpeciesSprites[(int)s] != null)
                    sr.sprite = SpeciesSprites[(int)s];
                sr.color = tint;
            }
        }

        // 운영자 fb #116 - 벌목 시 즉시 inventory 추가가 아닌 WoodPile entity drop.
        //  SceneSetup 이 sprite 로딩 후 여기에 박음.
        public static Sprite WoodPileSprite;

        [Header("Regen (Day 12)")]
        [SerializeField] private float saplingDelaySec = 120f;

        private float hp;
        private SpriteRenderer spriteRenderer;

        public bool IsDestroyed => hp <= 0f;
        public float HpRatio => maxHp > 0f ? hp / maxHp : 0f;   // 진단 로깅용 read-only

        private void Awake()
        {
            hp = maxHp;
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Throttle chop SFX — without this it fires every frame (60x/sec
        // = white-noise buzz instead of rhythmic chop).
        private float lastChopSoundTime = -10f;
        private const float ChopSoundInterval = 0.45f;

        /// <summary>Apply chop damage. Returns true if tree was destroyed this hit.</summary>
        public bool TakeChopDamage(float dmg)
        {
            if (IsDestroyed) return false;
            hp -= dmg;
            // #156 - Visual feedback (darken) 가 #149 species tint 를 덮어쓰면 종 구분 사라짐.
            //  #167 - TintHelper 로 통합 (minBright=0 으로 100% tree 가 까매질 수 있음).
            if (spriteRenderer != null)
            {
                var (_, _, _, baseTint) = SpeciesStats[(int)species];
                TintHelper.ApplyHpBrightness(spriteRenderer, baseTint, hp / maxHp, minBright: 0f);
            }
            // Chop SFX — throttled so it sounds rhythmic, not buzzy
            if (Time.time - lastChopSoundTime >= ChopSoundInterval)
            {
                AudioBank.Instance?.PlayChop();
                lastChopSoundTime = Time.time;
                // D3 몰입 — 타격 순간 나무칩 파편 (SFX 스로틀과 동기 = 도끼질 리듬).
                ParticleFx.Burst(transform.position + Vector3.up * 0.25f,
                                 new Color(0.55f, 0.38f, 0.20f, 1f));
            }
            if (hp <= 0f)
            {
                // #212 운영자 fb — 레퍼런스 콜로니심 정상 흐름: 벌목 시 즉시 +N 절대 안 함.
                //  목재 더미(WoodPile)가 "나무가 쓰러진 자리" = 이 나무의 cell 에 떨어진다.
                //  이후 hauler 가 stockpile priority 에 따라 운반 → stockpile 도착 시점에만
                //  inventory(global counter) 가 증가한다 (PawnHauler.GoToStockpile).
                //
                //  과거 v4 fallback 은 sprite 가 null 이면 ResourceManager.AddWood 로
                //  즉시 카운터에 꽂아 넣었다 — 이게 운영자가 본 "즉시 운반(=즉시 적립)" 버그의
                //  핵심.  #213: WoodPileEntity.Spawn 이 sprite null 이면 코드 기본 sprite 를
                //  보장하므로 pile 은 항상 "나무 쓰러진 자리"(transform.position)에 눈에 보이게
                //  떨어진다.  hauler 가 줍어 운반(등짐 아이콘 + 부드러운 이동)해야 카운터에
                //  들어간다 = 운영자 멘탈 모델과 일치 (순간이동 아님).
                // #게임필 배치2(2026-06-10 자율) — 보상 순간: 쓰러짐이 들리고 산출이 보인다.
                //  이전엔 나무가 무음으로 사라짐 — FloatingText 는 B6 때 만들어 두고 배선이
                //  FOLLOW-UP 으로 방치돼 있던 것의 이행 (격차 분석 갈래3/갈래4 합치 항목).
                AudioBank.Instance?.PlayTreefall();
                // D3 몰입 — 쓰러짐 순간 큰 버스트: 나무칩 + 잎사귀 (2색 = 2버스트).
                ParticleFx.Burst(transform.position + Vector3.up * 0.3f,
                                 new Color(0.55f, 0.38f, 0.20f, 1f), count: 12, speed: 2.4f);
                ParticleFx.Burst(transform.position + Vector3.up * 0.6f,
                                 new Color(0.35f, 0.60f, 0.25f, 1f), count: 10, speed: 2.0f);
                FloatingText.Spawn(transform.position + Vector3.up * 0.4f,
                                   $"+{woodDrop} 목재", new Color(0.45f, 0.85f, 0.35f, 1f));
                WoodPileEntity.Spawn(transform.position, woodDrop, WoodPileSprite);
                // 소크 r3 채점 관측성 갭 #4 — ClearTask 만으론 완료/중단 구분 불가.
                Debug.Log($"[Chop] felled {name} +{woodDrop} wood @ ({transform.position.x:F1},{transform.position.y:F1})");
                // 아트 v2 후속 (2026-06-12) — 그루터기: 벌목 자리에 흔적이 남는다
                //  (장소의 역사 — '여기서 일했다'가 지형에 쓰인다).  ~1.5게임일 후 소멸,
                //  콜라이더 없음(통행/클릭 무영향).  에셋 없으면 조용히 생략.
                var stumpSpr = Resources.Load<Sprite>("flora32/flora32_stump");
                if (stumpSpr != null)
                {
                    var stump = new GameObject("Stump");
                    stump.transform.position = transform.position;
                    var ssr = stump.AddComponent<SpriteRenderer>();
                    ssr.sprite = stumpSpr;
                    ssr.sortingOrder = 2;   // 꽃/데칼 층 — 폰(10)/더미(7) 아래
                    Destroy(stump, 1500f);  // ≈1.5게임일 (스케일 시계)
                }
                // Day 12: enqueue a future sapling at this tree's position
                // BEFORE Destroy(gameObject) — `transform.position` is read
                // synchronously, the scheduler stashes a Vector3, so once
                // Destroy fires the queued entry stands alone.  Null-guard
                // the singleton per lesson #7 (poll, never subscribe).
                RegrowthScheduler.Instance?.EnqueueSapling(transform.position, saplingDelaySec);
                Destroy(gameObject);
                return true;
            }
            return false;
        }
    }
}
