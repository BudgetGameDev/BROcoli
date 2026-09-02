// An additive energy glow for experience pickups, in one asset for every pipeline.
//
// The two subshaders differ only in how they reach Unity's transforms. The Universal one
// takes them from URP's own library. The other one declares the engine's built-in
// constant buffers itself and builds on nothing but the SRP core library, which both
// Universal and High Definition depend on. That is deliberate: a subshader that included
// High Definition's headers would fail to compile on a machine without that package
// installed -- Unity compiles every subshader, not only the one the active pipeline
// selects -- and would put a permanent error in the console of this Universal-only
// project. Declaring the bindings by hand costs a few lines and compiles anywhere.
Shader "BROcoli/XP Energy Glow"
{
    Properties
    {
        [HDR] _CoreColor("Core Color", Color) = (0.06, 0.42, 1, 1)
        [HDR] _RimColor("Rim Color", Color) = (0.35, 0.86, 1, 1)
        _Intensity("Intensity", Float) = 1
        _FresnelPower("Fresnel Power", Range(0.25, 12)) = 3
        _FresnelBias("Fresnel Bias", Range(0, 1)) = 0.02
        [Toggle] _FalloffInverted("Falloff Inverted", Float) = 0
        _BandScale("Band Scale", Float) = 5.5
        _BandSpeed("Band Speed", Float) = 1.7
        _BandSharpness("Band Sharpness", Range(1, 32)) = 6
        _BandStrength("Band Strength", Range(0, 1)) = 0.55
        _PulseSpeed("Pulse Speed", Float) = 4.1
        _PulseAmount("Pulse Amount", Range(0, 1)) = 0.22
        _FlickerSpeed("Flicker Speed", Float) = 19
        _FlickerAmount("Flicker Amount", Range(0, 1)) = 0.18
        _Fade("Fade", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "XpEnergyGlow"
            Tags { "LightMode" = "ForwardOnly" }

            Cull [_Cull]
            ZWrite Off
            ZTest LEqual
            Blend One One

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            // Unity binds these for any pipeline. Their layout matches the engine's, so a
            // pipeline running the SRP Batcher can still batch this material.
            CBUFFER_START(UnityPerDraw)
                float4x4 unity_ObjectToWorld;
                float4x4 unity_WorldToObject;
                float4 unity_LODFade;
                real4 unity_WorldTransformParams;
            CBUFFER_END

            float4x4 glstate_matrix_projection;
            float4x4 unity_MatrixV;
            float4x4 unity_MatrixInvV;
            float4x4 unity_MatrixVP;
            float4x4 unity_MatrixPreviousM;
            float4x4 unity_MatrixPreviousMI;
            float3 _WorldSpaceCameraPos;
            float4 _Time;

            #define UNITY_MATRIX_M unity_ObjectToWorld
            #define UNITY_MATRIX_I_M unity_WorldToObject
            #define UNITY_MATRIX_V unity_MatrixV
            #define UNITY_MATRIX_I_V unity_MatrixInvV
            #define UNITY_MATRIX_P glstate_matrix_projection
            #define UNITY_MATRIX_VP unity_MatrixVP
            #define UNITY_PREV_MATRIX_M unity_MatrixPreviousM
            #define UNITY_PREV_MATRIX_I_M unity_MatrixPreviousMI

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            #include "Packages/com.budgetgamedev.game.brocoli/Resources/Brocoli/Shaders/XpEnergyGlow.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 viewDirectionWS = _WorldSpaceCameraPos - input.positionWS;
                float3 glow = XpGlowShade(
                    input.positionOS,
                    input.normalWS,
                    viewDirectionWS,
                    _Time.y
                );
                return float4(glow, 0.0);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "XpEnergyGlow"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite Off
            ZTest LEqual
            Blend One One

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.budgetgamedev.game.brocoli/Resources/Brocoli/Shaders/XpEnergyGlow.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(
                    input.positionOS.xyz
                );
                output.positionOS = input.positionOS.xyz;
                output.positionWS = positionInputs.positionWS;
                output.positionCS = positionInputs.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 viewDirectionWS = GetWorldSpaceViewDir(input.positionWS);
                float3 glow = XpGlowShade(
                    input.positionOS,
                    input.normalWS,
                    viewDirectionWS,
                    _Time.y
                );
                return float4(glow, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
