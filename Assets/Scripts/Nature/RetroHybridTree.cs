using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(60)]
public sealed class RetroHybridTree : MonoBehaviour
{
    private const string GeneratedRootName = "__HybridTreeGenerated";
    private const string LeafShaderName = "Ultraloud/Nature/Hybrid Tree Leaves HDRP";
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

    [Header("Leaf Maps")]
    [SerializeField] private Texture2D leafBaseMap;
    [SerializeField] private Texture2D leafNormalMap;
    [SerializeField] private Texture2D leafDepthMap;
    [SerializeField] private Texture2D leafThicknessMap;
    [SerializeField] private Texture2D leafDensityMap;
    [SerializeField] private Texture2D leafWindMap;

    [Header("Bark Maps")]
    [SerializeField] private Texture2D barkBaseMap;
    [SerializeField] private Texture2D barkNormalMap;

    [Header("Impostor Maps")]
    [SerializeField] private Texture2D impostorBaseMap;
    [SerializeField] private Texture2D impostorNormalMap;
    [SerializeField] private Texture2D impostorDepthMap;
    [SerializeField] private Texture2D impostorThicknessMap;

    [Header("Shape")]
    [SerializeField, Min(0.5f)] private float treeHeight = 5.6f;
    [SerializeField, Min(0.25f)] private float trunkHeight = 3.25f;
    [SerializeField, Min(0.02f)] private float trunkRadius = 0.22f;
    [SerializeField, Min(0.01f)] private float branchRadius = 0.07f;
    [SerializeField, Range(0, 24)] private int branchCount = 11;
    [SerializeField, Min(0.25f)] private float canopyRadius = 2.05f;
    [SerializeField, Min(0.25f)] private float canopyHeight = 2.35f;
    [SerializeField, Range(16, 256)] private int leafCardCount = 112;
    [SerializeField, Min(0.05f)] private float leafCardMinSize = 0.82f;
    [SerializeField, Min(0.05f)] private float leafCardMaxSize = 1.55f;
    [SerializeField] private int seed = 71423;

    [Header("Wind")]
    [SerializeField] private bool animateWind = true;
    [SerializeField, Range(0f, 1f)] private float windStrength = 0.18f;
    [SerializeField, Min(0f)] private float windSpeed = 1.35f;
    [SerializeField, Range(0f, 1f)] private float windGustVariation = 0.45f;

    [Header("Leaf Shading")]
    [SerializeField] private Color leafTint = Color.white;
    [SerializeField, Range(0f, 0.4f)] private float alphaCutoff = 0.08f;
    [SerializeField, Range(0f, 0.2f)] private float coverageSoftness = 0.045f;
    [SerializeField, Range(0f, 2f)] private float normalScale = 1f;
    [SerializeField, Range(0f, 2f)] private float canopyNormalBend = 0.38f;
    [SerializeField, Range(0f, 1f)] private float wrapDiffuse = 0.55f;
    [SerializeField, Range(0f, 2f)] private float densityShadowStrength = 0.65f;
    [SerializeField, Range(0f, 2f)] private float depthSelfShadowStrength = 0.32f;
    [SerializeField, Range(0f, 1f)] private float surfaceRoughness = 0.82f;
    [SerializeField, Range(0f, 2f)] private float specularStrength = 0.14f;
    [SerializeField, Range(0f, 2f)] private float rimStrength = 0.1f;
    [SerializeField, Range(0.5f, 8f)] private float rimPower = 3f;
    [SerializeField] private Color transmissionColor = new(0.72f, 0.96f, 0.34f, 1f);
    [SerializeField, Range(0f, 4f)] private float transmissionStrength = 0.8f;
    [SerializeField, Range(0.5f, 8f)] private float transmissionPower = 2.2f;
    [SerializeField] private Color ambientTopColor = new(0.58f, 0.64f, 0.56f, 1f);
    [SerializeField] private Color ambientBottomColor = new(0.13f, 0.14f, 0.09f, 1f);
    [SerializeField, Min(0f)] private float ambientIntensity = 1.05f;
    [SerializeField] private bool includeRenderSettingsAmbient = true;
    [SerializeField, Min(0f)] private float renderSettingsAmbientScale = 0.25f;

