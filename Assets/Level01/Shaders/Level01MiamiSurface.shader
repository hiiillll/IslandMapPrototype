Shader "Custom/Level01MiamiSurface"
{
    Properties
    {
        _MainTex ("Miami Diffuse", 2D) = "white" {}
        [Normal] _NormalMap ("Miami Normal", 2D) = "bump" {}
        _MacroTex ("Macro Variation", 2D) = "gray" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _TileMeters ("Texture Size (Meters)", Float) = 5
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1
        _DetailScale ("Detail Scale", Range(1, 6)) = 2.8
        _DetailStrength ("Detail Strength", Range(0, 0.4)) = 0.12
        _MacroMeters ("Macro Size (Meters)", Float) = 38
        _MacroStrength ("Macro Variation", Range(0, 0.25)) = 0.06
        _MacroTintA ("Macro Tint A", Color) = (0.9, 0.9, 0.9, 1)
        _MacroTintB ("Macro Tint B", Color) = (1.05, 1.05, 1.05, 1)
        _PatchStrength ("Patch Strength", Range(0, 0.3)) = 0.08
        _WearStrength ("Surface Wear", Range(0, 0.3)) = 0
        _EdgeTint ("Edge Tint", Color) = (0.55, 0.52, 0.46, 1)
        _EdgeWidth ("Edge Width", Range(0, 0.2)) = 0
        _EdgeStrength ("Edge Strength", Range(0, 1)) = 0
        _Wetness ("Edge Wetness", Range(0, 1)) = 0
        _WetShoreLevel ("Wet Shore Level", Float) = 145
        _WetEdgeStart ("Wet Edge Start", Float) = 4
        _WetEdgeWidth ("Wet Edge Width", Float) = 22
        _WetSmoothness ("Wet Smoothness", Range(0, 1)) = 0.3
        _WetTint ("Wet Tint", Color) = (0.78, 0.8, 0.76, 1)
        _WetColorStrength ("Wet Color Strength", Range(0, 1)) = 0.62
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
        _SmoothnessVariation ("Smoothness Variation", Range(0, 0.3)) = 0.04
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _NormalMap;
        sampler2D _MacroTex;
        fixed4 _Color;
        float _TileMeters;
        float _NormalStrength;
        float _DetailScale;
        float _DetailStrength;
        float _MacroMeters;
        float _MacroStrength;
        fixed4 _MacroTintA;
        fixed4 _MacroTintB;
        float _PatchStrength;
        float _WearStrength;
        fixed4 _EdgeTint;
        float _EdgeWidth;
        float _EdgeStrength;
        float _Wetness;
        float _WetShoreLevel;
        float _WetEdgeStart;
        float _WetEdgeWidth;
        float _WetSmoothness;
        fixed4 _WetTint;
        float _WetColorStrength;
        half _Metallic;
        half _Smoothness;
        half _SmoothnessVariation;

        struct Input
        {
            float3 worldPos;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            float tileMeters = max(_TileMeters, 0.1);
            float macroMeters = max(_MacroMeters, 1.0);
            float2 surfaceUv = input.worldPos.xz / tileMeters;
            float2 macroUv = input.worldPos.xz / macroMeters;
            float2 detailUv = float2(
                surfaceUv.x * 0.78 - surfaceUv.y * 0.63,
                surfaceUv.x * 0.63 + surfaceUv.y * 0.78);
            detailUv = detailUv * _DetailScale + float2(0.37, 0.61);

            fixed4 albedo = tex2D(_MainTex, surfaceUv) * _Color;
            fixed3 detailAlbedo = tex2D(_MainTex, detailUv).rgb;
            albedo.rgb *= lerp(1.0, detailAlbedo * 2.0, _DetailStrength);
            half macro = tex2D(_MacroTex, macroUv).r;
            half macroSecondary = tex2D(
                _MacroTex,
                macroUv * 0.57 + float2(0.41, -0.23)).r;
            half macroBlend = saturate(macro * 0.68h + macroSecondary * 0.32h);
            half macroFactor = lerp(
                1.0h - _MacroStrength,
                1.0h + _MacroStrength,
                macroBlend);
            fixed3 macroTint = lerp(
                _MacroTintA.rgb,
                _MacroTintB.rgb,
                smoothstep(0.24h, 0.76h, macroBlend));
            half patchMask = smoothstep(0.61h, 0.82h, macroSecondary) * _PatchStrength;
            half wearNoise = tex2D(
                _MacroTex,
                macroUv * 1.85 + float2(0.31, -0.17)).r;
            half wearMask = smoothstep(0.34h, 0.72h, wearNoise) * _WearStrength;
            half alongEdge = abs(input.worldPos.x) > abs(input.worldPos.z)
                ? input.worldPos.z
                : input.worldPos.x;
            half shoreVariation = sin(alongEdge * 0.075h) * 2.6h
                + sin(alongEdge * 0.031h + 1.7h) * 1.8h;
            half edgeDistance = (_WetShoreLevel + shoreVariation)
                - max(abs(input.worldPos.x), abs(input.worldPos.z));
            half wetMask = 1.0h - smoothstep(
                _WetEdgeStart,
                _WetEdgeStart + max(_WetEdgeWidth, 0.1),
                edgeDistance);
            wetMask = saturate(wetMask) * _Wetness;

            float3 localPosition = mul(
                unity_WorldToObject,
                float4(input.worldPos, 1.0)).xyz;
            float scaleX = length(float3(
                unity_ObjectToWorld._m00,
                unity_ObjectToWorld._m10,
                unity_ObjectToWorld._m20));
            float scaleZ = length(float3(
                unity_ObjectToWorld._m02,
                unity_ObjectToWorld._m12,
                unity_ObjectToWorld._m22));
            half elongated = smoothstep(
                0.08h,
                0.24h,
                abs(scaleX - scaleZ) / max(max(scaleX, scaleZ), 0.001));
            half xIsLonger = step(scaleZ, scaleX);
            half lateralPosition = lerp(
                abs(localPosition.x),
                abs(localPosition.z),
                xIsLonger);
            half edgeMask = smoothstep(
                0.5h - max(_EdgeWidth, 0.001h),
                0.5h,
                lateralPosition);
            edgeMask *= elongated * _EdgeStrength;

            output.Albedo = albedo.rgb * macroFactor * macroTint;
            output.Albedo *= lerp(1.0h, 0.82h, patchMask);
            output.Albedo *= lerp(1.0h, 0.86h, wearMask);
            output.Albedo = lerp(
                output.Albedo,
                output.Albedo * _EdgeTint.rgb,
                edgeMask);
            output.Albedo = lerp(
                output.Albedo,
                output.Albedo * _WetTint.rgb,
                wetMask * _WetColorStrength);
            half3 baseNormal = UnpackScaleNormal(
                tex2D(_NormalMap, surfaceUv),
                _NormalStrength);
            half3 detailNormal = UnpackScaleNormal(
                tex2D(_NormalMap, detailUv),
                _NormalStrength * _DetailStrength * 0.75h);
            output.Normal = normalize(half3(
                baseNormal.xy + detailNormal.xy,
                baseNormal.z * detailNormal.z));
            output.Metallic = _Metallic;
            half drySmoothness = saturate(
                _Smoothness + (macroBlend - 0.5h) * _SmoothnessVariation);
            output.Smoothness = lerp(
                drySmoothness,
                max(drySmoothness, _WetSmoothness),
                wetMask);
            output.Alpha = albedo.a;
        }
        ENDCG
    }

    FallBack "Standard"
}
