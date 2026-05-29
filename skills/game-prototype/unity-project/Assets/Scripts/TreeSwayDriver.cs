using System.Collections.Generic;
using UnityEngine;

namespace MelonS.GameProto
{
    /// <summary>
    /// W-M4-02 Lane C — V5 remainder: tree-canopy sway.
    ///
    /// V5(a) fire-flicker already shipped (FlickerLight.cs).
    /// This file ships V5(b): a very-subtle horizontal sway on each tree's
    /// canopy, ±1 px-equivalent (~0.03-0.05 world units at PPU 16) sine at
    /// ~0.3 Hz, so the world stops looking frozen.
    ///
    /// ROOT-TRANSFORM RULE (mirrors PawnSpriteBob contract):
    ///   TreeEntity reads its own root transform for species scale, HP tint,
    ///   and position is used by scatter-clustering (SpawnTrees minimum distance)
    ///   and contact-shadow anchor, and SaveLoadManager.cs serialises
    ///   transform.position.  PathGrid reads entity positions for obstacle cells.
    ///   Therefore this driver MUST NOT move the tree root transform.
    ///
    /// SPRITE-CHILD pattern (mirrors PawnSpriteBob):
    ///   - Each tree's root SpriteRenderer is left on the root GO but its
    ///     draw is disabled (enabled = false) — it becomes the tint anchor.
    ///     TreeEntity.TakeChopDamage / TintHelper.ApplyHpBrightness still write
    ///     to it via GetComponent, and this driver mirrors root.color → child
    ///     each frame, so chop-tint and species-tint remain visible.
    ///   - A new child GO "TreeSway" is created with its own SpriteRenderer
    ///     (same sprite, same sortingLayerID, same sortingOrder as root).
    ///   - Only the child's localPosition.x is animated.
    ///   - The TreeShadow child (sortingOrder = root − 1) is left untouched;
    ///     shadows correctly stay static while the canopy sways.
    ///
    /// SELF-ATTACH pattern (mirrors FlickerLight / RainSoundDriver / NightLightPoolDriver):
    ///   [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] bootstraps a hidden
    ///   DontDestroyOnLoad driver GO once.  FindFirstObjectByType idempotency
    ///   guard prevents double-spawn on domain-reload-disabled sessions.
    ///   A periodic read-only poll (Time.unscaledTime) finds new TreeEntity
    ///   instances (regrown via RegrowthScheduler) and attaches sway children
    ///   without editing any entity or SceneSetup file.
    ///
    /// SWAY TUNABLES:
    ///   amplitude  = 0.04 world units  (~0.64 px at PPU 16; within ±1 px spec)
    ///   frequencyHz = 0.3 Hz           (spec target; subtle breeze feel)
    ///   Per-tree phase offset derived from GO instanceID for determinism (no
    ///   lock-step sway across the map).  Phase is eased in from zero so there
    ///   is no position-pop on initial attachment.
    ///
    /// ACCEPTANCE (binary, QA reads two frames ~1 s apart):
    ///   1. A tree's canopy shows ≤1 px lateral drift between two frames ~1 s apart.
    ///   2. The tree does NOT drift off its cell (root transform untouched).
    ///   3. Nothing else jitters.
    ///   4. The sway recedes — it does not out-move the pawns.
    ///
    /// Lane contract (W-M4-02 Lane C):
    ///   This file is the ONLY file owned by Lane C.
    ///   NOT edited: TreeEntity.cs, FlickerLight.cs, _gen_sprites.py,
    ///   any stove/fire entity, any SceneSetup/prefab/entity file,
    ///   SaveLoadManager.cs, MineDesignation/miner, or any other lane's files.
    ///
    /// >>> QA FLAG: TreeSway child GOs are parented to each TreeEntity GO at
    ///     runtime. Confirm they are removed when a tree is destroyed
    ///     (GOs are children of the tree, so Unity's Destroy(tree.gameObject)
    ///     cascades — no manual cleanup needed, but QA should verify no orphan
    ///     "TreeSway" GOs remain after a chop).
    /// >>> QA FLAG: root SpriteRenderer.enabled = false means TreeEntity's own
    ///     visual is hidden; only the child SR draws. Confirm species tint
    ///     (SetSpecies) and chop-damage tint (TakeChopDamage) are still
    ///     visible via the per-frame color mirror in TreeSwayChild.Update.
    /// >>> QA FLAG: scene-wiring — zero SceneSetup edits; self-attaches via
    ///     RuntimeInitializeOnLoadMethod. Confirm no double-attach on scene
    ///     reload (idempotency guard).
    /// </summary>
    public class TreeSwayDriver : MonoBehaviour
    {
        // ------------------------------------------------------------------ //
        //  Sway tunables                                                       //
        // ------------------------------------------------------------------ //

