using UnityEngine;

/// <summary>
/// Temporary damage boost - gives significant extra damage for 20 seconds
/// </summary>
public class DamageBoost : BoostBase
{
    public override float Amount => _damage;
    public override float Duration => _duration;
    public override ProceduralBoostAudio.BoostSoundType BoostSoundType => ProceduralBoostAudio.BoostSoundType.Damage;

    [SerializeField] private float _damage = 15f;  // +15 damage (150% boost from base 10)
    [SerializeField] private float _duration = 20f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyTemporaryBoost(TemporaryBoostType.Damage, _damage, _duration);
    }
}
