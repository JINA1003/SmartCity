Shader "SmartCity/BuildingUsage"
{
    Properties
    {
        _ColorPalette ("Color Palette (100x1)", 2D) = "white" {}
        _RankMap ("Rank Map (100x1)", 2D) = "gray" {}
        _DistrictIndex ("District Index", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_ColorPalette);
            SAMPLER(sampler_ColorPalette);
            TEXTURE2D(_RankMap);
            SAMPLER(sampler_RankMap);

            CBUFFER_START(UnityPerMaterial)
                float _DistrictIndex;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv2 = input.uv2;
                return output;
            }

            float4 SamplePaletteColor(float paletteIndexNormalized)
            {
                float2 paletteUv = float2(paletteIndexNormalized, 0.5);
                return SAMPLE_TEXTURE2D(_ColorPalette, sampler_ColorPalette, paletteUv);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float usageIndex = saturate(input.uv2.y);
                float cellIndex = _DistrictIndex * 4.0 + usageIndex;
                float rankU = (cellIndex + 0.5) / 100.0;
                float paletteIndexNormalized = SAMPLE_TEXTURE2D(_RankMap, sampler_RankMap, float2(rankU, 0.5)).r;
                float4 baseColor = SamplePaletteColor(paletteIndexNormalized);

                Light mainLight = GetMainLight();
                float3 normal = normalize(input.normalWS);
                float ndotl = saturate(dot(normal, mainLight.direction));
                float3 diffuse = baseColor.rgb * (mainLight.color * ndotl + 0.35);

                return half4(diffuse, baseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