        // Horizontal sway amplitude in world units.
        // PPU 16 → 1 world unit = 16 px → 0.04 wu = 0.64 px ≈ 1 px (spec ±1 px).
        private const float SwayAmplitude = 0.04f;

        // Sway frequency in Hz.  0.3 Hz = gentle breeze per spec.
        private const float SwayFrequencyHz = 0.3f;

        // How fast the live amplitude eases in from zero on first attach,
        // preventing a position pop when the child is created mid-game.
        private const float EaseLerpSpeed = 2.0f;

        // Rescan interval: pick up trees regrown via RegrowthScheduler.
        private const float ScanInterval = 2.0f;

        // ------------------------------------------------------------------ //
        //  Runtime state                                                       //
        // ------------------------------------------------------------------ //

        // One entry per tracked tree.
        private readonly List<TreeSwayEntry> _entries = new List<TreeSwayEntry>(64);

        private float _nextScan;

        // ------------------------------------------------------------------ //
        //  Bootstrap (no SceneSetup edit required)                            //
        // ------------------------------------------------------------------ //

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindDriver() != null) return;

            var go = new GameObject("~TreeSwayDriver");
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(go);
            go.AddComponent<TreeSwayDriver>();
        }

        private static TreeSwayDriver FindDriver()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<TreeSwayDriver>();
#else
            return Object.FindObjectOfType<TreeSwayDriver>();
#endif
        }

        // ------------------------------------------------------------------ //
        //  Lifecycle                                                           //
        // ------------------------------------------------------------------ //

        private void Update()
        {
            // Periodic rescan: pick up newly regrown trees + prune dead entries.
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + ScanInterval;
                ScanTrees();
            }

            float time = Time.time;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];

                // Prune dead trees (chopped down).
                if (e.Tree == null || e.Child == null)
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                e.Update(time, SwayAmplitude, SwayFrequencyHz, EaseLerpSpeed);
            }
        }

        private void OnDestroy()
        {
            // Children are parented to tree GOs (not to this driver), so Unity
            // will not auto-destroy them here.  We null-check and destroy any
            // remaining sway children when the driver itself is torn down.
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.Child != null)
                    Destroy(e.Child);
            }
            _entries.Clear();
        }

        // ------------------------------------------------------------------ //
        //  Tree scan                                                           //
        // ------------------------------------------------------------------ //

        private void ScanTrees()
        {
            // Remove stale entries (tree was chopped).
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].Tree == null || _entries[i].Child == null)
                    _entries.RemoveAt(i);
            }

#if UNITY_2023_1_OR_NEWER
            var trees = Object.FindObjectsByType<TreeEntity>(FindObjectsSortMode.None);
#else
            var trees = Object.FindObjectsOfType<TreeEntity>();
