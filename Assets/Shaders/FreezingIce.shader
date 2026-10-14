Shader "Game/FreezingIce"
{
    Properties
    {
        _BaseMap("Ice Texture", 2D) = "white" {}
        _BaseColor("Ice Color", Color) = (1,1,1,1)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Float) = 1
        _ParallaxMap("Height Map", 2D) = "black" {}
        _Parallax("Height Strength", Range(0.005,0.08)) = 0.005
        _MetallicGlossMap("Metallic Map", 2D) = "white" {}
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.6
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0,1)) = 1
        _EmissionMap("Emission Map", 2D) = "white" {}
        _EmissionColor("Emission", Color) = (0,0,0,0)
        _FreezeProgress("Freeze Progress", Range(0,1)) = 1
        _FreezeOrigin("Freeze Origin UV", Vector) = (0.5,0.5,0,0)
        _FreezeSize("Water Size", Vector) = (548,259,0,0)
        _EdgeSoftness("Edge Softness", Range(0.005,0.1)) = 0.025
        [HideInInspector] _Surface("Surface", Float) = 1
        [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
        [HideInInspector] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment FreezeFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _METALLICSPECGLOSSMAP
            #pragma shader_feature_local _OCCLUSIONMAP
            #pragma shader_feature_local _EMISSION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            float _FreezeProgress;
            float4 _FreezeOrigin, _FreezeSize;
            float _EdgeSoftness;

            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float4 n = frac(sin(float4(dot(cell, float2(127.1,311.7)),
                    dot(cell + float2(1,0), float2(127.1,311.7)),
                    dot(cell + float2(0,1), float2(127.1,311.7)),
                    dot(cell + 1, float2(127.1,311.7)))) * 43758.5453);
                return lerp(lerp(n.x, n.y, f.x), lerp(n.z, n.w, f.x), f.y);
            }

            half4 FreezeFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Unity's Plane UVs run opposite its local X/Z axes.
                float2 uv = 1 - (input.uv - _BaseMap_ST.zw) / _BaseMap_ST.xy;
                float2 size = max(_FreezeSize.xy, 0.001);
                float2 p = (uv - _FreezeOrigin.xy) * size;
                float irregularity = (Noise(p * 0.025) - 0.5) * 0.12
                    + (Noise(p * 0.12) - 0.5) * 0.04;
                float field = saturate(1 - uv.x + irregularity * (4 * uv.x * (1 - uv.x)));
                float edge = lerp(-_EdgeSoftness, 1 + _EdgeSoftness, _FreezeProgress) - field;
                float coverage = smoothstep(-_EdgeSoftness, _EdgeSoftness, edge);
                clip(coverage - 0.001);
                #if defined(_PARALLAXMAP)
                #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
                    half3 viewDirTS = input.viewDirTS;
                #else
                    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
                #endif
                ApplyPerPixelDisplacement(viewDirTS, input.uv);
                #endif
                SurfaceData surface;
                InitializeStandardLitSurfaceData(input.uv, surface);
                surface.alpha *= coverage;
                surface.albedo = lerp(surface.albedo, half3(0.8,0.9,1), (1 - smoothstep(0, 0.05, edge)) * 0.3);
                float activeFront = step(0.001, _FreezeProgress) * (1 - step(0.999, _FreezeProgress));
                float rim = (1 - smoothstep(0, 0.045, abs(edge))) * activeFront;
                surface.emission += half3(0.12, 0.38, 0.65) + half3(1.2, 3.2, 5.0) * rim;
                InputData lighting;
                InitializeInputData(input, surface.normalTS, lighting);
                InitializeBakedGIData(input, lighting);
                half4 color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, lighting.fogCoord);
                color.a = coverage * _BaseColor.a;
                return color;
            }
            ENDHLSL
        }
    }
}
