// Terrain renders this dependency into its distant tree atlas.
// No camera-dependent normals, wind, shadows or fog should be baked into it.
Shader "Hidden/SignalPost/Tree Bark Billboard Bake"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.33
        _AmbientStrength("Ambient Strength", Float) = 0.45
        _DirectLightStrength("Direct Light Strength", Float) = 1
        _BackLightStrength("Back Light Strength", Float) = 0.35
        _MinNdotL("Minimum Light", Float) = 0.2
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
        Cull Off
        ZWrite On
        ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            fixed4 _BaseColor;
            float _Cutoff;
            float _AmbientStrength, _DirectLightStrength, _BackLightStrength, _MinNdotL;
            float3 _TerrainTreeLightDirections[4];
            float4 _TerrainTreeLightColors[4];
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 lighting : TEXCOORD1;
            };
            Varyings vert(appdata_base input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                half3 light = UNITY_LIGHTMODEL_AMBIENT.rgb;
                for (int i=0; i<4; i++)
                    light += _TerrainTreeLightColors[i].rgb *
                        max(0.2, abs(dot(UnityObjectToWorldNormal(input.normal), _TerrainTreeLightDirections[i])));
                output.lighting = max(light, half3(0.18, 0.18, 0.18));
                return output;
            }
            fixed4 frag(Varyings input) : SV_Target
            {
                fixed4 sample = tex2D(_BaseMap, input.uv) * _BaseColor;
                return fixed4(sample.rgb * input.lighting, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
