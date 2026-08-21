Shader "Hidden/SpeedEscape/RearViewMirror"
{
    Properties
    {
        _MainTex ("Rear View", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.uv;
                float verticalTaper = lerp(0.445, 0.485, uv.y);
                float sideDistance = verticalTaper - abs(uv.x - 0.5);
                float endDistance = min(uv.y, 1.0 - uv.y);
                float shapeDistance = min(sideDistance, endDistance);
                float edgeSoftness = max(fwidth(shapeDistance) * 1.5, 0.0015);
                float shapeAlpha = smoothstep(0.0, edgeSoftness, shapeDistance);

                float frameWidth = 0.035;
                float innerMask = smoothstep(frameWidth, frameWidth + edgeSoftness, shapeDistance);
                fixed4 sceneColor = tex2D(_MainTex, uv);
                sceneColor.rgb = sceneColor.rgb * fixed3(0.94, 0.97, 1.0);

                float highlight = smoothstep(0.42, 1.0, uv.y) * 0.08;
                fixed3 frameColor = fixed3(0.035, 0.045, 0.055) + highlight;
                fixed3 finalColor = lerp(frameColor, sceneColor.rgb, innerMask);
                return fixed4(finalColor * input.color.rgb, shapeAlpha * input.color.a);
            }
            ENDCG
        }
    }
}
