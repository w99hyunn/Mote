Shader "Mote/Planet Air" {
 SubShader { Tags {"Queue"="Background"} Pass { Cull Off ZWrite Off
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct F{float4 pos:SV_POSITION;float4 screen:TEXCOORD0;};
 F vert(float4 v:POSITION){F o;o.pos=UnityObjectToClipPos(v);o.screen=ComputeScreenPos(o.pos);return o;}
 fixed4 frag(F i):SV_Target{
 float2 uv=i.screen.xy/i.screen.w;
 float3 sky=lerp(float3(.77,.94,.86),float3(.24,.83,.94),smoothstep(.18,1,uv.y));
 float lane=uv.y-.13*uv.x-.026*sin(uv.x*4+_Time.y*.027);
 float clouds=exp(-484*(lane-.72)*(lane-.72))*.21 + exp(-1024*(lane-.89)*(lane-.89))*.15;
 sky=lerp(sky,float3(.92,.99,.93),clouds);
 #ifndef UNITY_COLORSPACE_GAMMA
 sky=GammaToLinearSpace(sky);
 #endif
 return float4(sky,1);}
 ENDCG
 } } }
