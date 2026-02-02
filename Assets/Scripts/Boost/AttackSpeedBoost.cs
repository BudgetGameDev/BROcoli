using UnityEngine;

public class AttackSpeedBoost : BoostBase
{
    public override float Amount => _attackSpeedMultiplier;
    public override ProceduralBoostAudio.BoostSoundType BoostSoundType => ProceduralBoostAudio.BoostSoundType.AttackSpeed;

    [SerializeField] private float _attackSpeedMultiplier = 0.6f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyBoost(this);
    }
}
