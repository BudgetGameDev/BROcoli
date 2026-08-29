Shader "BROcoli/MoonlightGodray"
{
    Properties
    {
        [HDR] _Color("Moonlight Color", Color) = (0.55, 0.72, 1.4, 0.085)
        _EdgeSoftness("Edge Softness", Range(0.05, 0.49)) = 0.32
        _NoiseScale("Noise Scale", Range(0.25, 8)) = 2.2
        _NoiseStrength("Noise Strength", Range(0, 0.8)) = 0.28
        _ScrollSpeed("Drift Speed", Range(-1, 1)) = 0.045
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "MoonlightGodray"
            Blend One One
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _EdgeSoftness;
                half _NoiseScale;
                half _NoiseStrength;
                half _ScrollSpeed;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half edge = smoothstep(0.0h, _EdgeSoftness, input.uv.x)
                    * smoothstep(0.0h, _EdgeSoftness, 1.0h - input.uv.x);
                half lowerFade = smoothstep(0.0h, 0.30h, input.uv.y);
                half upperFade = smoothstep(0.0h, 0.30h, 1.0h - input.uv.y);

                half phase = (input.uv.y * _NoiseScale + _Time.y * _ScrollSpeed) * 6.283185h;
                half ripple = 0.5h + 0.5h * sin(phase + sin(input.uv.x * 9.0h));
                half noise = lerp(1.0h, ripple, _NoiseStrength);
                half alpha = _Color.a * edge * lowerFade * upperFade * noise;

                return half4(_Color.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
