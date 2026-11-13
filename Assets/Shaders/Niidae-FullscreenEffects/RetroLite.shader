Shader "Hidden/SignalPost/RetroLite"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "Retro Lite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _RetroPixel;      // intensity, block size, width, height
            float4 _RetroPoster;     // intensity, colors, offset, dither strength
            float4 _RetroScan;       // dither scale, scan strength, frequency, dispersion
            float4 _RetroGrade;      // contrast, saturation, vignette, grain
            float _RetroTime;

            half Bayer(uint2 p)
            {
                uint x=p.x&3, y=p.y&3;
                // Same 4x4 ordered pattern, without a texture lookup.
                uint low=(((x&1)^(y&1))<<1)+(y&1);
                uint high=((((x>>1)&1)^((y>>1)&1))<<1)+((y>>1)&1);
                return (half)(low*4+high);
            }

            half3 Grade(half3 color, float2 uv)
            {
                // HDR-safe: neutral parameters do not clip emissive highlights.
                half luma=dot(color,half3(.2126h,.7152h,.0722h));
                color=lerp(luma.xxx,color,(half)_RetroGrade.y);
                color=max((color-.18h)*(half)_RetroGrade.x+.18h,0.0h);
                float2 edge=uv*2.0-1.0;
                color*=1.0h-(half)_RetroGrade.z*saturate(dot(edge,edge)*.5);
                // Tiny, luminance-weighted grain; no extra texture, sine or blur pass.
                float2 cell=floor(uv*_RetroPixel.zw);
                float noise=frac(52.9829189*frac(dot(cell,float2(.06711056,.00583715))+floor(_RetroTime*20)*.017));
                color+=(half)((noise-.5)*_RetroGrade.w)*saturate(luma+.1h);
                return max(color,0.0h);
            }

            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;
                float2 size=max(_RetroPixel.zw,1.0);
                float2 blockUv=(round(uv*size/max(_RetroPixel.y,1.0))*max(_RetroPixel.y,1.0)+.5)/size;
                half4 color=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
                UNITY_BRANCH
                if(_RetroPixel.x>.0001)
                {
                    half4 blocked=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,blockUv);
                    color=lerp(color,blocked,saturate(_RetroPixel.x));
                }
                float count=max(_RetroPoster.y,2.0);
                uint2 cell=(uint2)round(uv*size/max(_RetroScan.x,1.0));
                half dither=Bayer(cell)*(half)(_RetroPoster.w/count);
                half3 quantized=floor(color.rgb*count+_RetroPoster.z+dither)/count;
                color.rgb=lerp(color.rgb,quantized,saturate(_RetroPoster.x));
                float phase=uv.y*_RetroScan.z;
                half3 scan=1.0h-half3(sin(phase+_RetroScan.w*1.571),sin(phase),sin(phase-_RetroScan.w*1.571))*(_RetroScan.y*_RetroScan.y);
                color.rgb=Grade(color.rgb*scan,uv);
                return color;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
