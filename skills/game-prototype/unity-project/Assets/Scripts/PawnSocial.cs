using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// 사회 상호작용 최소형 — "잡담" (G1).
    ///
    /// 정본 스펙: `docs/design-social-2026-07-24.md` §G1.  그 문서는 2026-07-24 에
    /// 작성돼 **운영자 픽 대기** 상태로 멈춰 있었고, 그 사이 `gameplay-audit` 의
    /// 첫인상 크리티컬 1위("림들이 서로를 모른다")와 제출 문서의 "최대 결손"으로
    /// 계속 기록만 되고 있었다.  2026-07-30 운영자 승인("둘다")으로 착수.
    ///
    /// 스코프는 **관계 시뮬레이션이 아니라 '살아있음의 증거' 1개**다.
    /// 10분 관찰에서 "이 사람들이 서로를 인식한다"는 신호가 화면에 나오면 목적 달성.
    /// 풀 관계망·연애·다툼은 스코프 밖 — 넓히면 밸런스·세이브·UI가 전부 딸려온다.
    ///
    /// 동작 (스펙 그대로):
    ///   트리거  두 림 거리 ≤2칸 · 둘 다 수면/징집/붕괴/다운 아님 · pair 쿨다운 경과
    ///   연출    말풍선 스프라이트 2.5초 (폰트 무의존 — 게이지바 전례)
    ///   효과    양쪽에 thought "즐거운 대화" +2 (0.4게임일)
    ///   호감도  pair 당 −100~100, 잡담 1회 +2
    ///   사교성  쿨다운에 socialMul 역수 (사교적인 림이 더 자주)
    ///
    /// 구현 노트(스펙): **신규 Update 루프를 만들지 않는다** — 스캔은 1.5초 주기로
    /// 자체 타이머를 두되 매 프레임 FindObjects 를 돌지 않는다(프로젝트 규약).
    /// 예약도 쓰지 않는다 — 잡담은 배타 자원이 아니고 pair 쿨다운만 있으면 된다.
    /// </summary>
    public class PawnSocial : MonoBehaviour
    {
        // ── 수치 (스펙 표 그대로) ──────────────────────────────────────────
        //  2026-07-30 실측: 2.0 칸으로는 1 게임일 동안 잡담 0건이었다.  3인이 각자
        //  다른 일을 하러 흩어져 '동시에 2칸 안'인 순간이 사실상 없다.  스펙의 2칸은
        //  모닥불 모임 반경과 결을 맞춘 값이었는데, 실제로는 모임이 짧고 작업 반경이 넓다.
        //  3.2 칸 = 화면에서 여전히 '바로 옆' 이면서 스쳐 지나가는 순간을 잡는다.
        private const float ChatRange = 3.2f;
        private const float PairCooldownSec = 250f;    // 0.25 게임일
        private const float ScanInterval = 1.5f;       // Decide 주기에 맞춤
        private const int OpinionPerChat = 2;
        private const int OpinionMin = -100, OpinionMax = 100;
        private const float BubbleSec = 2.5f;

        /// <summary>이름 → 호감도.  이름을 키로 쓰는 이유: 세이브/로드와 UI 가 전부
        /// 이름 기준이고, 인스턴스 ID 는 로드 후 바뀐다.</summary>
        public readonly Dictionary<string, int> opinion = new Dictionary<string, int>();

        private readonly Dictionary<string, float> lastChat = new Dictionary<string, float>();
        private PawnEntity entity;
        private PawnNeeds needs;
        private PawnAbilities abilities;
        private float nextScan;
        private GameObject bubble;
        private float bubbleUntil = -1f;
        private static bool _bootLogged;

        private void Awake()
        {
            entity = GetComponent<PawnEntity>();
            needs = GetComponent<PawnNeeds>();
            abilities = GetComponent<PawnAbilities>();
            // 스캔 시작을 림마다 흩어 놓는다 — 안 그러면 전원이 같은 프레임에
            //  FindObjects 를 돌아 주기적 스파이크가 생긴다.
            nextScan = Time.time + Random.Range(0f, ScanInterval);
            if (!_bootLogged) { _bootLogged = true; Debug.Log("[Social] PawnSocial 부착 확인"); }
        }

        /// <summary>가장 친한 동료 (인스펙터 표시용).  없으면 null.</summary>
        public bool TryGetBestFriend(out string name, out int score)
        {
            name = null; score = 0;
            foreach (var kv in opinion)
                if (name == null || kv.Value > score) { name = kv.Key; score = kv.Value; }
            return name != null;
        }

        private bool Available()
        {
            if (entity == null || entity.IsDead) return false;
            if (entity.IsDrafted) return false;                    // 징집 중엔 잡담 금지
            if (needs == null) return true;
            if (needs.IsSleeping) return false;
            if (needs.IsBreaking) return false;                    // 붕괴 연출 보호
            return true;
        }

        private void Update()
        {
            if (bubbleUntil > 0f && Time.time >= bubbleUntil) HideBubble();
            if (Time.time < nextScan) return;
            nextScan = Time.time + ScanInterval;
            if (!Available()) return;

            float socialMul = abilities != null ? Mathf.Max(0.1f, abilities.socialMul) : 1f;
            float cooldown = PairCooldownSec / socialMul;

            foreach (var other in Object.FindObjectsByType<PawnSocial>(FindObjectsSortMode.None))
            {
                if (other == null || other == this) continue;
                if (!other.Available()) continue;
                if (((Vector2)(other.transform.position - transform.position)).sqrMagnitude
                    > ChatRange * ChatRange) continue;

                string myName = entity != null ? entity.PawnName : name;
                string otherName = other.entity != null ? other.entity.PawnName : other.name;
                if (string.IsNullOrEmpty(myName) || string.IsNullOrEmpty(otherName)) continue;

                // pair 쿨다운 — 양쪽 중 하나라도 아직이면 건너뛴다.
                //  (한쪽만 보면 사교적인 림이 상대를 계속 붙잡는다.)
                if (lastChat.TryGetValue(otherName, out float t0) && Time.time - t0 < cooldown) continue;
                if (other.lastChat.TryGetValue(myName, out float t1)
                    && Time.time - t1 < PairCooldownSec / Mathf.Max(0.1f,
                        other.abilities != null ? other.abilities.socialMul : 1f)) continue;

                Chat(other, myName, otherName);
                return;   // 한 주기에 한 번만 — 여러 명 사이에서 연쇄 발화 방지
            }
        }

        private void Chat(PawnSocial other, string myName, string otherName)
        {
            float now = Time.time;
            lastChat[otherName] = now;
            other.lastChat[myName] = now;

            Bump(opinion, otherName);
            Bump(other.opinion, myName);

            GetComponent<PawnThoughts>()?.AddThought("즐거운 대화");
            other.GetComponent<PawnThoughts>()?.AddThought("즐거운 대화");

            ShowBubble();
            other.ShowBubble();

            Debug.Log($"[Social] {myName} ↔ {otherName} 잡담 "
                      + $"(호감도 {opinion[otherName]}/{other.opinion[myName]})");
        }

        private static void Bump(Dictionary<string, int> d, string key)
        {
            d.TryGetValue(key, out int v);
            d[key] = Mathf.Clamp(v + OpinionPerChat, OpinionMin, OpinionMax);
        }

        // ── 말풍선 (폰트 무의존 스프라이트 — 게이지바 패턴 재사용) ──────────
        private static Sprite _bubbleSprite;
        private static Sprite BubbleSprite()
        {
            if (_bubbleSprite != null) return _bubbleSprite;
            // 12x10 말풍선: 둥근 사각 + 꼬리 + 점 3개.  팩 아웃라인(22,28,46) 규약을 따른다.
            const int W = 12, H = 10;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var line = new Color32(22, 28, 46, 255);
            var fill = new Color32(238, 232, 214, 255);
            var dot  = new Color32(70, 62, 52, 255);
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool body = y >= 3 && y <= 9 && x >= 1 && x <= 10;
                    bool corner = (x <= 1 || x >= 10) && (y <= 3 || y >= 9);
                    bool tail = (y == 2 && x >= 3 && x <= 4) || (y == 1 && x == 3);
                    Color32 c = clear;
                    if (tail) c = fill;
                    else if (body && !corner) c = fill;
                    // 테두리
                    if (body && !corner && (x == 1 || x == 10 || y == 3 || y == 9)) c = line;
                    if (tail && y == 1) c = line;
                    // 말줄임 점 3개
                    if (y == 6 && (x == 3 || x == 5 || x == 7)) c = dot;
                    tex.SetPixel(x, y, c);
                }
            tex.filterMode = FilterMode.Point;
            tex.Apply();
            _bubbleSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 16f);
            return _bubbleSprite;
        }

        private void ShowBubble()
        {
            if (bubble == null)
            {
                bubble = new GameObject("ChatBubble");
                bubble.transform.SetParent(transform, false);
                bubble.transform.localPosition = new Vector3(0.28f, 0.75f, 0f);
                var sr = bubble.AddComponent<SpriteRenderer>();
                sr.sprite = BubbleSprite();
                sr.sortingOrder = 32;          // 이름표(30)/활동(31) 위
            }
            bubble.SetActive(true);
            bubbleUntil = Time.time + BubbleSec;
        }

        private void HideBubble()
        {
            if (bubble != null) bubble.SetActive(false);
            bubbleUntil = -1f;
        }
    }
}
