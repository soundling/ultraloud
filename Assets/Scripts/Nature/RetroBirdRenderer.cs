using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(65)]
public sealed class RetroBirdRenderer : MonoBehaviour
{
    private const string GeneratedRootName = "__BirdGenerated";
    private const string BirdShaderName = "Ultraloud/Nature/Small Bird HDRP";
    private const string FallbackShaderName = "Ultraloud/Directional Sprites/Billboard Lit HDRP";
    private const int MaxShaderLights = 4;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
    private static readonly int ThicknessMapId = Shader.PropertyToID("_ThicknessMap");
    private static readonly int PackedMasksId = Shader.PropertyToID("_PackedMasks");
    private static readonly int FrameUvRectId = Shader.PropertyToID("_FrameUvRect");
    private static readonly int SpriteFlipXId = Shader.PropertyToID("_SpriteFlipX");
    private static readonly int AlphaCutoffId = Shader.PropertyToID("_AlphaCutoff");
    private static readonly int CoverageSoftnessId = Shader.PropertyToID("_CoverageSoftness");
    private static readonly int NormalScaleId = Shader.PropertyToID("_NormalScale");
    private static readonly int DetailNormalInfluenceId = Shader.PropertyToID("_DetailNormalInfluence");
    private static readonly int MacroNormalBendId = Shader.PropertyToID("_MacroNormalBend");
    private static readonly int WrapDiffuseId = Shader.PropertyToID("_WrapDiffuse");
    private static readonly int WingShadowStrengthId = Shader.PropertyToID("_WingShadowStrength");
    private static readonly int BodyShadowStrengthId = Shader.PropertyToID("_BodyShadowStrength");
    private static readonly int SurfaceRoughnessId = Shader.PropertyToID("_SurfaceRoughness");
    private static readonly int SpecularStrengthId = Shader.PropertyToID("_SpecularStrength");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
    private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
    private static readonly int TransmissionColorId = Shader.PropertyToID("_TransmissionColor");
    private static readonly int TransmissionStrengthId = Shader.PropertyToID("_TransmissionStrength");
    private static readonly int TransmissionPowerId = Shader.PropertyToID("_TransmissionPower");
    private static readonly int WingTransmissionBoostId = Shader.PropertyToID("_WingTransmissionBoost");
    private static readonly int AmbientTopColorId = Shader.PropertyToID("_AmbientTopColor");
    private static readonly int AmbientBottomColorId = Shader.PropertyToID("_AmbientBottomColor");
    private static readonly int AmbientIntensityId = Shader.PropertyToID("_AmbientIntensity");
    private static readonly int UseNormalMapId = Shader.PropertyToID("_UseNormalMap");
    private static readonly int UseThicknessMapId = Shader.PropertyToID("_UseThicknessMap");
    private static readonly int UsePackedMasksId = Shader.PropertyToID("_UsePackedMasks");
    private static readonly int ManualLightCountId = Shader.PropertyToID("_ManualLightCount");
    private static readonly int ManualLightPositionsId = Shader.PropertyToID("_ManualLightPositionWS");
    private static readonly int ManualLightDirectionsId = Shader.PropertyToID("_ManualLightDirectionWS");
    private static readonly int ManualLightColorsId = Shader.PropertyToID("_ManualLightColor");
    private static readonly int ManualLightData0Id = Shader.PropertyToID("_ManualLightData0");
    private static readonly int ManualLightData1Id = Shader.PropertyToID("_ManualLightData1");

    [Header("Animation Atlas")]
    [SerializeField] private Texture2D baseMap;
    [SerializeField] private Texture2D normalMap;
    [SerializeField] private Texture2D thicknessMap;
    [SerializeField] private Texture2D packedMasksMap;
    [SerializeField, Range(1, 16)] private int frameColumns = 4;
    [SerializeField, Range(1, 16)] private int frameRows = 2;
    [SerializeField, Range(1, 256)] private int frameCount = 8;
    [SerializeField, Min(0f)] private float framesPerSecond = 12f;
    [SerializeField] private bool animateInEditMode = true;
    [SerializeField] private bool randomizeStartFrame = true;
    [SerializeField, Range(0f, 1f)] private float animationPhase;

