using System;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class FirstPersonSpriteVolumeRenderer : MonoBehaviour
{
    private const string ShaderName = "Ultraloud/First Person/Sprite Volume HDRP";
    private const int MaxShaderLights = 4;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int DetailNormalMapId = Shader.PropertyToID("_NormalMap");
    private static readonly int MacroNormalMapId = Shader.PropertyToID("_MacroNormalMap");
    private static readonly int FrontDepthMapId = Shader.PropertyToID("_HeightMap");
    private static readonly int BackDepthMapId = Shader.PropertyToID("_BackDepthMap");
    private static readonly int ThicknessMapId = Shader.PropertyToID("_ThicknessMap");
    private static readonly int PackedMaskMapId = Shader.PropertyToID("_MaskMap");
    private static readonly int ShellAtlasId = Shader.PropertyToID("_ShellOccupancyAtlas");
    private static readonly int SdfMapId = Shader.PropertyToID("_SdfMap");
    private static readonly int EmissiveMapId = Shader.PropertyToID("_EmissiveColorMap");
    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
    private static readonly int VolumeThicknessId = Shader.PropertyToID("_VolumeThickness");
    private static readonly int ParallaxScaleId = Shader.PropertyToID("_ParallaxScale");
    private static readonly int RaymarchStepsId = Shader.PropertyToID("_RaymarchSteps");
    private static readonly int ShadowStepsId = Shader.PropertyToID("_ShadowSteps");
    private static readonly int ShellSliceCountId = Shader.PropertyToID("_ShellSliceCount");
    private static readonly int ShellAtlasGridId = Shader.PropertyToID("_ShellAtlasGrid");
    private static readonly int InvertFrontDepthId = Shader.PropertyToID("_InvertFrontDepth");
    private static readonly int InvertBackDepthId = Shader.PropertyToID("_InvertBackDepth");
    private static readonly int AutoCorrectDualDepthId = Shader.PropertyToID("_AutoCorrectDualDepth");
    private static readonly int MinimumDepthSeparationId = Shader.PropertyToID("_MinimumDepthSeparation");
    private static readonly int DetailNormalScaleId = Shader.PropertyToID("_NormalScale");
    private static readonly int MacroNormalScaleId = Shader.PropertyToID("_MacroNormalScale");
    private static readonly int AlphaCutoffId = Shader.PropertyToID("_AlphaCutoff");
    private static readonly int PreserveBaseCoverageId = Shader.PropertyToID("_PreserveBaseCoverage");
    private static readonly int CoverageThresholdId = Shader.PropertyToID("_CoverageThreshold");
    private static readonly int SelfShadowStrengthId = Shader.PropertyToID("_SelfShadowStrength");
    private static readonly int TransmissionStrengthId = Shader.PropertyToID("_TransmissionStrength");
    private static readonly int AoStrengthId = Shader.PropertyToID("_AmbientOcclusionStrength");
    private static readonly int SpecularStrengthId = Shader.PropertyToID("_SpecularStrength");
    private static readonly int SdfSoftnessId = Shader.PropertyToID("_SdfSoftness");
    private static readonly int AmbientColorId = Shader.PropertyToID("_AmbientColor");
    private static readonly int AmbientIntensityId = Shader.PropertyToID("_AmbientIntensity");
    private static readonly int UseFrontDepthId = Shader.PropertyToID("_UseFrontDepth");
    private static readonly int UseBackDepthId = Shader.PropertyToID("_UseBackDepth");
    private static readonly int UseThicknessId = Shader.PropertyToID("_UseThicknessMap");
    private static readonly int UseMaskMapId = Shader.PropertyToID("_UseMaskMap");
    private static readonly int UseShellAtlasId = Shader.PropertyToID("_UseShellAtlas");
    private static readonly int UseSdfId = Shader.PropertyToID("_UseSdf");
    private static readonly int UseDetailNormalId = Shader.PropertyToID("_UseDetailNormal");
    private static readonly int UseMacroNormalId = Shader.PropertyToID("_UseMacroNormal");
    private static readonly int UseEmissiveMapId = Shader.PropertyToID("_UseEmissiveMap");
    private static readonly int ManualLightCountId = Shader.PropertyToID("_ManualLightCount");
    private static readonly int ManualLightPositionsId = Shader.PropertyToID("_ManualLightPositionWS");
    private static readonly int ManualLightDirectionsId = Shader.PropertyToID("_ManualLightDirectionWS");
    private static readonly int ManualLightColorsId = Shader.PropertyToID("_ManualLightColor");
    private static readonly int ManualLightData0Id = Shader.PropertyToID("_ManualLightData0");
    private static readonly int ManualLightData1Id = Shader.PropertyToID("_ManualLightData1");

    [SerializeField] private MeshRenderer targetRenderer;
    [SerializeField] private FirstPersonSpriteVolumeMapSet mapSet;
    [SerializeField] private Transform lightAnchor;
    [Header("Scene Lights")]
    [SerializeField] private bool refreshLightsInEditor = true;
    [SerializeField, Range(1, MaxShaderLights)] private int maxLights = MaxShaderLights;
    [SerializeField, Min(0.05f)] private float lightRefreshInterval = 0.15f;
    [SerializeField, Min(0f)] private float directionalLightScale = 0.00001f;
    [SerializeField, Min(0f)] private float punctualLightScale = 0.01f;
    [SerializeField, Min(0f)] private float maxNormalizedLightIntensity = 1.5f;
    [Header("Ambient")]
    [SerializeField] private Color ambientColor = new(0.48f, 0.52f, 0.58f, 1f);
    [SerializeField, Min(0f)] private float ambientIntensity = 1f;
    [SerializeField] private bool includeRenderSettingsAmbient;
    [SerializeField, Min(0f)] private float renderSettingsAmbientScale = 0.25f;

    private readonly Vector4[] manualLightPositions = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightDirections = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightColors = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightData0 = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightData1 = new Vector4[MaxShaderLights];
    private readonly Light[] bestLights = new Light[MaxShaderLights];
    private readonly float[] bestLightScores = new float[MaxShaderLights];

    private MaterialPropertyBlock propertyBlock;
    private Material originalSharedMaterial;
    private Material runtimeMaterial;
    private double nextLightRefreshTime = double.NegativeInfinity;
    private int cachedLightCount;

    public FirstPersonSpriteVolumeMapSet MapSet
    {
        get => mapSet;
        set
        {
            mapSet = value;
            ApplyNow(forceLightRefresh: true);
        }
    }

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
        EnsureRuntimeMaterial();
        ApplyNow(forceLightRefresh: true);
    }

    private void OnEnable()
    {
        AutoAssignReferences();
        EnsureRuntimeMaterial();
        ApplyNow(forceLightRefresh: true);
    }

    private void OnValidate()
    {
        maxLights = Mathf.Clamp(maxLights, 1, MaxShaderLights);
        lightRefreshInterval = Mathf.Max(0.05f, lightRefreshInterval);
        directionalLightScale = Mathf.Max(0f, directionalLightScale);
        punctualLightScale = Mathf.Max(0f, punctualLightScale);
        maxNormalizedLightIntensity = Mathf.Max(0f, maxNormalizedLightIntensity);
        ambientIntensity = Mathf.Max(0f, ambientIntensity);
        renderSettingsAmbientScale = Mathf.Max(0f, renderSettingsAmbientScale);

        AutoAssignReferences();
        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureRuntimeMaterial();
        ApplyNow(forceLightRefresh: true);
    }

    private void LateUpdate()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureRuntimeMaterial();
        ApplyNow(forceLightRefresh: false);
    }

    private void OnDisable()
    {
        ClearPropertyBlock();
        RestoreOriginalMaterial();
    }

    private void OnDestroy()
    {
        RestoreOriginalMaterial();
    }

    public void ApplyNow(bool forceLightRefresh)
    {
        if (targetRenderer == null || !EnsureRuntimeMaterial())
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        propertyBlock.Clear();

        Material sourceMaterial = originalSharedMaterial != null ? originalSharedMaterial : targetRenderer.sharedMaterial;
        Texture baseColor = ResolveTexture(mapSet != null ? mapSet.baseColor : null, sourceMaterial, BaseColorMapId, MainTexId);
        Texture detailNormal = ResolveTexture(mapSet != null ? mapSet.detailNormal : null, sourceMaterial, DetailNormalMapId);
        Texture macroNormal = ResolveTexture(mapSet != null ? mapSet.macroNormal : null, sourceMaterial, MacroNormalMapId);
        Texture frontDepth = ResolveTexture(mapSet != null ? mapSet.frontDepth : null, sourceMaterial, FrontDepthMapId);
        Texture backDepth = ResolveTexture(mapSet != null ? mapSet.backDepth : null, sourceMaterial, BackDepthMapId);
        Texture thickness = ResolveTexture(mapSet != null ? mapSet.thickness : null, sourceMaterial, ThicknessMapId);
        Texture packedMasks = ResolveTexture(mapSet != null ? mapSet.packedMasks : null, sourceMaterial, PackedMaskMapId);
        Texture shellAtlas = ResolveTexture(mapSet != null ? mapSet.shellOccupancyAtlas : null, sourceMaterial, ShellAtlasId);
        Texture sdf = ResolveTexture(mapSet != null ? mapSet.sdf : null, sourceMaterial, SdfMapId);
        Texture emissive = ResolveTexture(mapSet != null ? mapSet.emissive : null, sourceMaterial, EmissiveMapId);

        Color baseTint = mapSet != null ? mapSet.baseColorTint : ResolveColor(sourceMaterial, BaseColorId, Color.white);
        Color emissiveTint = mapSet != null ? mapSet.emissiveColor : ResolveColor(sourceMaterial, EmissiveColorId, Color.black);

        SetTextureIfPresent(propertyBlock, BaseColorMapId, baseColor);
        SetTextureIfPresent(propertyBlock, DetailNormalMapId, detailNormal);
        SetTextureIfPresent(propertyBlock, MacroNormalMapId, macroNormal);
        SetTextureIfPresent(propertyBlock, FrontDepthMapId, frontDepth);
        SetTextureIfPresent(propertyBlock, BackDepthMapId, backDepth);
        SetTextureIfPresent(propertyBlock, ThicknessMapId, thickness);
        SetTextureIfPresent(propertyBlock, PackedMaskMapId, packedMasks);
        SetTextureIfPresent(propertyBlock, ShellAtlasId, shellAtlas);
        SetTextureIfPresent(propertyBlock, SdfMapId, sdf);
        SetTextureIfPresent(propertyBlock, EmissiveMapId, emissive);

        propertyBlock.SetColor(BaseColorId, baseTint);
        propertyBlock.SetColor(EmissiveColorId, emissiveTint);
        propertyBlock.SetFloat(VolumeThicknessId, mapSet != null ? mapSet.volumeThickness : 0.12f);
        propertyBlock.SetFloat(ParallaxScaleId, mapSet != null ? mapSet.parallaxScale : 0.08f);
        propertyBlock.SetFloat(RaymarchStepsId, mapSet != null ? Mathf.Max(1, mapSet.raymarchSteps) : 24f);
        propertyBlock.SetFloat(ShadowStepsId, mapSet != null ? Mathf.Max(1, mapSet.shadowSteps) : 12f);
        propertyBlock.SetFloat(ShellSliceCountId, mapSet != null ? Mathf.Max(1, mapSet.shellSliceCount) : 16f);
        propertyBlock.SetVector(ShellAtlasGridId, mapSet != null ? new Vector4(
            Mathf.Max(1, mapSet.shellAtlasGrid.x),
            Mathf.Max(1, mapSet.shellAtlasGrid.y),
            0f,
            0f) : new Vector4(4f, 4f, 0f, 0f));
        propertyBlock.SetFloat(InvertFrontDepthId, mapSet != null && mapSet.invertFrontDepth ? 1f : 0f);
        propertyBlock.SetFloat(InvertBackDepthId, mapSet != null && mapSet.invertBackDepth ? 1f : 0f);
        propertyBlock.SetFloat(AutoCorrectDualDepthId, mapSet == null || mapSet.autoCorrectDualDepth ? 1f : 0f);
        propertyBlock.SetFloat(MinimumDepthSeparationId, mapSet != null ? mapSet.minimumDepthSeparation : 0.01f);
        propertyBlock.SetFloat(DetailNormalScaleId, mapSet != null ? mapSet.detailNormalScale : 1f);
        propertyBlock.SetFloat(MacroNormalScaleId, mapSet != null ? mapSet.macroNormalScale : 1f);
        propertyBlock.SetFloat(AlphaCutoffId, mapSet != null ? mapSet.alphaCutoff : ResolveFloat(sourceMaterial, AlphaCutoffId, 0.08f));
        propertyBlock.SetFloat(PreserveBaseCoverageId, mapSet == null || mapSet.preserveBaseCoverage ? 1f : 0f);
        propertyBlock.SetFloat(CoverageThresholdId, mapSet != null ? mapSet.coverageThreshold : 0.02f);
        propertyBlock.SetFloat(SelfShadowStrengthId, mapSet != null ? mapSet.selfShadowStrength : 0.6f);
        propertyBlock.SetFloat(TransmissionStrengthId, mapSet != null ? mapSet.transmissionStrength : 0.35f);
        propertyBlock.SetFloat(AoStrengthId, mapSet != null ? mapSet.ambientOcclusionStrength : 1f);
        propertyBlock.SetFloat(SpecularStrengthId, mapSet != null ? mapSet.specularStrength : 1f);
        propertyBlock.SetFloat(SdfSoftnessId, mapSet != null ? mapSet.sdfSoftness : 0.08f);
        propertyBlock.SetFloat(UseFrontDepthId, frontDepth != null ? 1f : 0f);
        propertyBlock.SetFloat(UseBackDepthId, backDepth != null ? 1f : 0f);
        propertyBlock.SetFloat(UseThicknessId, thickness != null ? 1f : 0f);
        propertyBlock.SetFloat(UseMaskMapId, packedMasks != null ? 1f : 0f);
        propertyBlock.SetFloat(UseShellAtlasId, shellAtlas != null ? 1f : 0f);
        propertyBlock.SetFloat(UseSdfId, sdf != null ? 1f : 0f);
        propertyBlock.SetFloat(UseDetailNormalId, detailNormal != null ? 1f : 0f);
        propertyBlock.SetFloat(UseMacroNormalId, macroNormal != null ? 1f : 0f);
        propertyBlock.SetFloat(UseEmissiveMapId, emissive != null ? 1f : 0f);

        Color combinedAmbient = ambientColor.linear;
        if (includeRenderSettingsAmbient)
        {
            combinedAmbient += RenderSettings.ambientLight.linear * renderSettingsAmbientScale;
        }

        propertyBlock.SetColor(AmbientColorId, combinedAmbient);
        propertyBlock.SetFloat(AmbientIntensityId, ambientIntensity);

        if (ShouldRefreshLights(forceLightRefresh))
        {
            RefreshManualLights(ResolveLightReferencePosition());
            nextLightRefreshTime = Time.realtimeSinceStartupAsDouble + lightRefreshInterval;
        }

        propertyBlock.SetFloat(ManualLightCountId, cachedLightCount);
        propertyBlock.SetVectorArray(ManualLightPositionsId, manualLightPositions);
        propertyBlock.SetVectorArray(ManualLightDirectionsId, manualLightDirections);
        propertyBlock.SetVectorArray(ManualLightColorsId, manualLightColors);
        propertyBlock.SetVectorArray(ManualLightData0Id, manualLightData0);
        propertyBlock.SetVectorArray(ManualLightData1Id, manualLightData1);

        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void AutoAssignReferences()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<MeshRenderer>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<MeshRenderer>(true);
        }

        if (lightAnchor == null)
        {
            Transform root = transform.root;
            if (root != null)
            {
                Camera rootCamera = root.GetComponentInChildren<Camera>(true);
                if (rootCamera != null)
                {
                    lightAnchor = rootCamera.transform;
                }
            }
        }

        if (lightAnchor == null && Camera.main != null)
        {
            lightAnchor = Camera.main.transform;
        }
    }

    private bool EnsureRuntimeMaterial()
    {
        if (targetRenderer == null)
        {
            return false;
        }

        Shader volumeShader = Shader.Find(ShaderName);
        if (volumeShader == null)
        {
            return false;
        }

        Material currentShared = targetRenderer.sharedMaterial;
        if (originalSharedMaterial == null && currentShared != runtimeMaterial)
        {
            originalSharedMaterial = currentShared;
        }

        if (runtimeMaterial == null || runtimeMaterial.shader != volumeShader)
        {
            DestroyRuntimeMaterial();

            runtimeMaterial = new Material(volumeShader)
            {
                name = $"{name}_FirstPersonSpriteVolume",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        CopyFallbackMaterialSettings(originalSharedMaterial, runtimeMaterial);

        if (targetRenderer.sharedMaterial != runtimeMaterial)
        {
            targetRenderer.sharedMaterial = runtimeMaterial;
        }

        return true;
    }

    private void RestoreOriginalMaterial()
    {
        if (targetRenderer != null && runtimeMaterial != null && targetRenderer.sharedMaterial == runtimeMaterial)
        {
            targetRenderer.sharedMaterial = originalSharedMaterial;
        }

        DestroyRuntimeMaterial();
    }

    private void DestroyRuntimeMaterial()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeMaterial);
        }
        else
        {
            DestroyImmediate(runtimeMaterial);
        }

        runtimeMaterial = null;
    }

    private void ClearPropertyBlock()
    {
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.SetPropertyBlock(null);
    }

    private static void CopyFallbackMaterialSettings(Material sourceMaterial, Material destinationMaterial)
    {
        if (destinationMaterial == null)
        {
            return;
        }

        if (sourceMaterial != null)
        {
            Texture baseTexture = ResolveTexture(null, sourceMaterial, BaseColorMapId, MainTexId);
            if (baseTexture != null)
            {
                destinationMaterial.SetTexture(BaseColorMapId, baseTexture);
            }

            if (sourceMaterial.HasProperty(BaseColorId))
            {
                destinationMaterial.SetColor(BaseColorId, sourceMaterial.GetColor(BaseColorId));
            }
            else
            {
                destinationMaterial.SetColor(BaseColorId, Color.white);
            }

            if (sourceMaterial.HasProperty(EmissiveMapId))
            {
                destinationMaterial.SetTexture(EmissiveMapId, sourceMaterial.GetTexture(EmissiveMapId));
            }

            if (sourceMaterial.HasProperty(EmissiveColorId))
            {
                destinationMaterial.SetColor(EmissiveColorId, sourceMaterial.GetColor(EmissiveColorId));
            }

            if (sourceMaterial.HasProperty(DetailNormalMapId))
            {
                destinationMaterial.SetTexture(DetailNormalMapId, sourceMaterial.GetTexture(DetailNormalMapId));
            }

            if (sourceMaterial.HasProperty(PackedMaskMapId))
            {
                destinationMaterial.SetTexture(PackedMaskMapId, sourceMaterial.GetTexture(PackedMaskMapId));
            }

            if (sourceMaterial.HasProperty(ThicknessMapId))
            {
                destinationMaterial.SetTexture(ThicknessMapId, sourceMaterial.GetTexture(ThicknessMapId));
            }

            if (sourceMaterial.HasProperty(FrontDepthMapId))
            {
                destinationMaterial.SetTexture(FrontDepthMapId, sourceMaterial.GetTexture(FrontDepthMapId));
            }

            if (sourceMaterial.HasProperty(AlphaCutoffId))
            {
                destinationMaterial.SetFloat(AlphaCutoffId, sourceMaterial.GetFloat(AlphaCutoffId));
            }

            destinationMaterial.renderQueue = sourceMaterial.renderQueue;
            destinationMaterial.enableInstancing = sourceMaterial.enableInstancing;
            return;
        }

        destinationMaterial.SetColor(BaseColorId, Color.white);
        destinationMaterial.renderQueue = 3000;
    }

    private bool ShouldRefreshLights(bool forceLightRefresh)
    {
        if (forceLightRefresh)
        {
            return true;
        }

        if (!Application.isPlaying && !refreshLightsInEditor)
        {
            return false;
        }

        return Time.realtimeSinceStartupAsDouble >= nextLightRefreshTime;
    }

    private Vector3 ResolveLightReferencePosition()
    {
        if (lightAnchor != null)
        {
            return lightAnchor.position;
        }

        if (targetRenderer != null)
        {
            return targetRenderer.bounds.center;
        }

        return transform.position;
    }

    private void RefreshManualLights(Vector3 referencePosition)
    {
        Array.Clear(manualLightPositions, 0, manualLightPositions.Length);
        Array.Clear(manualLightDirections, 0, manualLightDirections.Length);
        Array.Clear(manualLightColors, 0, manualLightColors.Length);
        Array.Clear(manualLightData0, 0, manualLightData0.Length);
        Array.Clear(manualLightData1, 0, manualLightData1.Length);
        Array.Clear(bestLights, 0, bestLights.Length);

        for (int i = 0; i < bestLightScores.Length; i++)
        {
            bestLightScores[i] = float.NegativeInfinity;
        }

        cachedLightCount = 0;

        if (!Application.isPlaying && !refreshLightsInEditor)
        {
            return;
        }

        Light[] sceneLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        if (sceneLights == null || sceneLights.Length == 0)
        {
            return;
        }

        int lightLimit = Mathf.Clamp(maxLights, 1, MaxShaderLights);
        for (int i = 0; i < sceneLights.Length; i++)
        {
            Light light = sceneLights[i];
            if (!IsSupportedLight(light))
            {
                continue;
            }

            float score = ScoreLight(light, referencePosition);
            if (score <= 0f)
            {
                continue;
            }

            InsertBestLight(light, score, lightLimit);
        }

        for (int i = 0; i < lightLimit; i++)
        {
            Light light = bestLights[i];
            if (light == null || bestLightScores[i] <= 0f)
            {
                continue;
            }

            PopulateLightData(light, cachedLightCount);
            cachedLightCount++;
        }
    }

    private void InsertBestLight(Light light, float score, int lightLimit)
    {
        for (int i = 0; i < lightLimit; i++)
        {
            if (score <= bestLightScores[i])
            {
                continue;
            }

            for (int shift = lightLimit - 1; shift > i; shift--)
            {
                bestLightScores[shift] = bestLightScores[shift - 1];
                bestLights[shift] = bestLights[shift - 1];
            }

            bestLightScores[i] = score;
            bestLights[i] = light;
            return;
        }
    }

    private void PopulateLightData(Light light, int slot)
    {
        Color linearColor = GetNormalizedLightColor(light);

        switch (light.type)
        {
            case LightType.Directional:
                manualLightPositions[slot] = Vector4.zero;
                manualLightDirections[slot] = -light.transform.forward;
                manualLightColors[slot] = linearColor;
                manualLightData0[slot] = new Vector4(0f, 0f, 0f, 0f);
                manualLightData1[slot] = Vector4.zero;
                break;

            case LightType.Point:
                manualLightPositions[slot] = new Vector4(light.transform.position.x, light.transform.position.y, light.transform.position.z, 1f);
                manualLightDirections[slot] = Vector4.zero;
                manualLightColors[slot] = linearColor;
                manualLightData0[slot] = new Vector4(1f, Mathf.Max(0.01f, light.range), 0f, 0f);
                manualLightData1[slot] = Vector4.zero;
                break;

            case LightType.Spot:
                manualLightPositions[slot] = new Vector4(light.transform.position.x, light.transform.position.y, light.transform.position.z, 1f);
                manualLightDirections[slot] = new Vector4(light.transform.forward.x, light.transform.forward.y, light.transform.forward.z, 0f);
                manualLightColors[slot] = linearColor;
                manualLightData0[slot] = new Vector4(2f, Mathf.Max(0.01f, light.range), 0f, 0f);
                manualLightData1[slot] = new Vector4(
                    Mathf.Cos(light.innerSpotAngle * 0.5f * Mathf.Deg2Rad),
                    Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad),
                    0f,
                    0f);
                break;
        }
    }

    private static bool IsSupportedLight(Light light)
    {
        if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
        {
            return false;
        }

        return light.type == LightType.Directional
            || light.type == LightType.Point
            || light.type == LightType.Spot;
    }

    private float ScoreLight(Light light, Vector3 referencePosition)
    {
        float normalizedIntensity = GetNormalizedLightIntensity(light);
        if (normalizedIntensity <= 0f)
        {
            return 0f;
        }

        if (light.type == LightType.Directional)
        {
            return normalizedIntensity * 4f;
        }

        float range = Mathf.Max(0.01f, light.range);
        float distance = Vector3.Distance(light.transform.position, referencePosition);
        if (distance > range)
        {
            return 0f;
        }

        float normalizedDistance = 1f - Mathf.Clamp01(distance / range);
        float distanceScore = normalizedDistance * normalizedDistance;

        if (light.type == LightType.Spot)
        {
            Vector3 toReference = (referencePosition - light.transform.position).normalized;
            float cone = Vector3.Dot(light.transform.forward, toReference);
            float outerCos = Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
            if (cone <= outerCos)
            {
                return 0f;
            }

            float innerCos = Mathf.Cos(light.innerSpotAngle * 0.5f * Mathf.Deg2Rad);
            float coneScore = Mathf.InverseLerp(outerCos, Mathf.Max(innerCos, outerCos + 0.0001f), cone);
            return normalizedIntensity * distanceScore * coneScore;
        }

        return normalizedIntensity * distanceScore;
    }

    private Color GetNormalizedLightColor(Light light)
    {
        float normalizedIntensity = GetNormalizedLightIntensity(light);
        if (normalizedIntensity <= 0f)
        {
            return Color.black;
        }

        Color normalizedColor = light.color.linear * normalizedIntensity;
        float peakChannel = Mathf.Max(normalizedColor.r, Mathf.Max(normalizedColor.g, normalizedColor.b));
        if (peakChannel > maxNormalizedLightIntensity && peakChannel > 0f)
        {
            normalizedColor *= maxNormalizedLightIntensity / peakChannel;
        }

        return normalizedColor;
    }

    private float GetNormalizedLightIntensity(Light light)
    {
        if (light == null)
        {
            return 0f;
        }

        float rawIntensity = Mathf.Max(0f, light.intensity);
        float scale = light.type == LightType.Directional ? directionalLightScale : punctualLightScale;
        if (rawIntensity <= 0f || scale <= 0f || maxNormalizedLightIntensity <= 0f)
        {
            return 0f;
        }

        return (1f - Mathf.Exp(-rawIntensity * scale)) * maxNormalizedLightIntensity;
    }

    private static Texture ResolveTexture(Texture explicitTexture, Material sourceMaterial, params int[] propertyIds)
    {
        if (explicitTexture != null)
        {
            return explicitTexture;
        }

        if (sourceMaterial == null || propertyIds == null)
        {
            return null;
        }

        for (int i = 0; i < propertyIds.Length; i++)
        {
            int propertyId = propertyIds[i];
            if (sourceMaterial.HasProperty(propertyId))
            {
                Texture texture = sourceMaterial.GetTexture(propertyId);
                if (texture != null)
                {
                    return texture;
                }
            }
        }

        return null;
    }

    private static Color ResolveColor(Material sourceMaterial, int propertyId, Color fallback)
    {
        if (sourceMaterial != null && sourceMaterial.HasProperty(propertyId))
        {
            return sourceMaterial.GetColor(propertyId);
        }

        return fallback;
    }

    private static float ResolveFloat(Material sourceMaterial, int propertyId, float fallback)
    {
        if (sourceMaterial != null && sourceMaterial.HasProperty(propertyId))
        {
            return sourceMaterial.GetFloat(propertyId);
        }

        return fallback;
    }

    private static void SetTextureIfPresent(MaterialPropertyBlock block, int propertyId, Texture texture)
    {
        if (texture != null)
        {
            block.SetTexture(propertyId, texture);
        }
    }
}
