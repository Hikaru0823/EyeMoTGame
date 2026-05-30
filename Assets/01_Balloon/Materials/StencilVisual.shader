Shader "Custom/StencilVisual"
{
    Properties
    {
        _MainTex ("Image", 2D) = "white" {}

        _Color ("Color", Color) = (1, 1, 1, 1)

        [Range(0,1)]
        _Alpha ("Alpha", Float) = 1

        [Enum(Off,0,On,1)]
        _ZWrite ("ZWrite", Float) = 0

        [Enum(UnityEngine.Rendering.CompareFunction)]
        _ZTest ("ZTest", Float) = 8
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            ZWrite [_ZWrite]
            ZTest [_ZTest]

            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref 1
                Comp Equal
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _Alpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                col *= _Color;
                col.a *= _Alpha;

                return col;
            }

            ENDCG
        }
    }
}