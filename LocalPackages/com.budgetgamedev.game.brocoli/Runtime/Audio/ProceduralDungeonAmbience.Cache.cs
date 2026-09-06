using BudgetGameDev.Shared;
using UnityEngine;
using UnityEngine.Audio;

namespace BudgetGameDev.Games.Brocoli
{
    internal sealed partial class ProceduralDungeonAmbience
    {
        private static AudioClip bedClip;
        private static AudioClip[] detailClips;
        private static AudioMixerGroup ambienceGroup;
        private static string mixerPath;

        private static void EnsureClips()
        {
            if (bedClip != null)
                return;
            bedClip = CreateClip("Dungeon ambience cavity air", SynthesizeBed(), 2);
            detailClips = new AudioClip[DetailVariants];
            for (int variant = 0; variant < DetailVariants; variant++)
                detailClips[variant] = CreateClip(
                    "Dungeon ambience " + (variant % 2 == 0 ? "drip " : "stone ") + variant,
                    SynthesizeDetail(variant),
                    1
                );
            Application.quitting -= ClearClips;
            Application.quitting += ClearClips;
        }

        private static AudioClip CreateClip(string name, float[] samples, int channels)
        {
            var clip = AudioClip.Create(
                name,
                samples.Length / channels,
                channels,
                SampleRate,
                false
            );
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioMixerGroup FindAmbienceGroup()
        {
            string requested = GameAudioSettings.MixerResourcePath;
            if (mixerPath == requested && ambienceGroup != null)
                return ambienceGroup;
            mixerPath = requested;
            ambienceGroup = null;
            if (string.IsNullOrEmpty(requested))
                return null;
            var mixer = Resources.Load<AudioMixer>(requested);
            if (mixer == null)
                return null;
            AudioMixerGroup[] groups = mixer.FindMatchingGroups("Ambience");
            if (groups.Length > 0)
                ambienceGroup = groups[0];
            return ambienceGroup;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearClips()
        {
            Application.quitting -= ClearClips;
            DisposeClip(bedClip);
            bedClip = null;
            if (detailClips != null)
                foreach (AudioClip clip in detailClips)
                    DisposeClip(clip);
            detailClips = null;
            ambienceGroup = null;
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
