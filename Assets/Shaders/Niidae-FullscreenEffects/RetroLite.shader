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
            float4 _RetroGhost;      // strength, horizontal offset
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
                // Ease extra retro contrast out of the shadows, instead of clamping them black.
                half contrast=lerp(1.0h,(half)_RetroGrade.x,smoothstep(0.0h,.18h,max(luma,0.0h)));
                color=max((color-.18h)*contrast+.18h,0.0h);
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
                half signal=max(color.r,max(color.g,color.b));
                half shadowProtection=smoothstep(0.0h,3.0/count,signal);
                color.rgb=lerp(color.rgb,max(quantized,0.0h),saturate(_RetroPoster.x)*shadowProtection);
                // One shifted sample, fused into this pass instead of another fullscreen pass.
                UNITY_BRANCH
                if(abs(_RetroGhost.x)>.0001)
                {
                    half3 ghost=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(-_RetroGhost.y,0)).rgb;
                    // Match the base palette before combining, so dark details remain consistent.
                    half3 ghostPoster=floor(ghost*count+_RetroPoster.z+dither)/count;
                    half ghostSignal=max(ghost.r,max(ghost.g,ghost.b));
                    ghost=lerp(ghost,max(ghostPoster,0.0h),saturate(_RetroPoster.x)*smoothstep(0.0h,3.0/count,ghostSignal));
                    // A negative, unblurred shifted copy acts as a harsh sharpening filter.
                    // Keep the same flat-field brightness, but only a gentle shifted trail.
                    // The floor limits subtraction relative to each channel, preserving dark detail.
                    if (_RetroGhost.x < 0.0)
                    {
                        ghost = lerp(color.rgb, ghost, 0.15h);
                        color.rgb = max(color.rgb + ghost * _RetroGhost.x,
                            color.rgb * max(1.0h + (half)_RetroGhost.x, 0.0h) * 0.5h);
                    }
                    else
                        color.rgb += ghost * _RetroGhost.x;
                }
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
