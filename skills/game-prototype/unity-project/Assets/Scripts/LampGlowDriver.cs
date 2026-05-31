using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M4-04 Lane A — wiki Dimension 4 (건축) #19:
    ///   "Add torch/standing-lamp buildable (night light pool)."
    ///   Acceptance: a built lamp emits a light glow at night.
    ///
    /// Self-attaching glow driver for LampEntity, deliberately MIRRORING
    /// NightLightPoolDriver.cs rather than editing it.
    ///
    /// Why a separate driver (NOT an edit to NightLightPoolDriver):
    ///   NightLightPoolDriver scans ONLY for StoveEntity and gates on
    ///   stove.MealsAvailable / CanCookOne() — it cannot auto-detect a lamp
    ///   without a code change, and the W-M4-04 Lane A contract explicitly
    ///   forbids editing NightLightPoolDriver.cs / DayNightCycle / FlickerLight.
    ///   So this dedicated driver scans for LampEntity and casts the same warm
    ///   amber radial pool with the same day/night alpha curve, the same
    ///   sortingOrder (8 — below pawns), the same alpha-blend material, and the
    ///   same procedural-sprite construction.  Visually interchangeable with the
    ///   stove glow; only the scan target + the always-lit gate differ.
    ///
    /// Pattern (identical to NightLightPoolDriver / FlickerLight / WeatherParticleDriver):
    ///   - [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] — NO SceneSetup edit.
    ///   - Hidden DontDestroyOnLoad GameObject.
    ///   - FindFirstObjectByType idempotency guard.
    ///   - Read-only poll of GameClock + LampEntity each scan; deterministic.
    ///   - Time.unscaledTime deltas, never WaitForSeconds (no background-freeze
    ///     risk — there is no long-running coroutine here).
    ///
    /// Render order:
    ///   Sorting order 8 — above ground tiles (0-7), BELOW pawns (9-11), below
    ///   the lamp body sprite (lamp uses order 5, same as stove) so the pool
    ///   paints on the floor and never washes a pawn or the lamp itself.
    ///
    /// >>> QA FLAG: at night a built lamp must show a warm amber glow pool on the
    ///     ground around it; by day the pool is not visible; the pool does not
    ///     wash over a pawn silhouette.  (No scene wiring needed — this driver
    ///     self-bootstraps and builds its sprite/material procedurally, exactly
    ///     like NightLightPoolDriver's runtime fallback path.)
    /// </summary>
    public class LampGlowDriver : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Day/night 커브는 공용 NightCurve(LampEntity.cs) 로 통합(2026-06-01). //
        //  바닥 풀이 LampLight / NightOverlay 와 동일 위상으로 페이드된다.      //
        // ------------------------------------------------------------------ //

        // ------------------------------------------------------------------ //
        //  Glow appearance tunables (match NightLightPoolDriver)              //
        // ------------------------------------------------------------------ //

        // Max sprite alpha at full night.  A lamp's whole job is light, so it
        // reads slightly warmer/stronger than the incidental stove glow (0.42).
        private const float MaxGlowAlpha = 0.46f;

        // World-space diameter of the glow pool, in cells.  A standing lamp
        // lights a small room corner — a touch wider than the stove pool (2.5).
        private const float GlowScale = 2.8f;

        // Small Z-offset below the lamp sprite (2D uses sortingOrder, harmless).
        private const float LocalZ = 0.01f;

        // Above ground tiles (0-7), below pawns (9-11) and the lamp body (5).
        private const int GlowSortingOrder = 8;

        // Rescan interval — pick up lamps built mid-game.
        private const float ScanInterval = 1.5f;

        // ------------------------------------------------------------------ //
        //  Runtime state                                                       //
        // ------------------------------------------------------------------ //

        private readonly Dictionary<LampEntity, SpriteRenderer> _glowMap =
            new Dictionary<LampEntity, SpriteRenderer>(8);

        private Sprite   _glowSprite;
        private Material _glowMat;
        private float    _nextScan;

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

                var go = new GameObject("~LampGlowDriver");
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);
                go.AddComponent<LampGlowDriver>();
                    });
        }

        private static LampGlowDriver FindDriver()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<LampGlowDriver>();
#else
            return Object.FindObjectOfType<LampGlowDriver>();
