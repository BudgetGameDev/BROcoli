using UnityEngine;

public class ExperienceBoost : BoostBase
{
    public override float Amount => _experience;
    public override ProceduralBoostAudio.BoostSoundType BoostSoundType => ProceduralBoostAudio.BoostSoundType.Experience;

    [SerializeField] private float _experience = 10f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyBoost(this);
    }
}
