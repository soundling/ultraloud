using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public enum RetroVegetationPatchKind
{
    GroundCover = 0,
    Bush = 1,
    Mixed = 2
}

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(62)]
public sealed class RetroVegetationPatch : MonoBehaviour
{
    private const string GeneratedRootName = "__VegetationPatchGenerated";
    private const string FoliageShaderName = "Ultraloud/Nature/Hybrid Tree Leaves HDRP";
    private const int MaxShaderLights = 4;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
    private static readonly int DepthMapId = Shader.PropertyToID("_DepthMap");
    private static readonly int ThicknessMapId = Shader.PropertyToID("_ThicknessMap");
    private static readonly int DensityMapId = Shader.PropertyToID("_DensityMap");
    private static readonly int WindMapId = Shader.PropertyToID("_WindMap");
    private static readonly int AlphaCutoffId = Shader.PropertyToID("_AlphaCutoff");
    private static readonly int CoverageSoftnessId = Shader.PropertyToID("_CoverageSoftness");
    private static readonly int NormalScaleId = Shader.PropertyToID("_NormalScale");
    private static readonly int CanopyNormalBendId = Shader.PropertyToID("_CanopyNormalBend");
    private static readonly int WrapDiffuseId = Shader.PropertyToID("_WrapDiffuse");
    private static readonly int DensityShadowStrengthId = Shader.PropertyToID("_DensityShadowStrength");
    private static readonly int DepthSelfShadowStrengthId = Shader.PropertyToID("_DepthSelfShadowStrength");
    private static readonly int SurfaceRoughnessId = Shader.PropertyToID("_SurfaceRoughness");
    private static readonly int SpecularStrengthId = Shader.PropertyToID("_SpecularStrength");
    private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
    private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
    private static readonly int TransmissionColorId = Shader.PropertyToID("_TransmissionColor");
    private static readonly int TransmissionStrengthId = Shader.PropertyToID("_TransmissionStrength");
    private static readonly int TransmissionPowerId = Shader.PropertyToID("_TransmissionPower");
    private static readonly int AmbientTopColorId = Shader.PropertyToID("_AmbientTopColor");
    private static readonly int AmbientBottomColorId = Shader.PropertyToID("_AmbientBottomColor");
    private static readonly int AmbientIntensityId = Shader.PropertyToID("_AmbientIntensity");
    private static readonly int UseNormalMapId = Shader.PropertyToID("_UseNormalMap");
    private static readonly int UseDepthMapId = Shader.PropertyToID("_UseDepthMap");
    private static readonly int UseThicknessMapId = Shader.PropertyToID("_UseThicknessMap");
    private static readonly int UseDensityMapId = Shader.PropertyToID("_UseDensityMap");
    private static readonly int ManualLightCountId = Shader.PropertyToID("_ManualLightCount");
    private static readonly int ManualLightPositionsId = Shader.PropertyToID("_ManualLightPositionWS");
    private static readonly int ManualLightDirectionsId = Shader.PropertyToID("_ManualLightDirectionWS");
    private static readonly int ManualLightColorsId = Shader.PropertyToID("_ManualLightColor");
    private static readonly int ManualLightData0Id = Shader.PropertyToID("_ManualLightData0");
    private static readonly int ManualLightData1Id = Shader.PropertyToID("_ManualLightData1");

    [Header("Maps")]
    [SerializeField] private Texture2D baseMap;
    [SerializeField] private Texture2D normalMap;
    [SerializeField] private Texture2D depthMap;
    [SerializeField] private Texture2D thicknessMap;
    [SerializeField] private Texture2D densityMap;
    [SerializeField] private Texture2D windMap;
    [SerializeField, Range(1, 8)] private int atlasColumns = 4;
    [SerializeField, Range(1, 8)] private int atlasRows = 4;

