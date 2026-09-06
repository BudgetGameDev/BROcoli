Shader "BROcoli/Torch Fire"
{
    Properties
    {
        [HDR] _BaseColor("Emission / Smoke Color", Color) = (6, 3, 0.5, 1)
        [Enum(Flame,0,Smoke,1,Ember,2,Heat,3)] _Layer("Layer", Float) = 0
        _HeatStrength("Heat Refraction (pixels)", Range(0, 4)) = 1.6
        _SoftDistance("Intersection Softness (metres)", Range(0.01, 1)) = 0.12
        [HideInInspector] _FlameForwardWS("World Forward", Vector) = (0, 0, 1, 0)
        [HideInInspector] _FlameLeanMetres("Forward Lean", Float) = 0
        [HideInInspector] _FlameHeightMetres("Flame Height", Float) = 0
        [HideInInspector] _FlamePhase("Breathing Phase", Float) = 0
        [HideInInspector] _FlamePlaneWeight("Crossed Sheet Weight", Float) = 1
        [HideInInspector] _AuthoringWhiteNits("Authoring White Nits", Float) = 200
    }
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "TorchFire"
            Tags { "LightMode"="ForwardOnly" }
            Cull Off
            ZWrite Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            #include "TorchFire.hlsl"
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float3 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float3 uv : TEXCOORD0; float eyeDepth : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                world = DeformTorchFlame(world, input.uv.y, _Time.y);
                output.positionCS = TransformWorldToHClip(world);
                output.eyeDepth = -TransformWorldToView(world).z;
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float depth = LoadCameraDepth(input.positionCS.xy);
                float sceneDepth = LinearEyeDepth(depth, _ZBufferParams);
                #if UNITY_REVERSED_Z
                    depth = 1.0 - depth;
                #endif
                sceneDepth = lerp(sceneDepth, lerp(_ProjectionParams.y, _ProjectionParams.z, depth), unity_OrthoParams.w);
                float fade = saturate((sceneDepth - input.eyeDepth) / max(_SoftDistance, 0.001));
                fade *= smoothstep(0.06, 0.3, input.eyeDepth);
                if (_Layer > 2.5)
                {
                    if (_EnableSSRefraction == 0)
                        return 0;
                    float3 heat = TorchHeatRefraction(input.uv, _Time.y, input.color.a, fade);
                    float2 uv = input.positionCS.xy * _ScreenSize.zw;
                    float3 background = SampleCameraColor(saturate(uv + heat.xy * _ScreenSize.zw));
                    // Camera color already carries HDRP exposure; do not expose it twice.
                    return float4(background * heat.z, heat.z);
                }
                float4 result = ShadeTorchFire(input.uv, input.color, _Time.y, fade);
                result.rgb *= _AuthoringWhiteNits * GetCurrentExposureMultiplier();
                return result;
            }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "TorchFire"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "TorchFire.hlsl"
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float3 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float3 uv : TEXCOORD0; float eyeDepth : TEXCOORD1; float fog : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                world = DeformTorchFlame(world, input.uv.y, _Time.y);
                output.positionCS = TransformWorldToHClip(world);
                output.eyeDepth = -TransformWorldToView(world).z;
                output.color = input.color;
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float depth = SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                float sceneDepth = LinearEyeDepth(depth, _ZBufferParams);
                // Orthographic dungeon cameras need linear interpolation of device depth.
                #if UNITY_REVERSED_Z
                    depth = 1.0 - depth;
                #endif
                sceneDepth = lerp(sceneDepth, lerp(_ProjectionParams.y, _ProjectionParams.z, depth), unity_OrthoParams.w);
                float fade = saturate((sceneDepth - input.eyeDepth) / max(_SoftDistance, 0.001));
                fade *= smoothstep(0.06, 0.3, input.eyeDepth);
                if (_Layer > 2.5)
                {
                    if (_CameraOpaqueTexture_TexelSize.z <= 1.0)
                        return 0;
                    float3 heat = TorchHeatRefraction(input.uv, _Time.y, input.color.a, fade);
                    float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                    float3 background = SampleSceneColor(saturate(uv + heat.xy / _ScaledScreenParams.xy));
                    return float4(background * heat.z, heat.z);
                }
                float4 result = ShadeTorchFire(input.uv, input.color, _Time.y, fade);
                result.rgb = MixFogColor(result.rgb, unity_FogColor.rgb * result.a, input.fog);
                return result;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
