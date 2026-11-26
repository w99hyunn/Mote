Shader "DoHwa/UI/Main Island Water"
{
    Properties
    {
        [PerRendererData] _MainTex("Island", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 size = float2(1072, 976);
                float2 pixel = float2(288, -80) + float2(input.uv.x, 1.0 - input.uv.y) * size;
                half4 island = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float waterColor = smoothstep(0.035, 0.22, island.b - island.r);

                float2 waterfall = (pixel - float2(837, 463)) / float2(34, 45);
                float waterfallMask = 1.0 - smoothstep(0.18, 1.0, dot(waterfall, waterfall));
                float fallWave = 0.5 + 0.5 * sin(pixel.y * 0.24 + sin(pixel.x * 0.19) - _Time.y * 7.0);

                float2 stream = (pixel - float2(941, 524)) / float2(88, 35);
                float streamMask = 1.0 - smoothstep(0.12, 1.0, dot(stream, stream));
                float streamWave = 0.5 + 0.5 * sin(pixel.x * 0.10 + pixel.y * 0.16 - _Time.y * 3.6);

                float shimmer = (waterfallMask * fallWave * fallWave * 0.23 +
                    streamMask * streamWave * streamWave * 0.15) * waterColor;
                island.rgb = lerp(island.rgb, half3(0.57, 0.96, 0.97), shimmer);
                return island * input.color;
            }
            ENDHLSL
        }
    }
}
