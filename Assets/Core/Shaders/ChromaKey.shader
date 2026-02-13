/// <summary>
/// White Background Removal Shader
/// Works on both PC and Mobile platforms
/// Optimized for removing white/light backgrounds using luminance-based detection
/// </summary>
Shader "Custom/ChromaKey"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        
        [Header(White Background Settings)]
        _LuminanceThreshold ("Luminance Threshold", Range(0.5, 1)) = 0.85
        _SaturationMax ("Max Saturation (white)", Range(0, 0.5)) = 0.15
        _Smoothness ("Edge Smoothness", Range(0, 0.3)) = 0.1
        
        [Header(Edge Detection)]
        _EdgeSensitivity ("Edge Sensitivity", Range(0, 5)) = 2
        _EdgeThickness ("Edge Thickness", Range(0.001, 0.01)) = 0.003
        
        [Header(Color Correction)]
        _Brightness ("Brightness", Range(0.5, 2)) = 1
        _Contrast ("Contrast", Range(0.5, 2)) = 1
        _Saturation ("Saturation Boost", Range(0.5, 2)) = 1
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }
        
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            // Mobile compatibility
            #pragma target 2.0
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            
            half _LuminanceThreshold;
            half _SaturationMax;
            half _Smoothness;
            half _EdgeSensitivity;
            half _EdgeThickness;
            half _Brightness;
            half _Contrast;
            half _Saturation;
            
            /// <summary>
            /// Calculates luminance from RGB color
            /// Uses standard NTSC/Rec601 weights
            /// </summary>
            /// <param name="rgb">Input RGB color</param>
            /// <returns>Luminance value 0-1</returns>
            half GetLuminance(fixed3 rgb)
            {
                return dot(rgb, half3(0.299, 0.587, 0.114));
            }
            
            /// <summary>
            /// Calculates saturation from RGB color
            /// </summary>
            /// <param name="rgb">Input RGB color</param>
            /// <returns>Saturation value 0-1</returns>
            half GetSaturation(fixed3 rgb)
            {
                float maxC = max(rgb.r, max(rgb.g, rgb.b));
                float minC = min(rgb.r, min(rgb.g, rgb.b));
                return (maxC - minC) / (maxC + 0.0001);
            }
            
            /// <summary>
            /// Detects edges using Sobel operator for better edge preservation
            /// </summary>
            /// <param name="uv">Current UV coordinates</param>
            /// <returns>Edge strength 0-1</returns>
            half DetectEdge(float2 uv)
            {
                float2 offset = _EdgeThickness;
                
                // Sample 3x3 neighborhood luminances
                half tl = GetLuminance(tex2D(_MainTex, uv + float2(-offset.x, offset.y)).rgb);
                half t  = GetLuminance(tex2D(_MainTex, uv + float2(0, offset.y)).rgb);
                half tr = GetLuminance(tex2D(_MainTex, uv + float2(offset.x, offset.y)).rgb);
                half l  = GetLuminance(tex2D(_MainTex, uv + float2(-offset.x, 0)).rgb);
                half r  = GetLuminance(tex2D(_MainTex, uv + float2(offset.x, 0)).rgb);
                half bl = GetLuminance(tex2D(_MainTex, uv + float2(-offset.x, -offset.y)).rgb);
                half b  = GetLuminance(tex2D(_MainTex, uv + float2(0, -offset.y)).rgb);
                half br = GetLuminance(tex2D(_MainTex, uv + float2(offset.x, -offset.y)).rgb);
                
                // Sobel operators
                half sobelX = (tr + 2*r + br) - (tl + 2*l + bl);
                half sobelY = (tl + 2*t + tr) - (bl + 2*b + br);
                
                // Edge magnitude
                return saturate(sqrt(sobelX * sobelX + sobelY * sobelY) * _EdgeSensitivity);
            }
            
            /// <summary>
            /// Calculates mask for white background removal
            /// High luminance + low saturation = white background
            /// </summary>
            /// <param name="rgb">Pixel color</param>
            /// <param name="uv">UV coordinates for edge detection</param>
            /// <returns>Alpha mask (0 = remove, 1 = keep)</returns>
            half CalculateWhiteMask(fixed3 rgb, float2 uv)
            {
                half luminance = GetLuminance(rgb);
                half saturation = GetSaturation(rgb);
                
                // White detection: high luminance AND low saturation
                half isWhite = smoothstep(_LuminanceThreshold - _Smoothness, _LuminanceThreshold + _Smoothness, luminance);
                half isDesaturated = smoothstep(_SaturationMax + _Smoothness, _SaturationMax - _Smoothness, saturation);
                
                // Combine conditions - pixel is white background if both match
                half whiteMatch = isWhite * isDesaturated;
                
                // Detect edges to preserve subject boundaries
                half edge = DetectEdge(uv);
                
                // Reduce removal at edges to preserve subject outline
                whiteMatch *= (1.0 - edge);
                
                // Return inverted (1 = keep, 0 = remove)
                return 1.0 - whiteMatch;
            }
            
            /// <summary>
            /// Applies brightness, contrast, and saturation adjustments
            /// </summary>
            /// <param name="color">Input color</param>
            /// <returns>Adjusted color</returns>
            fixed3 ApplyColorCorrection(fixed3 color)
            {
                // Apply contrast (centered at 0.5)
                color = (color - 0.5) * _Contrast + 0.5;
                
                // Apply brightness
                color *= _Brightness;
                
                // Apply saturation boost
                half lum = GetLuminance(color);
                color = lerp(half3(lum, lum, lum), color, _Saturation);
                
                return saturate(color);
            }
            
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, o.vertex);
                
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // Sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Calculate the white background mask
                half mask = CalculateWhiteMask(col.rgb, i.uv);
                
                // Apply color correction
                col.rgb = ApplyColorCorrection(col.rgb);
                
                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                
                // Set alpha based on mask
                col.a *= mask;
                
                return col;
            }
            ENDCG
        }
    }
    
    // Fallback for very old devices
    FallBack "Transparent/Diffuse"
}
