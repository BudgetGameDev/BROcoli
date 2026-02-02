using UnityEngine;

/// <summary>
/// Temporary movement speed boost - gives significant speed for 20 seconds
/// </summary>
public class MovementSpeedBoost : BoostBase
{
    public override float Amount => _movementSpeed;
    public override float Duration => _duration;
    public override ProceduralBoostAudio.BoostSoundType BoostSoundType => ProceduralBoostAudio.BoostSoundType.MovementSpeed;

    [SerializeField] private float _movementSpeed = 3f;  // +3 speed (75% boost from base 4)
    [SerializeField] private float _duration = 20f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyTemporaryBoost(TemporaryBoostType.MovementSpeed, _movementSpeed, _duration);
    }
}
