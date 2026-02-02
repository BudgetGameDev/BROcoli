using UnityEngine;

public class DamageBoost : BoostBase
{
    public override float Amount => _damage;
    public override ProceduralBoostAudio.BoostSoundType BoostSoundType => ProceduralBoostAudio.BoostSoundType.Damage;

    [SerializeField] private float _damage = 10f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyBoost(this);
    }
}
