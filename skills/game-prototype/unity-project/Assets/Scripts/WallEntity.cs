using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>#150 - 자재별 벽 HP (wiki: wood 100, stone 280, steel 300).</summary>
    public enum WallMaterial { Wood, Stone, Steel }

    /// <summary>Day 17: a built wall.  #150 - 자재별 HP + tint.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class WallEntity : MonoBehaviour
    {
        [SerializeField] private WallMaterial material = WallMaterial.Wood;
        [SerializeField] private float maxHp = 100f;
        private float hp;

        public WallMaterial Material => material;
        public float Hp => hp;
        public float MaxHp => maxHp;
        public string MaterialKr => material switch
        {
            WallMaterial.Wood => "목재 벽",
            WallMaterial.Stone => "석재 벽",
            WallMaterial.Steel => "철강 벽",
            _ => "벽",
        };

        // wiki spec - wood 100 / stone 280 / steel 300
        public static readonly (float hp, Color tint)[] MaterialStats = {
            (100f, new Color(1.00f, 1.00f, 1.00f, 1f)),
            (280f, new Color(0.78f, 0.78f, 0.80f, 1f)),
            (300f, new Color(0.55f, 0.60f, 0.70f, 1f)),
        };

        public bool ProvidesCover => true;

        public void SetMaterial(WallMaterial m)
        {
            material = m;
            var (h, tint) = MaterialStats[(int)m];
            maxHp = h;
            hp = h;
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = tint;
        }

        private void Awake()
        {
            if (hp <= 0f) hp = maxHp;
        }

        // #199 B3 — wall→grid: this wall blocks its cell for pathfinding.
        //  Registered in Start (NOT Awake) so the PathGrid built in
        //  TilemapStaticRefInit.Awake is guaranteed to exist (all Awakes run
        //  before any Start).  Runtime-spawned walls (blueprint complete) register
        //  the frame after Instantiate — same Start path, no special-casing.
        //  We cache the cell we registered so OnDestroy clears the SAME cell even
        //  if the transform ever moved (it won't, but this keeps the ref-count
        //  balanced — never double-clears, never leaks a blocked cell).
        private bool _cellRegistered;
        private Vector2Int _registeredCell;

        private void Start()
        {
            _registeredCell = AI.PathGrid.WorldToCell(transform.position);
            PawnMovement.RegisterWallCell(transform.position);
            _cellRegistered = true;
        }

        private void OnDestroy()
        {
            // RimWorld: a destroyed wall reopens its cell.  Unregister so pawns
            //  immediately path through the gap (PathGrid bumps Version → in-flight
            //  pawns re-path).  Guard: only clear if we actually registered, and
            //  clear the cached cell so the ref-count stays balanced.
            if (!_cellRegistered) return;
            if (PawnMovement.Grid != null)
                PawnMovement.Grid.SetStructureBlocked(_registeredCell, false);
            _cellRegistered = false;
        }

        public void TakeDamage(float dmg)
        {
            hp -= dmg;
            // #158 - 시각 피드백: HP 비율 × material tint (#156 lesson - hue 보존).
            //  #167 - TintHelper 로 통합.
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && maxHp > 0f)
            {
                var (_, baseTint) = MaterialStats[(int)material];
                TintHelper.ApplyHpBrightness(sr, baseTint, hp / maxHp, minBright: 0.4f);
            }
            if (hp <= 0f) Destroy(gameObject);
        }
    }
}
