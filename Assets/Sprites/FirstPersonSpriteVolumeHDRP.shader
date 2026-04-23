Shader "Ultraloud/First Person/Sprite Volume HDRP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseColorMap("Base Color Map", 2D) = "white" {}
        _NormalMap("Detail Normal", 2D) = "bump" {}
        _MacroNormalMap("Macro Normal", 2D) = "bump" {}
        _HeightMap("Front Depth", 2D) = "black" {}
        _BackDepthMap("Back Depth", 2D) = "white" {}
        _ThicknessMap("Thickness", 2D) = "white" {}
        _MaskMap("Packed Masks", 2D) = "white" {}
        _ShellOccupancyAtlas("Shell Occupancy Atlas", 2D) = "black" {}
        _SdfMap("SDF", 2D) = "gray" {}
        [HDR] _EmissiveColor("Emissive Color", Color) = (0, 0, 0, 1)
        _EmissiveColorMap("Emissive Map", 2D) = "black" {}

        [Header(Volume)]
        _VolumeThickness("Volume Thickness (Meters)", Range(0.0, 0.5)) = 0.12
        _ParallaxScale("Parallax Scale", Range(0.0, 0.25)) = 0.08
        _RaymarchSteps("Raymarch Steps", Range(1, 64)) = 24
        _ShadowSteps("Shadow Steps", Range(1, 32)) = 12
        _ShellSliceCount("Shell Slice Count", Range(1, 64)) = 16
        _ShellAtlasGrid("Shell Atlas Grid", Vector) = (4, 4, 0, 0)

        [Header(Depth Decoding)]
        [ToggleUI] _InvertFrontDepth("Invert Front Depth", Float) = 0
        [ToggleUI] _InvertBackDepth("Invert Back Depth", Float) = 0
        [ToggleUI] _AutoCorrectDualDepth("Auto Correct Dual Depth", Float) = 1
        _MinimumDepthSeparation("Minimum Depth Separation", Range(0.0, 0.05)) = 0.01

        [Header(Shading)]
        _NormalScale("Detail Normal Strength", Range(0.0, 2.0)) = 1.0
        _MacroNormalScale("Macro Normal Strength", Range(0.0, 2.0)) = 1.0
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.2
        _SelfShadowStrength("Self Shadow Strength", Range(0.0, 1.0)) = 0.6
        _TransmissionStrength("Transmission Strength", Range(0.0, 4.0)) = 0.35
        _AmbientOcclusionStrength("Ambient Occlusion Strength", Range(0.0, 4.0)) = 1.0
        _SpecularStrength("Specular Strength", Range(0.0, 4.0)) = 1.0
        _SdfSoftness("SDF Softness", Range(0.0, 1.0)) = 0.08
        _AmbientColor("Ambient Color", Color) = (0.48, 0.52, 0.58, 1)
        _AmbientIntensity("Ambient Intensity", Range(0.0, 4.0)) = 1.0

        [HideInInspector] _UseFrontDepth("Use Front Depth", Float) = 0
        [HideInInspector] _UseBackDepth("Use Back Depth", Float) = 0
        [HideInInspector] _UseThicknessMap("Use Thickness Map", Float) = 0
        [HideInInspector] _UseMaskMap("Use Mask Map", Float) = 0
        [HideInInspector] _UseShellAtlas("Use Shell Atlas", Float) = 0
        [HideInInspector] _UseSdf("Use SDF", Float) = 0
        [HideInInspector] _UseDetailNormal("Use Detail Normal", Float) = 0
        [HideInInspector] _UseMacroNormal("Use Macro Normal", Float) = 0
        [HideInInspector] _UseEmissiveMap("Use Emissive Map", Float) = 0
        [HideInInspector] _MainTex("MainTex", 2D) = "white" {}
        [HideInInspector] _Color("Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Cutoff("Cutoff", Range(0.0, 1.0)) = 0.2
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
    #pragma multi_compile_instancing
    #pragma multi_compile _ DOTS_INSTANCING_ON

    #define _ALPHATEST_ON
    #define _DEPTHOFFSET_ON
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

    TEXTURE2D(_BaseColorMap);
    SAMPLER(sampler_BaseColorMap);
    TEXTURE2D(_NormalMap);
    SAMPLER(sampler_NormalMap);
    TEXTURE2D(_MacroNormalMap);
    SAMPLER(sampler_MacroNormalMap);
    TEXTURE2D(_HeightMap);
    SAMPLER(sampler_HeightMap);
    TEXTURE2D(_BackDepthMap);
    SAMPLER(sampler_BackDepthMap);
    TEXTURE2D(_ThicknessMap);
    SAMPLER(sampler_ThicknessMap);
    TEXTURE2D(_MaskMap);
    SAMPLER(sampler_MaskMap);
    TEXTURE2D(_ShellOccupancyAtlas);
    SAMPLER(sampler_ShellOccupancyAtlas);
    TEXTURE2D(_SdfMap);
    SAMPLER(sampler_SdfMap);
    TEXTURE2D(_EmissiveColorMap);
    SAMPLER(sampler_EmissiveColorMap);

    CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _EmissiveColor;
    float4 _AmbientColor;
    float4 _BaseColorMap_ST;
    float4 _NormalMap_ST;
    float4 _MacroNormalMap_ST;
    float4 _HeightMap_ST;
    float4 _BackDepthMap_ST;
    float4 _ThicknessMap_ST;
    float4 _MaskMap_ST;
    float4 _ShellOccupancyAtlas_ST;
    float4 _SdfMap_ST;
    float4 _EmissiveColorMap_ST;
    float4 _ShellAtlasGrid;
    float _VolumeThickness;
    float _ParallaxScale;
    float _RaymarchSteps;
    float _ShadowSteps;
    float _ShellSliceCount;
    float _InvertFrontDepth;
    float _InvertBackDepth;
    float _AutoCorrectDualDepth;
    float _MinimumDepthSeparation;
    float _NormalScale;
    float _MacroNormalScale;
    float _AlphaCutoff;
    float _SelfShadowStrength;
    float _TransmissionStrength;
    float _AmbientOcclusionStrength;
    float _SpecularStrength;
    float _SdfSoftness;
    float _AmbientIntensity;
    float _UseFrontDepth;
    float _UseBackDepth;
    float _UseThicknessMap;
    float _UseMaskMap;
    float _UseShellAtlas;
    float _UseSdf;
    float _UseDetailNormal;
    float _UseMacroNormal;
    float _UseEmissiveMap;
    CBUFFER_END

    float _ManualLightCount;
    float4 _ManualLightPositionWS[4];
    float4 _ManualLightDirectionWS[4];
    float4 _ManualLightColor[4];
    float4 _ManualLightData0[4];
    float4 _ManualLightData1[4];

    struct SpriteHit
    {
        float found;
        float2 uv;
        float depth;
    };

    float3 UnpackSpriteNormal(float4 packedNormal, float scaleValue)
    {
        float3 normalTS = packedNormal.xyz * 2.0 - 1.0;
        normalTS.xy *= scaleValue;
        normalTS.z = sqrt(saturate(1.0 - dot(normalTS.xy, normalTS.xy)));
        return normalize(normalTS);
    }

    float3 BlendSpriteNormals(float3 baseNormal, float3 detailNormal)
    {
        float3 blended;
        blended.xy = baseNormal.xy + detailNormal.xy;
        blended.z = baseNormal.z * detailNormal.z;
        return normalize(blended);
    }

    float2 ComputeParallaxDirection(float3 directionTS)
    {
        return directionTS.xy / max(abs(directionTS.z), 0.05) * _ParallaxScale;
    }

    float DecodeDepthSample(float depthSample, float invertToggle)
    {
        float decoded = saturate(depthSample);
        if (invertToggle > 0.5)
        {
            decoded = 1.0 - decoded;
        }

        return decoded;
    }

    float SampleFrontDepth(float2 uv)
    {
        if (_UseFrontDepth < 0.5)
        {
            return 0.0;
        }

        float sampledDepth = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, TRANSFORM_TEX(uv, _HeightMap)).r;
        return DecodeDepthSample(sampledDepth, _InvertFrontDepth);
    }

    float SampleThickness(float2 uv)
    {
        float sampledThickness = 1.0;
        if (_UseThicknessMap > 0.5)
        {
            sampledThickness = SAMPLE_TEXTURE2D(_ThicknessMap, sampler_ThicknessMap, TRANSFORM_TEX(uv, _ThicknessMap)).r;
        }
        else if (_UseMaskMap > 0.5)
        {
            sampledThickness = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, TRANSFORM_TEX(uv, _MaskMap)).a;
        }

        return saturate(sampledThickness);
    }

    void SampleDepthInterval(float2 uv, out float frontDepth, out float backDepth)
    {
        frontDepth = SampleFrontDepth(uv);
        if (_UseBackDepth > 0.5)
        {
            float backSample = SAMPLE_TEXTURE2D(_BackDepthMap, sampler_BackDepthMap, TRANSFORM_TEX(uv, _BackDepthMap)).r;
            backDepth = DecodeDepthSample(backSample, _InvertBackDepth);

            if (_AutoCorrectDualDepth > 0.5 && backDepth + _MinimumDepthSeparation < frontDepth)
            {
                float flippedFront = 1.0 - frontDepth;
                float flippedBack = 1.0 - backDepth;
                if (flippedBack > flippedFront)
                {
                    frontDepth = flippedFront;
                    backDepth = flippedBack;
                }
            }
        }
        else
        {
            backDepth = frontDepth + SampleThickness(uv);
        }

        frontDepth = saturate(frontDepth);
        backDepth = saturate(max(backDepth, frontDepth + _MinimumDepthSeparation));
    }

    float4 SamplePackedMask(float2 uv)
    {
        if (_UseMaskMap < 0.5)
        {
            return float4(1.0, 0.55, 0.0, 1.0);
        }

        return SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, TRANSFORM_TEX(uv, _MaskMap));
    }

    float SampleShellSlice(float2 uv, float sliceIndex)
    {
        float columns = max(_ShellAtlasGrid.x, 1.0);
        float rows = max(_ShellAtlasGrid.y, 1.0);
        float clampedSlice = clamp(sliceIndex, 0.0, max(_ShellSliceCount - 1.0, 0.0));
        float tileX = fmod(clampedSlice, columns);
        float tileY = floor(clampedSlice / columns);
        float2 atlasUV = (uv + float2(tileX, tileY)) / float2(columns, rows);
        return SAMPLE_TEXTURE2D(_ShellOccupancyAtlas, sampler_ShellOccupancyAtlas, atlasUV).r;
    }

    float SampleShellOccupancy(float2 uv, float depthValue)
    {
        if (_UseShellAtlas < 0.5)
        {
            return 1.0;
        }

        float slicePosition = saturate(depthValue) * max(_ShellSliceCount - 1.0, 1.0);
        float lowerSlice = floor(slicePosition);
        float upperSlice = min(lowerSlice + 1.0, max(_ShellSliceCount - 1.0, 0.0));
        float blendFactor = frac(slicePosition);
        float lowerValue = SampleShellSlice(uv, lowerSlice);
        float upperValue = SampleShellSlice(uv, upperSlice);
        return lerp(lowerValue, upperValue, blendFactor);
    }

    float EvaluateOccupancy(float2 uv, float depthValue)
    {
        if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
        {
            return 0.0;
        }

        float front;
        float back;
        SampleDepthInterval(uv, front, back);
        float interval = step(front, depthValue) * step(depthValue, back);
        if (interval <= 0.0)
        {
            return 0.0;
        }

        if (_UseShellAtlas > 0.5)
        {
            interval *= step(0.15, SampleShellOccupancy(uv, depthValue));
        }

        return interval;
    }

    SpriteHit RaymarchSpriteVolume(float2 baseUv, float3 viewDirectionTS)
    {
        SpriteHit hit;
        hit.found = 0.0;
        hit.uv = baseUv;
        hit.depth = 0.0;

        int steps = (int)clamp(_RaymarchSteps, 1.0, 64.0);
        float stepSize = 1.0 / steps;
        float2 parallaxDirection = ComputeParallaxDirection(viewDirectionTS);

        [loop]
        for (int i = 0; i < 64; i++)
        {
            if (i >= steps)
            {
                break;
            }

            float depthValue = (i + 1) * stepSize;
            float2 uv = baseUv - parallaxDirection * depthValue;
            if (EvaluateOccupancy(uv, depthValue) <= 0.5)
            {
                continue;
            }

            float lowerDepth = max(0.0, depthValue - stepSize);
            float upperDepth = depthValue;

            [unroll]
            for (int refine = 0; refine < 5; refine++)
            {
                float middleDepth = 0.5 * (lowerDepth + upperDepth);
                float2 middleUv = baseUv - parallaxDirection * middleDepth;
                if (EvaluateOccupancy(middleUv, middleDepth) > 0.5)
                {
                    upperDepth = middleDepth;
                }
                else
                {
                    lowerDepth = middleDepth;
                }
            }

            hit.found = 1.0;
            hit.depth = upperDepth;
            hit.uv = baseUv - parallaxDirection * upperDepth;
            return hit;
        }

        if (EvaluateOccupancy(baseUv, 0.0) > 0.5)
        {
            hit.found = 1.0;
        }

        return hit;
    }

    float ComputeAlpha(float2 uv, float baseAlpha)
    {
        float alpha = saturate(baseAlpha);
        if (_UseSdf > 0.5)
        {
            float sdf = SAMPLE_TEXTURE2D(_SdfMap, sampler_SdfMap, TRANSFORM_TEX(uv, _SdfMap)).r;
            float softness = max(_SdfSoftness, 0.001);
            alpha *= smoothstep(_AlphaCutoff - softness, _AlphaCutoff + softness, sdf);
        }

        return saturate(alpha);
    }

    float3 SampleSurfaceNormalTS(float2 uv)
    {
        float3 normalTS = float3(0.0, 0.0, 1.0);

        if (_UseMacroNormal > 0.5)
        {
            float4 packedMacro = SAMPLE_TEXTURE2D(_MacroNormalMap, sampler_MacroNormalMap, TRANSFORM_TEX(uv, _MacroNormalMap));
            normalTS = BlendSpriteNormals(normalTS, UnpackSpriteNormal(packedMacro, _MacroNormalScale));
        }

        if (_UseDetailNormal > 0.5)
        {
            float4 packedDetail = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(uv, _NormalMap));
            normalTS = BlendSpriteNormals(normalTS, UnpackSpriteNormal(packedDetail, _NormalScale));
        }

        return normalize(normalTS);
    }

    float ComputeSelfShadow(float2 hitUv, float hitDepth, float3 lightDirectionTS)
    {
        if (_SelfShadowStrength <= 0.001 || _ShadowSteps <= 0.0)
        {
            return 1.0;
        }

        if (abs(lightDirectionTS.z) < 0.05)
        {
            return 1.0;
        }

        int steps = (int)clamp(_ShadowSteps, 1.0, 32.0);
        float depthStep = -sign(lightDirectionTS.z) / steps;
        float currentDepth = hitDepth + depthStep * 1.5;
        float2 currentUv = hitUv;

        [loop]
        for (int i = 0; i < 32; i++)
        {
            if (i >= steps)
            {
                break;
            }

            currentUv += lightDirectionTS.xy * (depthStep / max(abs(lightDirectionTS.z), 0.05)) * _ParallaxScale;

            if (currentDepth < 0.0 || currentDepth > 1.0)
            {
                break;
            }

            if (EvaluateOccupancy(currentUv, currentDepth) > 0.5)
            {
                return 1.0 - _SelfShadowStrength;
            }

            currentDepth += depthStep;
        }

        return 1.0;
    }

    float3 FresnelSchlick(float cosTheta, float3 f0)
    {
        return f0 + (1.0 - f0) * Pow4(1.0 - saturate(cosTheta));
    }

    float DistributionGGX(float nDotH, float roughness)
    {
        float alpha = max(roughness * roughness, 0.001);
        float alpha2 = alpha * alpha;
        float denominator = nDotH * nDotH * (alpha2 - 1.0) + 1.0;
        return alpha2 / max(PI * denominator * denominator, 0.001);
    }

    float GeometrySchlickGGX(float nDotV, float roughness)
    {
        float r = roughness + 1.0;
        float k = (r * r) * 0.125;
        return nDotV / lerp(nDotV, 1.0, k);
    }

    float GeometrySmith(float nDotV, float nDotL, float roughness)
    {
        return GeometrySchlickGGX(nDotV, roughness) * GeometrySchlickGGX(nDotL, roughness);
    }

    float3 EvaluateLighting(
        float3 albedo,
        float3 normalWS,
        float3 viewWS,
        float3 hitPositionWS,
        float3x3 tangentToWorld,
        float2 hitUv,
        float hitDepth)
    {
        float4 packedMask = SamplePackedMask(hitUv);
        float ao = saturate(1.0 - (1.0 - packedMask.r) * _AmbientOcclusionStrength);
        float roughness = saturate(packedMask.g);
        float metalness = saturate(packedMask.b);
        float thickness = SampleThickness(hitUv);
        float3 diffuseColor = albedo * (1.0 - metalness);
        float3 f0 = lerp(float3(0.04, 0.04, 0.04), albedo, metalness) * _SpecularStrength;
        float3 lighting = _AmbientColor.rgb * _AmbientIntensity * diffuseColor * ao;
        float nDotV = saturate(dot(normalWS, viewWS));

        [loop]
        for (int lightIndex = 0; lightIndex < 4; lightIndex++)
        {
            if (lightIndex >= _ManualLightCount)
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
            if (nDotL <= 0.0 || attenuation <= 0.0)
            {
                continue;
            }

            float3 lightDirectionTS;
            lightDirectionTS.x = dot(lightDirectionWS, tangentToWorld[0]);
            lightDirectionTS.y = dot(lightDirectionWS, tangentToWorld[1]);
            lightDirectionTS.z = dot(lightDirectionWS, tangentToWorld[2]);
            float selfShadow = ComputeSelfShadow(hitUv, hitDepth, lightDirectionTS);

            float wrap = 0.18;
            float wrappedDiffuse = saturate((dot(normalWS, lightDirectionWS) + wrap) / (1.0 + wrap));
            float3 halfwayDirection = normalize(lightDirectionWS + viewWS);
            float nDotH = saturate(dot(normalWS, halfwayDirection));
            float vDotH = saturate(dot(viewWS, halfwayDirection));
            float distribution = DistributionGGX(nDotH, roughness);
            float geometry = GeometrySmith(nDotV, nDotL, roughness);
            float3 fresnel = FresnelSchlick(vDotH, f0);
            float3 numerator = distribution * geometry * fresnel;
            float denominator = max(4.0 * nDotV * nDotL, 0.001);
            float3 specular = numerator / denominator;

            lighting += lightColor * attenuation * selfShadow * (diffuseColor * wrappedDiffuse + specular);

            float backLighting = saturate(-dot(normalWS, lightDirectionWS));
            lighting += lightColor * attenuation * backLighting * thickness * _TransmissionStrength * diffuseColor * 0.5;
        }

        if (_UseEmissiveMap > 0.5)
        {
            lighting += SAMPLE_TEXTURE2D(_EmissiveColorMap, sampler_EmissiveColorMap, TRANSFORM_TEX(hitUv, _EmissiveColorMap)).rgb * _EmissiveColor.rgb;
        }
        else
        {
            lighting += _EmissiveColor.rgb;
        }

        return lighting;
    }

    void GetSurfaceAndBuiltinData(
        FragInputs input,
        float3 viewDirectionWS,
        inout PositionInputs posInput,
        out SurfaceData surfaceData,
        out BuiltinData builtinData)
    {
        float2 baseUv = input.texCoord0.xy;
        float3x3 tangentToWorld = float3x3(
            normalize(input.tangentToWorld[0].xyz),
            normalize(input.tangentToWorld[1].xyz),
            normalize(input.tangentToWorld[2].xyz));

        float3 viewDirectionTS;
        viewDirectionTS.x = dot(viewDirectionWS, tangentToWorld[0]);
        viewDirectionTS.y = dot(viewDirectionWS, tangentToWorld[1]);
        viewDirectionTS.z = dot(viewDirectionWS, tangentToWorld[2]);

        SpriteHit hit = RaymarchSpriteVolume(baseUv, viewDirectionTS);
        float2 hitUv = hit.uv;
        float4 baseSample = SAMPLE_TEXTURE2D(_BaseColorMap, sampler_BaseColorMap, TRANSFORM_TEX(hitUv, _BaseColorMap));
        float alpha = hit.found * ComputeAlpha(hitUv, baseSample.a * _BaseColor.a);

        ZERO_BUILTIN_INITIALIZE(builtinData);
        builtinData.opacity = alpha;
        builtinData.alphaClipTreshold = _AlphaCutoff;

        GENERIC_ALPHA_TEST(alpha, _AlphaCutoff);

        float3 normalTS = SampleSurfaceNormalTS(hitUv);
        float3 normalWS = normalize(
            normalTS.x * tangentToWorld[0]
            + normalTS.y * tangentToWorld[1]
            + normalTS.z * tangentToWorld[2]);

        float depthOffset = hit.depth * _VolumeThickness;
        float3 hitPositionWS = GetAbsolutePositionWS(input.positionRWS + depthOffset * (-viewDirectionWS));
        ApplyDepthOffsetPositionInput(viewDirectionWS, depthOffset, GetViewForwardDir(), GetWorldToHClipMatrix(), posInput);
        builtinData.depthOffset = depthOffset;

        surfaceData.normalWS = normalWS;
        surfaceData.color = EvaluateLighting(
            baseSample.rgb * _BaseColor.rgb,
            normalWS,
            viewDirectionWS,
            hitPositionWS,
            tangentToWorld,
            hitUv,
            hit.depth);
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
