using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// D2 몰입 레이어 (design-immersion-2026-06-11) — 걷기 모션.
    /// 기존엔 모든 엔티티가 미끄러지듯 평행이동(애니메이션 0)해 정적인 화면의
    /// 주범이었다.  스프라이트 교체 없이 transform 만으로:
    ///   - 이동 중: 발걸음 리듬의 스케일 스쿼시 바운스 (y 늘고 x 살짝 줄기 교차)
    ///   - 수평 진행방향으로 SpriteRenderer.flipX (children 라벨/바는 안 뒤집힘)
    ///   - 정지: 스케일 부드럽게 원복
    /// 위치는 절대 건드리지 않는다 — PawnMovement/Rigidbody 의 소유권 침범 없음,
    /// 거리 probe(pawnNear 등)에도 영향 0.  움직임 감지는 transform 델타라
    /// 림(PawnMovement)/동물(Rigidbody)/적(MoveTowards) 모두 공용.
    /// 부착: 각 엔티티 Awake 에서 AddComponent (BlobShadow 와 동일 배선점).
    /// </summary>
    public class MotionFx : MonoBehaviour
    {
        [SerializeField] private float bounceAmp = 0.05f;   // y 스트레치 최대 비율
        [SerializeField] private float bounceHz = 3.2f;     // 보행 1배속 기준 step 주기
        private const float MinSpeed = 0.15f;               // 이하 = 정지로 간주 (떨림 방지)

        private SpriteRenderer sr;
        private Vector3 baseScale;
        private Vector3 prevPos;
        private float phase;
        private bool inited;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            // baseScale 은 첫 LateUpdate 에 캡처 — AnimalEntity.Configure 등 스포너가
            //  AddComponent 직후(같은 프레임) 종별 스케일을 세팅하므로, Awake 캡처면
            //  scale=1 을 기준으로 잡아 정지 시 동물이 쪼그라드는 버그가 된다.
            if (!inited)
            {
                baseScale = transform.localScale;
                prevPos = transform.position;
                inited = true;
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 pos = transform.position;
            Vector2 delta = pos - prevPos;
            prevPos = pos;
            float speed = delta.magnitude / dt;

            // 진행방향 flip — 수평 성분이 실재할 때만 (수직 이동 중 떨림 방지).
            if (sr != null && Mathf.Abs(delta.x) > 0.0005f)
                sr.flipX = delta.x < 0f;

            if (speed > MinSpeed)
            {
                // 빠를수록 발걸음도 빨라진다 (게임 배속 4x 에서도 자연스럽게).
                phase += dt * bounceHz * Mathf.Clamp(speed, 0.6f, 2.2f) * Mathf.PI * 2f;
                float s = Mathf.Abs(Mathf.Sin(phase));       // 0~1 step 펄스
                float squash = s * bounceAmp;
                transform.localScale = new Vector3(
                    baseScale.x * (1f - squash * 0.6f),      // 늘 때 살짝 좁아지기 (관성)
                    baseScale.y * (1f + squash),
                    baseScale.z);
            }
            else
            {
                phase = 0f;
                transform.localScale = Vector3.Lerp(transform.localScale, baseScale, 12f * dt);
            }
        }
    }
}
