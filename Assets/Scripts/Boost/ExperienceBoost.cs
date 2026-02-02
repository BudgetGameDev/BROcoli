using UnityEngine;

/// <summary>
/// Instant experience gain - one-time XP boost
/// </summary>
public class ExperienceBoost : BoostBase
{
    public override float Amount => _experience;
    public override float Duration => 0f; // Instant effect
    public override ProceduralBoostAudio.BoostSoundType BoostSoundType => ProceduralBoostAudio.BoostSoundType.Experience;

    [SerializeField] private float _experience = 15f;  // Instant XP

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyBoost(this); // Keep as instant XP
    }
}
