Shader "Ultraloud/Nature/Big Rock HDRP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _HeightMap("Height Map", 2D) = "gray" {}
        _PackedMasks("Packed Masks", 2D) = "white" {}
        _CavityMap("Cavity Map", 2D) = "black" {}

        [Header(Texture Mapping)]
        _UvScale("UV Scale", Vector) = (1, 1, 0, 0)
        _MacroColorA("Macro Color Low", Color) = (0.54, 0.50, 0.44, 1)
        _MacroColorB("Macro Color High", Color) = (0.78, 0.75, 0.67, 1)
        _MacroColorStrength("Macro Color Strength", Range(0.0, 1.0)) = 0.22

        [Header(Shape Shading)]
        _NormalScale("Normal Strength", Range(0.0, 3.0)) = 1.15
        _MacroNormalStrength("Macro Normal Strength", Range(0.0, 2.0)) = 0.35
        _HeightContrast("Height Contrast", Range(0.0, 2.0)) = 0.65
        _CrackDarkening("Crack Darkening", Range(0.0, 2.0)) = 0.95
        _CavityDarkening("Cavity Darkening", Range(0.0, 2.0)) = 0.45
        _EdgeWearBrightness("Edge Wear Brightness", Range(0.0, 2.0)) = 0.45
        _AoStrength("AO Strength", Range(0.0, 2.0)) = 0.85

        [Header(Material)]
        _RoughnessScale("Roughness Scale", Range(0.0, 2.0)) = 1.0
        _SpecularStrength("Specular Strength", Range(0.0, 2.0)) = 0.18
        _Wetness("Wetness", Range(0.0, 1.0)) = 0.0
        _WetnessDarkening("Wetness Darkening", Range(0.0, 1.0)) = 0.35
        _RimStrength("Rim Strength", Range(0.0, 2.0)) = 0.12
        _RimPower("Rim Power", Range(0.5, 8.0)) = 4.0

        [Header(Ambient)]
        _AmbientTopColor("Ambient Top Color", Color) = (0.54, 0.58, 0.62, 1)
        _AmbientBottomColor("Ambient Bottom Color", Color) = (0.12, 0.11, 0.09, 1)
        _AmbientIntensity("Ambient Intensity", Range(0.0, 4.0)) = 1.0

        [HideInInspector] _UseNormalMap("Use Normal Map", Float) = 0
        [HideInInspector] _UseHeightMap("Use Height Map", Float) = 0
        [HideInInspector] _UsePackedMasks("Use Packed Masks", Float) = 0
        [HideInInspector] _UseCavityMap("Use Cavity Map", Float) = 0
    }

    HLSLINCLUDE

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
    #pragma multi_compile_instancing
    #pragma multi_compile _ DOTS_INSTANCING_ON

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

    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);
    TEXTURE2D(_NormalMap);
    SAMPLER(sampler_NormalMap);
    TEXTURE2D(_HeightMap);
    SAMPLER(sampler_HeightMap);
    TEXTURE2D(_PackedMasks);
    SAMPLER(sampler_PackedMasks);
    TEXTURE2D(_CavityMap);
    SAMPLER(sampler_CavityMap);

    CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float4 _MacroColorA;
    float4 _MacroColorB;
    float4 _AmbientTopColor;
    float4 _AmbientBottomColor;
    float4 _BaseMap_ST;
    float4 _NormalMap_ST;
    float4 _HeightMap_ST;
    float4 _PackedMasks_ST;
    float4 _CavityMap_ST;
    float4 _UvScale;
    float _MacroColorStrength;
    float _NormalScale;
    float _MacroNormalStrength;
    float _HeightContrast;
    float _CrackDarkening;
    float _CavityDarkening;
    float _EdgeWearBrightness;
    float _AoStrength;
    float _RoughnessScale;
    float _SpecularStrength;
    float _Wetness;
    float _WetnessDarkening;
    float _RimStrength;
    float _RimPower;
    float _AmbientIntensity;
    float _UseNormalMap;
    float _UseHeightMap;
    float _UsePackedMasks;
    float _UseCavityMap;
    CBUFFER_END

    float _ManualLightCount;
    float4 _ManualLightPositionWS[4];
    float4 _ManualLightDirectionWS[4];
    float4 _ManualLightColor[4];
    float4 _ManualLightData0[4];
    float4 _ManualLightData1[4];

    float2 ResolveUv(float2 uv)
    {
        return uv * max(_UvScale.xy, float2(0.001, 0.001));
    }

    float3 UnpackRockNormal(float4 packedNormal)
    {
        float3 normalTS = packedNormal.xyz * 2.0 - 1.0;
        normalTS.xy *= _NormalScale;
        normalTS.z = sqrt(saturate(1.0 - dot(normalTS.xy, normalTS.xy)));
        return normalize(normalTS);
    }

    float3 BuildNormalTS(float2 uv)
    {
        float2 tiledUv = frac(ResolveUv(uv));
        float heightValue = _UseHeightMap > 0.5
            ? SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, TRANSFORM_TEX(tiledUv, _HeightMap)).r
            : 0.5;
        float2 macroSlope = (tiledUv - 0.5) * _MacroNormalStrength * (heightValue - 0.5);
        float3 macroNormal = normalize(float3(macroSlope.x, macroSlope.y, 1.0));
        if (_UseNormalMap < 0.5)
        {
            return macroNormal;
        }

        float3 detailNormal = UnpackRockNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(tiledUv, _NormalMap)));
        float3 combined;
        combined.xy = macroNormal.xy + detailNormal.xy;
        combined.z = macroNormal.z * detailNormal.z;
        return normalize(combined);
    }

    float4 SamplePackedMasks(float2 tiledUv)
    {
        if (_UsePackedMasks < 0.5)
        {
            return float4(1.0, 0.8, 0.0, 0.0);
        }

        return SAMPLE_TEXTURE2D(_PackedMasks, sampler_PackedMasks, TRANSFORM_TEX(tiledUv, _PackedMasks));
    }

    float3 EvaluateLighting(float3 albedo, float3 normalWS, float3 viewWS, float3 hitPositionWS, float2 uv)
    {
        float2 tiledUv = frac(ResolveUv(uv));
        float4 masks = SamplePackedMasks(tiledUv);
        float ao = lerp(1.0, masks.r, _AoStrength);
        float roughness = saturate(masks.g * _RoughnessScale);
        float edgeWear = masks.b;
        float cracks = masks.a;
        float cavity = _UseCavityMap > 0.5
            ? SAMPLE_TEXTURE2D(_CavityMap, sampler_CavityMap, TRANSFORM_TEX(tiledUv, _CavityMap)).r
            : cracks;
        float heightValue = _UseHeightMap > 0.5
            ? SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, TRANSFORM_TEX(tiledUv, _HeightMap)).r
            : 0.5;

        float macroTone = saturate((heightValue - 0.5) * _HeightContrast + 0.5);
        albedo = lerp(albedo, albedo * lerp(_MacroColorA.rgb, _MacroColorB.rgb, macroTone), _MacroColorStrength);
        albedo *= saturate(1.0 - cracks * _CrackDarkening * 0.55);
        albedo *= saturate(1.0 - cavity * _CavityDarkening * 0.4);
        albedo += edgeWear * _EdgeWearBrightness * 0.16;
        albedo *= lerp(1.0, 1.0 - _WetnessDarkening, _Wetness);

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
            float selfShadow = saturate(1.0 - cracks * 0.38 - cavity * 0.22);
            float diffuse = nDotL * selfShadow * ao;
            float3 halfwayDirection = normalize(lightDirectionWS + viewWS);
            float nDotH = saturate(dot(normalWS, halfwayDirection));
            float specularPower = lerp(96.0, 8.0, saturate(roughness + _Wetness * -0.35));
            float specular = pow(nDotH, specularPower) * nDotL * _SpecularStrength * lerp(1.0, 2.2, _Wetness);
            lighting += lightColor * attenuation * (albedo * diffuse + specular);
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
        float2 tiledUv = frac(ResolveUv(uv));
        float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(tiledUv, _BaseMap));

        ZERO_BUILTIN_INITIALIZE(builtinData);
        builtinData.opacity = 1.0;

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
        surfaceData.normalWS = normalWS;
        surfaceData.color = EvaluateLighting(baseSample.rgb * _BaseColor.rgb, normalWS, viewDirectionWS, hitPositionWS, uv);
    }

    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode" = "DepthForwardOnly" }

            Cull Back
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

            Cull Back
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

            Cull Back
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
