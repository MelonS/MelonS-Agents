using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 18: built door.  #171 - wiki: 문은 통과 시 시간이 걸림 (~0.45s).
    ///   trigger collider 유지 (pawn 통과 가능) + 통과 중 movement 감속.
    ///   PawnMovement 가 IsInsideDoor(pos) 폴링하여 speedMul *= PassMulAt(pos).
    ///
    /// W-M6-04 (B5) — autodoor variant: passMul is now a PER-INSTANCE field
    ///   (was a compile-time const).  A plain door keeps the historical 0.65
    ///   (DefaultPassMul); an autodoor prefab is the SAME DoorEntity built with
    ///   a HIGHER passMul (~0.95) so it slows the pawn LESS = faster cross.
    ///   PawnMovement reads the per-instance value via the static PassMulAt(pos)
    ///   helper (which returns DefaultPassMul when no door is at pos, so a plain
    ///   door behaves EXACTLY as before).  B9's NotifyPassing()→PlayDoor SFX call
    ///   is preserved verbatim.
    ///
    /// Operator follow-up — 문 열림/닫힘 모션: while a pawn passes (NotifyPassing
    ///   fires every PawnMovement tick), the leaf slides sideways (localPosition.x)
    ///   and tucks its width (localScale.x) toward an open pose, then lerps shut
    ///   after the pass.  Real transform motion (NOT a colour flash); no new sprite,
    ///   no per-frame allocation, no audio inside the lerp loop.
    ///
    /// 추후 확장: faction filter (bandit 차단), HP/repair.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class DoorEntity : MonoBehaviour
    {
        /// <summary>
        /// Plain-door pass-through speed multiplier — wiki: ~0.5 (60% slow);
        /// prototype 0.65 으로 완만하게.  Kept as a const so any external
        /// reference to the old DoorEntity.PassMul value still compiles to the
        /// same number, AND so the per-instance field below can default to it.
        /// </summary>
        public const float DefaultPassMul = 0.65f;

        /// <summary>
        /// W-M6-04 (B5) — per-instance pass-through multiplier (was the const
        /// PassMul).  Default 0.65 = plain door (identical to historical
        /// behaviour).  An autodoor prefab sets this HIGHER (~0.95) so it slows
        /// the crossing pawn LESS → the pawn crosses an autodoor FASTER than a
        /// plain door (wiki B5 acceptance).  SerializeField so the designer can
        /// tune day-1 feel without a code change.
        /// </summary>
        [SerializeField] private float passMul = DefaultPassMul;

        /// <summary>Per-instance pass-through multiplier (read-only accessor).</summary>
        public float PassMul => passMul;

        /// <summary>
        /// W-M6-04 (B5) — programmatic setter used by the autodoor lazy-prefab
        /// path (BuildManager) so it can build a faster-cross door from the
        /// SAME DoorEntity component without a separate subclass.  No-op-safe.
        /// </summary>
        public void SetPassMul(float v) => passMul = v;

        private float openHintTime = -10f;

        // --- Open/close slide motion (operator: 문 열림/닫힘 모션) ---------------
        //   While a pawn is passing, PawnMovement calls NotifyPassing() every tick,
        //   so openHintTime stays fresh.  We treat the door as "wanting open" for
        //   OpenHoldDuration after the last NotifyPassing, then it slides shut.
        //   The visual is a real open/close motion (NOT a colour flash):
        //   the sprite slides sideways (localPosition.x) AND its width shrinks
        //   (localScale.x) toward a "tucked into the frame" pose, lerped smoothly.
        //   Pure transform interpolation — no per-frame allocation, no audio in
        //   the loop (NotifyPassing already throttles its own SFX).
        private const float OpenHoldDuration = 0.18f; // grace after last pass tick before closing
        private const float OpenSlideX = 0.42f;       // local-X offset of the fully-open leaf
        private const float OpenScaleX = 0.18f;       // fully-open leaf width (fraction of closed)
        private const float OpenLerpRate = 14f;       // open speed (snappier)
        private const float CloseLerpRate = 8f;       // close speed (a touch slower = settling)

        private Vector3 closedLocalPos;   // captured in Awake — the built/closed pose
        private float closedScaleX;       // captured in Awake
        private float openness;           // 0 = closed, 1 = fully open (lerped each frame)

        /// <summary>
        /// protected virtual so AutodoorEntity can run base setup (trigger
        /// collider + sprite cache) then raise its own passMul.  A plain door's
        /// passMul stays at the serialized default (0.65) — unchanged behaviour.
        /// </summary>
        protected virtual void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            col.isTrigger = true;

            // Capture the closed (built) pose once so the slide always returns to
            // the authored transform — works regardless of where BuildManager
            // placed the sprite child.
            closedLocalPos = transform.localPosition;
            closedScaleX = transform.localScale.x;
            openness = 0f;
        }

        /// <summary>#171 - pos 가 door collider 안에 있는가 (OverlapBox).</summary>
        public static bool IsInsideDoor(Vector2 pos)
        {
            var hits = Physics2D.OverlapBoxAll(pos, Vector2.one * 0.3f, 0f);
            foreach (var h in hits)
                if (h != null && h.GetComponent<DoorEntity>() != null) return true;
            return false;
        }

        /// <summary>
        /// W-M6-04 (B5) — pass-through multiplier of the door at pos.  Surfaces
        /// the PER-INSTANCE passMul (so a plain door returns 0.65 and an autodoor
        /// returns its higher value → faster cross).  Returns DefaultPassMul
        /// (0.65) when NO door is at pos, so a caller that always multiplies by
        /// the result after an IsInsideDoor() check behaves EXACTLY as the old
        /// `*= DoorEntity.PassMul` const did for a plain door.  Mirrors
        /// IsInsideDoor's OverlapBox + first-match (break) semantics so it lines
        /// up with the door NotifyPassing() picks.
        /// </summary>
        public static float PassMulAt(Vector2 pos)
        {
            var hits = Physics2D.OverlapBoxAll(pos, Vector2.one * 0.3f, 0f);
            foreach (var h in hits)
            {
                var d = h != null ? h.GetComponent<DoorEntity>() : null;
                if (d != null) return d.passMul;
            }
            return DefaultPassMul;
        }

        /// <summary>pawn 이 안에 있을 때 호출 - 잠깐 밝아짐 (열린 표시).</summary>
        public void NotifyPassing()
        {
            openHintTime = Time.time;
            AudioBank.Instance?.PlayDoor();  // wiki B9: door open/pass-through SFX
        }

        private void Update()
        {
            // Real open/close slide motion (operator: 문 모션 없음 → 실제 열림/닫힘).
            //   target openness is 1 while a pawn is mid-pass (NotifyPassing keeps
            //   openHintTime fresh), then decays to 0 after the hold grace.
            //   asymmetric lerp: opens fast, closes a touch slower (settling feel).
            bool wantOpen = Time.time - openHintTime < OpenHoldDuration;
            float target = wantOpen ? 1f : 0f;
            float rate = wantOpen ? OpenLerpRate : CloseLerpRate;

            // exp-style frame-rate-independent approach to target; no alloc.
            openness = Mathf.MoveTowards(
                openness, target, rate * Time.deltaTime * Mathf.Max(0.05f, Mathf.Abs(target - openness) + 0.15f));
            openness = Mathf.Clamp01(openness);

            // Slide the leaf sideways and tuck its width — the door "opens".
            // smoothstep so the leaf eases in/out instead of moving linearly.
            float t = openness * openness * (3f - 2f * openness);

            Vector3 lp = closedLocalPos;
            lp.x = closedLocalPos.x + OpenSlideX * t;
            transform.localPosition = lp;

            Vector3 ls = transform.localScale;
            ls.x = Mathf.Lerp(closedScaleX, closedScaleX * OpenScaleX, t);
            transform.localScale = ls;
        }
    }
}
