// Built-in pipeline post stack: soft-threshold bloom (dual filter), speed radial blur,
// light color grading and vignette. Driven by PostFX.cs via OnRenderImage.
Shader "Hidden/KartGame/PostFX"
{
    Properties { _MainTex ("", 2D) = "white" {} }

    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float4 _Threshold;          // x threshold, y knee
    float _BloomIntensity;
    float _Exposure;
    float _SpeedBlur;
    float _Saturation;
    float _Contrast;
    float _Vignette;
    float4 _Tint;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    half3 Box4(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-d, d).xxyy;
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // 0: prefilter
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target
            {
                half3 c = Box4(i.uv, 1);
                half br = max(c.r, max(c.g, c.b));
                half soft = clamp(br - _Threshold.x + _Threshold.y, 0, 2 * _Threshold.y);
                soft = soft * soft / (4 * _Threshold.y + 1e-4);
                half contrib = max(soft, br - _Threshold.x) / max(br, 1e-4);
                return half4(c * contrib, 1);
            }
            ENDCG
        }
        // 1: downsample
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return half4(Box4(i.uv, 1), 1); }
            ENDCG
        }
        // 2: upsample (additive onto the larger level)
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return half4(Box4(i.uv, 0.5), 1); }
            ENDCG
        }
        // 3: composite
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                half3 c = tex2D(_MainTex, uv).rgb;

                if (_SpeedBlur > 0.001)
                {
                    float2 dir = uv - 0.5;
                    float mask = smoothstep(0.12, 0.6, length(dir));
                    half3 acc = c;
                    for (int k = 1; k < 6; k++)
                        acc += tex2D(_MainTex, uv - dir * (_SpeedBlur * 0.02 * k)).rgb;
                    c = lerp(c, acc / 6, mask);
                }

                c += tex2D(_BloomTex, uv).rgb * _BloomIntensity;

                // Preserve hue and midtones; smoothly roll HDR highlights into display range.
                c = max(c * _Exposure, 0);
                half peak = max(c.r, max(c.g, c.b));
                half shoulder = max(peak - 0.6, 0);
                half mappedPeak = peak <= 0.6 ? peak : 0.6 + 0.4 * shoulder / (shoulder + 0.4);
                c *= mappedPeak / max(peak, 1e-4);

                // grade
                half l = dot(c, half3(0.299, 0.587, 0.114));
                c = lerp(l.xxx, c, _Saturation);
                c = (c - 0.5) * _Contrast + 0.5;
                c *= _Tint.rgb;

                float2 vd = uv - 0.5;
                c *= 1 - dot(vd, vd) * _Vignette;
                return half4(saturate(c), 1);
            }
            ENDCG
        }
    }
}
