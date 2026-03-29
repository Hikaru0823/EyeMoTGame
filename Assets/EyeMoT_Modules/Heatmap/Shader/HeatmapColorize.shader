Shader "Custom/HeatmapColorize"
{
    Properties
    {
        _MainTex ("Heat Texture", 2D) = "black" {}
        _MaxValue ("Max Value", Float) = 1.0
        _AlphaMultiplier ("Alpha Multiplier", Float) = 1.0
        _MinVisible ("Min Visible Threshold", Float) = 0.001
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _MaxValue;
            float _AlphaMultiplier;
            float _MinVisible;

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

            float3 HeatmapColor(float t)
            {
                t = saturate(t);

                if (t < 0.25)
                    return lerp(float3(0,0,1), float3(0,1,1), t / 0.25);
                else if (t < 0.5)
                    return lerp(float3(0,1,1), float3(0,1,0), (t - 0.25) / 0.25);
                else if (t < 0.75)
                    return lerp(float3(0,1,0), float3(1,1,0), (t - 0.5) / 0.25);
                else
                    return lerp(float3(1,1,0), float3(1,0,0), (t - 0.75) / 0.25);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float heat = tex2D(_MainTex, i.uv).r;
                float t = heat / max(_MaxValue, 0.0001);

                if (heat <= _MinVisible)
                    return fixed4(0, 0, 0, 0);

                float3 col = HeatmapColor(t);
                float alpha = lerp(0.8, 1.0, saturate(t * _AlphaMultiplier));

                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}