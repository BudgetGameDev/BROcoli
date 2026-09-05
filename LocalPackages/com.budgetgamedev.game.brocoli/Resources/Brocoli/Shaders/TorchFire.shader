Shader "BROcoli/Torch Fire"
{
    Properties
    {
        [HDR] _BaseColor("Emission / Smoke Color", Color) = (6, 3, 0.5, 1)
        [Enum(Flame,0,Smoke,1,Ember,2)] _Layer("Layer", Float) = 0
        _SoftDistance("Intersection Softness (metres)", Range(0.01, 1)) = 0.12
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
            #include "TorchFire.hlsl"
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float3 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float3 uv : TEXCOORD0; float eyeDepth : TEXCOORD1; float fog : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.eyeDepth = -position.positionVS.z;
                output.color = input.color;
                output.uv = input.uv;
                output.fog = ComputeFogFactor(position.positionCS.z);
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
                float4 result = ShadeTorchFire(input.uv, input.color, _Time.y, fade);
                result.rgb = MixFogColor(result.rgb, unity_FogColor.rgb * result.a, input.fog);
                return result;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
