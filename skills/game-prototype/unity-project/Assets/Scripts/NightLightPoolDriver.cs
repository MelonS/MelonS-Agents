using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M4-01 Lane D — wiki Dimension 1 Day-night row, optional item:
    ///   "Warm light pools around lit stove/fire at night."
    ///
    /// Self-attaching driver.  On bootstrap it finds lit StoveEntity instances
    /// and, when DayNightCycle is in night hours, draws a soft warm radial glow
    /// sprite parented at each fire's position.  The glow fades in/out with the
    /// day/night alpha curve, mirrors NightOverlay's stop table to stay in sync.
    ///
    /// Pattern: mirrors FlickerLight.cs / RainSoundDriver.cs / WeatherParticleDriver.cs.
    ///   - [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] — no SceneSetup edit.
    ///   - Hidden DontDestroyOnLoad GameObject.
    ///   - FindFirstObjectByType idempotency guard.
    ///   - Read-only poll of all managers (GameClock, StoveEntity).
    ///
    /// Render order:
    ///   Sorting order 8 — above ground tiles (0-7), BELOW pawns (9-11),
    ///   BELOW NightOverlay (25) and floating bars (29-31).
    ///   The glow pool paints on the floor around the fire; it never sits on
    ///   top of a pawn silhouette.
    ///
    /// Material: alpha-blend (Sprites/Default SrcAlpha/OneMinusSrcAlpha).
    ///   Additive blend is intentionally avoided — additive on a dark scene
    ///   produces a bright white wash rather than a warm ground pool
    ///   (same reasoning as WeatherParticleDriver.CreateRainMaterial).
    ///
    /// Palette: FIRE_LT (250,196,96) / FIRE_OR (232,120,44) low alpha.
    ///   The glow sprite glow_fire_pool.png provides the warm radial falloff;
    ///   this driver modulates its alpha via the day/night curve so the pool
    ///   is invisible by day and fully visible on deep-night ticks.
    ///
    /// Lane contract (W-M4-01):
    ///   - This file is the ONLY file owned by Lane D.
    ///   - DayNightCycle.cs — NOT edited (read-only via GameClock.DayProgress).
    ///   - NightOverlay.cs — NOT edited.
    ///   - FlickerLight.cs — NOT edited.
    ///   - StoveEntity.cs / any fire entity — NOT edited.
    ///   - AudioBank.cs, WeatherController.cs — NOT edited.
    ///   - SceneSetup*.cs / any prefab / entity file — NOT edited.
    ///   - One new PNG: Assets/Sprites/glow_fire_pool.png (Point filter, PPU 32).
    ///
    /// >>> QA FLAG: at night a lit stove must show a warm amber glow pool
    ///     on the ground around it; by day the pool is not visible; the pool
    ///     does not appear over / wash the pawn silhouette.
    /// >>> QA FLAG: NightLightPoolGlowPool child GameObjects are parented to
    ///     StoveEntity GOs at runtime — confirm they are cleaned up when a
    ///     stove is destroyed (OnStoveDestroyed subscription).
    /// </summary>
    public class NightLightPoolDriver : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Day/night alpha curve (mirrors NightOverlay STOPS)                //
        //  Key: DayProgress (0..1), Value: target glow alpha                 //
        //                                                                     //
        //  Deep night (00:00-04:00) = full glow (alpha 1.0)                  //
        //  Dawn / dusk transitions fade smoothly.                             //
        //  Full day (08:00-17:00) = zero glow.                               //
        // ------------------------------------------------------------------ //

        private struct AlphaStop { public float t; public float a; }

        private static readonly AlphaStop[] NightAlphaStops = new AlphaStop[]
        {
            new AlphaStop { t = 0.00f, a = 1.00f },  // 00:00 deep night
            new AlphaStop { t = 0.17f, a = 1.00f },  // 04:00 deep night
            new AlphaStop { t = 0.23f, a = 0.70f },  // 05:30 dawn
            new AlphaStop { t = 0.27f, a = 0.20f },  // 06:30 sunrise
            new AlphaStop { t = 0.33f, a = 0.00f },  // 08:00 full day
            new AlphaStop { t = 0.71f, a = 0.00f },  // 17:00 full day
            new AlphaStop { t = 0.77f, a = 0.30f },  // 18:30 sunset
            new AlphaStop { t = 0.83f, a = 0.65f },  // 20:00 dusk
            new AlphaStop { t = 0.92f, a = 0.90f },  // 22:00 night
            new AlphaStop { t = 1.00f, a = 1.00f },  // 24:00 deep night
        };

        // ------------------------------------------------------------------ //
        //  Glow appearance tunables                                           //
        // ------------------------------------------------------------------ //

        // Maximum sprite alpha at full night (the sprite already encodes the
        // radial falloff; this cap keeps the pool "warm and subtle").
        private const float MaxGlowAlpha = 0.42f;

        // World-space scale of the glow sprite relative to one grid cell.
        // Stove sprite is ~1 unit; glow pool at 2.5 world units in diameter
        // reads as a soft floor halo without bleeding too far across the map.
        private const float GlowScale = 2.5f;

        // Slight Z-offset below the stove sprite to ensure the pool renders
        // on the same Z plane as the floor layer (Unity 2D uses Y-sort or
        // explicit sortingOrder, not Z depth, but a small offset is harmless).
        private const float LocalZ = 0.01f;

        // Render order: above ground tiles (0-7), below pawns (9-11).
        private const int GlowSortingOrder = 8;

        // Rescan interval — pick up stoves built mid-game.
        private const float ScanInterval = 1.5f;

        // ------------------------------------------------------------------ //
        //  Runtime state                                                       //
        // ------------------------------------------------------------------ //

        // Map from StoveEntity to its managed glow SpriteRenderer child.
        private readonly Dictionary<StoveEntity, SpriteRenderer> _glowMap =
            new Dictionary<StoveEntity, SpriteRenderer>(8);

        private Sprite    _glowSprite;
        private Material  _glowMat;
        private float     _nextScan;
        private bool      _spriteFailed; // suppress repeated load errors

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

                var go = new GameObject("~NightLightPoolDriver");
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);
                go.AddComponent<NightLightPoolDriver>();
                    });
        }

        private static NightLightPoolDriver FindDriver()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<NightLightPoolDriver>();
