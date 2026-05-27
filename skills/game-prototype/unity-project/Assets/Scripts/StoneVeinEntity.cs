using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// #119 - 림월드 광맥 (stone vein).
    ///  채광 시 stone chunk drop (1-3 개).  HP 200 (나무 100 보다 더 단단).
    ///  PawnMiner 가 채광.  HaulStoneAction 으로 chunk 운반.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class StoneVeinEntity : MonoBehaviour
    {
        [SerializeField] private float maxHp = 200f;
        [SerializeField] private int stoneYieldMin = 1;
        [SerializeField] private int stoneYieldMax = 3;

        private float hp;
        private SpriteRenderer sr;

        // chunk drop sprite (GameManager 에서 박음)
        public static Sprite StoneChunkSprite;

        public bool IsDestroyed => hp <= 0f;

        private void Awake()
        {
            hp = maxHp;
            sr = GetComponent<SpriteRenderer>();
        }

        private float lastSfxTime = -10f;
        private const float SfxInterval = 0.6f;

        /// <summary>채광 데미지. true 반환 = 광맥 소진.</summary>
        public bool TakeMineDamage(float dmg)
        {
            if (IsDestroyed) return false;
            hp -= dmg;
            if (sr != null)
            {
                float t = Mathf.Clamp01(hp / maxHp);
                sr.color = new Color(0.4f + 0.6f * t, 0.4f + 0.6f * t, 0.4f + 0.6f * t, 1f);
            }
            if (Time.time - lastSfxTime >= SfxInterval)
            {
                AudioBank.Instance?.PlayChop();  // 채광 SFX 도 chop 재활용
                lastSfxTime = Time.time;
            }
            if (hp <= 0f)
            {
                int yieldN = Random.Range(stoneYieldMin, stoneYieldMax + 1);
                if (StoneChunkSprite != null)
                {
                    // chunks 를 광맥 주변에 spawn
                    for (int i = 0; i < yieldN; i++)
                    {
                        Vector3 off = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0);
                        StoneChunkEntity.Spawn(transform.position + off, 1, StoneChunkSprite);
                    }
                }
                else
                {
                    ResourceManager.Instance?.AddStone(yieldN);
                }
                Destroy(gameObject);
                return true;
            }
            return false;
        }
    }
}
