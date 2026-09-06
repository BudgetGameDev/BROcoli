using UnityEngine;
using UnityEngine.Audio;

namespace BudgetGameDev.Shared
{
    public sealed partial class GameAudioSettings
    {
        // The launcher can create this host before a game has selected its mixer.
        // Rebind that existing host when the game is selected, keeping slider values live.
        private void BindMixer()
        {
            mixer = null;
            ambienceGroup = sfxGroup = null;
            nextSourceScan = 0;
            if (string.IsNullOrEmpty(MixerResourcePath))
                return;

            mixer = Resources.Load<AudioMixer>(MixerResourcePath);
            if (mixer == null)
            {
                Debug.LogError($"[Audio Settings] Missing Resources/{MixerResourcePath}.mixer");
                return;
            }

            ambienceGroup = FindGroup("Ambience");
            sfxGroup = FindGroup("SFX");
            ApplyMixerVolumes();
        }
    }
}
