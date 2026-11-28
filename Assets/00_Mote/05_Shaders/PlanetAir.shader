Shader "Mote/Planet Air" {
 Properties {
  _Horizon ("Horizon (sRGB)", Vector) = (.77,.94,.86,1)
  _Zenith ("Zenith (sRGB)", Vector) = (.24,.83,.94,1)
  _Cloud ("Cloud (sRGB)", Vector) = (.92,.99,.93,1)
  [Toggle(CLOUD_BANKS)] _CloudBanks ("Soft cloud banks", Float) = 0
  _Stars ("Star visibility", Range(0,1)) = 0
 }
 SubShader { Tags {"Queue"="Background"} Pass { Cull Off ZWrite Off
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma shader_feature_local_fragment _ CLOUD_BANKS
 #include "UnityCG.cginc"
 float4 _Horizon, _Zenith, _Cloud;
 float _Stars;

 struct F{float4 pos:SV_POSITION;float4 screen:TEXCOORD0;};
 F vert(float4 v:POSITION){F o;o.pos=UnityObjectToClipPos(v);o.screen=ComputeScreenPos(o.pos);return o;}
 fixed4 frag(F i):SV_Target{
 float2 uv=i.screen.xy/i.screen.w;
 float3 sky=lerp(_Horizon.rgb,_Zenith.rgb,smoothstep(.18,1,uv.y));
 #ifdef CLOUD_BANKS
 float aspect=_ScreenParams.x/_ScreenParams.y;

 float2 p=(uv-float2(.5,.51))*float2(aspect,1);
 // A pale halo leaves the open sky clear above the planet.
 sky=lerp(_Zenith.rgb,_Horizon.rgb,.82*(1-smoothstep(.07,.63,length(p))));
 #else
 float lane=uv.y-.13*uv.x-.026*sin(uv.x*4+_Time.y*.027);
 float clouds=exp(-484*(lane-.72)*(lane-.72))*.21 + exp(-1024*(lane-.89)*(lane-.89))*.15;
 sky=lerp(sky,_Cloud.rgb,clouds);
 #endif
 if (_Stars > 0) {
     float2 grid = uv * float2(29 * _ScreenParams.x / _ScreenParams.y,29);
     float2 cell = floor(grid);
     float seed = frac(sin(dot(cell,float2(13.17,71.43)))*4375.23);
     float2 center = .2 + .6 * frac(float2(seed*13.1,seed*41.7));
     float star = (1-smoothstep(.012,.046,length(frac(grid)-center))) * step(.72,seed);
     sky = lerp(sky,_Cloud.rgb,star*_Stars*smoothstep(.15,.7,uv.y));
 }
 #ifndef UNITY_COLORSPACE_GAMMA
 sky=GammaToLinearSpace(sky);
 #endif
 return float4(sky,1);}
 ENDCG
 } } }
