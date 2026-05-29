using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 18: built door.  #171 - wiki: 문은 통과 시 시간이 걸림 (~0.45s).
    ///   trigger collider 유지 (pawn 통과 가능) + 통과 중 movement 감속.
    ///   PawnMovement 가 IsInsideDoor(pos) 폴링하여 speedMul *= PassMul.
    ///
    /// 추후 확장: faction filter (bandit 차단), 열림/닫힘 animation, HP/repair.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class DoorEntity : MonoBehaviour
    {
        /// <summary>wiki: ~0.5 (60% slow).  prototype 0.65 으로 완만하게.</summary>
        public const float PassMul = 0.65f;

        private SpriteRenderer sr;
        private Color baseColor = Color.white;
        private float openHintTime = -10f;
        private const float OpenHintDuration = 0.3f;

        private void Awake()
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
