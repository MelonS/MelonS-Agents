using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// Polish Wave v3 / V5 ambient micro-motion — fire flicker.
    ///
    /// Goal: lit stoves (and, as a follow-up, lamps) should visibly *breathe*
    /// so the world stops feeling frozen.  We do NOT recolor / pulse the root
    /// body SpriteRenderer (that renderer carries a tint contract owned by
    /// other systems — selection highlight, blueprint ghost, night overlay).
    /// Instead each lit stove gets a small ADDITIVE child overlay sprite that
    /// sits a hair above the body and gently pulses alpha + scale at ~4 Hz with
    /// low-amplitude value-noise, reading as a living flame rather than a strobe.
    ///
    /// Lane constraint (W-M1-01 lane B): this wave may ONLY add this one file.
    /// Stoves are instantiated at runtime by BuildManager from stovePrefab, and
    /// we may not edit that builder or SceneSetup this wave.  So FlickerLight is
    /// SELF-ATTACHING:
    ///   - a [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] bootstrap spawns a
    ///     hidden driver GameObject;
    ///   - the driver periodically scans for StoveEntity instances that lack a
    ///     FlickerLight and ensures each gets one (handles mid-game-built stoves).
    ///
    /// Lit-gating: StoveEntity exposes no explicit "isLit" flag.  The cleanest
    /// public signals are MealsAvailable (something has been cooked) and
    /// CanCookOne() (fuel/food available to cook now).  We treat a stove as
    /// "lit" when it has cooked at least one meal OR can currently cook — i.e.
    /// it is an active hearth.  When neither holds the flame fades to near-zero
    /// (faint ember) rather than hard-off, so the gating is forgiving.
    /// >>> QA FLAG: confirm this lit-gating heuristic matches design intent.
    ///
    /// Lamp: there is a lamp.png sprite but NO LampEntity script and no lamp
    /// GameObject discoverable by name at runtime (grep found none).  Lamp
    /// flicker is therefore OUT OF SCOPE this wave and flagged as follow-up.
    /// </summary>
    [DisallowMultipleComponent]
    public class FlickerLight : MonoBehaviour
    {
        // ---- tunables (SerializeField so designer can tweak without code) ----
        [Header("Flicker feel")]
        [SerializeField] private float frequencyHz = 4f;       // base flutter rate
        [SerializeField] private float alphaBase = 0.55f;      // overlay alpha when fully lit
        [SerializeField] private float alphaAmplitude = 0.18f; // +/- alpha swing (low = not a strobe)
        [SerializeField] private float scaleBase = 1.0f;       // overlay scale (relative to body)
        [SerializeField] private float scaleAmplitude = 0.08f; // +/- scale swing
        [SerializeField] private float emberAlpha = 0.10f;     // faint glow when not "lit"
        [SerializeField] private Color flameColor = new Color(1f, 0.62f, 0.18f, 1f); // warm orange

        [Header("Overlay placement")]
        [SerializeField] private int sortingOrderOffset = 1;   // sit just above body
        [SerializeField] private Vector3 overlayLocalOffset = new Vector3(0f, 0.08f, 0f);

        // ---- runtime state ----
        private SpriteRenderer bodyRenderer;
        private SpriteRenderer overlayRenderer;
        private Transform overlayTf;
        private StoveEntity stove;

        // Per-instance phase so every stove flickers out of sync (no global strobe).
        private float phaseSeedA;
        private float phaseSeedB;

        private void Awake()
        {
            stove = GetComponent<StoveEntity>();
            bodyRenderer = GetComponent<SpriteRenderer>();

            phaseSeedA = Random.Range(0f, 1000f);
            phaseSeedB = Random.Range(0f, 1000f);

            BuildOverlay();
            BuildEmbers();
        }

        // D3b 몰입 (design-immersion-2026-06-11) — 불티: 위로 떠올라 사그라드는 작은
        //  입자.  점등 게이트는 Update 에서 emission rate 로 (IsLit 폴백 true 인
        //  모닥불은 상시, 화덕은 조리 가능할 때만).
        private ParticleSystem embers;

        private void BuildEmbers()
        {
            var go = new GameObject("Embers");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = overlayLocalOffset;
            embers = go.AddComponent<ParticleSystem>();
            embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = embers.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.78f, 0.30f, 0.9f), new Color(1f, 0.45f, 0.12f, 0.9f));
            main.gravityModifier = -0.06f;        // 미세 상승 (열기류)
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = embers.emission;
            em.enabled = true;
            em.rateOverTime = 0f;                 // Update 에서 점등 게이트

            var shape = embers.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.12f;

            var col = embers.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(new Color(1f, 0.30f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = ParticleFx.SharedMaterial;
            psr.sortingOrder = (bodyRenderer != null ? bodyRenderer.sortingOrder : 20)
                               + sortingOrderOffset + 1;
            embers.Play();
        }

        private void BuildOverlay()
        {
            var go = new GameObject("FlameOverlay");
            overlayTf = go.transform;
            overlayTf.SetParent(transform, worldPositionStays: false);
            overlayTf.localPosition = overlayLocalOffset;
            overlayTf.localRotation = Quaternion.identity;
            overlayTf.localScale = Vector3.one * scaleBase;

            overlayRenderer = go.AddComponent<SpriteRenderer>();

            // Reuse the body's sprite as the flame silhouette — keeps it aligned
            // with the actual fire pixels of the stove art without needing a new
            // asset.  Additive-style warm tint + low alpha makes it read as glow,
            // not as a second opaque body.
            if (bodyRenderer != null)
            {
                overlayRenderer.sprite = bodyRenderer.sprite;
                overlayRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
                overlayRenderer.sortingOrder = bodyRenderer.sortingOrder + sortingOrderOffset;
            }
            else
            {
                overlayRenderer.sortingOrder = 21; // stove body is ~20 in BuildManager
            }

            overlayRenderer.color = WithAlpha(flameColor, alphaBase);

            // Soft-additive look: use additive blend if a particle-additive
            // material/shader is available; otherwise fall back to the default
            // sprite material (still reads as a warm overlay via low alpha).
            var addShader = Shader.Find("Particles/Standard Unlit");
            if (addShader == null) addShader = Shader.Find("Sprites/Default");
            if (addShader != null)
            {
                var mat = new Material(addShader);
                // Best-effort additive blend setup; harmless if properties absent.
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                overlayRenderer.sharedMaterial = mat;
            }
        }

        private bool IsLit()
        {
            if (stove == null) return true; // no entity to gate on -> always subtle flicker
            // #버그헌트4(2026-06-05): MealsAvailable 단조증가라 'MealsAvailable>0' 는 첫 조리 후 영구
            //  true(불빛 영구 점등).  현재 조리 가능 여부로만 — 활성 화덕만 점등.
            return stove.CanCookOne();
        }

        private void Update()
        {
            if (overlayRenderer == null) return;

            float t = Time.time;
            float lit = IsLit() ? 1f : 0f;

            // Two detuned value-noise streams summed -> organic, non-periodic flicker.
            // Mathf.PerlinNoise gives smooth [0,1]; we map to a centered swing.
            float n1 = Mathf.PerlinNoise(phaseSeedA, t * frequencyHz);
            float n2 = Mathf.PerlinNoise(phaseSeedB, t * frequencyHz * 1.7f);
            float noise = (n1 * 0.65f + n2 * 0.35f) * 2f - 1f; // -> ~[-1, 1]

            float targetAlpha = lit > 0.5f
                ? alphaBase + noise * alphaAmplitude
                : emberAlpha + noise * (emberAlpha * 0.4f);
            // TOP-3 (visual-polish-backlog 2026-06-11) — 한낮에 글로우가 풀강도면 빛이
            //  "따뜻함" 신호를 잃는다.  어두울수록 강하게 (낮 30% 바닥, 밤 100%).
            targetAlpha *= Mathf.Lerp(0.3f, 1f, NightOverlay.CurrentDarkness01);
            targetAlpha = Mathf.Clamp01(targetAlpha);

            float scale = scaleBase + (lit > 0.5f ? noise * scaleAmplitude : noise * scaleAmplitude * 0.3f);

            overlayRenderer.color = WithAlpha(flameColor, targetAlpha);
            overlayTf.localScale = Vector3.one * scale;

            // D3b — 불티는 점등 중에만 방출 (꺼진 화덕에서 불티 안 튀게).
            if (embers != null)
            {
                var em = embers.emission;
                em.rateOverTime = lit > 0.5f ? 5f : 0f;
            }
        }

        private void OnDisable()
        {
            // We never touch the body renderer's tint, so there is nothing on the
            // body to restore.  Just hide the overlay cleanly.
            if (overlayRenderer != null)
                overlayRenderer.color = WithAlpha(flameColor, 0f);
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        // ============================================================
        // Self-attach bootstrap + runtime sweep (no SceneSetup edit).
        // ============================================================
        // >>> QA FLAG: confirm FlickerLight actually lands on stoves that are
        //     built MID-GAME (BuildManager instantiates stovePrefab at runtime).
        //     The sweep below rescans periodically so newly-built stoves are
        //     picked up within <scanInterval> seconds.

        private const float ScanInterval = 1.0f; // seconds between rescans

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Game-scene gate: never spawn on MainMenu (operator 2026-05-30).
            GameSceneGate.RunWhenGameScene(() =>
            {
                // Idempotent: avoid spawning a second driver on scene reload / domain
                // reload-disabled play sessions.
                if (FindDriver() != null) return;

                var go = new GameObject("~FlickerLightDriver");
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);
                go.AddComponent<FlickerLightDriver>();
                    });
        }

        private static FlickerLightDriver FindDriver()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<FlickerLightDriver>();
