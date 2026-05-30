using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M3-03 Lane D — wiki acceptance #14:
    ///   "During a storm a rain particle layer is visible; clear weather has none."
    ///
    /// Self-attaching driver that creates a Unity ParticleSystem ENTIRELY IN CODE
    /// (no prefab, no scene edit) and follows the main camera so rain covers the
    /// visible area.  Polls WeatherController.Instance.Current read-only each frame;
    /// transitions emission on/off with a _lastKind state guard (no per-frame restart).
    ///
    /// Pattern: mirrors RainSoundDriver.cs / FlickerLight.cs bootstrap approach —
    /// hidden DontDestroyOnLoad GameObject, [RuntimeInitializeOnLoadMethod(AfterSceneLoad)],
    /// FindFirstObjectByType guard for idempotency.
    ///
    /// Lane contract (W-M3-03 Lane D):
    ///   - This file is the ONLY file owned by Lane D.
    ///   - WeatherController.cs is NOT edited.
    ///   - AudioBank.cs / RainSoundDriver.cs / any SceneSetup*.cs / entity/prefab: NOT touched.
    ///   - No particle texture file needed — driver uses Unity built-in white particle sprite.
    ///
    /// Palette / render notes:
    ///   - Rain tint: cool desaturated blue-grey (WATER_* family), low alpha ~0.40.
    ///   - Render order 26 = above NightOverlay (25) and ground (0-7) but BELOW
    ///     PawnFloatingBars (29-31) and UI Canvas (200).  Pawns sit at 9-11; rain is
    ///     at 26, so it draws above pawns in z-order.  Low alpha ensures the pawn
    ///     silhouette is still fully legible.
    /// </summary>
    public class WeatherParticleDriver : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Tunable constants                                                  //
        // ------------------------------------------------------------------ //

        // Emission rate while storm is active.
        private const float EmissionRate    = 120f;

        // Particle lifetime — streaks cross the screen in ~0.6s at the velocity below.
        private const float Lifetime        = 0.65f;

        // Downward velocity (world-units / s).  Negative Y = falling down.
        private const float VelocityY       = -7.5f;

        // Slight horizontal drift (wind).
        private const float VelocityX       = -0.8f;

        // Spawn spread around the camera: half-extents in world units.
        // Sized to cover a typical 16-wide camera view with overshoot so particles
        // do not pop into existence at the edge.
        private const float SpawnHalfW      = 12f;
        private const float SpawnHalfH      = 1.5f;  // spawn above camera top

        // Vertical offset of the spawn plane above camera center (world units).
        private const float SpawnAboveCam   = 5.5f;

        // Particle size — small elongated streaks.
        private const float SizeX           = 0.045f;  // thin
        private const float SizeY           = 0.22f;   // tall streak

        // Rain tint: cool blue-grey, low alpha so it reads as atmosphere.
        // WATER_* palette family: hue ~205°, S 0.28, V 0.70, alpha 0.40.
        private static readonly Color RainColor = new Color(0.565f, 0.647f, 0.706f, 0.40f);

        // Sorting order above NightOverlay (25) and below PawnFloatingBars (29).
        private const int SortingOrder      = 26;

        // ------------------------------------------------------------------ //
        //  Runtime state                                                       //
        // ------------------------------------------------------------------ //

        private ParticleSystem   _ps;
        private WeatherKind      _lastKind   = WeatherKind.Clear;
        private bool             _initialized = false;

        // ------------------------------------------------------------------ //
        //  Bootstrap (no SceneSetup edit required)                            //
        // ------------------------------------------------------------------ //

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Game-scene gate: never spawn on MainMenu (operator 2026-05-30).
            GameSceneGate.RunWhenGameScene(() =>
            {
                if (FindDriver() != null) return;

                var go = new GameObject("~WeatherParticleDriver");
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);
                go.AddComponent<WeatherParticleDriver>();
                    });
        }

        private static WeatherParticleDriver FindDriver()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<WeatherParticleDriver>();
#else
            return Object.FindObjectOfType<WeatherParticleDriver>();
