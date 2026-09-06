#ifndef BROCOLI_TORCH_FIRE_INCLUDED
#define BROCOLI_TORCH_FIRE_INCLUDED

CBUFFER_START(UnityPerMaterial)
float4 _BaseColor;
float _Layer;
float _AuthoringWhiteNits;
float _SoftDistance;
float _HeatStrength;
float4 _FlameForwardWS;
float _FlameLeanMetres;
float _FlameHeightMetres;
float _FlamePhase;
float _FlamePlaneWeight;
CBUFFER_END

float FireHash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float FireNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(FireHash(i), FireHash(i + float2(1, 0)), f.x),
        lerp(FireHash(i + float2(0, 1)), FireHash(i + 1.0), f.x), f.y);
}

float FireTurbulence(float2 p)
{
    return FireNoise(p) * 0.57 + FireNoise(p * 2.07 + 11.3) * 0.29
        + FireNoise(p * 4.13 + 37.7) * 0.14;
}

// Segmented, world-upright sheets leave the fuel along the torch's forward
// direction, then curve into buoyant vertical rise. Every displacement is zero
// at the foot, including the coherent breathing shared by all flame sheets.
float3 DeformTorchFlame(float3 world, float height, float time)
{
    if (_Layer > 0.5)
        return world;
    float h = saturate(height);
    float breath = sin(time * 1.35 + _FlamePhase) * 0.65
        + sin(time * 0.61 + _FlamePhase * 1.7) * 0.35;
    float bend = 1.0 - exp(-3.5 * h);
    float lean = _FlameLeanMetres * bend;
    world += _FlameForwardWS.xyz * lean;
    // Leave the bowl mostly along its outward axis. Vertical rise accelerates
    // smoothly as the outward travel levels off, rather than hovering upright
    // above the rim. The origin remains fixed at h=0.
    world.y += _FlameHeightMetres * (h * h * breath * 0.11 - bend * (0.65 / 3.5));
    return world;
}

// A soft, inset ellipse keeps refraction continuous with the untouched background.
// Return the displacement in pixels and premultiplied blend coverage separately.
float3 TorchHeatRefraction(float3 uvRandom, float time, float alpha, float depthFade)
{
    float2 p = (uvRandom.xy - float2(0.5, 0.48)) * float2(2.3, 2.15);
    float mask = 1.0 - smoothstep(0.25, 0.95, length(p));
    float2 flow = float2(p.x * 2.1 + uvRandom.z * 17.0, p.y * 2.7 - time * 0.65);
    float2 curl = float2(FireNoise(flow), FireNoise(flow + 13.7)) * 2.0 - 1.0;
    return float3(curl * _HeatStrength * mask, mask * saturate(alpha) * depthFade);
}

