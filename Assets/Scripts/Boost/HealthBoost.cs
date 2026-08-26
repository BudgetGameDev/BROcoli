using UnityEngine;

/// <summary>
/// Instant health restore - one-time heal effect
/// </summary>
public class HealthBoost : BoostBase
{
    public override float Amount => _healthAmount;
    public override float DropWeight => 6f;
    public override float Duration => 0f; // Instant effect
    public override ProceduralBoostAudio.BoostSoundType BoostSoundType =>
        ProceduralBoostAudio.BoostSoundType.Health;

    [SerializeField]
    private float _healthAmount = 25f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyBoost(this); // Keep as instant heal
    }
}
