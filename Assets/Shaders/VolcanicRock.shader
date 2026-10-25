// World-space triplanar rock for generated volcano meshes: no UV stretching on cones, walls or tunnels.
// Steep faces use the side set, upward faces the top set; both carry glowing emissive cracks.
Shader "Game/VolcanicRock"
{
    Properties
    {
        _BaseMap("Side Albedo", 2D) = "white" {}
        _BumpMap("Side Normal", 2D) = "bump" {}
        _EmissionMap("Side Emission", 2D) = "black" {}
        _TopMap("Top Albedo", 2D) = "white" {}
        _TopBumpMap("Top Normal", 2D) = "bump" {}
        _TopEmissionMap("Top Emission", 2D) = "black" {}
        _BaseColor("Side Tint", Color) = (1, 1, 1, 1)
        _TopColor("Top Tint", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Side Emission", Color) = (0, 0, 0, 1)
        [HDR] _TopEmissionColor("Top Emission", Color) = (0, 0, 0, 1)
        _Tiling("Side Tiling (per metre)", Float) = 0.08
        _TopTiling("Top Tiling (per metre)", Float) = 0.12
        _TopBlend("Top Coverage", Range(0, 1)) = 0.35
        _TopSharpness("Top Sharpness", Range(1, 16)) = 6
        _BumpScale("Normal Strength", Float) = 1
        _Smoothness("Smoothness", Range(0, 1)) = 0.15
        _EmissionPulse("Emission Pulse", Range(0, 1)) = 0.15
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BumpMap_ST, _EmissionMap_ST, _TopMap_ST, _TopBumpMap_ST, _TopEmissionMap_ST;
            half4 _BaseColor, _TopColor, _EmissionColor, _TopEmissionColor;
            float _Tiling, _TopTiling, _TopBlend, _TopSharpness, _BumpScale, _Smoothness, _EmissionPulse, _Cull;
        CBUFFER_END
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); TEXTURE2D(_EmissionMap);
            TEXTURE2D(_TopMap); TEXTURE2D(_TopBumpMap); TEXTURE2D(_TopEmissionMap);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float fogCoord : TEXCOORD2; };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Whiteout-blended triplanar normal (Golus), returned in world space.
            float3 TriplanarNormal(TEXTURE2D_PARAM(map, samplerMap), float3 p, float3 n, float3 w, float tiling)
            {
                half3 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(map, samplerMap, p.zy * tiling), _BumpScale);
                half3 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(map, samplerMap, p.xz * tiling), _BumpScale);
                half3 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(map, samplerMap, p.xy * tiling), _BumpScale);
                tx = half3(tx.xy + n.zy, abs(tx.z) * n.x);
                ty = half3(ty.xy + n.xz, abs(ty.z) * n.y);
                tz = half3(tz.xy + n.xy, abs(tz.z) * n.z);
                return normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);
            }

            half3 Triplanar(TEXTURE2D_PARAM(map, samplerMap), float3 p, float3 w, float tiling)
            {
                return SAMPLE_TEXTURE2D(map, samplerMap, p.zy * tiling).rgb * w.x
                     + SAMPLE_TEXTURE2D(map, samplerMap, p.xz * tiling).rgb * w.y
                     + SAMPLE_TEXTURE2D(map, samplerMap, p.xy * tiling).rgb * w.z;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 w = pow(abs(n), 4);
                w /= dot(w, 1);
                float3 p = input.positionWS;
                float top = saturate((n.y - (1 - _TopBlend)) * _TopSharpness);

                half3 albedo = lerp(Triplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), p, w, _Tiling) * _BaseColor.rgb,
                                    SAMPLE_TEXTURE2D(_TopMap, sampler_BaseMap, p.xz * _TopTiling).rgb * _TopColor.rgb, top);
                float3 normalWS = normalize(lerp(TriplanarNormal(TEXTURE2D_ARGS(_BumpMap, sampler_BaseMap), p, n, w, _Tiling),
                                                 TriplanarNormal(TEXTURE2D_ARGS(_TopBumpMap, sampler_BaseMap), p, n, w, _TopTiling), top));
                half pulse = 1 + _EmissionPulse * sin(_Time.y * 1.3 + p.x * 0.05 + p.z * 0.07);
                half3 emission = lerp(Triplanar(TEXTURE2D_ARGS(_EmissionMap, sampler_BaseMap), p, w, _Tiling) * _EmissionColor.rgb,
                                      SAMPLE_TEXTURE2D(_TopEmissionMap, sampler_BaseMap, p.xz * _TopTiling).rgb * _TopEmissionColor.rgb, top) * pulse;

                InputData data = (InputData)0;
                data.positionWS = p;
                data.positionCS = input.positionCS;
                data.normalWS = normalWS;
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(p);
                data.shadowCoord = TransformWorldToShadowCoord(p);
                data.fogCoord = input.fogCoord;
                data.bakedGI = SampleSH(normalWS);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.smoothness = _Smoothness;
                surface.occlusion = 1;
                surface.emission = emission;
                surface.alpha = 1;
                surface.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(data, surface);
                color.rgb = MixFog(color.rgb, data.fogCoord);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 direction = normalize(_LightPosition - p);
                #else
                    float3 direction = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(p, n, direction));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }
            half ShadowFrag() : SV_Target { return 0; }
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
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 DepthVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }
            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
