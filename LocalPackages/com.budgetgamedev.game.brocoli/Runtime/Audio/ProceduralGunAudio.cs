using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// High-quality dynamic weapon sound generator with multi-layer synthesis,
    /// complex modulation, room simulation, and punch compression.
    /// Each gun type has a completely unique sound signature.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public partial class ProceduralGunAudio : MonoBehaviour
    {
        public enum GunSoundType
        {
            AssaultRifle,
            Shotgun,
            HandCannon,
            EnergyBlaster,
            HeavyMachineGun,
        }

        // Static cache for prewarmed clips
        private static System.Collections.Generic.Dictionary<GunSoundType, AudioClip> cachedClips;

        /// <summary>
        /// Pre-generates and caches AudioClips for all gun types to avoid first-shot hitches.
        /// Call this during loading screens or game initialization.
        /// </summary>
        public static void PrewarmAll()
        {
            cachedClips = new System.Collections.Generic.Dictionary<GunSoundType, AudioClip>();
            cachedClips[GunSoundType.AssaultRifle] = GenerateGunClipStatic(
                GunSoundType.AssaultRifle
            );
            cachedClips[GunSoundType.Shotgun] = GenerateGunClipStatic(GunSoundType.Shotgun);
            cachedClips[GunSoundType.HandCannon] = GenerateGunClipStatic(GunSoundType.HandCannon);
            cachedClips[GunSoundType.EnergyBlaster] = GenerateGunClipStatic(
                GunSoundType.EnergyBlaster
            );
            cachedClips[GunSoundType.HeavyMachineGun] = GenerateGunClipStatic(
                GunSoundType.HeavyMachineGun
            );
        }
    }
}
