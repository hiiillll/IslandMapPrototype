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
        _MacroMeters ("Macro Size (Meters)", Float) = 38
        _MacroStrength ("Macro Variation", Range(0, 0.25)) = 0.06
        _WearStrength ("Surface Wear", Range(0, 0.3)) = 0
        _Wetness ("Edge Wetness", Range(0, 1)) = 0
        _WetShoreLevel ("Wet Shore Level", Float) = 145
        _WetEdgeStart ("Wet Edge Start", Float) = 4
        _WetEdgeWidth ("Wet Edge Width", Float) = 22
        _WetSmoothness ("Wet Smoothness", Range(0, 1)) = 0.3
        _WetTint ("Wet Tint", Color) = (0.78, 0.8, 0.76, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
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
        float _MacroMeters;
        float _MacroStrength;
        float _WearStrength;
        float _Wetness;
        float _WetShoreLevel;
        float _WetEdgeStart;
        float _WetEdgeWidth;
        float _WetSmoothness;
        fixed4 _WetTint;
        half _Metallic;
        half _Smoothness;

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

            fixed4 albedo = tex2D(_MainTex, surfaceUv) * _Color;
            half macro = tex2D(_MacroTex, macroUv).r;
            half macroFactor = lerp(1.0h - _MacroStrength, 1.0h + _MacroStrength, macro);
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

            output.Albedo = albedo.rgb * macroFactor;
            output.Albedo *= lerp(1.0h, 0.86h, wearMask);
            output.Albedo = lerp(output.Albedo, output.Albedo * _WetTint.rgb, wetMask * 0.62h);
            output.Normal = UnpackScaleNormal(tex2D(_NormalMap, surfaceUv), _NormalStrength);
            output.Metallic = _Metallic;
            output.Smoothness = lerp(_Smoothness, max(_Smoothness, _WetSmoothness), wetMask);
            output.Alpha = albedo.a;
        }
        ENDCG
    }

    FallBack "Standard"
}
