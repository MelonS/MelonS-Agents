using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Magnets toward player when nearby, pickup adds XP.</summary>
    public class XPGem : MonoBehaviour
    {
        [SerializeField] private int xpValue = 1;
        [SerializeField] private float magnetRange = 2.5f;
        [SerializeField] private float magnetSpeed = 8f;
        [SerializeField] private float pickupRange = 0.5f;

        private void Update()
        {
            if (PlayerMovement.Instance == null) return;
            Vector3 target = PlayerMovement.Instance.transform.position;
            float dist = Vector3.Distance(transform.position, target);
            if (dist <= pickupRange)
            {
                if (XPManager.Instance != null) XPManager.Instance.AddXP(xpValue);
                if (AudioBank.Instance != null) AudioBank.Instance.PlayPickup();
                Destroy(gameObject);
                return;
            }
            if (dist <= magnetRange)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, magnetSpeed * Time.deltaTime);
            }
        }
    }
}
