using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    // (벽 연결 시각화 구현은 partial 없이 이 한 파일 안에 모두 둔다 — 배정 범위.)

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
            // #201 — the reference sim eject-on-block.  This wall now makes its cell
            //  impassable.  Any pawn standing in that cell (idle, mid-walk, drafted)
            //  would be sealed inside a solid structure — AStar refuses a blocked
            //  START cell so it could never path out, and an idle pawn never even
            //  re-paths.  Push every such pawn out to the nearest open cell, exactly
            //  like the reference sim pushes a pawn off a freshly-built wall.  Called AFTER
            //  RegisterWallCell so the cell already reads as blocked → the ejected
            //  pawn lands on a genuinely-open neighbour, not back under the wall.
            PawnMovement.EjectPawnsFromCell(transform.position);

            // #260 시각 연결감 — the reference sim 벽처럼 인접 벽과 '이어져' 보이게.
            //  pathing/collider/HP 와 완전히 독립된 순수 시각 레이어.
            //  내 이음새 갱신 + 새로 생긴 벽 때문에 이웃의 이음새도 갱신.
            RefreshSeams();
            NotifyNeighbours();
        }

        private void OnDestroy()
        {
            // #260 내가 사라지면 인접 벽들의 이음새도 다시 계산되어야 틈이 메워진/
            //  벌어진 상태가 정확히 반영된다.  (OnDestroy 는 씬 종료 시에도 불리므로
            //  이웃이 이미 파괴 중일 수 있으나 NotifyNeighbours 는 null/파괴 안전.)
            NotifyNeighbours();

            // the reference sim: a destroyed wall reopens its cell.  Unregister so pawns
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

        // ===================================================================
        // #260 벽 연결 시각화 (autotile 경량판) — 순수 시각, 무영향 firewall
        // ===================================================================
        //
        //  문제: 벽 여러 개가 나란히 있어도 각각 독립 사각형으로 보여 레고블록처럼
        //        따로 논다.  the reference sim 는 이웃 방향에 따라 벽이 이어져 보인다.
        //
        //  접근(저위험): 완전 16조각 autotile 대신, 인접 4방향(상하좌우)에 벽이
        //        있으면 그 방향으로 셀 경계를 살짝 넘는 얇은 '이음새(seam)' 자식
        //        SpriteRenderer 를 켜서 두 벽 사이의 틈을 메운다.  부모 sprite/
        //        scale/collider/HP/SetMaterial/pathing 은 일절 건드리지 않는다.
        //
        //  안전성:
        //   - 인접 검사는 RoofDesignation.IsWallCell 과 동일한 OverlapBox 패턴.
        //   - Start(벽 생성) / 이웃 변경(빌드·파괴) 시 1회만 갱신 → per-frame 아님.
        //   - 자식 SpriteRenderer 는 collider 없음 → 물리/pathing 영향 0.
        //   - 헤드리스 안전: SpriteRenderer/sprite 없으면 조용히 return.
        //   - 버그패턴 회피: 타이머/코루틴/싱글톤 구독/spawn-grace 없음.
        //     OverlapBox 는 GetComponent 비교(GetInstanceID 레이스 #5 무관).

        // 4방향 셀 오프셋 (오른·왼·위·아래) + 각 방향 seam 의 위치/크기 계산용.
        private static readonly Vector2[] _dirs =
        {
            Vector2.right, Vector2.left, Vector2.up, Vector2.down,
        };

        // 셀 경계 너머로 seam 이 넘어가는 정도(월드 단위).  1셀=1유닛 기준 0.5 면
        //  정확히 이웃 셀 중앙까지.  두 벽이 각자 0.5 씩 뻗어 정확히 맞물린다.
        //  (실제 seam 은 부모 sprite 의 ~30% 두께 + 0.5 길이로 틈만 메운다.)
        private const float SEAM_REACH = 0.5f;   // 이웃 셀 중앙까지
        private const float SEAM_THICK = 0.34f;  // 벽 통로 폭 대비 이음새 두께
        private const float SEAM_OVERLAP = 0.04f; // 부모와 살짝 겹쳐 틈 0 보장

        // 4개의 seam SpriteRenderer (없으면 lazy 생성, 방향별로 on/off 만 토글).
        private SpriteRenderer[] _seams;

        /// <summary>이음새 자식들을 (없으면) 생성.  부모 sprite 를 재사용하되
        /// 가운데를 잘라 쓰지 않고 단색 사각형처럼 보이도록 부모 tint 를 입힌다.</summary>
        private void EnsureSeams()
        {
            if (_seams != null) return;
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;  // 헤드리스/스프라이트 없음

            _seams = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("WallSeam_" + i);
                go.transform.SetParent(transform, worldPositionStays: false);
                var ssr = go.AddComponent<SpriteRenderer>();
                ssr.sprite = sr.sprite;             // 같은 텍스처 재사용 (메모리/배칭 ok)
                ssr.sortingLayerID = sr.sortingLayerID;
                ssr.sortingOrder = sr.sortingOrder - 1;  // 부모 벽 본체 바로 뒤에 깔림
                ssr.enabled = false;                // 기본 off, 연결 시에만 on
                _seams[i] = ssr;
            }
        }

        /// <summary>인접 4방향 벽 유무를 검사해 각 방향 이음새를 on/off + 위치·크기 갱신.
        /// BlueprintEntity 가 부모 scale 을 footprint/spriteSize 로 맞춰두므로, 자식
        /// seam 의 localScale·localPos 는 부모 lossyScale·sprite bounds 로 보정해
        /// '월드에서 원하는 크기·위치'가 나오게 한다 (부모 비균일 scale 도 정확히 보정).
        /// Start(생성)·이웃 변경 시에만 호출 — per-frame 아님.</summary>
        private void RefreshSeams()
        {
            EnsureSeams();
            if (_seams == null) return;

            var sr = GetComponent<SpriteRenderer>();
            Vector3 ps = transform.lossyScale;
            // 0 division 방지 (헤드리스/축소 0 케이스).
            float sx = Mathf.Approximately(ps.x, 0f) ? 1f : ps.x;
            float sy = Mathf.Approximately(ps.y, 0f) ? 1f : ps.y;

            // 부모 sprite 의 PPU 1유닛 크기 — seam 의 월드 크기를 sprite bounds 기준으로
            //  환산하기 위함.  sprite.bounds.size 는 부모 scale 적용 전 1유닛 sprite 크기.
            Vector2 sb = (sr != null && sr.sprite != null) ? (Vector2)sr.sprite.bounds.size : Vector2.one;
            float sbx = sb.x < 0.01f ? 1f : sb.x;
            float sby = sb.y < 0.01f ? 1f : sb.y;

            for (int i = 0; i < 4; i++)
            {
                var ssr = _seams[i];
                if (ssr == null) continue;

                bool connected = HasWallNeighbour(_dirs[i]);
                ssr.enabled = connected;
                if (!connected) continue;

                // 부모와 동일 tint (HP darken 포함) — 색 불일치로 이음새가 도드라지지 않게.
                if (sr != null) ssr.color = sr.color;

                Vector2 dir = _dirs[i];
                bool horizontal = !Mathf.Approximately(dir.x, 0f);

                // 월드 기준으로 원하는 seam 크기(유닛):
                //  - 연결 축: 셀 경계에서 이웃 중앙까지 (SEAM_REACH) + 부모와 겹침
                //  - 수직 축: 통로 두께 (SEAM_THICK)
                float wLen = SEAM_REACH + SEAM_OVERLAP;       // 연결 방향 길이
                float wThk = SEAM_THICK;                      // 두께
                float wWorldX = horizontal ? wLen : wThk;
                float wWorldY = horizontal ? wThk : wLen;

                // 월드 길이 → 자식 localScale.
                //  자식 worldSize = childLocalScale * parentLossyScale * spriteBounds(1유닛)
                //  ⇒ childLocalScale = worldSize / (parentLossy * spriteBounds)
                ssr.transform.localScale = new Vector3(
                    wWorldX / (sx * sbx),
                    wWorldY / (sy * sby),
                    1f);

                // 위치: 부모 중심에서 연결 방향으로 이동해 seam 안쪽 끝이 부모 본체와
                //  맞닿고(overlap 만큼 겹침) 바깥 끝이 이웃 셀 중앙(거리 1.0)에 닿게 한다.
                //  seam 중심의 월드 이동량 = 경계(0.5) + 길이의 절반 − 겹침.
                //  localPos = 월드이동 / 부모lossyScale (자식은 부모 좌표계).
                float worldShift = 0.5f + (wLen * 0.5f) - SEAM_OVERLAP;
                Vector3 localPos = new Vector3(
                    dir.x * worldShift / sx,
                    dir.y * worldShift / sy,
                    0f);
                ssr.transform.localPosition = localPos;
            }
        }

        /// <summary>지정 방향 인접 셀에 벽(또는 문)이 있는지 OverlapBox 로 검사.
        /// RoofDesignation.IsWallCell 과 동일 패턴.  헤드리스에서도 안전(히트 0).</summary>
        private bool HasWallNeighbour(Vector2 dir)
        {
            Vector2 nc = (Vector2)transform.position + dir;     // 이웃 셀 중앙
            var hits = Physics2D.OverlapBoxAll(nc, Vector2.one * 0.45f, 0f);
            for (int h = 0; h < hits.Length; h++)
            {
                var col = hits[h];
                if (col == null) continue;
                var go = col.gameObject;
                if (go == gameObject) continue;                 // 나 자신 제외
                // 문도 벽선을 잇는 구조물 → 시각적으로 연결 (the reference sim 동일).
                if (go.GetComponent<WallEntity>() != null) return true;
                if (go.GetComponent<DoorEntity>() != null) return true;
            }
            return false;
        }

        /// <summary>내가 생기거나 사라질 때, 인접 4방향 벽에게 이음새 재계산을 알린다.
        /// (이웃은 자기 기준으로 다시 OverlapBox 검사 → 양방향 이음새가 맞물린다.)
        /// per-frame 아님: 빌드/파괴 이벤트 시 1회.</summary>
        private void NotifyNeighbours()
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 nc = (Vector2)transform.position + _dirs[i];
                var hits = Physics2D.OverlapBoxAll(nc, Vector2.one * 0.45f, 0f);
                for (int h = 0; h < hits.Length; h++)
                {
                    var col = hits[h];
                    if (col == null) continue;
                    var go = col.gameObject;
                    if (go == gameObject) continue;
                    var w = go.GetComponent<WallEntity>();
                    if (w != null) w.RefreshSeams();
                }
            }
        }
    }
}
