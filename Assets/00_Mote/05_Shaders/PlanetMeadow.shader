Shader "Mote/Planet Meadow"
{
    Properties
    {
        _Breeze ("Crown movement", Range(0,1)) = 0
        _Shore ("Meadow and lake", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Breeze, _Shore;
            CBUFFER_END
            struct Input { float3 pos:POSITION; float3 normal:NORMAL; half4 color:COLOR; float2 shore:TEXCOORD1; };
            struct Varyings { float4 pos:SV_POSITION; half3 normal:TEXCOORD0; half4 color:COLOR; float3 local:TEXCOORD1; float3 world:TEXCOORD2; half shore:TEXCOORD3; };
            Varyings Vert(Input v)
            {
                Varyings o;
                float wave = sin(_Time.y*1.65 + dot(v.pos,float3(5,2,3)));
                v.pos.xz += float2(.006,.003)*wave*v.color.a*_Breeze;
                o.local=v.pos;
                o.shore=v.shore.x;
                o.world=TransformObjectToWorld(v.pos);
                o.pos=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normal);
                o.color=v.color;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord=ComputeScreenPos(TransformWorldToHClip(i.world));
                #else
                    float4 shadowCoord=TransformWorldToShadowCoord(i.world);
                #endif
                Light sun=GetMainLight(shadowCoord);
                float light=dot(normalize(i.normal),normalize(float3(-.50,.85,-.75)));
                float3 paint=i.color.rgb;
                float3 p=i.local;
                float brush=sin(dot(p,float3(12,7,9))+1.3*sin(dot(p,float3(6,13,-9))))+
                            .45*sin(dot(p,float3(-23,15,20)));
                float pigment=smoothstep(-.85,.85,brush);
                float water=smoothstep(.08,.88,i.shore+.04*sin(dot(p,float3(42,17,-31))));
                float3 grass=lerp(float3(.52,.72,.36),float3(.75,.83,.37),pigment);
                float deep=smoothstep(.65,.97,i.shore);
                float3 lake=lerp(float3(.31,.70,.67),float3(.21,.57,.76),deep);
                lake+=.012*sin(dot(p,float3(23,16,9))-_Time.y*.67);
                paint=lerp(paint,lerp(grass,lake,water),_Shore);
                float3 tint=lerp(float3(.61,.72,.77),float3(1.04,1.025,.97),smoothstep(-.50,.68,light));
                paint=saturate(paint*tint*lerp(float3(.73,.81,.87),float3(1,1,1),sun.shadowAttenuation));
                #ifndef UNITY_COLORSPACE_GAMMA
                    paint=SRGBToLinear(paint);
                #endif
                return half4(paint,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}

