using UnityEngine;

public class DamageBoost : BoostBase
{
    public override float Amount => _damage;

    [SerializeField] private float _damage = 10f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyBoost(this);
    }
}
