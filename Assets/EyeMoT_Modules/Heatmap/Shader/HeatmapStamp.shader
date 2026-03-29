Shader "Custom/HeatmapStamp"
{
        Properties
    {
        _MainTex ("Previous Heat", 2D) = "black" {}
        _MouseUV ("Mouse UV", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Float) = 0.05
        _Intensity ("Intensity", Float) = 0.02
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MouseUV;
            float _Radius;
            float _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float prev = tex2D(_MainTex, i.uv).r;

                float d = distance(i.uv, _MouseUV.xy);
                float spot = 1.0 - smoothstep(0.0, _Radius, d);

                float heat = prev + spot * _Intensity;
                heat = saturate(heat); // 0～1に丸める

                return fixed4(heat, 0, 0, 1);
            }
            ENDCG
        }
    }
}