#endif

            for (int i = 0; i < trees.Length; i++)
                EnsureSway(trees[i]);
        }

        private void EnsureSway(TreeEntity tree)
        {
            if (tree == null) return;

            // Already tracked?
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Tree == tree) return;
            }

            // Build the sway child and register the entry.
            var entry = TreeSwayEntry.Create(tree);
            if (entry != null)
                _entries.Add(entry);
        }

        // ------------------------------------------------------------------ //
        //  Per-tree sway state                                                 //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Encapsulates the per-tree sway child and its animation state.
        /// Not a MonoBehaviour — driven directly by TreeSwayDriver.Update
        /// to avoid the cost of one MonoBehaviour per tree.
        /// </summary>
        private sealed class TreeSwayEntry
        {
            public TreeEntity Tree;
            public GameObject Child; // "TreeSway" child GO

            private SpriteRenderer _rootSr;   // tint anchor (draw-disabled)
            private SpriteRenderer _childSr;  // visible, swayed
            private Transform      _childTf;

            // Per-tree deterministic phase offset (derived from GO instanceID).
            private float _phaseOffset;

            // Eased live amplitude (ramps from 0 → SwayAmplitude on attach).
            private float _liveAmplitude;

            // Last colour pushed to child (skip redundant writes).
            private Color _lastColor;
            private bool  _hasPushedColor;

            private TreeSwayEntry() { }

            /// <summary>
            /// Create the sway child for the given tree, or return null if the
            /// tree's root SpriteRenderer cannot be located.
            /// </summary>
            internal static TreeSwayEntry Create(TreeEntity tree)
            {
                var rootSr = tree.GetComponent<SpriteRenderer>();
                if (rootSr == null) return null;

                // Deterministic per-tree phase: spread trees across a full
                // 2π cycle using the last 8 bits of the instanceID so the map
                // of 45 trees gets good coverage without pseudo-randomness.
                int id = tree.gameObject.GetInstanceID();
                float phaseOffset = (id & 0xFF) / 256f * Mathf.PI * 2f;

                // Build child GO parented to the tree root.
                var childGo = new GameObject("TreeSway");
                childGo.transform.SetParent(tree.transform, worldPositionStays: false);
                childGo.transform.localPosition = Vector3.zero;
                childGo.transform.localRotation = Quaternion.identity;
                childGo.transform.localScale    = Vector3.one;

                // Child SpriteRenderer mirrors root: same sprite, same layer,
                // same order so it renders at exactly the same depth.
                var childSr = childGo.AddComponent<SpriteRenderer>();
                childSr.sprite         = rootSr.sprite;
                childSr.sortingLayerID = rootSr.sortingLayerID;
                childSr.sortingOrder   = rootSr.sortingOrder;
                childSr.color          = rootSr.color;

                // Disable root draw so there is no double-draw / ghost.
                // Root SR stays present as the tint anchor (TreeEntity.TakeChopDamage
                // writes to it; we mirror its color to childSr each frame).
                rootSr.enabled = false;

                var entry = new TreeSwayEntry
                {
                    Tree         = tree,
                    Child        = childGo,
                    _rootSr      = rootSr,
                    _childSr     = childSr,
                    _childTf     = childGo.transform,
                    _phaseOffset = phaseOffset,
                    _liveAmplitude = 0f, // ease in from zero (no pop)
                };

                return entry;
            }

            /// <summary>
            /// Advance sway animation for one frame.
            /// Called by TreeSwayDriver.Update — not a MonoBehaviour Update.
            /// </summary>
            internal void Update(float time, float amplitude, float freqHz, float easeSpeed)
            {
                // Ease amplitude in from zero to the target so the first frame
                // does not pop.  Use Time.deltaTime approximation via the
                // continuous-time easing formula.
                _liveAmplitude = Mathf.Lerp(_liveAmplitude, amplitude,
                    1f - Mathf.Exp(-easeSpeed * Time.deltaTime));

                // Sine sway — only X; Y and Z untouched.
                float angle = time * freqHz * Mathf.PI * 2f + _phaseOffset;
                float xOffset = Mathf.Sin(angle) * _liveAmplitude;

                // SPRITE CHILD local position X only.  Root transform untouched.
                _childTf.localPosition = new Vector3(xOffset, 0f, 0f);

                // Mirror root tint to child each frame.
                // TreeEntity.TakeChopDamage / TintHelper / SetSpecies write to
                // root SR (draw-disabled); we forward the colour here so the
                // player sees chop-tint and species-tint on the swaying child.
                MirrorTint();
            }

            private void MirrorTint()
            {
                if (_rootSr == null || _childSr == null) return;
                Color c = _rootSr.color;
                if (_hasPushedColor && c == _lastColor) return;
                _childSr.color  = c;
                _lastColor      = c;
                _hasPushedColor = true;
            }
        }
    }
}
