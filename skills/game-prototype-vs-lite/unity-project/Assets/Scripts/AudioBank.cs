using UnityEngine;

namespace MelonS.GameProto
{
    public class AudioBank : MonoBehaviour
    {
        public static AudioBank Instance { get; private set; }

        [SerializeField] private AudioClip shoot;
        [SerializeField] private AudioClip hit;
        [SerializeField] private AudioClip pickup;
        [SerializeField] private AudioClip levelup;
        private AudioSource src;
        private float lastShoot = -10f;
        private float lastHit = -10f;

        public void SetClips(AudioClip s, AudioClip h, AudioClip p, AudioClip l)
        { shoot = s; hit = h; pickup = p; levelup = l; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            src = GetComponent<AudioSource>();
            if (src == null) src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.volume = 0.5f;
        }

        public void PlayShoot()
        {
            if (shoot == null || src == null) return;
            if (Time.time - lastShoot < 0.1f) return;
            src.PlayOneShot(shoot, 0.4f);
            lastShoot = Time.time;
        }

        public void PlayHit()
        {
            if (hit == null || src == null) return;
            if (Time.time - lastHit < 0.05f) return;
            src.PlayOneShot(hit, 0.5f);
            lastHit = Time.time;
        }

        public void PlayPickup() { if (pickup != null && src != null) src.PlayOneShot(pickup, 0.6f); }
        public void PlayLevelUp() { if (levelup != null && src != null) src.PlayOneShot(levelup, 0.7f); }
    }
}
