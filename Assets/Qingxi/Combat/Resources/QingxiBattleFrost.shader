Shader "Qinglan/Battle Hud Frost"
{
    Properties
    {
        [PerRendererData] _MainTex ("Scene Capture", 2D) = "black" {}
        _FrostViewport ("Viewport", Vector) = (0, 0, 1, 1)
        _FrostShape ("Shape", Vector) = (1280, 720, 12, 0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _FrostViewport;
            float4 _FrostShape;

            struct VertexInput
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            VertexOutput Vertex(VertexInput input)
            {
                VertexOutput output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                return output;
            }

            fixed4 Fragment(VertexOutput input) : SV_Target
            {
                float2 uv = _FrostViewport.xy + input.uv * _FrostViewport.zw;
                float2 stepSize = abs(_MainTex_TexelSize.xy) * 2.4;
                float3 world = tex2D(_MainTex, uv).rgb * 0.20;
                world += (tex2D(_MainTex, uv + float2(stepSize.x, 0)).rgb +
                    tex2D(_MainTex, uv - float2(stepSize.x, 0)).rgb +
                    tex2D(_MainTex, uv + float2(0, stepSize.y)).rgb +
                    tex2D(_MainTex, uv - float2(0, stepSize.y)).rgb) * 0.12;
                world += (tex2D(_MainTex, uv + stepSize).rgb +
                    tex2D(_MainTex, uv - stepSize).rgb +
                    tex2D(_MainTex, uv + float2(stepSize.x, -stepSize.y)).rgb +
                    tex2D(_MainTex, uv + float2(-stepSize.x, stepSize.y)).rgb) * 0.08;

                float luminance = dot(world, float3(0.2126, 0.7152, 0.0722));
                float3 ink = float3(0.018, 0.034, 0.027) + lerp(luminance.xxx, world, 0.26) * 0.18;
                float2 distanceToEdge = min(input.uv, 1 - input.uv) * _FrostShape.xy;
                float mask = smoothstep(0.0, _FrostShape.z, min(distanceToEdge.x, distanceToEdge.y));
                return fixed4(ink, mask);
            }
            ENDCG
        }
    }
}