// Premultiplied output: fire and embers emit light (alpha zero), smoke absorbs it.
float4 ShadeTorchFire(float3 uvRandom, float4 particle, float time, float depthFade)
{
    float2 uv = uvRandom.xy;
    float seed = uvRandom.z * 93.71;
    float alpha = saturate(particle.a) * depthFade;
    if (_Layer < 0.5)
        alpha *= _FlamePlaneWeight;
    if (_Layer > 1.5)
    {
        float radius = length((uv - 0.5) * float2(2.0, 1.6));
        float spark = pow(saturate(1.0 - radius), 3.0);
        float sizzle = 0.78 + 0.22 * FireNoise(float2(seed, time * 9.0 + seed));
        return float4(_BaseColor.rgb * particle.rgb * spark * alpha * sizzle, 0.0);
    }

    if (_Layer > 0.5)
    {
        float2 smokeFlow = float2(uv.x * 3.6 + seed, uv.y * 2.8 - time * 2.4);
        float billow = FireTurbulence(smokeFlow);
        float radius = length((uv - 0.5) * 2.0);
        float density = smoothstep(0.25, 0.76, billow)
            * (1.0 - smoothstep(0.25, 0.95, radius));
        float opacity = density * alpha;
        return float4(_BaseColor.rgb * particle.rgb * opacity, opacity);
    }

    // The inset, rounded foot tapers to the wick before the UV boundary. Slow
    // buoyant curls move the crown while the ignition zone stays seated.
    float2 flow = float2(uv.x * 3.1 + seed, uv.y * 2.5 - time * 0.62);
    float billow = FireTurbulence(flow);
    float curl = FireNoise(float2(uv.y * 3.1 - time * 0.42, seed + time * 0.13)) - 0.5;
    float breathingWidth = 1.0 + sin(time * 1.35 + _FlamePhase) * uv.y * uv.y * 0.07;
    float x = (uv.x - 0.5) * 2.0 / breathingWidth + curl * (0.015 + uv.y * uv.y * 0.32);
    float width = 0.94 * sqrt(saturate((uv.y - 0.006) / 0.048))
        * pow(saturate(1.0 - uv.y), 0.72);
    float envelope = 1.0 - abs(x) / max(width, 0.03);
    float detail = FireNoise(flow * float2(2.1, 1.25) + float2(billow, -time * 0.35));
    float density = envelope + (billow - 0.5) * uv.y * 0.42
        + (detail - 0.5) * uv.y * uv.y * 0.25;
    float crown = smoothstep(0.65, 0.94, uv.y);
    density -= crown * (0.10 + (1.0 - detail) * 0.65);
    float coverage = smoothstep(0.02, 0.26, density);
    float tip = 0.87 + FireNoise(float2(x * 5.5 + seed, time * 1.1 + seed)) * 0.10;
    coverage *= smoothstep(0.006, 0.025, uv.y) * (1.0 - smoothstep(tip - 0.065, tip + 0.025, uv.y));
    float intensity = max(_BaseColor.r, max(_BaseColor.g, _BaseColor.b));
    float ignitionRadius = length(float2(x / 0.76, (uv.y - 0.064) / 0.050));
    float ignition = 1.0 - smoothstep(0.30, 1.0, ignitionRadius);
    // An arched inner fuel pocket leaves thin orange reaction sheets around its
    // sides; this is not a horizontal dim band through the entire flame.
    float pocketSway = (FireNoise(float2(seed, time * 1.15)) - 0.5) * 0.11;
    float pocketHeight = 0.19 + (FireNoise(float2(seed + 9.2, time * 0.91)) - 0.5) * 0.022;
    float pocketRadius = length(float2((x + pocketSway) / max(width * 0.78, 0.03), (uv.y - pocketHeight) / 0.11));
    pocketRadius += (billow - 0.5) * 0.28;
    float gap = 1.0 - smoothstep(0.57, 1.08, pocketRadius);
    float combustion = smoothstep(0.105, 0.17, uv.y) * (1.0 - gap);
    float core = smoothstep(0.62, 0.98, density) * (1.0 - smoothstep(0.52, 0.88, uv.y));
    core *= smoothstep(0.22, 0.34, uv.y);
    // Keep the colored perimeter below clipping even when HDR calibration raises
    // the material's peak. Only the small interior uses the full authoring range.
    float3 fringe = float3(1.0, 0.29, 0.025) * min(intensity * 0.15, 0.72);
    fringe *= lerp(float3(1.0, 1.0, 1.0), float3(0.9, 0.64, 0.40), crown);
    float3 hot = float3(1.0, 0.73, 0.38) * intensity * core * core * 0.35;
    float3 blue = float3(0.06, 0.20, 0.65) * min(intensity * 0.11, 0.48) * ignition;
    float3 emission = blue + (fringe + hot) * combustion;
    // Optical density lets overlapping crossed sheets build a visible warm-gray
    // fuel pocket against brightly lit stone, rather than whitening into it.
    float soot = gap * coverage * (1.0 - exp(-alpha * (1.0 + billow * 0.6)));
    return float4(emission * coverage * alpha + float3(0.075, 0.068, 0.061) * soot, soot);
}

#endif
