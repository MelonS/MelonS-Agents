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
    ///   a HIGHER passMul (≈0.95) so it slows the pawn LESS = faster cross.
    ///   PawnMovement reads the per-instance value via the static PassMulAt(pos)
    ///   helper (which returns DefaultPassMul when no door is at pos, so a plain
    ///   door behaves EXACTLY as before).  B9's NotifyPassing()→PlayDoor SFX call
    ///   is preserved verbatim.
    ///
    /// 추후 확장: faction filter (bandit 차단), 열림/닫힘 animation, HP/repair.
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
        /// behaviour).  An autodoor prefab sets this HIGHER (≈0.95) so it slows
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

        private SpriteRenderer sr;
        private Color baseColor = Color.white;
        private float openHintTime = -10f;
        private const float OpenHintDuration = 0.3f;

        /// <summary>
        /// protected virtual so AutodoorEntity can run base setup (trigger
        /// collider + sprite cache) then raise its own passMul.  A plain door's
        /// passMul stays at the serialized default (0.65) — unchanged behaviour.
        /// </summary>
        protected virtual void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            col.isTrigger = true;
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
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
            // 통과 중 sprite 살짝 밝아짐 - 시각 피드백
            if (sr != null && baseColor.a > 0)
            {
                bool recentPass = Time.time - openHintTime < OpenHintDuration;
                float b = recentPass ? 1.25f : 1f;
                sr.color = new Color(
                    Mathf.Min(1f, baseColor.r * b),
                    Mathf.Min(1f, baseColor.g * b),
                    Mathf.Min(1f, baseColor.b * b),
                    baseColor.a);
            }
        }
    }
}
