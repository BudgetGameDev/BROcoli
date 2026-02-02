using UnityEngine;

public class SprayWidthBoost : BoostBase
{
    [SerializeField] private float _amount = 10f; // Degrees to add to spray cone
    
    public override float Amount => _amount;
    public override ProceduralBoostAudio.BoostSoundType BoostSoundType => ProceduralBoostAudio.BoostSoundType.SprayWidth;
    
    public override void Apply(PlayerStats stats)
    {
        stats.AddSprayWidth(_amount);
    }
}
