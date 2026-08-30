namespace BudgetGameDev.Games.Brocoli
{
    using UnityEngine;

    /// <summary>
    /// Temporary 20% attack-speed boost for 10 seconds.
    /// </summary>
    public class AttackSpeedBoost : BoostBase
    {
        public override float Amount => _attackSpeedMultiplier;
        public override float DropWeight => 0.5f;
        public override float Duration => _duration;
        public override ProceduralBoostAudio.BoostSoundType BoostSoundType =>
            ProceduralBoostAudio.BoostSoundType.AttackSpeed;

        [SerializeField]
        private float _attackSpeedMultiplier = 0.2f;

        [SerializeField]
        private float _duration = 10f;

        public override void Apply(PlayerStats stats)
        {
            stats.ApplyTemporaryBoost(
                TemporaryBoostType.AttackSpeed,
                _attackSpeedMultiplier,
                _duration
            );
        }
    }
}
