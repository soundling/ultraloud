Shader "Ultraloud/Effects/Gore Sprite HDRP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Atlas", 2D) = "white" {}
        _NormalMap("Normal Atlas", 2D) = "bump" {}
        _PackedMasks("Packed Masks", 2D) = "white" {}
        _EmissionMap("Emission Atlas", 2D) = "black" {}

        [Header(Cutout)]
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.04
        _CoverageSoftness("Coverage Softness", Range(0.0, 0.25)) = 0.035

        [Header(Wet Shading)]
        _NormalScale("Normal Strength", Range(0.0, 3.0)) = 1.2
        _WetSpecular("Wet Specular", Range(0.0, 4.0)) = 1.1
        _RimStrength("Rim Strength", Range(0.0, 3.0)) = 0.35
        _RimPower("Rim Power", Range(0.5, 8.0)) = 2.7
        _EmissionStrength("Emission Strength", Range(0.0, 4.0)) = 0.35
        _AmbientColor("Ambient Color", Color) = (0.55, 0.48, 0.42, 1)
        _LightDirection("Fake Light Direction", Vector) = (0.35, 0.65, 0.45, 0)

        [HideInInspector] _FrameUvRect("Frame UV Rect", Vector) = (0, 0, 1, 1)
        [HideInInspector] _UseNormalMap("Use Normal Map", Float) = 0
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
    TEXTURE2D(_PackedMasks);
    SAMPLER(sampler_PackedMasks);
    TEXTURE2D(_EmissionMap);
    SAMPLER(sampler_EmissionMap);

    CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _AmbientColor;
    float4 _LightDirection;
    float4 _BaseMap_ST;
    float4 _NormalMap_ST;
    float4 _PackedMasks_ST;
    float4 _EmissionMap_ST;
    float4 _FrameUvRect;
    float _AlphaCutoff;
    float _CoverageSoftness;
    float _NormalScale;
    float _WetSpecular;
    float _RimStrength;
    float _RimPower;
    float _EmissionStrength;
    float _UseNormalMap;
    float _UsePackedMasks;
    float _UseEmissionMap;
    CBUFFER_END

    float2 LocalFrameUv(float2 atlasUv)
    {
        return saturate((atlasUv - _FrameUvRect.xy) / max(_FrameUvRect.zw, float2(0.0001, 0.0001)));
    }

    float3 UnpackGoreNormal(float4 packedNormal)
    {
        float3 normalTS = packedNormal.xyz * 2.0 - 1.0;
        normalTS.xy *= _NormalScale;
        normalTS.z = sqrt(saturate(1.0 - dot(normalTS.xy, normalTS.xy)));
        return normalize(normalTS);
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

    void GetSurfaceAndBuiltinData(
        FragInputs input,
        float3 viewDirectionWS,
        inout PositionInputs posInput,
        out SurfaceData surfaceData,
        out BuiltinData builtinData)
    {
        float2 uv = input.texCoord0.xy;
        float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(uv, _BaseMap));
        float alpha = ComputeAlpha(baseSample.a);

        ZERO_BUILTIN_INITIALIZE(builtinData);
        builtinData.opacity = alpha;
        builtinData.alphaClipTreshold = _AlphaCutoff;

        float3 normalTS = float3((LocalFrameUv(uv).x * 2.0 - 1.0) * 0.25, 0.0, 1.0);
        if (_UseNormalMap > 0.5)
        {
            normalTS = UnpackGoreNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(uv, _NormalMap)));
        }

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

        float4 masks = _UsePackedMasks > 0.5
            ? SAMPLE_TEXTURE2D(_PackedMasks, sampler_PackedMasks, TRANSFORM_TEX(uv, _PackedMasks))
            : float4(0.75, 0.2, 0.0, 1.0);
        float wet = masks.r;
        float bone = masks.b;

        float3 lightDir = normalize(_LightDirection.xyz);
        float nDotL = saturate(dot(normalWS, lightDir) * 0.5 + 0.5);
        float nDotV = saturate(dot(normalWS, viewDirectionWS));
        float3 halfDir = normalize(lightDir + viewDirectionWS);
        float specular = pow(saturate(dot(normalWS, halfDir)), lerp(18.0, 78.0, wet)) * _WetSpecular * (0.25 + wet);
        float rim = pow(1.0 - nDotV, _RimPower) * _RimStrength;
        float3 emission = _UseEmissionMap > 0.5
            ? SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, TRANSFORM_TEX(uv, _EmissionMap)).rgb * _EmissionStrength
            : 0.0;

        float3 albedo = baseSample.rgb * _BaseColor.rgb;
        float3 lit = albedo * (_AmbientColor.rgb * 0.55 + nDotL * 0.72);
        lit += specular.xxx * lerp(float3(1.0, 0.45, 0.35), float3(1.0, 0.95, 0.75), bone);
        lit += rim.xxx * lerp(float3(0.65, 0.04, 0.03), float3(1.0, 0.84, 0.58), bone);
        lit += emission;

        surfaceData.normalWS = normalWS;
        surfaceData.color = lit;
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
    }
}
