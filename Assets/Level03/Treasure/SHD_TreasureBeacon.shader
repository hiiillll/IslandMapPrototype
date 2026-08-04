Shader "IslandMap/TreasureBeacon"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (1, 0.65, 0.15, 0.2)
        _FlowSpeed ("Flow Speed", Range(0, 3)) = 0.45
        _FlowStrength ("Flow Strength", Range(0, 1)) = 0.32
        _EdgeSoftness ("Edge Softness", Range(0.5, 4)) = 1.6
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha One
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normalWorld : TEXCOORD0;
                float3 viewDirection : TEXCOORD1;
                float2 uv : TEXCOORD2;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _TintColor;
            float _FlowSpeed;
            float _FlowStrength;
            float _EdgeSoftness;

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                float3 worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.normalWorld = UnityObjectToWorldNormal(input.normal);
                output.viewDirection = _WorldSpaceCameraPos.xyz - worldPosition;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float facing = abs(dot(
                    normalize(input.normalWorld),
                    normalize(input.viewDirection)));
                float edgeFade = lerp(0.42, 1.0, pow(saturate(facing), _EdgeSoftness));

                float flowPhase = input.uv.y * 22.0 - _Time.y * _FlowSpeed;
                float broadFlow = 0.5 + 0.5 * sin(flowPhase * 6.2831853);
                float fineFlow = 0.5 + 0.5 * sin((flowPhase * 2.37 + input.uv.x) * 6.2831853);
                float flow = lerp(1.0, 0.68 + broadFlow * 0.22 + fineFlow * 0.1, _FlowStrength);

                float alpha = _TintColor.a * input.color.a * edgeFade * flow;
                float energy = 0.82 + flow * 0.36;
                return fixed4(_TintColor.rgb * energy, alpha);
            }
            ENDCG
        }
    }
}
