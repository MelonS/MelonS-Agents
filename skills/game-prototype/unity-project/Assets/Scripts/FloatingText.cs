using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M6-01 Lane B6 — wiki Dimension 6 'Floating combat/work text':
    ///   "Damage numbers on hit, '+5 wood' on harvest, rising-fade popup."
    ///
    /// Binary acceptance (B6): an arrow hit (or QA-invoked Spawn) produces a
    ///   colored number that rises ~0.5 world-unit and fades alpha 1->0 over
    ///   ~1 s above the target.
    ///
    /// PUBLIC API — the ONLY entry point:
    ///   FloatingText.Spawn(Vector3 worldPos, string text, Color color)
    ///
    /// The call-sites (ArrowProjectile hit -> red "-N"; PawnChopper/PawnGatherer
    /// harvest -> green "+N wood") are a FOLLOW-UP wave; this lane does NOT
    /// hook those events.  QA verifies binary acceptance by calling Spawn
    /// directly from a test or the Unity console.
    ///
    /// Self-bootstrap pattern (mirrors NightLightPoolDriver / ScatterVarietyDriver):
    ///   [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] creates a single hidden
    ///   DontDestroyOnLoad host GO.  FindFirstObjectByType idempotency guard
    ///   prevents double-spawn on scene-reload / domain-reload-disabled play.
    ///   No SceneSetup edit required.
    ///
    /// Rendering:
    ///   Uses a procedurally built TextMesh (world-space) on its own child GO.
    ///   sortingOrder 50 — above pawns (9-11), alert stack (~20), floating bars
    ///   (29-31), NightOverlay (25).  This matches the "above pawns" contract.
    ///   Font: Unity's built-in Arial (Resources.GetBuiltinResource).  No
    ///   external font asset required; no .meta/.material import needed at
    ///   runtime (the built-in font is always available in batchmode and builds).
    ///
    /// Time:
    ///   Time.unscaledDeltaTime — immune to TimeScale pauses/speed changes.
    ///
    /// Lane contract (W-M6-01 B6):
    ///   - This file is the ONLY file owned by Lane B6.
    ///   - No existing file is edited.
    ///   - No SceneSetup*.cs / prefab / entity file is touched.
    ///   - ArrowProjectile.cs / PawnChopper.cs / PawnGatherer.cs — NOT wired
    ///     (follow-up wave).
    ///   - AudioBank.cs — NOT touched.
    ///   - No external font/material asset is required; built-in Arial is used.
    ///
    /// >>> QA FLAG: FloatingText.Spawn exists and is callable.  Suggested console
    ///     test: FloatingText.Spawn(Camera.main.transform.position + Vector3.up,
    ///     "-5", Color.red);
    /// >>> QA FLAG: eventual call-sites (arrow hit -> red '-N', harvest -> '+N wood')
    ///     are a follow-up wave; not wired in this file.
    /// >>> QA FLAG: TextMesh uses Unity built-in Arial; no external font asset or
    ///     .meta file is needed.  If the project has a custom font asset the
    ///     follow-up wave should swap it in.
    /// >>> QA FLAG: sortingOrder 50 places text above all known layers; confirm
    ///     no UI Canvas z-fighting in the built player.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Tunables                                                            //
        // ------------------------------------------------------------------ //

        // Total duration of the rise-and-fade animation (seconds).
        private const float Lifetime = 1.0f;

        // Total upward distance travelled over the lifetime (world units).
        private const float RiseDistance = 0.5f;

        // TextMesh character size (world units — chosen so a 2-digit number
        // reads clearly at typical camera zoom without dominating the scene).
        private const float CharSize = 0.07f;

        // Font size for the TextMesh component.
        private const int FontSize = 24;

        // sortingOrder: above pawns (9-11), bars (29-31), NightOverlay (25).
        private const int SortingOrder = 50;

        // ------------------------------------------------------------------ //
        //  Per-instance state (one FloatingText component per popup)           //
        // ------------------------------------------------------------------ //

        private float      _elapsed;
        private Color      _startColor;
        private TextMesh   _mesh;
        private Renderer   _renderer;
        private Vector3    _startPos;

        // ------------------------------------------------------------------ //
        //  Bootstrap — hidden singleton host GO (no SceneSetup edit)           //
        // ------------------------------------------------------------------ //

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Game-scene gate: never spawn on MainMenu (operator 2026-05-30).
            GameSceneGate.RunWhenGameScene(() =>
            {
                // Idempotency guard — only one host GO ever lives in the scene.
                if (FindHost() != null) return;

                var go = new GameObject("~FloatingTextHost");
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);
                go.AddComponent<FloatingTextHost>();
                    });
        }

        private static FloatingTextHost FindHost()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<FloatingTextHost>();
