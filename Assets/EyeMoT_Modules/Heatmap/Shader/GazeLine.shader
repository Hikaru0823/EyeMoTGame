Shader "Custom/GazeLine"
{
    Properties
    {
        _MainTex ("Previous Lines", 2D) = "black" {}
        _StartUV ("Start UV", Vector) = (0.5, 0.5, 0, 0)
        _EndUV ("End UV", Vector) = (0.5, 0.5, 0, 0)
        _LineWidth ("Line Width", Float) = 0.003
        _Aspect ("Aspect", Float) = 1.0
        _LineColor ("Line Color", Color) = (1, 1, 1, 1)
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
            float4 _StartUV;
            float4 _EndUV;
            float _LineWidth;
            float _Aspect;
            float4 _LineColor;

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

            float DistanceToSegment(float2 sampleUV, float2 startUV, float2 endUV)
            {
                float2 aspectScale = float2(_Aspect, 1.0);
                float2 pointFromStart = (sampleUV - startUV) * aspectScale;
                float2 segment = (endUV - startUV) * aspectScale;
                float segmentLengthSquared = dot(segment, segment);
                float positionOnSegment = saturate(
                    dot(pointFromStart, segment) /
                    max(segmentLengthSquared, 0.000001)
                );

                return length(pointFromStart - segment * positionOnSegment);
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 previousColor = tex2D(_MainTex, i.uv);
                float distanceToLine =
                    DistanceToSegment(i.uv, _StartUV.xy, _EndUV.xy);
                float width = max(_LineWidth, 0.0001);
                float lineMask =
                    1.0 - smoothstep(width, width * 1.5, distanceToLine);
                float lineAlpha = lineMask * saturate(_LineColor.a);

                float3 color =
                    lerp(previousColor.rgb, _LineColor.rgb, lineAlpha);
                float alpha = max(previousColor.a, lineAlpha);
                return float4(color, alpha);
            }
            ENDCG
        }
    }
}
