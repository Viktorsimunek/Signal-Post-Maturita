// Terrain renders this dependency into its distant tree atlas.
// No camera-dependent normals, wind, shadows or fog should be baked into it.
Shader "Hidden/SignalPost/Tree Leaves Billboard Bake"
{
    Properties
    {
        _LeafSaturation("Leaf Color Saturation", Range(0, 1)) = 0.55

        [Header(Crown Depth)]
        _CrownShadeStrength("Crown Center Darkening", Range(0, 0.8)) = 0.32
        _CrownInnerTint("Crown Inner Tint", Color) = (0.92, 0.98, 0.9, 1)
        [HideInInspector] _CrownShape("Crown Radius Base InvHeight Taper", Vector) = (3, 0, 0.1, 0.45)

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
            half _LeafSaturation;
            half _CrownShadeStrength;
            half4 _CrownInnerTint;
            float4 _CrownShape;
            #include "Assets/Shaders/TreeCrownShading.hlsl"
            float _Cutoff;
            float _AmbientStrength, _DirectLightStrength, _BackLightStrength, _MinNdotL;
            float3 _TerrainTreeLightDirections[4];
            float4 _TerrainTreeLightColors[4];
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 lighting : TEXCOORD1;
                float3 crownPositionOS : TEXCOORD2;
            };
            Varyings vert(appdata_base input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.crownPositionOS = input.vertex.xyz;
                half3 light = UNITY_LIGHTMODEL_AMBIENT.rgb * _AmbientStrength;
                for (int i=0; i<4; i++)
                    light += _TerrainTreeLightColors[i].rgb *
                        (max(abs(_TerrainTreeLightDirections[i].y), _MinNdotL) * _DirectLightStrength + _BackLightStrength);
                output.lighting = max(light, (_AmbientStrength * 0.35).xxx);
                return output;
            }
            fixed4 frag(Varyings input) : SV_Target
            {
                fixed4 sample = tex2D(_BaseMap, input.uv) * _BaseColor;
                float coverage = saturate((sample.a - _Cutoff) /
                    max(fwidth(sample.a), 1.0 / 255.0) + 0.5);
                clip(coverage - 0.001);
                return fixed4(MuteTreeLeafColor(sample.rgb) * input.lighting *
                    GetTreeCrownTint(input.crownPositionOS), coverage);
            }
            ENDCG
        }
    }
    Fallback Off
}
