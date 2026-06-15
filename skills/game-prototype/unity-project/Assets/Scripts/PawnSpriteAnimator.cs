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
    /// 시트 레이아웃 (2026-06-15): cols = idle|walk1..6|work1..2 (9칼럼, 288x96), rows = S|E|N.
    ///  보행 6프레임 = 접촉→half-lift 전환→패싱 ×2 (이전 4프레임 march-y 완화).
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
        private const int COLS = 9, ROWS = 3, CELL = 32;   // 2026-06-15: 8→9 (보행 4→6프레임)
        private const int COL_IDLE = 0, COL_WALK0 = 1, COL_WORK0 = 7;   // idle | walk1..6 | work1..2
        private const int WALK_FRAMES = 6;
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
        private PawnUtilityAI util;           // 작업 타깃 단일 출처 (8개 워커 우선순위 집계)
        private int variantIdx;
        private int row = ROW_S;
        private bool flip;                    // W = E 프레임 flip (E 원화는 좌향)
        private float walkClock;
        private float workClock;              // per-pawn 작업 스윙 클록 (전역 Time.time 대체)
        private float swingPhase;             // per-pawn 스윙 위상 오프셋 (전 림 동시 스윙 robotic 방지)
        private Vector2 velSmooth;            // 저역통과 속도 — facing 플립플롭·임계 깜빡임 제거
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
            util = GetComponent<PawnUtilityAI>();
            // 인스턴스별 스윙 위상 — 같은 변형이라도 림마다 도끼질 박자가 어긋나 자연스럽게.
            swingPhase = (Mathf.Abs(GetInstanceID()) % 1000) / 1000f * 0.4f;
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
            // 저역통과 속도: 단일 프레임 delta 는 ~1000fps + 서브픽셀 + A* 경로 보정으로
            //  부호가 떨려 facing 이 플립플롭한다.  속도 벡터를 ~0.08s 시정수로 평활해
            //  거기서 speed(임계 깜빡임 방지)와 dir(방향 떨림 방지)을 함께 얻는다.
            Vector2 instVel = dt > 0.0001f ? (Vector2)(pos - prevPos) / dt : Vector2.zero;
            prevPos = pos;
            velSmooth = Vector2.Lerp(velSmooth, instVel, 1f - Mathf.Exp(-12f * dt));
            float speed = velSmooth.magnitude;
            Vector2 dir = velSmooth;

            int col;
            bool sleeping = needs != null && needs.IsSleeping;
            bool moving = !sleeping && speed > 0.15f;
            // 작업 판정·페이싱 단일 출처(PawnUtilityAI.TryGetWorkTargetPos) — 8개 워커
            //  (건설/벌목/채광/수확/채집/사냥/요리/치료) 우선순위 집계.  이전엔 작업 스윙
            //  facing 이 chopper/miner 만 타깃을 봐 builder/harvester 가 작업물을 등졌다.
            //  수면 누움 포즈는 PawnPoseDriver(자식 회전 단독 소유자)가 처리.
            Vector3 workTargetPos = default;
            bool hasWorkTarget = !sleeping && util != null && util.TryGetWorkTargetPos(out workTargetPos);
            bool working = !moving && hasWorkTarget;

            if (moving)
            {
                // 방향: 지배 축.  좌우는 E 행 + flip (원화 E 는 좌향 → 우향 이동 = flip).
                if (Mathf.Abs(dir.x) >= Mathf.Abs(dir.y))
                {
                    row = ROW_E;
                    // E 원화는 동쪽(우향)을 본다(눈 x18, 작업 팔 x20-21).  flipX 는 서향 미러
                    //  이므로 좌(서)향 이동에서만 flip — 이전 규약(우향=flip)이 반대라 림이
                    //  이동/작업 대상을 등졌다(운영자 "나무 등지고 캠").
                    flip = dir.x < -0.01f ? true : (dir.x > 0.01f ? false : flip);
                }
                else row = dir.y > 0f ? ROW_N : ROW_S;

                // 보행 사이클 — 속도 비례 (4x 배속에서도 발걸음이 따라온다).
                //  6프레임(접촉→half→패싱 ×2)으로 march-y 스터터 완화.
                walkClock += dt * Mathf.Clamp(speed, 0.8f, 2.6f) * 5.5f;
                col = COL_WALK0 + ((int)walkClock % WALK_FRAMES);
            }
            else if (working)
            {
                // 작업 대상 방향으로 facing (레퍼런스 문법 — 일하는 림은 작업물을 본다).
                //  단일 출처라 모든 작업 동작이 대상을 바라본다.
                Vector2 wd = (Vector2)workTargetPos - (Vector2)pos;
                // 대상이 좌(서)에 있을 때만 flip(미러) — E 원화가 동쪽을 보므로.  이전 wd.x>0
                //  은 반대라 동쪽 나무를 등지고 도끼질했다.
                if (Mathf.Abs(wd.x) >= Mathf.Abs(wd.y)) { row = ROW_E; flip = wd.x < 0f; }
                else row = wd.y > 0f ? ROW_N : ROW_S;

                // 작업 스윙 2프레임 — per-pawn 클록 + 위상오프셋.  전역 Time.time 을 쓰면
                //  정지 시 스윙이 desync 되고 전 림이 동시에 도끼질해 robotic 했다.
                workClock += dt;
                col = COL_WORK0 + ((int)((workClock + swingPhase) * 2.5f) & 1);
                // 손 도구 합성: 벌목=도끼, 채광=곡괭이 (그 외 작업은 맨손).  없으면 폴백.
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
                // idle — walkClock 을 0 으로 리셋하지 않는다(다음 보행이 0프레임부터
                //  시작하는 '재시작 스냅' 제거).  증가만 멈추고 위상 유지.
                col = COL_IDLE;                                 // 마지막 facing 의 idle 유지
                if (sleeping) row = ROW_S;                      // 수면은 정면 (SleepPose 가 연출)
            }

            rootSr.sprite = frames[row, col];
            if (childSr != null) childSr.flipX = row == ROW_E && flip;
        }
    }
}
