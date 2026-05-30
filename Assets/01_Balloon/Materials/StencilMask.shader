Shader "Custom/StencilMask"
{
    SubShader
    {
        Tags
        {
            "Queue"="Geometry-10"
            "RenderType"="Opaque"
        }

        Pass
        {
            ColorMask 0
            ZWrite Off

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }
        }
    }
}