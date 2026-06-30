Shader "SmartCity/UI/Glass Panel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _Tint ("Frost Tint", Color) = (1, 1, 1, 0.45)
        _Frost ("Frost Amount", Range(0, 1)) = 0.55
        _BlurMix ("Blur Mix", Range(0, 1)) = 1

        [Header(Convex Edge)]
        _BulgeIntensity ("Bulge Intensity", Range(0, 1)) = 0.55
        _BulgeWidth ("Bulge Width", Range(0.01, 0.25)) = 0.1
        _CenterDim ("Center Dim", Range(0, 0.3)) = 0.06

        [Header(Iridescence)]
        _IridescenceIntensity ("Iridescence Intensity", Range(0, 1)) = 0.35
        _IridescenceWidth ("Iridescence Width", Range(0.005, 0.12)) = 0.035

        [Header(Border and Light)]
        _BorderColor ("Border Color", Color) = (1, 1, 1, 0.85)
        _BorderWidth ("Border Width", Range(0.001, 0.08)) = 0.012

        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _HighlightIntensity ("Highlight Intensity", Range(0, 1)) = 0.35
        _HighlightSize ("Highlight Size", Range(0.02, 0.4)) = 0.16

        _ShadowColor ("Inner Shadow", Color) = (0, 0, 0, 0.25)
        _ShadowIntensity ("Shadow Intensity", Range(0, 1)) = 0.18
        _ShadowSize ("Shadow Size", Range(0.02, 0.4)) = 0.12

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _GlobalUniversalBlurTexture;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            fixed4 _Tint;
            half _Frost;
            half _BlurMix;

            half _BulgeIntensity;
            half _BulgeWidth;
            half _CenterDim;

            half _IridescenceIntensity;
            half _IridescenceWidth;

            fixed4 _BorderColor;
            half _BorderWidth;

            fixed4 _HighlightColor;
            half _HighlightIntensity;
            half _HighlightSize;

            fixed4 _ShadowColor;
            half _ShadowIntensity;
            half _ShadowSize;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            half EdgeDistance(float2 uv)
            {
                float2 d = min(uv, 1.0 - uv);
                return min(d.x, d.y);
            }

            // 가장자리 각도 + 거리로 무지개 색상 변화
            half3 Iridescence(float2 uv, half edge)
            {
                float angle = atan2(uv.y - 0.5, uv.x - 0.5) * 0.15915494; // / 2pi
                half t = frac(angle + edge * 4.0 + uv.x * 0.35);

                half3 a = half3(0.45, 0.72, 1.0);   // blue-cyan
                half3 b = half3(0.92, 0.50, 0.95);  // pink-violet
                half3 c = half3(0.50, 0.95, 0.82);  // mint-green
                half3 d = half3(1.00, 0.78, 0.45);   // warm gold

                if (t < 0.25) return lerp(a, b, t * 4.0);
                if (t < 0.50) return lerp(b, c, (t - 0.25) * 4.0);
                if (t < 0.75) return lerp(c, d, (t - 0.50) * 4.0);
                return lerp(d, a, (t - 0.75) * 4.0);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 mask = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                mask *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                mask.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(mask.a - 0.001);
                #endif

                float2 uv = IN.texcoord;
                float2 screenUv = IN.screenPos.xy / IN.screenPos.w;
                half edge = EdgeDistance(uv);

                half3 blurred = tex2D(_GlobalUniversalBlurTexture, screenUv).rgb;
                half3 frost = lerp(blurred, half3(1, 1, 1), _Frost);
                half3 glass = lerp(blurred, frost * _Tint.rgb, _Tint.a * _BlurMix);

                // 중앙 살짝 어둡게 → 가장자리가 볼록 튀어나온 느낌
                half center = 1.0 - length((uv - 0.5) * 2.0);
                center = smoothstep(0.0, 0.85, center);
                glass *= 1.0 - (1.0 - center) * _CenterDim;

                // 가장자리 볼록 렌즈 밴드 (전체 둘레)
                half bulge = smoothstep(_BulgeWidth, 0.0, edge);
                bulge = pow(bulge, 1.6);
                half bulgePeak = smoothstep(_BulgeWidth * 0.15, 0.0, abs(edge - _BulgeWidth * 0.45));
                bulge = saturate(bulge + bulgePeak * 0.35);
                glass += bulge * _BulgeIntensity;

                // 무지개 이리데센스 — 가장자리 링에만
                half irisBand = smoothstep(_IridescenceWidth, 0.0, edge);
                irisBand *= 1.0 - smoothstep(0.0, _IridescenceWidth * 0.35, edge);
                irisBand = saturate(irisBand + bulge * 0.25);
                half3 iris = Iridescence(uv, edge);
                glass = lerp(glass, glass + iris, irisBand * _IridescenceIntensity);

                // 좌상단 스펙큘러
                half top = smoothstep(1.0 - _HighlightSize, 1.0, uv.y);
                half left = smoothstep(_HighlightSize, 0.0, uv.x);
                glass += _HighlightColor.rgb * (top * left * _HighlightIntensity);

                // 우하단 음영
                half bottom = smoothstep(0.0, _ShadowSize, uv.y);
                half right = smoothstep(1.0 - _ShadowSize, 1.0, uv.x);
                glass = lerp(glass, glass * (1.0 - _ShadowColor.a), bottom * right * _ShadowIntensity);

                // 얇은 밝은 외곽선
                half border = 1.0 - smoothstep(0.0, _BorderWidth, edge);
                glass = lerp(glass, _BorderColor.rgb, border * _BorderColor.a);

                half alpha = mask.a * saturate(_Tint.a + 0.15);
                return fixed4(glass, alpha);
            }
            ENDCG
        }
    }
}
