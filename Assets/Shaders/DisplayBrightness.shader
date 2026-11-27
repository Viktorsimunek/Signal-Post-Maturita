Shader "Signal Post/Display Brightness"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _Gain ("Brightness Gain", Range(0.5, 2)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend DstColor SrcColor
        ColorMask RGB
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float _Gain;
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(float4 positionOS : POSITION)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(positionOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                return half4(_Gain * 0.5h, _Gain * 0.5h, _Gain * 0.5h, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
