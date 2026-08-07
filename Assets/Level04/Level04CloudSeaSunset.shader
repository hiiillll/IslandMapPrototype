Shader "Custom/Level04CloudSeaSunset"
{
    Properties
    {
        _MainTex ("Cloud Sea", 2D) = "white" {}
        _DetailTexA ("Fast Sky Detail A", 2D) = "gray" {}
        _DetailTexB ("Fast Sky Detail B", 2D) = "gray" {}
        _Tint ("Cloud Tint", Color) = (0.82, 0.86, 0.94, 1)
        _ShadowTint ("Shadow Tint", Color) = (0.2, 0.25, 0.34, 1)
        _WarmTint ("Sunset Tint", Color) = (1, 0.56, 0.3, 1)
        _DetailStrength ("Detail Strength", Range(0, 0.5)) = 0.12
        _WarmStrength ("Sunset Strength", Range(0, 0.5)) = 0.12
        _SunDirection ("Sun Direction", Vector) = (-0.64, 0.045, 0.77, 0)
        _DriftA ("Detail Drift A", Vector) = (0.0005, 0.00015, 0, 0)
        _DriftB ("Detail Drift B", Vector) = (-0.0002, 0.00035, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        Cull Back
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _DetailTexA;
            sampler2D _DetailTexB;
            float4 _MainTex_ST;
            fixed4 _Tint;
            fixed4 _ShadowTint;
            fixed4 _WarmTint;
            float _DetailStrength;
            float _WarmStrength;
            float4 _SunDirection;
            float4 _DriftA;
            float4 _DriftB;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                UNITY_TRANSFER_FOG(output, output.vertex);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float time = _Time.y;
                fixed3 source = tex2D(_MainTex, input.uv).rgb;
                float detailA = tex2D(
                    _DetailTexA,
                    input.uv * 1.7 + _DriftA.xy * time).r;
                float detailB = tex2D(
                    _DetailTexB,
                    input.uv * 3.1 + float2(0.37, 0.61) + _DriftB.xy * time).r;
                float detail = (detailA * 0.62 + detailB * 0.38 - 0.5) * _DetailStrength;
                float luminance = dot(source, fixed3(0.299, 0.587, 0.114));
                float cloudHeight = smoothstep(0.18, 0.82, luminance + detail);
                fixed3 modeled = lerp(_ShadowTint.rgb, _Tint.rgb, cloudHeight);
                modeled *= lerp(0.82, 1.12, saturate(luminance + detail * 2.0));

                // A broad world-space sunset wash keeps the low-angle cloud deck
                // from reading as glossy water while preserving the top-down detail.
                float2 horizontalSun = normalize(_SunDirection.xz);
                float broadWarmth = saturate(
                    0.34
                    + dot(input.worldPosition.xz, horizontalSun) / 3600.0
                    + detailA * 0.22);
                modeled = lerp(
                    modeled,
                    modeled * _WarmTint.rgb * 1.08,
                    broadWarmth * _WarmStrength);
                float modeledLight = saturate(
                    dot(normalize(input.worldNormal), normalize(_SunDirection.xyz)) * 0.7 + 0.46);
                modeled *= lerp(0.86, 1.12, modeledLight);
                fixed4 color = fixed4(modeled, 1.0);
                UNITY_APPLY_FOG(input.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }
}
