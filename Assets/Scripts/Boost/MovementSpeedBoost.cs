using UnityEngine;

public class MovementSpeedBoost : BoostBase
{
    public override float Amount => _movementSpeed;
    public override ProceduralBoostAudio.BoostSoundType BoostSoundType => ProceduralBoostAudio.BoostSoundType.MovementSpeed;

    [SerializeField] private float _movementSpeed = 2f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyBoost(this);
    }
}