#endif
        }

        // ------------------------------------------------------------------ //
        //  Lifecycle                                                           //
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            _glowSprite = BuildProceduralGlowSprite();
            _glowMat    = CreateGlowMaterial();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + ScanInterval;
                ScanLamps();
            }

            float dayAlpha = ComputeNightAlpha();
            float alpha    = dayAlpha * MaxGlowAlpha;

            foreach (var kv in _glowMap)
            {
                var lamp = kv.Key;
                var sr   = kv.Value;
                if (sr == null) continue;

                bool isLit = (lamp != null) && lamp.IsLit;
                float finalAlpha = isLit ? alpha : 0f;

                if (lamp != null)
                {
                    // Centre the pool a little above the lamp origin, toward the
                    // flame head, so the light reads as coming from the flame.
                    sr.transform.position = new Vector3(
                        lamp.transform.position.x,
                        lamp.transform.position.y + LampEntity.FlameHeightCells,
                        lamp.transform.position.z + LocalZ);
                }

                Color c = sr.color;
                c.a = finalAlpha;
                sr.color = c;
            }
        }

        private void OnDestroy()
        {
            foreach (var kv in _glowMap)
            {
                if (kv.Value != null)
                    Destroy(kv.Value.gameObject);
            }
            _glowMap.Clear();
        }

        // ------------------------------------------------------------------ //
        //  Lamp scan                                                           //
        // ------------------------------------------------------------------ //

        private void ScanLamps()
        {
            // Remove dead entries (lamp deconstructed / destroyed mid-game).
            var toRemove = new List<LampEntity>(4);
            foreach (var kv in _glowMap)
            {
                if (kv.Key == null || kv.Value == null)
                    toRemove.Add(kv.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                var dead = toRemove[i];
                if (_glowMap.TryGetValue(dead, out var sr) && sr != null)
                    Destroy(sr.gameObject);
                _glowMap.Remove(dead);
            }

#if UNITY_2023_1_OR_NEWER
            var lamps = Object.FindObjectsByType<LampEntity>(FindObjectsSortMode.None);
#else
            var lamps = Object.FindObjectsOfType<LampEntity>();
#endif
            for (int i = 0; i < lamps.Length; i++)
                EnsureGlow(lamps[i]);
        }

        private void EnsureGlow(LampEntity lamp)
        {
            if (lamp == null) return;
            if (_glowMap.ContainsKey(lamp)) return;

            // Child of this driver's GO (not of the lamp) so no entity file is
            // modified.  Update repositions it to track the lamp each frame.
            var go = new GameObject("LampGlowPool");
            go.transform.SetParent(transform, worldPositionStays: true);
            go.transform.position = new Vector3(
                lamp.transform.position.x,
                lamp.transform.position.y + LampEntity.FlameHeightCells,
                lamp.transform.position.z + LocalZ);
            go.transform.localScale = Vector3.one * GlowScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _glowSprite;
            sr.material     = _glowMat;
            sr.sortingOrder = GlowSortingOrder;
            sr.color = new Color(1f, 1f, 1f, 0f);  // fades in as night falls

            _glowMap[lamp] = sr;
        }

        // ------------------------------------------------------------------ //
        //  Day/night alpha interpolation (read-only from GameClock)           //
        // ------------------------------------------------------------------ //

        private static float ComputeNightAlpha()
        {
            if (GameClock.Instance == null) return 0f;
            // 공용 NightCurve: 한낮 0 … 한밤 1.  IsLit 게이트가 낮엔 0 으로 끈다.
            return NightCurve.Sample(GameClock.Instance.DayProgress);
        }

        // ------------------------------------------------------------------ //
        //  Sprite + material construction (procedural — no asset dependency)   //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Build a 32x32 radial warm glow sprite in code — identical content to
        /// NightLightPoolDriver.BuildProceduralGlowSprite (FIRE_LT center fading
        /// to FIRE_OR edge, cosine alpha falloff).  Procedural so it resolves in
        /// batchmode AND a player build with zero Resources/AssetDatabase
        /// dependency (the magenta-fallback trap Lane B is fixing for scatter).
        /// </summary>
        private static Sprite BuildProceduralGlowSprite()
        {
            const int SIZE = 32;
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.name       = "LampGlowPool_Proc";

            float cx = SIZE * 0.5f;
            float cy = SIZE * 0.5f;
            float rMax = SIZE * 0.5f;

            var pixels = new Color32[SIZE * SIZE];
            for (int y = 0; y < SIZE; y++)
            {
                for (int x = 0; x < SIZE; x++)
                {
                    float dx   = (x + 0.5f) - cx;
                    float dy   = (y + 0.5f) - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist >= rMax)
                    {
                        pixels[y * SIZE + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float t       = dist / rMax;
                    float falloff = 0.5f * (1f + Mathf.Cos(Mathf.PI * t));

                    // FIRE_LT (250,196,96) at center, FIRE_OR (232,120,44) at edge.
                    byte r = (byte)Mathf.RoundToInt(Mathf.Lerp(250f, 232f, t));
                    byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(196f, 120f, t));
                    byte b = (byte)Mathf.RoundToInt(Mathf.Lerp(96f,  44f,  t));
                    byte a = (byte)Mathf.RoundToInt(falloff * 220f);

                    pixels[y * SIZE + x] = new Color32(r, g, b, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false);

            return Sprite.Create(
                tex,
                new Rect(0, 0, SIZE, SIZE),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 32f);
        }

        /// <summary>
        /// Alpha-blended material (SrcAlpha/OneMinusSrcAlpha, NOT additive) —
        /// same guard as NightLightPoolDriver.CreateGlowMaterial: additive on a
        /// dark scene washes white instead of pooling warm amber.
        /// </summary>
        private static Material CreateGlowMaterial()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");

            var mat = new Material(shader) { name = "LampGlowPoolMat" };

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
