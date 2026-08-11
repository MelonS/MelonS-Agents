using UnityEngine;

namespace MelonS.GameProto.Naval
{
    /// <summary>배 뒤 자유 회전 추적 카메라 — 우클릭 드래그로 궤도 회전.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig3D : MonoBehaviour
    {
        public Transform target;
        public float distance = 10f;
        public float height = 4f;
        public float orbitSpeed = 120f;
        public float followDamping = 5f;

        private float yaw;
        private float pitch = 15f;

        private void LateUpdate()
        {
            if (target == null) return;

            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * orbitSpeed * Time.deltaTime;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * orbitSpeed * Time.deltaTime, -10f, 70f);
            }
            else
            {
                yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, followDamping * Time.deltaTime);
            }

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
            Vector3 desiredPos = target.position - rot * Vector3.forward * distance + Vector3.up * height;
            transform.position = Vector3.Lerp(transform.position, desiredPos, followDamping * Time.deltaTime);
            transform.LookAt(target.position + Vector3.up * height * 0.5f);
        }
    }
}
