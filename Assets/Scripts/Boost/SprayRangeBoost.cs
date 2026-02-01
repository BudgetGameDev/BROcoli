using UnityEngine;

public class SprayRangeBoost : BoostBase
{
    [SerializeField] private float _amount = 0.3f;
    
    public override float Amount => _amount;
    
    public override void Apply(PlayerStats stats)
    {
        stats.AddSprayRange(_amount);
    }
}
