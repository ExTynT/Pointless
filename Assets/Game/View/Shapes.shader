Shader "Pointless/Shapes"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Attributes
            {
                float3 position : POSITION;
                half4 color : COLOR;
                float4 shape : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                half4 color : COLOR;
                float4 shape : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = TransformObjectToHClip(input.position);
                output.color = input.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                output.color.rgb = SRGBToLinear(input.color.rgb);
                #endif
                output.shape = input.shape;
                return output;
            }

            float Triangle(float2 p, float corner)
            {
                const float k = 1.7320508;
                float r = 1.0 - corner * k;
                p.x = abs(p.x) - r;
                p.y = p.y + r / k;
                if (p.x + k * p.y > 0.0) p = float2(p.x - k * p.y, -k * p.x - p.y) * 0.5;
                p.x -= clamp(p.x, -2.0 * r, 0.0);
                return -length(p) * sign(p.y) - corner;
            }

            float Capsule(float2 p, float reach)
            {
                p.x = max(abs(p.x) - reach, 0.0);
                return length(p) - 1.0;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float kind = input.shape.z;
                float soft = input.shape.w;
                float edge = kind >= 0.0 ? Triangle(input.shape.xy, kind) : Capsule(input.shape.xy, -kind - 1.0);
                float crisp = saturate(0.5 - edge / max(fwidth(edge), 1e-5));
                float haze = saturate(1.0 - edge / max(soft, 1e-5));
                float cover = soft > 0.0 ? haze * haze : crisp;
                return half4(input.color.rgb, input.color.a * cover);
            }
            ENDHLSL
        }
    }
}