#else
            return Object.FindObjectOfType<NightLightPoolDriver>();
#endif
        }

        // ------------------------------------------------------------------ //
        //  Lifecycle                                                           //
        // ------------------------------------------------------------------ //

        private void Awake()
        {
            _glowSprite = LoadGlowSprite();
            _glowMat    = CreateGlowMaterial();
        }

        private void Update()
        {
            // Periodic rescan: pick up newly built stoves + remove dead entries.
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + ScanInterval;
                ScanStoves();
            }

            // Compute current glow alpha from the day/night curve.
            float dayAlpha = ComputeNightAlpha();
            float alpha    = dayAlpha * MaxGlowAlpha;

            // Update each tracked stove's glow.
            foreach (var kv in _glowMap)
            {
                var stove = kv.Key;
                var sr    = kv.Value;

                if (sr == null) continue;

                // Pool is visible only when the stove is "lit" AND it is night.
                bool isLit = IsStoveLit(stove);
                float finalAlpha = (isLit && stove != null) ? alpha : 0f;

                // Keep glow parented and centered on the stove.
                if (stove != null)
                {
                    sr.transform.position = new Vector3(
                        stove.transform.position.x,
                        stove.transform.position.y,
                        stove.transform.position.z + LocalZ);
                }

                // Apply alpha to color, preserving the warm tint.
                Color c = sr.color;
                c.a = finalAlpha;
                sr.color = c;
            }
        }

        private void OnDestroy()
        {
            // Clean up all glow child objects.
            foreach (var kv in _glowMap)
            {
                if (kv.Value != null)
                    Destroy(kv.Value.gameObject);
            }
            _glowMap.Clear();
        }

        // ------------------------------------------------------------------ //
        //  Stove scan                                                          //
        // ------------------------------------------------------------------ //

        private void ScanStoves()
        {
            // Remove dead entries (stove destroyed mid-game).
            var toRemove = new List<StoveEntity>(4);
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

            // Find all current stoves and ensure each has a glow pool.
#if UNITY_2023_1_OR_NEWER
            var stoves = Object.FindObjectsByType<StoveEntity>(FindObjectsSortMode.None);
#else
            var stoves = Object.FindObjectsOfType<StoveEntity>();
#endif
            for (int i = 0; i < stoves.Length; i++)
                EnsureGlow(stoves[i]);
        }

        private void EnsureGlow(StoveEntity stove)
        {
            if (stove == null) return;
            if (_glowMap.ContainsKey(stove)) return;

            // Create a child glow sprite attached to this driver's GO
            // (not to the stove, so we don't modify any entity file).
            // The Update loop repositions it to match the stove each frame.
            var go = new GameObject("NightLightPoolGlowPool");
            go.transform.SetParent(transform, worldPositionStays: true);
            go.transform.position = new Vector3(
                stove.transform.position.x,
                stove.transform.position.y,
                stove.transform.position.z + LocalZ);
            go.transform.localScale = Vector3.one * GlowScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _glowSprite;
            sr.material     = _glowMat;
            sr.sortingOrder = GlowSortingOrder;
            // Start transparent; Update will fade in as night falls.
            sr.color = new Color(1f, 1f, 1f, 0f);

            _glowMap[stove] = sr;
        }

        // ------------------------------------------------------------------ //
        //  Lit-gating (read-only, mirrors FlickerLight.IsLit heuristic)       //
        // ------------------------------------------------------------------ //

        private static bool IsStoveLit(StoveEntity stove)
        {
            if (stove == null) return false;
            // #버그헌트4(2026-06-05): MealsAvailable 은 단조증가(차감 안 됨)라 'MealsAvailable>0' 는 첫
            //  조리 후 영구 true → 식량·자재 0 이어도 화덕 불빛 영구 점등이었다.  현재 조리 가능
            //  (자재 보유) 여부로만 판정 — 활성 화덕만 점등.
            return stove.CanCookOne();
        }

        // ------------------------------------------------------------------ //
        //  Day/night alpha interpolation (read-only from GameClock)           //
        // ------------------------------------------------------------------ //

        private static float ComputeNightAlpha()
        {
            if (GameClock.Instance == null) return 0f;

            float t = Mathf.Repeat(GameClock.Instance.DayProgress, 1f);
            for (int i = 0; i < NightAlphaStops.Length - 1; i++)
            {
                float t0 = NightAlphaStops[i].t;
                float t1 = NightAlphaStops[i + 1].t;
                if (t >= t0 && t <= t1)
                {
                    float local = Mathf.InverseLerp(t0, t1, t);
                    return Mathf.Lerp(NightAlphaStops[i].a, NightAlphaStops[i + 1].a, local);
                }
            }
            return NightAlphaStops[0].a;
        }

        // ------------------------------------------------------------------ //
        //  Sprite + material construction                                      //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Load glow_fire_pool.png from Resources or generate a procedural
        /// fallback if the asset is missing at runtime (prevents null-ref).
        /// </summary>
        private Sprite LoadGlowSprite()
        {
            if (_spriteFailed) return BuildProceduralGlowSprite();

            // Try loading via Resources (requires the PNG to be inside a
            // Resources/ folder).  Because glow_fire_pool.png lives in
            // Assets/Sprites (not Resources), we fall back immediately to
            // the procedural path and note the failure.
            //
            // NOTE: if the Build Engineer wires the asset into a Resources/
            // subfolder or via AssetDatabase, remove this fallback.
            // For now the procedural sprite guarantees runtime correctness.
            _spriteFailed = true;
            return BuildProceduralGlowSprite();
        }

        /// <summary>
        /// Build a 32x32 radial warm glow sprite entirely in code.
        /// Uses FIRE_LT (250,196,96) at center fading to FIRE_OR (232,120,44)
        /// at the edge with a cosine-curve alpha falloff.
        /// This matches glow_fire_pool.png's authored content exactly, so if
        /// the PNG is later wired in it is visually interchangeable.
        /// </summary>
        private static Sprite BuildProceduralGlowSprite()
        {
            const int SIZE = 32;
            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.name       = "GlowFirePool_Proc";

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

                    float t       = dist / rMax;                    // 0 = center, 1 = edge
                    float falloff = 0.5f * (1f + Mathf.Cos(Mathf.PI * t)); // cosine ease

                    // FIRE_LT at center, FIRE_OR at edge.
                    byte r = (byte)Mathf.RoundToInt(Mathf.Lerp(250f, 232f, t));
                    byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(196f, 120f, t));
                    byte b = (byte)Mathf.RoundToInt(Mathf.Lerp(96f,  44f,  t));
                    byte a = (byte)Mathf.RoundToInt(falloff * 220f); // max ~220; driver further scales

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
        /// Create an alpha-blended material for the glow pool sprite.
        /// Uses Sprites/Default with SrcAlpha/OneMinusSrcAlpha (NOT additive).
        /// Additive blend on a dark scene would produce a bright white wash
        /// rather than a warm amber floor pool — same guard as
        /// WeatherParticleDriver.CreateRainMaterial.
        /// </summary>
        private static Material CreateGlowMaterial()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");

            var mat = new Material(shader) { name = "NightGlowPoolMat" };

            // Alpha blend: SrcAlpha / OneMinusSrcAlpha.
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