#endif
        }

        // ------------------------------------------------------------------ //
        //  Lifecycle                                                           //
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            BuildParticleSystem();
        }

        private void Update()
        {
            var wc = WeatherController.Instance;
            if (wc == null) return;

            WeatherKind current = wc.Current;

            // Transition guard — only act on weather state changes.
            if (!_initialized || current != _lastKind)
            {
                _initialized = true;
                _lastKind    = current;
                ApplyWeatherState(current);
            }

            // Keep the particle emitter positioned above the main camera so rain
            // covers the full viewport regardless of camera movement.
            if (_ps != null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    var tf = _ps.transform;
                    var cp = cam.transform.position;
                    tf.position = new Vector3(cp.x, cp.y + SpawnAboveCam, cp.z - 1f);
                }
            }
        }

        // ------------------------------------------------------------------ //
        //  State transitions                                                   //
        // ------------------------------------------------------------------ //

        private void ApplyWeatherState(WeatherKind kind)
        {
            if (_ps == null) return;

            if (kind == WeatherKind.Storm)
            {
                // Resume emission.
                var emission = _ps.emission;
                emission.rateOverTime = EmissionRate;

                if (!_ps.isPlaying)
                    _ps.Play();
            }
            else
            {
                // Clear weather: stop new particles, allow existing ones to expire.
                var emission = _ps.emission;
                emission.rateOverTime = 0f;

                // Clear any in-flight particles immediately so there is no lingering
                // rain after the storm ends.
                _ps.Clear();
                _ps.Stop(withChildren: false,
                         stopBehavior: ParticleSystemStopBehavior.StopEmitting);
            }
        }

        // ------------------------------------------------------------------ //
        //  ParticleSystem construction (100% in code, no prefab/scene edit)  //
        // ------------------------------------------------------------------ //

        private void BuildParticleSystem()
        {
            // Attach to this driver's GameObject.
            _ps = gameObject.AddComponent<ParticleSystem>();

            // ---- Main module ------------------------------------------------
            var main = _ps.main;
            main.loop               = true;
            main.playOnAwake        = false;   // we start explicitly on Storm.
            main.startLifetime      = Lifetime;
            main.startSpeed         = 0f;      // velocity set via velocityOverLifetime.
            main.startSize          = 1f;      // base; X/Y set in shape + renderer.
            main.startColor         = RainColor;
            main.maxParticles       = 600;
            main.simulationSpace    = ParticleSystemSimulationSpace.World;

            // ---- Emission ---------------------------------------------------
            var emission = _ps.emission;
            emission.enabled        = true;
            emission.rateOverTime   = 0f;       // start silent; ApplyWeatherState sets rate.

            // ---- Shape (spawn plane above camera) ----------------------------
            var shape           = _ps.shape;
            shape.enabled       = true;
            shape.shapeType     = ParticleSystemShapeType.Box;
            shape.scale         = new Vector3(SpawnHalfW * 2f, SpawnHalfH * 2f, 0.01f);
            // Particles spawn across a wide flat box; the driver Update() moves
            // the entire PS transform above the camera each frame.

            // ---- Velocity over lifetime (downward + slight wind drift) -------
            var vel             = _ps.velocityOverLifetime;
            vel.enabled         = true;
            vel.space           = ParticleSystemSimulationSpace.World;
            vel.x               = new ParticleSystem.MinMaxCurve(VelocityX);
            vel.y               = new ParticleSystem.MinMaxCurve(VelocityY);
            vel.z               = new ParticleSystem.MinMaxCurve(0f);

            // ---- Renderer ---------------------------------------------------
            var renderer            = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode     = ParticleSystemRenderMode.Stretch;
            // Stretch mode: lengthScale controls the streak elongation along velocity.
            renderer.lengthScale    = 2.8f;
            renderer.velocityScale  = 0f;
            renderer.cameraVelocityScale = 0f;

            // Use Unity's built-in default particle sprite — no external texture needed.
            // The built-in "Default-Particle" sprite is always available in Unity.
            renderer.material = CreateRainMaterial();

            // Sorting: above ground and NightOverlay (25), below floating bars (29).
            renderer.sortingOrder = SortingOrder;

            // Scale particle to thin-streak aspect.
            main.startSize3D    = true;
            main.startSizeX     = new ParticleSystem.MinMaxCurve(
                SizeX * 0.85f, SizeX * 1.15f);  // slight width variation
            main.startSizeY     = new ParticleSystem.MinMaxCurve(
                SizeY * 0.80f, SizeY * 1.20f);  // slight length variation
            main.startSizeZ     = new ParticleSystem.MinMaxCurve(1f);

            // Slight alpha variation for depth illusion.
            var colorOverLifetime = _ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var alphaGrad = new Gradient();
            alphaGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(RainColor, 0f),
                    new GradientColorKey(RainColor, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f,   0.00f),   // fade in at spawn
                    new GradientAlphaKey(1f,   0.08f),
                    new GradientAlphaKey(1f,   0.85f),
                    new GradientAlphaKey(0f,   1.00f)    // fade out at death
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(alphaGrad);
        }

        /// <summary>
        /// Create a rain streak material.  Uses the Sprites/Default shader with
        /// an alpha-blended configuration so the low-alpha tint does not occlude
        /// game objects.  Additive blend is intentionally avoided — additive on a
        /// dark scene produces a bright white wash rather than a subtle cool veil.
        /// </summary>
        private static Material CreateRainMaterial()
        {
            // Prefer the standard particle shader for correct depth sorting.
            // Fall back to Sprites/Default if the particle shader is unavailable
            // (stripped builds or URP without the particle pass).
            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");

            var mat = new Material(shader) { name = "RainParticleMat" };

            // Alpha blend (not additive): SrcAlpha / OneMinusSrcAlpha.
            // Best-effort property set; harmless if the shader omits them.
            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))
                mat.SetFloat("_ZWrite", 0f);

            return mat;
        }
    }
}
