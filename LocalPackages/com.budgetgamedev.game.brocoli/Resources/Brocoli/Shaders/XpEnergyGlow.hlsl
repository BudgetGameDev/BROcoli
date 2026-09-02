#ifndef BROCOLI_XP_ENERGY_GLOW_INCLUDED
#define BROCOLI_XP_ENERGY_GLOW_INCLUDED

// The glow's whole appearance, shared by every render pipeline's subshader. Only the
// pipeline plumbing above this file differs; the look must not, or the two pipelines
// would drift apart the moment one of them is edited.
//
// Everything here is additive. A pixel the glow does not reach is left at exactly the
// value the frame already held, which is what keeps an OLED's unlit pixels switched off
// instead of painting a faintly grey box around the crystal. It also means alpha carries
// no information: coverage is expressed as brightness, so a fading orb loses light rather
// than becoming a translucent shape over the dungeon floor.

CBUFFER_START(UnityPerMaterial)
    float4 _CoreColor;
    float4 _RimColor;
    float _Intensity;
    float _FresnelPower;
    float _FresnelBias;
    float _FalloffInverted;
    float _BandScale;
    float _BandSpeed;
    float _BandSharpness;
    float _BandStrength;
    float _PulseSpeed;
    float _PulseAmount;
    float _FlickerSpeed;
    float _FlickerAmount;
    float _Fade;
CBUFFER_END

/// How the shell's brightness is distributed across its own silhouette.
///
/// The magnitude is taken because the two shells are seen from opposite sides. The inner
/// one is drawn front-faces-only, so its normals point at the camera; the halo is drawn
/// from the inside, so its normals point away. Without the magnitude the halo's dot
/// product would be negative everywhere, saturate would flatten it to a constant, and the
/// halo would render as a hard-edged disc rather than as light.
///
/// Not inverted, the silhouette runs hot and the face pointing at the camera stays thin --
/// a rim. Inverted, it is brightest looking straight through the shell and falls to nothing
/// at the silhouette, which is the falloff a glow around an object has. The crystal itself
/// hides the middle, so what is left of an inverted shell is a halo that fades outward.
float XpGlowFalloff(float3 normalWS, float3 viewDirectionWS)
{
    float facing = abs(dot(normalize(normalWS), normalize(viewDirectionWS)));
    float shaped = lerp(1.0 - facing, facing, saturate(_FalloffInverted));
    return saturate(_FresnelBias + (1.0 - _FresnelBias) * pow(saturate(shaped), _FresnelPower));
}

/// Energy rings climbing the crystal's own axis. Object space rather than world space, so
/// the bands ride the pickup as it bobs and spins instead of the crystal sliding through a
/// pattern nailed to the dungeon.
float XpGlowBands(float heightOS, float time)
{
    float sweep = frac(heightOS * _BandScale - time * _BandSpeed);
    float ridge = 1.0 - abs(sweep * 2.0 - 1.0);
    return pow(saturate(ridge), _BandSharpness);
}

/// A slow breath over the whole effect.
float XpGlowPulse(float time)
{
    return 1.0 + sin(time * _PulseSpeed) * _PulseAmount;
}

/// The fast, unsteady part of the look. Two sines whose periods do not divide each other
/// never repeat on any timescale a player watches, which reads as arcing electricity
/// without a noise texture to sample or a random seed to keep in sync.
float XpGlowFlicker(float time)
{
    float arc = sin(time * _FlickerSpeed) * sin(time * _FlickerSpeed * 0.37 + 1.3);
    return 1.0 + arc * _FlickerAmount;
}

/// The scene-linear light this fragment adds. The colours arrive already authored against
/// the display -- in HDR they are solved so the rim lands on the calibrated peak -- so
/// nothing here may rescale them beyond the animation that is the effect itself.
float3 XpGlowShade(float3 positionOS, float3 normalWS, float3 viewDirectionWS, float time)
{
    float falloff = XpGlowFalloff(normalWS, viewDirectionWS);
    float bands = XpGlowBands(positionOS.y, time);

    // The core colour is shaped by the same falloff as the rim, or the inner shell would
    // wash a flat wall of blue over the crystal's facets and hide the gem it belongs to.
    float3 core = _CoreColor.rgb * lerp(1.0 - _BandStrength, 1.0, bands) * falloff;
    float3 rim = _RimColor.rgb * falloff * falloff;

    float animation = XpGlowPulse(time) * XpGlowFlicker(time);
    return max((core + rim) * _Intensity * animation * _Fade, 0.0);
}

#endif
