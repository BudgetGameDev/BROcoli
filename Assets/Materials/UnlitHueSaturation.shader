Shader "Custom/UnlitHueSaturation"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseMap ("Base Map", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _HueShift ("Hue Shift", Range(-180, 180)) = 0
        _Saturation ("Saturation", Range(-100, 100)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull [_Cull]
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Unlit Hue Saturation"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_MainTex);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseMap_ST;
                half4 _Color;
                half4 _BaseColor;
                half _HueShift;
                half _Saturation;
                half _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // RGB to HSV conversion
            half3 RGBtoHSV(half3 c)
            {
                half4 K = half4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                half4 p = lerp(half4(c.bg, K.wz), half4(c.gb, K.xy), step(c.b, c.g));
                half4 q = lerp(half4(p.xyw, c.r), half4(c.r, p.yzx), step(p.x, c.r));
                half d = q.x - min(q.w, q.y);
                half e = 1.0e-10;
                return half3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            // HSV to RGB conversion
            half3 HSVtoRGB(half3 c)
            {
                half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                half3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            // Apply hue shift and saturation adjustment
            half3 ApplyHueSaturation(half3 color, half hueShift, half saturation)
            {
                half3 hsv = RGBtoHSV(color);
                
                // Apply hue shift (convert from degrees to 0-1 range)
                hsv.x += hueShift / 360.0;
                hsv.x = frac(hsv.x); // Wrap around
                
                // Apply saturation (convert from -100 to 100 percent)
                hsv.y *= 1.0 + (saturation / 100.0);
                hsv.y = saturate(hsv.y);
                
                return HSVtoRGB(hsv);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                
                // Try _BaseMap first (URP convention), fall back to _MainTex
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                
                // Use BaseColor if set, otherwise Color
                half4 tint = _BaseColor.a > 0 ? _BaseColor : _Color;
                output.color = input.color * tint;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample texture - try _BaseMap first, then _MainTex
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                if (texColor.a < 0.001)
                    texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                    
                half4 finalColor = texColor * input.color;
                
                // Apply hue/saturation shift
                finalColor.rgb = ApplyHueSaturation(finalColor.rgb, _HueShift, _Saturation);
                
                return finalColor;
            }
            ENDHLSL
        }

        // Fallback pass for SRPDefaultUnlit
        Pass
        {
            Name "Unlit Hue Saturation Fallback"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_MainTex);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseMap_ST;
                half4 _Color;
                half4 _BaseColor;
                half _HueShift;
                half _Saturation;
                half _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half3 RGBtoHSV(half3 c)
            {
                half4 K = half4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                half4 p = lerp(half4(c.bg, K.wz), half4(c.gb, K.xy), step(c.b, c.g));
                half4 q = lerp(half4(p.xyw, c.r), half4(c.r, p.yzx), step(p.x, c.r));
                half d = q.x - min(q.w, q.y);
                half e = 1.0e-10;
                return half3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            half3 HSVtoRGB(half3 c)
            {
                half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                half3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            half3 ApplyHueSaturation(half3 color, half hueShift, half saturation)
            {
                half3 hsv = RGBtoHSV(color);
                hsv.x += hueShift / 360.0;
                hsv.x = frac(hsv.x);
                hsv.y *= 1.0 + (saturation / 100.0);
                hsv.y = saturate(hsv.y);
                return HSVtoRGB(hsv);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                half4 tint = _BaseColor.a > 0 ? _BaseColor : _Color;
                output.color = input.color * tint;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                if (texColor.a < 0.001)
                    texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 finalColor = texColor * input.color;
                finalColor.rgb = ApplyHueSaturation(finalColor.rgb, _HueShift, _Saturation);
                return finalColor;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
