#include "UnityCG.cginc"
#include "Lighting.cginc"

sampler2D _NormalTexS;
sampler2D _NormalTexD;
sampler2D _NormalTexR;
sampler2D _FoamTex;
float4 _NormalTexS_ST;
float4 _suimono_Dir;
float _NormalStrength;
float _heightScale;
float _lgWaveHeight;
float _CompatWaveAmplitude;
float _AnimSpeed;
float _turbulenceFactor;
float _specularPower;
float _roughness;
float _overallBrightness;
float _Level01ColorBlend;
fixed4 _Level01ReflectionTint;
float _CinematicOcean;
float _CinematicReflection;
float _CinematicSunGlint;
float _CinematicHorizonBlend;
fixed4 _CinematicHorizonColor;
float _CinematicMicroRipple;
float _OpenOceanBlend;
float _OpenOceanChop;
float _OpenOceanFoam;
float _ShorelineLevel;
float _ShorelineWidth;
float _ShorelineFoam;
float _shorelineFrequency;
float _shorelineSpeed;
float _shorelineHeight;
float _TideAmount;
float _TideSpread;
fixed4 _depthColor;
fixed4 _shallowColor;
fixed4 _SpecularColor;
fixed4 _FoamColor;
float _Level02WhirlpoolCount;
float _Level02WhirlpoolTime;
float4 _Level02WhirlpoolPositions[4];
float4 _Level02WhirlpoolParameters[4];

struct SuimonoCompatVertexInput
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 texcoord : TEXCOORD0;
};

struct SuimonoCompatVertexOutput
{
    float4 position : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 worldPosition : TEXCOORD1;
    float3 geometricNormal : TEXCOORD2;
    float waveHeight : TEXCOORD3;
    UNITY_FOG_COORDS(4)
};

