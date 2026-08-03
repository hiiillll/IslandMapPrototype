Shader "Custom/Level04CloudSheet"
{
    Properties
    {
        _MainTex ("Cloud Mask", 2D) = "black" {}
        _Tint ("Tint", Color) = (0.72, 0.8, 0.88, 0.4)
        _Cutoff ("Density Cutoff", Range(0, 1)) = 0.24
        _Softness ("Edge Softness", Range(0.01, 0.5)) = 0.2
        _SecondaryScale ("Secondary Scale", Range(1, 4)) = 1.9
        _DetailBlend ("Detail Blend", Range(0, 1)) = 0.32
        _DriftA ("Primary Drift", Vector) = (0.0015, 0.0004, 0, 0)
        _DriftB ("Secondary Drift", Vector) = (-0.0007, 0.001, 0, 0)
        _GradientStrength ("Volume Lighting", Range(0, 2)) = 0.65
        _VerticalShade ("Vertical Shade", Range(0, 0.5)) = 0.12
        _EdgeFadeX ("Horizontal Edge Fade", Range(0, 0.5)) = 0.14
        _EdgeFadeY ("Vertical Edge Fade", Range(0, 0.5)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-10"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Tint;
            float _Cutoff;
            float _Softness;
            float _SecondaryScale;
            float _DetailBlend;
            float4 _DriftA;
            float4 _DriftB;
            float _GradientStrength;
            float _VerticalShade;
            float _EdgeFadeX;
            float _EdgeFadeY;
            float _Phase;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 rawUv : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.rawUv = input.uv;
                UNITY_TRANSFER_FOG(output, output.vertex);
                return output;
            }

            float SampleDensity(float2 uv)
            {
                fixed3 sampleColor = tex2D(_MainTex, uv).rgb;
                return dot(sampleColor, fixed3(0.299, 0.587, 0.114));
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float animatedTime = _Time.y + _Phase;
                float2 primaryUv = input.uv + _DriftA.xy * animatedTime;
                float2 secondaryUv = input.uv * _SecondaryScale
                    + float2(0.371, 0.619)
                    + _DriftB.xy * animatedTime;
                float primaryDensity = SampleDensity(primaryUv);
                float secondaryDensity = SampleDensity(secondaryUv);
                float detailModulation = lerp(0.74, 1.18, secondaryDensity);
                float density = saturate(lerp(
                    primaryDensity,
                    primaryDensity * detailModulation,
                    _DetailBlend));
                float alpha = smoothstep(
                    _Cutoff - _Softness,
                    _Cutoff + _Softness,
                    density);

                float edgeX = _EdgeFadeX > 0.0001
                    ? smoothstep(0.0, _EdgeFadeX, min(input.rawUv.x, 1.0 - input.rawUv.x))
                    : 1.0;
                float edgeY = _EdgeFadeY > 0.0001
                    ? smoothstep(0.0, _EdgeFadeY, min(input.rawUv.y, 1.0 - input.rawUv.y))
                    : 1.0;
                alpha *= edgeX * edgeY * _Tint.a;
                clip(alpha - 0.01);

                float2 lightStep = float2(-_MainTex_TexelSize.x * 8.0, _MainTex_TexelSize.y * 8.0);
                float lightFacingDensity = SampleDensity(primaryUv + lightStep);
                float shadowFacingDensity = SampleDensity(primaryUv - lightStep);
                float densityGradient = lightFacingDensity - shadowFacingDensity;
                float verticalModeling = (input.rawUv.y - 0.5) * _VerticalShade;
                fixed brightness = saturate(
                    lerp(0.62, 1.0, density)
                    + densityGradient * _GradientStrength
                    + verticalModeling);
                fixed4 color = fixed4(_Tint.rgb * brightness, alpha);
                UNITY_APPLY_FOG(input.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }
}
