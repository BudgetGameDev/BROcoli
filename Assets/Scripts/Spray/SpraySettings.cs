using UnityEngine;

/// <summary>
/// Static configuration settings for the sanitizer spray weapon.
/// Centralizes all hardcoded values to prevent scene overrides and enable easy tuning.
/// </summary>
public static class SpraySettings
{
    // ==================== Base Stats ====================
    public const float BaseSprayRange = 8.0f;
    public const float BaseSprayAngle = 60f;           // Cone angle in degrees
    public const float BaseDamagePerParticle = 0.5f;
    public const float DamageTickRate = 0.1f;          // How often damage is calculated

    // ==================== Burst Settings ====================
    public const float BurstDuration = 0.4f;           // How long each spray burst lasts
    public const float BurstCooldown = 0.1f;           // Minimum time between bursts

    // ==================== Visual Settings ====================
    public const float HandOffset = 0.7f;
    public const float VisualZOffset = -0.5f;
    public const float IsometricYOffset = 0.3f;
    public const float CameraAngle = 60f;
    public static readonly Color SprayColor = new Color(0.7f, 0.9f, 1f, 0.6f);
    public const bool FlipHandWithDirection = true;

    // ==================== Hand Animation ====================
    public const float HandRotationSpeed = 720f;       // Degrees per second
    public const float AimDelayBeforeSpray = 0.08f;
    public const bool ShowHandAlways = true;

    // ==================== Particle Settings ====================
    public const float ParticleSpeedMultiplier = 2.5f;
    public const float ParticleLifetimeBase = 0.8f;
    public const int EmissionRate = 60;
    public const float NozzleOffset = 0.7f;
    public const int MaxParticles = 400;
    public const float ParticleMinSize = 0.3f;
    public const float ParticleMaxSize = 0.6f;

    // ==================== Particle Shape ====================
    public const float ParticleShapeRadius = 0.08f;
    public const float ParticleShapeRadiusThickness = 1f;

    // ==================== Particle Noise ====================
    public const float NoiseStrength = 0.08f;
    public const float NoiseFrequency = 4f;
    public const float NoiseScrollSpeed = 0.2f;
    public const float NoiseStrengthX = 0.1f;
    public const float NoiseStrengthY = 0.1f;
    public const float NoiseStrengthZ = 0f;            // No Z noise - stay on plane

    // ==================== Hand Visual Colors ====================
    public static readonly Color SkinToneColor = new Color(0.96f, 0.87f, 0.78f);
    public static readonly Color SprayCanColor = new Color(0.2f, 0.6f, 0.9f);
    public static readonly Color NozzleColor = new Color(0.3f, 0.3f, 0.3f);

    // ==================== Hand Visual Scales ====================
    public static readonly Vector3 HandSpriteScale = new Vector3(0.15f, 0.12f, 1f);
    public static readonly Vector3 SprayCanScale = new Vector3(0.2f, 0.12f, 1f);
    public static readonly Vector3 NozzleScale = new Vector3(0.06f, 0.04f, 1f);

    // ==================== Hand Visual Positions ====================
    public static readonly Vector3 HandSpriteLocalPos = new Vector3(-0.1f, 0, 0);
    public static readonly Vector3 SprayCanLocalPos = new Vector3(0.08f, 0, 0);
    public static readonly Vector3 NozzleLocalPos = new Vector3(0.22f, 0.02f, 0);

    // ==================== Sorting Layers ====================
    public const int ParticleSortingOrder = 10;
    public const int HandSpriteSortingOrder = 12;
    public const int SprayCanSortingOrder = 13;
    public const int NozzleSortingOrder = 14;

    // ==================== Detection ====================
    public const int HitBufferSize = 20;
    public const float AngleToleranceForFiring = 15f;  // Degrees
}