SuimonoCompatVertexOutput SuimonoCompatVert(SuimonoCompatVertexInput input)
{
    SuimonoCompatVertexOutput output;
    float3 worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
    float time = _Time.y * max(0.1, _AnimSpeed);
    float legacySwell = sin(worldPosition.x * 0.055 + time * 0.52)
        + sin(worldPosition.z * 0.071 - time * 0.43) * 0.65
        + sin((worldPosition.x + worldPosition.z) * 0.035 + time * 0.28) * 0.35;

    float phaseA = worldPosition.x * 0.052 + worldPosition.z * 0.017 + time * 0.44;
    float phaseB = worldPosition.x * -0.021 + worldPosition.z * 0.068 - time * 0.36;
    float phaseC = (worldPosition.x + worldPosition.z) * 0.031 + time * 0.23;
    float phaseD = worldPosition.x * 0.093 - worldPosition.z * 0.047 + time * 0.57;
    float cinematicSwell = sin(phaseA) * 0.42
        + sin(phaseB) * 0.28
        + sin(phaseC) * 0.19
        + sin(phaseD) * 0.11;
    float displacement = lerp(
        legacySwell,
        cinematicSwell,
        saturate(_CinematicOcean)) * max(0.0, _CompatWaveAmplitude);

    // Open water needs a smaller cross-swell riding on top of the broad waves.
    // Level 01 leaves this disabled so its calmer coastline remains unchanged.
    float shortCrossSwell = sin(
        worldPosition.x * 0.118
        - worldPosition.z * 0.073
        + time * 0.91) * 0.58
        + sin(
            worldPosition.x * -0.086
            + worldPosition.z * 0.142
            - time * 0.74) * 0.42;
    displacement += shortCrossSwell
        * max(0.0, _CompatWaveAmplitude)
        * 0.24
        * saturate(_OpenOceanBlend)
        * saturate(_OpenOceanChop);

    // Add a low-cost geometric breaker that uses the same coastline and tide
    // phase as the beach surface shader. The denser shallow-water mesh carries
    // this deformation close to shore while the ocean mesh handles the horizon.
    float maxCoordinate = max(abs(worldPosition.x), abs(worldPosition.z));
    float alongEdge = abs(worldPosition.x) > abs(worldPosition.z)
        ? worldPosition.z
        : worldPosition.x;
    float shoreVariation = sin(alongEdge * 0.075) * 2.6
        + sin(alongEdge * 0.031 + 1.7) * 1.8
        + sin(alongEdge * 0.17 + 0.4) * 0.65;
    float shoreDistance = maxCoordinate - (_ShorelineLevel + shoreVariation);
    float tidePhase = _Time.y * max(_shorelineSpeed, 0.1)
        + alongEdge * 0.018
        + sin(alongEdge * 0.011) * 0.45;
    float tideCycle = 0.5 + 0.5 * sin(tidePhase);
    float tideSurge = smoothstep(0.08, 0.88, tideCycle);
    float shoreReach = max(1.8, _TideAmount * 2.2 + _TideSpread * 1.4);
    float waveAdvance = lerp(1.8, -shoreReach, tideSurge)
        + sin(_Time.y * max(_shorelineSpeed, 0.1) * 1.46
            - alongEdge * 0.036) * 0.28;
    float breakerBand = 1.0 - smoothstep(
        0.35,
        2.6,
        abs(shoreDistance - waveAdvance));
    displacement += breakerBand
        * max(0.025, _shorelineHeight * 0.1)
        * saturate(_ShorelineFoam);

    float whirlpoolDepression = 0.0;
    [unroll]
    for (int whirlpoolIndex = 0; whirlpoolIndex < 4; whirlpoolIndex++)
    {
        float activeWhirlpool = step(whirlpoolIndex + 0.5, _Level02WhirlpoolCount);
        float4 whirlpoolPosition = _Level02WhirlpoolPositions[whirlpoolIndex];
        float whirlpoolRadius = max(0.01, whirlpoolPosition.z);
        float whirlpoolDistance = distance(worldPosition.xz, whirlpoolPosition.xy);
        float normalizedWhirlpoolDistance = whirlpoolDistance / whirlpoolRadius;
        float funnel = 1.0 - smoothstep(0.08, 0.78, normalizedWhirlpoolDistance);
        whirlpoolDepression = max(
            whirlpoolDepression,
            funnel * funnel * whirlpoolPosition.w * activeWhirlpool);
    }
    displacement -= whirlpoolDepression * 0.34 * saturate(_CinematicOcean);
    input.vertex.y += displacement;

    float slopeX = cos(phaseA) * 0.42 * 0.052
        + cos(phaseB) * 0.28 * -0.021
        + cos(phaseC) * 0.19 * 0.031
        + cos(phaseD) * 0.11 * 0.093;
    float slopeZ = cos(phaseA) * 0.42 * 0.017
        + cos(phaseB) * 0.28 * 0.068
        + cos(phaseC) * 0.19 * 0.031
        + cos(phaseD) * 0.11 * -0.047;
    float3 cinematicNormal = normalize(float3(
        -slopeX * _CompatWaveAmplitude,
        1.0,
        -slopeZ * _CompatWaveAmplitude));

    output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
    output.position = UnityObjectToClipPos(input.vertex);
    output.uv = output.worldPosition.xz * 0.045;
    output.geometricNormal = lerp(float3(0.0, 1.0, 0.0), cinematicNormal, saturate(_CinematicOcean));
    output.waveHeight = displacement;
    UNITY_TRANSFER_FOG(output, output.position);
    return output;
}