#else
            return Object.FindObjectOfType<FlickerLightDriver>();
#endif
        }

        /// <summary>Ensure the given stove has exactly one FlickerLight.</summary>
        internal static void EnsureOn(StoveEntity s)
        {
            if (s == null) return;
            if (s.GetComponent<FlickerLight>() == null)
                s.gameObject.AddComponent<FlickerLight>();
        }

        /// <summary>
        /// Hidden persistent driver.  Periodically scans for StoveEntity
        /// instances lacking a FlickerLight and attaches one.  This is the
        /// lane-safe alternative to editing BuildManager/stovePrefab.
        ///
        /// runInBackground note (bug-pattern #9): this driver uses Time-based
        /// throttling inside Update (NOT a WaitForSeconds heartbeat coroutine),
        /// so it does not introduce a background-only timer.  AutoScreenshotter
        /// already owns Application.runInBackground for CLI QA paths; we add no
        /// new always-on background timer here.
        /// </summary>
        private sealed class FlickerLightDriver : MonoBehaviour
        {
            private float nextScan;
            // Reused scratch buffer — no per-frame List allocation.
            private static readonly List<StoveEntity> scratch = new List<StoveEntity>(32);

            private void Update()
            {
                if (Time.unscaledTime < nextScan) return;
                nextScan = Time.unscaledTime + ScanInterval;

                scratch.Clear();
#if UNITY_2023_1_OR_NEWER
                var found = Object.FindObjectsByType<StoveEntity>(FindObjectsSortMode.None);
#else
                var found = Object.FindObjectsOfType<StoveEntity>();
#endif
                for (int i = 0; i < found.Length; i++)
                    EnsureOn(found[i]);
            }
        }
    }
}
