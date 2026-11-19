Shader "Qinglan/Tactical Ground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Tile mask", 2D) = "white" {}
        _GroundArt ("Ground art", 2D) = "white" {}
        _ArtBounds ("Ground art bounds", Vector) = (0,0,1,1)
        _GroundTint ("Ground tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_GroundArt);
            SAMPLER(sampler_GroundArt);
            CBUFFER_START(UnityPerMaterial)
                float4 _ArtBounds;
                half4 _GroundTint;
            CBUFFER_END

            struct Attributes
            {
                float3 position : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 clip : SV_POSITION;
                float2 maskUv : TEXCOORD0;
                float2 artUv : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vertex(Attributes input)
            {
                float3 world = TransformObjectToWorld(input.position);
                Varyings output;
                output.clip = TransformWorldToHClip(world);
                output.maskUv = input.uv;
                output.artUv = (world.xy - _ArtBounds.xy) / _ArtBounds.zw;
                output.color = input.color * _GroundTint;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_GroundArt, sampler_GroundArt, input.artUv) * input.color;
                color.a *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.maskUv).a;
                return color;
            }
            ENDHLSL
        }
    }
}
