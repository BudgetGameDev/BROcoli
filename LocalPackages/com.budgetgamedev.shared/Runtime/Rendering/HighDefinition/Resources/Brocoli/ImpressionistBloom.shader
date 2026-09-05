Shader "Hidden/BROcoli/ImpressionistBloom"
{
    Properties
    {
        [HideInInspector] _BloomSource("Source", 2DArray) = "black" {}
        [HideInInspector] _BloomLow("Low Mip", 2DArray) = "black" {}
        [HideInInspector] _BloomSourceSize("Source Size", Vector) = (1, 1, 1, 1)
        [HideInInspector] _BloomSourceScale("Source Scale", Vector) = (1, 1, 0, 0)
        [HideInInspector] _BloomLowSize("Low Mip Size", Vector) = (1, 1, 1, 1)
        [HideInInspector] _BloomSettings("Settings", Vector) = (0, 0, 0, 0)
    }
    HLSLINCLUDE
    #pragma target 4.5
    #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

    // Kernel weights and ordering follow Unity Graphics URP 17 Bloom.shader / SetupBloom:
    // HQ 13-sample prefilter, separable Gaussian pyramid and bicubic reconstruction.
    // No URP include or runtime dependency is required by this HDRP front end.
    TEXTURE2D_X(_BloomSource);
    TEXTURE2D_X(_BloomLow);
    float4 _BloomSourceSize; // 1 / valid viewport size, valid viewport size
    float4 _BloomSourceScale; // valid viewport / physical backing texture
    float4 _BloomLowSize; // texture size, 1 / texture size
    float4 _BloomSettings; // linear threshold, knee, remapped scatter, intensity

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };
    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };
    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    float4 Source(float2 uv, float2 offset)
    {
        float2 halfTexel = _BloomSourceSize.xy * 0.5;
        uv = clamp(uv + offset * _BloomSourceSize.xy, halfTexel, 1.0 - halfTexel);
        return SAMPLE_TEXTURE2D_X(_BloomSource, s_linear_clamp_sampler, uv * _BloomSourceScale.xy);
    }
    float3 Low(float2 uv)
    {
        return SampleTexture2DBicubic(TEXTURE2D_X_ARGS(_BloomLow, s_linear_clamp_sampler),
            uv, _BloomLowSize, (1.0).xx, unity_StereoEyeIndex).rgb;
    }

    float4 Prefilter(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.uv;
        float3 a = Source(uv, float2(-1, -1)).rgb;
        float3 b = Source(uv, float2( 0, -1)).rgb;
        float3 c = Source(uv, float2( 1, -1)).rgb;
        float3 d = Source(uv, float2(-0.5, -0.5)).rgb;
        float3 e = Source(uv, float2( 0.5, -0.5)).rgb;
        float3 f = Source(uv, float2(-1, 0)).rgb;
        float3 g = Source(uv, float2( 0, 0)).rgb;
        float3 h = Source(uv, float2( 1, 0)).rgb;
        float3 i = Source(uv, float2(-0.5, 0.5)).rgb;
        float3 j = Source(uv, float2( 0.5, 0.5)).rgb;
        float3 k = Source(uv, float2(-1, 1)).rgb;
        float3 l = Source(uv, float2( 0, 1)).rgb;
        float3 m = Source(uv, float2( 1, 1)).rgb;
        float3 color = (d + e + i + j) * 0.125;
        color += ((a + b + g + f) + (b + c + h + g)
            + (f + g + l + k) + (g + h + m + l)) * 0.03125;
        color = min(color, 65472.0);
        float brightness = Max3(color.r, color.g, color.b);
        float softness = clamp(brightness - _BloomSettings.x + _BloomSettings.y,
            0.0, 2.0 * _BloomSettings.y);
        softness = softness * softness / (4.0 * _BloomSettings.y + 1e-4);
        color *= max(brightness - _BloomSettings.x, softness) / max(brightness, 1e-4);
        return float4(max(color, 0.0), 1.0);
    }

    float4 Horizontal(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.uv;
        float3 color = Source(uv, float2(0, 0)).rgb * 0.22702703;
        color += (Source(uv, float2(-2, 0)).rgb + Source(uv, float2(2, 0)).rgb) * 0.19459459;
        color += (Source(uv, float2(-4, 0)).rgb + Source(uv, float2(4, 0)).rgb) * 0.12162162;
        color += (Source(uv, float2(-6, 0)).rgb + Source(uv, float2(6, 0)).rgb) * 0.05405405;
        color += (Source(uv, float2(-8, 0)).rgb + Source(uv, float2(8, 0)).rgb) * 0.01621622;
        return float4(color, 1.0);
    }
    float4 Vertical(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.uv;
        float3 color = Source(uv, float2(0, 0)).rgb * 0.22702703;
        color += (Source(uv, float2(0, -1.38461538)).rgb
            + Source(uv, float2(0, 1.38461538)).rgb) * 0.31621622;
        color += (Source(uv, float2(0, -3.23076923)).rgb
            + Source(uv, float2(0, 3.23076923)).rgb) * 0.07027027;
        return float4(color, 1.0);
    }
    float4 Upsample(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        return float4(lerp(Source(input.uv, 0).rgb, Low(input.uv), _BloomSettings.z), 1.0);
    }
    float4 Composite(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float4 source = Source(input.uv, 0);
        // Keep all scene highlights and source alpha. The grade / ACES receives the
        // overshooting core plus its halo, with no saturation to the SDR [0, 1] range.
        return float4(source.rgb + Low(input.uv) * _BloomSettings.w, source.a);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off
        Pass
        {
            Name "Prefilter"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Prefilter
            ENDHLSL
        }
        Pass
        {
            Name "Horizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Horizontal
            ENDHLSL
        }
        Pass
        {
            Name "Vertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Vertical
            ENDHLSL
        }
        Pass
        {
            Name "Upsample"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Upsample
            ENDHLSL
        }
        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
    }
    Fallback Off
}
