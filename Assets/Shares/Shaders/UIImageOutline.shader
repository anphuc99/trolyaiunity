// Shader tạo hiệu ứng outline cho UI Image trong Unity.
// Sử dụng bằng cách tạo Material với shader này và gán vào Image component.
Shader "Custom/UI/ImageOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        // Outline properties
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0, 100)) = 1
        
        // Stencil properties for UI masking
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
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
        Blend One OneMinusSrcAlpha
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
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 mask : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Sample 16 hướng tại một khoảng cách cho outline mượt hơn
            fixed SampleOutlineRing(float2 uv, float2 offset)
            {
                fixed alpha = 0;
                
                // 4 hướng chính: trên, dưới, trái, phải
                alpha = max(alpha, tex2D(_MainTex, uv + float2(offset.x, 0)).a);
                alpha = max(alpha, tex2D(_MainTex, uv + float2(-offset.x, 0)).a);
                alpha = max(alpha, tex2D(_MainTex, uv + float2(0, offset.y)).a);
                alpha = max(alpha, tex2D(_MainTex, uv + float2(0, -offset.y)).a);
                
                // 4 góc chéo
                alpha = max(alpha, tex2D(_MainTex, uv + float2(offset.x, offset.y)).a);
                alpha = max(alpha, tex2D(_MainTex, uv + float2(-offset.x, offset.y)).a);
                alpha = max(alpha, tex2D(_MainTex, uv + float2(offset.x, -offset.y)).a);
                alpha = max(alpha, tex2D(_MainTex, uv + float2(-offset.x, -offset.y)).a);
                
                // 8 hướng phụ (góc 22.5°, 67.5°, etc.) để lấp khoảng trống
                float d = 0.7071; // cos(45°) = sin(45°)
                float d1 = 0.3827; // sin(22.5°)
                float d2 = 0.9239; // cos(22.5°)
                
                alpha = max(alpha, tex2D(_MainTex, uv + float2(offset.x * d2, offset.y * d1)).a);
                alpha = max(alpha, tex2D(_MainTex, uv + float2(offset.x * d1, offset.y * d2)).a);
                alpha = max(alpha, tex2D(_MainTex, uv + float2(-offset.x * d2, offset.y * d1)).a);
                alpha = max(alpha, tex2D(_MainTex, uv + float2(-offset.x * d1, offset.y * d2)).a);
                alpha = max(alpha, tex2D(_MainTex, uv + float2(offset.x * d2, -offset.y * d1)).a);
                alpha = max(alpha, tex2D(_MainTex, uv + float2(offset.x * d1, -offset.y * d2)).a);
                alpha = max(alpha, tex2D(_MainTex, uv + float2(-offset.x * d2, -offset.y * d1)).a);
                alpha = max(alpha, tex2D(_MainTex, uv + float2(-offset.x * d1, -offset.y * d2)).a);
                
                return alpha;
            }
            
            // Sample nhiều lớp từ trong ra ngoài để tạo outline dày đặc
            fixed SampleOutlineMultiLayer(float2 uv, float2 baseOffset, int layers)
            {
                fixed alpha = 0;
                
                // Sample nhiều lớp với khoảng cách tăng dần
                for (int i = 1; i <= layers; i++)
                {
                    float scale = (float)i / (float)layers;
                    float2 offset = baseOffset * scale;
                    alpha = max(alpha, SampleOutlineRing(uv, offset));
                }
                
                return alpha;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Sample texture chính
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // Tính offset dựa trên kích thước texture và độ rộng outline
                float2 offset = _MainTex_TexelSize.xy * _OutlineWidth;
                
                // Xác định số lớp dựa trên độ rộng outline (tối thiểu 2, tối đa 8)
                int layers = clamp((int)ceil(_OutlineWidth / 2.0), 2, 8);
                
                // Lấy alpha của outline từ nhiều lớp sample
                fixed outlineAlpha = SampleOutlineMultiLayer(IN.texcoord, offset, layers);
                
                // Tính toán outline color với alpha
                fixed4 outlineCol = _OutlineColor;
                outlineCol.a *= outlineAlpha;
                
                // Mix giữa outline và color gốc
                // Nếu pixel gốc có alpha > 0, hiển thị pixel gốc
                // Nếu không, hiển thị outline (nếu có)
                fixed4 result;
                result.rgb = lerp(outlineCol.rgb, color.rgb, color.a);
                result.a = max(color.a, outlineCol.a * (1 - color.a));
                
                // Apply vertex color alpha
                result.a *= IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                result.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif

                // Premultiply alpha cho blending đúng
                result.rgb *= result.a;

                return result;
            }
            ENDCG
        }
    }
}
