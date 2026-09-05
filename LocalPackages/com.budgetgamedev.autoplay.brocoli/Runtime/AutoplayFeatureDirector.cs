using System;
using BudgetGameDev.Autoplay;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Drives the systems combat never reaches on its own -- the inventory and map
    /// overlays, the pause menu and its settings pane, and checkpointing -- and
    /// records each one only after checking the game actually responded. A probe
    /// that pokes a button and assumes it worked would report coverage the harness
    /// does not have.
    ///
    /// Probes run one per step on the unscaled clock, so they cost the same handful
    /// of game-seconds whether or not the run is being fast-forwarded, then rest for
    /// long enough that a marathon still spends nearly all of its time playing.
    /// </summary>
    public partial class AutoplayFeatureDirector : MonoBehaviour
    {
        /// <summary>Game-seconds before the first probe, so the dungeon exists first.</summary>
        private const float WarmUpSeconds = 8f;

        /// <summary>Game-seconds a probe is left in place before the next one runs.</summary>
        private const float StepSeconds = 0.4f;

        /// <summary>Game-seconds of uninterrupted play between sweeps.</summary>
        private const float RestSeconds = 30f;

        private Action[] probes;
        private int next;
        private float elapsed;
        private float nextProbeTime;

        internal int CompletedSweeps { get; private set; }

        private void Start()
        {
            probes = new Action[]
            {
                OpenInventory,
                NavigateInventory,
                EquipInventoryItem,
                OpenMap,
                PanMap,
                CloseOverlay,
                OpenPauseMenu,
                OpenPauseSettings,
                ResumeFromPause,
                ProbeSaveRoundTrip,
            };
            nextProbeTime = WarmUpSeconds;
        }

        private void Update()
        {
            if (probes == null)
                return;

            elapsed += AutoplayTimeControl.GameDelta;
            if (elapsed < nextProbeTime)
                return;

            if (next >= probes.Length)
            {
                next = 0;
                CompletedSweeps++;
                nextProbeTime = elapsed + RestSeconds;
                return;
            }

            RunProbe(probes[next++]);
            nextProbeTime = elapsed + StepSeconds;
        }

        /// <summary>
        /// A probe that throws must not take the run down with it: the failure is the
        /// interesting result, and the telemetry's error counter already fails the
        /// run for it. Swallowing it here keeps the remaining probes running so one
        /// broken system does not hide every system after it.
        /// </summary>
        private static void RunProbe(Action probe)
        {
            try
            {
                probe();
            }
            catch (Exception error)
            {
                Debug.LogError($"[Autoplay] Feature probe failed: {error.Message}");
            }
        }
    }
}
