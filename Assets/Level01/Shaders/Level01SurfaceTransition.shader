Shader "Custom/Level01SurfaceTransition"
{
    Properties
    {
        _MainTex ("Transition Texture", 2D) = "white" {}
        _NoiseTex ("Breakup", 2D) = "gray" {}
        _Color ("Tint", Color) = (0.75, 0.8, 0.62, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        LOD 150
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf Standard alpha:fade noshadow noambient
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _NoiseTex;
        fixed4 _Color;
        half _Opacity;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_NoiseTex;
            float3 worldPos;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            float2 worldUv = input.worldPos.xz;
            fixed3 baseColor = tex2D(_MainTex, worldUv * 0.32).rgb;
            half broadNoise = tex2D(_NoiseTex, worldUv * 0.075).r;
            half fineNoise = tex2D(_NoiseTex, worldUv * 0.34 + 0.37).r;
            half across = saturate(input.uv_MainTex.y);

            half innerWidth = lerp(0.06h, 0.2h, fineNoise);
            half outerLimit = lerp(0.58h, 0.98h, broadNoise);
            half innerFade = smoothstep(0.0h, innerWidth, across);
            half outerFade = 1.0h - smoothstep(outerLimit - 0.26h, outerLimit, across);
            half breakup = lerp(0.62h, 1.0h, smoothstep(0.25h, 0.78h, fineNoise));
            half edgeFade = innerFade * outerFade;

            output.Albedo = baseColor * _Color.rgb;
            output.Metallic = 0.0;
            output.Smoothness = 0.06;
            output.Alpha = edgeFade * breakup * _Opacity;
        }
        ENDCG
    }
}
