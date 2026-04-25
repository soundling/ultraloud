Shader "Ultraloud/Nature/Hybrid Tree Leaves HDRP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _DepthMap("Depth Map", 2D) = "black" {}
        _ThicknessMap("Thickness Map", 2D) = "white" {}
        _DensityMap("Density Map", 2D) = "gray" {}
        _WindMap("Wind Map", 2D) = "black" {}

        [Header(Cutout)]
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.08
        _CoverageSoftness("Coverage Softness", Range(0.0, 0.2)) = 0.04

        [Header(Shading)]
        _NormalScale("Normal Strength", Range(0.0, 2.0)) = 1.0
        _CanopyNormalBend("Canopy Normal Bend", Range(0.0, 2.0)) = 0.35
        _WrapDiffuse("Wrap Diffuse", Range(0.0, 1.0)) = 0.5
        _DensityShadowStrength("Density Shadow", Range(0.0, 2.0)) = 0.55
        _DepthSelfShadowStrength("Depth Self Shadow", Range(0.0, 2.0)) = 0.3
        _SurfaceRoughness("Surface Roughness", Range(0.0, 1.0)) = 0.78
        _SpecularStrength("Specular Strength", Range(0.0, 2.0)) = 0.18
        _RimStrength("Rim Strength", Range(0.0, 2.0)) = 0.12
        _RimPower("Rim Power", Range(0.5, 8.0)) = 3.0

        [Header(Transmission)]
        _TransmissionColor("Transmission Color", Color) = (0.72, 0.95, 0.34, 1)
        _TransmissionStrength("Transmission Strength", Range(0.0, 4.0)) = 0.75
        _TransmissionPower("Transmission Power", Range(0.5, 8.0)) = 2.1

        [Header(Ambient)]
        _AmbientTopColor("Ambient Top Color", Color) = (0.58, 0.64, 0.56, 1)
        _AmbientBottomColor("Ambient Bottom Color", Color) = (0.13, 0.14, 0.09, 1)
        _AmbientIntensity("Ambient Intensity", Range(0.0, 4.0)) = 1.05

        [HideInInspector] _UseNormalMap("Use Normal Map", Float) = 0
        [HideInInspector] _UseDepthMap("Use Depth Map", Float) = 0
        [HideInInspector] _UseThicknessMap("Use Thickness Map", Float) = 0
        [HideInInspector] _UseDensityMap("Use Density Map", Float) = 0
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
    TEXTURE2D(_DepthMap);
    SAMPLER(sampler_DepthMap);
    TEXTURE2D(_ThicknessMap);
    SAMPLER(sampler_ThicknessMap);
    TEXTURE2D(_DensityMap);
    SAMPLER(sampler_DensityMap);
    TEXTURE2D(_WindMap);
    SAMPLER(sampler_WindMap);

    CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _TransmissionColor;
    float4 _AmbientTopColor;
    float4 _AmbientBottomColor;
    float4 _BaseMap_ST;
    float4 _NormalMap_ST;
    float4 _DepthMap_ST;
    float4 _ThicknessMap_ST;
    float4 _DensityMap_ST;
    float4 _WindMap_ST;
    float _AlphaCutoff;
    float _CoverageSoftness;
    float _NormalScale;
    float _CanopyNormalBend;
    float _WrapDiffuse;
    float _DensityShadowStrength;
    float _DepthSelfShadowStrength;
    float _SurfaceRoughness;
    float _SpecularStrength;
    float _RimStrength;
    float _RimPower;
    float _TransmissionStrength;
    float _TransmissionPower;
    float _AmbientIntensity;
    float _UseNormalMap;
    float _UseDepthMap;
    float _UseThicknessMap;
    float _UseDensityMap;
    CBUFFER_END

    float _ManualLightCount;
    float4 _ManualLightPositionWS[4];
    float4 _ManualLightDirectionWS[4];
    float4 _ManualLightColor[4];
    float4 _ManualLightData0[4];
    float4 _ManualLightData1[4];

    float4 SampleBase(float2 uv)
    {
        return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(uv, _BaseMap));
    }

    float SampleOptionalMap(TEXTURE2D_PARAM(mapTexture, mapSampler), float2 uv, float4 st, float enabled, float fallbackValue)
    {
        if (enabled < 0.5)
        {
            return fallbackValue;
        }

        return SAMPLE_TEXTURE2D(mapTexture, mapSampler, uv * st.xy + st.zw).r;
    }

    float3 UnpackTreeNormal(float4 packedNormal)
    {
        float3 normalTS = packedNormal.xyz * 2.0 - 1.0;
        normalTS.xy *= _NormalScale;
        normalTS.z = sqrt(saturate(1.0 - dot(normalTS.xy, normalTS.xy)));
        return normalize(normalTS);
    }

    float3 BuildSurfaceNormalTS(float2 uv)
    {
        float3 macroNormal = normalize(float3((uv.x * 2.0 - 1.0) * _CanopyNormalBend, 0.0, 1.0));
        if (_UseNormalMap < 0.5)
        {
            return macroNormal;
        }

        float3 detailNormal = UnpackTreeNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(uv, _NormalMap)));
        float3 combined;
        combined.xy = macroNormal.xy + detailNormal.xy;
        combined.z = macroNormal.z * detailNormal.z;
        return normalize(combined);
    }

    float ComputeAlpha(float baseAlpha)
    {
        float alpha = saturate(baseAlpha * _BaseColor.a);
        float clipAlpha = alpha;
        if (_CoverageSoftness > 0.0001)
        {
            clipAlpha = saturate((alpha - _AlphaCutoff) / max(_CoverageSoftness, 0.0001));
        }

        GENERIC_ALPHA_TEST(alpha, _AlphaCutoff);
        return clipAlpha;
    }

    float3 EvaluateLighting(float3 albedo, float3 normalWS, float3 viewWS, float3 hitPositionWS, float2 uv)
    {
        float depth = SampleOptionalMap(TEXTURE2D_ARGS(_DepthMap, sampler_DepthMap), uv, _DepthMap_ST, _UseDepthMap, 0.0);
        float thickness = SampleOptionalMap(TEXTURE2D_ARGS(_ThicknessMap, sampler_ThicknessMap), uv, _ThicknessMap_ST, _UseThicknessMap, 0.65);
        float density = SampleOptionalMap(TEXTURE2D_ARGS(_DensityMap, sampler_DensityMap), uv, _DensityMap_ST, _UseDensityMap, 0.35);

        float3 ambient = lerp(_AmbientBottomColor.rgb, _AmbientTopColor.rgb, saturate(uv.y)) * _AmbientIntensity;
        float3 lighting = ambient * albedo * (1.0 - density * _DensityShadowStrength * 0.35);
        float nDotV = saturate(dot(normalWS, viewWS));
        float roughness = saturate(_SurfaceRoughness);

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

            float signedNDotL = dot(normalWS, lightDirectionWS);
            float wrappedDiffuse = saturate((abs(signedNDotL) + _WrapDiffuse) / (1.0 + _WrapDiffuse));
            float selfShadow = saturate(1.0 - depth * _DepthSelfShadowStrength * saturate(1.0 - abs(signedNDotL)));
            float diffuseMask = saturate(wrappedDiffuse * selfShadow * (1.0 - density * _DensityShadowStrength * 0.45));

            float3 halfwayDirection = normalize(lightDirectionWS + viewWS);
            float specularNormalSign = signedNDotL >= 0.0 ? 1.0 : -1.0;
            float nDotH = saturate(dot(normalWS * specularNormalSign, halfwayDirection));
            float specularPower = lerp(64.0, 8.0, roughness);
            float specular = pow(nDotH, specularPower) * saturate(abs(signedNDotL)) * _SpecularStrength;

            float backlight = pow(saturate(dot(-lightDirectionWS, viewWS)), _TransmissionPower);
            float transmission = backlight * thickness * _TransmissionStrength * (0.35 + density * 0.65);
            float3 transmitted = _TransmissionColor.rgb * albedo * transmission;

            lighting += lightColor * attenuation * (albedo * diffuseMask + specular + transmitted);
        }

        float rim = pow(1.0 - nDotV, _RimPower) * _RimStrength;
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
        float4 baseSample = SampleBase(uv);
        float alpha = ComputeAlpha(baseSample.a);

        ZERO_BUILTIN_INITIALIZE(builtinData);
        builtinData.opacity = alpha;
        builtinData.alphaClipTreshold = _AlphaCutoff;

        float3 normalTS = BuildSurfaceNormalTS(uv);
        float3x3 tangentToWorld = float3x3(
            normalize(input.tangentToWorld[0].xyz),
            normalize(input.tangentToWorld[1].xyz),
            normalize(input.tangentToWorld[2].xyz));
        float3 normalWS = normalize(
            normalTS.x * tangentToWorld[0]
            + normalTS.y * tangentToWorld[1]
            + normalTS.z * tangentToWorld[2]);

        if (dot(normalWS, viewDirectionWS) < 0.0)
        {
            normalWS = -normalWS;
        }

        float3 hitPositionWS = GetAbsolutePositionWS(input.positionRWS);
        surfaceData.normalWS = normalWS;
        surfaceData.color = EvaluateLighting(baseSample.rgb * _BaseColor.rgb, normalWS, viewDirectionWS, hitPositionWS, uv);
    }

    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
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
            Blend SrcAlpha OneMinusSrcAlpha
            Blend 1 SrcAlpha OneMinusSrcAlpha
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
