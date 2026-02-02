using UnityEngine;

/// <summary>
/// Temporary magnet boost - attracts pickups (XP orbs) towards the player for 20 seconds.
/// The Amount specifies the magnet radius.
/// </summary>
public class MagnetBoost : BoostBase
{
    public override float Amount => _magnetRadius;
    public override float Duration => _duration;
    public override ProceduralBoostAudio.BoostSoundType BoostSoundType => ProceduralBoostAudio.BoostSoundType.Magnet;

    [SerializeField] private float _magnetRadius = 10f;  // Pickup attraction radius
    [SerializeField] private float _duration = 20f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyTemporaryBoost(TemporaryBoostType.Magnet, _magnetRadius, _duration);
    }
}
