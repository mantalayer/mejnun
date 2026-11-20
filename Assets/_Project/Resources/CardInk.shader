Shader "Mejnun/Card Ink"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _InkMode ("Ink Mode", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _InkMode;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.uv) * input.color;
                if (_InkMode > 1.5)
                {
                    float darkness = max(color.r, max(color.g, color.b));
                    if (darkness < 0.22)
                        color.rgb = fixed3(0.06, 0.55, 0.16) * color.a;
                }
                else if (_InkMode > 0.5)
                {
                    if (color.r > color.g * 1.25 && color.r > color.b * 1.25)
                        color.rgb = fixed3(0.05, 0.26, 0.88) * color.a;
                }
                return color;
            }
            ENDCG
        }
    }
}