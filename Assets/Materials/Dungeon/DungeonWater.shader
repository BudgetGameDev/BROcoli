Shader "BROcoli/Dungeon Water"
{
    Properties
    {
        [HDR] _ShallowColor("Shallow Color", Color) = (0.03, 0.42, 0.72, 0.84)
        [HDR] _DeepColor("Deep Color", Color) = (0.008, 0.12, 0.38, 0.94)
        [HDR] _FoamColor("Edge Highlight", Color) = (0.15, 0.75, 1.0, 0.42)
        _WaveScale("Ripple Scale", Range(0.2, 8.0)) = 2.2
        _WaveSpeed("Ripple Speed", Range(0.0, 2.0)) = 0.45
        _RippleStrength("Ripple Strength", Range(0.0, 0.5)) = 0.14
        _ColorVariation("Color Variation", Range(0.0, 0.5)) = 0.24
        _RippleHighlight("Ripple Highlight", Range(0.0, 0.25)) = 0.075
        _ElevationVariation("Elevation Variation", Range(0.0, 0.02)) = 0.007
        _PoolingStrength("Off-Center Pooling", Range(0.0, 0.6)) = 0.3
        _PoolingRadius("Pooling Radius", Range(0.12, 0.5)) = 0.3
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.74
        _SpecularStrength("Specular Strength", Range(0.0, 2.0)) = 0.52
        _FresnelStrength("Fresnel Strength", Range(0.0, 1.0)) = 0.3
        _EdgeHighlight("Edge Highlight", Range(0.0, 1.0)) = 0.42
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float _WaveScale;
                float _WaveSpeed;
                float _RippleStrength;
                half _ColorVariation;
                half _RippleHighlight;
                float _ElevationVariation;
                half _PoolingStrength;
                half _PoolingRadius;
                half _Smoothness;
                half _SpecularStrength;
                half _FresnelStrength;
                half _EdgeHighlight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 GetPoolFocus()
            {
                float2 objectOrigin = TransformObjectToWorld(float3(0.0, 0.0, 0.0)).xz;
                float randomX = frac(sin(dot(objectOrigin, float2(12.9898, 78.233))) * 43758.5453);
                float randomY = frac(sin(dot(objectOrigin, float2(39.3468, 11.135))) * 24634.6345);
                return lerp(float2(0.34, 0.34), float2(0.66, 0.66), float2(randomX, randomY));
            }

            half GetPoolingWeight(float2 uv, float2 poolFocus)
            {
                float2 delta = (uv - poolFocus) * float2(1.02, 0.96);
                float distanceFromFocus = length(delta);
                return 1.0h - smoothstep(_PoolingRadius * 0.35h, _PoolingRadius, distanceFromFocus);
            }

            Varyings WaterVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 undisplacedPositionWS = TransformObjectToWorld(input.positionOS.xyz);
                float time = _Time.y * _WaveSpeed;
                float2 waterPosition = undisplacedPositionWS.xz * _WaveScale;
                const float2 directionA = float2(0.8192, 0.5735);
                const float2 directionB = float2(-0.4472, 0.8944);
                const float2 directionC = float2(0.9656, -0.2599);
                float elevationWaves = sin(dot(waterPosition, directionA) + time) * 0.46
                    + sin(dot(waterPosition * 1.63, directionB) - time * 1.34) * 0.34
                    + sin(dot(waterPosition * 0.71, directionC) + time * 0.63) * 0.2;
                float radialDistance = length((input.uv - 0.5) * float2(1.02, 0.94));
                float anchoredInterior = 1.0 - smoothstep(0.33, 0.49, radialDistance);
                half poolingWeight = GetPoolingWeight(input.uv, GetPoolFocus());
                float elevation = elevationWaves
                    * _ElevationVariation
                    * anchoredInterior
                    * lerp(1.0, 0.58, poolingWeight);
                elevation -= poolingWeight * _ElevationVariation * 0.55;
                input.positionOS.y += elevation;

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half3 ShadeWaterLight(
                Light light,
                half3 surfaceColor,
                half3 normalWS,
                half3 viewDirectionWS
            )
            {
                half attenuation = light.distanceAttenuation * light.shadowAttenuation;
                half diffuse = saturate(dot(normalWS, light.direction));
                half3 halfDirection = SafeNormalize(light.direction + viewDirectionWS);
                half specularPower = lerp(18.0h, 112.0h, _Smoothness);
                half specular = pow(saturate(dot(normalWS, halfDirection)), specularPower)
                    * _SpecularStrength;
                return (surfaceColor * diffuse * 0.95h + specular)
                    * light.color
                    * attenuation;
            }

            half4 WaterFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float time = _Time.y * _WaveSpeed;
                float2 waterPosition = input.positionWS.xz * _WaveScale;
                float2 poolFocus = GetPoolFocus();
                half poolingWeight = GetPoolingWeight(input.uv, poolFocus);

                const float2 directionA = float2(0.8192, 0.5735);
                const float2 directionB = float2(-0.4472, 0.8944);
                const float2 directionC = float2(0.9656, -0.2599);
                float phaseA = dot(waterPosition, directionA) + time;
                float phaseB = dot(waterPosition * 1.63, directionB) - time * 1.34;
                float phaseC = dot(waterPosition * 0.71, directionC) + time * 0.63;
                float waveA = sin(phaseA);
                float waveB = sin(phaseB);
                float waveC = sin(phaseC);
                float variation = waveA * 0.46 + waveB * 0.34 + waveC * 0.2;

                float2 slope = cos(phaseA) * directionA * 0.46
                    + cos(phaseB) * directionB * 0.55
                    + cos(phaseC) * directionC * 0.14;
                slope += (input.uv - poolFocus) * poolingWeight * _PoolingStrength * 0.32;
                half3 rippleNormal = normalize(half3(
                    -slope.x * _RippleStrength,
                    1.0h,
                    -slope.y * _RippleStrength
                ));
                half surfaceIsUp = step(0.0h, input.normalWS.y);
                rippleNormal.y *= lerp(-1.0h, 1.0h, surfaceIsUp);
                half3 normalWS = normalize(lerp(normalize(input.normalWS), rippleNormal, 0.92h));
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float radialDistance = length((input.uv - 0.5) * float2(1.02, 0.94));
                half edge = smoothstep(0.34, 0.51, radialDistance);
                half depth = saturate(
                    (1.0h - edge) * 0.76h
                    + variation * _ColorVariation
                    + poolingWeight * _PoolingStrength
                );
                half3 surfaceColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth);

                half3 ambient = max(SampleSH(normalWS), half3(0.25h, 0.3h, 0.38h));
                half3 litColor = surfaceColor * ambient;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                litColor += ShadeWaterLight(mainLight, surfaceColor, normalWS, viewDirectionWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirectionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightCount = GetAdditionalLightsCount();

                    #if USE_CLUSTER_LIGHT_LOOP
                        [loop] for (uint lightIndex = 0u;
                            lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS);
                            ++lightIndex)
                        {
                            Light light = GetAdditionalLight(lightIndex, input.positionWS);
                            litColor += ShadeWaterLight(
                                light,
                                surfaceColor,
                                normalWS,
                                viewDirectionWS
                            );
                        }
                    #endif

                    LIGHT_LOOP_BEGIN(additionalLightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS);
                        litColor += ShadeWaterLight(
                            light,
                            surfaceColor,
                            normalWS,
                            viewDirectionWS
                        );
                    LIGHT_LOOP_END
                #endif

                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirectionWS)), 4.0h);
                half rippleHighlightA = pow(saturate(waveA * 0.5h + 0.5h), 8.0h);
                half rippleHighlightB = pow(saturate(waveB * 0.5h + 0.5h), 10.0h);
                half rippleHighlight = saturate(rippleHighlightA * 0.62h + rippleHighlightB * 0.48h);
                rippleHighlight *= lerp(1.0h, 0.68h, poolingWeight * _PoolingStrength);
                half edgeRipple = edge * saturate(0.56h + variation * 0.44h) * _EdgeHighlight;
                litColor += _FoamColor.rgb * (_FoamColor.a * edgeRipple);
                litColor += lerp(_ShallowColor.rgb, _FoamColor.rgb, 0.65h)
                    * (fresnel * _FresnelStrength + rippleHighlight * _RippleHighlight);

                litColor = MixFog(litColor, input.fogFactor);
                half alpha = lerp(_ShallowColor.a, _DeepColor.a, depth);
                alpha = saturate(alpha + fresnel * 0.08h + edgeRipple * _FoamColor.a * 0.08h);
                return half4(litColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
