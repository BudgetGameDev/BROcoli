using UnityEngine;

public class AttackSpeedBoost : BoostBase
{
    public override float Amount => _attackSpeedMultiplier;

    [SerializeField] private float _attackSpeedMultiplier = 0.6f;

    public override void Apply(PlayerController player)
    {
        player.AddAttackSpeed(_attackSpeedMultiplier);
    }
}