    [Header("Patch")]
    [SerializeField] private RetroVegetationPatchKind patchKind = RetroVegetationPatchKind.Mixed;
    [SerializeField, Range(4, 384)] private int cardCount = 96;
    [SerializeField, Min(0.1f)] private float patchRadius = 3.25f;
    [SerializeField, Min(0.05f)] private float patchHeight = 1.35f;
    [SerializeField, Min(0.02f)] private float minCardSize = 0.36f;
    [SerializeField, Min(0.02f)] private float maxCardSize = 1.05f;
    [SerializeField, Range(0f, 1f)] private float horizontalCardRatio = 0.24f;
    [SerializeField, Range(0f, 1f)] private float centerDensity = 0.64f;
    [SerializeField, Min(0f)] private float groundOffset = 0.025f;
    [SerializeField] private int seed = 49217;

    [Header("Wind")]
    [SerializeField] private bool animateWind = true;
    [SerializeField, Range(0f, 1f)] private float windStrength = 0.11f;
    [SerializeField, Min(0f)] private float windSpeed = 1.6f;
    [SerializeField, Range(0f, 1f)] private float gustVariation = 0.52f;

    [Header("Shading")]
    [SerializeField] private Color tint = Color.white;
    [SerializeField, Range(0f, 0.4f)] private float alphaCutoff = 0.08f;
    [SerializeField, Range(0f, 0.2f)] private float coverageSoftness = 0.045f;
    [SerializeField, Range(0f, 2f)] private float normalScale = 1f;
    [SerializeField, Range(0f, 2f)] private float normalBend = 0.32f;
    [SerializeField, Range(0f, 1f)] private float wrapDiffuse = 0.58f;
    [SerializeField, Range(0f, 2f)] private float densityShadowStrength = 0.7f;
    [SerializeField, Range(0f, 2f)] private float depthSelfShadowStrength = 0.3f;
    [SerializeField, Range(0f, 1f)] private float surfaceRoughness = 0.84f;
    [SerializeField, Range(0f, 2f)] private float specularStrength = 0.1f;
    [SerializeField, Range(0f, 2f)] private float rimStrength = 0.13f;
    [SerializeField, Range(0.5f, 8f)] private float rimPower = 3.2f;
    [SerializeField] private Color transmissionColor = new Color(0.75f, 0.95f, 0.38f, 1f);
    [SerializeField, Range(0f, 4f)] private float transmissionStrength = 0.78f;
    [SerializeField, Range(0.5f, 8f)] private float transmissionPower = 2.4f;
    [SerializeField] private Color ambientTopColor = new Color(0.55f, 0.62f, 0.52f, 1f);
    [SerializeField] private Color ambientBottomColor = new Color(0.13f, 0.12f, 0.08f, 1f);
    [SerializeField, Min(0f)] private float ambientIntensity = 1.05f;
    [SerializeField, Range(1, MaxShaderLights)] private int maxLights = MaxShaderLights;
    [SerializeField, Min(0.02f)] private float lightRefreshInterval = 0.22f;
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

    private GameObject generatedRoot;
    private MeshRenderer patchRenderer;
    private Mesh patchMesh;
    private Material material;
    private Vector3[] baseVertices;
    private Vector3[] animatedVertices;
    private Vector2[] windData;
    private bool rebuildRequested = true;
    private bool resetAfterWind;
    private int lastBuildHash;
    private double nextLightRefreshTime = double.NegativeInfinity;
#if UNITY_EDITOR
    private bool deferredRebuildQueued;
#endif

    private void Reset()
    {
        ClampSettings();
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
        RebuildIfNeeded(true);
        UpdateRuntime(Time.realtimeSinceStartup);
    }

    private void OnValidate()
    {
        ClampSettings();
        RequestRebuild();
    }

