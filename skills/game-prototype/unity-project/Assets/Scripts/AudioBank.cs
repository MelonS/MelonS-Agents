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
    ///                   resonance body + grit noise burst; 0.18s -- shorter/denser than chop).
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
    /// M6 sound-coverage push (wiki B8, W-M6-01 B8 lane):
    ///   footstep.wav      — short soft earth-thud footfall, ~0.07s (wiki B8: a walking
    ///                       pawn plays a faint footstep ~every 0.4s). Played via
    ///                       PlayFootstep(); 0.2s AudioBank-side throttle (defense-in-depth,
    ///                       outer per-pawn 0.4s in FootstepDriver is the primary gate).
    ///   ambient_night.wav — looping outdoor cricket/insect night bed, ~15s (wiki B8:
    ///                       ambient bed differs at 02:00 vs 12:00). AudioBank.ambientSource
    ///                       clip is swapped between sfxAmbient (day: birds) and
    ///                       sfxAmbientNight (night: crickets) via SwapAmbientToDay() /
    ///                       SwapAmbientToNight(). FootstepDriver polls GameClock.DayProgress
    ///                       and calls the swap on day/night boundary transitions.
    ///
    /// M6 sound-coverage push (wiki B9, W-M6-03 B9 lane):
    ///   door.wav   — soft door creak/thud, ~0.18s (wiki B9: opening a door plays a
    ///                distinct sound). Played via PlayDoor(); 0.15s throttle (DoorInterval).
    ///                DoorEntity.NotifyPassing() calls AudioBank.Instance?.PlayDoor().
    ///   cook.wav   — sizzle/clatter cooking sound, ~0.30s (wiki B9: cooking at a stove
    ///                plays a distinct sound). Played via PlayCook(); 0.5s throttle
    ///                (CookInterval). StoveEntity.CookOne() calls AudioBank.Instance?.PlayCook().
    ///   shoot.wav  — bow-twang arrow-launch, ~0.15s (wiki B9: firing an arrow plays a
    ///                distinct sound). Played via PlayShoot(); no throttle (ShootInterval=0).
    ///                ArrowProjectile.SpawnArrow() calls AudioBank.Instance?.PlayShoot().
    ///
    /// Throttle discipline (Lesson #4, 2026-05-27 PawnSim chop-buzz):
    ///   PlayChop()        — 0.25s min-interval.  PawnChopper.Update() calls
    ///                       TakeChopDamage every frame; without throttle = 60
    ///                       PlayOneShot/sec = audio buffer collapse ("이상한 사운드").
    ///   PlayHit()         — 0.25s (per-entity at call site; ArrowProjectile
    ///                       fires per-collision so stays at 0.25s here).
    ///   PlayHarvest()     — 0.25s (was 0.15s; CropEntity may call from tight
    ///                       gather loops; increased to match chop safety margin).
    ///   PlaySelect()      — 0.0s (no throttle: each pawn click is a distinct
    ///                       user action, not a tight loop).
    ///   PlayWolfHowl()    — 0.0s (event-driven, one-shot per wolf spawn event).
    ///   PlayBuild()       — 0.25s (BlueprintEntity.Complete() fires once per wall,
    ///                       but rapid-place scripts could batch completions; throttle
    ///                       guards overlapping build-finish calls in a single frame).
    ///   PlayAlert()       — 3.0s global burst guard.  Alert fires on raid events;
    ///                       without guard a multi-enemy raid trigger loop could spam
    ///                       back-to-back bursts.  Tier-scaled beep count runs inside
    ///                       the burst via a coroutine on the AudioBank MonoBehaviour.
    ///   PlayMine()        — 0.25s (MineInterval).  StoneVeinEntity.TakeMineDamage()
    ///                       already has an entity-level 0.6s SfxInterval guard, but
    ///                       AudioBank-side throttle guards any future callers that
    ///                       might lack the entity guard (defense-in-depth pattern).
    ///   PlayRain()        — No per-call throttle: it is a looping bed, not a one-shot.
    ///                       RainSoundDriver polls once per frame via Update(); the
    ///                       isPlaying guard inside PlayRain() is the idempotency gate
    ///                       (calling PlayRain() again while the loop runs is a no-op).
    ///   PlayDangerMusic() — No per-call throttle: looping bed controlled by
    ///                       MusicDirector state-machine (only fires on tier transitions).
    ///                       Idempotency: _dangerActive flag prevents re-triggering while
    ///                       already in danger mode or mid-crossfade.
    ///   StopDangerMusic() — Same idempotency pattern via _dangerActive flag.
    ///   PlayFootstep()    — 0.2s AudioBank-side throttle (defense-in-depth).
    ///                       FootstepDriver throttles 0.4s per-pawn as the primary gate.
    ///                       Pawn movement Update() fires every frame; multiple walking
    ///                       pawns at 60fps without per-pawn + bank throttle = footstep
    ///                       spam. Volume 0.45 (faint, below SFX 0.7 and BGM 0.25 bed).
    ///   SwapAmbientToNight() / SwapAmbientToDay() -- No per-call throttle: transition
    ///                       is state-machine driven (only fires on day/night boundary).
    ///                       Idempotency: _isNightAmbient flag prevents clip-restart if
    ///                       already in the correct ambient state.
    ///   PlayDoor()        — 0.15s (DoorInterval). DoorEntity.NotifyPassing() fires
    ///                       once per pawn pass-through; 0.15s guards the brief window
    ///                       where a pawn's enter+exit might both fire in one frame.
    ///   PlayCook()        — 0.5s (CookInterval). PawnCook.Update() calls
    ///                       StoveEntity.CookOne() at cookInterval=1.5s, so 0.5s bank
    ///                       guard is well within normal use but blocks sub-frame bursts
    ///                       if two pawns share a stove simultaneously.
    ///   PlayShoot()       — 0.0s (no throttle). ArrowProjectile.SpawnArrow() fires
    ///                       once per bow-draw cycle -- each arrow is a distinct event,
    ///                       not a tight loop. No throttle needed.
    ///
    /// PROGRAMMER ACTIONS FLAGGED (Sound Designer lane -- do not edit entities):
    ///   1. TreeEntity.cs:84  -- PlayChop() called from inside TakeChopDamage()
    ///      which PawnChopper.Update() calls every frame with deltaTime damage.
    ///      The 0.25s throttle added HERE in PlayChop() guards this.
    ///      If you want per-tree independence, add lastChopTime to TreeEntity
    ///      and guard there instead (remove throttle here when done).
    ///   2. AnimalEntity.cs:149 -- PlayChop() used for animal hit sound.
    ///      Semantically wrong: animal combat hit should call PlayHit().
    ///      Change AnimalEntity.TakeDamage() line 149 to PlayHit().
    ///   3. StoneVeinEntity.cs -- mining now calls PlayMine() (was PlayChop()).
    ///      Wiki #7: pick-on-stone distinct from axe-on-wood chop thunk. DONE.
    ///   4. PlayWolfHowl() has no callers.  Wire to AIDirector wolf_pack event.
    ///   5. sfxBuild/sfxAlert/sfxAmbient slots require scene-side SerializeField
    ///      assignment on the AudioBank GameObject -- see QA flags below.
    ///   6. sfxMine slot requires scene-side SerializeField assignment on the
    ///      AudioBank GameObject (assign mine.wav same as sfxBuild/alert/ambient
    ///      last wave).  QA FLAG: wire sfxMine in the Inspector before verifying.
    ///   7. rainLoop slot requires scene-side SerializeField assignment on the
    ///      AudioBank GameObject -- assign rain.wav.
    ///      QA FLAG: wire rainLoop in the Inspector (same workflow as sfxBuild/
    ///      sfxAlert/sfxAmbient/sfxMine). Wiki #9 cannot verify without this wire.
    ///   8. dangerBgm slot requires scene-side SerializeField assignment on the
    ///      AudioBank GameObject -- assign danger.wav.
    ///      QA FLAG: wire dangerBgm in the Inspector (same workflow as sfxBuild/
    ///      sfxAlert/sfxAmbient/sfxMine/rainLoop). Wiki #8 cannot verify without
    ///      this wire.  MusicDirector.cs calls PlayDangerMusic()/StopDangerMusic()
    ///      when AIDirector.CurrentThreatTier crosses the tier=2 threshold.
    ///   9. sfxFootstep slot requires scene-side SerializeField assignment on the
    ///      AudioBank GameObject -- assign footstep.wav.
    ///      QA FLAG: wire sfxFootstep in the Inspector (same workflow as above).
    ///      FootstepDriver.cs calls PlayFootstep() per walking pawn (~every 0.4s).
    ///  10. sfxAmbientNight slot requires scene-side SerializeField assignment on the
    ///      AudioBank GameObject -- assign ambient_night.wav.
    ///      QA FLAG: wire sfxAmbientNight in the Inspector. FootstepDriver calls
    ///      SwapAmbientToNight() / SwapAmbientToDay() on day/night transitions.
    ///      Without this wire, the ambient bed stays on the day clip indefinitely
    ///      (graceful null no-op -- no crash, just no night variation).
    ///  11. sfxDoor slot requires scene-side Inspector assignment on the AudioBank GO.
    ///      QA FLAG: assign door.wav to sfxDoor. DoorEntity.NotifyPassing() calls
    ///      AudioBank.Instance?.PlayDoor(). Without wire: graceful null no-op.
    ///  12. sfxCook slot requires scene-side Inspector assignment on the AudioBank GO.
    ///      QA FLAG: assign cook.wav to sfxCook. StoveEntity.CookOne() calls
    ///      AudioBank.Instance?.PlayCook(). Without wire: graceful null no-op.
    ///  13. sfxShoot slot requires scene-side Inspector assignment on the AudioBank GO.
    ///      QA FLAG: assign shoot.wav to sfxShoot. ArrowProjectile.SpawnArrow() calls
    ///      AudioBank.Instance?.PlayShoot(). Without wire: graceful null no-op.
    /// </summary>
    public class AudioBank : MonoBehaviour
    {
        public static AudioBank Instance => Services.Get<AudioBank>();  // R6

        // -- existing SFX slots --
        public AudioClip bgm;
        public AudioClip sfxChop;
        public AudioClip sfxSelect;
        public AudioClip sfxHit;       // combat arrow/melee impact
        public AudioClip sfxHarvest;   // crop harvest
        public AudioClip sfxWolfHowl;  // wolf appear

        // -- M2 SFX slots (wiki #1/#2/#4) --
        public AudioClip sfxBuild;     // hammer/clink -- wall/blueprint construction finish
        public AudioClip sfxAlert;     // alert siren -- tier-scaled raid warning
        public AudioClip sfxAmbient;   // outdoor ambient bed -- wind/birds (loops on ambientSource)

        // -- M3 SFX slots (wiki #7, W-M3-01 Lane D) --
        // QA FLAG: assign mine.wav to this slot on the AudioBank GameObject in
        // the Inspector (same workflow as sfxBuild/sfxAlert/sfxAmbient last wave).
        public AudioClip sfxMine;      // pick-on-stone -- mining impact (distinct from chop)

        // -- M3 SFX slots (wiki #9, W-M3-02 Lane D) --
        // QA FLAG: assign rain.wav to this slot on the AudioBank GameObject in
        // the Inspector (same workflow as sfxBuild/sfxAlert/sfxAmbient/sfxMine).
        // RainSoundDriver.cs calls PlayRain()/StopRain() when weather transitions.
        public AudioClip rainLoop;     // soft loopable rain bed -- played on rainSource

        // -- M3 SFX slots (wiki #8, W-M3-03 Lane A) --
        // QA FLAG: assign danger.wav to this slot on the AudioBank GameObject in
        // the Inspector (same workflow as sfxBuild/sfxAlert/sfxAmbient/sfxMine/rainLoop).
        // MusicDirector.cs calls PlayDangerMusic()/StopDangerMusic() when
        // AIDirector.CurrentThreatTier crosses the tier=2 boundary.
        public AudioClip dangerBgm;    // tense drone/percussive bed -- played on dangerSource

        // -- M6 SFX slots (wiki B8, W-M6-01 B8 lane) --
        // QA FLAG: assign footstep.wav to sfxFootstep on the AudioBank GameObject in
        // the Inspector (same workflow as sfxBuild/sfxAlert/sfxAmbient/sfxMine/rainLoop/dangerBgm).
        // FootstepDriver.cs calls PlayFootstep() per walking pawn (throttled 0.4s per-pawn
        // in FootstepDriver; 0.2s guard here as defense-in-depth).
        public AudioClip sfxFootstep;  // soft earth-thud footfall -- ~0.07s

        // QA FLAG: assign ambient_night.wav to sfxAmbientNight on the AudioBank GameObject
        // in the Inspector. FootstepDriver swaps ambientSource clip between sfxAmbient
        // (day: birds) and sfxAmbientNight (night: crickets) via SwapAmbientToNight/Day.
        // Without this assignment, night ambient stays silent (graceful null no-op).
        public AudioClip sfxAmbientNight; // outdoor night cricket/insect bed -- ~15s loop

        // -- M6 SFX slots (wiki B9, W-M6-03 B9 lane) --
        // QA FLAG: assign door.wav to sfxDoor on the AudioBank GameObject in the Inspector.
        // DoorEntity.NotifyPassing() calls AudioBank.Instance?.PlayDoor() on pawn pass-through.
        // Without this assignment: graceful null no-op (no crash, just silent door).
        public AudioClip sfxDoor;      // soft door creak/thud -- ~0.18s

        // QA FLAG: assign cook.wav to sfxCook on the AudioBank GameObject in the Inspector.
        // StoveEntity.CookOne() calls AudioBank.Instance?.PlayCook() on each successful cook.
        // Without this assignment: graceful null no-op (no crash, just silent cooking).
        public AudioClip sfxCook;      // sizzle/clatter cooking sound -- ~0.30s

        // QA FLAG: assign shoot.wav to sfxShoot on the AudioBank GameObject in the Inspector.
        // ArrowProjectile.SpawnArrow() calls AudioBank.Instance?.PlayShoot() on arrow launch.
        // Without this assignment: graceful null no-op (no crash, just silent bow).
        public AudioClip sfxShoot;     // bow-twang arrow-launch -- ~0.15s

        // -- Audio sources --
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;
        // ambientSource: independent looping outdoor bed (NOT bgmSource -- wiki #4)
        // Also used for night-ambient swap (FootstepDriver swaps its clip).
        private AudioSource ambientSource;
        // rainSource: dedicated looping source for rain bed (wiki #9 -- distinct
        // from ambientSource so rain can start/stop independently of ambient).
        private AudioSource rainSource;
        // dangerSource: dedicated looping source for the danger/tension music track
        // (wiki #8). Exists alongside bgmSource -- crossfade coroutine lerps volumes
        // between the two sources so the transition is smooth (~1s).
        private AudioSource dangerSource;

        // -- Per-key throttle timestamps (Sound Designer -- 2026-05-30) --
        private float _lastChopTime     = -10f;
        private float _lastHitTime      = -10f;
        private float _lastHarvestTime  = -10f;
        private float _lastBuildTime    = -10f;   // wiki #1: wall finish throttle
        private float _lastAlertTime    = -10f;   // wiki #2: alert burst guard
        private float _lastMineTime     = -10f;   // wiki #7: pick-on-stone throttle
        private float _lastFootstepTime = -10f;   // wiki B8: footstep bank-side guard
        private float _lastDoorTime     = -10f;   // wiki B9: door pass-through guard
        private float _lastCookTime     = -10f;   // wiki B9: cook tick guard

        // -- Ambient state (wiki B8 day/night swap) --
        // True when ambientSource is playing sfxAmbientNight; false when on sfxAmbient (day).
        // Guards idempotency so FootstepDriver can call SwapAmbientToNight every frame on
        // nighttime DayProgress without restarting the clip each frame.
        private bool _isNightAmbient = false;

        // -- Danger music state (wiki #8 crossfade) --
        // True while dangerSource is playing (or mid-fade-in); false while calm
        // bgmSource is primary (or mid-fade-out).  Guards idempotency so
        // MusicDirector can call PlayDangerMusic every frame on tier>=2 without
        // retriggering the fade each frame.
        private bool _dangerActive = false;
        // Reference to any in-progress crossfade coroutine so we can stop the
        // previous one if a rapid tier transition reverses direction mid-fade.
        private Coroutine _crossfadeCoroutine;

        // -- Min-interval constants -- rationale documented above --
        private const float ChopInterval      = 0.25f;  // per-frame work-loop safe
        private const float HitInterval       = 0.25f;  // per-collision safe
        private const float HarvestInterval   = 0.25f;  // gather-loop safe
        private const float BuildInterval     = 0.25f;  // per-complete safe
        private const float AlertInterval     = 3.0f;   // burst-guard (raid spam)
        private const float MineInterval      = 0.25f;  // per-mine-hit safe (entity has own 0.6s guard)
        private const float FootstepInterval  = 0.20f;  // bank-side guard; outer per-pawn 0.4s in FootstepDriver
        private const float DoorInterval      = 0.15f;  // door pass-through: enter+exit sub-frame dedup
        private const float CookInterval      = 0.50f;  // cook tick: outer cookInterval=1.5s; bank guard for multi-pawn edge

        // -- Alert beep inter-repeat gap (pitch variation spread) --
        private const float AlertBeepGap    = 0.35f;  // seconds between beeps in a burst

        // -- Crossfade duration (wiki #8: ~1s fade) --
        private const float CrossfadeDuration = 1.0f;

        // -- BGM and danger volumes --
        private const float BgmVolume    = 0.25f;    // calm track volume
        private const float DangerVolume = 0.30f;    // danger track volume (slightly higher for urgency)

        private void Awake()
        {
            if (Services.Has<AudioBank>() && Services.Get<AudioBank>() != this)
            { Destroy(gameObject); return; }
            Services.Register<AudioBank>(this);

            if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();

            // ambientSource: second independent source -- does NOT share bgmSource
            ambientSource = gameObject.AddComponent<AudioSource>();

            // rainSource: third independent source -- loopable rain bed (wiki #9)
            rainSource = gameObject.AddComponent<AudioSource>();

            // dangerSource: fourth independent source -- loopable danger music (wiki #8)
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
            rainSource.volume          = 0.20f;  // quiet bed -- rain behind everything
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
            // Default: day ambient (birds). FootstepDriver swaps to night if needed.
            if (sfxAmbient != null)
            {
                ambientSource.clip = sfxAmbient;
                ambientSource.Play();
                _isNightAmbient = false;
            }

            // rain.wav does NOT auto-start -- RainSoundDriver controls it.
            // danger.wav does NOT auto-start -- MusicDirector controls it.
        }

        // --------------------------------------------------------------------
        //  EXISTING METHODS (unchanged)
        // --------------------------------------------------------------------

        /// <summary>
        /// Axe-into-wood thunk.  Throttled 0.25s -- caller (TreeEntity via
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
        /// Soft UI blip.  No throttle -- each pawn click is a distinct user
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
        /// Note: AnimalEntity.TakeDamage currently calls PlayChop() -- it
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
        /// Eerie wolf howl.  No throttle -- event-driven, one per wolf spawn.
        /// Currently has no callers -- wire to AIDirector wolf_pack event.
        /// </summary>
        public void PlayWolfHowl()
        {
            if (sfxWolfHowl != null && sfxSource != null)
                sfxSource.PlayOneShot(sfxWolfHowl, 0.65f);
        }

        // --------------------------------------------------------------------
        //  M2 NEW METHODS (wiki #1/#2)
        // --------------------------------------------------------------------

        /// <summary>
        /// Hammer/clink construct finish -- call from BlueprintEntity.Complete().
        /// Wiki acceptance: "A wall finishing plays a construct sound once (throttled)."
        /// Throttled 0.25s (BuildInterval) so rapid batch-completions in the same
        /// frame do not stack into a burst.  Graceful null no-op if sfxBuild or
        /// sfxSource is not assigned (Scene-side SerializeField wiring may be absent
        /// on prototype day-1 -- QA flag: AudioBank.sfxBuild needs scene assignment).
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
        ///   tier &lt;= 1  =&gt; 2 beeps
        ///   tier == 2   =&gt; 3 beeps
        ///   tier &gt;= 3   =&gt; 4 beeps
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

        // --------------------------------------------------------------------
        //  M3 NEW METHODS (wiki #7, W-M3-01 Lane D)
        // --------------------------------------------------------------------

        /// <summary>
        /// Pick-on-stone mining impact -- call from StoneVeinEntity.TakeMineDamage().
        /// Wiki acceptance #7: "Mining ... plays a distinct sound" -- a pick-on-stone,
        /// NOT the chop thunk (chop = axe-into-wood, low woody resonance 150-240Hz;
        /// mine = metal pick on rock, high 2400Hz transient + 420Hz stone ring).
        ///
        /// Throttled 0.25s (MineInterval) with its own _lastMineTime guard, independent
        /// of PlayChop/_lastChopTime -- swapping PlayChop->PlayMine in StoneVeinEntity
        /// does NOT share the chop throttle window, which would have silenced mining
        /// for 0.25s after any tree chop in the same frame.
        ///
        /// StoneVeinEntity.TakeMineDamage() also has its own entity-level 0.6s
        /// SfxInterval guard.  The AudioBank-side 0.25s guard is defense-in-depth
        /// for any future caller lacking the entity guard (same pattern as PlayChop).
        ///
        /// Graceful null no-op if sfxMine or sfxSource is not assigned.
        /// QA FLAG: AudioBank.sfxMine requires scene-side SerializeField assignment
        /// on the AudioBank GameObject -- assign mine.wav in the Inspector
        /// (same workflow as sfxBuild/sfxAlert/sfxAmbient last wave).
        /// </summary>
        public void PlayMine()
        {
            if (sfxMine == null || sfxSource == null) return;
            if (Time.time - _lastMineTime < MineInterval) return;
            _lastMineTime = Time.time;
            sfxSource.PlayOneShot(sfxMine, 0.80f);
        }

        // --------------------------------------------------------------------
        //  M3 NEW METHODS (wiki #9, W-M3-02 Lane D)
        // --------------------------------------------------------------------

        /// <summary>
        /// Start the looping rain bed -- called by RainSoundDriver when
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
        /// assignment on the AudioBank GameObject -- assign rain.wav in the
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
        /// Stop the looping rain bed -- called by RainSoundDriver when
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

        // --------------------------------------------------------------------
        //  M3 NEW METHODS (wiki #8, W-M3-03 Lane A)
        // --------------------------------------------------------------------

        /// <summary>
        /// Crossfade to the danger/tension music track over ~1s.
        /// Called by MusicDirector when AIDirector.CurrentThreatTier >= 2.
        ///
        /// Idempotent: if danger is already active (or mid-fade-in), this is a
        /// no-op -- MusicDirector calls this every frame while tier >= 2 so the
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
        /// assignment on the AudioBank GameObject -- assign danger.wav in the
        /// Inspector (same workflow as sfxBuild/sfxAlert/sfxAmbient/sfxMine/rainLoop).
        /// </summary>
        public void PlayDangerMusic()
        {
            if (dangerBgm == null || dangerSource == null) return;
            if (_dangerActive) return;  // already in danger mode -- idempotent

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
            if (!_dangerActive) return;  // already in calm mode -- idempotent

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

        // --------------------------------------------------------------------
        //  M6 NEW METHODS (wiki B8, W-M6-01 B8 lane)
        // --------------------------------------------------------------------

        /// <summary>
        /// Soft earth-thud footfall -- called by FootstepDriver per walking pawn.
        ///
        /// Wiki B8 acceptance: "A walking pawn plays a faint footstep ~every 0.4s."
        ///
        /// Throttle: 0.2s AudioBank-side guard (FootstepInterval) as defense-in-depth.
        /// The outer per-pawn 0.4s throttle in FootstepDriver is the primary gate.
        /// Without the bank-side guard, multiple pawns walking simultaneously could
        /// combine their per-pawn calls to exceed the intended footstep rate:
        ///   e.g. 4 pawns each at 0.4s = 10 calls/sec through the bank without this guard.
        /// The 0.2s bank guard caps the bank's rate at 5 plays/sec regardless of pawn count.
        ///
        /// Volume 0.45 (faint): footsteps should be present but clearly below BGM,
        /// SFX impacts, and ambient -- they are texture, not foreground.
        ///
        /// Graceful null no-op if sfxFootstep or sfxSource is not assigned.
        /// QA FLAG: AudioBank.sfxFootstep requires scene-side SerializeField assignment
        /// on the AudioBank GameObject -- assign footstep.wav in the Inspector
        /// (same workflow as sfxBuild/sfxAlert/sfxAmbient/sfxMine/rainLoop/dangerBgm).
        /// </summary>
        public void PlayFootstep()
        {
            if (sfxFootstep == null || sfxSource == null) return;
            if (Time.time - _lastFootstepTime < FootstepInterval) return;
            _lastFootstepTime = Time.time;
            sfxSource.PlayOneShot(sfxFootstep, 0.45f);
        }

        /// <summary>
        /// Swap the ambient bed to the night clip (cricket/insect).
        /// Called by FootstepDriver when GameClock.DayProgress enters nighttime
        /// (DayProgress >= 0.83 [22:00] or DayProgress &lt; 0.27 [06:30]).
        ///
        /// Idempotent: if already playing the night clip, this is a no-op.
        /// Snap (not crossfade): clip swap is immediate -- the night-to-day
        /// boundary is a single moment, not a gradual twilight audio transition.
        ///
        /// Graceful null no-op if sfxAmbientNight is not assigned. If sfxAmbient
        /// (day clip) is also missing, ambientSource stays silent -- same as the
        /// existing no-ambient case before M6. No crash.
        ///
        /// Wiki B8 acceptance: ambient differs at 02:00 vs 12:00.
        ///
        /// QA FLAG: AudioBank.sfxAmbientNight requires scene-side SerializeField
        /// assignment on the AudioBank GameObject -- assign ambient_night.wav.
        /// Without this wire, this method is a silent no-op (graceful degradation).
        /// </summary>
        public void SwapAmbientToNight()
        {
            if (_isNightAmbient) return;          // already night -- idempotent
            if (sfxAmbientNight == null) return;  // graceful null no-op
            if (ambientSource == null) return;

            _isNightAmbient = true;
            ambientSource.clip = sfxAmbientNight;
            ambientSource.loop = true;
            ambientSource.Play();
        }

        /// <summary>
        /// Swap the ambient bed back to the day clip (birds/wind).
        /// Called by FootstepDriver when GameClock.DayProgress enters daytime
        /// (DayProgress in [0.27, 0.83) -- sunrise 06:30 to 22:00).
        ///
        /// Idempotent: if already playing the day clip, this is a no-op.
        /// Graceful: if sfxAmbient is null, ambientSource is stopped (no day clip
        /// assigned -- same as prototype-day-1 no-wiring state, no crash).
        ///
        /// Wiki B8 acceptance: ambient differs at 02:00 vs 12:00 (daytime = birds).
        /// </summary>
        public void SwapAmbientToDay()
        {
            if (!_isNightAmbient) return;   // already day -- idempotent
            if (ambientSource == null) return;

            _isNightAmbient = false;
            if (sfxAmbient != null)
            {
                ambientSource.clip = sfxAmbient;
                ambientSource.loop = true;
                ambientSource.Play();
            }
            else
            {
                ambientSource.Stop();
            }
        }

        // --------------------------------------------------------------------
        //  M6 NEW METHODS (wiki B9, W-M6-03 B9 lane)
        // --------------------------------------------------------------------

        /// <summary>
        /// Soft door creak/thud -- called by DoorEntity.NotifyPassing() on pawn pass-through.
        ///
        /// Wiki B9 acceptance: "Opening a door plays a distinct sound."
        ///
        /// Throttle: 0.15s (DoorInterval). DoorEntity.OpenHintDuration is 0.3s; the
        /// 0.15s bank guard prevents a pawn whose enter/exit straddles a frame boundary
        /// from triggering two sounds within a single visual open-hint window.
        ///
        /// Volume 0.75: door sound should be clearly audible but not above SFX impacts.
        ///
        /// Graceful null no-op if sfxDoor or sfxSource is not assigned.
        /// QA FLAG: AudioBank.sfxDoor requires scene-side Inspector assignment on the
        /// AudioBank GameObject -- assign door.wav. DoorEntity.NotifyPassing() calls
        /// AudioBank.Instance?.PlayDoor(). Without wire: graceful null no-op.
        /// </summary>
        public void PlayDoor()
        {
            if (sfxDoor == null || sfxSource == null) return;
            if (Time.time - _lastDoorTime < DoorInterval) return;
            _lastDoorTime = Time.time;
            sfxSource.PlayOneShot(sfxDoor, 0.75f);
        }

        /// <summary>
        /// Sizzle/clatter cooking sound -- called by StoveEntity.CookOne() on each
        /// successful cook action.
        ///
        /// Wiki B9 acceptance: "Cooking at a stove plays a distinct sound."
        ///
        /// Throttle: 0.5s (CookInterval). PawnCook outer cookInterval is 1.5s, so
        /// this is well within normal use.  Bank-side guard covers edge cases where
        /// two pawns share a stove or a rapid API call completes multiple cooks in
        /// quick succession.
        ///
        /// Volume 0.70: cooking sound is foreground feedback, clearly audible.
        ///
        /// Graceful null no-op if sfxCook or sfxSource is not assigned.
        /// QA FLAG: AudioBank.sfxCook requires scene-side Inspector assignment on the
        /// AudioBank GameObject -- assign cook.wav. StoveEntity.CookOne() calls
        /// AudioBank.Instance?.PlayCook(). Without wire: graceful null no-op.
        /// </summary>
        public void PlayCook()
        {
            if (sfxCook == null || sfxSource == null) return;
            if (Time.time - _lastCookTime < CookInterval) return;
            _lastCookTime = Time.time;
            sfxSource.PlayOneShot(sfxCook, 0.70f);
        }

        /// <summary>
        /// Bow-twang arrow-launch -- called by ArrowProjectile.SpawnArrow() on arrow fire.
        ///
        /// Wiki B9 acceptance: "Firing an arrow plays a distinct sound."
        ///
        /// Throttle: none (ShootInterval=0.0). Each arrow spawn is a discrete,
        /// intentional event (one per PawnHunter bow-draw cycle, not a tight loop).
        /// No global throttle -- rapid manual fire by scripted tests is acceptable and
        /// each arrow is a distinct event that should be heard.
        ///
        /// Volume 0.80: bow-twang is a satisfying action-feedback sound, prominent.
        ///
        /// Graceful null no-op if sfxShoot or sfxSource is not assigned.
        /// QA FLAG: AudioBank.sfxShoot requires scene-side Inspector assignment on the
        /// AudioBank GameObject -- assign shoot.wav. ArrowProjectile.SpawnArrow() calls
        /// AudioBank.Instance?.PlayShoot(). Without wire: graceful null no-op.
        /// </summary>
        public void PlayShoot()
        {
            if (sfxShoot == null || sfxSource == null) return;
            sfxSource.PlayOneShot(sfxShoot, 0.80f);
        }
    }
}
