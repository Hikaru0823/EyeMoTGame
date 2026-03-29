Shader "Custom/LineBeam"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0.2, 0.8, 1.0, 1.0)
        _Intensity ("Intensity", Range(0, 10)) = 2
        _ScrollX ("Scroll X", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Fog { Mode Off }

        Pass
        {
            Blend SrcAlpha One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Intensity;
            float _ScrollX;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos    : SV_POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                float2 uv = TRANSFORM_TEX(v.uv, _MainTex);
                uv.x += _Time.y * _ScrollX;
                o.uv = uv;

                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);

                fixed4 col;
                col.rgb = tex.rgb * _Color.rgb * i.color.rgb * _Intensity;
                col.a   = tex.a   * _Color.a   * i.color.a;

                // 透明部の黒にじみ軽減
                col.rgb *= col.a;

                return col;
            }
            ENDCG
        }
    }
}