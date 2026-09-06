using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Handles all player audio: SFX and ambient sounds.
    /// Creates the dungeon soundscape and current procedural SFX, and owns the
    /// player's AudioSources.
    /// </summary>
    public class PlayerAudioHandler : MonoBehaviour
    {
        // Resource paths for audio clips (ggj-2023 sfx folder)
        // Note: Resources.Load paths are relative to Resources folder, without extension
        private const string WalkSoundPath = "Brocoli/Audio/walk-0"; // Generic footstep from Audio folder
        private const string DamageSoundPath = "Brocoli/Sprites/ggj-2023/sfx/ohno-trædid-minkar2"; // Damage/shrink sound
        private const string CollisionSoundPath =
            "Brocoli/Sprites/ggj-2023/sfx/rakar-stein-ella-vegg"; // Collision with wall/stone
        private const string GrowSoundPath = "Brocoli/Sprites/ggj-2023/sfx/trædid-veksur"; // Tree grows sound
        private const string ShrinkSoundPath = "Brocoli/Sprites/ggj-2023/sfx/ohno-trædid-minkar2"; // Tree shrinks sound

        // Audio clips (loaded from Resources)
        private AudioClip _walkClip;
        private AudioClip _damageClip;
        private AudioClip _collisionClip;
        private AudioClip _deathClip;
        private AudioClip _growClip;
        private AudioClip _shrinkClip;

        // Audio sources (created dynamically or found on GameObject)
        private AudioSource _sfxSource;
        private AudioSource _sfxSource2;
        private ProceduralDungeonAmbience _dungeonAmbience;
        private AudioSource _deathSource;

        private void Awake()
        {
            LoadAudioClips();
            SetupAudioSources();
        }

        private void LoadAudioClips()
        {
            _walkClip = LoadClip(WalkSoundPath);
            _damageClip = LoadClip(DamageSoundPath);
            _collisionClip = LoadClip(CollisionSoundPath);
            _deathClip = ProceduralPlayerDeathAudio.GetOrCreateClip();
            _growClip = LoadClip(GrowSoundPath);
            _shrinkClip = LoadClip(ShrinkSoundPath);
        }

        private AudioClip LoadClip(string path)
        {
            AudioClip clip = Resources.Load<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogWarning($"PlayerAudioHandler: Could not load audio clip from '{path}'");
            }
            return clip;
        }

        private void SetupAudioSources()
        {
            // Get existing AudioSources or create new ones
            AudioSource[] existingSources = GetComponents<AudioSource>();

            if (existingSources.Length >= 2)
            {
                _sfxSource = existingSources[0];
                _sfxSource2 = existingSources[1];
            }
            else
            {
                _sfxSource = GetOrAddAudioSource(0);
                _sfxSource2 = GetOrAddAudioSource(1);
            }

            EnsureDungeonAmbience();
            _deathSource = CreateAmbientSource("PlayerDeathSource", _deathClip, false, 1f);
        }

        internal void EnsureDungeonAmbience()
        {
            if (_dungeonAmbience != null)
                return;
            var child = new GameObject("Dungeon Ambience");
            child.transform.SetParent(transform, false);
            _dungeonAmbience = child.AddComponent<ProceduralDungeonAmbience>();
        }

        private AudioSource GetOrAddAudioSource(int index)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length > index)
            {
                return sources[index];
            }
            return gameObject.AddComponent<AudioSource>();
        }

        private AudioSource CreateAmbientSource(
            string name,
            AudioClip clip,
            bool loop,
            float volume
        )
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(transform);
            child.transform.localPosition = Vector3.zero;

            AudioSource source = child.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.volume = volume;
            source.playOnAwake = loop;
            source.spatialBlend = 0f; // 2D sound

            if (loop && clip != null)
            {
                source.Play();
            }

            return source;
        }

        /// <summary>
        /// Play the walk/footstep sound.
        /// </summary>
        public void PlayWalkSound()
        {
            PlayOneShot(_sfxSource, _walkClip);
        }

        /// <summary>
        /// Play the damage/hurt sound.
        /// </summary>
        public void PlayDamageSound()
        {
            PlayClip(_sfxSource2, _damageClip);
        }

        /// <summary>
        /// Play the collision sound.
        /// </summary>
        public void PlayCollisionSound()
        {
            PlayClip(_sfxSource2, _collisionClip);
        }

        /// <summary>
        /// Play the broccoli defeat sound.
        /// </summary>
        public void PlayDeathSound()
        {
            if (_deathSource != null && _deathClip != null)
            {
                _deathSource.Stop();
                _deathSource.Play();
            }
        }

        /// <summary>
        /// Play the grow/level up sound.
        /// </summary>
        public void PlayGrowSound()
        {
            PlayOneShot(_sfxSource, _growClip);
        }

        /// <summary>
        /// Play the shrink sound.
        /// </summary>
        public void PlayShrinkSound()
        {
            PlayOneShot(_sfxSource, _shrinkClip);
        }

        /// <summary>
        /// Stop all ambient audio sources.
        /// </summary>
        public void StopAllAmbient()
        {
            if (_dungeonAmbience != null)
                _dungeonAmbience.enabled = false;
        }

        private void PlayOneShot(AudioSource source, AudioClip clip)
        {
            if (source != null && clip != null)
            {
                source.PlayOneShot(clip);
            }
        }

        private void PlayClip(AudioSource source, AudioClip clip)
        {
            if (source != null && clip != null)
            {
                source.clip = clip;
                source.Play();
            }
        }

        private void OnDestroy()
        {
            // Clean up dynamically created child GameObjects
            if (_dungeonAmbience != null)
                DestroyOwnedAudio(_dungeonAmbience.gameObject);
            if (_deathSource != null)
                DestroyOwnedAudio(_deathSource.gameObject);
        }

        private static void DestroyOwnedAudio(GameObject child)
        {
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }
}
