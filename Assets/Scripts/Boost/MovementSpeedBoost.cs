using UnityEngine;

public class MovementSpeedBoost : BoostBase
{
    public override float Amount => _movementSpeed;

    [SerializeField] private float _movementSpeed = 2f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyBoost(this);
    }
}
