using System.Collections;
using UnityEngine;
using MelonS.GameProto.Core;

namespace MelonS.GameProto
{
    /// <summary>
    /// Day 6 audio bank.  Holds AudioClip references.  Optional —
    /// scripts gracefully no-op if clips are not assigned.
    ///
    /// SFX redesign 2026-05-30 (operator: "도끼로 나무 찍는 소리"):
    ///   chop.wav      — dull axe-into-wood thunk (woody resonance + transient)
    ///   harvest.wav   — plant rustle + snap
    ///   hit.wav       — combat thud (sharper transient, melee/arrow impact)
    ///   select.wav    — soft short UI blip (gentle, per-click)
    ///   wolf_howl     — short eerie howl (unchanged)
    ///   bgm_ambient   — calm low loopable ambient bed (unchanged, bgmSource)
    ///
    /// M2 sound-coverage push 2026-05-30 (wiki Dim2 items #1/#2/#4):
    ///   build.wav     — hammer/clink construct finish (wiki #1: wall finishing plays once, throttled)
    ///   alert.wav     — alert siren (wiki #2: tier-scaled repeat count, tier3 > tier1)
    ///   ambient.wav   — looping outdoor wind/birds bed (wiki #4: continuous ambient independent of music)
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
    ///   PlayBuild()   — 0.25s (BlueprintEntity.Complete() fires once per wall,
    ///                   but rapid-place scripts could batch completions; throttle
    ///                   guards overlapping build-finish calls in a single frame).
    ///   PlayAlert()   — 3.0s global burst guard.  Alert fires on raid events;
    ///                   without guard a multi-enemy raid trigger loop could spam
    ///                   back-to-back bursts.  Tier-scaled beep count runs inside
    ///                   the burst via a coroutine on the AudioBank MonoBehaviour.
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
    ///   5. sfxBuild/sfxAlert/sfxAmbient slots require scene-side SerializeField
    ///      assignment on the AudioBank GameObject — see QA flags below.
    /// </summary>
    public class AudioBank : MonoBehaviour
    {
        public static AudioBank Instance => Services.Get<AudioBank>();  // R6

        // ── existing SFX slots ──────────────────────────────────────────────
        public AudioClip bgm;
        public AudioClip sfxChop;
        public AudioClip sfxSelect;
        public AudioClip sfxHit;       // combat arrow/melee impact
        public AudioClip sfxHarvest;   // crop harvest
        public AudioClip sfxWolfHowl;  // wolf appear

        // ── M2 SFX slots (wiki #1/#2/#4) ───────────────────────────────────
        public AudioClip sfxBuild;     // hammer/clink — wall/blueprint construction finish
        public AudioClip sfxAlert;     // alert siren — tier-scaled raid warning
        public AudioClip sfxAmbient;   // outdoor ambient bed — wind/birds (loops on ambientSource)

        // ── Audio sources ───────────────────────────────────────────────────
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;
        // ambientSource: independent looping outdoor bed (NOT bgmSource — wiki #4)
        private AudioSource ambientSource;

        // ── Per-key throttle timestamps (Sound Designer — 2026-05-30) ──────
        private float _lastChopTime    = -10f;
        private float _lastHitTime     = -10f;
        private float _lastHarvestTime = -10f;
        private float _lastBuildTime   = -10f;   // wiki #1: wall finish throttle
        private float _lastAlertTime   = -10f;   // wiki #2: alert burst guard

        // ── Min-interval constants — rationale documented above ─────────────
        private const float ChopInterval    = 0.25f;  // per-frame work-loop safe
        private const float HitInterval     = 0.25f;  // per-collision safe
        private const float HarvestInterval = 0.25f;  // gather-loop safe
        private const float BuildInterval   = 0.25f;  // per-complete safe
        private const float AlertInterval   = 3.0f;   // burst-guard (raid spam)

        // ── Alert beep inter-repeat gap (pitch variation spread) ────────────
        private const float AlertBeepGap    = 0.35f;  // seconds between beeps in a burst