    [Header("Shape")]
    [SerializeField] private Vector2 spriteSize = new(1.25f, 0.72f);
    [SerializeField] private Vector3 pivotOffset = Vector3.zero;
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private bool flipFromVelocity = true;
    [SerializeField, Min(0f)] private float minVelocityForFlip = 0.08f;
    [SerializeField, Range(0f, 75f)] private float maxBankAngle = 22f;
    [SerializeField] private bool castShadows;
    [SerializeField] private bool receiveShadows;

    [Header("Shading")]
    [SerializeField] private Color baseColorTint = Color.white;
    [SerializeField, Range(0f, 0.35f)] private float alphaCutoff = 0.08f;
    [SerializeField, Range(0f, 0.25f)] private float coverageSoftness = 0.035f;
    [SerializeField, Range(0f, 2f)] private float normalScale = 1f;
    [SerializeField, Range(0f, 1f)] private float detailNormalInfluence = 0.7f;
    [SerializeField, Range(0f, 2f)] private float macroNormalBend = 0.5f;
    [SerializeField, Range(0f, 1f)] private float wrapDiffuse = 0.45f;
    [SerializeField, Range(0f, 2f)] private float wingShadowStrength = 0.34f;
    [SerializeField, Range(0f, 2f)] private float bodyShadowStrength = 0.6f;
    [SerializeField, Range(0f, 1f)] private float surfaceRoughness = 0.78f;
    [SerializeField, Range(0f, 2f)] private float specularStrength = 0.12f;
    [SerializeField] private Color rimColor = new(0.72f, 0.67f, 0.58f, 1f);
    [SerializeField, Range(0f, 2f)] private float rimStrength = 0.18f;
    [SerializeField, Range(0.5f, 8f)] private float rimPower = 3f;

    [Header("Transmission")]
    [SerializeField] private Color transmissionColor = new(0.98f, 0.78f, 0.48f, 1f);
    [SerializeField, Range(0f, 4f)] private float transmissionStrength = 0.38f;
    [SerializeField, Range(0.5f, 8f)] private float transmissionPower = 2.25f;
    [SerializeField, Range(0f, 4f)] private float wingTransmissionBoost = 1.45f;

    [Header("Ambient")]
    [SerializeField] private Color ambientTopColor = new(0.58f, 0.62f, 0.66f, 1f);
    [SerializeField] private Color ambientBottomColor = new(0.17f, 0.14f, 0.12f, 1f);
    [SerializeField, Min(0f)] private float ambientIntensity = 1.05f;
    [SerializeField] private bool includeRenderSettingsAmbient = true;
    [SerializeField, Min(0f)] private float renderSettingsAmbientScale = 0.2f;

    [Header("Scene Lights")]
    [SerializeField, Range(1, MaxShaderLights)] private int maxLights = MaxShaderLights;
    [SerializeField, Min(0.02f)] private float lightRefreshInterval = 0.18f;
    [SerializeField, Min(0f)] private float directionalLightScale = 0.00001f;
    [SerializeField, Min(0f)] private float punctualLightScale = 0.01f;
    [SerializeField, Min(0f)] private float maxNormalizedLightIntensity = 1.45f;

    private readonly Vector4[] manualLightPositions = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightDirections = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightColors = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightData0 = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightData1 = new Vector4[MaxShaderLights];
    private readonly Light[] bestLights = new Light[MaxShaderLights];
    private readonly float[] bestLightScores = new float[MaxShaderLights];
    private readonly Vector2[] quadUvs = new Vector2[4];

    private GameObject generatedRoot;
    private MeshRenderer birdRenderer;
    private Mesh birdMesh;
    private Material birdMaterial;
    private RetroFlockAgent flockAgent;
    private Vector3 lastPosition;
    private Vector3 estimatedVelocity;
    private bool hasLastPosition;
    private double lastRuntimeTime;
    private bool hasRuntimeTime;
    private bool rebuildRequested = true;
    private int lastBuildHash;
    private int lastFrame = -1;
    private bool lastFlipX;
    private double nextLightRefreshTime = double.NegativeInfinity;
#if UNITY_EDITOR
    private bool deferredRebuildQueued;
#endif

    private void Reset()
    {
        ClampSettings();
        if (randomizeStartFrame)
        {
            animationPhase = UnityEngine.Random.value;
        }

        RequestRebuild();
    }

