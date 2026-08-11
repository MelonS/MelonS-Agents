using UnityEngine;

namespace MelonS.GameProto.Naval
{
    /// <summary>고정 피치각으로 배를 따라가는 카메라 — 범선 항해 게임 장르에서 흔한 부감 시점.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig2Point5D : MonoBehaviour
    {
        public Transform target;
        public float pitchDeg = 55f;
        public float distance = 14f;
        public float followDamping = 4f;

        private void LateUpdate()
        {
            if (target == null) return;
            Quaternion rot = Quaternion.Euler(pitchDeg, target.eulerAngles.y, 0);
            Vector3 desiredPos = target.position - rot * Vector3.forward * distance;
            transform.position = Vector3.Lerp(transform.position, desiredPos, followDamping * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, followDamping * Time.deltaTime);
        }
    }
}
