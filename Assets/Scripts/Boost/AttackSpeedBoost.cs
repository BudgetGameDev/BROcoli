using UnityEngine;

/// <summary>
/// Temporary attack speed boost - gives significant attack speed for 20 seconds
/// </summary>
public class AttackSpeedBoost : BoostBase
{
    public override float Amount => _attackSpeedMultiplier;
    public override float Duration => _duration;
    public override ProceduralBoostAudio.BoostSoundType BoostSoundType => ProceduralBoostAudio.BoostSoundType.AttackSpeed;

    [SerializeField] private float _attackSpeedMultiplier = 0.5f;  // 50% faster attacks
    [SerializeField] private float _duration = 20f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyTemporaryBoost(TemporaryBoostType.AttackSpeed, _attackSpeedMultiplier, _duration);
    }
}