    private void LateUpdate()
    {
        RebuildIfNeeded(false);
        UpdateRuntime(Time.realtimeSinceStartup);
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

    [ContextMenu("Rebuild Vegetation Now")]
    public void RebuildVegetationNow()
    {
        rebuildRequested = true;
        RebuildIfNeeded(true);
    }

    private void ClampSettings()
    {
        cardCount = Mathf.Clamp(cardCount, 4, 384);
        patchRadius = Mathf.Max(0.1f, patchRadius);
        patchHeight = Mathf.Max(0.05f, patchHeight);
        minCardSize = Mathf.Max(0.02f, minCardSize);
        maxCardSize = Mathf.Max(minCardSize, maxCardSize);
        horizontalCardRatio = Mathf.Clamp01(horizontalCardRatio);
        centerDensity = Mathf.Clamp01(centerDensity);
        groundOffset = Mathf.Max(0f, groundOffset);
        atlasColumns = Mathf.Clamp(atlasColumns, 1, 8);
        atlasRows = Mathf.Clamp(atlasRows, 1, 8);
        windStrength = Mathf.Clamp01(windStrength);
        windSpeed = Mathf.Max(0f, windSpeed);
        gustVariation = Mathf.Clamp01(gustVariation);
        alphaCutoff = Mathf.Clamp(alphaCutoff, 0f, 0.4f);
        coverageSoftness = Mathf.Clamp(coverageSoftness, 0f, 0.2f);
        normalScale = Mathf.Clamp(normalScale, 0f, 2f);
        normalBend = Mathf.Clamp(normalBend, 0f, 2f);
        wrapDiffuse = Mathf.Clamp01(wrapDiffuse);
        densityShadowStrength = Mathf.Clamp(densityShadowStrength, 0f, 2f);
        depthSelfShadowStrength = Mathf.Clamp(depthSelfShadowStrength, 0f, 2f);
        surfaceRoughness = Mathf.Clamp01(surfaceRoughness);
        specularStrength = Mathf.Clamp(specularStrength, 0f, 2f);
        rimStrength = Mathf.Clamp(rimStrength, 0f, 2f);
        rimPower = Mathf.Clamp(rimPower, 0.5f, 8f);
        transmissionStrength = Mathf.Clamp(transmissionStrength, 0f, 4f);
        transmissionPower = Mathf.Clamp(transmissionPower, 0.5f, 8f);
        ambientIntensity = Mathf.Max(0f, ambientIntensity);
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
        if (deferredRebuildQueued || Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        deferredRebuildQueued = true;
        EditorApplication.delayCall += DeferredEditorRebuild;
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
        UpdateRuntime((float)EditorApplication.timeSinceStartup);
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

        UpdateRuntime((float)EditorApplication.timeSinceStartup);
    }
#endif

    private void RebuildIfNeeded(bool force)
    {
        int hash = ComputeBuildHash();
        if (!force && !rebuildRequested && hash == lastBuildHash)
        {
            return;
        }

        RebuildInternal();
        lastBuildHash = hash;
        rebuildRequested = false;
    }

    private bool HasGeneratedRoot()
    {
        if (generatedRoot != null)
        {
            return true;
        }

        Transform existing = transform.Find(GeneratedRootName);
        if (existing == null)
        {
            return false;
        }

        generatedRoot = existing.gameObject;
        return true;
    }

    private int ComputeBuildHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + patchKind.GetHashCode();
            hash = hash * 31 + cardCount;
            hash = hash * 31 + patchRadius.GetHashCode();
            hash = hash * 31 + patchHeight.GetHashCode();
            hash = hash * 31 + minCardSize.GetHashCode();
            hash = hash * 31 + maxCardSize.GetHashCode();
            hash = hash * 31 + horizontalCardRatio.GetHashCode();
            hash = hash * 31 + centerDensity.GetHashCode();
            hash = hash * 31 + atlasColumns;
            hash = hash * 31 + atlasRows;
            hash = hash * 31 + seed;
            hash = hash * 31 + GetAssetHash(baseMap);
            hash = hash * 31 + GetAssetHash(normalMap);
            hash = hash * 31 + GetAssetHash(depthMap);
            return hash;
        }
    }

    private static int GetAssetHash(Object asset)
    {
        return asset != null ? asset.GetHashCode() : 0;
    }

