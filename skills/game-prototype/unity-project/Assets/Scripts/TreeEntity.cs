using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// A choppable tree.  Day 3 = HP + on-chop damage + destroy + wood drop.
    /// </summary>
    /// <summary>#149 - 레퍼런스 콜로니심 wiki tree species (Oak 단단/yield 큼, Pine 빠름, Birch 중간).</summary>
    public enum TreeSpecies { Pine, Birch, Oak }

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
            _ => "나무",
        };

        // 종별 spec (HP, woodDrop, scale, tint)
        // 운영자 2026-06-02: 종별 스프라이트 추가(소나무만 → 자작·참나무).  sprite 가 색을
        //  가지므로 tint 는 흰색(런타임 HP-darken 의 base).  scale 로 크기 다양성 유지.
        public static readonly (float hp, int yield, float scale, Color tint)[] SpeciesStats = {
            (80f,  4, 0.95f, new Color(1f, 1f, 1f, 1f)),   // Pine  - 빠름 작음
            (100f, 5, 1.00f, new Color(1f, 1f, 1f, 1f)),   // Birch - 중간
            (150f, 7, 1.10f, new Color(1f, 1f, 1f, 1f)),   // Oak   - 단단 큼
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
                FloatingText.Spawn(transform.position + Vector3.up * 0.4f,
                                   $"+{woodDrop} 목재", new Color(0.45f, 0.85f, 0.35f, 1f));
                WoodPileEntity.Spawn(transform.position, woodDrop, WoodPileSprite);
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
