Shader "DoHwa/UI/Main Background Stars"
{
    Properties
    {
        [PerRendererData] _MainTex("Background", 2D) = "white" {}
        _Strength("Twinkle Strength", Range(0, 1)) = 0.72
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
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
            float _Strength;
            CBUFFER_END

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
                half4 pulses : TEXCOORD1;
                half2 edgePulses : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;

                float4 wave = 0.5 + 0.5 * sin(_Time.y * float4(1.31, 1.69, 1.13, 1.51)
                    + float4(0.3, 3.8, 2.2, 5.4));
                float2 edgeWave = 0.5 + 0.5 * sin(_Time.y * float2(1.19, 1.43) + float2(4.2, 1.0));
                output.pulses = wave * wave * wave;
                output.edgePulses = edgeWave * edgeWave * edgeWave;
                return output;
            }

            float StarGlow(float2 uv, float2 center, float radius, float pulse)
            {
                float2 delta = uv - center;
                delta.x *= _MainTex_TexelSize.z / _MainTex_TexelSize.w;
                float distanceSquared = dot(delta, delta) / (radius * radius);
                float halo = 1.0 - smoothstep(0.0, 1.0, distanceSquared);
                float core = 1.0 - smoothstep(0.0, 0.13, distanceSquared);
                return pulse * (0.30 * halo * halo + 0.22 * core);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Match the six painted stars in PeachOrchardTitle. Source coordinates start at the top left.
                float2 artworkUV = float2(input.uv.x, 1.0 - input.uv.y);
                float glow = StarGlow(artworkUV, float2(381, 220) / float2(1672, 941), 0.027, input.pulses.x)
                    + StarGlow(artworkUV, float2(546, 102) / float2(1672, 941), 0.024, input.pulses.y)
                    + StarGlow(artworkUV, float2(1392, 126) / float2(1672, 941), 0.030, input.pulses.z)
                    + StarGlow(artworkUV, float2(1462, 454) / float2(1672, 941), 0.028, input.pulses.w)
                    + StarGlow(artworkUV, float2(53, 282) / float2(1672, 941), 0.018, input.edgePulses.x)
                    + StarGlow(artworkUV, float2(271, 544) / float2(1672, 941), 0.026, input.edgePulses.y);
                half4 background = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                background.rgb = lerp(background.rgb, half3(1.0, 0.88, 0.61), saturate(glow * _Strength));
                return background * input.color;
            }
            ENDHLSL
        }
    }
}
