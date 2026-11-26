Shader "DoHwa/UI/Journey Confetti" {
Properties {
[PerRendererData] _MainTex("Sprite",2D)="white"{}
_Clicked("Clicked",Float)=0
_Color("Tint",Color)=(1,.76,.40,1)
_StencilComp("Stencil Comparison",Float)=8
_Stencil("Stencil ID",Float)=0
_StencilOp("Stencil Operation",Float)=0
_StencilWriteMask("Stencil Write Mask",Float)=255
_StencilReadMask("Stencil Read Mask",Float)=255
_ColorMask("Color Mask",Float)=15
}
SubShader {
Tags {"Queue"="Transparent" "RenderType"="Transparent"}
Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
Blend SrcAlpha OneMinusSrcAlpha
ColorMask [_ColorMask]
Pass {
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
#include "UnityCG.cginc"
#include "UnityUI.cginc"
struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;float4 world:TEXCOORD1;};
float _Clicked;float4 _Color,_ClipRect;
v2f vert(appdata v){v2f o;o.world=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
float hash(float seed)
{
    return frac(sin(seed * 127.1) * 43758.5453);
}

fixed4 frag(v2f i) : SV_Target
{
    float aspect = length(ddy(i.uv)) / max(length(ddx(i.uv)), .00001);
    float2 p = (i.uv - .5) * float2(aspect, 1);
    float elapsed = _Clicked > .5 ? saturate(i.color.r) * .9 : 0;
    float idleTime = _Time.y - elapsed;
    float travel = (1 - exp(-elapsed * 3.2)) / 3.2;
    float release = 1 - (1 + elapsed * 7) * exp(-elapsed * 7);
    // Highlighted and pressed clips lower the green channel to open the spray.
    float spread = saturate(1 - i.color.g);
    float motes = 0;
    for (int n = 0; n < 26; n++)
    {
        if (n >= 16 && spread <= 0) break;
        float seed = n + 1;
        float variation = hash(seed);
        float side = n % 2 == 0 ? -1 : 1;
        float rate = .24 + variation * .13;
        float life = frac(idleTime * rate + variation);
        float reach = (.22 + variation * .18) * (1 + .45 * spread);
        float sway = idleTime * 1.8 + seed;
        float2 center = float2(side * (.31 + life * reach),
            (hash(seed + 7) - .5) * (.115 + .12 * spread) + sin(sway) * .018 + life * .045);
        float2 velocity = float2(side * rate * reach, cos(sway) * .0324 + rate * .045);
        center += velocity * travel + float2(side * (.09 + .06 * variation), (hash(seed + 9) - .5) * .12) * release;

        float angle = idleTime * (variation - .5) * 3 + seed + life * 5 + elapsed * (variation - .5) * 15;
        float sine, cosine;
        sincos(angle, sine, cosine);
        float2 d = p - center;
        float2 rotated = float2(cosine * d.x - sine * d.y, sine * d.x + cosine * d.y);
        float radius = .006 + variation * .006;
        float edge;
        if (_Clicked > .5)
        {
            float2 shard = abs(rotated) / (radius * float2(.27 + .23 * abs(sine), 1.65));
            edge = max(max(shard.x, shard.y), (shard.x + shard.y) * .64);
        }
        else
        {
            edge = length(rotated / (radius * float2(.70 + .30 * abs(sine), 1.18)));
        }
        float feather = max(fwidth(edge) * .5, .07);
        float shape = 1 - smoothstep(1 - feather, 1 + feather, edge);
        float fade = sin(life * 3.14159) * (1 - smoothstep(.18 + variation * .10, .52 + variation * .25, elapsed));
        motes += shape * fade * (n < 16 ? 1 : spread);
    }
    float flash = 0;
    if (_Clicked > .5)
    {
        float bloom = smoothstep(0, .09, elapsed);
        // Keep a softly lit span beneath the label, with feathered ends.
        float beyondText = max(abs(p.x) - .135, 0);
        float2 offset = float2(beyondText / .25, p.y / .176);
        float pulse = bloom * (1 - smoothstep(.10, .42, elapsed));
        float distanceSquared = dot(offset, offset);
        float softFalloff = .60 * exp(-1.45 * distanceSquared) + .40 * exp(-.68 * distanceSquared);
        flash = softFalloff * pulse * .25;
    }
    float alpha = saturate(motes + flash) * i.color.a;
#ifdef UNITY_UI_CLIP_RECT
    alpha *= UnityGet2DClipping(i.world.xy, _ClipRect);
#endif
    // Spread low-opacity rounding across neighbouring pixels instead of visible rings.
    if (flash > 0 && motes < .001)
    {
        float noise = frac(52.9829189 * frac(dot(floor(i.vertex.xy), float2(.06711056, .00583715))));
        alpha = floor(saturate(alpha) * 255 + noise) / 255;
    }
    return float4(lerp(_Color.rgb, float3(1, .94, .78), saturate(motes * .55 + flash)), alpha);
}
ENDCG
}
}
}