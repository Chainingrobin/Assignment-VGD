Shader "Game/Moon"
{
    Properties { _Color ("Moon Color", Color) = (0.85, 0.9, 1, 1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="TransparentCutout" }
        Pass
        {
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float radius = length(p);
                clip(1 - radius);
                float craters = 0.12 * (1 - smoothstep(0.18, 0.3, length(p - float2(-0.3, 0.25))))
                    + 0.1 * (1 - smoothstep(0.1, 0.22, length(p - float2(0.35, -0.25))))
                    + 0.08 * (1 - smoothstep(0.12, 0.25, length(p - float2(-0.1, -0.4))));
                return half4(_Color.rgb * (1 - craters - 0.15 * radius * radius), 1);
            }
            ENDHLSL
        }
    }
}
