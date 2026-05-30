using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MelonS.GameProto
{
    /// <summary>
    /// Scene-scope gate for self-bootstrapping gameplay components.
    ///
    /// BUG FIX (operator 2026-05-30): ~20 gameplay UI/FX components self-attach
    /// via [RuntimeInitializeOnLoadMethod(AfterSceneLoad)].  AfterSceneLoad fires
    /// once at process start while the MainMenu scene is the active scene, so all
    /// of them spawned their buttons / alert stack / gizmo bar / scatter / FX
    /// ON TOP of the MainMenu — the operator saw 채광/경작/해제 buttons before
    /// ever pressing "Start Game".
    ///
    /// Fix: each offending Bootstrap() now defers its spawn through
    /// <see cref="RunWhenGameScene"/>.  Nothing spawns while MainMenu is active;
    /// everything spawns the instant the Game scene becomes active (or right now,
    /// if the gate is already evaluated inside the Game scene — e.g. the
    /// game-only or verify-only build that boots straight into Game.unity).
    ///
    /// "Game scene" detection is robust to scene-name drift:
    ///   1. If a <see cref="GameManager"/> exists in the loaded scene, it is the
    ///      Game scene (GameManager lives ONLY in Game.unity — confirmed by grep:
    ///      MainMenu.unity has zero GameManager references).
    ///   2. Else fall back to the active scene name == <see cref="GameSceneName"/>.
    ///
    /// Idempotent: each queued action is invoked at most once.  The existing
    /// per-component FindExisting() guards stay in place, so even a double-fire
    /// can never double-spawn.
    /// </summary>
    public static class GameSceneGate
    {
        /// <summary>Canonical Game scene name (matches BuildScript scene list and
        /// MainMenuController.gameSceneName).  Used only as a fallback when no
        /// GameManager marker is found.</summary>
        public const string GameSceneName = "Game";

        /// <summary>
        /// True when the currently-active scene is the Game scene.
        ///
        /// Primary signal: a GameManager component exists (Game-only marker).
        /// Fallback signal: active scene name equals <see cref="GameSceneName"/>.
        /// The name fallback covers the brief window before GameManager.Start has
        /// run, and the game-only/verify builds whose active scene is "Game".
        /// </summary>
        public static bool IsGameScene()
        {
#if UNITY_2023_1_OR_NEWER
            var gm = UnityEngine.Object.FindFirstObjectByType<GameManager>();
#else
            var gm = UnityEngine.Object.FindObjectOfType<GameManager>();
#endif
            if (gm != null) return true;

            return SceneManager.GetActiveScene().name == GameSceneName;
        }

        /// <summary>
        /// Runs <paramref name="spawn"/> when (and only when) the Game scene is
        /// active.  If the Game scene is already active, runs immediately.
        /// Otherwise subscribes to <see cref="SceneManager.sceneLoaded"/> and
        /// fires the action exactly once when the Game scene loads, then
        /// unsubscribes itself.  Safe to call from a RuntimeInitialize bootstrap
        /// while the MainMenu scene is active — nothing spawns until Game loads.
        /// </summary>
        public static void RunWhenGameScene(Action spawn)
        {
            if (spawn == null) return;

            if (IsGameScene())
            {
                spawn();
                return;
            }

            // Not in the Game scene yet (we are on MainMenu, or the very first
            // AfterSceneLoad on MainMenu).  Wait for the Game scene to load.
            // One-shot, self-unsubscribing handler.  A guard flag protects
            // against the (theoretical) double-invoke of the same delegate.
            bool fired = false;

            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> handler = null;
            handler = (scene, mode) =>
            {
                if (fired) return;

                // Only fire for the Game scene.  Use the same robust check:
                // the just-loaded scene's name, since the active scene may not
                // be switched to it yet at the moment sceneLoaded fires.
                bool isGame = scene.name == GameSceneName;
                if (!isGame)
                {
                    // Could also be a reload of MainMenu or a test scene — ignore.
                    return;
                }

                fired = true;
                SceneManager.sceneLoaded -= handler;
                spawn();
            };

            SceneManager.sceneLoaded += handler;
        }
    }
}
