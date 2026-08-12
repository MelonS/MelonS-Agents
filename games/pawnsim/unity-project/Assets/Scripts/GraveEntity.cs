using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// GDD G3 (design-social-2026-07-24) — 무덤 1×2.  빈 무덤 → PawnBurier 가 시신
    /// 운반·안장하면 봉분+각인.  "시체가 영원히 방치되는 쓰레기"에서 "이야기의
    /// 마침표"로.  스프라이트: Resources/Sprites/grave64_{empty,mound}.png (잉크
    /// 문법 v3.2, 없으면 BuildManager 절차 폴백 스프라이트 유지).
    /// ⚠ 세이브 v1 한계: StructureTag(mode,pos) 재구성이라 재로드 시 빈 무덤으로
    /// 복원(안장자 각인 소실) — SaveLoadManager 확장 시 occupant 직렬화 예정
    /// (design-social §G3 후속).
    /// </summary>
    public class GraveEntity : MonoBehaviour
    {
        public bool Occupied { get; private set; }
        public string OccupantName { get; private set; } = "";
        public string Epitaph { get; private set; } = "";

        private SpriteRenderer sr;
        private static Sprite _empty, _mound;
        private static bool _loaded;

        private static void EnsureSprites()
        {
            if (_loaded) return;
            _loaded = true;
            _empty = Resources.Load<Sprite>("Sprites/grave64_empty");
            _mound = Resources.Load<Sprite>("Sprites/grave64_mound");
        }

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            ApplyVisual();
        }

        /// <summary>안장 — 봉분 전환 + 각인 + 전 콜로니 "동료를 묻어줌" +3 (0.5일).</summary>
        public void Bury(string pawnName, string story)
        {
            Occupied = true;
            OccupantName = pawnName ?? "";
            Epitaph = story ?? "";
            ApplyVisual();
            foreach (var th in Object.FindObjectsByType<PawnThoughts>(FindObjectsSortMode.None))
                th.AddThought("동료를 묻어줌", +3f, 500f);
            Debug.Log($"[Grave] {OccupantName} 안장 완료 @ ({transform.position.x:F1},{transform.position.y:F1})");
        }

        private void ApplyVisual()
        {
            EnsureSprites();
            if (sr == null) return;
            var sp = Occupied ? _mound : _empty;
            if (sp != null) sr.sprite = sp;
        }

        public string Description => Occupied
            ? $"여기 {OccupantName} 잠들다.\n{Epitaph}"
            : "빈 무덤 — 시신을 안장할 수 있다.";

        /// <summary>가장 가까운, 타인이 예약 안 한 빈 무덤 (매장 job 탐색).</summary>
        public static GraveEntity FindNearestEmpty(Vector2 from, GameObject claimant)
        {
            GraveEntity best = null;
            float bestSq = float.MaxValue;
            foreach (var g in Object.FindObjectsByType<GraveEntity>(FindObjectsSortMode.None))
            {
                if (g == null || g.Occupied) continue;
                if (MelonS.GameProto.AI.ReservationManager.IsReservedByOther(g, claimant)) continue;
                float sq = ((Vector2)g.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = g; }
            }
            return best;
        }
    }
}
