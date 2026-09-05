#ifndef BROCOLI_TORCH_FIRE_INCLUDED
#define BROCOLI_TORCH_FIRE_INCLUDED

CBUFFER_START(UnityPerMaterial)
float4 _BaseColor;
float _Layer;
float _AuthoringWhiteNits;
float _SoftDistance;
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

// Linear-light approximation to incandescent soot: red fringe, orange reaction
// zone, amber interior, tiny pale yellow core. Only the core spends the HDR range.
float3 FireTemperature(float heat)
{
    float3 warm = lerp(float3(1.0, 0.065, 0.004), float3(1.0, 0.38, 0.025),
        smoothstep(0.05, 0.55, heat));
    return lerp(warm, float3(1.0, 0.82, 0.47), smoothstep(0.58, 0.98, heat));
}

// Premultiplied output: fire and embers emit light (alpha zero), smoke absorbs it.
float4 ShadeTorchFire(float3 uvRandom, float4 particle, float time, float depthFade)
{
    float2 uv = uvRandom.xy;
    float seed = uvRandom.z * 93.71;
    float alpha = saturate(particle.a) * depthFade;
    if (_Layer > 1.5)
    {
        float radius = length((uv - 0.5) * float2(2.0, 1.6));
        float spark = pow(saturate(1.0 - radius), 3.0);
        return float4(_BaseColor.rgb * particle.rgb * spark * alpha, 0.0);
    }

    float2 flow = float2(uv.x * 3.6 + seed, uv.y * 2.8 - time * 2.4);
    float billow = FireTurbulence(flow);
    if (_Layer > 0.5)
    {
        float radius = length((uv - 0.5) * 2.0);
        float density = smoothstep(0.25, 0.76, billow)
            * (1.0 - smoothstep(0.25, 0.95, radius));
        float opacity = density * alpha;
        return float4(_BaseColor.rgb * particle.rgb * opacity, opacity);
    }

    // Coarse curls transport fine eddies upward. The shrinking envelope breaks
    // into detached tongues instead of scaling a circular glow sprite.
    float curl = FireNoise(float2(uv.y * 4.1 - time * 1.7, seed + time * 0.4)) - 0.5;
    float x = (uv.x - 0.5) * 2.0 + curl * (0.12 + uv.y * 0.48);
    float width = lerp(0.79, 0.06, pow(saturate(uv.y), 0.72));
    float envelope = 1.0 - abs(x) / max(width, 0.03);
    float detail = FireNoise(flow * float2(2.1, 1.25) + float2(billow, -time));
    float density = envelope + (billow - 0.5) * 1.05 + (detail - 0.5) * uv.y * 0.52;
    float coverage = smoothstep(0.02, 0.3, density);
    coverage *= smoothstep(0.0, 0.09, uv.y) * (1.0 - smoothstep(0.87, 1.0, uv.y));
    float heat = saturate(density * (1.0 - uv.y * 0.65));
    float energy = lerp(0.075, 1.0, heat * heat);
    float intensity = max(_BaseColor.r, max(_BaseColor.g, _BaseColor.b));
    return float4(FireTemperature(heat) * intensity * energy * coverage * alpha, 0.0);
}

#endif
