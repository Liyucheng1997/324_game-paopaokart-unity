// Stylized lake water: depth tint, animated ripples, fresnel sky reflection and sun glints.
Shader "KartGame/Water"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.05,0.35,0.55,0.9)
        _Shallow ("Shallow", Color) = (0.2,0.7,0.8,0.75)
        _Foam ("Foam", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Deep, _Shallow, _Foam, _LightColor0;
            fixed4 _KG_SkyTop, _KG_SkyHorizon;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
                float4 color : COLOR;
                UNITY_FOG_COORDS(1)
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                float3 w = mul(unity_ObjectToWorld, v.vertex).xyz;
                w.y += sin(w.x * 0.35 + _Time.y * 1.3) * 0.05 + cos(w.z * 0.3 + _Time.y * 1.1) * 0.05;
                o.wpos = w;
                o.pos = mul(UNITY_MATRIX_VP, float4(w, 1));
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1,0)), u.x),
                            lerp(hash21(i + float2(0,1)), hash21(i + float2(1,1)), u.x), u.y);
            }
            float waves(float2 p)
            {
                return vnoise(p * 0.6 + _Time.y * float2(0.3, 0.2)) * 0.5
                     + vnoise(p * 1.3 - _Time.y * float2(0.25, 0.35)) * 0.35
                     + vnoise(p * 3.1 + _Time.y * 0.6) * 0.15;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float e = 0.15;
                float h0 = waves(i.wpos.xz);
                float3 n = normalize(float3(h0 - waves(i.wpos.xz + float2(e, 0)), e * 1.6, h0 - waves(i.wpos.xz + float2(0, e))));
                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
                float fres = pow(1 - saturate(dot(n, v)), 3);
                float depth = i.color.r;   // 0 = shore, 1 = deep (baked in vertex color)
                fixed4 col = lerp(_Shallow, _Deep, depth);
                float3 sky = lerp(_KG_SkyHorizon.rgb, _KG_SkyTop.rgb, 0.5);
                col.rgb = lerp(col.rgb, sky, fres * 0.6);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 H = normalize(L + v);
                col.rgb += _LightColor0.rgb * pow(saturate(dot(n, H)), 180) * 1.5;
                float foam = smoothstep(0.25, 0.0, depth) * (0.6 + 0.4 * sin(_Time.y * 2 + i.wpos.x * 0.5));
                col.rgb = lerp(col.rgb, _Foam.rgb, foam * 0.5);
                col.a = saturate(col.a + fres * 0.2);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
