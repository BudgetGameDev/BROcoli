using UnityEngine;

/// <summary>
/// Instant health restore - one-time heal effect
/// </summary>
public class HealthBoost : BoostBase
{
    public override float Amount => _healthAmount;
    public override float Duration => 0f; // Instant effect
    public override ProceduralBoostAudio.BoostSoundType BoostSoundType => ProceduralBoostAudio.BoostSoundType.Health;

    [SerializeField] private float _healthAmount = 30f;  // Heal 30 HP instantly

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyBoost(this); // Keep as instant heal
    }
}
