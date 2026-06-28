Shader "SmartCity/BuildingUsage"
{
    Properties
    {
        _ColorPalette ("Color Palette", 2D) = "white" {}
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
                float t : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                // UV2.y: C#에서 기록한 정규화 수요감축 필요도 (0=낮음/초록, 1=높음/빨강)
                output.t = saturate(input.uv2.y);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float4 baseColor = SAMPLE_TEXTURE2D(_ColorPalette, sampler_ColorPalette, float2(input.t, 0.5));

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
