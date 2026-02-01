using UnityEngine;

public class HealthBoost : BoostBase
{
    public override float Amount => _healthAmount;

    [SerializeField] private float _healthAmount = 20f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyBoost(this);
    }
}
