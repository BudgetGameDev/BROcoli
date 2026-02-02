using UnityEngine;

public class DetectionRadiusBoost : BoostBase
{
    public override float Amount => _detectionRadius;
    public override ProceduralBoostAudio.BoostSoundType BoostSoundType => ProceduralBoostAudio.BoostSoundType.DetectionRadius;

    [SerializeField] private float _detectionRadius = 2f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyBoost(this);
    }
}
