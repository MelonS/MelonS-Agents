using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 6 audio bank.  Holds AudioClip references.  Optional —
    /// scripts gracefully no-op if clips are not assigned.
    ///
    /// SFX redesign 2026-05-30 (operator: "도끼로 나무 찍는 소리"):
    ///   chop.wav    — dull axe-into-wood thunk (woody resonance + transient)
    ///   harvest.wav — plant rustle + snap
    ///   hit.wav     — combat thud (sharper transient, melee/arrow impact)
    ///   select.wav  — soft short UI blip (gentle, per-click)
    ///   wolf_howl   — short eerie howl (unchanged)
    ///   bgm_ambient — calm low loopable ambient bed (unchanged)
    ///
    /// Throttle discipline (Lesson #4, 2026-05-27 PawnSim chop-buzz):
    ///   PlayChop()    — 0.25s min-interval.  PawnChopper.Update() calls
    ///                   TakeChopDamage every frame; without throttle = 60
    ///                   PlayOneShot/sec = audio buffer collapse ("이상한 사운드").
    ///   PlayHit()     — 0.25s (per-entity at call site; ArrowProjectile
    ///                   fires per-collision so stays at 0.25s here).
    ///   PlayHarvest() — 0.25s (was 0.15s; CropEntity may call from tight
    ///                   gather loops; increased to match chop safety margin).
    ///   PlaySelect()  — 0.0s (no throttle: each pawn click is a distinct
    ///                   user action, not a tight loop).
    ///   PlayWolfHowl()— 0.0s (event-driven, one-shot per wolf spawn event).
    ///
    /// PROGRAMMER ACTIONS FLAGGED (Sound Designer lane — do not edit entities):
    ///   1. TreeEntity.cs:84  — PlayChop() called from inside TakeChopDamage()
    ///      which PawnChopper.Update() calls every frame with deltaTime damage.
    ///      The 0.25s throttle added HERE in PlayChop() guards this.
    ///      If you want per-tree independence, add lastChopTime to TreeEntity
    ///      and guard there instead (remove throttle here when done).
    ///   2. AnimalEntity.cs:149 — PlayChop() used for animal hit sound.
    ///      Semantically wrong: animal combat hit should call PlayHit().
    ///      Change AnimalEntity.TakeDamage() line 149 to PlayHit().
    ///   3. StoneVeinEntity.cs:78 — PlayChop() for mining.  Already has
    ///      entity-level 0.6s guard (SfxInterval).  Acceptable for now;
    ///      ideally add sfxMine slot + mine.wav for distinct pick-on-stone feel.
    ///   4. PlayWolfHowl() has no callers.  Wire to AIDirector wolf_pack event.
    /// </summary>
    public class AudioBank : MonoBehaviour
    {
        public static AudioBank Instance => Services.Get<AudioBank>();  // R6

        public AudioClip bgm;
        public AudioClip sfxChop;
        public AudioClip sfxSelect;
        public AudioClip sfxHit;       // Day 80 — arrow/melee impact
        public AudioClip sfxHarvest;   // Day 80 — crop harvest
        public AudioClip sfxWolfHowl;  // Day 80 — wolf appear

        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        // Per-key throttle timestamps  (Sound Designer — 2026-05-30)
        private float _lastChopTime    = -10f;
        private float _lastHitTime     = -10f;
        private float _lastHarvestTime = -10f;

        // Min-interval constants — documented above with rationale
        private const float ChopInterval    = 0.25f;  // per-frame work-loop safe
        private const float HitInterval     = 0.25f;  // per-collision safe
        private const float HarvestInterval = 0.25f;  // gather-loop safe

        private void Awake()
        {
            if (Services.Has<AudioBank>() && Services.Get<AudioBank>() != this)
            { Destroy(gameObject); return; }
            Services.Register<AudioBank>(this);
            if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.volume = 0.45f;
            sfxSource.volume = 0.7f;
        }

        private void Start()
        {
            if (bgm != null)
            {
                bgmSource.clip = bgm;
                bgmSource.loop = true;
                bgmSource.volume = 0.25f;
                bgmSource.Play();
            }
        }

        /// <summary>
        /// Axe-into-wood thunk.  Throttled 0.25s — caller (TreeEntity via
        /// PawnChopper.Update) fires every frame; without throttle = buzz.
        /// </summary>
        public void PlayChop()
        {
            if (sfxChop == null || sfxSource == null) return;
            if (Time.time - _lastChopTime < ChopInterval) return;
            _lastChopTime = Time.time;
            sfxSource.PlayOneShot(sfxChop, 0.85f);
        }

        /// <summary>
        /// Soft UI blip.  No throttle — each pawn click is a distinct user
        /// action, not a per-frame loop.  Volume kept low (0.6) so it never
        /// fatigues on rapid clicks.
        /// </summary>
        public void PlaySelect()
        {
            if (sfxSelect != null && sfxSource != null)
                sfxSource.PlayOneShot(sfxSelect, 0.6f);
        }

        /// <summary>
        /// Combat thud (melee/arrow impact).  Throttled 0.25s.
        /// Note: AnimalEntity.TakeDamage currently calls PlayChop() — it
        /// should call PlayHit() instead for correct audio character.
        /// </summary>
        public void PlayHit()
        {
            if (sfxHit == null || sfxSource == null) return;
            if (Time.time - _lastHitTime < HitInterval) return;
            _lastHitTime = Time.time;
            sfxSource.PlayOneShot(sfxHit, 0.75f);
        }

        /// <summary>
        /// Plant rustle + snap on crop harvest.  Throttled 0.25s.
        /// </summary>
        public void PlayHarvest()
        {
            if (sfxHarvest == null || sfxSource == null) return;
            if (Time.time - _lastHarvestTime < HarvestInterval) return;
            _lastHarvestTime = Time.time;
            sfxSource.PlayOneShot(sfxHarvest, 0.85f);
        }

        /// <summary>
        /// Eerie wolf howl.  No throttle — event-driven, one per wolf spawn.
        /// Currently has no callers — wire to AIDirector wolf_pack event.
        /// </summary>
        public void PlayWolfHowl()
        {
            if (sfxWolfHowl != null && sfxSource != null)
                sfxSource.PlayOneShot(sfxWolfHowl, 0.65f);
        }
    }
}
