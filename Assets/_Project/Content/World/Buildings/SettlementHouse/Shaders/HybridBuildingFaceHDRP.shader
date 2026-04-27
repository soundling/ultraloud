Shader "Ultraloud/Buildings/Hybrid Building Face HDRP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _HeightMap("Height Map", 2D) = "gray" {}
        _PackedMasks("Packed Masks", 2D) = "white" {}
        _EmissionMap("Emission Map", 2D) = "black" {}

        [Header(Cutout)]
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.08

        [Header(Material)]
        _NormalScale("Normal Strength", Range(0.0, 3.0)) = 1.15
        _HeightContrast("Height Contrast", Range(0.0, 2.0)) = 0.55
        _AoStrength("AO Strength", Range(0.0, 2.0)) = 0.88
        _RoughnessScale("Roughness Scale", Range(0.0, 2.0)) = 1.0
        _SpecularStrength("Specular Strength", Range(0.0, 2.0)) = 0.13
        _EdgeWearBrightness("Edge Wear Brightness", Range(0.0, 2.0)) = 0.34
        _CrackDarkening("Crack Darkening", Range(0.0, 2.0)) = 0.82
        _EmissionStrength("Emission Strength", Range(0.0, 6.0)) = 1.65

        [Header(Ambient)]
        _AmbientTopColor("Ambient Top Color", Color) = (0.58, 0.62, 0.64, 1)
        _AmbientBottomColor("Ambient Bottom Color", Color) = (0.13, 0.12, 0.10, 1)
        _AmbientIntensity("Ambient Intensity", Range(0.0, 4.0)) = 1.0

        [HideInInspector] _UseNormalMap("Use Normal Map", Float) = 0
        [HideInInspector] _UseHeightMap("Use Height Map", Float) = 0
        [HideInInspector] _UsePackedMasks("Use Packed Masks", Float) = 0
        [HideInInspector] _UseEmissionMap("Use Emission Map", Float) = 0
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
    #pragma multi_compile_instancing
    #pragma multi_compile _ DOTS_INSTANCING_ON

    #define _ALPHATEST_ON
    #define ATTRIBUTES_NEED_NORMAL
    #define ATTRIBUTES_NEED_TANGENT
    #define ATTRIBUTES_NEED_TEXCOORD0
    #define VARYINGS_NEED_POSITION_WS
    #define VARYINGS_NEED_TANGENT_TO_WORLD
    #define VARYINGS_NEED_TEXCOORD0

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/VaryingMesh.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/Unlit.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl"

    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);
    TEXTURE2D(_NormalMap);
    SAMPLER(sampler_NormalMap);
    TEXTURE2D(_HeightMap);
    SAMPLER(sampler_HeightMap);
    TEXTURE2D(_PackedMasks);
    SAMPLER(sampler_PackedMasks);
    TEXTURE2D(_EmissionMap);
    SAMPLER(sampler_EmissionMap);

    CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _AmbientTopColor;
    float4 _AmbientBottomColor;
    float4 _BaseMap_ST;
    float4 _NormalMap_ST;
    float4 _HeightMap_ST;
    float4 _PackedMasks_ST;
    float4 _EmissionMap_ST;
    float _AlphaCutoff;
    float _NormalScale;
    float _HeightContrast;
    float _AoStrength;
    float _RoughnessScale;
    float _SpecularStrength;
    float _EdgeWearBrightness;
    float _CrackDarkening;
    float _EmissionStrength;
    float _AmbientIntensity;
    float _UseNormalMap;
    float _UseHeightMap;
    float _UsePackedMasks;
    float _UseEmissionMap;
    CBUFFER_END

    float _ManualLightCount;
    float4 _ManualLightPositionWS[4];
    float4 _ManualLightDirectionWS[4];
    float4 _ManualLightColor[4];
    float4 _ManualLightData0[4];
    float4 _ManualLightData1[4];

    float3 UnpackBuildingNormal(float4 packedNormal)
    {
        float3 normalTS = packedNormal.xyz * 2.0 - 1.0;
        normalTS.xy *= _NormalScale;
        normalTS.z = sqrt(saturate(1.0 - dot(normalTS.xy, normalTS.xy)));
        return normalize(normalTS);
    }

    float3 BuildNormalTS(float2 uv)
    {
        float heightValue = _UseHeightMap > 0.5
            ? SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, TRANSFORM_TEX(uv, _HeightMap)).r
            : 0.5;
        float2 macroSlope = (uv - 0.5) * _HeightContrast * (heightValue - 0.5) * 0.35;
        float3 macroNormal = normalize(float3(macroSlope.x, macroSlope.y, 1.0));
        if (_UseNormalMap < 0.5)
        {
            return macroNormal;
        }

        float3 detailNormal = UnpackBuildingNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(uv, _NormalMap)));
        float3 combined;
        combined.xy = macroNormal.xy + detailNormal.xy;
        combined.z = macroNormal.z * detailNormal.z;
        return normalize(combined);
    }

    float4 SamplePackedMasks(float2 uv)
    {
        if (_UsePackedMasks < 0.5)
        {
            return float4(1.0, 0.82, 0.0, 0.0);
        }

        return SAMPLE_TEXTURE2D(_PackedMasks, sampler_PackedMasks, TRANSFORM_TEX(uv, _PackedMasks));
    }

    float3 EvaluateLighting(float3 albedo, float3 normalWS, float3 viewWS, float3 hitPositionWS, float2 uv)
    {
        float4 masks = SamplePackedMasks(uv);
        float ao = lerp(1.0, masks.r, _AoStrength);
        float roughness = saturate(masks.g * _RoughnessScale);
        float edgeWear = masks.b;
        float cracks = masks.a;
        float heightValue = _UseHeightMap > 0.5
            ? SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, TRANSFORM_TEX(uv, _HeightMap)).r
            : 0.5;

        float reliefTone = saturate((heightValue - 0.5) * _HeightContrast + 0.5);
        albedo *= lerp(0.9, 1.08, reliefTone);
        albedo *= saturate(1.0 - cracks * _CrackDarkening * 0.36);
        albedo += edgeWear * _EdgeWearBrightness * 0.12;

        float verticalAmbient = saturate(normalWS.y * 0.5 + 0.5);
        float3 lighting = lerp(_AmbientBottomColor.rgb, _AmbientTopColor.rgb, verticalAmbient) * _AmbientIntensity * albedo * ao;
        float nDotV = saturate(dot(normalWS, viewWS));

        [loop]
        for (int lightIndex = 0; lightIndex < 4; lightIndex++)
        {
            if (lightIndex >= (int)_ManualLightCount)
            {
                break;
            }

            float lightType = _ManualLightData0[lightIndex].x;
            float3 lightColor = _ManualLightColor[lightIndex].rgb;
            float3 lightDirectionWS = 0.0;
            float attenuation = 1.0;

            if (lightType < 0.5)
            {
                lightDirectionWS = normalize(_ManualLightDirectionWS[lightIndex].xyz);
            }
            else
            {
                float3 toLight = _ManualLightPositionWS[lightIndex].xyz - hitPositionWS;
                float distanceToLight = length(toLight);
                float lightRange = max(_ManualLightData0[lightIndex].y, 0.01);
                if (distanceToLight >= lightRange)
                {
                    continue;
                }

                lightDirectionWS = toLight / max(distanceToLight, 0.001);
                float distanceFade = saturate(1.0 - distanceToLight / lightRange);
                attenuation = distanceFade * distanceFade;

                if (lightType > 1.5)
                {
                    float outerCos = _ManualLightData1[lightIndex].y;
                    float innerCos = max(_ManualLightData1[lightIndex].x, outerCos + 0.001);
                    float coneCos = dot(normalize(hitPositionWS - _ManualLightPositionWS[lightIndex].xyz), normalize(_ManualLightDirectionWS[lightIndex].xyz));
                    float coneFade = saturate((coneCos - outerCos) / max(innerCos - outerCos, 0.001));
                    attenuation *= coneFade * coneFade;
                }
            }

            float nDotL = saturate(dot(normalWS, lightDirectionWS));
            float diffuse = nDotL * ao * saturate(1.0 - cracks * 0.28);
            float3 halfwayDirection = normalize(lightDirectionWS + viewWS);
            float nDotH = saturate(dot(normalWS, halfwayDirection));
            float specularPower = lerp(80.0, 10.0, roughness);
            float specular = pow(nDotH, specularPower) * nDotL * _SpecularStrength;
            lighting += lightColor * attenuation * (albedo * diffuse + specular);
        }

        float rim = pow(1.0 - nDotV, 3.7) * 0.08;
        lighting += _AmbientTopColor.rgb * albedo * rim;
        return lighting;
    }

    void GetSurfaceAndBuiltinData(
        FragInputs input,
        float3 viewDirectionWS,
        inout PositionInputs posInput,
        out SurfaceData surfaceData,
        out BuiltinData builtinData)
    {
        float2 uv = input.texCoord0.xy;
        float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(uv, _BaseMap));
        float alpha = saturate(baseSample.a * _BaseColor.a);
        GENERIC_ALPHA_TEST(alpha, _AlphaCutoff);

        ZERO_BUILTIN_INITIALIZE(builtinData);
        builtinData.opacity = alpha;
        builtinData.alphaClipTreshold = _AlphaCutoff;

        float3 normalTS = BuildNormalTS(uv);
        float3x3 tangentToWorld = float3x3(
            normalize(input.tangentToWorld[0].xyz),
            normalize(input.tangentToWorld[1].xyz),
            normalize(input.tangentToWorld[2].xyz));
        float3 normalWS = normalize(
            normalTS.x * tangentToWorld[0]
            + normalTS.y * tangentToWorld[1]
            + normalTS.z * tangentToWorld[2]);

        float3 hitPositionWS = GetAbsolutePositionWS(input.positionRWS);
        float3 color = EvaluateLighting(baseSample.rgb * _BaseColor.rgb, normalWS, viewDirectionWS, hitPositionWS, uv);
        if (_UseEmissionMap > 0.5)
        {
            color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, TRANSFORM_TEX(uv, _EmissionMap)).rgb * _EmissionStrength;
        }

        surfaceData.normalWS = normalWS;
        surfaceData.color = color;
    }

    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode" = "DepthForwardOnly" }

            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #define SHADERPASS SHADERPASS_DEPTH_ONLY
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #define SHADERPASS SHADERPASS_FORWARD_UNLIT
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForwardUnlit.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #define SHADERPASS SHADERPASS_SHADOWS
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
