using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Shared fixture for the landscape enforcer. Every piece of its state is
    /// static and its overlay is built to outlive a scene load, so each test has
    /// to start - and leave - the type in the state a fresh run would see.
    /// </summary>
    public abstract class ForceLandscapeAspectTestBase
    {
        private readonly List<GameObject> spawned = new();

        /// <summary>Objects the enforcer asked to survive a scene load.</summary>
        protected readonly List<GameObject> KeptAcrossScenes = new();

        private float savedTimeScale;

        /// <summary>Creates a scene object the fixture will clean up.</summary>
        protected GameObject NewObject(string name)
        {
            var created = new GameObject(name);
            spawned.Add(created);
            return created;
        }

        /// <summary>Registers an object the code under test created itself.</summary>
        protected void Track(GameObject created)
        {
            spawned.Add(created);
        }

        /// <summary>
        /// Puts a pause screen in front of the enforcer. Gameplay auto-pauses and
        /// menus do not, and which one is loaded is the only thing that decides it.
        /// </summary>
        protected static TestPauseController NewPauseMenu()
        {
            var pause = new TestPauseController();
            ForceLandscapeAspect.FindPauseController = () => pause;
            return pause;
        }

        private void RecordKeptAcrossScenes(GameObject target)
        {
            KeptAcrossScenes.Add(target);
            spawned.Add(target);
        }

        [SetUp]
        public void ResetAspectState()
        {
            savedTimeScale = Time.timeScale;
            ForceLandscapeAspect.ResetStatics();

            // DontDestroyOnLoad throws outside play mode, so the fixture records the
            // request instead - and owns the object well enough to destroy it after.
            ForceLandscapeAspect.KeepAcrossScenes = RecordKeptAcrossScenes;

            // No scene is loaded, so the enforcer must be told there is no pause menu.
            ForceLandscapeAspect.FindPauseController = () => null;

            // Every test runs inside the editor, where the focus pause is deliberately
            // off. A shipped player is the case worth covering, so that is the default.
            ForceLandscapeAspect.IsEditorPlayer = () => false;
        }

        [TearDown]
        public void RestoreAspectState()
        {
            if (ForceLandscapeAspect._rotateOverlay != null)
            {
                Object.DestroyImmediate(ForceLandscapeAspect._rotateOverlay);
            }

            foreach (GameObject created in spawned)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            spawned.Clear();
            KeptAcrossScenes.Clear();
            ForceLandscapeAspect.ResetStatics();
            Time.timeScale = savedTimeScale;
        }
    }
}
