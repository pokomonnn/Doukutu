Shader "Custom/InteractableWhiteSilhouette2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Alpha Multiplier", Color) = (1, 1, 1, 1)
        _AlphaThreshold ("Outline Alpha Threshold", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "WhiteSilhouette"
            Tags { "LightMode" = "Universal2D" }

            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _AlphaThreshold;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half sampledAlpha = SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex, input.uv).a;

                half alpha = sampledAlpha * input.color.a * _Color.a;
                clip(sampledAlpha - _AlphaThreshold);
                // テクスチャが何色でも、輪郭用の複製は白いシルエット。
                return half4(1.0h, 1.0h, 1.0h, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
