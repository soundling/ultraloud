Shader "Ultraloud/Directional Sprites/Billboard Lit HDRP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}

        [Header(Shading)]
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.08
        _NormalScale("Normal Strength", Range(0.0, 2.0)) = 1.0
        _DetailNormalInfluence("Detail Normal Influence", Range(0.0, 1.0)) = 0.55
        _MacroNormalBend("Macro Normal Bend", Range(0.0, 2.0)) = 0.75
        _WrapDiffuse("Wrap Diffuse", Range(0.0, 1.0)) = 0.35
        _AmbientTopColor("Ambient Top Color", Color) = (0.52, 0.56, 0.62, 1)
        _AmbientBottomColor("Ambient Bottom Color", Color) = (0.14, 0.12, 0.10, 1)
        _AmbientIntensity("Ambient Intensity", Range(0.0, 4.0)) = 1.0
        _SurfaceRoughness("Surface Roughness", Range(0.0, 1.0)) = 0.7
        _SpecularStrength("Specular Strength", Range(0.0, 4.0)) = 0.45
        _MinSpecularPower("Min Specular Power", Range(1.0, 32.0)) = 6.0
        _MaxSpecularPower("Max Specular Power", Range(1.0, 128.0)) = 24.0
        _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimStrength("Rim Strength", Range(0.0, 4.0)) = 0.18
        _RimPower("Rim Power", Range(0.5, 8.0)) = 3.0

        [HideInInspector] _SpriteFlipX("Sprite Flip X", Float) = 1
        [HideInInspector] _UseNormalMap("Use Normal Map", Float) = 0
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

    CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _AmbientTopColor;
    float4 _AmbientBottomColor;
    float4 _RimColor;
    float4 _BaseMap_ST;
    float4 _NormalMap_ST;
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
    float _SpriteFlipX;
    float _UseNormalMap;
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

    float3 EvaluateLighting(float3 albedo, float3 normalWS, float3 viewWS, float3 hitPositionWS, float2 uv)
    {
        float roughness = saturate(_SurfaceRoughness);
        float3 lighting = lerp(_AmbientBottomColor.rgb, _AmbientTopColor.rgb, saturate(uv.y)) * _AmbientIntensity * albedo;
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
            float specular = pow(nDotH, specularPower) * specularNdotL * _SpecularStrength;
            if (wrappedDiffuse <= 0.0 && specular <= 0.0)
            {
                continue;
            }

            lighting += lightColor * attenuation * (albedo * wrappedDiffuse + specular);
        }

        float rim = pow(1.0 - nDotV, _RimPower) * _RimStrength;
        lighting += _RimColor.rgb * rim;
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
        float4 baseSample = SampleBase(spriteUv);
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
