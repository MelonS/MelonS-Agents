using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>Singleton audio bank — plays drop/merge/gameover SFX.</summary>
    public class AudioBank : MonoBehaviour
    {
        public static AudioBank Instance { get; private set; }

        [SerializeField] private AudioClip dropClip;
        [SerializeField] private AudioClip mergeClip;
        [SerializeField] private AudioClip gameOverClip;
        private AudioSource source;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = 0.5f;
        }

        public void PlayDrop()    { if (dropClip != null && source != null)    source.PlayOneShot(dropClip); }
        public void PlayMerge()   { if (mergeClip != null && source != null)   source.PlayOneShot(mergeClip); }
        public void PlayGameOver(){ if (gameOverClip != null && source != null) source.PlayOneShot(gameOverClip); }
    }
}
