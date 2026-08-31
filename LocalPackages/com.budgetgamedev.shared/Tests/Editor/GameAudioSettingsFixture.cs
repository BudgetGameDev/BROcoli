using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace BudgetGameDev.Shared.Tests
{
    /// <summary>
    /// Shared state handling for the audio settings tests. The component keeps its
    /// volumes in statics and in PlayerPrefs, and the reset that
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/> drives never fires in edit
    /// mode, so every test starts from the same blank slate and the editor's own saved
    /// preferences are put back afterwards.
    /// </summary>
    public abstract class GameAudioSettingsFixture
    {
        protected const string MasterKey = "Audio.MasterVolume";
        protected const string AmbienceKey = "Audio.AmbienceVolume";
        protected const string SfxKey = "Audio.SfxVolume";

        private static readonly string[] VolumeKeys = { MasterKey, AmbienceKey, SfxKey };

        private readonly List<GameObject> spawned = new();
        private readonly Dictionary<string, float?> savedPreferences = new();

        [SetUp]
        public void ClearAudioState()
        {
            savedPreferences.Clear();
            foreach (string key in VolumeKeys)
            {
                savedPreferences[key] = PlayerPrefs.HasKey(key)
                    ? PlayerPrefs.GetFloat(key)
                    : (float?)null;
                PlayerPrefs.DeleteKey(key);
            }

            GameAudioSettings.ResetStatics();
            GameAudioSettings.Configure(null, null);
        }

        [TearDown]
        public void RestoreAudioState()
        {
            GameAudioSettings.ResetStatics();
            GameAudioSettings.Configure(null, null);

            foreach (GameObject spawnedObject in spawned)
            {
                if (spawnedObject != null)
                    Object.DestroyImmediate(spawnedObject);
            }

            spawned.Clear();

            foreach (KeyValuePair<string, float?> saved in savedPreferences)
            {
                if (saved.Value.HasValue)
                    PlayerPrefs.SetFloat(saved.Key, saved.Value.Value);
                else
                    PlayerPrefs.DeleteKey(saved.Key);
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// A settings component that has not run any Unity message yet; edit mode does
        /// not call Awake, so each test drives the lifecycle it wants explicitly.
        /// </summary>
        protected GameAudioSettings NewSettings()
        {
            GameObject root = new("Game Audio Settings Under Test");
            spawned.Add(root);
            return root.AddComponent<GameAudioSettings>();
        }

        protected GameObject Track(GameObject spawnedObject)
        {
            spawned.Add(spawnedObject);
            return spawnedObject;
        }
    }
}
