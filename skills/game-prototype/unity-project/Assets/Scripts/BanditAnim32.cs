using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 아트 v2 잔여 — 적대 인간형 전용 32px (강도가 '빨간 틴트 콜로니스트'였던 것).
    /// Resources/pawn32/pawn32_hostile_{a|b}.png (256x96, 콜로니스트 시트와 동일
    /// 레이아웃: cols idle|walk1..4|work1..2|예약 × rows S|E|N)를 런타임 슬라이스.
    /// 다크 의상 + 빨간 반다나 + 다크레드 외곽선 — 아군과 0.5초 구분.
    ///
    /// PawnSpriteAnimator 의 경량 사촌: 강도는 루트 SpriteRenderer 단일(자식/Bob 없음),
    /// 수면/작업 컴포넌트도 없으므로 상태는 {idle, walk, 공격 스윙} 3개뿐.
    ///   - 공격 스윙: BanditEnemy.LastAttackTime 후 0.7s 동안 work 2프레임 2.5Hz.
    ///   - 시트가 있으면 스폰 틴트(빨강)를 white 로 인계 — 적대 신호는 원화가 담당.
    ///     (인계 없이 두면 다크 의상 위 빨간 틴트가 원화를 뭉갠다.)
    ///   - 시트 없으면 조용히 비활성 — 기존 틴트 외형 폴백 (v2 자산 없는 환경 안전).
    /// </summary>
    [DisallowMultipleComponent]
    public class BanditAnim32 : MonoBehaviour
    {
        private const int COLS = 8, ROWS = 3, CELL = 32;
        private const int COL_IDLE = 0, COL_WALK0 = 1, COL_WORK0 = 5;
        private const int ROW_S = 0, ROW_E = 1, ROW_N = 2;
        private const float SwingHoldSec = 0.7f;

        private static readonly Dictionary<string, Sprite[,]> sheetCache =
            new Dictionary<string, Sprite[,]>();

        private SpriteRenderer sr;
        private BanditEnemy bandit;
        private Sprite[,] frames;
        private int row = ROW_S;
        private bool flip;
        private float walkClock;
        private Vector3 prevPos;

        private void Start()
        {
            // a/b 변형: 이름 해시 — 같은 wave 인덱스는 런 내 일관, rng 체인 비소비.
            string variant = (Mathf.Abs(gameObject.name.GetHashCode()) & 1) == 0 ? "a" : "b";
            frames = LoadSheet(variant);
            if (frames == null) { enabled = false; return; }

            sr = GetComponent<SpriteRenderer>();
            bandit = GetComponent<BanditEnemy>();
            var fx = GetComponent<MotionFx>();
            if (fx != null) { fx.squashEnabled = false; fx.flipEnabled = false; }
            bandit?.OverrideBaseColor(Color.white);   // 틴트 인계 — 원화가 적대 신호
            if (sr != null) sr.sprite = frames[ROW_S, COL_IDLE];
            prevPos = transform.position;
        }

        private static Sprite[,] LoadSheet(string variant)
        {
            if (sheetCache.TryGetValue(variant, out var cached)) return cached;
            var tex = Resources.Load<Texture2D>($"pawn32/pawn32_hostile_{variant}");
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
                    var rect = new Rect(c * CELL, tex.height - (r + 1) * CELL, CELL, CELL);
                    var spr = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 32f,
                                            0, SpriteMeshType.FullRect);
                    spr.name = $"pawn32_hostile_{variant}_r{r}c{c}";
                    grid[r, c] = spr;
                }
            }
            sheetCache[variant] = grid;
            return grid;
        }

        private void LateUpdate()
        {
            if (frames == null || sr == null) return;
            if (bandit != null && bandit.IsDead) return;

            float dt = Time.deltaTime;
            Vector3 pos = transform.position;
            Vector2 delta = pos - prevPos;
            prevPos = pos;
            float speed = dt > 0.0001f ? delta.magnitude / dt : 0f;

            bool swinging = bandit != null && Time.time - bandit.LastAttackTime < SwingHoldSec;

            int col;
            if (speed > 0.15f)
            {
                if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                {
                    row = ROW_E;
                    flip = delta.x > 0.0005f ? true : (delta.x < -0.0005f ? false : flip);
                }
                else row = delta.y > 0f ? ROW_N : ROW_S;

                walkClock += dt * Mathf.Clamp(speed, 0.8f, 2.6f) * 5.5f;
                col = COL_WALK0 + ((int)walkClock & 3);
            }
            else if (swinging)
            {
                col = COL_WORK0 + ((int)(Time.time * 2.5f) & 1);   // 클럽 스윙
            }
            else
            {
                walkClock = 0f;
                col = COL_IDLE;
            }

            sr.sprite = frames[row, col];
            sr.flipX = row == ROW_E && flip;
        }
    }
}
