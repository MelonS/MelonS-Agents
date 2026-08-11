using UnityEngine;

namespace MelonS.GameProto.Naval
{
    /// <summary>
    /// 배 바닥 4점(뱃머리·고물·좌현·우현) 높이를 파도에서 샘플링해 배를 띄운다.
    /// OpenMMO 의 water system 문서엔 이 컴포넌트에 대응하는 게 없다 — 저쪽도
    /// 물 높이를 렌더링 전용으로만 쓴다. 부력은 우리가 직접 설계했다
    /// (docs/next-title-naval-prototype-2026-08-12.md 참고).
    /// 요(yaw)는 건드리지 않는다 — 그건 ShipController 의 몫, 여기선 피치·롤·Y만.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShipBuoyancy : MonoBehaviour
    {
        public float bowOffsetZ = 3f;
        public float sternOffsetZ = -3f;
        public float beamOffsetX = 1f;
        public float floatDamping = 3f;
        public float rotationDamping = 2f;

        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
        }

        private void FixedUpdate()
        {
            if (OceanWaveSampler.Instance == null) return;

            float hBow = SampleLocal(new Vector3(0, 0, bowOffsetZ));
            float hStern = SampleLocal(new Vector3(0, 0, sternOffsetZ));
            float hPort = SampleLocal(new Vector3(-beamOffsetX, 0, 0));
            float hStbd = SampleLocal(new Vector3(beamOffsetX, 0, 0));
            float avg = (hBow + hStern + hPort + hStbd) * 0.25f;

            Vector3 pos = transform.position;
            Vector3 targetPos = new Vector3(pos.x, avg, pos.z);
            rb.MovePosition(Vector3.Lerp(pos, targetPos, floatDamping * Time.fixedDeltaTime));

            float pitchDeg = Mathf.Atan2(hStern - hBow, bowOffsetZ - sternOffsetZ) * Mathf.Rad2Deg;
            float rollDeg = Mathf.Atan2(hPort - hStbd, beamOffsetX * 2f) * Mathf.Rad2Deg;
            Quaternion targetRot = Quaternion.Euler(pitchDeg, transform.eulerAngles.y, rollDeg);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, rotationDamping * Time.fixedDeltaTime));
        }

        private float SampleLocal(Vector3 local)
        {
            Vector3 world = transform.TransformPoint(local);
            return OceanWaveSampler.Instance.SampleHeight(new Vector2(world.x, world.z), Time.time);
        }
    }
}
