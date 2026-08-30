using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    /// <summary>
    /// Temporarily slows enemies, their attack animations, and hostile projectiles
    /// without reducing player movement or weapon speed.
    /// </summary>
    public class HourglassBoost : BoostBase
    {
        public override float Amount => _enemyTimeScale;
        public override float Duration => _duration;
        public override float DropWeight => 1f;
        public override ProceduralBoostAudio.BoostSoundType BoostSoundType =>
            ProceduralBoostAudio.BoostSoundType.TimeSlow;

        [SerializeField, Range(0.1f, 1f)]
        private float _enemyTimeScale = 0.55f;

        [SerializeField, Min(1f)]
        private float _duration = 8f;

        public override void Apply(PlayerStats stats)
        {
            stats.ApplyTemporaryBoost(TemporaryBoostType.TimeSlow, _enemyTimeScale, _duration);
        }
    }
}
