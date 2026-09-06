using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.Audio;

namespace BudgetGameDev.Games.Brocoli
{
    internal sealed partial class ProceduralTorchFireAudio
    {
        private static AudioClip bedClip;
        private static AudioClip[] crackleClips;
        private static AudioMixerGroup sfxGroup;
        private static string mixerPath;

        private static void EnsureClips()
        {
            if (bedClip != null)
                return;
            bedClip = CreateClip("Torch burning fuel", SynthesizeBed());
            crackleClips = new AudioClip[CrackleVariants];
            for (int index = 0; index < CrackleVariants; index++)
                crackleClips[index] = CreateClip(
                    "Torch ember crackle " + index,
                    SynthesizeCrackle(index)
                );
            Application.quitting -= ClearClips;
            Application.quitting += ClearClips;
        }

        private static AudioClip CreateClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioMixerGroup FindSfxGroup()
        {
            string requested = GameAudioSettings.MixerResourcePath;
            if (mixerPath == requested && sfxGroup != null)
                return sfxGroup;
            mixerPath = requested;
            sfxGroup = null;
            if (string.IsNullOrEmpty(requested))
                return null;
            var mixer = Resources.Load<AudioMixer>(requested);
            if (mixer == null)
                return null;
            AudioMixerGroup[] groups = mixer.FindMatchingGroups("SFX");
            if (groups.Length > 0)
                sfxGroup = groups[0];
            return sfxGroup;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearClips()
        {
            Application.quitting -= ClearClips;
            DisposeClip(bedClip);
            bedClip = null;
            if (crackleClips != null)
                foreach (AudioClip clip in crackleClips)
                    DisposeClip(clip);
            crackleClips = null;
            sfxGroup = null;
            mixerPath = null;
        }

        private static void DisposeClip(AudioClip clip)
        {
            if (clip == null)
                return;
            if (Application.isPlaying)
                Destroy(clip);
            else
                DestroyImmediate(clip);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterReloadCleanup()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ClearClips;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ClearClips;
        }
#endif
    }
}
