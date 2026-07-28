Shader "IslandMap/IllustratedOcean"
{
    Properties
    {
        _MainTex ("Ocean Texture", 2D) = "white" {}
        _ShoreMask ("Shore Distance Mask", 2D) = "black" {}
        _DeepColor ("Deep Color", Color) = (0.035, 0.20, 0.31, 1)
        _MidColor ("Mid Color", Color) = (0.04, 0.36, 0.48, 1)
        _ShallowColor ("Shallow Color", Color) = (0.12, 0.58, 0.61, 1)
        _FoamColor ("Foam Color", Color) = (0.66, 0.84, 0.75, 1)
        _TextureStrength ("Texture Strength", Range(0, 1)) = 0.28
        _FoamStrength ("Foam Strength", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-10" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 oceanUv : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _ShoreMask;
            float4 _DeepColor;
            float4 _MidColor;
            float4 _ShallowColor;
            float4 _FoamColor;
            float _TextureStrength;
            float _FoamStrength;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.oceanUv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed3 oceanSample = tex2D(_MainTex, input.oceanUv).rgb;
                fixed2 shore = tex2D(_ShoreMask, input.uv).rg;
                float luminance = dot(oceanSample, fixed3(0.2126, 0.7152, 0.0722));
                float textureVariation = lerp(1.0, lerp(0.78, 1.18, luminance), _TextureStrength);

                fixed3 water = lerp(_DeepColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.72, shore.r));
                water = lerp(water, _ShallowColor.rgb, smoothstep(0.72, 1.0, shore.r));
                water *= textureVariation;
                water = lerp(water, _FoamColor.rgb, saturate(shore.g * _FoamStrength));
                return fixed4(water, 1.0);
            }
            ENDCG
        }
    }
}
