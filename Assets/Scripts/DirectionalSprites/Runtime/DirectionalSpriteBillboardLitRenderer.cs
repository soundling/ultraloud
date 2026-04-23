using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class DirectionalSpriteBillboardLitRenderer : MonoBehaviour
{
    private enum BillboardLightingBasisMode
    {
        Quad = 0,
        FacingReference = 1,
        SpriteAngle = 2
    }

    private enum LightReferenceMode
    {
        RendererBoundsCenter = 0,
        ExplicitTransform = 1,
        MainCamera = 2
    }

    private const string ShaderName = "Ultraloud/Directional Sprites/Billboard Lit HDRP";
    private const int MaxShaderLights = 4;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
    private static readonly int AlphaCutoffId = Shader.PropertyToID("_AlphaCutoff");
    private static readonly int NormalScaleId = Shader.PropertyToID("_NormalScale");
    private static readonly int DetailNormalInfluenceId = Shader.PropertyToID("_DetailNormalInfluence");
    private static readonly int MacroNormalBendId = Shader.PropertyToID("_MacroNormalBend");
    private static readonly int WrapDiffuseId = Shader.PropertyToID("_WrapDiffuse");
    private static readonly int AmbientTopColorId = Shader.PropertyToID("_AmbientTopColor");
    private static readonly int AmbientBottomColorId = Shader.PropertyToID("_AmbientBottomColor");
    private static readonly int AmbientIntensityId = Shader.PropertyToID("_AmbientIntensity");
    private static readonly int SurfaceRoughnessId = Shader.PropertyToID("_SurfaceRoughness");
    private static readonly int SpecularStrengthId = Shader.PropertyToID("_SpecularStrength");
    private static readonly int MinSpecularPowerId = Shader.PropertyToID("_MinSpecularPower");
    private static readonly int MaxSpecularPowerId = Shader.PropertyToID("_MaxSpecularPower");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
    private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
    private static readonly int SpriteFlipXId = Shader.PropertyToID("_SpriteFlipX");
    private static readonly int UseNormalMapId = Shader.PropertyToID("_UseNormalMap");
    private static readonly int TwoSidedLightingId = Shader.PropertyToID("_TwoSidedLighting");
    private static readonly int FlipBackfacingNormalsId = Shader.PropertyToID("_FlipBackfacingNormals");
    private static readonly int UseCustomLightingBasisId = Shader.PropertyToID("_UseCustomLightingBasis");
    private static readonly int LightingRightWsId = Shader.PropertyToID("_LightingRightWS");
    private static readonly int LightingUpWsId = Shader.PropertyToID("_LightingUpWS");
    private static readonly int LightingForwardWsId = Shader.PropertyToID("_LightingForwardWS");
    private static readonly int ManualLightCountId = Shader.PropertyToID("_ManualLightCount");
    private static readonly int ManualLightPositionsId = Shader.PropertyToID("_ManualLightPositionWS");
    private static readonly int ManualLightDirectionsId = Shader.PropertyToID("_ManualLightDirectionWS");
    private static readonly int ManualLightColorsId = Shader.PropertyToID("_ManualLightColor");
    private static readonly int ManualLightData0Id = Shader.PropertyToID("_ManualLightData0");
    private static readonly int ManualLightData1Id = Shader.PropertyToID("_ManualLightData1");

    [SerializeField] private DirectionalSpriteAnimator animator;
    [SerializeField] private MeshRenderer targetRenderer;
    [SerializeField] private LightReferenceMode lightReferenceMode = LightReferenceMode.RendererBoundsCenter;
    [SerializeField] private Transform lightAnchor;

    [Header("Scene Lights")]
    [SerializeField] private bool refreshLightsInEditor = true;
    [SerializeField, Range(1, MaxShaderLights)] private int maxLights = MaxShaderLights;
    [SerializeField, Min(0.05f)] private float lightRefreshInterval = 0.15f;
    [SerializeField, Min(0f)] private float directionalLightScale = 0.00001f;
    [SerializeField, Min(0f)] private float punctualLightScale = 0.01f;
    [SerializeField, Min(0f)] private float maxNormalizedLightIntensity = 1.5f;

    [Header("Shading")]
    [SerializeField] private BillboardLightingBasisMode lightingBasisMode = BillboardLightingBasisMode.SpriteAngle;
    [SerializeField] private Color baseColorTint = Color.white;
    [SerializeField, Range(0f, 1f)] private float alphaCutoff = 0.08f;
    [SerializeField, Range(0f, 2f)] private float normalScale = 1f;
    [SerializeField, Range(0f, 1f)] private float detailNormalInfluence = 0.55f;
    [SerializeField, Range(0f, 2f)] private float macroNormalBend = 0.75f;
    [SerializeField, Range(0f, 1f)] private float spriteAngleLightingInfluence = 0.35f;
    [SerializeField] private bool twoSidedLighting = true;
    [SerializeField] private bool flipBackfacingNormals = true;
    [SerializeField, Range(0f, 1f)] private float wrapDiffuse = 0.35f;
    [SerializeField] private Color ambientTopColor = new(0.52f, 0.56f, 0.62f, 1f);
    [SerializeField] private Color ambientBottomColor = new(0.14f, 0.12f, 0.10f, 1f);
    [SerializeField, Min(0f)] private float ambientIntensity = 1f;
    [SerializeField] private bool includeRenderSettingsAmbient = true;
    [SerializeField, Min(0f)] private float renderSettingsAmbientScale = 0.25f;
    [SerializeField, Range(0f, 1f)] private float surfaceRoughness = 0.7f;
    [SerializeField, Range(0f, 4f)] private float specularStrength = 0.45f;
    [SerializeField, Range(1f, 32f)] private float minSpecularPower = 6f;
    [SerializeField, Range(1f, 128f)] private float maxSpecularPower = 24f;
    [SerializeField] private Color rimColor = Color.white;
    [SerializeField, Range(0f, 4f)] private float rimStrength = 0.18f;
    [SerializeField, Range(0.5f, 8f)] private float rimPower = 3f;

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
#if UNITY_EDITOR
    private bool editorSceneCallbacksRegistered;
#endif

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
        EnsureRuntimeMaterial();
        ApplyNow(true);
    }

    private void OnEnable()
    {
        AutoAssignReferences();
#if UNITY_EDITOR
        RegisterEditorCallbacks();
#endif
        EnsureRuntimeMaterial();
        ApplyNow(true);
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
        surfaceRoughness = Mathf.Clamp01(surfaceRoughness);
        specularStrength = Mathf.Max(0f, specularStrength);
        minSpecularPower = Mathf.Max(1f, minSpecularPower);
        maxSpecularPower = Mathf.Max(minSpecularPower, maxSpecularPower);
        spriteAngleLightingInfluence = Mathf.Clamp01(spriteAngleLightingInfluence);
        rimStrength = Mathf.Max(0f, rimStrength);
        rimPower = Mathf.Max(0.5f, rimPower);

        AutoAssignReferences();
        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureRuntimeMaterial();
        animator?.RefreshNow();
        ApplyNow(true);
    }

    private void LateUpdate()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureRuntimeMaterial();
        ApplyNow(false);
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnregisterEditorCallbacks();
#endif
        ClearPropertyBlock();
        RestoreOriginalMaterial();
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        UnregisterEditorCallbacks();
#endif
        RestoreOriginalMaterial();
    }

    public void ApplyNow(bool forceLightRefresh)
    {
        if (animator == null || targetRenderer == null || !EnsureRuntimeMaterial())
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        propertyBlock.Clear();

        Sprite sprite = animator.CurrentSprite;
        Texture baseTexture = sprite != null ? sprite.texture : null;
        Texture normalTexture = animator.CurrentNormalMap;

        if (baseTexture == null)
        {
            targetRenderer.enabled = false;
            ClearPropertyBlock();
            return;
        }

        targetRenderer.enabled = true;
        propertyBlock.SetTexture(BaseMapId, baseTexture);
        if (normalTexture != null)
        {
            propertyBlock.SetTexture(NormalMapId, normalTexture);
        }

        propertyBlock.SetColor(BaseColorId, baseColorTint);
        propertyBlock.SetFloat(AlphaCutoffId, alphaCutoff);
        propertyBlock.SetFloat(NormalScaleId, normalScale);
        propertyBlock.SetFloat(DetailNormalInfluenceId, detailNormalInfluence);
        propertyBlock.SetFloat(MacroNormalBendId, macroNormalBend);
        propertyBlock.SetFloat(WrapDiffuseId, wrapDiffuse);
        propertyBlock.SetFloat(SurfaceRoughnessId, surfaceRoughness);
        propertyBlock.SetFloat(SpecularStrengthId, specularStrength);
        propertyBlock.SetFloat(MinSpecularPowerId, minSpecularPower);
        propertyBlock.SetFloat(MaxSpecularPowerId, maxSpecularPower);
        propertyBlock.SetColor(RimColorId, rimColor);
        propertyBlock.SetFloat(RimStrengthId, rimStrength);
        propertyBlock.SetFloat(RimPowerId, rimPower);
        propertyBlock.SetFloat(SpriteFlipXId, animator.CurrentFlipX ? -1f : 1f);
        propertyBlock.SetFloat(UseNormalMapId, normalTexture != null ? 1f : 0f);
        propertyBlock.SetFloat(TwoSidedLightingId, twoSidedLighting ? 1f : 0f);
        propertyBlock.SetFloat(FlipBackfacingNormalsId, flipBackfacingNormals ? 1f : 0f);

        if (TryBuildLightingBasis(out Vector3 lightingRight, out Vector3 lightingUp, out Vector3 lightingForward))
        {
            propertyBlock.SetFloat(UseCustomLightingBasisId, 1f);
            propertyBlock.SetVector(LightingRightWsId, new Vector4(lightingRight.x, lightingRight.y, lightingRight.z, 0f));
            propertyBlock.SetVector(LightingUpWsId, new Vector4(lightingUp.x, lightingUp.y, lightingUp.z, 0f));
            propertyBlock.SetVector(LightingForwardWsId, new Vector4(lightingForward.x, lightingForward.y, lightingForward.z, 0f));
        }
        else
        {
            propertyBlock.SetFloat(UseCustomLightingBasisId, 0f);
        }

        Color topAmbient = ambientTopColor.linear;
        Color bottomAmbient = ambientBottomColor.linear;
        if (includeRenderSettingsAmbient)
        {
            Color ambient = RenderSettings.ambientLight.linear * renderSettingsAmbientScale;
            topAmbient += ambient;
            bottomAmbient += ambient * 0.65f;
        }

        propertyBlock.SetColor(AmbientTopColorId, topAmbient);
        propertyBlock.SetColor(AmbientBottomColorId, bottomAmbient);
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

#if UNITY_EDITOR
    private void RegisterEditorCallbacks()
    {
        if (editorSceneCallbacksRegistered)
        {
            return;
        }

        SceneView.duringSceneGui += OnEditorSceneGui;
        editorSceneCallbacksRegistered = true;
    }

    private void UnregisterEditorCallbacks()
    {
        if (!editorSceneCallbacksRegistered)
        {
            return;
        }

        SceneView.duringSceneGui -= OnEditorSceneGui;
        editorSceneCallbacksRegistered = false;
    }

    private void OnEditorSceneGui(SceneView sceneView)
    {
        if (Application.isPlaying || !isActiveAndEnabled || sceneView == null || sceneView.camera == null)
        {
            return;
        }

        animator?.RefreshNow();
        EnsureRuntimeMaterial();
        ApplyNow(false);
    }
#endif

    private void AutoAssignReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<DirectionalSpriteAnimator>();
        }

        if (animator == null)
        {
            animator = GetComponentInParent<DirectionalSpriteAnimator>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<MeshRenderer>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<MeshRenderer>(true);
        }
    }

    private bool EnsureRuntimeMaterial()
    {
        if (targetRenderer == null)
        {
            return false;
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            return false;
        }

        Material currentShared = targetRenderer.sharedMaterial;
        if (originalSharedMaterial == null && currentShared != runtimeMaterial)
        {
            originalSharedMaterial = currentShared;
        }

        if (runtimeMaterial == null || runtimeMaterial.shader != shader)
        {
            DestroyRuntimeMaterial();
            runtimeMaterial = new Material(shader)
            {
                name = $"{name}_DirectionalSpriteBillboardLit",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (targetRenderer.sharedMaterial != runtimeMaterial)
        {
            targetRenderer.sharedMaterial = runtimeMaterial;
        }

        return true;
    }

    private bool TryBuildLightingBasis(out Vector3 right, out Vector3 up, out Vector3 forward)
    {
        right = Vector3.right;
        up = Vector3.up;
        forward = Vector3.forward;

        if (lightingBasisMode == BillboardLightingBasisMode.Quad)
        {
            return false;
        }

        Transform reference = animator != null ? animator.FacingReferenceTransform : transform;
        if (reference == null)
        {
            return false;
        }

        up = Vector3.up;
        Vector3 baseForward = Vector3.ProjectOnPlane(reference.forward, up);
        if (baseForward.sqrMagnitude < 0.0001f)
        {
            baseForward = Vector3.ProjectOnPlane(reference.rotation * Vector3.forward, up);
            if (baseForward.sqrMagnitude < 0.0001f)
            {
                baseForward = Vector3.forward;
            }
        }

        baseForward.Normalize();
        float yaw = 0f;
        if (lightingBasisMode == BillboardLightingBasisMode.SpriteAngle && animator != null && animator.CurrentAngle != null)
        {
            yaw = ResolveVisualAngleYaw(animator.CurrentAngle, animator.CurrentFlipX) * spriteAngleLightingInfluence;
        }

        forward = Quaternion.AngleAxis(yaw, up) * baseForward;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = baseForward;
        }

        forward.Normalize();
        right = Vector3.Cross(up, forward);
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }
        else
        {
            right.Normalize();
        }

        up = Vector3.Cross(forward, right).normalized;
        return true;
    }

    private static float ResolveVisualAngleYaw(DirectionalSpriteAngleSet angle, bool currentFlipX)
    {
        if (angle == null)
        {
            return 0f;
        }

        float yaw = angle.yawDegrees;
        bool mirrorableAngle = angle.symmetry == DirectionalSpriteSymmetry.MirrorToOppositeSide
            && Mathf.Abs(yaw) > 0.001f
            && Mathf.Abs(Mathf.Abs(yaw) - 180f) > 0.001f;
        if (mirrorableAngle && currentFlipX)
        {
            yaw = -yaw;
        }

        return yaw;
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
        if (targetRenderer != null)
        {
            targetRenderer.SetPropertyBlock(null);
        }
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
        switch (lightReferenceMode)
        {
            case LightReferenceMode.ExplicitTransform:
                if (lightAnchor != null)
                {
                    return lightAnchor.position;
                }
                break;

            case LightReferenceMode.MainCamera:
                if (Camera.main != null)
                {
                    return Camera.main.transform.position;
                }
                break;
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

        Light[] sceneLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
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
}
