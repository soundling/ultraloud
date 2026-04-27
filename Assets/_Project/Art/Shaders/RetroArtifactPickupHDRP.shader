Shader "Ultraloud/Pickups/Artifact Pickup HDRP"
{
    Properties
    {
        [PerRendererData] [MainTexture] _MainTex("Sprite Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Header(Cutout)]
        _AlphaCutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.035
        _CoverageSoftness("Coverage Softness", Range(0.0, 0.25)) = 0.025

        [Header(Artifact Glow)]
        _EmissionColor("Emission Color", Color) = (0.42, 0.65, 1.0, 1.0)
        _EmissionStrength("Emission Strength", Range(0.0, 8.0)) = 2.4
        _ArtifactGlowStrength("Artifact Glow Strength", Range(0.0, 8.0)) = 2.4
        _PulseSpeed("Pulse Speed", Range(0.0, 8.0)) = 2.1
        _PulseAmount("Pulse Amount", Range(0.0, 1.0)) = 0.42
        _ScanlineStrength("Rune Scanline Strength", Range(0.0, 2.0)) = 0.28
        _ScanlineDensity("Rune Scanline Density", Range(4.0, 80.0)) = 34.0
        _RimColor("Rim Color", Color) = (0.38, 0.72, 1.0, 1.0)
        _RimStrength("Rim Strength", Range(0.0, 4.0)) = 0.9
        _RimPower("Rim Power", Range(0.5, 8.0)) = 2.3

        [Header(Fake Lighting)]
        _AmbientColor("Ambient Color", Color) = (0.72, 0.68, 0.62, 1.0)
        _LightColor("Light Color", Color) = (1.0, 0.86, 0.52, 1.0)
        _LightDirection("Fake Light Direction", Vector) = (0.35, 0.65, 0.45, 0.0)
        _WrapDiffuse("Wrap Diffuse", Range(0.0, 1.0)) = 0.54
        _SpecularStrength("Specular Strength", Range(0.0, 3.0)) = 0.75
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

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    float4 _BaseColor;
    float4 _EmissionColor;
    float4 _RimColor;
    float4 _AmbientColor;
    float4 _LightColor;
    float4 _LightDirection;
    float _AlphaCutoff;
    float _CoverageSoftness;
    float _EmissionStrength;
    float _ArtifactGlowStrength;
    float _PulseSpeed;
    float _PulseAmount;
    float _ScanlineStrength;
    float _ScanlineDensity;
    float _RimStrength;
    float _RimPower;
    float _WrapDiffuse;
    float _SpecularStrength;
    CBUFFER_END

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

    float3 BuildSpriteNormal(float2 uv)
    {
        float2 centered = uv * 2.0 - 1.0;
        float3 normalTS = float3(centered.x * 0.34, centered.y * 0.20, 1.0);
        return normalize(normalTS);
    }

    void GetSurfaceAndBuiltinData(
        FragInputs input,
        float3 viewDirectionWS,
        inout PositionInputs posInput,
        out SurfaceData surfaceData,
        out BuiltinData builtinData)
    {
        float2 uv = saturate(input.texCoord0.xy);
        float4 baseSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(uv, _MainTex));
        float alpha = ComputeAlpha(baseSample.a);

        ZERO_BUILTIN_INITIALIZE(builtinData);
        builtinData.opacity = alpha;
        builtinData.alphaClipTreshold = _AlphaCutoff;

        float3x3 tangentToWorld = float3x3(
            normalize(input.tangentToWorld[0].xyz),
            normalize(input.tangentToWorld[1].xyz),
            normalize(input.tangentToWorld[2].xyz));
        float3 normalTS = BuildSpriteNormal(uv);
        float3 normalWS = normalize(
            normalTS.x * tangentToWorld[0]
            + normalTS.y * tangentToWorld[1]
            + normalTS.z * tangentToWorld[2]);

        if (dot(normalWS, viewDirectionWS) < 0.0)
        {
            normalWS = -normalWS;
        }

        float3 lightDirection = normalize(_LightDirection.xyz);
        float wrappedDiffuse = saturate((dot(normalWS, lightDirection) + _WrapDiffuse) / (1.0 + _WrapDiffuse));
        float3 halfwayDirection = normalize(lightDirection + viewDirectionWS);
        float specular = pow(saturate(dot(normalWS, halfwayDirection)), 36.0) * _SpecularStrength;
        float rim = pow(1.0 - saturate(dot(normalWS, viewDirectionWS)), _RimPower) * _RimStrength;

        float pulse = 1.0 + sin(_Time.y * _PulseSpeed + uv.y * 6.2831853) * _PulseAmount;
        float scanlineWave = sin((uv.y + _Time.y * 0.28) * _ScanlineDensity);
        float scanline = smoothstep(0.62, 1.0, scanlineWave * 0.5 + 0.5) * _ScanlineStrength;
        float edgeGlow = smoothstep(0.22, 0.78, length(uv - 0.5) * 1.25);

        float3 albedo = baseSample.rgb * _BaseColor.rgb;
        float3 lit = albedo * (_AmbientColor.rgb * 0.50 + _LightColor.rgb * wrappedDiffuse * 0.70);
        lit += specular.xxx * _LightColor.rgb;
        lit += _RimColor.rgb * rim * alpha;
        lit += _EmissionColor.rgb * alpha * (_ArtifactGlowStrength * pulse * (0.18 + edgeGlow * 0.42) + scanline * _EmissionStrength);

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
