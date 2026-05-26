using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Smoothly follow player.  Camera Z preserved.</summary>
    public class CameraFollowPlayer : MonoBehaviour
    {
        [SerializeField] private float smoothTime = 0.15f;
        private Vector3 vel;

        private void LateUpdate()
        {
            if (PlayerMovement.Instance == null) return;
            Vector3 target = PlayerMovement.Instance.transform.position;
            target.z = transform.position.z;
            transform.position = Vector3.SmoothDamp(transform.position, target, ref vel, smoothTime);
        }
    }
}
