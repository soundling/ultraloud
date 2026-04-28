Shader "Ultraloud/Directional Sprites/Billboard Lit HDRP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _PackedMasks("Packed Masks", 2D) = "black" {}
        _EmissionMap("Emission Map", 2D) = "black" {}

        [Header(Shading)]
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.08
        _NormalScale("Normal Strength", Range(0.0, 2.0)) = 0.9
        _DetailNormalInfluence("Detail Normal Influence", Range(0.0, 1.0)) = 0.48
        _MacroNormalBend("Macro Normal Bend", Range(0.0, 2.0)) = 0.55
        _WrapDiffuse("Wrap Diffuse", Range(0.0, 1.0)) = 0.34
        _AmbientTopColor("Ambient Top Color", Color) = (0.52, 0.56, 0.62, 1)
        _AmbientBottomColor("Ambient Bottom Color", Color) = (0.14, 0.12, 0.10, 1)
        _AmbientIntensity("Ambient Intensity", Range(0.0, 4.0)) = 1.0
        _SurfaceRoughness("Surface Roughness", Range(0.0, 1.0)) = 0.94
        _SpecularStrength("Specular Strength", Range(0.0, 4.0)) = 0.025
        _MinSpecularPower("Min Specular Power", Range(1.0, 32.0)) = 8.0
        _MaxSpecularPower("Max Specular Power", Range(1.0, 128.0)) = 14.0
        _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimStrength("Rim Strength", Range(0.0, 4.0)) = 0.025
        _RimPower("Rim Power", Range(0.5, 8.0)) = 4.2
        _EmissionColor("Emission Color", Color) = (1.0, 0.23, 0.08, 1.0)
        _EmissionStrength("Emission Strength", Range(0.0, 6.0)) = 0.0
        _WetSpecularBoost("Wet Specular Boost", Range(0.0, 2.0)) = 0.0
        _BloodPulseStrength("Blood Pulse Strength", Range(0.0, 2.0)) = 0.0
        _SurfaceCrawlStrength("Surface Crawl Strength", Range(0.0, 0.04)) = 0.0
        _SurfaceCrawlSpeed("Surface Crawl Speed", Range(0.0, 8.0)) = 1.2

        [HideInInspector] _SpriteFlipX("Sprite Flip X", Float) = 1
        [HideInInspector] _UseNormalMap("Use Normal Map", Float) = 0
        [HideInInspector] _UsePackedMasks("Use Packed Masks", Float) = 0
        [HideInInspector] _UseEmissionMap("Use Emission Map", Float) = 0
        [HideInInspector] _TwoSidedLighting("Two Sided Lighting", Float) = 1
        [HideInInspector] _FlipBackfacingNormals("Flip Backfacing Normals", Float) = 1
        [HideInInspector] _UseCustomLightingBasis("Use Custom Lighting Basis", Float) = 0
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
    TEXTURE2D(_PackedMasks);
    SAMPLER(sampler_PackedMasks);
    TEXTURE2D(_EmissionMap);
    SAMPLER(sampler_EmissionMap);

    CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _AmbientTopColor;
    float4 _AmbientBottomColor;
    float4 _RimColor;
    float4 _EmissionColor;
    float4 _BaseMap_ST;
    float4 _NormalMap_ST;
    float4 _PackedMasks_ST;
    float4 _EmissionMap_ST;
    float _AlphaCutoff;
    float _NormalScale;
    float _DetailNormalInfluence;
    float _MacroNormalBend;
    float _WrapDiffuse;
    float _AmbientIntensity;
    float _SurfaceRoughness;
    float _SpecularStrength;
    float _MinSpecularPower;
    float _MaxSpecularPower;
    float _RimStrength;
    float _RimPower;
    float _EmissionStrength;
    float _WetSpecularBoost;
    float _BloodPulseStrength;
    float _SurfaceCrawlStrength;
    float _SurfaceCrawlSpeed;
    float _SpriteFlipX;
    float _UseNormalMap;
    float _UsePackedMasks;
    float _UseEmissionMap;
    float _TwoSidedLighting;
    float _FlipBackfacingNormals;
    float _UseCustomLightingBasis;
    CBUFFER_END

    float _ManualLightCount;
    float4 _ManualLightPositionWS[4];
    float4 _ManualLightDirectionWS[4];
    float4 _ManualLightColor[4];
    float4 _ManualLightData0[4];
    float4 _ManualLightData1[4];
    float4 _LightingRightWS;
    float4 _LightingUpWS;
    float4 _LightingForwardWS;

    float2 ResolveSpriteUV(float2 uv)
    {
        if (_SpriteFlipX < 0.0)
        {
            uv.x = 1.0 - uv.x;
        }

        return uv;
    }

    float4 SampleBase(float2 uv)
    {
        return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(uv, _BaseMap));
    }

    float4 SamplePackedMasks(float2 spriteUv)
    {
        if (_UsePackedMasks < 0.5)
        {
            return float4(0.0, 0.0, 0.0, 0.0);
        }

        return SAMPLE_TEXTURE2D(_PackedMasks, sampler_PackedMasks, TRANSFORM_TEX(spriteUv, _PackedMasks));
    }

    float2 ApplySurfaceCrawl(float2 spriteUv, float4 masks)
    {
        if (_UsePackedMasks < 0.5 || _SurfaceCrawlStrength <= 0.0001 || _SurfaceCrawlSpeed <= 0.0001)
        {
            return spriteUv;
        }

        float pulsePhase = _Time.y * _SurfaceCrawlSpeed + masks.g * 6.28318 + spriteUv.y * 3.75;
        float crawlMask = saturate(masks.a + masks.g * 0.35);
        float crawlAmount = crawlMask * _SurfaceCrawlStrength;
        float2 crawlDirection = normalize(float2(sin(pulsePhase), cos(pulsePhase * 1.37)) + float2(0.001, 0.001));
        return saturate(spriteUv + crawlDirection * crawlAmount);
    }

    float3 SampleEmission(float2 spriteUv, float4 masks)
    {
        if (_UseEmissionMap < 0.5 || _EmissionStrength <= 0.0001)
        {
            return 0.0;
        }

        float3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, TRANSFORM_TEX(spriteUv, _EmissionMap)).rgb;
        float pulse = 0.75 + 0.25 * sin(_Time.y * 4.8 + masks.g * 8.0 + spriteUv.y * 2.0);
        return emission * _EmissionColor.rgb * _EmissionStrength * pulse;
    }

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

    float3 BuildSurfaceNormalTS(float2 uv)
    {
        float2 spriteUv = ResolveSpriteUV(uv);
        float macroX = (spriteUv.x * 2.0 - 1.0) * _MacroNormalBend * _SpriteFlipX;
        float3 macroNormal = normalize(float3(macroX, 0.0, 1.0));
        if (_UseNormalMap < 0.5)
        {
            return macroNormal;
        }

        float3 detailNormal = UnpackSpriteNormal(
            SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(spriteUv, _NormalMap)),
            _NormalScale);
        detailNormal.x *= _SpriteFlipX;

        float3 combined = BlendSpriteNormals(macroNormal, detailNormal);
        return normalize(lerp(macroNormal, combined, _DetailNormalInfluence));
    }

    float ComputeAlpha(float baseAlpha)
    {
        float alpha = saturate(baseAlpha);
        GENERIC_ALPHA_TEST(alpha, _AlphaCutoff);
        return alpha;
    }

    float ComputeEffectCoverage(float alpha)
    {
        float coverage = saturate((alpha - _AlphaCutoff) / max(1.0 - _AlphaCutoff, 0.0001));
        return smoothstep(0.18, 0.82, coverage);
    }

    float3x3 ResolveLightingBasis(FragInputs input)
    {
        if (_UseCustomLightingBasis > 0.5)
        {
            return float3x3(
                normalize(_LightingRightWS.xyz),
                normalize(_LightingUpWS.xyz),
                normalize(_LightingForwardWS.xyz));
        }

        return float3x3(
            normalize(input.tangentToWorld[0].xyz),
            normalize(input.tangentToWorld[1].xyz),
            normalize(input.tangentToWorld[2].xyz));
    }

    float3 EvaluateLighting(float3 albedo, float alpha, float3 normalWS, float3 viewWS, float3 hitPositionWS, float2 uv, float4 masks, float3 emission)
    {
        float wetMask = _UsePackedMasks > 0.5 ? saturate(masks.r) : 0.0;
        float pulseMask = _UsePackedMasks > 0.5 ? saturate(masks.g) : 0.0;
        float grimeMask = _UsePackedMasks > 0.5 ? saturate(masks.b) : 0.0;
        float roughness = saturate(_SurfaceRoughness);
        float3 lighting = lerp(_AmbientBottomColor.rgb, _AmbientTopColor.rgb, saturate(uv.y)) * _AmbientIntensity * albedo;
        float nDotV = saturate(dot(normalWS, viewWS));
        float spriteCoverage = ComputeEffectCoverage(alpha);
        float albedoLuma = dot(albedo, float3(0.2126, 0.7152, 0.0722));
        float edgeTintMask = saturate(albedoLuma * 1.6) * spriteCoverage;
        float3 specularTint = lerp(float3(0.04, 0.04, 0.04), albedo, 0.35);

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

            if (attenuation <= 0.0)
            {
                continue;
            }

            float signedNDotL = dot(normalWS, lightDirectionWS);
            float lightingNDotL = _TwoSidedLighting > 0.5 ? abs(signedNDotL) : signedNDotL;
            float wrappedDiffuse = saturate((lightingNDotL + _WrapDiffuse) / (1.0 + _WrapDiffuse));
            float specularNdotL = saturate(lightingNDotL);
            float3 specularNormalWS = signedNDotL >= 0.0 || _TwoSidedLighting <= 0.5
                ? normalWS
                : -normalWS;
            float3 halfwayDirection = normalize(lightDirectionWS + viewWS);
            float nDotH = saturate(dot(specularNormalWS, halfwayDirection));
            float specularPower = lerp(_MaxSpecularPower, _MinSpecularPower, roughness);
            float specular = pow(nDotH, specularPower) * specularNdotL * _SpecularStrength * (1.0 + wetMask * _WetSpecularBoost) * spriteCoverage;
            if (wrappedDiffuse <= 0.0 && specular <= 0.0)
            {
                continue;
            }

            lighting += lightColor * attenuation * (albedo * wrappedDiffuse + specularTint * specular);
        }

        float rim = pow(1.0 - nDotV, _RimPower) * _RimStrength * edgeTintMask;
        lighting += _RimColor.rgb * albedo * rim;
        lighting *= lerp(1.0, 0.58, grimeMask);

        float bloodPulse = 0.5 + 0.5 * sin(_Time.y * 5.6 + pulseMask * 6.28318);
        lighting += float3(0.72, 0.035, 0.012) * pulseMask * bloodPulse * _BloodPulseStrength * spriteCoverage;
        lighting += emission * spriteCoverage;
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
        float2 spriteUv = ResolveSpriteUV(uv);
        float4 masks = SamplePackedMasks(spriteUv);
        float2 shadedUv = ApplySurfaceCrawl(spriteUv, masks);
        float4 baseSample = SampleBase(shadedUv);
        float alpha = ComputeAlpha(baseSample.a * _BaseColor.a);

        ZERO_BUILTIN_INITIALIZE(builtinData);
        builtinData.opacity = alpha;
        builtinData.alphaClipTreshold = _AlphaCutoff;

        float3x3 tangentToWorld = ResolveLightingBasis(input);

        float3 normalTS = BuildSurfaceNormalTS(uv);
        float3 normalWS = normalize(
            normalTS.x * tangentToWorld[0]
            + normalTS.y * tangentToWorld[1]
            + normalTS.z * tangentToWorld[2]);

        if (_FlipBackfacingNormals > 0.5 && dot(normalWS, viewDirectionWS) < 0.0)
        {
            normalWS = -normalWS;
        }

        float3 hitPositionWS = GetAbsolutePositionWS(input.positionRWS);
        surfaceData.normalWS = normalWS;
        float3 emission = SampleEmission(shadedUv, masks);
        surfaceData.color = EvaluateLighting(baseSample.rgb * _BaseColor.rgb, alpha, normalWS, viewDirectionWS, hitPositionWS, uv, masks, emission);
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
