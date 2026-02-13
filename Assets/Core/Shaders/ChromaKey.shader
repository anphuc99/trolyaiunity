/// <summary>
/// Chroma Key Shader - Removes green screen background
/// Works on both PC and Mobile platforms
/// Uses HSV-based color matching for better accuracy
/// </summary>
Shader "Custom/ChromaKey"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        
        [Header(Chroma Key Settings)]
        _KeyColor ("Key Color", Color) = (0, 1, 0, 1)
        _HueTolerance ("Hue Tolerance", Range(0, 0.5)) = 0.1
        _SaturationTolerance ("Saturation Tolerance", Range(0, 1)) = 0.3
        _ValueTolerance ("Value Tolerance", Range(0, 1)) = 0.3
        _Smoothness ("Edge Smoothness", Range(0, 0.5)) = 0.05
        
        [Header(Color Correction)]
        _SpillRemoval ("Spill Removal", Range(0, 1)) = 0.5
        _Brightness ("Brightness", Range(0.5, 2)) = 1
        _Contrast ("Contrast", Range(0.5, 2)) = 1
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
            
            fixed4 _KeyColor;
            half _HueTolerance;
            half _SaturationTolerance;
            half _ValueTolerance;
            half _Smoothness;
            half _SpillRemoval;
            half _Brightness;
            half _Contrast;
            
            /// <summary>
            /// Converts RGB color to HSV color space
            /// More efficient for chroma key detection
            /// </summary>
            /// <param name="rgb">Input RGB color</param>
            /// <returns>HSV color (x=Hue, y=Saturation, z=Value)</returns>
            float3 RGBtoHSV(float3 rgb)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(rgb.bg, K.wz), float4(rgb.gb, K.xy), step(rgb.b, rgb.g));
                float4 q = lerp(float4(p.xyw, rgb.r), float4(rgb.r, p.yzx), step(p.x, rgb.r));
                
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                
                return float3(
                    abs(q.z + (q.w - q.y) / (6.0 * d + e)),
                    d / (q.x + e),
                    q.x
                );
            }
            
            /// <summary>
            /// Calculates the chroma key mask based on HSV comparison
            /// Returns 0 for pixels to remove, 1 for pixels to keep
            /// </summary>
            /// <param name="pixelHSV">HSV of current pixel</param>
            /// <param name="keyHSV">HSV of key color</param>
            /// <returns>Alpha mask value</returns>
            half CalculateChromaMask(float3 pixelHSV, float3 keyHSV)
            {
                // Calculate hue difference (handle wraparound at 0/1)
                float hueDiff = abs(pixelHSV.x - keyHSV.x);
                hueDiff = min(hueDiff, 1.0 - hueDiff);
                
                // Calculate saturation and value differences
                float satDiff = abs(pixelHSV.y - keyHSV.y);
                float valDiff = abs(pixelHSV.z - keyHSV.z);
                
                // Check if pixel matches key color within tolerance
                // Weight saturation more heavily as green screens are usually saturated
                float hueMatch = smoothstep(_HueTolerance + _Smoothness, _HueTolerance, hueDiff);
                float satMatch = smoothstep(_SaturationTolerance + _Smoothness, _SaturationTolerance, satDiff);
                float valMatch = smoothstep(_ValueTolerance + _Smoothness, _ValueTolerance, valDiff);
                
                // Require minimum saturation to be considered green screen
                // This prevents low-saturation grey/white pixels from being removed
                float saturationGate = smoothstep(0.1, 0.2, pixelHSV.y);
                
                // Combine all matches - pixel is keyed if all conditions match
                float keyMatch = hueMatch * satMatch * valMatch * saturationGate;
                
                // Return inverted (1 = keep, 0 = remove)
                return 1.0 - keyMatch;
            }
            
            /// <summary>
            /// Removes green color spill from edges of keyed objects
            /// Common issue when green reflects onto subjects
            /// </summary>
            /// <param name="color">Original pixel color</param>
            /// <param name="keyColor">Key color to remove spill of</param>
            /// <param name="amount">Spill removal strength</param>
            /// <returns>Color with reduced green spill</returns>
            fixed3 RemoveSpill(fixed3 color, fixed3 keyColor, half amount)
            {
                // Calculate how much the pixel is tinted toward key color
                float spillAmount = max(0, color.g - max(color.r, color.b));
                
                // Reduce the green channel to remove spill
                color.g = lerp(color.g, max(color.r, color.b), amount * spillAmount * 2);
                
                return color;
            }
            
            /// <summary>
            /// Applies brightness and contrast adjustments
            /// </summary>
            /// <param name="color">Input color</param>
            /// <returns>Adjusted color</returns>
            fixed3 ApplyColorCorrection(fixed3 color)
            {
                // Apply contrast (centered at 0.5)
                color = (color - 0.5) * _Contrast + 0.5;
                
                // Apply brightness
                color *= _Brightness;
                
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
                
                // Convert pixel and key color to HSV
                float3 pixelHSV = RGBtoHSV(col.rgb);
                float3 keyHSV = RGBtoHSV(_KeyColor.rgb);
                
                // Calculate the chroma key mask
                half mask = CalculateChromaMask(pixelHSV, keyHSV);
                
                // Remove green spill from edges
                col.rgb = RemoveSpill(col.rgb, _KeyColor.rgb, _SpillRemoval);
                
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
