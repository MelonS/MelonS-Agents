using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 아트 v2 — 림 프레임 애니메이터 (운영자 2026-06-11 "림 애니메이션이 잘 보이게,
    /// 획기적인 시스템").  32px 시트(Resources/pawn32/pawn32_v{0..7}.png, 256x96)를
    /// 런타임 슬라이스해 3방향(S/E/N, W=E flip) × {idle, walk 4프레임, 작업스윙 2프레임}
    /// 을 상태기계로 재생한다.  기존 트랜스폼 트릭(스쿼시)을 실제 보행 사이클로 대체.
    ///
    /// 시트 레이아웃 (artv2-style-spec): cols = idle|walk1..4|work1..2|예약, rows = S|E|N (위→아래).
    ///
    /// 렌더 계약 (PawnSpriteBob 의 ROOT-TRANSFORM RULE 준수):
    ///   - 프레임은 ROOT SpriteRenderer.sprite 에 쓴다 — PawnSpriteBob 이 매 프레임
    ///     가시 자식으로 sprite+color 를 미러하므로 본 컴포넌트는 child 를 모른다.
    ///   - 좌우 방향(flipX)은 가시 자식 소유였던 PawnFacing 을 인계(disable)하고
    ///     직접 쓴다 — 4방향 facing 은 단일 출처여야 한다 (idle 시 마지막 facing 유지).
    ///   - 시트가 없으면(Resources 미존재) 조용히 비활성 — 기존 16px 외형 그대로.
    /// </summary>
    [DisallowMultipleComponent]
    public class PawnSpriteAnimator : MonoBehaviour
    {
        private const int COLS = 8, ROWS = 3, CELL = 32;
        private const int COL_IDLE = 0, COL_WALK0 = 1, COL_WORK0 = 5;
        private const int ROW_S = 0, ROW_E = 1, ROW_N = 2;   // 시트 위→아래

        // variant -> [row, col] 스프라이트 (전 림 공유 캐시)
        private static readonly Dictionary<int, Sprite[,]> sheetCache = new Dictionary<int, Sprite[,]>();

        private SpriteRenderer rootSr;        // 틴트/변형 앵커 (draw-off) — 여기에 프레임을 쓴다
        private SpriteRenderer childSr;       // 가시 자식 (flipX 만 직접)
        private PawnEntity entity;
        private PawnNeeds needs;
        private PawnMovement movement;
        private PawnChopper chopper;
        private PawnMiner miner;
        private PawnBuilder builder;
        private PawnHarvester harvester;

        private Sprite[,] frames;
        // 도구 합성 작업 프레임 캐시 — key: "{face}_{frame}_{tool}" (없으면 null=폴백)
        private static readonly Dictionary<string, Sprite> toolCache = new Dictionary<string, Sprite>();
        private int variantIdx;
        private int row = ROW_S;
        private bool flip;                    // W = E 프레임 flip (E 원화는 좌향)
        private float walkClock;
        private Vector3 prevPos;

        private void Awake()
        {
            rootSr = GetComponent<SpriteRenderer>();
            entity = GetComponent<PawnEntity>();
            needs = GetComponent<PawnNeeds>();
            movement = GetComponent<PawnMovement>();
            chopper = GetComponent<PawnChopper>();
            miner = GetComponent<PawnMiner>();
            builder = GetComponent<PawnBuilder>();
            harvester = GetComponent<PawnHarvester>();
            prevPos = transform.position;
        }

        private void Start()
        {
            // variant: GameManager 가 루트에 박은 기존 변형 sprite 이름(pawn_v{i})에서 파생.
            //  실패 시 이름 해시 — 어떤 경로든 0..7 로 수렴.
            int variant = 0;
            string sn = rootSr != null && rootSr.sprite != null ? rootSr.sprite.name : "";
            int vi = sn.LastIndexOf("_v");
            if (vi >= 0 && vi + 2 < sn.Length && char.IsDigit(sn[vi + 2]))
                variant = sn[vi + 2] - '0';
            else if (entity != null && !string.IsNullOrEmpty(entity.PawnName))
                variant = Mathf.Abs(entity.PawnName.GetHashCode()) % 8;

            frames = LoadSheet(variant);
            variantIdx = variant;
            if (frames == null) { enabled = false; return; }   // v2 자산 없음 → 기존 외형

            // 가시 자식 + facing 인계.
            var bob = GetComponentInChildren<PawnSpriteBob>();
            if (bob != null) childSr = bob.GetComponent<SpriteRenderer>();
            var facing = GetComponentInChildren<PawnFacing>();
            if (facing != null) facing.enabled = false;        // 4방향 단일 출처 = 본 컴포넌트
            var fx = GetComponent<MotionFx>();
            if (fx != null) fx.squashEnabled = false;          // 실제 보행 프레임이 대체 (먼지는 유지)

            rootSr.sprite = frames[ROW_S, COL_IDLE];
        }

        private static Sprite[,] LoadSheet(int variant)
        {
            if (sheetCache.TryGetValue(variant, out var cached)) return cached;
            var tex = Resources.Load<Texture2D>($"pawn32/pawn32_v{variant}");
            if (tex == null || tex.width < COLS * CELL || tex.height < ROWS * CELL)
            {
                sheetCache[variant] = null;
                return null;
            }
            var grid = new Sprite[ROWS, COLS];
            for (int r = 0; r < ROWS; r++)
            {
                for (int c = 0; c < COLS; c++)
                {
                    // row 0(S) 은 시트 최상단 — 텍스처 좌표는 아래가 0.
                    var rect = new Rect(c * CELL, tex.height - (r + 1) * CELL, CELL, CELL);
                    var spr = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 32f,
                                            0, SpriteMeshType.FullRect);
                    spr.name = $"pawn32_v{variant}_r{r}c{c}";
                    grid[r, c] = spr;
                }
            }
            sheetCache[variant] = grid;
            return grid;
        }

        private static Sprite LoadToolFrame(int variant, int rowIdx, int workNo, string tool)
        {
            string face = rowIdx == ROW_S ? "S" : rowIdx == ROW_E ? "E" : "N";
            string key = $"pawn32_v{variant}_{face}_work{workNo}_{tool}";
            if (toolCache.TryGetValue(key, out var hit)) return hit;
            var spr = Resources.Load<Sprite>($"pawn32tool/{key}");
            toolCache[key] = spr;   // null 도 캐시 (반복 Load 방지)
            return spr;
        }

        private void LateUpdate()
        {
            if (frames == null || rootSr == null) return;
            if (entity != null && entity.IsDead) return;       // 시체 연출은 기존 경로 소유

            float dt = Time.deltaTime;
            Vector3 pos = transform.position;
            Vector2 delta = pos - prevPos;
            prevPos = pos;
            float speed = dt > 0.0001f ? delta.magnitude / dt : 0f;

            int col;
            bool sleeping = needs != null && needs.IsSleeping;
            bool working = !sleeping && speed <= 0.15f
                && ((chopper != null && chopper.HasTask)
                 || (miner != null && miner.HasTask)
                 || (builder != null && builder.HasTask)
                 || (harvester != null && harvester.HasTask));

            if (!sleeping && speed > 0.15f)
            {
                // 방향: 지배 축.  좌우는 E 행 + flip (원화 E 는 좌향 → 우향 이동 = flip).
                if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                {
                    row = ROW_E;
                    flip = delta.x > 0.0005f ? true : (delta.x < -0.0005f ? false : flip);
                }
                else row = delta.y > 0f ? ROW_N : ROW_S;

                // 보행 사이클 — 속도 비례 (4x 배속에서도 발걸음이 따라온다).
                walkClock += dt * Mathf.Clamp(speed, 0.8f, 2.6f) * 5.5f;
                col = COL_WALK0 + ((int)walkClock & 3);
            }
            else if (working)
            {
                // 작업 스윙 2프레임 @ 2.5Hz — 도끼질/곡괭이질 리듬.
                col = COL_WORK0 + ((int)(Time.time * 2.5f) & 1);
                // 아트 v2 후속 (2026-06-12) — 손에 도구가 보인다: 벌목=도끼, 채광=곡괭이.
                //  합성 프레임(Resources/pawn32tool)이 있으면 그걸로 교체, 없으면 맨손 폴백.
                string tool = (chopper != null && chopper.HasTask) ? "axe"
                            : (miner != null && miner.HasTask) ? "pick" : null;
                if (tool != null)
                {
                    var ts = LoadToolFrame(variantIdx, row, col - COL_WORK0 + 1, tool);
                    if (ts != null)
                    {
                        rootSr.sprite = ts;
                        if (childSr != null) childSr.flipX = row == ROW_E && flip;
                        return;
                    }
                }
            }
            else
            {
                walkClock = 0f;
                col = COL_IDLE;                                 // 마지막 facing 의 idle 유지
                if (sleeping) row = ROW_S;                      // 수면은 정면 (SleepPose 가 연출)
            }

            rootSr.sprite = frames[row, col];
            if (childSr != null) childSr.flipX = row == ROW_E && flip;
        }
    }
}
