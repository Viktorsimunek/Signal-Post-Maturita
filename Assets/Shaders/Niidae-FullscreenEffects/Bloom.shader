Shader "Hidden/Custom/URP/BloomVolume"
{
    Properties
    {
        _MainTex ("MainTex", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Prefilter"
            HLSLPROGRAM
            #pragma vertex VertFullscreenTriangle
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            Varyings VertFullscreenTriangle(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.texcoord = uv;
                return output;
            }

            TEXTURE2D(_BlitTexture);
            float4 _BlitTexture_TexelSize;

            float4 _Params; // x threshold, y knee, z unused, w unused

            float3 Prefilter(float3 color)
            {
                // Unity-style soft knee threshold.
                float brightness = Max3(color.r, color.g, color.b);
                float threshold = _Params.x;
                float knee = max(1e-5, _Params.y * threshold);

                float soft = brightness - threshold + knee;
                soft = clamp(soft, 0.0, 2.0 * knee);
                soft = (soft * soft) / (4.0 * knee + 1e-5);

                float contrib = max(brightness - threshold, soft);
                contrib /= max(brightness, 1e-5);
                return color * contrib;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                // Multi-tap prefilter reduces point-sparkle and diffusion artifacts on tiny highlights.
                float2 texel = _BlitTexture_TexelSize.xy;
                float3 c = 0.0;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb * 0.4;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + texel * float2( 1.0,  1.0)).rgb * 0.15;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + texel * float2(-1.0,  1.0)).rgb * 0.15;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + texel * float2( 1.0, -1.0)).rgb * 0.15;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + texel * float2(-1.0, -1.0)).rgb * 0.15;

                return float4(Prefilter(c), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "BlurH"
            HLSLPROGRAM
            #pragma vertex VertFullscreenTriangle
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "niidae_helpers.cginc"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            Varyings VertFullscreenTriangle(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.texcoord = uv;
                return output;
            }

            TEXTURE2D(_BlitTexture);
            float4 _BlitTexture_TexelSize;

            float4 _BlurParams; // z radius scale, w rotation angle (radians)

            float3 SampleRoundBlur(float2 uv)
            {
                float2 r = _BlitTexture_TexelSize.xy * max(_BlurParams.z, 0.1);
                float a = _BlurParams.w;

                float2 d0 = Rotate2D(float2( 1.0,  0.0), a) * r;
                float2 d1 = Rotate2D(float2(-1.0,  0.0), a) * r;
                float2 d2 = Rotate2D(float2( 0.0,  1.0), a) * r;
                float2 d3 = Rotate2D(float2( 0.0, -1.0), a) * r;
                float2 d4 = Rotate2D(float2( 0.7071,  0.7071), a) * r;
                float2 d5 = Rotate2D(float2(-0.7071,  0.7071), a) * r;
                float2 d6 = Rotate2D(float2( 0.7071, -0.7071), a) * r;
                float2 d7 = Rotate2D(float2(-0.7071, -0.7071), a) * r;

                float3 c = 0;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb * 0.25;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d0).rgb * 0.09375;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d1).rgb * 0.09375;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d2).rgb * 0.09375;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d3).rgb * 0.09375;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d4).rgb * 0.09375;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d5).rgb * 0.09375;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d6).rgb * 0.09375;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d7).rgb * 0.09375;
                return c;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                return float4(SampleRoundBlur(uv), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "BlurV"
            HLSLPROGRAM
            #pragma vertex VertFullscreenTriangle
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "niidae_helpers.cginc"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            Varyings VertFullscreenTriangle(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.texcoord = uv;
                return output;
            }

            TEXTURE2D(_BlitTexture);
            float4 _BlitTexture_TexelSize;

            float4 _BlurParams; // z radius scale, w rotation angle (radians)

            float3 SampleRoundBlur(float2 uv)
            {
                float2 r = _BlitTexture_TexelSize.xy * max(_BlurParams.z, 0.1);
                float a = _BlurParams.w;

                float2 d0 = Rotate2D(float2( 1.0,  0.0), a) * r;
                float2 d1 = Rotate2D(float2(-1.0,  0.0), a) * r;
                float2 d2 = Rotate2D(float2( 0.0,  1.0), a) * r;
                float2 d3 = Rotate2D(float2( 0.0, -1.0), a) * r;
                float2 d4 = Rotate2D(float2( 0.7071,  0.7071), a) * r;
                float2 d5 = Rotate2D(float2(-0.7071,  0.7071), a) * r;
                float2 d6 = Rotate2D(float2( 0.7071, -0.7071), a) * r;
                float2 d7 = Rotate2D(float2(-0.7071, -0.7071), a) * r;

                float3 c = 0;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb * 0.25;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d0).rgb * 0.09375;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d1).rgb * 0.09375;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d2).rgb * 0.09375;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d3).rgb * 0.09375;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d4).rgb * 0.09375;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d5).rgb * 0.09375;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d6).rgb * 0.09375;
                c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + d7).rgb * 0.09375;
                return c;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                return float4(SampleRoundBlur(uv), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex VertFullscreenTriangle
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            Varyings VertFullscreenTriangle(Attributes input)
            {
                Varyings output;
                float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                output.texcoord = uv;
                return output;
            }

            TEXTURE2D(_BlitTexture);
            float4 _BlitTexture_TexelSize;

            TEXTURE2D(_BloomTex);
            TEXTURE2D(_BloomTex2);

            float4 _CompositeParams; // x intensity, y steps, z saturation, w gamma
            float4 _MultiScaleParams; // x half weight, y quarter weight
            float4 _ReservedParams; // x gamma2 (reserved, currently unused)

            float4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                float2 uvSrc = uv;
                uvSrc.y = 1.0 - uvSrc.y;
                float3 src = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uvSrc).rgb;
                float3 bloomHalf = SAMPLE_TEXTURE2D(_BloomTex, sampler_LinearClamp, uv).rgb;
                float3 bloomQuarter = SAMPLE_TEXTURE2D(_BloomTex2, sampler_LinearClamp, uv).rgb;
                float3 bloom = bloomHalf * _MultiScaleParams.x + bloomQuarter * _MultiScaleParams.y;
                bloom = pow(lerp(dot(bloom, float3(0.2126, 0.7152, 0.0722)), bloom, _CompositeParams.z), _CompositeParams.w); // saturation and gamma

                // quantize when low counts
                if (_CompositeParams.y < 255.0)
                    bloom = round(bloom * _CompositeParams.y) / _CompositeParams.y;
                
                bloom *= pow(bloom, _ReservedParams.x);
                //return float4(bloom, 1);
                return float4(src + bloom * _CompositeParams.x, 1);
            }
            ENDHLSL
        }
    }
}
