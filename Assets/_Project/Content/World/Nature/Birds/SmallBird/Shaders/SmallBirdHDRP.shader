Shader "Ultraloud/Nature/Small Bird HDRP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Atlas", 2D) = "white" {}
        _NormalMap("Normal Atlas", 2D) = "bump" {}
        _ThicknessMap("Thickness Atlas", 2D) = "white" {}
        _PackedMasks("Packed Masks", 2D) = "white" {}

        [Header(Cutout)]
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.08
        _CoverageSoftness("Coverage Softness", Range(0.0, 0.25)) = 0.035

        [Header(Shading)]
        _NormalScale("Normal Strength", Range(0.0, 2.0)) = 1.0
        _DetailNormalInfluence("Detail Normal Influence", Range(0.0, 1.0)) = 0.7
        _MacroNormalBend("Macro Normal Bend", Range(0.0, 2.0)) = 0.5
        _WrapDiffuse("Wrap Diffuse", Range(0.0, 1.0)) = 0.45
        _WingShadowStrength("Wing Shadow", Range(0.0, 2.0)) = 0.34
        _BodyShadowStrength("Body Shadow", Range(0.0, 2.0)) = 0.6
        _SurfaceRoughness("Surface Roughness", Range(0.0, 1.0)) = 0.78
        _SpecularStrength("Specular Strength", Range(0.0, 2.0)) = 0.12
        _RimColor("Rim Color", Color) = (0.72, 0.67, 0.58, 1)
        _RimStrength("Rim Strength", Range(0.0, 2.0)) = 0.18
        _RimPower("Rim Power", Range(0.5, 8.0)) = 3.0

        [Header(Transmission)]
        _TransmissionColor("Transmission Color", Color) = (0.98, 0.78, 0.48, 1)
        _TransmissionStrength("Transmission Strength", Range(0.0, 4.0)) = 0.38
        _TransmissionPower("Transmission Power", Range(0.5, 8.0)) = 2.25
        _WingTransmissionBoost("Wing Transmission Boost", Range(0.0, 4.0)) = 1.45

        [Header(Ambient)]
        _AmbientTopColor("Ambient Top Color", Color) = (0.58, 0.62, 0.66, 1)
        _AmbientBottomColor("Ambient Bottom Color", Color) = (0.17, 0.14, 0.12, 1)
        _AmbientIntensity("Ambient Intensity", Range(0.0, 4.0)) = 1.05

        [HideInInspector] _FrameUvRect("Frame UV Rect", Vector) = (0, 0, 1, 1)
        [HideInInspector] _SpriteFlipX("Sprite Flip X", Float) = 1
        [HideInInspector] _UseNormalMap("Use Normal Map", Float) = 0
        [HideInInspector] _UseThicknessMap("Use Thickness Map", Float) = 0
        [HideInInspector] _UsePackedMasks("Use Packed Masks", Float) = 0
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
    TEXTURE2D(_ThicknessMap);
    SAMPLER(sampler_ThicknessMap);
    TEXTURE2D(_PackedMasks);
    SAMPLER(sampler_PackedMasks);

    CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _TransmissionColor;
    float4 _AmbientTopColor;
    float4 _AmbientBottomColor;
    float4 _RimColor;
    float4 _BaseMap_ST;
    float4 _NormalMap_ST;
    float4 _ThicknessMap_ST;
    float4 _PackedMasks_ST;
    float4 _FrameUvRect;
    float _AlphaCutoff;
    float _CoverageSoftness;
    float _NormalScale;
    float _DetailNormalInfluence;
    float _MacroNormalBend;
    float _WrapDiffuse;
    float _WingShadowStrength;
    float _BodyShadowStrength;
    float _SurfaceRoughness;
    float _SpecularStrength;
    float _RimStrength;
    float _RimPower;
    float _TransmissionStrength;
    float _TransmissionPower;
    float _WingTransmissionBoost;
    float _AmbientIntensity;
    float _SpriteFlipX;
    float _UseNormalMap;
    float _UseThicknessMap;
    float _UsePackedMasks;
    CBUFFER_END

    float _ManualLightCount;
    float4 _ManualLightPositionWS[4];
    float4 _ManualLightDirectionWS[4];
    float4 _ManualLightColor[4];
    float4 _ManualLightData0[4];
    float4 _ManualLightData1[4];

    float2 LocalFrameUv(float2 atlasUv)
    {
        return saturate((atlasUv - _FrameUvRect.xy) / max(_FrameUvRect.zw, float2(0.0001, 0.0001)));
    }

    float4 SampleBase(float2 uv)
    {
        return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(uv, _BaseMap));
    }

    float4 SamplePackedMasks(float2 uv)
    {
        if (_UsePackedMasks < 0.5)
        {
            return float4(0.35, 0.45, 0.08, 1.0);
        }

        return SAMPLE_TEXTURE2D(_PackedMasks, sampler_PackedMasks, TRANSFORM_TEX(uv, _PackedMasks));
    }

    float SampleThickness(float2 uv, float wingMask, float edgeMask)
    {
        float thickness = 0.25 + wingMask * 0.55 + edgeMask * 0.18;
        if (_UseThicknessMap > 0.5)
        {
            thickness = SAMPLE_TEXTURE2D(_ThicknessMap, sampler_ThicknessMap, TRANSFORM_TEX(uv, _ThicknessMap)).r;
        }

        return saturate(thickness);
    }

    float3 UnpackBirdNormal(float4 packedNormal)
    {
        float3 normalTS = packedNormal.xyz * 2.0 - 1.0;
        normalTS.xy *= _NormalScale;
        normalTS.z = sqrt(saturate(1.0 - dot(normalTS.xy, normalTS.xy)));
        return normalize(normalTS);
    }

    float3 BuildSurfaceNormalTS(float2 uv)
    {
        float2 localUv = LocalFrameUv(uv);
        float macroX = (localUv.x * 2.0 - 1.0) * _MacroNormalBend * _SpriteFlipX;
        float macroY = (localUv.y * 2.0 - 1.0) * _MacroNormalBend * 0.25;
        float3 macroNormal = normalize(float3(macroX, macroY, 1.0));
        if (_UseNormalMap < 0.5)
        {
            return macroNormal;
        }

        float3 detailNormal = UnpackBirdNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(uv, _NormalMap)));
        detailNormal.x *= _SpriteFlipX;
        float3 combined;
        combined.xy = macroNormal.xy + detailNormal.xy;
        combined.z = macroNormal.z * detailNormal.z;
        return normalize(lerp(macroNormal, combined, _DetailNormalInfluence));
    }

    float ComputeAlpha(float baseAlpha)
    {
        float alpha = saturate(baseAlpha * _BaseColor.a);
        float softAlpha = alpha;
        if (_CoverageSoftness > 0.0001)
        {
            softAlpha = saturate((alpha - _AlphaCutoff) / max(_CoverageSoftness, 0.0001));
        }

        GENERIC_ALPHA_TEST(alpha, _AlphaCutoff);
        return softAlpha;
    }

    float3 EvaluateLighting(float3 albedo, float3 normalWS, float3 viewWS, float3 hitPositionWS, float2 uv, float4 masks)
    {
        float wingMask = masks.r;
        float bodyMask = masks.g;
        float edgeMask = masks.b;
        float thickness = SampleThickness(uv, wingMask, edgeMask);
        float roughness = saturate(_SurfaceRoughness);
        float selfShadow = saturate(1.0 - bodyMask * _BodyShadowStrength * 0.32 - wingMask * _WingShadowStrength * 0.18);
        float verticalAmbient = saturate(normalWS.y * 0.5 + 0.5);
        float3 lighting = lerp(_AmbientBottomColor.rgb, _AmbientTopColor.rgb, verticalAmbient) * _AmbientIntensity * albedo * selfShadow;
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

            float signedNDotL = dot(normalWS, lightDirectionWS);
            float wrappedDiffuse = saturate((abs(signedNDotL) + _WrapDiffuse) / (1.0 + _WrapDiffuse));
            float diffuse = wrappedDiffuse * selfShadow;

            float3 specularNormalWS = signedNDotL >= 0.0 ? normalWS : -normalWS;
            float3 halfwayDirection = normalize(lightDirectionWS + viewWS);
            float nDotH = saturate(dot(specularNormalWS, halfwayDirection));
            float specularPower = lerp(72.0, 10.0, roughness);
            float specular = pow(nDotH, specularPower) * saturate(abs(signedNDotL)) * _SpecularStrength * (0.45 + bodyMask);

            float backlight = pow(saturate(dot(-lightDirectionWS, viewWS)), _TransmissionPower);
            float transmission = backlight * thickness * _TransmissionStrength * (1.0 + wingMask * _WingTransmissionBoost);
            float3 transmitted = _TransmissionColor.rgb * albedo * transmission;

            lighting += lightColor * attenuation * (albedo * diffuse + specular + transmitted);
        }

        float rim = pow(1.0 - nDotV, _RimPower) * _RimStrength * (1.0 + edgeMask * 0.8);
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
        float4 baseSample = SampleBase(uv);
        float alpha = ComputeAlpha(baseSample.a);
        float4 masks = SamplePackedMasks(uv);

        ZERO_BUILTIN_INITIALIZE(builtinData);
        builtinData.opacity = alpha;
        builtinData.alphaClipTreshold = _AlphaCutoff;

        float3x3 tangentToWorld = float3x3(
            normalize(input.tangentToWorld[0].xyz),
            normalize(input.tangentToWorld[1].xyz),
            normalize(input.tangentToWorld[2].xyz));
        float3 normalTS = BuildSurfaceNormalTS(uv);
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
        surfaceData.color = EvaluateLighting(baseSample.rgb * _BaseColor.rgb, normalWS, viewDirectionWS, hitPositionWS, uv, masks);
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
