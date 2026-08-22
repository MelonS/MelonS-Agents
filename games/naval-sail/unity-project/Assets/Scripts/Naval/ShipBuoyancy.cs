using UnityEngine;

namespace MelonS.GameProto.Naval
{
    /// <summary>
    /// 배 바닥 4점(뱃머리·고물·좌현·우현)의 잠긴 깊이에 비례해 위쪽 힘을 준다
    /// (스프링 부력). 예전엔 <c>Rigidbody.MovePosition/MoveRotation</c>으로
    /// Y·피치·롤을 직접 명령했는데, 그게 매 FixedUpdate마다 "이번 스텝 끝의
    /// X/Z는 이번 스텝 시작 값과 같아야 한다"고 강제하는 꼴이라
    /// <see cref="ShipController"/>의 <c>AddForce</c> 전진력과 충돌해서 배가
    /// 실제로는 거의 안 움직였다 (2026-08-12, 운영자 실플레이 리포트로 발견 —
    /// 자동조종 검증 땐 작은 박스 placeholder라 우연히 덜 티가 났다).
    /// 힘 기반으로 바꾸면 같은 Rigidbody에 걸리는 여러 힘이 물리 솔버에서
    /// 자연히 합산된다 — 충돌하지 않는다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShipBuoyancy : MonoBehaviour
    {
        public float bowOffsetZ = 3f;
        public float sternOffsetZ = -3f;
        public float beamOffsetX = 1f;
        public float buoyancyStrength = 6000f;   // N per m 잠긴 깊이, 샘플점당
        public float maxSubmersionForM = 2f;     // 잠긴 깊이 상한 — 과잠수 시 힘 폭주 방지
        public float waterLinearDrag = 1.5f;     // 잠긴 동안만 추가되는 항력(선체가 물을 가름)
        public float waterAngularDrag = 2.5f;

        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = true; // 부력이 중력과 실제로 힘겨루기를 해야 한다
        }

        private void FixedUpdate()
        {
            if (OceanWaveSampler.Instance == null) return;

            int submergedCount = 0;
            submergedCount += ApplyCornerForce(new Vector3(0, 0, bowOffsetZ));
            submergedCount += ApplyCornerForce(new Vector3(0, 0, sternOffsetZ));
            submergedCount += ApplyCornerForce(new Vector3(-beamOffsetX, 0, 0));
            submergedCount += ApplyCornerForce(new Vector3(beamOffsetX, 0, 0));

            if (submergedCount > 0)
            {
                rb.AddForce(-rb.linearVelocity * waterLinearDrag, ForceMode.Acceleration);
                rb.AddTorque(-rb.angularVelocity * waterAngularDrag, ForceMode.Acceleration);
            }
        }

        private int ApplyCornerForce(Vector3 local)
        {
            Vector3 worldPoint = transform.TransformPoint(local);
            float waveHeight = OceanWaveSampler.Instance.SampleHeight(new Vector2(worldPoint.x, worldPoint.z), Time.time);
            float submersion = waveHeight - worldPoint.y;
            if (submersion <= 0f) return 0;

            float force = Mathf.Min(submersion, maxSubmersionForM) * buoyancyStrength;
            rb.AddForceAtPosition(Vector3.up * force, worldPoint, ForceMode.Force);
            return 1;
        }
    }
}