    private void RebuildInternal()
    {
        DestroyRuntimeMaterial();
        DestroyGeneratedRoot();

        generatedRoot = new GameObject(GeneratedRootName);
        generatedRoot.hideFlags = HideFlags.DontSave;
        generatedRoot.transform.SetParent(transform, false);
        generatedRoot.transform.localPosition = Vector3.zero;
        generatedRoot.transform.localRotation = Quaternion.identity;
        generatedRoot.transform.localScale = Vector3.one;

        GameObject cards = new GameObject("VegetationCards");
        cards.hideFlags = HideFlags.DontSave;
        cards.transform.SetParent(generatedRoot.transform, false);
        MeshFilter filter = cards.AddComponent<MeshFilter>();
        patchRenderer = cards.AddComponent<MeshRenderer>();
        patchRenderer.shadowCastingMode = ShadowCastingMode.On;
        patchRenderer.receiveShadows = false;
        patchMesh = BuildPatchMesh();
        filter.sharedMesh = patchMesh;
        ApplyMaterial();
    }

    private Mesh BuildPatchMesh()
    {
        List<Vector3> vertices = new List<Vector3>(cardCount * 4);
        List<Vector3> normals = new List<Vector3>(cardCount * 4);
        List<Vector4> tangents = new List<Vector4>(cardCount * 4);
        List<Vector2> uvs = new List<Vector2>(cardCount * 4);
        List<Vector2> uv2 = new List<Vector2>(cardCount * 4);
        List<int> indices = new List<int>(cardCount * 6);

        System.Random random = new System.Random(seed);
        for (int i = 0; i < cardCount; i++)
        {
            AddVegetationCard(vertices, normals, tangents, uvs, uv2, indices, random);
        }

        Mesh mesh = new Mesh
        {
            name = "Generated Vegetation Patch",
            hideFlags = HideFlags.DontSave
        };
        mesh.MarkDynamic();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTangents(tangents);
        mesh.SetUVs(0, uvs);
        mesh.SetUVs(1, uv2);
        mesh.SetTriangles(indices, 0);
        mesh.bounds = BuildPatchBounds();

        baseVertices = vertices.ToArray();
        animatedVertices = vertices.ToArray();
        windData = uv2.ToArray();
        resetAfterWind = true;
        return mesh;
    }

    private Bounds BuildPatchBounds()
    {
        float height = Mathf.Max(patchHeight * 1.5f, maxCardSize * 2.2f) + windStrength * 2f;
        Vector3 size = new Vector3(patchRadius * 2.6f + maxCardSize, height, patchRadius * 2.6f + maxCardSize);
        return new Bounds(new Vector3(0f, height * 0.42f, 0f), size);
    }

    private void AddVegetationCard(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector4> tangents,
        List<Vector2> uvs,
        List<Vector2> uv2,
        List<int> indices,
        System.Random random)
    {
        Vector3 center = SamplePatchPosition(random);
        float horizontalChance = ResolveHorizontalChance(center);
        bool horizontal = RandomRange(random, 0f, 1f) < horizontalChance;
        float radialFactor = Mathf.Clamp01(new Vector2(center.x, center.z).magnitude / Mathf.Max(0.001f, patchRadius));
        float size = RandomRange(random, minCardSize, maxCardSize) * Mathf.Lerp(1.12f, 0.78f, radialFactor);
        float aspect = RandomRange(random, 0.72f, 1.48f);

        if (horizontal)
        {
            AddHorizontalCard(vertices, normals, tangents, uvs, uv2, indices, random, center, size, aspect);
        }
        else
        {
            AddVerticalCard(vertices, normals, tangents, uvs, uv2, indices, random, center, size, aspect);
        }
    }

    private Vector3 SamplePatchPosition(System.Random random)
    {
        float angle = RandomRange(random, 0f, Mathf.PI * 2f);
        float distancePower = Mathf.Lerp(0.5f, 1.6f, centerDensity);
        float distance = Mathf.Pow(RandomRange(random, 0f, 1f), distancePower) * patchRadius;
        return new Vector3(Mathf.Cos(angle) * distance, groundOffset, Mathf.Sin(angle) * distance);
    }

