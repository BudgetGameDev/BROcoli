using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class HealthBoost : BoostBase
{
    [SerializeField] private int _healthAmount = 20;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyBoost(this);
    }
}
