using UnityEngine;

namespace MelonS.GameProto.Naval
{
    /// <summary>
    /// 스로틀 전후진 + 방향타 좌우. 관성 있는 가감속 — 즉각 목표치로 뛰지 않고
    /// Lerp 로 뒤쫓게 해서 "묵직한 배" 느낌을 낸다.
    /// Unity 6 Rigidbody API: drag/angularDrag/velocity 가 아니라
    /// linearDamping/angularDamping/linearVelocity 를 쓴다 (엔진 버전 확인:
    /// 6000.0.75f1).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShipController : MonoBehaviour
    {
        public float maxForwardForce = 4000f;
        public float maxTurnTorque = 1500f;
        public float throttleResponse = 0.6f;
        public float rudderResponse = 1.2f;
        public float linearDrag = 1.2f;
        public float angularDrag = 2.0f;

        private Rigidbody rb;
        private float throttleInput;
        private float rudderInput;
        private float currentThrottle;
        private float currentRudder;

        // 자동조종 경로 — Unity -batchmode 에서 키 입력 없이 물리 sanity 만
        // 확인하기 위한 테스트 훅. **이 경로로 "조작이 된다"를 검증했다고 보고하면
        // 안 된다** — 실제 키보드 입력 경로(Input.GetAxis)는 사람이 직접 눌러
        // 확인해야 진짜 검증이다 (거짓 검증 금지 — memory: verify-the-real-path).
        private bool autopilot;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.linearDamping = linearDrag;
            rb.angularDamping = angularDrag;

            foreach (string a in System.Environment.GetCommandLineArgs())
            {
                if (a == "-autopilot") autopilot = true;
            }
        }

        private void Update()
        {
            if (autopilot)
            {
                float t = Time.time;
                throttleInput = t < 6f ? 1f : 0f;
                rudderInput = (t > 3f && t < 6f) ? 0.6f : 0f;
            }
            else
            {
                throttleInput = Input.GetAxis("Vertical");
                rudderInput = Input.GetAxis("Horizontal");
            }
        }

        private void FixedUpdate()
        {
            currentThrottle = Mathf.Lerp(currentThrottle, throttleInput, throttleResponse * Time.fixedDeltaTime);
            currentRudder = Mathf.Lerp(currentRudder, rudderInput, rudderResponse * Time.fixedDeltaTime);

            rb.AddForce(transform.forward * currentThrottle * maxForwardForce);

            // 방향타는 전진 속도가 있어야 잘 듣는다 — 정지 상태 제자리 회전을
            // 약화시켜서 실제 배처럼 "달리면서 돈다"가 되게 한다.
            float speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / 3f);
            rb.AddTorque(Vector3.up * currentRudder * maxTurnTorque * Mathf.Max(speedFactor, 0.15f));
        }
    }
}
