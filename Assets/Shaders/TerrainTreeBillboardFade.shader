// Distance-faded runtime billboards for Terrain trees.
Shader "Hidden/TerrainEngine/BillboardTree"
{
    Properties
    {
        _MainTex("Tree Atlas", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent-100" "IgnoreProjector"="True" "RenderType"="TreeBillboard" }
        Pass
        {
            ColorMask RGB
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex BillboardVertex
            #pragma fragment BillboardFragment
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "TerrainEngine.cginc"
            Texture2D _MainTex;
            // Inline sampler keeps fractional mip transitions smooth on the generated atlas.
            SamplerState sampler_TreeAtlas_trilinear_clamp;
            struct BillboardVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 tint : TEXCOORD1;
                half distanceFade : TEXCOORD2;
                half haze : TEXCOORD3;
                UNITY_FOG_COORDS(4)
                UNITY_VERTEX_OUTPUT_STEREO
            };
            BillboardVaryings BillboardVertex(appdata_tree_billboard input)
            {
                BillboardVaryings output = (BillboardVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // Measure at the tree root, so an entire tree fades uniformly.
                float3 rootWS = mul(unity_ObjectToWorld, input.vertex).xyz;
                float rootDistance = distance(rootWS, _WorldSpaceCameraPos);
                float treeVariation = frac(sin(dot(rootWS.xz, float2(12.9898, 78.233))) * 43758.5453);
                float fadeEnd = lerp(550.0, 620.0, treeVariation);
                output.distanceFade = 1.0 - smoothstep(260.0, fadeEnd, rootDistance);
                output.haze = smoothstep(180.0, 620.0, rootDistance) * 0.45;
                TerrainBillboardTree(input.vertex, input.texcoord1.xy, input.texcoord.y);
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.uv = float2(input.texcoord.x, input.texcoord.y > 0.0);
                output.tint = input.color;
                UNITY_TRANSFER_FOG(output, output.positionCS);
                return output;
            }
            half4 BillboardFragment(BillboardVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 tree = _MainTex.Sample(sampler_TreeAtlas_trilinear_clamp, input.uv);
                tree.rgb *= input.tint.rgb;
                clip(tree.a - 0.00001h);
                tree.a *= input.distanceFade;
                // Reduce distant silhouette contrast before the final smooth fade.
                tree.rgb = lerp(tree.rgb, unity_FogColor.rgb, input.haze);
                UNITY_APPLY_FOG(input.fogCoord, tree);
                return tree;
            }
            ENDCG
        }
    }
    Fallback Off
}