fixed4 SuimonoCompatFrag(SuimonoCompatVertexOutput input) : SV_Target
{
    float time = _Time.y * max(0.1, _AnimSpeed);
    float2 direction = normalize(_suimono_Dir.xz + float2(0.001, 0.001));
    float2 perpendicular = float2(-direction.y, direction.x);
    float2 uvA = input.uv + direction * time * 0.018;
    float2 uvB = input.uv * 0.53 - perpendicular * time * 0.012;
    float2 uvC = input.uv * 0.19 + (direction + perpendicular) * time * 0.004;

    float3 normalA = UnpackNormal(tex2D(_NormalTexS, uvA));
    float3 normalB = UnpackNormal(tex2D(_NormalTexD, uvB));
    float3 normalC = UnpackNormal(tex2D(_NormalTexR, uvC));
    float2 uvD = input.worldPosition.xz * 0.16
        + direction * time * 0.041;
    float2 uvE = input.worldPosition.xz * 0.29
        - perpendicular * time * 0.054
        + float2(0.31, 0.67);
    float3 normalD = UnpackNormal(tex2D(_NormalTexS, uvD));
    float3 normalE = UnpackNormal(tex2D(_NormalTexD, uvE));
    float2 slope = normalA.xy
        + normalB.xy * (0.52 + _turbulenceFactor)
        + normalC.xy * 0.24;
    slope += (normalD.xy * 0.3 + normalE.xy * 0.16)
        * saturate(_OpenOceanBlend)
        * saturate(_OpenOceanChop);

    float vortexDepth = 0.0;
    float vortexFoam = 0.0;
    float vortexFlowBands = 0.0;
    float2 vortexNormalOffset = float2(0.0, 0.0);
    [unroll]
    for (int whirlpoolIndex = 0; whirlpoolIndex < 4; whirlpoolIndex++)
    {
        float activeWhirlpool = step(whirlpoolIndex + 0.5, _Level02WhirlpoolCount);
        float4 whirlpoolPosition = _Level02WhirlpoolPositions[whirlpoolIndex];
        float4 whirlpoolParameters = _Level02WhirlpoolParameters[whirlpoolIndex];
        float whirlpoolRadius = max(0.01, whirlpoolPosition.z);
        float2 vortexDelta = input.worldPosition.xz - whirlpoolPosition.xy;
        float vortexDistance = max(0.001, length(vortexDelta));
        float normalizedVortexDistance = vortexDistance / whirlpoolRadius;
        float vortexIntensity = whirlpoolPosition.w * activeWhirlpool;
        float vortexMask = (1.0 - smoothstep(0.72, 1.0, normalizedVortexDistance))
            * vortexIntensity;
        float vortexAngle = atan2(vortexDelta.y, vortexDelta.x);
        float rotationTime = _Level02WhirlpoolTime * whirlpoolParameters.y;

        float mainSpiral = cos(
            vortexAngle * 2.0
            - normalizedVortexDistance * 10.5
            - rotationTime * 3.1
            + whirlpoolParameters.x) * 0.5 + 0.5;
        float secondarySpiral = cos(
            vortexAngle * 3.0
            - normalizedVortexDistance * 15.5
            - rotationTime * 4.0
            + whirlpoolParameters.x * 1.7) * 0.5 + 0.5;
        float brokenArc = smoothstep(
            0.28,
            0.68,
            sin(vortexAngle * 5.0 + normalizedVortexDistance * 11.0 - rotationTime * 5.0) * 0.5 + 0.5);
        float vortexFoamTexture = tex2D(
            _FoamTex,
            vortexDelta * 0.075 + float2(rotationTime * 0.018, -rotationTime * 0.014)).r;
        float textureBreakup = smoothstep(0.26, 0.74, vortexFoamTexture);
        float armRegion = smoothstep(0.15, 0.3, normalizedVortexDistance)
            * (1.0 - smoothstep(0.56, 0.88, normalizedVortexDistance));
        float spiralFoam = smoothstep(0.82, 0.975, mainSpiral)
            + smoothstep(0.88, 0.985, secondarySpiral) * 0.24;
        spiralFoam *= armRegion
            * lerp(0.22, 1.0, brokenArc)
            * lerp(0.38, 1.0, textureBreakup)
            * vortexIntensity;

        float coreRingRadius = 0.27
            + sin(vortexAngle * 3.0 - rotationTime * 4.0) * 0.028
            + (vortexFoamTexture - 0.5) * 0.025;
        float coreRing = 1.0 - smoothstep(
            0.02,
            0.065,
            abs(normalizedVortexDistance - coreRingRadius));
        coreRing *= lerp(0.08, 0.62, brokenArc * textureBreakup) * vortexIntensity;

        float2 radialDirection = vortexDelta / vortexDistance;
        float2 tangentDirection = float2(-radialDirection.y, radialDirection.x);
        float rotatingFlow = sin(
            vortexAngle * 6.0
            - normalizedVortexDistance * 21.0
            - rotationTime * 8.2
            + whirlpoolParameters.x) * 0.5 + 0.5;
        float flowDerivative = cos(
            vortexAngle * 6.0
            - normalizedVortexDistance * 21.0
            - rotationTime * 8.2
            + whirlpoolParameters.x);
        vortexNormalOffset += (
            tangentDirection * (0.14 + flowDerivative * 0.075)
            + radialDirection * ((1.0 - normalizedVortexDistance) * 0.09 + flowDerivative * 0.04))
            * vortexMask;
        vortexDepth = max(
            vortexDepth,
            (1.0 - smoothstep(0.06, 0.68, normalizedVortexDistance)) * vortexIntensity);
        vortexFoam = saturate(vortexFoam + spiralFoam + coreRing * 0.52);
        vortexFlowBands = saturate(
            vortexFlowBands
            + rotatingFlow * armRegion * vortexIntensity * 0.32);
    }

    slope += vortexNormalOffset;
    float cameraDistance = distance(_WorldSpaceCameraPos.xyz, input.worldPosition);
    float distantWaveFade = lerp(1.0, 0.58, saturate(cameraDistance / 480.0));
    slope *= max(0.28, _NormalStrength) * distantWaveFade;

    float3 worldNormal = normalize(float3(slope.x, 1.0, slope.y));
    float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.worldPosition);
    float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
    float3 halfDirection = normalize(viewDirection + lightDirection);
    float3 reflectionDirection = reflect(-viewDirection, worldNormal);

    float diffuse = saturate(dot(worldNormal, lightDirection));
    float fresnel = pow(1.0 - saturate(dot(worldNormal, viewDirection)), 3.2);
    float gloss = lerp(92.0, 34.0, saturate(_roughness));
    float specular = pow(saturate(dot(worldNormal, halfDirection)), gloss)
        * max(0.35, _specularPower);
    float ripple = saturate(length(slope) * 0.62
        + abs(normalA.z - normalB.z) * 0.55);

    float colorInfluence = lerp(0.12, 0.68, saturate(_Level01ColorBlend));
    float3 deepBlue = lerp(float3(0.13, 0.17, 0.21), _depthColor.rgb, colorInfluence);
    float3 crestBlue = lerp(float3(0.24, 0.28, 0.29), _shallowColor.rgb, colorInfluence);
    float3 waterColor = lerp(deepBlue, crestBlue, ripple * 0.46);
    waterColor *= lerp(0.92, 1.16, diffuse);

    half4 encodedSky = UNITY_SAMPLE_TEXCUBE_LOD(
        unity_SpecCube0,
        reflectionDirection,
        lerp(4.8, 2.3, fresnel));
    float3 sampledSky = DecodeHDR(encodedSky, unity_SpecCube0_HDR);
    float sampledPeak = max(sampledSky.r, max(sampledSky.g, sampledSky.b));
    sampledSky *= min(1.0, 1.15 / max(0.001, sampledPeak));
    float sampledLuminance = dot(sampledSky, float3(0.24, 0.62, 0.14));
    sampledSky = lerp(sampledSky, sampledLuminance.xxx, 0.38);
    sampledSky *= float3(1.03, 1.0, 0.94);
    sampledSky *= lerp(
        float3(1.0, 1.0, 1.0),
        _Level01ReflectionTint.rgb,
        saturate(_Level01ColorBlend));
    float skySampleEnergy = saturate(sampledLuminance * 4.0);
    float skyElevation = saturate(reflectionDirection.y);
    float3 fallbackSky = lerp(
        float3(0.82, 0.47, 0.28),
        float3(0.28, 0.26, 0.24),
        smoothstep(0.02, 0.72, skyElevation));
    float3 level01FallbackSky = lerp(
        float3(0.72, 0.46, 0.3),
        float3(0.2, 0.36, 0.52),
        smoothstep(0.02, 0.72, skyElevation));
    fallbackSky = lerp(
        fallbackSky,
        level01FallbackSky,
        saturate(_Level01ColorBlend));
    float3 skyReflection = lerp(fallbackSky, sampledSky, skySampleEnergy);

    float horizonView = pow(1.0 - saturate(viewDirection.y), 2.2);
    float reflectionAmount = saturate(0.1 + fresnel * 0.7 + horizonView * 0.2);
    float reflectionBrightness = lerp(0.68, 0.98, horizonView)
        * lerp(1.0, 1.08, saturate(_Level01ColorBlend));
    waterColor = lerp(waterColor, skyReflection * reflectionBrightness, reflectionAmount);

    float horizonDistanceBlend = smoothstep(280.0, 500.0, cameraDistance);
    float3 horizonWaterColor = skyReflection * 0.94;
    waterColor = lerp(waterColor, horizonWaterColor, horizonDistanceBlend * 0.86);

    float maxCoordinate = max(abs(input.worldPosition.x), abs(input.worldPosition.z));
    float alongEdge = abs(input.worldPosition.x) > abs(input.worldPosition.z)
        ? input.worldPosition.z
        : input.worldPosition.x;
    float shoreVariation = sin(alongEdge * 0.075) * 2.6
        + sin(alongEdge * 0.031 + 1.7) * 1.8
        + sin(alongEdge * 0.17 + 0.4) * 0.65;
    float shoreDistance = maxCoordinate - (_ShorelineLevel + shoreVariation);
    float shoreEnabled = step(0.001, _ShorelineFoam);
    float shallowBand = (1.0 - smoothstep(
        -2.0,
        max(_ShorelineWidth, 0.1),
        shoreDistance)) * shoreEnabled;

    float shorelineRate = max(0.1, _shorelineSpeed);
    float shorelineFrequency = max(0.1, _shorelineFrequency);
    float shorelineTime = _Time.y * shorelineRate;
    float tidePhase = shorelineTime
        + alongEdge * 0.018
        + sin(alongEdge * 0.011) * 0.45;
    float tideCycle = 0.5 + 0.5 * sin(tidePhase);
    float tideSurge = smoothstep(0.08, 0.88, tideCycle);
    float shoreReach = max(1.8, _TideAmount * 2.2 + _TideSpread * 1.4);
    float waveAdvance = lerp(1.8, -shoreReach, tideSurge)
        + sin(shorelineTime * 1.46 - alongEdge * 0.036) * 0.28;
    float foamNoise = 0.5 + 0.28 * sin(alongEdge * 0.31 + _Time.y * 0.94)
        + 0.22 * sin(alongEdge * 0.73 - _Time.y * 0.53);
    float2 foamUv = input.worldPosition.xz * 0.105
        + direction * _Time.y * 0.021;
    float rawFoamTexture = tex2D(_FoamTex, foamUv).r;
    float foamTexture = smoothstep(0.3, 0.7, rawFoamTexture);
    float foamBreakup = smoothstep(
        0.38,
        0.68,
        foamNoise * 0.38 + foamTexture * 0.62);
    foamBreakup = lerp(0.1, 1.0, foamBreakup);
    float brokenWaveAdvance = waveAdvance
        + (rawFoamTexture - 0.5) * 1.15
        + sin(alongEdge * 0.21 + _Time.y * 0.37) * 0.32;
    float primaryLine = 1.0 - smoothstep(
        0.14,
        1.28,
        abs(shoreDistance - brokenWaveAdvance));
    float secondaryLine = 1.0 - smoothstep(
        0.18,
        1.42,
        abs(shoreDistance - brokenWaveAdvance - 1.55));
    secondaryLine *= smoothstep(0.58, 0.84, foamNoise) * 0.2;
    float shoreFoam = saturate(primaryLine * foamBreakup + secondaryLine);
    shoreFoam *= _ShorelineFoam * shoreEnabled;

    float3 reflectedShallow = lerp(float3(0.34, 0.46, 0.49), skyReflection, 0.26);
    waterColor = lerp(waterColor, reflectedShallow, shallowBand * 0.7);
    waterColor = lerp(waterColor, _FoamColor.rgb, shoreFoam);

    float sunMirror = pow(saturate(dot(reflectionDirection, lightDirection)), 110.0);
    float3 sunColor = _LightColor0.rgb * lerp(float3(1.0, 1.0, 1.0), _SpecularColor.rgb, 0.12);
    waterColor += sunColor * sunMirror * 1.35;
    waterColor += sunColor * specular * 0.22;
    waterColor += ripple * float3(0.006, 0.018, 0.035);
    waterColor *= max(0.92, _overallBrightness);

    float cinematicBlend = saturate(_CinematicOcean);
    float microStrength = lerp(0.16, 0.48, saturate(_CinematicMicroRipple));
    float3 cinematicNormal = normalize(float3(
        input.geometricNormal.x + slope.x * microStrength,
        max(0.45, input.geometricNormal.y),
        input.geometricNormal.z + slope.y * microStrength));
    float3 cinematicReflectionDirection = reflect(-viewDirection, cinematicNormal);
    float cinematicNdotV = saturate(dot(cinematicNormal, viewDirection));
    float cinematicNdotL = saturate(dot(cinematicNormal, lightDirection));
    float physicalFresnel = 0.02 + 0.98 * pow(1.0 - cinematicNdotV, 5.0);

    half4 cinematicEncodedSky = UNITY_SAMPLE_TEXCUBE_LOD(
        unity_SpecCube0,
        cinematicReflectionDirection,
        lerp(3.8, 1.6, physicalFresnel));
    float3 cinematicSky = DecodeHDR(cinematicEncodedSky, unity_SpecCube0_HDR);
    float cinematicSkyPeak = max(cinematicSky.r, max(cinematicSky.g, cinematicSky.b));
    cinematicSky *= min(1.0, 1.65 / max(0.001, cinematicSkyPeak));
    float cinematicSkyEnergy = saturate(dot(cinematicSky, float3(0.24, 0.62, 0.14)) * 3.2);
    float cinematicSkyElevation = saturate(cinematicReflectionDirection.y);
    float3 cinematicFallbackSky = lerp(
        float3(0.68, 0.44, 0.3),
        float3(0.2, 0.36, 0.5),
        smoothstep(0.0, 0.78, cinematicSkyElevation));
    cinematicSky = lerp(cinematicFallbackSky, cinematicSky, cinematicSkyEnergy);
    cinematicSky *= _Level01ReflectionTint.rgb;

    float waveCrest = saturate(
        input.waveHeight / max(0.04, _CompatWaveAmplitude) * 0.5 + 0.5);
    float fineRipple = saturate(length(slope) * 0.44);
    float3 cinematicBase = lerp(
        _depthColor.rgb,
        _shallowColor.rgb,
        saturate(waveCrest * 0.22 + fineRipple * 0.18));
    cinematicBase *= lerp(0.82, 1.08, cinematicNdotL);
    cinematicBase += _LightColor0.rgb * waveCrest * cinematicNdotL * 0.035;

    float viewReflection = saturate(0.16 + physicalFresnel * 0.78);
    viewReflection *= saturate(_CinematicReflection);
    float3 cinematicColor = lerp(cinematicBase, cinematicSky, viewReflection);
    // The previous cinematic branch discarded shallowBand entirely, so the
    // deep-ocean blue continued all the way to dry sand. Restore an underwater
    // depth gradient here: teal over the first ~30 m, then a smooth transition
    // into the deeper reflective ocean.
    float cinematicShoreWater = (1.0 - smoothstep(
        1.5,
        max(18.0, _ShorelineWidth),
        shoreDistance)) * shoreEnabled;
    float3 cinematicShallow = lerp(
        float3(0.08, 0.27, 0.3),
        _shallowColor.rgb,
        0.62);
    cinematicShallow = lerp(
        cinematicShallow,
        cinematicSky,
        saturate(viewReflection * 0.42));
    cinematicColor = lerp(
        cinematicColor,
        cinematicShallow,
        saturate(cinematicShoreWater * 0.76));
    float3 vortexDeepColor = lerp(float3(0.045, 0.16, 0.2), cinematicSky, 0.12);
    cinematicColor = lerp(cinematicColor, vortexDeepColor, vortexDepth * 0.58);
    cinematicColor = lerp(
        cinematicColor,
        lerp(vortexDeepColor, cinematicSky, 0.42),
        vortexFlowBands * 0.16);
    cinematicColor = lerp(
        cinematicColor,
        float3(0.64, 0.78, 0.8),
        saturate(vortexFoam * 0.42));

    // Sparse, texture-broken capillary foam appears only on the strongest
    // open-ocean crests. Keeping it blue-grey avoids the painted white stripes
    // that made the previous water feel synthetic.
    float openWaveHeight = saturate(
        input.waveHeight / max(0.04, _CompatWaveAmplitude) * 0.5 + 0.5);
    float openFoamTexture = tex2D(
        _FoamTex,
        input.worldPosition.xz * 0.052
            + direction * time * 0.009).r;
    float openCrestFoam = smoothstep(
        0.67,
        0.9,
        openWaveHeight * 0.58
            + fineRipple * 0.22
            + openFoamTexture * 0.2);
    openCrestFoam *= saturate(_OpenOceanBlend)
        * saturate(_OpenOceanFoam)
        * lerp(1.0, 0.62, saturate(cameraDistance / 850.0));
    cinematicColor = lerp(
        cinematicColor,
        float3(0.58, 0.7, 0.72),
        openCrestFoam * 0.34);

    float3 cinematicHalfDirection = normalize(viewDirection + lightDirection);
    float cinematicNdotH = saturate(dot(cinematicNormal, cinematicHalfDirection));
    float broadGlint = pow(cinematicNdotH, 72.0) * 0.2;
    float fineGlint = pow(cinematicNdotH, 180.0)
        * smoothstep(0.18, 0.62, fineRipple);
    cinematicColor += _LightColor0.rgb
        * (broadGlint + fineGlint)
        * saturate(_CinematicSunGlint);

    float normalizedHorizonDistance = saturate((cameraDistance - 80.0) / 760.0);
    float distanceAtmosphere = smoothstep(0.0, 1.0, normalizedHorizonDistance);
    float grazingAtmosphere = smoothstep(
        0.22,
        0.92,
        1.0 - cinematicNdotV);
    float horizonAtmosphere = saturate(
        distanceAtmosphere
        * lerp(0.78, 1.0, grazingAtmosphere)
        * saturate(_CinematicHorizonBlend));
    float3 horizonColor = lerp(
        _CinematicHorizonColor.rgb,
        cinematicSky,
        0.08);
    cinematicColor = lerp(cinematicColor, _FoamColor.rgb, shoreFoam * 0.5);
    cinematicColor *= lerp(0.96, 1.04, saturate(_overallBrightness - 0.8));
    UNITY_APPLY_FOG(input.fogCoord, cinematicColor);
    cinematicColor = lerp(
        cinematicColor,
        horizonColor,
        horizonAtmosphere * 0.55);

    return fixed4(lerp(waterColor, cinematicColor, cinematicBlend), 1.0);
}