    private void Awake()
    {
        ClampSettings();
        RebuildIfNeeded(true);
    }

    private void OnEnable()
    {
        ClampSettings();
        flockAgent = GetComponent<RetroFlockAgent>();
        hasRuntimeTime = false;
        RebuildIfNeeded(true);
        UpdateRuntime(ResolveTime(), 0.016f);
    }

    private void OnValidate()
    {
        ClampSettings();
        RequestRebuild();
    }

    private void LateUpdate()
    {
        RebuildIfNeeded(false);
        double now = ResolveTime();
        float deltaTime = ResolveDeltaTime(now);
        UpdateRuntime(now, deltaTime);
    }

    private void OnDisable()
    {
        DestroyRuntimeMaterial();
    }

    private void OnDestroy()
    {
        DestroyGeneratedRoot();
        DestroyRuntimeMaterial();
    }

    [ContextMenu("Rebuild Bird Now")]
    public void RebuildBirdNow()
    {
        rebuildRequested = true;
        RebuildIfNeeded(true);
    }

    private void ClampSettings()
    {
        frameColumns = Mathf.Clamp(frameColumns, 1, 16);
        frameRows = Mathf.Clamp(frameRows, 1, 16);
        frameCount = Mathf.Clamp(frameCount, 1, frameColumns * frameRows);
        framesPerSecond = Mathf.Max(0f, framesPerSecond);
        spriteSize.x = Mathf.Max(0.01f, spriteSize.x);
        spriteSize.y = Mathf.Max(0.01f, spriteSize.y);
        minVelocityForFlip = Mathf.Max(0f, minVelocityForFlip);
        alphaCutoff = Mathf.Clamp(alphaCutoff, 0f, 0.35f);
        coverageSoftness = Mathf.Clamp(coverageSoftness, 0f, 0.25f);
        normalScale = Mathf.Clamp(normalScale, 0f, 2f);
        detailNormalInfluence = Mathf.Clamp01(detailNormalInfluence);
        macroNormalBend = Mathf.Clamp(macroNormalBend, 0f, 2f);
        wrapDiffuse = Mathf.Clamp01(wrapDiffuse);
        wingShadowStrength = Mathf.Clamp(wingShadowStrength, 0f, 2f);
        bodyShadowStrength = Mathf.Clamp(bodyShadowStrength, 0f, 2f);
        surfaceRoughness = Mathf.Clamp01(surfaceRoughness);
        specularStrength = Mathf.Clamp(specularStrength, 0f, 2f);
        rimStrength = Mathf.Clamp(rimStrength, 0f, 2f);
        rimPower = Mathf.Clamp(rimPower, 0.5f, 8f);
        transmissionStrength = Mathf.Clamp(transmissionStrength, 0f, 4f);
        transmissionPower = Mathf.Clamp(transmissionPower, 0.5f, 8f);
        wingTransmissionBoost = Mathf.Clamp(wingTransmissionBoost, 0f, 4f);
        ambientIntensity = Mathf.Max(0f, ambientIntensity);
        renderSettingsAmbientScale = Mathf.Max(0f, renderSettingsAmbientScale);
        maxLights = Mathf.Clamp(maxLights, 1, MaxShaderLights);
        lightRefreshInterval = Mathf.Max(0.02f, lightRefreshInterval);
        directionalLightScale = Mathf.Max(0f, directionalLightScale);
        punctualLightScale = Mathf.Max(0f, punctualLightScale);
        maxNormalizedLightIntensity = Mathf.Max(0f, maxNormalizedLightIntensity);
    }

    private void RequestRebuild()
    {
        rebuildRequested = true;
#if UNITY_EDITOR
        if (!Application.isPlaying && !deferredRebuildQueued)
        {
            deferredRebuildQueued = true;
            EditorApplication.delayCall += DeferredEditorRebuild;
        }
#endif
    }

#if UNITY_EDITOR
    private void DeferredEditorRebuild()
    {
        deferredRebuildQueued = false;
        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        RebuildIfNeeded(true);
        UpdateRuntime(EditorApplication.timeSinceStartup, 0.016f);
        SceneView.RepaintAll();
    }

