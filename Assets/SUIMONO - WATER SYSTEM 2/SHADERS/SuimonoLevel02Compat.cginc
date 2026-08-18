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
float _turbulenceFactor;
float _specularPower;
float _roughness;
float _overallBrightness;
float _Level01ColorBlend;
fixed4 _Level01ReflectionTint;
float _ShorelineLevel;
float _ShorelineWidth;
float _ShorelineFoam;
fixed4 _depthColor;
fixed4 _shallowColor;
fixed4 _SpecularColor;

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
};

SuimonoCompatVertexOutput SuimonoCompatVert(SuimonoCompatVertexInput input)
{
    SuimonoCompatVertexOutput output;
    float3 worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
    float time = _Time.y;
    float swell = sin(worldPosition.x * 0.055 + time * 0.52)
        + sin(worldPosition.z * 0.071 - time * 0.43) * 0.65;
    input.vertex.y += swell * (_heightScale + _lgWaveHeight) * 0.16;

    output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
    output.position = UnityObjectToClipPos(input.vertex);
    output.uv = output.worldPosition.xz * 0.045;
    return output;
}

fixed4 SuimonoCompatFrag(SuimonoCompatVertexOutput input) : SV_Target
{
    float time = _Time.y;
    float2 direction = normalize(_suimono_Dir.xz + float2(0.001, 0.001));
    float2 perpendicular = float2(-direction.y, direction.x);
    float2 uvA = input.uv + direction * time * 0.018;
    float2 uvB = input.uv * 0.53 - perpendicular * time * 0.012;
    float2 uvC = input.uv * 0.19 + (direction + perpendicular) * time * 0.004;

    float3 normalA = UnpackNormal(tex2D(_NormalTexS, uvA));
    float3 normalB = UnpackNormal(tex2D(_NormalTexD, uvB));
    float3 normalC = UnpackNormal(tex2D(_NormalTexR, uvC));
    float2 slope = normalA.xy
        + normalB.xy * (0.52 + _turbulenceFactor)
        + normalC.xy * 0.24;
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

    float waveAdvance = sin(time * 0.72 + alongEdge * 0.052) * 0.72
        + sin(time * 0.37 - alongEdge * 0.021) * 0.38;
    float foamNoise = 0.5 + 0.3 * sin(alongEdge * 0.31 + time * 0.84)
        + 0.2 * sin(alongEdge * 0.73 - time * 0.47);
    float2 foamUv = input.worldPosition.xz * 0.082
        + direction * time * 0.014;
    float foamTexture = smoothstep(0.28, 0.76, tex2D(_FoamTex, foamUv).r);
    float foamBreakup = lerp(0.08, 1.0, smoothstep(0.44, 0.8, foamNoise));
    foamBreakup *= lerp(0.42, 1.12, foamTexture);
    float primaryLine = 1.0 - smoothstep(
        0.12,
        0.48,
        abs(shoreDistance - 0.65 - waveAdvance * 0.82));
    float secondaryLine = 1.0 - smoothstep(
        0.1,
        0.48,
        abs(shoreDistance - 3.1 + waveAdvance * 0.42));
    secondaryLine *= smoothstep(0.72, 0.91, foamNoise) * 0.12;
    float shoreFoam = saturate(primaryLine * foamBreakup + secondaryLine);
    shoreFoam *= _ShorelineFoam * shoreEnabled;

    float3 reflectedShallow = lerp(float3(0.34, 0.46, 0.49), skyReflection, 0.26);
    waterColor = lerp(waterColor, reflectedShallow, shallowBand * 0.7);
    waterColor = lerp(waterColor, float3(0.84, 0.88, 0.86), shoreFoam);

    float sunMirror = pow(saturate(dot(reflectionDirection, lightDirection)), 110.0);
    float3 sunColor = _LightColor0.rgb * lerp(float3(1.0, 1.0, 1.0), _SpecularColor.rgb, 0.12);
    waterColor += sunColor * sunMirror * 1.35;
    waterColor += sunColor * specular * 0.22;
    waterColor += ripple * float3(0.006, 0.018, 0.035);
    waterColor *= max(0.92, _overallBrightness);

    return fixed4(waterColor, 1.0);
}
