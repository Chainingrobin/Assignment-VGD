// Molten lava: two churning flow layers, drifting crust plates with glowing seams, crust gathering at banks.
// UV0 = surface coordinates in flow space; UV1.x = 0 at the flow centre, 1 at its banks.
Shader "Game/Lava"
{
    Properties
    {
        _LavaMap("Molten", 2D) = "white" {}
        _CrustMap("Crust", 2D) = "black" {}
        _NoiseMap("Crust Cells", 2D) = "gray" {}
        [HDR] _HotColor("Hot Multiplier", Color) = (2.2, 1.4, 1, 1)
        [HDR] _CoolColor("Cool Multiplier", Color) = (1.1, 0.45, 0.3, 1)
        _CrustTint("Crust Tint", Color) = (0.32, 0.27, 0.25, 1)
        _Tiling("Tiling (XY, stretch Y for falls)", Vector) = (1, 1, 0, 0)
        _CrustScale("Crust Plate Scale", Float) = 0.2
        _Flow("Flow (UV per second)", Vector) = (0, 0.1, 0, 0)
        _CrustCoverage("Crust Coverage", Range(0, 1)) = 0.4
        _EdgeCrust("Bank Crust", Range(0, 1.5)) = 0.6
        _SeamGlow("Seam Glow", Float) = 1.5
        _Wave("Wave Height", Float) = 0.03
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _LavaMap_ST, _CrustMap_ST, _NoiseMap_ST;
            half4 _HotColor, _CoolColor, _CrustTint;
            float _CrustScale, _CrustCoverage, _EdgeCrust, _SeamGlow, _Wave;
            float4 _Flow, _Tiling;
        CBUFFER_END

        float3 Undulate(float3 positionWS, float3 normalWS)
        {
            float wave = sin(_Time.y * 1.7 + positionWS.x * 0.6 + positionWS.z * 0.45) + 0.5 * sin(_Time.y * 2.9 - positionWS.z * 1.1);
            return positionWS + normalWS * wave * _Wave;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_LavaMap); SAMPLER(sampler_LavaMap);
            TEXTURE2D(_CrustMap);
            TEXTURE2D(_NoiseMap);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float2 edge : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float edge : TEXCOORD1; float3 normalWS : TEXCOORD2; float3 positionWS : TEXCOORD3; float fogCoord : TEXCOORD4; };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = Undulate(TransformObjectToWorld(input.positionOS.xyz), output.normalWS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv * _Tiling.xy;
                output.edge = input.edge.x;
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 flow = _Flow.xy * _Time.y;
                // Two scales moving at different speeds read as churning rather than a sliding texture.
                half3 a = SAMPLE_TEXTURE2D(_LavaMap, sampler_LavaMap, input.uv - flow).rgb;
                half3 b = SAMPLE_TEXTURE2D(_LavaMap, sampler_LavaMap, input.uv * 1.31 - flow * 1.45 + float2(0.37, 0.61)).rgb;
                half heat = saturate(dot(a + b, half3(0.3, 0.25, 0.05)));
                half pulse = 1 + 0.12 * sin(_Time.y * 1.3 + input.positionWS.x * 0.15 + input.positionWS.z * 0.11);
                // The texture's own swirls carry the detail; hot spots are pushed brighter than cooler veins.
                half3 molten = (a + b) * 0.5 * lerp(_CoolColor.rgb, _HotColor.rgb, smoothstep(0.3, 0.9, heat)) * pulse;

                // Crust islands ride the flow slightly slower than the molten surface.
                float cells = SAMPLE_TEXTURE2D(_NoiseMap, sampler_LavaMap, (input.uv - flow * 0.9) * _CrustScale).r;
                float field = cells - input.edge * _EdgeCrust;
                float crust = 1 - smoothstep(_CrustCoverage - 0.12, _CrustCoverage + 0.05, field);
                float seam = saturate(1 - abs(field - _CrustCoverage + 0.03) / 0.07) * (1 - crust * 0.6);

                float3 normalWS = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 crustAlbedo = SAMPLE_TEXTURE2D(_CrustMap, sampler_LavaMap, (input.uv - flow * 0.9) * 0.6).rgb * _CrustTint.rgb;
                half3 crustLit = crustAlbedo * (light.color * light.shadowAttenuation * saturate(dot(normalWS, light.direction)) + SampleSH(normalWS))
                    + crustAlbedo * molten * 0.12;

                half3 color = lerp(molten, crustLit, crust) + molten * seam * _SeamGlow * 0.4;
                color = MixFog(color, input.fogCoord);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 DepthVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformWorldToHClip(Undulate(TransformObjectToWorld(input.positionOS.xyz), TransformObjectToWorldNormal(input.normalOS)));
            }
            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
