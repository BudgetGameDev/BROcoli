/// <summary>
/// Convention for systems that can be prewarmed/preloaded during game startup.
/// 
/// Classes that generate runtime assets (procedural audio, materials, etc.)
/// should implement a static PrewarmAll() method following this pattern:
/// 
///     public static void PrewarmAll()
///     {
///         // Pre-generate and cache assets here
///     }
/// 
/// Then register the call in GamePreloader.WarmupAudio() or create a new warmup step.
/// 
/// Current prewarmed systems:
/// - ProceduralBoostAudio (9 boost pickup sounds)
/// - ProceduralXPPickupAudio (XP pickup sound)
/// - ProceduralProjectileHitAudio (5 player hit sounds)
/// - ProceduralEnemyProjectileHitAudio (5 enemy hit sounds)
/// - ProceduralLevelUpAudio (level up fanfare)
/// - ProceduralGunAudio (5 gun types)
/// - ProceduralFootstepAudio (footstep sound)
/// - ProceduralUIAudio (UI hover/select sounds)
/// - SprayMaterialCreator (spray particle materials)
/// </summary>
public static class PrewarmableConvention
{
    // This is a documentation-only class describing the prewarming convention.
    // See GamePreloader.WarmupAudio() for the actual prewarming calls.
}