#else
            return Object.FindObjectOfType<FloatingTextHost>();
#endif
        }

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Spawn a floating text popup at <paramref name="worldPos"/> with the
        /// given <paramref name="text"/> and <paramref name="color"/>.
        /// The popup rises ~0.5 world-unit and fades alpha 1->0 over ~1 s,
        /// then self-destroys.
        ///
        /// Call-site examples (follow-up wave):
        ///   arrow hit  -> FloatingText.Spawn(target.transform.position, "-5",  Color.red);
        ///   harvest    -> FloatingText.Spawn(pawn.transform.position,   "+5 wood", Color.green);
        /// </summary>
        public static void Spawn(Vector3 worldPos, string text, Color color)
        {
            // Create a dedicated child GO for this single popup.
            var go = new GameObject("FloatingText_" + text);

            // Position slightly above the target so the text starts at the
            // top of the entity sprite rather than at its pivot.
            go.transform.position = new Vector3(worldPos.x, worldPos.y + 0.3f, 0f);

            // Build the TextMesh on a child so we can set sortingOrder via
            // MeshRenderer without fighting the parent transform.
            var ft = go.AddComponent<FloatingText>();
            ft.Init(text, color);
        }

        // ------------------------------------------------------------------ //
        //  Initialisation (called immediately after AddComponent)             //
        // ------------------------------------------------------------------ //

        private void Init(string text, Color color)
        {
            _startColor = color;
            _startPos   = transform.position;

            // TextMesh renders in world space at the GO's position.
            _mesh = gameObject.AddComponent<TextMesh>();
            _mesh.text      = text;
            _mesh.fontSize  = FontSize;
            _mesh.characterSize = CharSize;
            _mesh.anchor    = TextAnchor.MiddleCenter;
            _mesh.alignment = TextAlignment.Center;
            _mesh.color     = color;

            // Built-in Arial — always present in Unity, no external asset needed.
            _mesh.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Set sortingOrder on the MeshRenderer that TextMesh drives.
            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
                _renderer.sortingOrder = SortingOrder;
        }

        // ------------------------------------------------------------------ //
        //  Per-frame update — rise + fade                                      //
        // ------------------------------------------------------------------ //

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;

            float t = _elapsed / Lifetime; // 0 at spawn, 1 at end

            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            // Rise: linear upward movement.
            float rise = RiseDistance * t;
            transform.position = new Vector3(
                _startPos.x,
                _startPos.y + rise,
                0f);

            // Fade: alpha 1 -> 0 linearly.
            float alpha = Mathf.Lerp(1f, 0f, t);
            if (_mesh != null)
            {
                Color c = _startColor;
                c.a = alpha;
                _mesh.color = c;
            }
        }
    }

    // ------------------------------------------------------------------ //
    //  FloatingTextHost — trivial MonoBehaviour that marks the hidden     //
    //  bootstrap GO so FindFirstObjectByType can find it for the           //
    //  idempotency guard.  Contains no logic.                             //
    // ------------------------------------------------------------------ //

    internal sealed class FloatingTextHost : MonoBehaviour { }
}