        private void Awake()
        {
            if (Services.Has<AudioBank>() && Services.Get<AudioBank>() != this)
            { Destroy(gameObject); return; }
            Services.Register<AudioBank>(this);

            if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();

            // ambientSource: second independent source — does NOT share bgmSource
            ambientSource = gameObject.AddComponent<AudioSource>();

            bgmSource.loop   = true;
            bgmSource.volume = 0.45f;
            sfxSource.volume = 0.7f;

            ambientSource.loop            = true;
            ambientSource.volume          = 0.18f;  // quiet bed, below BGM and SFX
            ambientSource.spatialBlend    = 0f;     // 2-D (global, not positional)
            ambientSource.playOnAwake     = false;
        }

        private void Start()
        {
            if (bgm != null)
            {
                bgmSource.clip   = bgm;
                bgmSource.loop   = true;
                bgmSource.volume = 0.25f;
                bgmSource.Play();
            }

            // wiki #4: start looping outdoor ambient bed independent of music
            if (sfxAmbient != null)
            {
                ambientSource.clip = sfxAmbient;
                ambientSource.Play();
            }
        }

        // ────────────────────────────────────────────────────────────────────
        //  EXISTING METHODS (unchanged)
        // ────────────────────────────────────────────────────────────────────

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

        // ────────────────────────────────────────────────────────────────────
        //  M2 NEW METHODS (wiki #1/#2)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Hammer/clink construct finish — call from BlueprintEntity.Complete().
        /// Wiki acceptance: "A wall finishing plays a construct sound once (throttled)."
        /// Throttled 0.25s (BuildInterval) so rapid batch-completions in the same
        /// frame do not stack into a burst.  Graceful null no-op if sfxBuild or
        /// sfxSource is not assigned (Scene-side SerializeField wiring may be absent
        /// on prototype day-1 — QA flag: AudioBank.sfxBuild needs scene assignment).
        /// </summary>
        public void PlayBuild()
        {
            if (sfxBuild == null || sfxSource == null) return;
            if (Time.time - _lastBuildTime < BuildInterval) return;
            _lastBuildTime = Time.time;
            sfxSource.PlayOneShot(sfxBuild, 0.80f);
        }

        /// <summary>
        /// Alert siren with tier-scaled repeat count.
        /// Wiki acceptance: "A raid event plays an alert siren; tier-3 repeats more than tier-1."
        ///   tier &lt;= 1  => 2 beeps
        ///   tier == 2   => 3 beeps
        ///   tier >= 3   => 4 beeps
        /// Global burst guard: AlertInterval (3.0s) prevents raid-loop spam.
        /// Beeps are staggered via a coroutine (AlertBeepGap = 0.35s between repeats)
        /// with slight pitch offsets so the siren pattern feels mechanical/urgent.
        /// Graceful null no-op if sfxAlert or sfxSource is not assigned.
        /// QA flag: AudioBank.sfxAlert needs scene-side SerializeField assignment.
        /// </summary>
        public void PlayAlert(int tier)
        {
            if (sfxAlert == null || sfxSource == null) return;
            if (Time.time - _lastAlertTime < AlertInterval) return;
            _lastAlertTime = Time.time;

            int beepCount = tier <= 1 ? 2 : tier == 2 ? 3 : 4;
            StartCoroutine(AlertBurstCoroutine(beepCount));
        }

        /// <summary>
        /// Fires beepCount PlayOneShot calls staggered by AlertBeepGap seconds,
        /// each with a slight pitch offset so repeating beeps feel like a siren
        /// pattern rather than a stuck key.  Runs entirely on this MonoBehaviour
        /// so no external scheduler dependency.
        /// </summary>
        private IEnumerator AlertBurstCoroutine(int beepCount)
        {
            float[] pitchOffsets = { 1.0f, 1.08f, 0.96f, 1.04f };  // mild siren sweep

            for (int i = 0; i < beepCount; i++)
            {
                if (sfxAlert == null || sfxSource == null) yield break;
                sfxSource.pitch = pitchOffsets[i % pitchOffsets.Length];
                sfxSource.PlayOneShot(sfxAlert, 0.90f);
                sfxSource.pitch = 1.0f;  // restore default after scheduling
                if (i < beepCount - 1)
                    yield return new WaitForSeconds(AlertBeepGap);
            }

            sfxSource.pitch = 1.0f;  // ensure clean reset even if loop exits early
        }
    }
}
