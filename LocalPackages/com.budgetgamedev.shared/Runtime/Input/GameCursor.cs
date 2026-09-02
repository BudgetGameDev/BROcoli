using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BudgetGameDev.Shared
{
    /// <summary>
    /// Owns the mouse pointer for the whole game: whether it is on screen, and what it looks
    /// like when it is.
    ///
    /// The pointer is the operating system's hardware cursor wearing the game's own image, so
    /// it never lags behind the mouse however badly a frame goes. A pointer drawn by the game
    /// would trail a frame behind the hand that moved it, and that is the first thing players
    /// notice.
    ///
    /// Which screens hold the pointer up is not decided here. A game registers what counts,
    /// because the shared layer has no business knowing that BROcoli has a map.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed partial class GameCursor : MonoBehaviour
    {
        private const string HostName = "GameCursor";

        private static GameCursor instance;
        private static readonly List<Func<bool>> VisibilityHolds = new();

        private Vector2 lastPointerPosition;
        private float lastMovementTime;
        private bool hasPointerPosition;
        private bool appliedVisible = true;

        /// <summary>
        /// Whether the pointer is currently drawn. Exposed for tests and for anything that has
        /// to lay out around it.
        /// </summary>
        public static bool IsPointerShown => instance == null || instance.appliedVisible;

        /// <summary>
        /// Registers a reason the pointer must stay on screen -- typically a screen that is
        /// clicked. The pointer is shown while any registered hold returns true. Holds are
        /// polled rather than pushed so a screen closed by being destroyed cannot leave the
        /// pointer stuck on.
        /// </summary>
        public static void AddVisibilityHold(Func<bool> hold)
        {
            if (hold != null && !VisibilityHolds.Contains(hold))
                VisibilityHolds.Add(hold);
        }

        public static void RemoveVisibilityHold(Func<bool> hold)
        {
            VisibilityHolds.Remove(hold);
        }

        /// <summary>
        /// Whether any registered screen is holding the pointer up. A hold that throws is
        /// treated as not holding: a broken screen must not be able to take the pointer away
        /// from every other one, nor to strand it on screen forever.
        /// </summary>
        internal static bool IsHeldVisible()
        {
            foreach (Func<bool> hold in VisibilityHolds)
            {
                try
                {
                    if (hold())
                        return true;
                }
                catch (Exception failure)
                {
                    Debug.LogException(failure);
                }
            }

            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            instance = null;
            VisibilityHolds.Clear();
            ResetArt();
        }

        /// <summary>
        /// Creates the pointer's own object, or returns the one already running. Safe to call
        /// from every scene that wants a pointer; the object survives the load between them.
        /// </summary>
        public static GameCursor EnsurePresent()
        {
            if (instance != null)
                return instance;

            var host = new GameObject(HostName);
            DontDestroyOnLoad(host);
            instance = host.AddComponent<GameCursor>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            lastMovementTime = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            if (instance != this)
                return;

            instance = null;
            // Whatever the game was doing, the player gets their pointer back.
            Cursor.visible = true;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                // A touch device or a pad-only session. Nothing here has anything to say about
                // the pointer, so it is left exactly as the platform set it.
                return;
            }

            NotePointerPosition(mouse.position.ReadValue());

            float sinceMoved = Time.unscaledTime - lastMovementTime;
            ApplyPointerVisibility(
                PointerRevealPolicy.ShouldShowPointer(IsHeldVisible(), sinceMoved)
            );
        }

        /// <summary>
        /// Restarts the reveal when the mouse has actually travelled. The first reading only
        /// establishes where the mouse is; treating it as movement would reveal the pointer
        /// every time a scene loads under a hand that never touched the mouse.
        /// </summary>
        private void NotePointerPosition(Vector2 position)
        {
            if (!hasPointerPosition)
            {
                hasPointerPosition = true;
                lastPointerPosition = position;
                return;
            }

            if (
                (position - lastPointerPosition).sqrMagnitude
                >= PointerRevealPolicy.MovementThresholdPixels
                    * PointerRevealPolicy.MovementThresholdPixels
            )
            {
                lastPointerPosition = position;
                lastMovementTime = Time.unscaledTime;
            }
        }

        private void ApplyPointerVisibility(bool show)
        {
            if (show == appliedVisible && Cursor.visible == show)
                return;

            appliedVisible = show;
            Cursor.visible = show;
            if (show)
                ApplyHardwarePointer();
        }
    }
}
