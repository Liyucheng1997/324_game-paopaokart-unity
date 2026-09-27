// Stylized lit shader used by every generated mesh (karts, track, props).
// - vertex colors tint the albedo (all procedural meshes carry colors)
// - uv2.x adds per-vertex gloss, vertex alpha < 1 marks emissive parts
// - soft toon ramp + rim light + cheap sky reflection for a clean "kart game" look
Shader "KartGame/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Gloss ("Gloss", Range(0,1)) = 0.15
        _RimColor ("Rim Color (a = strength)", Color) = (1,1,1,0.25)
        _Emission ("Emission", Color) = (0,0,0,0)
        _Scroll ("UV Scroll (xy)", Vector) = (0,0,0,0)
        _WorldUV ("World-space UV scale (0 = mesh UV)", Float) = 0
        _Wrap ("Toon softness", Range(0.02,1)) = 0.45
        _Detail ("Detail noise strength", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Toon fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        #include "UnityPBSLighting.cginc"

        sampler2D _MainTex;
        float4 _MainTex_ST;
        fixed4 _Color;
        half _Gloss;
        fixed4 _RimColor;
        fixed4 _Emission;
        float4 _Scroll;
        float _WorldUV;
        half _Wrap;
        half _Detail;

        // set globally from the environment so glossy paint reflects the sky
        fixed4 _KG_SkyTop;
        fixed4 _KG_SkyHorizon;
        fixed4 _KG_Ground;

        struct Input
        {
            float2 texCoord;
            float2 extra;
            float4 color : COLOR;
            float3 worldPos;
        };

        struct SurfaceOutputToon
        {
            fixed3 Albedo;
            fixed3 Normal;
            fixed3 Emission;
            half Gloss;
            fixed Alpha;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.texCoord = v.texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw + _Scroll.xy * _Time.y;
            o.extra = v.texcoord1.xy;
        }

        float hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float vnoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            float2 u = f * f * (3 - 2 * f);
            return lerp(lerp(hash21(i), hash21(i + float2(1,0)), u.x),
                        lerp(hash21(i + float2(0,1)), hash21(i + float2(1,1)), u.x), u.y);
        }

        void surf(Input IN, inout SurfaceOutputToon o)
        {
            float2 uv = _WorldUV > 0 ? IN.worldPos.xz * _WorldUV : IN.texCoord;
            fixed4 tex = tex2D(_MainTex, uv);
            fixed3 albedo = tex.rgb * _Color.rgb * IN.color.rgb;
            if (_Detail > 0)
            {
                float n = vnoise(IN.worldPos.xz * 0.35) * 0.6 + vnoise(IN.worldPos.xz * 1.7) * 0.4;
                albedo *= lerp(1, 0.78 + n * 0.44, _Detail);
            }
            o.Albedo = albedo;
            o.Alpha = 1;
            o.Gloss = saturate(_Gloss + IN.extra.x);
            half emissive = saturate(1 - IN.color.a) * 4;
            o.Emission = albedo * emissive + _Emission.rgb;
        }

        half4 LightingToon(SurfaceOutputToon s, half3 viewDir, UnityGI gi)
        {
            half3 N = normalize(s.Normal);
            half3 L = gi.light.dir;
            half ndl = dot(N, L);
            half ramp = smoothstep(-_Wrap * 0.35, _Wrap, ndl);

            half3 diffuse = s.Albedo * (gi.light.color * ramp + gi.indirect.diffuse);

            half3 H = normalize(L + viewDir);
            half specPow = exp2(s.Gloss * 9 + 2);
            half spec = pow(saturate(dot(N, H)), specPow) * s.Gloss * 1.6;
            spec = spec * smoothstep(0.0, 0.2, ndl);

            half fres = pow(1 - saturate(dot(N, viewDir)), 4);
            half3 R = reflect(-viewDir, N);
            half3 env = R.y > 0 ? lerp(_KG_SkyHorizon.rgb, _KG_SkyTop.rgb, saturate(R.y * 1.5))
                                : lerp(_KG_SkyHorizon.rgb, _KG_Ground.rgb, saturate(-R.y * 3));
            half3 refl = env * (0.04 + fres * 0.7) * s.Gloss;

            half rim = pow(1 - saturate(dot(N, viewDir)), 3) * _RimColor.a * (0.35 + ramp * 0.65);

            half4 c;
            c.rgb = diffuse + gi.light.color * spec + refl + _RimColor.rgb * rim;
            c.a = 1;
            return c;
        }

        void LightingToon_GI(SurfaceOutputToon s, UnityGIInput data, inout UnityGI gi)
        {
            gi = UnityGlobalIllumination(data, 1.0, s.Normal);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
