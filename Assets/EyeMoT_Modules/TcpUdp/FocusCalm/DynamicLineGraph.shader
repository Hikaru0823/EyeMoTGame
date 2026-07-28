Shader "UI/DynamicLineGraph"
{
    Properties
    {
        [PerRendererData] _MainTex ("Data Texture", 2D) = "white" {}

        _LineColor ("Line Color", Color) = (0, 1, 0, 1)
        _BackgroundColor ("Background Color", Color) = (0.03, 0.03, 0.03, 1)

        _LineWidth ("Line Width", Range(0.0005, 0.05)) = 0.006
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]

        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            fixed4 _LineColor;
            fixed4 _BackgroundColor;
            float _LineWidth;

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

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // テクスチャの横幅＝データ数
                float pointCount = _MainTex_TexelSize.z;

                // 現在の横位置が何番目のデータ間にあるか
                float samplePosition =
                    saturate(i.uv.x) * (pointCount - 1.0);

                float index0 = floor(samplePosition);
                float index1 = min(index0 + 1.0, pointCount - 1.0);

                float interpolation =
                    frac(samplePosition);

                // 各データのテクスチャ上のX座標
                float textureX0 =
                    (index0 + 0.5) / pointCount;

                float textureX1 =
                    (index1 + 0.5) / pointCount;

                // Rチャンネルに保存された値を取得
                float value0 =
                    tex2D(_MainTex, float2(textureX0, 0.5)).r;

                float value1 =
                    tex2D(_MainTex, float2(textureX1, 0.5)).r;

                // 点と点の間を線形補間
                float lineY =
                    lerp(value0, value1, interpolation);

                // 現在のピクセルと線との距離
                float distanceFromLine =
                    abs(i.uv.y - lineY);

                // アンチエイリアス
                float antialiasWidth =
                    fwidth(distanceFromLine);

                float lineAmount =
                    1.0 - smoothstep(
                        _LineWidth,
                        _LineWidth + antialiasWidth,
                        distanceFromLine
                    );

                fixed4 result =
                    lerp(
                        _BackgroundColor,
                        _LineColor,
                        lineAmount
                    );

                result *= i.color;

                return result;
            }

            ENDCG
        }
    }
}