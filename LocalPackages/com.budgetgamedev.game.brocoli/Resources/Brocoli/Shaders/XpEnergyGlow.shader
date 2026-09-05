// Additive pickup glow with native transforms for each rendering pipeline.
// The shared shading keeps pickup colors and animation consistent.
Shader "BROcoli/XP Energy Glow"
{
    Properties
    {
        [HDR] _CoreColor("Core Color", Color) = (0.06, 0.42, 1, 1)
        [HDR] _RimColor("Rim Color", Color) = (0.35, 0.86, 1, 1)
        _Intensity("Intensity", Float) = 1
        [HideInInspector] _AuthoringWhiteNits("Authoring White Nits", Float) = 200
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
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
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
                float3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 glow = XpGlowShade(
                    input.positionOS,
                    input.normalWS,
                    viewDirectionWS,
                    _Time.y
                );
                // Colors are authored in URP scene units. HDRP exposes physical luminance;
                // convert with the same white reference used by the dungeon's fixed EV.
                return float4(glow * _AuthoringWhiteNits * GetCurrentExposureMultiplier(), 0.0);
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