    private float ResolveHorizontalChance(Vector3 center)
    {
        switch (patchKind)
        {
            case RetroVegetationPatchKind.GroundCover:
                return Mathf.Max(horizontalCardRatio, 0.48f);
            case RetroVegetationPatchKind.Bush:
                return Mathf.Min(horizontalCardRatio, 0.16f);
            default:
                float radial = Mathf.Clamp01(new Vector2(center.x, center.z).magnitude / Mathf.Max(0.001f, patchRadius));
                return Mathf.Lerp(horizontalCardRatio * 0.55f, horizontalCardRatio * 1.3f, radial);
        }
    }

    private void AddHorizontalCard(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector4> tangents,
        List<Vector2> uvs,
        List<Vector2> uv2,
        List<int> indices,
        System.Random random,
        Vector3 center,
        float size,
        float aspect)
    {
        float yaw = RandomRange(random, 0f, Mathf.PI * 2f);
        Vector3 right = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)).normalized;
        Vector3 forward = new Vector3(-right.z, 0f, right.x);
        float width = size * aspect * RandomRange(random, 0.92f, 1.6f);
        float depth = size * RandomRange(random, 0.62f, 1.05f);
        Vector3 halfRight = right * (width * 0.5f);
        Vector3 halfForward = forward * (depth * 0.5f);
        int baseIndex = vertices.Count;

        vertices.Add(center - halfRight - halfForward);
        vertices.Add(center + halfRight - halfForward);
        vertices.Add(center + halfRight + halfForward);
        vertices.Add(center - halfRight + halfForward);

        AddSharedSurfaceData(normals, tangents, Vector3.up, new Vector4(right.x, right.y, right.z, 1f));
        AddAtlasUvs(uvs, PickAtlasCell(random));
        AddWindData(uv2, random, center, 0.28f);
        AddQuadIndices(indices, baseIndex);
    }

    private void AddVerticalCard(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector4> tangents,
        List<Vector2> uvs,
        List<Vector2> uv2,
        List<int> indices,
        System.Random random,
        Vector3 groundCenter,
        float size,
        float aspect)
    {
        float kindHeightScale = patchKind == RetroVegetationPatchKind.Bush ? 1.45f : patchKind == RetroVegetationPatchKind.Mixed ? 1.12f : 0.78f;
        float height = Mathf.Clamp(size * kindHeightScale * RandomRange(random, 0.85f, 1.5f), minCardSize, patchHeight);
        float width = Mathf.Max(0.05f, height * aspect * RandomRange(random, 0.82f, 1.26f));
        Vector3 center = groundCenter + Vector3.up * (height * 0.5f);
        Vector3 radial = new Vector3(center.x, 0f, center.z);
        Vector3 radialDirection = radial.sqrMagnitude > 0.0001f ? radial.normalized : Vector3.forward;
        float yaw = Mathf.Atan2(radialDirection.z, radialDirection.x) + Mathf.PI * 0.5f + RandomRange(random, -1.15f, 1.15f);
        Vector3 right = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)).normalized;
        Vector3 up = (Vector3.up + radialDirection * RandomRange(random, -0.18f, 0.28f)).normalized;
        Vector3 forward = Vector3.Cross(right, up).normalized;
        if (Vector3.Dot(forward, radialDirection) < 0f)
        {
            forward = -forward;
        }

        right = Vector3.Cross(up, forward).normalized;
        Vector3 halfRight = right * (width * 0.5f);
        Vector3 halfUp = up * (height * 0.5f);
        int baseIndex = vertices.Count;

        vertices.Add(center - halfRight - halfUp);
        vertices.Add(center + halfRight - halfUp);
        vertices.Add(center + halfRight + halfUp);
        vertices.Add(center - halfRight + halfUp);

        AddSharedSurfaceData(normals, tangents, forward, new Vector4(right.x, right.y, right.z, 1f));
        AddAtlasUvs(uvs, PickAtlasCell(random));
        float amplitude = Mathf.Lerp(0.65f, 1.25f, Mathf.Clamp01(height / Mathf.Max(0.01f, patchHeight)));
        AddWindData(uv2, random, center, amplitude);
        AddQuadIndices(indices, baseIndex);
    }

    private int PickAtlasCell(System.Random random)
    {
        int cellCount = Mathf.Max(1, atlasColumns * atlasRows);
        return Mathf.Clamp((int)(RandomRange(random, 0f, 0.9999f) * cellCount), 0, cellCount - 1);
    }

    private void AddAtlasUvs(List<Vector2> uvs, int cell)
    {
        const float Pad = 0.012f;
        int column = cell % atlasColumns;
        int row = cell / atlasColumns;
        float cellWidth = 1f / atlasColumns;
        float cellHeight = 1f / atlasRows;
        float x0 = column * cellWidth + Pad * cellWidth;
        float x1 = (column + 1) * cellWidth - Pad * cellWidth;
        float y0 = 1f - (row + 1) * cellHeight + Pad * cellHeight;
        float y1 = 1f - row * cellHeight - Pad * cellHeight;
        uvs.Add(new Vector2(x0, y0));
        uvs.Add(new Vector2(x1, y0));
        uvs.Add(new Vector2(x1, y1));
        uvs.Add(new Vector2(x0, y1));
    }

    private static void AddSharedSurfaceData(List<Vector3> normals, List<Vector4> tangents, Vector3 normal, Vector4 tangent)
    {
        for (int i = 0; i < 4; i++)
        {
            normals.Add(normal);
            tangents.Add(tangent);
        }
    }

    private static void AddWindData(List<Vector2> uv2, System.Random random, Vector3 center, float amplitude)
    {
        float phase = RandomRange(random, 0f, Mathf.PI * 2f) + center.x * 0.41f + center.z * 0.31f;
        uv2.Add(new Vector2(phase + 0.07f, amplitude * 0.28f));
        uv2.Add(new Vector2(phase + 0.23f, amplitude * 0.42f));
        uv2.Add(new Vector2(phase + 0.51f, amplitude));
        uv2.Add(new Vector2(phase + 0.71f, amplitude));
    }

    private static void AddQuadIndices(List<int> indices, int baseIndex)
    {
        indices.Add(baseIndex);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 1);
        indices.Add(baseIndex);
        indices.Add(baseIndex + 3);
        indices.Add(baseIndex + 2);
    }

    private void UpdateRuntime(float time)
    {
        ApplyMaterial();
        ApplyMaterialSettings(material);
        AnimatePatch(time);
        if (time >= nextLightRefreshTime)
        {
            nextLightRefreshTime = time + lightRefreshInterval;
            RefreshLighting(ResolveCamera());
        }
    }

    private void AnimatePatch(float time)
    {
        if (patchMesh == null || baseVertices == null || animatedVertices == null || windData == null)
        {
            return;
        }

        if (!animateWind || windStrength <= 0.0001f)
        {
            if (!resetAfterWind)
            {
                patchMesh.vertices = baseVertices;
                patchMesh.bounds = BuildPatchBounds();
                resetAfterWind = true;
            }

            return;
        }

        for (int i = 0; i < baseVertices.Length; i++)
        {
            Vector3 baseVertex = baseVertices[i];
            Vector2 wind = windData[i];
            Vector3 radial = new Vector3(baseVertex.x, 0f, baseVertex.z);
            Vector3 radialDirection = radial.sqrMagnitude > 0.0001f ? radial.normalized : Vector3.forward;
            Vector3 sideDirection = Vector3.Cross(Vector3.up, radialDirection).normalized;
            float heightFactor = Mathf.Clamp01(baseVertex.y / Mathf.Max(0.01f, patchHeight));
            float primary = Mathf.Sin(time * windSpeed + wind.x);
            float gust = Mathf.Sin(time * windSpeed * 0.33f + wind.x * 1.7f) * gustVariation;
            float displacement = (primary + gust) * windStrength * wind.y * Mathf.Lerp(0.25f, 1f, heightFactor);
            Vector3 offset = (radialDirection * 0.68f + sideDirection * 0.32f) * displacement;
            offset.y = Mathf.Sin(time * windSpeed * 1.37f + wind.x * 0.7f) * windStrength * 0.08f * wind.y * heightFactor;
            animatedVertices[i] = baseVertex + offset;
        }

        patchMesh.vertices = animatedVertices;
        patchMesh.bounds = BuildPatchBounds();
        resetAfterWind = false;
    }

    private void ApplyMaterial()
    {
        if (patchRenderer == null)
        {
            return;
        }

        material ??= CreateFoliageMaterial();
        patchRenderer.sharedMaterial = material;
    }

    private Material CreateFoliageMaterial()
    {
        Shader shader = Shader.Find(FoliageShaderName)
            ?? Shader.Find("Ultraloud/Directional Sprites/Billboard Lit HDRP")
            ?? Shader.Find("HDRP/Unlit")
            ?? Shader.Find("Unlit/Transparent");
        Material createdMaterial = new Material(shader)
        {
            name = "Runtime Vegetation Patch",
            hideFlags = HideFlags.DontSave
        };
        ApplyMaterialSettings(createdMaterial);
        return createdMaterial;
    }

    private void ApplyMaterialSettings(Material targetMaterial)
    {
        if (targetMaterial == null)
        {
            return;
        }

        SetTextureIfPresent(targetMaterial, BaseMapId, baseMap);
        SetTextureIfPresent(targetMaterial, NormalMapId, normalMap);
        SetTextureIfPresent(targetMaterial, DepthMapId, depthMap);
        SetTextureIfPresent(targetMaterial, ThicknessMapId, thicknessMap);
        SetTextureIfPresent(targetMaterial, DensityMapId, densityMap);
        SetTextureIfPresent(targetMaterial, WindMapId, windMap);
        SetColorIfPresent(targetMaterial, BaseColorId, tint);
        SetFloatIfPresent(targetMaterial, AlphaCutoffId, alphaCutoff);
        SetFloatIfPresent(targetMaterial, CoverageSoftnessId, coverageSoftness);
        SetFloatIfPresent(targetMaterial, NormalScaleId, normalScale);
        SetFloatIfPresent(targetMaterial, CanopyNormalBendId, normalBend);
        SetFloatIfPresent(targetMaterial, WrapDiffuseId, wrapDiffuse);
        SetFloatIfPresent(targetMaterial, DensityShadowStrengthId, densityShadowStrength);
        SetFloatIfPresent(targetMaterial, DepthSelfShadowStrengthId, depthSelfShadowStrength);
        SetFloatIfPresent(targetMaterial, SurfaceRoughnessId, surfaceRoughness);
        SetFloatIfPresent(targetMaterial, SpecularStrengthId, specularStrength);
        SetFloatIfPresent(targetMaterial, RimStrengthId, rimStrength);
        SetFloatIfPresent(targetMaterial, RimPowerId, rimPower);
        SetColorIfPresent(targetMaterial, TransmissionColorId, transmissionColor);
        SetFloatIfPresent(targetMaterial, TransmissionStrengthId, transmissionStrength);
        SetFloatIfPresent(targetMaterial, TransmissionPowerId, transmissionPower);
        SetColorIfPresent(targetMaterial, AmbientTopColorId, ambientTopColor.linear + RenderSettings.ambientLight.linear * 0.22f);
        SetColorIfPresent(targetMaterial, AmbientBottomColorId, ambientBottomColor.linear + RenderSettings.ambientLight.linear * 0.12f);
        SetFloatIfPresent(targetMaterial, AmbientIntensityId, ambientIntensity);
        SetFloatIfPresent(targetMaterial, UseNormalMapId, normalMap != null ? 1f : 0f);
        SetFloatIfPresent(targetMaterial, UseDepthMapId, depthMap != null ? 1f : 0f);
        SetFloatIfPresent(targetMaterial, UseThicknessMapId, thicknessMap != null ? 1f : 0f);
        SetFloatIfPresent(targetMaterial, UseDensityMapId, densityMap != null ? 1f : 0f);
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

    private void RefreshLighting(Camera activeCamera)
    {
        Vector3 anchor = activeCamera != null
            ? Vector3.Lerp(transform.position + Vector3.up * patchHeight * 0.5f, activeCamera.transform.position, 0.15f)
            : transform.position + Vector3.up * patchHeight * 0.5f;
        int lightCount = CollectBestLights(anchor);
        ApplyLightingToMaterial(material, lightCount);
    }

    private int CollectBestLights(Vector3 anchor)
    {
        Array.Clear(bestLights, 0, bestLights.Length);
        Array.Clear(bestLightScores, 0, bestLightScores.Length);

        foreach (Light lightSource in RetroSceneLightCache.ActiveLights)
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
            manualLightData0[index] = Vector4.zero;
            manualLightData1[index] = Vector4.zero;
            return;
        }

        Vector3 position = lightSource.transform.position;
        manualLightPositions[index] = new Vector4(position.x, position.y, position.z, 1f);
        Vector3 spotDirection = lightSource.transform.forward;
        manualLightDirections[index] = new Vector4(spotDirection.x, spotDirection.y, spotDirection.z, 0f);

        if (lightSource.type == LightType.Spot)
        {
            float outerCos = Mathf.Cos(lightSource.spotAngle * 0.5f * Mathf.Deg2Rad);
            float innerCos = Mathf.Cos(lightSource.innerSpotAngle * 0.5f * Mathf.Deg2Rad);
            manualLightData0[index] = new Vector4(2f, lightSource.range, 0f, 0f);
            manualLightData1[index] = new Vector4(innerCos, outerCos, 0f, 0f);
        }
        else
        {
            manualLightData0[index] = new Vector4(1f, lightSource.range, 0f, 0f);
            manualLightData1[index] = Vector4.zero;
        }
    }

    private void ApplyLightingToMaterial(Material targetMaterial, int lightCount)
    {
        if (targetMaterial == null)
        {
            return;
        }

        targetMaterial.SetFloat(ManualLightCountId, lightCount);
        targetMaterial.SetVectorArray(ManualLightPositionsId, manualLightPositions);
        targetMaterial.SetVectorArray(ManualLightDirectionsId, manualLightDirections);
        targetMaterial.SetVectorArray(ManualLightColorsId, manualLightColors);
        targetMaterial.SetVectorArray(ManualLightData0Id, manualLightData0);
        targetMaterial.SetVectorArray(ManualLightData1Id, manualLightData1);
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
        patchRenderer = null;
        patchMesh = null;
        baseVertices = null;
        animatedVertices = null;
        windData = null;
    }

    private void DestroyRuntimeMaterial()
    {
        DestroyUnityObject(material);
        material = null;
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

    private static void SetTextureIfPresent(Material targetMaterial, int propertyId, Texture texture)
    {
        if (targetMaterial != null && targetMaterial.HasProperty(propertyId) && texture != null)
        {
            targetMaterial.SetTexture(propertyId, texture);
        }
    }

    private static void SetColorIfPresent(Material targetMaterial, int propertyId, Color value)
    {
        if (targetMaterial != null && targetMaterial.HasProperty(propertyId))
        {
            targetMaterial.SetColor(propertyId, value);
        }
    }

    private static void SetFloatIfPresent(Material targetMaterial, int propertyId, float value)
    {
        if (targetMaterial != null && targetMaterial.HasProperty(propertyId))
        {
            targetMaterial.SetFloat(propertyId, value);
        }
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}

#if UNITY_EDITOR
[InitializeOnLoad]
internal static class RetroVegetationPatchEditorPreviewBridge
{
    private const double UpdateInterval = 0.25;

    private static double nextUpdateTime;

    static RetroVegetationPatchEditorPreviewBridge()
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
        RefreshVisiblePatches(false);
    }

    private static void FullRefresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        RefreshVisiblePatches(true);
        SceneView.RepaintAll();
    }

    private static void RefreshVisiblePatches(bool force)
    {
        RetroVegetationPatch[] patches = Object.FindObjectsByType<RetroVegetationPatch>(FindObjectsInactive.Exclude);
        foreach (RetroVegetationPatch patch in patches)
        {
            if (patch == null)
            {
                continue;
            }

            patch.EnsureEditorPreview(force);
        }
    }
}
#endif
