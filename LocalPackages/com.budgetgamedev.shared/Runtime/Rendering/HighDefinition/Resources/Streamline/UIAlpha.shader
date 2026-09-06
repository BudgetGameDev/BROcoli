Shader "Hidden/BudgetGameDev/StreamlineUIAlpha"
{
    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            TEXTURE2D(_UITexture);
            SAMPLER(sampler_UITexture);
            float4 _ViewportScale;
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(uint id : SV_VertexID)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(id);
                output.uv = GetFullScreenTriangleTexCoord(id);
                return output;
            }
            float Frag(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_UITexture, sampler_UITexture, input.uv * _ViewportScale.xy).a;
            }
            ENDHLSL
        }
    }
}
