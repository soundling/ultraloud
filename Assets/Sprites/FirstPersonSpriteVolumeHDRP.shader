Shader "Ultraloud/First Person/Sprite Volume HDRP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseColorMap("Base Color Map", 2D) = "white" {}
        _NormalMap("Normal", 2D) = "bump" {}
        _HeightMap("Front Depth", 2D) = "black" {}
        [HDR] _EmissiveColor("Emissive Color", Color) = (0, 0, 0, 1)
        _EmissiveColorMap("Emissive Map", 2D) = "black" {}

        [Header(Shape Tricks)]
        _VolumeThickness("Depth Offset Thickness", Range(0.0, 0.2)) = 0.06
        _ParallaxScale("Parallax Scale", Range(0.0, 0.08)) = 0.012
        [ToggleUI] _InvertFrontDepth("Invert Front Depth", Float) = 0

        [Header(Shading)]
        _NormalScale("Normal Strength", Range(0.0, 2.0)) = 1.0
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.08
        [ToggleUI] _PreserveBaseCoverage("Preserve Base Coverage", Float) = 1
        _CoverageThreshold("Coverage Threshold", Range(0.0, 0.2)) = 0.02
        _SelfShadowStrength("Cheap Self Shadow", Range(0.0, 1.0)) = 0.25
        _TransmissionStrength("Backlight Transmission", Range(0.0, 4.0)) = 0.2
        _AmbientOcclusionStrength("AO Strength", Range(0.0, 4.0)) = 1.0
        _SpecularStrength("Specular Strength", Range(0.0, 4.0)) = 1.2
        _MaterialAmbientOcclusion("Ambient Occlusion", Range(0.0, 1.0)) = 1.0
        _MaterialRoughness("Roughness", Range(0.0, 1.0)) = 0.55
        _MaterialMetallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MaterialThickness("Thickness", Range(0.0, 1.0)) = 0.65
        _AmbientColor("Ambient Color", Color) = (0.48, 0.52, 0.58, 1)
        _AmbientIntensity("Ambient Intensity", Range(0.0, 4.0)) = 1.0

        [HideInInspector] _UseFrontDepth("Use Front Depth", Float) = 0
        [HideInInspector] _UseNormalMap("Use Normal Map", Float) = 0
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
    TEXTURE2D(_HeightMap);
    SAMPLER(sampler_HeightMap);
    TEXTURE2D(_EmissiveColorMap);
    SAMPLER(sampler_EmissiveColorMap);

    CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _EmissiveColor;
    float4 _AmbientColor;
    float4 _BaseColorMap_ST;
    float4 _NormalMap_ST;
    float4 _HeightMap_ST;
    float4 _EmissiveColorMap_ST;
    float _VolumeThickness;
    float _ParallaxScale;
    float _InvertFrontDepth;
    float _NormalScale;
    float _AlphaCutoff;
    float _PreserveBaseCoverage;
    float _CoverageThreshold;
    float _SelfShadowStrength;
    float _TransmissionStrength;
    float _AmbientOcclusionStrength;
    float _SpecularStrength;
    float _MaterialAmbientOcclusion;
    float _MaterialRoughness;
    float _MaterialMetallic;
    float _MaterialThickness;
    float _AmbientIntensity;
    float _UseFrontDepth;
    float _UseNormalMap;
    float _UseEmissiveMap;
    CBUFFER_END

    float _ManualLightCount;
    float4 _ManualLightPositionWS[4];
    float4 _ManualLightDirectionWS[4];
    float4 _ManualLightColor[4];
    float4 _ManualLightData0[4];
    float4 _ManualLightData1[4];

    float4 SampleBaseColor(float2 uv)
    {
        return SAMPLE_TEXTURE2D(_BaseColorMap, sampler_BaseColorMap, TRANSFORM_TEX(uv, _BaseColorMap));
    }

    float DecodeFrontDepth(float depthSample)
    {
        float decoded = saturate(depthSample);
        if (_InvertFrontDepth > 0.5)
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
        return DecodeFrontDepth(sampledDepth);
    }

    float3 UnpackSpriteNormal(float4 packedNormal)
    {
        float3 normalTS = packedNormal.xyz * 2.0 - 1.0;
        normalTS.xy *= _NormalScale;
        normalTS.z = sqrt(saturate(1.0 - dot(normalTS.xy, normalTS.xy)));
        return normalize(normalTS);
    }

    float3 SampleSurfaceNormalTS(float2 uv)
    {
        if (_UseNormalMap < 0.5)
        {
            return float3(0.0, 0.0, 1.0);
        }

        float4 packedNormal = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(uv, _NormalMap));
        return UnpackSpriteNormal(packedNormal);
    }

    float ComputeAlpha(float baseAlpha)
    {
        return saturate(baseAlpha * _BaseColor.a);
    }

    float2 ApplyCheapParallax(float2 baseUv, float3 viewDirectionTS, out float depthValue)
    {
        depthValue = SampleFrontDepth(baseUv);
        if (_UseFrontDepth < 0.5 || _ParallaxScale <= 0.0001)
        {
            return baseUv;
        }

        float2 viewOffset = viewDirectionTS.xy / max(abs(viewDirectionTS.z), 0.18);
        return baseUv - viewOffset * (_ParallaxScale * depthValue);
    }

    float CheapSelfShadow(float depthValue, float nDotL)
    {
        float recess = _UseFrontDepth > 0.5 ? saturate(depthValue) : 0.0;
        float grazing = saturate(1.0 - nDotL);
        return saturate(1.0 - _SelfShadowStrength * recess * grazing);
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
        float2 hitUv,
        float depthValue)
    {
        float ao = saturate(1.0 - (1.0 - _MaterialAmbientOcclusion) * _AmbientOcclusionStrength);
        float roughness = clamp(_MaterialRoughness, 0.04, 1.0);
        float metalness = saturate(_MaterialMetallic);
        float thickness = saturate(_MaterialThickness);
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

            float selfShadow = CheapSelfShadow(depthValue, nDotL);
            float wrap = 0.2;
            float wrappedDiffuse = saturate((dot(normalWS, lightDirectionWS) + wrap) / (1.0 + wrap));
            float3 halfwayDirection = normalize(lightDirectionWS + viewWS);
            float nDotH = saturate(dot(normalWS, halfwayDirection));
            float vDotH = saturate(dot(viewWS, halfwayDirection));
            float distribution = DistributionGGX(nDotH, roughness);
            float geometry = GeometrySmith(nDotV, nDotL, roughness);
            float3 fresnel = FresnelSchlick(vDotH, f0);
            float3 specular = distribution * geometry * fresnel / max(4.0 * nDotV * nDotL, 0.001);

            lighting += lightColor * attenuation * selfShadow * (diffuseColor * wrappedDiffuse + specular);

            float backLighting = saturate(-dot(normalWS, lightDirectionWS));
            lighting += lightColor * attenuation * backLighting * thickness * _TransmissionStrength * diffuseColor * 0.5;
        }

        float rim = Pow4(1.0 - nDotV) * saturate(1.0 - roughness * 0.65) * _SpecularStrength * 0.08;
        lighting += albedo * rim;

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

        float4 flatBaseSample = SampleBaseColor(baseUv);
        float flatAlpha = ComputeAlpha(flatBaseSample.a);
        float depthValue = 0.0;
        float2 hitUv = ApplyCheapParallax(baseUv, viewDirectionTS, depthValue);
        float4 baseSample = flatBaseSample;
        float alpha = flatAlpha;

        if (hitUv.x >= 0.0 && hitUv.x <= 1.0 && hitUv.y >= 0.0 && hitUv.y <= 1.0)
        {
            baseSample = SampleBaseColor(hitUv);
            alpha = ComputeAlpha(baseSample.a);
            if (_PreserveBaseCoverage > 0.5 && alpha <= _CoverageThreshold && flatAlpha > alpha)
            {
                hitUv = baseUv;
                baseSample = flatBaseSample;
                alpha = flatAlpha;
                depthValue = 0.0;
            }
        }
        else
        {
            hitUv = baseUv;
            depthValue = 0.0;
        }

        if (_PreserveBaseCoverage > 0.5)
        {
            alpha = max(alpha, flatAlpha);
        }

        ZERO_BUILTIN_INITIALIZE(builtinData);
        builtinData.opacity = alpha;
        builtinData.alphaClipTreshold = _AlphaCutoff;

        GENERIC_ALPHA_TEST(alpha, _AlphaCutoff);

        float3 normalTS = SampleSurfaceNormalTS(hitUv);
        float3 normalWS = normalize(
            normalTS.x * tangentToWorld[0]
            + normalTS.y * tangentToWorld[1]
            + normalTS.z * tangentToWorld[2]);

        float depthOffset = (_UseFrontDepth > 0.5) ? depthValue * _VolumeThickness : 0.0;
        float3 hitPositionWS = GetAbsolutePositionWS(input.positionRWS + depthOffset * (-viewDirectionWS));
        ApplyDepthOffsetPositionInput(viewDirectionWS, depthOffset, GetViewForwardDir(), GetWorldToHClipMatrix(), posInput);
        builtinData.depthOffset = depthOffset;

        surfaceData.normalWS = normalWS;
        surfaceData.color = EvaluateLighting(
            baseSample.rgb * _BaseColor.rgb,
            normalWS,
            viewDirectionWS,
            hitPositionWS,
            hitUv,
            depthValue);
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
