Shader "Custom/Level04CinematicSkybox"
{
    Properties
    {
        _CloudTexA ("Cloud Shape", 2D) = "gray" {}
        _CloudTexB ("Cloud Detail", 2D) = "gray" {}
        _ZenithColor ("Zenith", Color) = (0.11, 0.15, 0.23, 1)
        _HorizonColor ("Horizon", Color) = (0.52, 0.47, 0.45, 1)
        _CloudShadow ("Cloud Shadow", Color) = (0.13, 0.16, 0.22, 1)
        _CloudLight ("Cloud Light", Color) = (0.64, 0.61, 0.59, 1)
        _SunColor ("Sun", Color) = (1, 0.5, 0.2, 1)
        _SunDirection ("Sun Direction", Vector) = (-0.64, 0.045, 0.77, 0)
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.5
        _CloudContrast ("Cloud Contrast", Range(0.02, 0.4)) = 0.14
        _CloudBrightness ("Cloud Brightness", Range(0, 2)) = 1
        _Exposure ("Exposure", Range(0, 2)) = 1
        _DriftA ("Drift A", Vector) = (0.0007, 0.00015, 0, 0)
        _DriftB ("Drift B", Vector) = (-0.0003, 0.00025, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _CloudTexA;
            sampler2D _CloudTexB;
            fixed4 _ZenithColor;
            fixed4 _HorizonColor;
            fixed4 _CloudShadow;
            fixed4 _CloudLight;
            fixed4 _SunColor;
            float4 _SunDirection;
            float _CloudCoverage;
            float _CloudContrast;
            float _CloudBrightness;
            float _Exposure;
            float4 _DriftA;
            float4 _DriftB;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 viewDirection : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.viewDirection = mul((float3x3)unity_ObjectToWorld, input.vertex.xyz);
                return output;
            }

            float2 MirrorRepeat(float2 uv)
            {
                float2 pingPong = frac(uv * 0.5) * 2.0;
                return 1.0 - abs(pingPong - 1.0);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 direction = normalize(input.viewDirection);
                float longitude = atan2(direction.x, direction.z) * 0.15915494 + 0.5;
                float latitude = asin(clamp(direction.y, -1.0, 1.0)) * 0.31830989 + 0.5;
                float2 sphericalUv = float2(longitude, latitude);

                float time = _Time.y;
                // Longitude wraps from 1 back to 0 at the rear of the sky dome.
                // Integer horizontal periods keep both sides of that boundary identical.
                float2 shapeUv = MirrorRepeat(
                    sphericalUv * float2(2.0, 2.65) + _DriftA.xy * time);
                float2 detailUv = MirrorRepeat(
                    sphericalUv * float2(4.0, 4.9)
                    + float2(0.37, 0.61)
                    + _DriftB.xy * time);
                float shape = tex2D(_CloudTexA, shapeUv).r;
                float detail = tex2D(_CloudTexB, detailUv).r;
                float noise = shape * 0.72 + detail * 0.28;
                float cloud = smoothstep(
                    _CloudCoverage - _CloudContrast,
                    _CloudCoverage + _CloudContrast,
                    noise);
                float skyMask = smoothstep(-0.12, 0.08, direction.y);
                cloud *= skyMask;

                float horizonBlend = saturate(direction.y * 1.6 + 0.18);
                fixed3 color = lerp(_HorizonColor.rgb, _ZenithColor.rgb, horizonBlend);

                float3 sunDirection = normalize(_SunDirection.xyz);
                float sunAlignment = saturate(dot(direction, sunDirection));
                float warmScatter = pow(sunAlignment, 8.0);
                float cloudLightness = saturate((noise - 0.22) * 1.65);
                fixed3 cloudColor = lerp(_CloudShadow.rgb, _CloudLight.rgb, cloudLightness);
                cloudColor = lerp(
                    cloudColor,
                    _SunColor.rgb * 0.92 + _CloudLight.rgb * 0.28,
                    warmScatter * 0.82);
                color = lerp(color, cloudColor * _CloudBrightness, cloud * 0.96);

                float sunDisk = pow(sunAlignment, 4200.0);
                float sunGlow = pow(sunAlignment, 58.0);
                color += _SunColor.rgb * (sunDisk * 2.8 + sunGlow * 0.48);
                color += _SunColor.rgb * warmScatter * 0.1 * (1.0 - cloud * 0.45);
                return fixed4(color * _Exposure, 1.0);
            }
            ENDCG
        }
    }
}
