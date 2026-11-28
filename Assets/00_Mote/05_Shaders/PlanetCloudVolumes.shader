Shader "Mote/Planet Cloud Volumes" {
 Properties {
     _Sunlit ("Sunlit", Color) = (.97,1,.96,1)
     _Shade ("Underside", Color) = (.65,.92,.92,1)
     _Wind ("Wind (radians per second)", Range(0,.06)) = .013
 }
 SubShader {
     Tags { "Queue"="Transparent" "RenderType"="Transparent" }
     Pass {
         Cull Back ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
         CGPROGRAM
         #pragma vertex vert
         #pragma fragment frag
         #include "UnityCG.cginc"
         float4 _Sunlit, _Shade;
         float _Wind;
         struct Input { float4 vertex:POSITION; float3 normal:NORMAL; float2 wind:TEXCOORD0; float3 center:TEXCOORD1; };
         struct Output { float4 pos:SV_POSITION; half3 shade:TEXCOORD0; half alpha:TEXCOORD1; };
         float3 turn(float3 v,float s,float c) {return float3(c*v.x+s*v.z,v.y,c*v.z-s*v.x);}
         Output vert(Input v) {
             float s,c;
             sincos(_Time.y*_Wind*v.wind.x,s,c);
             float3 center=turn(v.center,s,c);
             float3 pos=turn(v.vertex.xyz,s,c);
             Output o;
             o.pos=UnityObjectToClipPos(float4(pos,1));
             float3 n=UnityObjectToWorldNormal(turn(v.normal,s,c));
             float3 radial=normalize(mul((float3x3)unity_ObjectToWorld,center));
             float3 toCamera=normalize(lerp(_WorldSpaceCameraPos-mul(unity_ObjectToWorld,float4(center,1)).xyz,
                 UNITY_MATRIX_V[2].xyz,unity_OrthoParams.w));
             float facing=dot(radial,toCamera);
             float light=smoothstep(-.65,.68,dot(n,normalize(float3(-.36,.84,-.42))));
             o.shade=lerp(_Shade.rgb,_Sunlit.rgb,light);
             float rim=smoothstep(.02,.43,saturate(dot(n,toCamera)));
             o.alpha=v.wind.y*lerp(.36,1,rim)*(1-smoothstep(.03,.54,facing));
             return o;
         }
         fixed4 frag(Output i):SV_Target {return fixed4(i.shade,i.alpha);}
         ENDCG
     }
 }
}