using UnityEngine;

public class ExperienceBoost : BoostBase
{
    public override float Amount => _experience;

    [SerializeField] private float _experience = 10f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyBoost(this);
    }
}
