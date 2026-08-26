using UnityEngine;

/// <summary>
/// Temporary +1 movement-speed boost for 10 seconds.
/// </summary>
public class MovementSpeedBoost : BoostBase
{
    public override float Amount => _movementSpeed;
    public override float DropWeight => 0.4f;
    public override float Duration => _duration;
    public override ProceduralBoostAudio.BoostSoundType BoostSoundType =>
        ProceduralBoostAudio.BoostSoundType.MovementSpeed;

    [SerializeField]
    private float _movementSpeed = 1f;

    [SerializeField]
    private float _duration = 10f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyTemporaryBoost(TemporaryBoostType.MovementSpeed, _movementSpeed, _duration);
    }
}
