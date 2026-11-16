// Soft coloured halo around a sprite's silhouette (the boon outline behind Hanuman).
// Draw it on a copy of the sprite, scaled up by _Grow, behind the real one. Colour comes from the SpriteRenderer colour.
// Lives in Resources so builds include it (loaded with Resources.Load<Shader>("Level02/Shaders/FightGlow")).
Shader "BalHanuman/FightGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Grow ("Quad grow (matches transform scale)", Float) = 1.12
        _Spread ("Halo radius in texels", Float) = 10
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_TexelSize;
            float _Grow, _Spread;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = (v.uv - 0.5) * _Grow + 0.5;     // the quad is drawn bigger, the picture stays the same size: room for the halo
                o.color = v.color;
                return o;
            }

            float A(float2 uv)
            {
                float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                return tex2D(_MainTex, uv).a * inside;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 r = _MainTex_TexelSize.xy * _Spread;
                float a = A(i.uv) * 0.5;
                // two rings of 8 taps: a cheap blur of the silhouette
                [unroll] for (int k = 0; k < 8; k++)
                {
                    float ang = k * 0.785398;
                    float2 d = float2(cos(ang), sin(ang));
                    a += A(i.uv + d * r) * 0.1;
                    a += A(i.uv + d * r * 0.5) * 0.12;
                }
                a = saturate(a);
                return fixed4(i.color.rgb, a * i.color.a);
            }
            ENDCG
        }
    }
}
