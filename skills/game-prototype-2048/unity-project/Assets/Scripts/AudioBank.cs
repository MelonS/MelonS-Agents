using UnityEngine;

namespace MelonS.GameProto
{
    public class AudioBank : MonoBehaviour
    {
        public static AudioBank Instance { get; private set; }
        [SerializeField] private AudioClip slide;
        [SerializeField] private AudioClip merge;
        private AudioSource src;
        public void SetClips(AudioClip s, AudioClip m) { slide = s; merge = m; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            src = GetComponent<AudioSource>();
            if (src == null) src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.volume = 0.5f;
        }
        public void PlaySlide() { if (slide != null && src != null) src.PlayOneShot(slide, 0.5f); }
        public void PlayMerge() { if (merge != null && src != null) src.PlayOneShot(merge, 0.7f); }
    }
}