    internal void EnsureEditorPreview(bool force)
    {
        if (this == null || Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!isActiveAndEnabled || EditorUtility.IsPersistent(this))
        {
            return;
        }

        ClampSettings();
        if (force || !HasGeneratedRoot())
        {
            rebuildRequested = true;
            RebuildIfNeeded(true);
        }
        else
        {
            RebuildIfNeeded(false);
        }

        UpdateRuntime(EditorApplication.timeSinceStartup, 0.016f);
    }
#endif

    private void RebuildIfNeeded(bool force)
    {
        int hash = ComputeBuildHash();
        if (!force && !rebuildRequested && hash == lastBuildHash && HasGeneratedRoot())
        {
            return;
        }

        RebuildInternal();
        lastBuildHash = hash;
        rebuildRequested = false;
    }

    private int ComputeBuildHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + spriteSize.GetHashCode();
            hash = hash * 31 + frameColumns;
            hash = hash * 31 + frameRows;
            hash = hash * 31 + frameCount;
            hash = hash * 31 + GetAssetHash(baseMap);
            hash = hash * 31 + GetAssetHash(normalMap);
            hash = hash * 31 + GetAssetHash(thicknessMap);
            hash = hash * 31 + GetAssetHash(packedMasksMap);
            hash = hash * 31 + castShadows.GetHashCode();
            hash = hash * 31 + receiveShadows.GetHashCode();
            return hash;
        }
    }

    private static int GetAssetHash(Object asset)
    {
        return asset != null ? asset.GetHashCode() : 0;
    }

    private bool HasGeneratedRoot()
    {
        if (generatedRoot != null && birdRenderer != null && birdMesh != null)
        {
            return true;
        }

        Transform existing = transform.Find(GeneratedRootName);
        if (existing == null)
        {
            return false;
        }

        generatedRoot = existing.gameObject;
        birdRenderer = generatedRoot.GetComponentInChildren<MeshRenderer>(true);
        MeshFilter filter = generatedRoot.GetComponentInChildren<MeshFilter>(true);
        birdMesh = filter != null ? filter.sharedMesh : null;
        return birdRenderer != null && birdMesh != null;
    }

    private void RebuildInternal()
    {
        DestroyRuntimeMaterial();
        DestroyGeneratedRoot();

        generatedRoot = new GameObject(GeneratedRootName)
        {
            hideFlags = HideFlags.DontSave
        };
        generatedRoot.transform.SetParent(transform, false);
        generatedRoot.transform.localPosition = pivotOffset;
        generatedRoot.transform.localRotation = Quaternion.identity;
        generatedRoot.transform.localScale = Vector3.one;

        GameObject meshObject = new("BirdCard")
        {
            hideFlags = HideFlags.DontSave
        };
        meshObject.transform.SetParent(generatedRoot.transform, false);
        MeshFilter filter = meshObject.AddComponent<MeshFilter>();
        birdRenderer = meshObject.AddComponent<MeshRenderer>();
        birdRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        birdRenderer.receiveShadows = receiveShadows;
        birdRenderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        birdRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        birdMesh = BuildQuadMesh();
        filter.sharedMesh = birdMesh;
        lastFrame = -1;
        lastFlipX = false;
        ApplyMaterial();
        birdRenderer.sharedMaterial = birdMaterial;
    }

    private Mesh BuildQuadMesh()
    {
        float halfWidth = spriteSize.x * 0.5f;
        float halfHeight = spriteSize.y * 0.5f;
        Mesh mesh = new()
        {
            name = "Generated Small Bird Quad",
            hideFlags = HideFlags.DontSave
        };
        mesh.vertices = new[]
        {
            new Vector3(-halfWidth, -halfHeight, 0f),
            new Vector3(halfWidth, -halfHeight, 0f),
            new Vector3(halfWidth, halfHeight, 0f),
            new Vector3(-halfWidth, halfHeight, 0f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
        mesh.tangents = new[]
        {
            new Vector4(1f, 0f, 0f, 1f),
            new Vector4(1f, 0f, 0f, 1f),
            new Vector4(1f, 0f, 0f, 1f),
            new Vector4(1f, 0f, 0f, 1f)
        };
        SetFrameUvs(0, false, true);
        mesh.RecalculateBounds();
        return mesh;
    }

    private void UpdateRuntime(double time, float deltaTime)
    {
        ApplyMaterial();
        UpdateVelocityEstimate(deltaTime);
        Camera activeCamera = ResolveCamera();
        bool flipX = ResolveFlip(activeCamera);
        int frame = ResolveAnimationFrame(time);
        SetFrameUvs(frame, flipX, false);
        UpdateBillboard(activeCamera);

        if (time >= nextLightRefreshTime)
        {
            nextLightRefreshTime = time + lightRefreshInterval;
            RefreshLighting(activeCamera);
        }
    }

    private float ResolveDeltaTime(double time)
    {
        if (!hasRuntimeTime)
        {
            lastRuntimeTime = time;
            hasRuntimeTime = true;
            return 0.016f;
        }

        float deltaTime = Mathf.Clamp((float)(time - lastRuntimeTime), 0.0001f, 0.05f);
        lastRuntimeTime = time;
        return deltaTime;
    }

    private void UpdateVelocityEstimate(float deltaTime)
    {
        if (flockAgent == null)
        {
            flockAgent = GetComponent<RetroFlockAgent>();
        }

        if (flockAgent != null)
        {
            estimatedVelocity = flockAgent.Velocity;
            lastPosition = transform.position;
            hasLastPosition = true;
            return;
        }

        if (!hasLastPosition)
        {
            lastPosition = transform.position;
            estimatedVelocity = transform.forward;
            hasLastPosition = true;
            return;
        }

        float safeDeltaTime = Mathf.Max(deltaTime, 0.0001f);
        Vector3 movement = (transform.position - lastPosition) / safeDeltaTime;
        estimatedVelocity = movement.sqrMagnitude > 0.0001f ? movement : estimatedVelocity;
        lastPosition = transform.position;
    }

    private int ResolveAnimationFrame(double time)
    {
        if (!Application.isPlaying && !animateInEditMode)
        {
            return Mathf.Clamp(Mathf.RoundToInt(animationPhase * Mathf.Max(0, frameCount - 1)), 0, Mathf.Max(0, frameCount - 1));
        }

        if (framesPerSecond <= 0f)
        {
            return 0;
        }

        double frameTime = time * framesPerSecond + animationPhase * frameCount;
        return Mathf.FloorToInt((float)(frameTime % frameCount + frameCount) % frameCount);
    }

    private bool ResolveFlip(Camera activeCamera)
    {
        if (!flipFromVelocity || activeCamera == null || estimatedVelocity.sqrMagnitude < minVelocityForFlip * minVelocityForFlip)
        {
            return lastFlipX;
        }

        return Vector3.Dot(estimatedVelocity, activeCamera.transform.right) < 0f;
    }

    private void SetFrameUvs(int frame, bool flipX, bool force)
    {
        if (birdMesh == null || (!force && frame == lastFrame && flipX == lastFlipX))
        {
            return;
        }

        frame = Mathf.Clamp(frame, 0, Mathf.Max(0, frameCount - 1));
        int column = frame % frameColumns;
        int row = frame / frameColumns;
        float invColumns = 1f / frameColumns;
        float invRows = 1f / frameRows;
        float uMin = column * invColumns;
        float uMax = (column + 1) * invColumns;
        float vMax = 1f - row * invRows;
        float vMin = vMax - invRows;
        float left = flipX ? uMax : uMin;
        float right = flipX ? uMin : uMax;

        quadUvs[0] = new Vector2(left, vMin);
        quadUvs[1] = new Vector2(right, vMin);
        quadUvs[2] = new Vector2(right, vMax);
        quadUvs[3] = new Vector2(left, vMax);
        birdMesh.uv = quadUvs;

        if (birdMaterial != null)
        {
            SetVectorIfPresent(birdMaterial, FrameUvRectId, new Vector4(uMin, vMin, invColumns, invRows));
            SetFloatIfPresent(birdMaterial, SpriteFlipXId, flipX ? -1f : 1f);
        }

        lastFrame = frame;
        lastFlipX = flipX;
    }

    private void UpdateBillboard(Camera activeCamera)
    {
        if (generatedRoot == null)
        {
            return;
        }

        generatedRoot.transform.localPosition = pivotOffset;
        if (!faceCamera || activeCamera == null)
        {
            generatedRoot.transform.localRotation = Quaternion.identity;
            return;
        }

        Vector3 toCamera = activeCamera.transform.position - generatedRoot.transform.position;
        if (toCamera.sqrMagnitude < 0.0001f)
        {
            toCamera = -activeCamera.transform.forward;
        }

        Vector3 cameraUp = activeCamera.transform.up;
        Quaternion billboardRotation = Quaternion.LookRotation(toCamera.normalized, cameraUp);
        if (estimatedVelocity.sqrMagnitude > 0.0001f && maxBankAngle > 0f)
        {
            float vertical = Vector3.Dot(estimatedVelocity.normalized, cameraUp);
            float roll = Mathf.Clamp(-vertical * maxBankAngle, -maxBankAngle, maxBankAngle);
            billboardRotation = Quaternion.AngleAxis(roll, toCamera.normalized) * billboardRotation;
        }

        generatedRoot.transform.rotation = billboardRotation;
    }

    private void ApplyMaterial()
    {
        if (birdMaterial == null)
        {
            Shader shader = Shader.Find(BirdShaderName);
            if (shader == null)
            {
                shader = Shader.Find(FallbackShaderName);
            }

            if (shader == null)
            {
                shader = Shader.Find("HDRP/Unlit");
            }

            birdMaterial = new Material(shader)
            {
                name = "Generated Small Bird Material",
                hideFlags = HideFlags.DontSave
            };
            birdMaterial.renderQueue = (int)RenderQueue.Transparent;
        }

        SetTextureIfPresent(birdMaterial, BaseMapId, baseMap);
        SetTextureIfPresent(birdMaterial, NormalMapId, normalMap);
        SetTextureIfPresent(birdMaterial, ThicknessMapId, thicknessMap);
        SetTextureIfPresent(birdMaterial, PackedMasksId, packedMasksMap);
        SetColorIfPresent(birdMaterial, BaseColorId, baseColorTint);
        SetFloatIfPresent(birdMaterial, AlphaCutoffId, alphaCutoff);
        SetFloatIfPresent(birdMaterial, CoverageSoftnessId, coverageSoftness);
        SetFloatIfPresent(birdMaterial, NormalScaleId, normalScale);
        SetFloatIfPresent(birdMaterial, DetailNormalInfluenceId, detailNormalInfluence);
        SetFloatIfPresent(birdMaterial, MacroNormalBendId, macroNormalBend);
        SetFloatIfPresent(birdMaterial, WrapDiffuseId, wrapDiffuse);
        SetFloatIfPresent(birdMaterial, WingShadowStrengthId, wingShadowStrength);
        SetFloatIfPresent(birdMaterial, BodyShadowStrengthId, bodyShadowStrength);
        SetFloatIfPresent(birdMaterial, SurfaceRoughnessId, surfaceRoughness);
        SetFloatIfPresent(birdMaterial, SpecularStrengthId, specularStrength);
        SetColorIfPresent(birdMaterial, RimColorId, rimColor);
        SetFloatIfPresent(birdMaterial, RimStrengthId, rimStrength);
        SetFloatIfPresent(birdMaterial, RimPowerId, rimPower);
        SetColorIfPresent(birdMaterial, TransmissionColorId, transmissionColor);
        SetFloatIfPresent(birdMaterial, TransmissionStrengthId, transmissionStrength);
        SetFloatIfPresent(birdMaterial, TransmissionPowerId, transmissionPower);
        SetFloatIfPresent(birdMaterial, WingTransmissionBoostId, wingTransmissionBoost);
        SetColorIfPresent(birdMaterial, AmbientTopColorId, ResolveAmbientTop());
        SetColorIfPresent(birdMaterial, AmbientBottomColorId, ResolveAmbientBottom());
        SetFloatIfPresent(birdMaterial, AmbientIntensityId, ambientIntensity);
        SetFloatIfPresent(birdMaterial, UseNormalMapId, normalMap != null ? 1f : 0f);
        SetFloatIfPresent(birdMaterial, UseThicknessMapId, thicknessMap != null ? 1f : 0f);
        SetFloatIfPresent(birdMaterial, UsePackedMasksId, packedMasksMap != null ? 1f : 0f);
        SetTextureIfPresent(birdMaterial, "_MainTex", baseMap);
    }

    private Color ResolveAmbientTop()
    {
        Color top = ambientTopColor.linear;
        if (includeRenderSettingsAmbient)
        {
            top += RenderSettings.ambientLight.linear * renderSettingsAmbientScale;
        }

        return top;
    }

    private Color ResolveAmbientBottom()
    {
        Color bottom = ambientBottomColor.linear;
        if (includeRenderSettingsAmbient)
        {
            bottom += RenderSettings.ambientLight.linear * renderSettingsAmbientScale * 0.65f;
        }

        return bottom;
    }

    private Camera ResolveCamera()
    {
        Camera activeCamera = Camera.main;
#if UNITY_EDITOR
        if (!Application.isPlaying && SceneView.lastActiveSceneView != null)
        {
            activeCamera = SceneView.lastActiveSceneView.camera;
        }
#endif
        return activeCamera;
    }

    private static double ResolveTime()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return EditorApplication.timeSinceStartup;
        }
#endif
        return Time.realtimeSinceStartupAsDouble;
    }

    private void RefreshLighting(Camera activeCamera)
    {
        Vector3 anchor = transform.position;
        if (activeCamera != null)
        {
            anchor = Vector3.Lerp(anchor, activeCamera.transform.position, 0.08f);
        }

        int lightCount = CollectBestLights(anchor);
        ApplyLightingToMaterial(birdMaterial, lightCount);
    }

    private int CollectBestLights(Vector3 anchor)
    {
        Array.Clear(bestLights, 0, bestLights.Length);
        Array.Clear(bestLightScores, 0, bestLightScores.Length);

#if UNITY_2023_1_OR_NEWER
        Light[] sceneLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
#else
        Light[] sceneLights = Object.FindObjectsOfType<Light>();
#endif
        foreach (Light lightSource in sceneLights)
        {
            if (lightSource == null || !lightSource.isActiveAndEnabled || lightSource.intensity <= 0f)
            {
                continue;
            }

            float score = ScoreLight(lightSource, anchor);
            if (score <= 0f)
            {
                continue;
            }

            InsertLight(lightSource, score);
        }

        int count = 0;
        for (int i = 0; i < maxLights; i++)
        {
            Light lightSource = bestLights[i];
            if (lightSource == null)
            {
                continue;
            }

            EncodeLight(lightSource, count++);
        }

        return count;
    }

    private float ScoreLight(Light lightSource, Vector3 anchor)
    {
        if (lightSource.type == LightType.Directional)
        {
            return Mathf.Max(0f, lightSource.intensity);
        }

        float range = Mathf.Max(0.01f, lightSource.range);
        float distance = Vector3.Distance(anchor, lightSource.transform.position);
        if (distance >= range)
        {
            return 0f;
        }

        float distanceFade = 1f - distance / range;
        return Mathf.Max(0f, lightSource.intensity * distanceFade * distanceFade);
    }

    private void InsertLight(Light lightSource, float score)
    {
        for (int i = 0; i < maxLights; i++)
        {
            if (score <= bestLightScores[i])
            {
                continue;
            }

            for (int j = maxLights - 1; j > i; j--)
            {
                bestLightScores[j] = bestLightScores[j - 1];
                bestLights[j] = bestLights[j - 1];
            }

            bestLightScores[i] = score;
            bestLights[i] = lightSource;
            return;
        }
    }

    private void EncodeLight(Light lightSource, int index)
    {
        float scale = lightSource.type == LightType.Directional ? directionalLightScale : punctualLightScale;
        Color color = lightSource.color.linear * Mathf.Min(lightSource.intensity * scale, maxNormalizedLightIntensity);
        manualLightColors[index] = new Vector4(color.r, color.g, color.b, 1f);

        if (lightSource.type == LightType.Directional)
        {
            Vector3 directionToLight = -lightSource.transform.forward;
            manualLightPositions[index] = Vector4.zero;
            manualLightDirections[index] = new Vector4(directionToLight.x, directionToLight.y, directionToLight.z, 0f);
            manualLightData0[index] = new Vector4(0f, 0f, 0f, 0f);
            manualLightData1[index] = Vector4.zero;
            return;
        }

        Vector3 position = lightSource.transform.position;
        manualLightPositions[index] = new Vector4(position.x, position.y, position.z, 1f);
        Vector3 spotDirection = lightSource.transform.forward;
        manualLightDirections[index] = new Vector4(spotDirection.x, spotDirection.y, spotDirection.z, 0f);

        if (lightSource.type == LightType.Spot)
        {
            float outer = Mathf.Cos(lightSource.spotAngle * 0.5f * Mathf.Deg2Rad);
            float inner = Mathf.Cos(lightSource.innerSpotAngle * 0.5f * Mathf.Deg2Rad);
            manualLightData0[index] = new Vector4(2f, lightSource.range, 0f, 0f);
            manualLightData1[index] = new Vector4(inner, outer, 0f, 0f);
        }
        else
        {
            manualLightData0[index] = new Vector4(1f, lightSource.range, 0f, 0f);
            manualLightData1[index] = Vector4.zero;
        }
    }

    private void ApplyLightingToMaterial(Material material, int lightCount)
    {
        if (material == null)
        {
            return;
        }

        material.SetFloat(ManualLightCountId, lightCount);
        material.SetVectorArray(ManualLightPositionsId, manualLightPositions);
        material.SetVectorArray(ManualLightDirectionsId, manualLightDirections);
        material.SetVectorArray(ManualLightColorsId, manualLightColors);
        material.SetVectorArray(ManualLightData0Id, manualLightData0);
        material.SetVectorArray(ManualLightData1Id, manualLightData1);
    }

    private static void SetTextureIfPresent(Material material, int propertyId, Texture texture)
    {
        if (material != null && material.HasProperty(propertyId) && texture != null)
        {
            material.SetTexture(propertyId, texture);
        }
    }

    private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
    {
        if (material != null && material.HasProperty(propertyName) && texture != null)
        {
            material.SetTexture(propertyName, texture);
        }
    }

    private static void SetColorIfPresent(Material material, int propertyId, Color value)
    {
        if (material != null && material.HasProperty(propertyId))
        {
            material.SetColor(propertyId, value);
        }
    }

    private static void SetFloatIfPresent(Material material, int propertyId, float value)
    {
        if (material != null && material.HasProperty(propertyId))
        {
            material.SetFloat(propertyId, value);
        }
    }

    private static void SetVectorIfPresent(Material material, int propertyId, Vector4 value)
    {
        if (material != null && material.HasProperty(propertyId))
        {
            material.SetVector(propertyId, value);
        }
    }

    private void DestroyGeneratedRoot()
    {
        GameObject rootToDestroy = generatedRoot;
        if (rootToDestroy == null)
        {
            Transform existing = transform.Find(GeneratedRootName);
            rootToDestroy = existing != null ? existing.gameObject : null;
        }

        if (rootToDestroy != null)
        {
            DestroyUnityObject(rootToDestroy);
        }

        generatedRoot = null;
        birdRenderer = null;
        DestroyUnityObject(birdMesh);
        birdMesh = null;
    }

    private void DestroyRuntimeMaterial()
    {
        DestroyUnityObject(birdMaterial);
        birdMaterial = null;
    }

    private static void DestroyUnityObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}

