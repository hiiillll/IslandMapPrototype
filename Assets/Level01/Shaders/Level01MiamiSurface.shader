Shader "Custom/Level01MiamiSurface"
{
    Properties
    {
        _MainTex ("Miami Diffuse", 2D) = "white" {}
        _SecondaryTex ("Secondary Diffuse", 2D) = "gray" {}
        _SecondaryScale ("Secondary Scale", Range(0.5, 4)) = 1.7
        _SecondaryStrength ("Secondary Strength", Range(0, 0.7)) = 0
        [Normal] _NormalMap ("Miami Normal", 2D) = "bump" {}
        _MacroTex ("Macro Variation", 2D) = "gray" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _TileMeters ("Texture Size (Meters)", Float) = 5
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1
        _DetailScale ("Detail Scale", Range(1, 6)) = 2.8
        _DetailStrength ("Detail Strength", Range(0, 0.4)) = 0.12
        _DetailFadeStart ("Detail Fade Start (Meters)", Float) = 80
        _DetailFadeEnd ("Detail Fade End (Meters)", Float) = 220
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
        _TideSpeed ("Shore Break Speed", Float) = 1.8
        _TideReach ("Shore Break Reach", Float) = 8
        _ShoreFoamWidth ("Shore Foam Width", Float) = 2.2
        _ShoreFoamStrength ("Shore Foam Strength", Range(0, 1)) = 0.32
        _ShoreFoamColor ("Shore Foam Color", Color) = (0.68, 0.77, 0.78, 1)
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
        sampler2D _SecondaryTex;
        sampler2D _NormalMap;
        sampler2D _MacroTex;
        fixed4 _Color;
        float _SecondaryScale;
        float _SecondaryStrength;
        float _TileMeters;
        float _NormalStrength;
        float _DetailScale;
        float _DetailStrength;
        float _DetailFadeStart;
        float _DetailFadeEnd;
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
        float _TideSpeed;
        float _TideReach;
        float _ShoreFoamWidth;
        float _ShoreFoamStrength;
        fixed4 _ShoreFoamColor;
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
            float cameraDistance = distance(_WorldSpaceCameraPos.xyz, input.worldPos);
            float detailFade = 1.0 - smoothstep(
                min(_DetailFadeStart, _DetailFadeEnd),
                max(_DetailFadeStart, _DetailFadeEnd),
                cameraDistance);
            float detailStrength = _DetailStrength * detailFade;
            UNITY_BRANCH
            if (detailStrength > 0.001)
            {
                fixed3 detailAlbedo = tex2D(_MainTex, detailUv).rgb;
                albedo.rgb *= lerp(1.0, detailAlbedo * 2.0, detailStrength);
            }

            // A differently rotated second source breaks the broad horizontal
            // repetition in the beach texture without shifting its authored
            // colour.  We only transfer fine luminance variation so sand stays
            // neutral instead of becoming mottled orange or grey at distance.
            UNITY_BRANCH
            if (_SecondaryStrength > 0.001)
            {
                float2 secondaryUv = float2(
                    surfaceUv.x * 0.43 + surfaceUv.y * 0.90,
                    surfaceUv.x * -0.90 + surfaceUv.y * 0.43);
                secondaryUv = secondaryUv * max(_SecondaryScale, 0.5)
                    + float2(0.19, -0.37);
                fixed3 secondaryAlbedo = tex2D(_SecondaryTex, secondaryUv).rgb;
                fixed3 secondarySurface = secondaryAlbedo
                    * _Color.rgb
                    * 1.08h;
                albedo.rgb = lerp(
                    albedo.rgb,
                    secondarySurface,
                    _SecondaryStrength * detailFade);
            }

            half macroBlend = 0.5h;
            half macroSecondary = 0.5h;
            UNITY_BRANCH
            if (_MacroStrength > 0.001 || _PatchStrength > 0.001 || _WearStrength > 0.001)
            {
                half macro = tex2D(_MacroTex, macroUv).r;
                macroSecondary = tex2D(
                    _MacroTex,
                    macroUv * 0.57 + float2(0.41, -0.23)).r;
                macroBlend = saturate(macro * 0.68h + macroSecondary * 0.32h);
            }
            half macroFactor = lerp(
                1.0h - _MacroStrength,
                1.0h + _MacroStrength,
                macroBlend);
            fixed3 macroTint = lerp(
                _MacroTintA.rgb,
                _MacroTintB.rgb,
                smoothstep(0.24h, 0.76h, macroBlend));
            half patchMask = smoothstep(0.61h, 0.82h, macroSecondary) * _PatchStrength;
            half wearMask = 0.0h;
            UNITY_BRANCH
            if (_WearStrength > 0.001)
            {
                half wearNoise = tex2D(
                    _MacroTex,
                    macroUv * 1.85 + float2(0.31, -0.17)).r;
                wearMask = smoothstep(0.34h, 0.72h, wearNoise) * _WearStrength;
            }
            half alongEdge = abs(input.worldPos.x) > abs(input.worldPos.z)
                ? input.worldPos.z
                : input.worldPos.x;
            half shoreVariation = sin(alongEdge * 0.075h) * 2.6h
                + sin(alongEdge * 0.031h + 1.7h) * 1.8h
                + sin(alongEdge * 0.17h + 0.4h) * 0.65h;
            // Positive values are seaward and negative values are inland. This
            // signed distance exactly matches the generated beach mesh and the
            // water shader, so foam and wet sand share one physical shoreline.
            half shoreDistance = max(abs(input.worldPos.x), abs(input.worldPos.z))
                - (_WetShoreLevel + shoreVariation);
            half tidePhase = _Time.y * max(_TideSpeed, 0.1)
                + alongEdge * 0.018h
                + sin(alongEdge * 0.011h) * 0.45h;
            half tideCycle = 0.5h + 0.5h * sin(tidePhase);
            half tideSurge = smoothstep(0.08h, 0.88h, tideCycle);
            half tideFront = lerp(
                max(_WetEdgeStart, 0.2h),
                -max(_TideReach, 0.2h),
                tideSurge);
            tideFront += sin(
                _Time.y * max(_TideSpeed, 0.1h) * 1.46h
                - alongEdge * 0.036h) * 0.28h;

            // The broad band is always dark wet sand. Only the narrow water-film
            // band follows the active breaker, preventing moving white blobs.
            half wetLimit = -max(_WetEdgeWidth, 0.5h)
                - tideSurge * max(_TideReach * 0.35h, 0.2h);
            half wetMask = smoothstep(
                wetLimit - 2.0h,
                wetLimit + 2.0h,
                shoreDistance);
            wetMask *= 1.0h - smoothstep(7.0h, 13.0h, shoreDistance);
            wetMask = saturate(wetMask) * _Wetness;
            half foamNoise = 0.5h
                + 0.28h * sin(alongEdge * 0.31h + _Time.y * 0.94h)
                + 0.22h * sin(alongEdge * 0.73h - _Time.y * 0.53h);
            // A second world-space noise breaks the former continuous white
            // ribbon into clusters. The slow offset makes each breaker form,
            // fragment and dissolve instead of sliding as one painted stripe.
            half foamGrain = tex2D(
                _MacroTex,
                input.worldPos.xz * 0.115h
                    + float2(_Time.y * 0.018h, -_Time.y * 0.011h)).r;
            half foamBreakup = smoothstep(
                0.39h,
                0.7h,
                foamNoise * 0.42h + foamGrain * 0.58h);
            half brokenFront = tideFront
                + (foamGrain - 0.5h) * 1.15h
                + sin(alongEdge * 0.21h + _Time.y * 0.37h) * 0.32h;

            // A connected sheet of shallow water travels behind the breaker.
            // This creates the visible advance/retreat missing from the old
            // effect, while the lingering broad wet mask fades much more slowly.
            half behindBreaker = shoreDistance - brokenFront;
            half waterFilm = smoothstep(-0.45h, 0.25h, behindBreaker)
                * (1.0h - smoothstep(
                    max(_ShoreFoamWidth * 2.6h, 2.5h),
                    max(_ShoreFoamWidth * 5.2h, 5.6h),
                    behindBreaker));
            half leadingSheen = 1.0h - smoothstep(
                0.12h,
                max(_ShoreFoamWidth * 1.25h, 0.8h),
                abs(behindBreaker));
            waterFilm = saturate(waterFilm + leadingSheen * 0.35h) * _Wetness;

            half shoreFoam = 1.0h - smoothstep(
                max(_ShoreFoamWidth, 0.08h) * 0.16h,
                max(_ShoreFoamWidth, 0.08h),
                abs(shoreDistance - brokenFront));
            shoreFoam *= lerp(0.24h, 1.0h, foamBreakup)
                * _ShoreFoamStrength
                * _Wetness;
            half trailingFoam = 1.0h - smoothstep(
                max(_ShoreFoamWidth, 0.08h) * 0.22h,
                max(_ShoreFoamWidth, 0.08h) * 1.18h,
                abs(behindBreaker - 1.45h));
            trailingFoam *= smoothstep(0.52h, 0.82h, foamNoise)
                * _ShoreFoamStrength
                * _Wetness
                * 0.28h;
            shoreFoam = saturate(shoreFoam + trailingFoam);

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
            // The advancing sheet first darkens and smooths the sand. Foam is
            // deliberately a low-energy surface tint so it reads as wet bubbles,
            // not an emissive white decal disconnected from the sea.
            output.Albedo = lerp(
                output.Albedo,
                output.Albedo * lerp(_WetTint.rgb, half3(0.86h, 0.9h, 0.88h), 0.18h),
                saturate(waterFilm * 0.58h));
            output.Albedo = lerp(
                output.Albedo,
                half3(0.24h, 0.36h, 0.35h),
                saturate(waterFilm * 0.18h));
            output.Albedo = lerp(
                output.Albedo,
                _ShoreFoamColor.rgb,
                shoreFoam * 0.66h);
            half3 baseNormal = UnpackScaleNormal(
                tex2D(_NormalMap, surfaceUv),
                _NormalStrength);
            half3 detailNormal = half3(0.0h, 0.0h, 1.0h);
            UNITY_BRANCH
            if (detailStrength > 0.001)
            {
                detailNormal = UnpackScaleNormal(
                    tex2D(_NormalMap, detailUv),
                    _NormalStrength * detailStrength * 0.75h);
            }
            output.Normal = normalize(half3(
                baseNormal.xy + detailNormal.xy,
                baseNormal.z * detailNormal.z));
            output.Metallic = _Metallic;
            half drySmoothness = saturate(
                _Smoothness + (macroBlend - 0.5h) * _SmoothnessVariation);
            output.Smoothness = lerp(
                drySmoothness,
                max(drySmoothness, _WetSmoothness),
                saturate(wetMask * 0.56h + waterFilm * 0.44h));
            output.Alpha = albedo.a;
        }
        ENDCG
    }

    FallBack "Standard"
}
