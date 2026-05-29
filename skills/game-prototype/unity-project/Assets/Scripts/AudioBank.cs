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
    /// M3 sound-coverage push 2026-05-30 (wiki Dim2 item #7, W-M3-01 Lane D):
    ///   mine.wav      — pick-on-stone impact (wiki #7: mining plays a distinct sound,
    ///                   not the chop thunk; 2400Hz metal-pick transient + 420Hz stone
    ///                   resonance body + grit noise burst; 0.18s — shorter/denser than chop).
    ///
    /// M3 sound-coverage push 2026-05-30 (wiki Dim2 item #9, W-M3-02 Lane D):
    ///   rain.wav      — soft continuous filtered-noise rain bed, loopable (wiki #9:
    ///                   a storm plays a rain loop; clear weather is silent of rain).
    ///                   Played on a dedicated rainSource (2D, vol=0.20).
    ///                   RainSoundDriver.cs polls WeatherController and calls
    ///                   PlayRain()/StopRain() when weather transitions.
    ///
    /// M3 sound-coverage push 2026-05-30 (wiki Dim2 item #8, W-M3-03 Lane A):
    ///   danger.wav    — tense low-drone + slow percussive bed, loopable (wiki #8:
    ///                   music swaps to the tension track during a raid (threatTier>=2)
    ///                   and back when clear).
    ///                   Played on a dedicated dangerSource (2D, loop=true).
    ///                   MusicDirector.cs polls AIDirector.Instance.CurrentThreatTier
    ///                   and calls PlayDangerMusic()/StopDangerMusic() on transitions.
    ///                   Crossfade: ~1s volume lerp between bgmSource and dangerSource
    ///                   via DangerCrossfadeCoroutine; idempotent; graceful null no-op.
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
    ///   PlayMine()    — 0.25s (MineInterval).  StoneVeinEntity.TakeMineDamage()
    ///                   already has an entity-level 0.6s SfxInterval guard, but
    ///                   AudioBank-side throttle guards any future callers that
    ///                   might lack the entity guard (defense-in-depth pattern).
    ///   PlayRain()    — No per-call throttle: it is a looping bed, not a one-shot.
    ///                   RainSoundDriver polls once per frame via Update(); the
    ///                   isPlaying guard inside PlayRain() is the idempotency gate
    ///                   (calling PlayRain() again while the loop runs is a no-op).
    ///   PlayDangerMusic() — No per-call throttle: looping bed controlled by
    ///                   MusicDirector state-machine (only fires on tier transitions).
    ///                   Idempotency: _dangerActive flag prevents re-triggering while
    ///                   already in danger mode or mid-crossfade.
    ///   StopDangerMusic() — Same idempotency pattern via _dangerActive flag.
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
    ///   3. StoneVeinEntity.cs — mining now calls PlayMine() (was PlayChop()).
    ///      Wiki #7: pick-on-stone distinct from axe-on-wood chop thunk. DONE.
    ///   4. PlayWolfHowl() has no callers.  Wire to AIDirector wolf_pack event.
    ///   5. sfxBuild/sfxAlert/sfxAmbient slots require scene-side SerializeField
    ///      assignment on the AudioBank GameObject — see QA flags below.
    ///   6. sfxMine slot requires scene-side SerializeField assignment on the
    ///      AudioBank GameObject (assign mine.wav same as sfxBuild/alert/ambient
    ///      last wave).  QA FLAG: wire sfxMine in the Inspector before verifying.
    ///   7. rainLoop slot requires scene-side SerializeField assignment on the
    ///      AudioBank GameObject — assign rain.wav.
    ///      QA FLAG: wire rainLoop in the Inspector (same workflow as sfxBuild/
    ///      sfxAlert/sfxAmbient/sfxMine). Wiki #9 cannot verify without this wire.
    ///   8. dangerBgm slot requires scene-side SerializeField assignment on the
    ///      AudioBank GameObject — assign danger.wav.
    ///      QA FLAG: wire dangerBgm in the Inspector (same workflow as sfxBuild/
    ///      sfxAlert/sfxAmbient/sfxMine/rainLoop). Wiki #8 cannot verify without
    ///      this wire.  MusicDirector.cs calls PlayDangerMusic()/StopDangerMusic()
    ///      when AIDirector.CurrentThreatTier crosses the tier=2 threshold.
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

        // ── M3 SFX slots (wiki #7, W-M3-01 Lane D) ─────────────────────────
        // QA FLAG: assign mine.wav to this slot on the AudioBank GameObject in
        // the Inspector (same workflow as sfxBuild/sfxAlert/sfxAmbient last wave).
        public AudioClip sfxMine;      // pick-on-stone — mining impact (distinct from chop)

        // ── M3 SFX slots (wiki #9, W-M3-02 Lane D) ─────────────────────────
        // QA FLAG: assign rain.wav to this slot on the AudioBank GameObject in
        // the Inspector (same workflow as sfxBuild/sfxAlert/sfxAmbient/sfxMine).
        // RainSoundDriver.cs calls PlayRain()/StopRain() when weather transitions.
        public AudioClip rainLoop;     // soft loopable rain bed — played on rainSource

        // ── M3 SFX slots (wiki #8, W-M3-03 Lane A) ─────────────────────────
        // QA FLAG: assign danger.wav to this slot on the AudioBank GameObject in
        // the Inspector (same workflow as sfxBuild/sfxAlert/sfxAmbient/sfxMine/rainLoop).
        // MusicDirector.cs calls PlayDangerMusic()/StopDangerMusic() when
        // AIDirector.CurrentThreatTier crosses the tier=2 boundary.
        public AudioClip dangerBgm;    // tense drone/percussive bed — played on dangerSource

        // ── Audio sources ───────────────────────────────────────────────────
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;
        // ambientSource: independent looping outdoor bed (NOT bgmSource — wiki #4)
        private AudioSource ambientSource;
        // rainSource: dedicated looping source for rain bed (wiki #9 — distinct
        // from ambientSource so rain can start/stop independently of ambient).
        private AudioSource rainSource;
        // dangerSource: dedicated looping source for the danger/tension music track
        // (wiki #8). Exists alongside bgmSource — crossfade coroutine lerps volumes
        // between the two sources so the transition is smooth (~1s).
        private AudioSource dangerSource;

        // ── Per-key throttle timestamps (Sound Designer — 2026-05-30) ──────
        private float _lastChopTime    = -10f;
        private float _lastHitTime     = -10f;
        private float _lastHarvestTime = -10f;
        private float _lastBuildTime   = -10f;   // wiki #1: wall finish throttle
        private float _lastAlertTime   = -10f;   // wiki #2: alert burst guard
        private float _lastMineTime    = -10f;   // wiki #7: pick-on-stone throttle

        // ── Danger music state (wiki #8 crossfade) ─────────────────────────
        // True while dangerSource is playing (or mid-fade-in); false while calm
        // bgmSource is primary (or mid-fade-out).  Guards idempotency so
        // MusicDirector can call PlayDangerMusic every frame on tier>=2 without
        // retriggering the fade each frame.
        private bool _dangerActive = false;
        // Reference to any in-progress crossfade coroutine so we can stop the
        // previous one if a rapid tier transition reverses direction mid-fade.
        private Coroutine _crossfadeCoroutine;

        // ── Min-interval constants — rationale documented above ─────────────
        private const float ChopInterval    = 0.25f;  // per-frame work-loop safe
        private const float HitInterval     = 0.25f;  // per-collision safe
        private const float HarvestInterval = 0.25f;  // gather-loop safe
        private const float BuildInterval   = 0.25f;  // per-complete safe
        private const float AlertInterval   = 3.0f;   // burst-guard (raid spam)
        private const float MineInterval    = 0.25f;  // per-mine-hit safe (entity has own 0.6s guard)

        // ── Alert beep inter-repeat gap (pitch variation spread) ────────────
        private const float AlertBeepGap    = 0.35f;  // seconds between beeps in a burst

        // ── Crossfade duration (wiki #8: ~1s fade) ─────────────────────────
        private const float CrossfadeDuration = 1.0f;

        // ── BGM and danger volumes ──────────────────────────────────────────
        private const float BgmVolume    = 0.25f;    // calm track volume
        private const float DangerVolume = 0.30f;    // danger track volume (slightly higher for urgency)

        private void Awake()
        {
            if (Services.Has<AudioBank>() && Services.Get<AudioBank>() != this)
            { Destroy(gameObject); return; }
            Services.Register<AudioBank>(this);

            if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();

            // ambientSource: second independent source — does NOT share bgmSource
            ambientSource = gameObject.AddComponent<AudioSource>();

            // rainSource: third independent source — loopable rain bed (wiki #9)
            rainSource = gameObject.AddComponent<AudioSource>();

            // dangerSource: fourth independent source — loopable danger music (wiki #8)
            // Starts silent (volume=0); PlayDangerMusic() fades it in while fading bgmSource out.
            dangerSource = gameObject.AddComponent<AudioSource>();

            bgmSource.loop   = true;
            bgmSource.volume = BgmVolume;
            sfxSource.volume = 0.7f;

            ambientSource.loop            = true;
            ambientSource.volume          = 0.18f;  // quiet bed, below BGM and SFX
            ambientSource.spatialBlend    = 0f;     // 2-D (global, not positional)
            ambientSource.playOnAwake     = false;

            rainSource.loop            = true;
            rainSource.volume          = 0.20f;  // quiet bed — rain behind everything
            rainSource.spatialBlend    = 0f;     // 2-D (global, not positional)
            rainSource.playOnAwake     = false;

            dangerSource.loop          = true;
            dangerSource.volume        = 0f;     // starts silent; crossfade brings it in
            dangerSource.spatialBlend  = 0f;     // 2-D (global, not positional)
            dangerSource.playOnAwake   = false;
        }

        private void Start()
        {
            if (bgm != null)
            {
                bgmSource.clip   = bgm;
                bgmSource.loop   = true;
                bgmSource.volume = BgmVolume;
                bgmSource.Play();
            }

            // wiki #4: start looping outdoor ambient bed independent of music
            if (sfxAmbient != null)
            {
                ambientSource.clip = sfxAmbient;
                ambientSource.Play();
            }

            // rain.wav does NOT auto-start — RainSoundDriver controls it.
            // danger.wav does NOT auto-start — MusicDirector controls it.
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

        // ────────────────────────────────────────────────────────────────────
        //  M3 NEW METHODS (wiki #7, W-M3-01 Lane D)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Pick-on-stone mining impact — call from StoneVeinEntity.TakeMineDamage().
        /// Wiki acceptance #7: "Mining ... plays a distinct sound" — a pick-on-stone,
        /// NOT the chop thunk (chop = axe-into-wood, low woody resonance 150-240Hz;
        /// mine = metal pick on rock, high 2400Hz transient + 420Hz stone ring).
        ///
        /// Throttled 0.25s (MineInterval) with its own _lastMineTime guard, independent
        /// of PlayChop/_lastChopTime — swapping PlayChop->PlayMine in StoneVeinEntity
        /// does NOT share the chop throttle window, which would have silenced mining
        /// for 0.25s after any tree chop in the same frame.
        ///
        /// StoneVeinEntity.TakeMineDamage() also has its own entity-level 0.6s
        /// SfxInterval guard.  The AudioBank-side 0.25s guard is defense-in-depth
        /// for any future caller lacking the entity guard (same pattern as PlayChop).
        ///
        /// Graceful null no-op if sfxMine or sfxSource is not assigned.
        /// QA FLAG: AudioBank.sfxMine requires scene-side SerializeField assignment
        /// on the AudioBank GameObject — assign mine.wav in the Inspector
        /// (same workflow as sfxBuild/sfxAlert/sfxAmbient last wave).
        /// </summary>
        public void PlayMine()
        {
            if (sfxMine == null || sfxSource == null) return;
            if (Time.time - _lastMineTime < MineInterval) return;
            _lastMineTime = Time.time;
            sfxSource.PlayOneShot(sfxMine, 0.80f);
        }

        // ────────────────────────────────────────────────────────────────────
        //  M3 NEW METHODS (wiki #9, W-M3-02 Lane D)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Start the looping rain bed — called by RainSoundDriver when
        /// WeatherController.Current == WeatherKind.Storm.
        ///
        /// Idempotent: if rainSource is already playing the correct clip,
        /// this is a no-op (avoids restarting the loop mid-weather-event
        /// if the driver polls while rain is already running).
        ///
        /// Graceful null no-op if rainLoop clip or rainSource is not ready
        /// (e.g. scene-side SerializeField wiring for rainLoop not yet done).
        ///
        /// Wiki acceptance #9: "A storm plays a rain loop."
        ///
        /// QA FLAG: AudioBank.rainLoop requires scene-side SerializeField
        /// assignment on the AudioBank GameObject — assign rain.wav in the
        /// Inspector (same workflow as sfxBuild/sfxAlert/sfxAmbient/sfxMine).
        /// </summary>
        public void PlayRain()
        {
            if (rainLoop == null || rainSource == null) return;
            if (rainSource.isPlaying && rainSource.clip == rainLoop) return;
            rainSource.clip = rainLoop;
            rainSource.loop = true;
            rainSource.Play();
        }

        /// <summary>
        /// Stop the looping rain bed — called by RainSoundDriver when
        /// WeatherController.Current == WeatherKind.Clear.
        ///
        /// Graceful: if rainSource is not playing, Stop() is a no-op in Unity.
        /// Null-safe if rainSource was never created (should not happen after
        /// Awake, but defensive check prevents NRE in edge-case domain reloads).
        ///
        /// Wiki acceptance #9: "Clear weather is silent of rain."
        /// </summary>
        public void StopRain()
        {
            if (rainSource == null) return;
            if (rainSource.isPlaying) rainSource.Stop();
        }

        // ────────────────────────────────────────────────────────────────────
        //  M3 NEW METHODS (wiki #8, W-M3-03 Lane A)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Crossfade to the danger/tension music track over ~1s.
        /// Called by MusicDirector when AIDirector.CurrentThreatTier >= 2.
        ///
        /// Idempotent: if danger is already active (or mid-fade-in), this is a
        /// no-op — MusicDirector calls this every frame while tier >= 2 so the
        /// _dangerActive guard prevents re-triggering the coroutine.
        ///
        /// Crossfade behavior:
        ///   - Ensures dangerSource has dangerBgm clip loaded and is playing
        ///     (paused at vol=0 if not yet started) before beginning the lerp.
        ///   - Over CrossfadeDuration (~1s) lerps:
        ///       bgmSource.volume  : BgmVolume -> 0
        ///       dangerSource.volume: 0 -> DangerVolume
        ///   - Any previous crossfade coroutine is stopped first to prevent
        ///     simultaneous opposing fades (rapid tier flip edge case).
        ///
        /// Graceful null no-op if dangerBgm or dangerSource is unassigned.
        ///
        /// Wiki acceptance #8: "Music swaps to the tension track during a raid
        /// (threatTier>=2) and back when clear."
        ///
        /// QA FLAG: AudioBank.dangerBgm requires scene-side SerializeField
        /// assignment on the AudioBank GameObject — assign danger.wav in the
        /// Inspector (same workflow as sfxBuild/sfxAlert/sfxAmbient/sfxMine/rainLoop).
        /// </summary>
        public void PlayDangerMusic()
        {
            if (dangerBgm == null || dangerSource == null) return;
            if (_dangerActive) return;  // already in danger mode — idempotent

            _dangerActive = true;

            // Ensure dangerSource has the clip and is playing before we start fading
            if (dangerSource.clip != dangerBgm)
            {
                dangerSource.clip   = dangerBgm;
                dangerSource.loop   = true;
                dangerSource.volume = 0f;
                dangerSource.Play();
            }
            else if (!dangerSource.isPlaying)
            {
                dangerSource.volume = 0f;
                dangerSource.Play();
            }

            if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = StartCoroutine(
                DangerCrossfadeCoroutine(towardsDanger: true));
        }

        /// <summary>
        /// Crossfade back to the calm BGM over ~1s.
        /// Called by MusicDirector when AIDirector.CurrentThreatTier falls below 2.
        ///
        /// Idempotent: if calm is already active (or mid-fade-out), this is a no-op.
        ///
        /// Crossfade behavior:
        ///   - Over CrossfadeDuration (~1s) lerps:
        ///       dangerSource.volume: DangerVolume -> 0
        ///       bgmSource.volume  : 0 -> BgmVolume
        ///   - After the fade completes, dangerSource.Stop() so it does not
        ///     consume audio resources while idle.
        ///   - Any previous crossfade coroutine is stopped first.
        ///
        /// Graceful null no-op if dangerSource is unassigned.
        ///
        /// Wiki acceptance #8: "Music swaps ... back when clear."
        /// </summary>
        public void StopDangerMusic()
        {
            if (dangerSource == null) return;
            if (!_dangerActive) return;  // already in calm mode — idempotent

            _dangerActive = false;

            if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = StartCoroutine(
                DangerCrossfadeCoroutine(towardsDanger: false));
        }

        /// <summary>
        /// Shared crossfade coroutine used by both PlayDangerMusic and StopDangerMusic.
        /// Lerps bgmSource and dangerSource volumes over CrossfadeDuration seconds.
        ///   towardsDanger=true  : bgm -> 0 / danger -> DangerVolume
        ///   towardsDanger=false : danger -> 0 / bgm -> BgmVolume (then stops danger)
        ///
        /// Uses unscaled time (Time.unscaledDeltaTime) so the crossfade is not
        /// affected by Time.timeScale pausing (player pauses game during a raid).
        /// </summary>
        private IEnumerator DangerCrossfadeCoroutine(bool towardsDanger)
        {
            float elapsed = 0f;

            // Snapshot current volumes at fade start so we lerp from wherever
            // the sources currently sit (handles mid-fade reversal cleanly).
            float bgmStart    = (bgmSource    != null) ? bgmSource.volume    : 0f;
            float dangerStart = (dangerSource != null) ? dangerSource.volume : 0f;

            float bgmTarget    = towardsDanger ? 0f        : BgmVolume;
            float dangerTarget = towardsDanger ? DangerVolume : 0f;

            while (elapsed < CrossfadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / CrossfadeDuration);

                if (bgmSource    != null) bgmSource.volume    = Mathf.Lerp(bgmStart,    bgmTarget,    t);
                if (dangerSource != null) dangerSource.volume = Mathf.Lerp(dangerStart, dangerTarget, t);

                yield return null;
            }

            // Snap to exact targets at end of fade
            if (bgmSource    != null) bgmSource.volume    = bgmTarget;
            if (dangerSource != null) dangerSource.volume = dangerTarget;

            // After a fade-to-calm completes, stop the danger source so it
            // does not consume audio resources while silent.
            if (!towardsDanger && dangerSource != null && dangerSource.isPlaying)
                dangerSource.Stop();

            _crossfadeCoroutine = null;
        }
    }
}