#if UNITY_EDITOR
[InitializeOnLoad]
internal static class RetroBirdEditorPreviewBridge
{
    private const double UpdateInterval = 0.12;

    private static double nextUpdateTime;

    static RetroBirdEditorPreviewBridge()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        AssemblyReloadEvents.afterAssemblyReload -= QueueFullRefresh;
        AssemblyReloadEvents.afterAssemblyReload += QueueFullRefresh;
        QueueFullRefresh();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.ExitingPlayMode)
        {
            QueueFullRefresh();
        }
    }

    private static void QueueFullRefresh()
    {
        EditorApplication.delayCall -= FullRefresh;
        EditorApplication.delayCall += FullRefresh;
    }

    private static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now < nextUpdateTime)
        {
            return;
        }

        nextUpdateTime = now + UpdateInterval;
        RefreshVisibleBirds(false);
    }

    private static void FullRefresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        RefreshVisibleBirds(true);
        SceneView.RepaintAll();
    }

    private static void RefreshVisibleBirds(bool force)
    {
        RetroBirdRenderer[] birds = Object.FindObjectsByType<RetroBirdRenderer>(FindObjectsInactive.Exclude);
        foreach (RetroBirdRenderer bird in birds)
        {
            if (bird == null)
            {
                continue;
            }

            bird.EnsureEditorPreview(force);
        }
    }
}
#endif
