// Procedural skybox: three-color gradient, sun disc + halo, drifting clouds.
Shader "KartGame/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.25,0.5,0.95,1)
        _Horizon ("Horizon", Color) = (0.75,0.88,1,1)
        _Bottom ("Bottom", Color) = (0.55,0.65,0.7,1)
        _SunDir ("Sun Direction", Vector) = (0.4,0.6,0.3,0)
        _SunColor ("Sun Color", Color) = (1,0.95,0.8,1)
        _CloudColor ("Cloud Color", Color) = (1,1,1,1)
        _CloudShade ("Cloud Shade", Color) = (0.72,0.78,0.9,1)
        _CloudAmount ("Cloud Amount", Range(0,1)) = 0.55
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Top, _Horizon, _Bottom, _SunColor, _CloudColor, _CloudShade;
            float4 _SunDir;
            float _CloudAmount;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(233.34, 851.73));
                p += dot(p, p + 23.45);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1,0)), u.x),
                            lerp(hash21(i + float2(0,1)), hash21(i + float2(1,1)), u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { v += vnoise(p) * a; p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float3 col = h > 0 ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(h), 0.55))
                                   : lerp(_Horizon.rgb, _Bottom.rgb, pow(saturate(-h * 3), 0.6));

                float3 sd = normalize(_SunDir.xyz);
                float sdot = saturate(dot(d, sd));
                col += _SunColor.rgb * (pow(sdot, 900) * 6 + pow(sdot, 12) * 0.35 + pow(sdot, 3) * 0.08);

                if (h > 0)
                {
                    float2 uv = d.xz / (h + 0.12) * 0.9 + _Time.y * float2(0.004, 0.002);
                    float n = fbm(uv * 1.3);
                    float mask = smoothstep(1 - _CloudAmount * 0.85, 1.05 - _CloudAmount * 0.55, n);
                    float shade = saturate((fbm(uv * 1.3 + sd.xz * 0.08) - n) * 6 + 0.6);
                    float3 cloud = lerp(_CloudShade.rgb, _CloudColor.rgb, shade);
                    mask *= saturate(h * 5);
                    col = lerp(col, cloud, mask * 0.92);
                }
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
