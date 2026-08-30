namespace BudgetGameDev.Games.Brocoli
{
    using UnityEngine;

    /// <summary>
    /// Temporary +2 damage boost for 10 seconds.
    /// </summary>
    public class DamageBoost : BoostBase
    {
        public override float Amount => _damage;
        public override float DropWeight => 0.5f;
        public override float Duration => _duration;
        public override ProceduralBoostAudio.BoostSoundType BoostSoundType =>
            ProceduralBoostAudio.BoostSoundType.Damage;

        [SerializeField]
        private float _damage = 2f;

        [SerializeField]
        private float _duration = 10f;

        public override void Apply(PlayerStats stats)
        {
            stats.ApplyTemporaryBoost(TemporaryBoostType.Damage, _damage, _duration);
        }
    }
}
