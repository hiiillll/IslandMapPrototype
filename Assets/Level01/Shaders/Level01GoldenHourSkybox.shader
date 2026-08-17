Shader "Custom/Level01GoldenHourSkybox"
{
    Properties
    {
        _CloudTexA ("Cloud Shape", 2D) = "gray" {}
        _CloudTexB ("Cloud Detail", 2D) = "gray" {}
        _ZenithColor ("Zenith", Color) = (0.09, 0.18, 0.34, 1)
        _UpperSkyColor ("Upper Sky", Color) = (0.25, 0.40, 0.60, 1)
        _HorizonColor ("Horizon", Color) = (0.78, 0.52, 0.32, 1)
        _GroundColor ("Below Horizon", Color) = (0.34, 0.35, 0.38, 1)
        _CloudShadow ("Cloud Shadow", Color) = (0.34, 0.36, 0.41, 1)
        _CloudLight ("Cloud Light", Color) = (0.82, 0.76, 0.68, 1)
        _SunColor ("Sun", Color) = (1.0, 0.67, 0.34, 1)
        _SunDirection ("Sun Direction", Vector) = (-0.64, 0.045, 0.77, 0)
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.62
        _CloudSoftness ("Cloud Softness", Range(0.02, 0.3)) = 0.14
        _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.58
        _Exposure ("Exposure", Range(0, 2)) = 1.0
        _DriftA ("Drift A", Vector) = (0.00045, 0.00008, 0, 0)
        _DriftB ("Drift B", Vector) = (-0.00018, 0.00012, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _CloudTexA;
            sampler2D _CloudTexB;
            fixed4 _ZenithColor;
            fixed4 _UpperSkyColor;
            fixed4 _HorizonColor;
            fixed4 _GroundColor;
            fixed4 _CloudShadow;
            fixed4 _CloudLight;
            fixed4 _SunColor;
            float4 _SunDirection;
            float _CloudCoverage;
            float _CloudSoftness;
            float _CloudOpacity;
            float _Exposure;
            float4 _DriftA;
            float4 _DriftB;

            struct Attributes
            {
                float4 vertex : POSITION;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = mul((float3x3)unity_ObjectToWorld, input.vertex.xyz);
                return output;
            }

            float2 MirrorRepeat(float2 uv)
            {
                float2 pingPong = frac(uv * 0.5) * 2.0;
                return 1.0 - abs(pingPong - 1.0);
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float skyHeight = saturate(direction.y);
                float horizonMask = exp(-abs(direction.y) * 7.5);

                float lowerBlend = smoothstep(0.0, 0.32, skyHeight);
                float upperBlend = smoothstep(0.28, 0.92, skyHeight);
                float3 skyColor = lerp(_HorizonColor.rgb, _UpperSkyColor.rgb, lowerBlend);
                skyColor = lerp(skyColor, _ZenithColor.rgb, upperBlend);
                skyColor = lerp(_GroundColor.rgb, skyColor, smoothstep(-0.055, 0.025, direction.y));

                float3 sunDirection = normalize(_SunDirection.xyz);
                float sunAlignment = saturate(dot(direction, sunDirection));
                float sunsetScatter = pow(sunAlignment, 9.0) * (1.0 - skyHeight * 0.38);
                skyColor = lerp(skyColor, _SunColor.rgb, sunsetScatter * 0.22);
                skyColor += _SunColor.rgb * horizonMask * pow(sunAlignment, 22.0) * 0.12;

                float longitude = atan2(direction.x, direction.z) * 0.15915494 + 0.5;
                float latitude = asin(clamp(direction.y, -1.0, 1.0)) * 0.31830989 + 0.5;
                float2 sphericalUv = float2(longitude, latitude);
                float time = _Time.y;
                float2 shapeUv = MirrorRepeat(sphericalUv * float2(2.0, 2.4) + _DriftA.xy * time);
                float2 detailUv = MirrorRepeat(
                    sphericalUv * float2(4.0, 4.2)
                    + float2(0.31, 0.57)
                    + _DriftB.xy * time);
                float shape = tex2D(_CloudTexA, shapeUv).r;
                float detail = tex2D(_CloudTexB, detailUv).r;
                float noise = shape * 0.7 + detail * 0.3;
                float cloud = smoothstep(
                    _CloudCoverage - _CloudSoftness,
                    _CloudCoverage + _CloudSoftness,
                    noise);
                cloud *= smoothstep(0.015, 0.16, direction.y);

                float cloudLight = saturate(0.42 + direction.y * 0.48 + sunsetScatter * 0.68);
                float3 cloudColor = lerp(_CloudShadow.rgb, _CloudLight.rgb, cloudLight);
                cloudColor = lerp(cloudColor, _SunColor.rgb * 0.82 + _CloudLight.rgb * 0.28, sunsetScatter * 0.5);
                skyColor = lerp(skyColor, cloudColor, cloud * _CloudOpacity);

                float sunDisk = pow(sunAlignment, 3600.0);
                float sunGlow = pow(sunAlignment, 72.0);
                skyColor += _SunColor.rgb * (sunDisk * 1.75 + sunGlow * 0.18);

                return fixed4(skyColor * _Exposure, 1.0);
            }
            ENDCG
        }
    }
}