    [Header("Scene Lights")]
    [SerializeField, Range(1, MaxShaderLights)] private int maxLights = MaxShaderLights;
    [SerializeField, Min(0.02f)] private float lightRefreshInterval = 0.18f;
    [SerializeField, Min(0f)] private float directionalLightScale = 0.00001f;
    [SerializeField, Min(0f)] private float punctualLightScale = 0.01f;
    [SerializeField, Min(0f)] private float maxNormalizedLightIntensity = 1.55f;

    [Header("LOD")]
    [SerializeField] private bool enableImpostorLod = true;
    [SerializeField, Min(1f)] private float impostorDistance = 34f;
    [SerializeField, Min(0f)] private float impostorFadeBand = 4f;
    [SerializeField] private bool billboardImpostor = true;

    private readonly Vector4[] manualLightPositions = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightDirections = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightColors = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightData0 = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightData1 = new Vector4[MaxShaderLights];
    private readonly Light[] bestLights = new Light[MaxShaderLights];
    private readonly float[] bestLightScores = new float[MaxShaderLights];

    private GameObject generatedRoot;
    private MeshRenderer trunkRenderer;
    private MeshRenderer canopyRenderer;
    private MeshRenderer impostorRenderer;
    private Transform impostorTransform;
    private Mesh canopyMesh;
    private Mesh trunkMesh;
    private Mesh impostorMesh;
    private Vector3[] canopyBaseVertices;
    private Vector3[] canopyAnimatedVertices;
    private Vector2[] canopyWindData;
    private Material leafMaterial;
    private Material barkMaterial;
    private Material impostorMaterial;
    private double nextLightRefreshTime = double.NegativeInfinity;
    private bool rebuildRequested = true;
    private int lastBuildHash;
    private bool canopyResetAfterWind;
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
        DestroyRuntimeMaterials();
    }

    private void OnDestroy()
    {
        DestroyGeneratedRoot();
        DestroyRuntimeMaterials();
    }

    [ContextMenu("Rebuild Tree Now")]
    public void RebuildTreeNow()
    {
        rebuildRequested = true;
        RebuildIfNeeded(true);
    }

    private void ClampSettings()
    {
        treeHeight = Mathf.Max(0.5f, treeHeight);
        trunkHeight = Mathf.Clamp(trunkHeight, 0.25f, Mathf.Max(0.3f, treeHeight - 0.1f));
        trunkRadius = Mathf.Max(0.02f, trunkRadius);
        branchRadius = Mathf.Clamp(branchRadius, 0.01f, trunkRadius);
        branchCount = Mathf.Clamp(branchCount, 0, 24);
        canopyRadius = Mathf.Max(0.25f, canopyRadius);
        canopyHeight = Mathf.Max(0.25f, canopyHeight);
        leafCardCount = Mathf.Clamp(leafCardCount, 16, 256);
        leafCardMinSize = Mathf.Max(0.05f, leafCardMinSize);
        leafCardMaxSize = Mathf.Max(leafCardMinSize, leafCardMaxSize);
        windStrength = Mathf.Clamp01(windStrength);
        windSpeed = Mathf.Max(0f, windSpeed);
        windGustVariation = Mathf.Clamp01(windGustVariation);
        alphaCutoff = Mathf.Clamp(alphaCutoff, 0f, 0.4f);
        coverageSoftness = Mathf.Clamp(coverageSoftness, 0f, 0.2f);
        normalScale = Mathf.Clamp(normalScale, 0f, 2f);
        canopyNormalBend = Mathf.Clamp(canopyNormalBend, 0f, 2f);
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
        renderSettingsAmbientScale = Mathf.Max(0f, renderSettingsAmbientScale);
        maxLights = Mathf.Clamp(maxLights, 1, MaxShaderLights);
        lightRefreshInterval = Mathf.Max(0.02f, lightRefreshInterval);
        directionalLightScale = Mathf.Max(0f, directionalLightScale);
        punctualLightScale = Mathf.Max(0f, punctualLightScale);
        maxNormalizedLightIntensity = Mathf.Max(0f, maxNormalizedLightIntensity);
        impostorDistance = Mathf.Max(1f, impostorDistance);
        impostorFadeBand = Mathf.Max(0f, impostorFadeBand);
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
            hash = hash * 31 + treeHeight.GetHashCode();
            hash = hash * 31 + trunkHeight.GetHashCode();
            hash = hash * 31 + trunkRadius.GetHashCode();
            hash = hash * 31 + branchRadius.GetHashCode();
            hash = hash * 31 + branchCount;
            hash = hash * 31 + canopyRadius.GetHashCode();
            hash = hash * 31 + canopyHeight.GetHashCode();
            hash = hash * 31 + leafCardCount;
            hash = hash * 31 + leafCardMinSize.GetHashCode();
            hash = hash * 31 + leafCardMaxSize.GetHashCode();
            hash = hash * 31 + seed;
            hash = hash * 31 + GetAssetHash(leafBaseMap);
            hash = hash * 31 + GetAssetHash(leafNormalMap);
            hash = hash * 31 + GetAssetHash(barkBaseMap);
            hash = hash * 31 + GetAssetHash(impostorBaseMap);
            return hash;
        }
    }

    private static int GetAssetHash(Object asset)
    {
        return asset != null ? asset.GetHashCode() : 0;
    }

    private void RebuildInternal()
    {
        DestroyRuntimeMaterials();
        DestroyGeneratedRoot();

        generatedRoot = new GameObject(GeneratedRootName);
        generatedRoot.hideFlags = HideFlags.DontSave;
        generatedRoot.transform.SetParent(transform, false);
        generatedRoot.transform.localPosition = Vector3.zero;
        generatedRoot.transform.localRotation = Quaternion.identity;
        generatedRoot.transform.localScale = Vector3.one;

        CreateTrunk(generatedRoot.transform);
        CreateCanopy(generatedRoot.transform);
        CreateImpostor(generatedRoot.transform);
        ApplyMaterials();
        UpdateLod(ResolveCamera());
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

    private void CreateTrunk(Transform parent)
    {
        GameObject trunk = new("TrunkAndBranches");
        trunk.hideFlags = HideFlags.DontSave;
        trunk.transform.SetParent(parent, false);
        MeshFilter filter = trunk.AddComponent<MeshFilter>();
        trunkRenderer = trunk.AddComponent<MeshRenderer>();
        trunkRenderer.shadowCastingMode = ShadowCastingMode.On;
        trunkRenderer.receiveShadows = true;
        trunkMesh = BuildTrunkMesh();
        filter.sharedMesh = trunkMesh;
    }

    private Mesh BuildTrunkMesh()
    {
        List<Vector3> vertices = new();
        List<Vector3> normals = new();
        List<Vector2> uvs = new();
        List<int> indices = new();

        AddCylinder(vertices, normals, uvs, indices, Vector3.zero, new Vector3(0f, trunkHeight, 0f), trunkRadius, trunkRadius * 0.62f, 12, 0f, trunkHeight);

        System.Random random = new(seed * 13 + 31);
        const float GoldenAngle = 2.3999632f;
        for (int i = 0; i < branchCount; i++)
        {
            float t = branchCount <= 1 ? 0.5f : i / (float)(branchCount - 1);
            float angle = i * GoldenAngle + RandomRange(random, -0.28f, 0.28f);
            float startY = Mathf.Lerp(trunkHeight * 0.38f, trunkHeight * 0.95f, Mathf.Pow(t, 0.8f));
            float length = RandomRange(random, canopyRadius * 0.45f, canopyRadius * 1.1f);
            float radialScale = RandomRange(random, 0.72f, 1.05f);
            Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle) * 0.72f);
            Vector3 start = new(0f, startY, 0f);
            Vector3 end = start
                + radial.normalized * length * radialScale
                + Vector3.up * RandomRange(random, 0.15f, canopyHeight * 0.42f);
            end.y = Mathf.Clamp(end.y, trunkHeight * 0.65f, treeHeight * 0.98f);
            float baseRadius = branchRadius * Mathf.Lerp(1.25f, 0.65f, t);
            AddCylinder(vertices, normals, uvs, indices, start, end, baseRadius, baseRadius * 0.28f, 7, 0f, length);
        }

        Mesh mesh = new()
        {
            name = "Generated Hybrid Tree Trunk",
            hideFlags = HideFlags.DontSave
        };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddCylinder(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector2> uvs,
        List<int> indices,
        Vector3 start,
        Vector3 end,
        float startRadius,
        float endRadius,
        int sides,
        float uvStartV,
        float uvLength)
    {
        Vector3 axis = end - start;
        float length = axis.magnitude;
        if (length <= 0.0001f)
        {
            return;
        }

        Vector3 axisNormal = axis / length;
        Vector3 basisA = Vector3.Cross(axisNormal, Vector3.up);
        if (basisA.sqrMagnitude < 0.0001f)
        {
            basisA = Vector3.Cross(axisNormal, Vector3.right);
        }

        basisA.Normalize();
        Vector3 basisB = Vector3.Cross(axisNormal, basisA).normalized;
        int baseIndex = vertices.Count;

        for (int i = 0; i <= sides; i++)
        {
            float u = i / (float)sides;
            float angle = u * Mathf.PI * 2f;
            Vector3 radial = Mathf.Cos(angle) * basisA + Mathf.Sin(angle) * basisB;
            vertices.Add(start + radial * startRadius);
            vertices.Add(end + radial * endRadius);
            normals.Add(radial);
            normals.Add(radial);
            uvs.Add(new Vector2(u, uvStartV));
            uvs.Add(new Vector2(u, uvStartV + uvLength));
        }

        for (int i = 0; i < sides; i++)
        {
            int a = baseIndex + i * 2;
            int b = baseIndex + (i + 1) * 2;
            int c = a + 1;
            int d = b + 1;
            indices.Add(a);
            indices.Add(c);
            indices.Add(d);
            indices.Add(a);
            indices.Add(d);
            indices.Add(b);
        }
    }

    private void CreateCanopy(Transform parent)
    {
        GameObject canopy = new("LeafCards");
        canopy.hideFlags = HideFlags.DontSave;
        canopy.transform.SetParent(parent, false);
        MeshFilter filter = canopy.AddComponent<MeshFilter>();
        canopyRenderer = canopy.AddComponent<MeshRenderer>();
        canopyRenderer.shadowCastingMode = ShadowCastingMode.On;
        canopyRenderer.receiveShadows = false;
        canopyMesh = BuildCanopyMesh();
        filter.sharedMesh = canopyMesh;
    }

    private Mesh BuildCanopyMesh()
    {
        List<Vector3> vertices = new(leafCardCount * 4);
        List<Vector3> normals = new(leafCardCount * 4);
        List<Vector4> tangents = new(leafCardCount * 4);
        List<Vector2> uvs = new(leafCardCount * 4);
        List<Vector2> uv2 = new(leafCardCount * 4);
        List<int> indices = new(leafCardCount * 6);

        System.Random random = new(seed);
        float canopyCenterY = Mathf.Lerp(trunkHeight, treeHeight, 0.62f);
        for (int i = 0; i < leafCardCount; i++)
        {
            AddLeafCard(vertices, normals, tangents, uvs, uv2, indices, random, canopyCenterY);
        }

        Mesh mesh = new()
        {
            name = "Generated Hybrid Tree Leaf Cards",
            hideFlags = HideFlags.DontSave
        };
        mesh.MarkDynamic();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTangents(tangents);
        mesh.SetUVs(0, uvs);
        mesh.SetUVs(1, uv2);
        mesh.SetTriangles(indices, 0);
        mesh.bounds = BuildCanopyBounds();

        canopyBaseVertices = vertices.ToArray();
        canopyAnimatedVertices = vertices.ToArray();
        canopyWindData = uv2.ToArray();
        canopyResetAfterWind = true;
        return mesh;
    }

    private Bounds BuildCanopyBounds()
    {
        float centerY = Mathf.Lerp(trunkHeight, treeHeight, 0.62f);
        Vector3 size = new(canopyRadius * 3.1f + windStrength * 2f, canopyHeight * 1.9f + windStrength * 2f, canopyRadius * 2.6f + windStrength * 2f);
        return new Bounds(new Vector3(0f, centerY, 0f), size);
    }

    private void AddLeafCard(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector4> tangents,
        List<Vector2> uvs,
        List<Vector2> uv2,
        List<int> indices,
        System.Random random,
        float canopyCenterY)
    {
        float theta = RandomRange(random, 0f, Mathf.PI * 2f);
        float radialT = Mathf.Pow(RandomRange(random, 0f, 1f), 0.58f);
        float x = Mathf.Cos(theta) * canopyRadius * radialT;
        float z = Mathf.Sin(theta) * canopyRadius * 0.74f * radialT;
        float ySpread = RandomRange(random, -1f, 1f) * canopyHeight * 0.5f * Mathf.Lerp(1.0f, 0.62f, radialT);
        float y = canopyCenterY + ySpread + Mathf.Sin(theta * 2.1f) * 0.12f;
        y = Mathf.Clamp(y, trunkHeight * 0.78f, treeHeight);

        Vector3 center = new(x, y, z);
        Vector3 radial = new(x, 0f, z);
        Vector3 radialDir = radial.sqrMagnitude > 0.001f ? radial.normalized : Vector3.forward;
        float yaw = theta + Mathf.PI * 0.5f + RandomRange(random, -0.75f, 0.75f);
        Vector3 right = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)).normalized;
        Vector3 up = (Vector3.up + radialDir * RandomRange(random, -0.18f, 0.32f)).normalized;
        Vector3 forward = Vector3.Cross(right, up).normalized;
        if (Vector3.Dot(forward, radialDir) < 0f)
        {
            forward = -forward;
        }

        right = Vector3.Cross(up, forward).normalized;
        int quadrant = PickLeafQuadrant(random);
        float size = RandomRange(random, leafCardMinSize, leafCardMaxSize) * Mathf.Lerp(1.08f, 0.86f, radialT);
        float aspect = GetQuadrantAspect(quadrant);
        float width = size * aspect;
        float height = size;
        Vector3 halfRight = right * (width * 0.5f);
        Vector3 halfUp = up * (height * 0.5f);
        int baseIndex = vertices.Count;

        vertices.Add(center - halfRight - halfUp);
        vertices.Add(center + halfRight - halfUp);
        vertices.Add(center + halfRight + halfUp);
        vertices.Add(center - halfRight + halfUp);

        for (int i = 0; i < 4; i++)
        {
            normals.Add(forward);
            tangents.Add(new Vector4(right.x, right.y, right.z, 1f));
        }

        GetQuadrantUvs(quadrant, out Vector2 uvMin, out Vector2 uvMax);
        uvs.Add(new Vector2(uvMin.x, uvMin.y));
        uvs.Add(new Vector2(uvMax.x, uvMin.y));
        uvs.Add(new Vector2(uvMax.x, uvMax.y));
        uvs.Add(new Vector2(uvMin.x, uvMax.y));

        float phase = RandomRange(random, 0f, Mathf.PI * 2f) + center.x * 0.37f + center.z * 0.29f;
        float heightFactor = Mathf.InverseLerp(trunkHeight * 0.45f, treeHeight, center.y);
        float amplitude = RandomRange(random, 0.65f, 1.25f) * Mathf.Lerp(0.35f, 1f, heightFactor);
        uv2.Add(new Vector2(phase + 0.11f, amplitude));
        uv2.Add(new Vector2(phase + 0.31f, amplitude));
        uv2.Add(new Vector2(phase + 0.53f, amplitude));
        uv2.Add(new Vector2(phase + 0.73f, amplitude));

        indices.Add(baseIndex);
        indices.Add(baseIndex + 2);
        indices.Add(baseIndex + 1);
        indices.Add(baseIndex);
        indices.Add(baseIndex + 3);
        indices.Add(baseIndex + 2);
    }

    private static int PickLeafQuadrant(System.Random random)
    {
        float value = RandomRange(random, 0f, 1f);
        if (value < 0.42f)
        {
            return 0;
        }

        if (value < 0.72f)
        {
            return 1;
        }

        if (value < 0.86f)
        {
            return 2;
        }

        return 3;
    }

    private static float GetQuadrantAspect(int quadrant)
    {
        switch (quadrant)
        {
            case 1:
                return 1.38f;
            case 2:
                return 0.88f;
            case 3:
                return 1.24f;
            default:
                return 1.05f;
        }
    }

    private static void GetQuadrantUvs(int quadrant, out Vector2 uvMin, out Vector2 uvMax)
    {
        const float Pad = 0.018f;
        bool right = quadrant == 1 || quadrant == 3;
        bool top = quadrant == 0 || quadrant == 1;
        float x0 = right ? 0.5f : 0f;
        float x1 = right ? 1f : 0.5f;
        float y0 = top ? 0.5f : 0f;
        float y1 = top ? 1f : 0.5f;
        uvMin = new Vector2(x0 + Pad, y0 + Pad);
        uvMax = new Vector2(x1 - Pad, y1 - Pad);
    }

    private void CreateImpostor(Transform parent)
    {
        GameObject impostor = new("DistantImpostor");
        impostor.hideFlags = HideFlags.DontSave;
        impostor.transform.SetParent(parent, false);
        impostorTransform = impostor.transform;
        MeshFilter filter = impostor.AddComponent<MeshFilter>();
        impostorRenderer = impostor.AddComponent<MeshRenderer>();
        impostorRenderer.shadowCastingMode = ShadowCastingMode.On;
        impostorRenderer.receiveShadows = false;
        impostorMesh = BuildImpostorMesh();
        filter.sharedMesh = impostorMesh;
    }

    private Mesh BuildImpostorMesh()
    {
        float width = canopyRadius * 2.65f;
        float height = treeHeight * 1.08f;
        Vector3[] vertices =
        {
            new(-width * 0.5f, 0f, 0f),
            new(width * 0.5f, 0f, 0f),
            new(width * 0.5f, height, 0f),
            new(-width * 0.5f, height, 0f)
        };
        Vector2[] uvs =
        {
            new(0f, 0f),
            new(1f, 0f),
            new(1f, 1f),
            new(0f, 1f)
        };
        Vector3[] normals =
        {
            Vector3.forward,
            Vector3.forward,
            Vector3.forward,
            Vector3.forward
        };
        Vector4[] tangents =
        {
            new(1f, 0f, 0f, 1f),
            new(1f, 0f, 0f, 1f),
            new(1f, 0f, 0f, 1f),
            new(1f, 0f, 0f, 1f)
        };
        int[] triangles = { 0, 2, 1, 0, 3, 2 };

        Mesh mesh = new()
        {
            name = "Generated Hybrid Tree Impostor",
            hideFlags = HideFlags.DontSave
        };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private void ApplyMaterials()
    {
        if (trunkRenderer != null)
        {
            barkMaterial ??= CreateBarkMaterial();
            trunkRenderer.sharedMaterial = barkMaterial;
        }

        if (canopyRenderer != null)
        {
            leafMaterial ??= CreateLeafMaterial(false);
            canopyRenderer.sharedMaterial = leafMaterial;
        }

        if (impostorRenderer != null)
        {
            impostorMaterial ??= CreateLeafMaterial(true);
            impostorRenderer.sharedMaterial = impostorMaterial;
        }
    }

    private Material CreateLeafMaterial(bool impostor)
    {
        Shader shader = Shader.Find(LeafShaderName)
            ?? Shader.Find("Ultraloud/Directional Sprites/Billboard Lit HDRP")
            ?? Shader.Find("HDRP/Unlit")
            ?? Shader.Find("Unlit/Transparent");
        Material material = new(shader)
        {
            name = impostor ? "Runtime Hybrid Tree Impostor" : "Runtime Hybrid Tree Leaves",
            hideFlags = HideFlags.DontSave
        };
        ApplyLeafMaterialSettings(material, impostor);
        return material;
    }

    private Material CreateBarkMaterial()
    {
        Shader shader = Shader.Find("HDRP/Lit")
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");
        Material material = new(shader)
        {
            name = "Runtime Hybrid Tree Bark",
            hideFlags = HideFlags.DontSave
        };
        SetColorIfPresent(material, "_BaseColor", new Color(0.65f, 0.46f, 0.28f, 1f));
        SetTextureIfPresent(material, "_BaseColorMap", barkBaseMap);
        SetTextureIfPresent(material, "_MainTex", barkBaseMap);
        SetTextureIfPresent(material, "_BaseMap", barkBaseMap);
        SetTextureIfPresent(material, "_NormalMap", barkNormalMap);
        SetTextureIfPresent(material, "_BumpMap", barkNormalMap);
        SetFloatIfPresent(material, "_NormalScale", barkNormalMap != null ? 1f : 0f);
        SetFloatIfPresent(material, "_Smoothness", 0.32f);
        SetFloatIfPresent(material, "_Metallic", 0f);
        if (barkNormalMap != null)
        {
            material.EnableKeyword("_NORMALMAP");
        }

        return material;
    }

    private void ApplyLeafMaterialSettings(Material material, bool impostor)
    {
        if (material == null)
        {
            return;
        }

        Texture2D baseMap = impostor ? impostorBaseMap : leafBaseMap;
        Texture2D normalMap = impostor ? impostorNormalMap : leafNormalMap;
        Texture2D depthMap = impostor ? impostorDepthMap : leafDepthMap;
        Texture2D thicknessMap = impostor ? impostorThicknessMap : leafThicknessMap;
        Texture2D densityMap = impostor ? null : leafDensityMap;

        SetTextureIfPresent(material, BaseMapId, baseMap);
        SetTextureIfPresent(material, NormalMapId, normalMap);
        SetTextureIfPresent(material, DepthMapId, depthMap);
        SetTextureIfPresent(material, ThicknessMapId, thicknessMap);
        SetTextureIfPresent(material, DensityMapId, densityMap);
        SetTextureIfPresent(material, WindMapId, leafWindMap);
        SetColorIfPresent(material, BaseColorId, leafTint);
        SetFloatIfPresent(material, AlphaCutoffId, alphaCutoff);
        SetFloatIfPresent(material, CoverageSoftnessId, coverageSoftness);
        SetFloatIfPresent(material, NormalScaleId, normalScale);
        SetFloatIfPresent(material, CanopyNormalBendId, impostor ? 0.25f : canopyNormalBend);
        SetFloatIfPresent(material, WrapDiffuseId, wrapDiffuse);
        SetFloatIfPresent(material, DensityShadowStrengthId, densityShadowStrength);
        SetFloatIfPresent(material, DepthSelfShadowStrengthId, depthSelfShadowStrength);
        SetFloatIfPresent(material, SurfaceRoughnessId, surfaceRoughness);
        SetFloatIfPresent(material, SpecularStrengthId, specularStrength);
        SetFloatIfPresent(material, RimStrengthId, rimStrength);
        SetFloatIfPresent(material, RimPowerId, rimPower);
        SetColorIfPresent(material, TransmissionColorId, transmissionColor);
        SetFloatIfPresent(material, TransmissionStrengthId, transmissionStrength);
        SetFloatIfPresent(material, TransmissionPowerId, transmissionPower);
        SetColorIfPresent(material, AmbientTopColorId, ResolveAmbientTop());
        SetColorIfPresent(material, AmbientBottomColorId, ResolveAmbientBottom());
        SetFloatIfPresent(material, AmbientIntensityId, ambientIntensity);
        SetFloatIfPresent(material, UseNormalMapId, normalMap != null ? 1f : 0f);
        SetFloatIfPresent(material, UseDepthMapId, depthMap != null ? 1f : 0f);
        SetFloatIfPresent(material, UseThicknessMapId, thicknessMap != null ? 1f : 0f);
        SetFloatIfPresent(material, UseDensityMapId, densityMap != null ? 1f : 0f);
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

    private static void SetColorIfPresent(Material material, string propertyName, Color value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static void SetFloatIfPresent(Material material, int propertyId, float value)
    {
        if (material != null && material.HasProperty(propertyId))
        {
            material.SetFloat(propertyId, value);
        }
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private void UpdateRuntime(float time)
    {
        ApplyMaterials();
        ApplyLeafMaterialSettings(leafMaterial, false);
        ApplyLeafMaterialSettings(impostorMaterial, true);
        AnimateCanopy(time);
        Camera activeCamera = ResolveCamera();
        UpdateLod(activeCamera);
        if (time >= nextLightRefreshTime)
        {
            nextLightRefreshTime = time + lightRefreshInterval;
            RefreshLighting(activeCamera);
        }
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

    private void AnimateCanopy(float time)
    {
        if (canopyMesh == null || canopyBaseVertices == null || canopyAnimatedVertices == null || canopyWindData == null)
        {
            return;
        }

        if (!animateWind || windStrength <= 0.0001f)
        {
            if (!canopyResetAfterWind)
            {
                canopyMesh.vertices = canopyBaseVertices;
                canopyMesh.bounds = BuildCanopyBounds();
                canopyResetAfterWind = true;
            }

            return;
        }

        for (int i = 0; i < canopyBaseVertices.Length; i++)
        {
            Vector3 baseVertex = canopyBaseVertices[i];
            Vector2 wind = canopyWindData[i];
            Vector3 radial = new(baseVertex.x, 0f, baseVertex.z);
            Vector3 radialDirection = radial.sqrMagnitude > 0.0001f ? radial.normalized : Vector3.forward;
            Vector3 sideDirection = Vector3.Cross(Vector3.up, radialDirection).normalized;
            float heightFactor = Mathf.InverseLerp(trunkHeight * 0.45f, treeHeight, baseVertex.y);
            float primary = Mathf.Sin(time * windSpeed + wind.x);
            float gust = Mathf.Sin(time * windSpeed * 0.37f + wind.x * 1.73f) * windGustVariation;
            float displacement = (primary + gust) * windStrength * wind.y * heightFactor;
            Vector3 offset = (radialDirection * 0.72f + sideDirection * 0.28f) * displacement;
            offset.y = Mathf.Sin(time * windSpeed * 1.41f + wind.x * 0.7f) * windStrength * 0.12f * wind.y * heightFactor;
            canopyAnimatedVertices[i] = baseVertex + offset;
        }

        canopyMesh.vertices = canopyAnimatedVertices;
        canopyMesh.bounds = BuildCanopyBounds();
        canopyResetAfterWind = false;
    }

    private void UpdateLod(Camera activeCamera)
    {
        bool useImpostor = false;
        if (enableImpostorLod && activeCamera != null && impostorRenderer != null)
        {
            float distance = Vector3.Distance(activeCamera.transform.position, transform.position + Vector3.up * (treeHeight * 0.5f));
            useImpostor = distance > impostorDistance + impostorFadeBand * 0.5f;
        }

        SetRendererActive(trunkRenderer, !useImpostor);
        SetRendererActive(canopyRenderer, !useImpostor);
        SetRendererActive(impostorRenderer, useImpostor);

        if (useImpostor && billboardImpostor && activeCamera != null && impostorTransform != null)
        {
            Vector3 toCamera = activeCamera.transform.position - impostorTransform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                impostorTransform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            }
        }
        else if (impostorTransform != null)
        {
            impostorTransform.localRotation = Quaternion.identity;
        }
    }

    private static void SetRendererActive(Renderer renderer, bool active)
    {
        if (renderer != null && renderer.enabled != active)
        {
            renderer.enabled = active;
        }
    }

    private void RefreshLighting(Camera activeCamera)
    {
        Vector3 anchor = activeCamera != null
            ? Vector3.Lerp(transform.position + Vector3.up * trunkHeight, activeCamera.transform.position, 0.2f)
            : transform.position + Vector3.up * trunkHeight;
        int lightCount = CollectBestLights(anchor);
        ApplyLightingToMaterial(leafMaterial, lightCount);
        ApplyLightingToMaterial(impostorMaterial, lightCount);
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

    private void DestroyRuntimeMaterials()
    {
        DestroyUnityObject(leafMaterial);
        DestroyUnityObject(barkMaterial);
        DestroyUnityObject(impostorMaterial);
        leafMaterial = null;
        barkMaterial = null;
        impostorMaterial = null;
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}

#if UNITY_EDITOR
[InitializeOnLoad]
internal static class RetroHybridTreeEditorPreviewBridge
{
    private const double UpdateInterval = 0.25;

    private static double nextUpdateTime;

    static RetroHybridTreeEditorPreviewBridge()
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
        RefreshVisibleTrees(false);
    }

    private static void FullRefresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        RefreshVisibleTrees(true);
        SceneView.RepaintAll();
    }

    private static void RefreshVisibleTrees(bool force)
    {
        RetroHybridTree[] trees = Object.FindObjectsByType<RetroHybridTree>(FindObjectsInactive.Exclude);
        foreach (RetroHybridTree tree in trees)
        {
            if (tree == null)
            {
                continue;
            }

            tree.EnsureEditorPreview(force);
        }
    }
}
#endif
