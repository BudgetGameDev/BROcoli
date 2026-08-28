Shader "BROcoli/Dungeon Occlusion Fade"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        [HideInInspector] _OcclusionFade("Occlusion Fade", Range(0, 1)) = 0
        [HideInInspector] _FadeStartY("Fade Start Y", Float) = 0
        [HideInInspector] _FadeFeather("Fade Feather", Float) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half _OcclusionFade;
                float _FadeStartY;
                float _FadeFeather;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half DitherNoise(float2 pixelPosition)
            {
                float2 pixel = floor(pixelPosition);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            void ApplyOcclusionClip(float2 pixelPosition, float worldY)
            {
                half heightMask = smoothstep(
                    _FadeStartY,
                    _FadeStartY + max(_FadeFeather, 0.001),
                    worldY
                );
                clip(DitherNoise(pixelPosition) - saturate(_OcclusionFade) * heightMask);
            }

            half3 EvaluateLight(
                Light light,
                half3 normalWS,
                half3 viewDirectionWS,
                half3 albedo,
                half3 specularColor
            )
            {
                half attenuation = light.distanceAttenuation * light.shadowAttenuation;
                half normalDotLight = saturate(dot(normalWS, light.direction));
                half3 halfDirection = SafeNormalize(light.direction + viewDirectionWS);
                half specularPower = exp2(1.0h + _Smoothness * 10.0h);
                half specular = pow(saturate(dot(normalWS, halfDirection)), specularPower);
                return light.color
                    * attenuation
                    * (albedo * normalDotLight + specularColor * specular * normalDotLight);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                ApplyOcclusionClip(input.positionCS.xy, input.positionWS.y);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv)
                    * _BaseColor;
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 specularColor = lerp(0.04h.xxx, baseSample.rgb, _Metallic);
                half3 diffuseColor = baseSample.rgb * (1.0h - _Metallic);
                half3 color = SampleSH(normalWS) * diffuseColor;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                color += EvaluateLight(
                    mainLight,
                    normalWS,
                    viewDirectionWS,
                    diffuseColor,
                    specularColor
                );

                #if defined(_ADDITIONAL_LIGHTS)
                    uint lightCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < lightCount; lightIndex++)
                    {
                        Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS);
                        color += EvaluateLight(
                            additionalLight,
                            normalWS,
                            viewDirectionWS,
                            diffuseColor,
                            specularColor
                        );
                    }
                #endif

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Occlusion fading is camera-only. The wall remains physically present,
        // so its complete geometry must continue to cast its normal shadow.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }

    FallBack Off
}
