using UnityEngine;

/// <summary>
/// Temporary magnet boost that attracts every dropped pickup and XP orb,
/// including objects beyond the current camera view.
/// </summary>
public class MagnetBoost : BoostBase
{
    public override float Amount => 1f;
    public override float DropWeight => 0.8f;
    public override float Duration => _duration;
    public override ProceduralBoostAudio.BoostSoundType BoostSoundType =>
        ProceduralBoostAudio.BoostSoundType.Magnet;

    [SerializeField]
    private float _duration = 10f;

    public override void Apply(PlayerStats stats)
    {
        stats.ApplyTemporaryBoost(TemporaryBoostType.Magnet, Amount, _duration);
    }
}
