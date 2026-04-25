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
[DefaultExecutionOrder(55)]
public sealed class RetroBigRock : MonoBehaviour
{
    private const string GeneratedRootName = "__BigRockGenerated";
    private const string RockShaderName = "Ultraloud/Nature/Big Rock HDRP";
    private const int MaxShaderLights = 4;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
    private static readonly int HeightMapId = Shader.PropertyToID("_HeightMap");
    private static readonly int PackedMasksId = Shader.PropertyToID("_PackedMasks");
    private static readonly int CavityMapId = Shader.PropertyToID("_CavityMap");
    private static readonly int UvScaleId = Shader.PropertyToID("_UvScale");
    private static readonly int MacroColorAId = Shader.PropertyToID("_MacroColorA");
    private static readonly int MacroColorBId = Shader.PropertyToID("_MacroColorB");
    private static readonly int MacroColorStrengthId = Shader.PropertyToID("_MacroColorStrength");
    private static readonly int NormalScaleId = Shader.PropertyToID("_NormalScale");
    private static readonly int MacroNormalStrengthId = Shader.PropertyToID("_MacroNormalStrength");
    private static readonly int HeightContrastId = Shader.PropertyToID("_HeightContrast");
    private static readonly int CrackDarkeningId = Shader.PropertyToID("_CrackDarkening");
    private static readonly int CavityDarkeningId = Shader.PropertyToID("_CavityDarkening");
    private static readonly int EdgeWearBrightnessId = Shader.PropertyToID("_EdgeWearBrightness");
    private static readonly int AoStrengthId = Shader.PropertyToID("_AoStrength");
    private static readonly int RoughnessScaleId = Shader.PropertyToID("_RoughnessScale");
    private static readonly int SpecularStrengthId = Shader.PropertyToID("_SpecularStrength");
    private static readonly int WetnessId = Shader.PropertyToID("_Wetness");
    private static readonly int WetnessDarkeningId = Shader.PropertyToID("_WetnessDarkening");
    private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
    private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
    private static readonly int AmbientTopColorId = Shader.PropertyToID("_AmbientTopColor");
    private static readonly int AmbientBottomColorId = Shader.PropertyToID("_AmbientBottomColor");
    private static readonly int AmbientIntensityId = Shader.PropertyToID("_AmbientIntensity");
    private static readonly int UseNormalMapId = Shader.PropertyToID("_UseNormalMap");
    private static readonly int UseHeightMapId = Shader.PropertyToID("_UseHeightMap");
    private static readonly int UsePackedMasksId = Shader.PropertyToID("_UsePackedMasks");
    private static readonly int UseCavityMapId = Shader.PropertyToID("_UseCavityMap");
    private static readonly int ManualLightCountId = Shader.PropertyToID("_ManualLightCount");
    private static readonly int ManualLightPositionsId = Shader.PropertyToID("_ManualLightPositionWS");
    private static readonly int ManualLightDirectionsId = Shader.PropertyToID("_ManualLightDirectionWS");
    private static readonly int ManualLightColorsId = Shader.PropertyToID("_ManualLightColor");
    private static readonly int ManualLightData0Id = Shader.PropertyToID("_ManualLightData0");
    private static readonly int ManualLightData1Id = Shader.PropertyToID("_ManualLightData1");

    [Header("Texture Maps")]
    [SerializeField] private Texture2D baseMap;
    [SerializeField] private Texture2D normalMap;
    [SerializeField] private Texture2D heightMap;
    [SerializeField] private Texture2D aoMap;
    [SerializeField] private Texture2D roughnessMap;
    [SerializeField] private Texture2D crackMaskMap;
    [SerializeField] private Texture2D edgeWearMap;
    [SerializeField] private Texture2D cavityMap;
    [SerializeField] private Texture2D displacementMap;
    [SerializeField] private Texture2D packedMasksMap;

    [Header("Shape")]
    [SerializeField] private Vector3 size = new(4.2f, 2.35f, 3.15f);
    [SerializeField, Range(8, 96)] private int radialSegments = 36;
    [SerializeField, Range(6, 48)] private int verticalSegments = 18;
    [SerializeField, Range(0f, 1.5f)] private float surfaceDisplacement = 0.42f;
    [SerializeField, Range(0f, 1f)] private float facetStrength = 0.58f;
    [SerializeField, Range(0f, 1f)] private float baseFlattening = 0.82f;
    [SerializeField, Range(0.7f, 1.4f)] private float baseSpread = 1.12f;
    [SerializeField, Range(0.5f, 4f)] private float noiseScale = 1.45f;
    [SerializeField] private int seed = 18531;
    [SerializeField] private Vector2 materialUvScale = new(1.45f, 1.08f);
    [SerializeField] private bool addMeshCollider = true;

    [Header("Shading")]
    [SerializeField] private Color baseColorTint = Color.white;
    [SerializeField] private Color macroColorLow = new(0.54f, 0.50f, 0.44f, 1f);
    [SerializeField] private Color macroColorHigh = new(0.78f, 0.75f, 0.67f, 1f);
    [SerializeField, Range(0f, 1f)] private float macroColorStrength = 0.22f;
    [SerializeField, Range(0f, 3f)] private float normalScale = 1.15f;
    [SerializeField, Range(0f, 2f)] private float macroNormalStrength = 0.35f;
    [SerializeField, Range(0f, 2f)] private float heightContrast = 0.65f;
    [SerializeField, Range(0f, 2f)] private float crackDarkening = 0.95f;
    [SerializeField, Range(0f, 2f)] private float cavityDarkening = 0.45f;
    [SerializeField, Range(0f, 2f)] private float edgeWearBrightness = 0.45f;
    [SerializeField, Range(0f, 2f)] private float aoStrength = 0.85f;
    [SerializeField, Range(0f, 2f)] private float roughnessScale = 1f;
    [SerializeField, Range(0f, 2f)] private float specularStrength = 0.18f;
    [SerializeField, Range(0f, 1f)] private float wetness = 0f;
    [SerializeField, Range(0f, 1f)] private float wetnessDarkening = 0.35f;
    [SerializeField, Range(0f, 2f)] private float rimStrength = 0.12f;
    [SerializeField, Range(0.5f, 8f)] private float rimPower = 4f;
    [SerializeField] private Color ambientTopColor = new(0.54f, 0.58f, 0.62f, 1f);
    [SerializeField] private Color ambientBottomColor = new(0.12f, 0.11f, 0.09f, 1f);
    [SerializeField, Min(0f)] private float ambientIntensity = 1f;
    [SerializeField] private bool includeRenderSettingsAmbient = true;
    [SerializeField, Min(0f)] private float renderSettingsAmbientScale = 0.22f;

    [Header("Scene Lights")]
    [SerializeField, Range(1, MaxShaderLights)] private int maxLights = MaxShaderLights;
    [SerializeField, Min(0.02f)] private float lightRefreshInterval = 0.18f;
    [SerializeField, Min(0f)] private float directionalLightScale = 0.00001f;
    [SerializeField, Min(0f)] private float punctualLightScale = 0.01f;
    [SerializeField, Min(0f)] private float maxNormalizedLightIntensity = 1.6f;

    private readonly Vector4[] manualLightPositions = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightDirections = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightColors = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightData0 = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightData1 = new Vector4[MaxShaderLights];
    private readonly Light[] bestLights = new Light[MaxShaderLights];
    private readonly float[] bestLightScores = new float[MaxShaderLights];

    private GameObject generatedRoot;
    private Mesh rockMesh;
    private MeshRenderer rockRenderer;
    private MeshCollider rockCollider;
    private Material rockMaterial;
    private bool rebuildRequested = true;
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

    [ContextMenu("Rebuild Rock Now")]
    public void RebuildRockNow()
    {
        rebuildRequested = true;
        RebuildIfNeeded(true);
    }

    private void ClampSettings()
    {
        size.x = Mathf.Max(0.1f, size.x);
        size.y = Mathf.Max(0.1f, size.y);
        size.z = Mathf.Max(0.1f, size.z);
        radialSegments = Mathf.Clamp(radialSegments, 8, 96);
        verticalSegments = Mathf.Clamp(verticalSegments, 6, 48);
        surfaceDisplacement = Mathf.Clamp(surfaceDisplacement, 0f, 1.5f);
        facetStrength = Mathf.Clamp01(facetStrength);
        baseFlattening = Mathf.Clamp01(baseFlattening);
        baseSpread = Mathf.Clamp(baseSpread, 0.7f, 1.4f);
        noiseScale = Mathf.Clamp(noiseScale, 0.5f, 4f);
        materialUvScale.x = Mathf.Max(0.05f, materialUvScale.x);
        materialUvScale.y = Mathf.Max(0.05f, materialUvScale.y);
        macroColorStrength = Mathf.Clamp01(macroColorStrength);
        normalScale = Mathf.Clamp(normalScale, 0f, 3f);
        macroNormalStrength = Mathf.Clamp(macroNormalStrength, 0f, 2f);
        heightContrast = Mathf.Clamp(heightContrast, 0f, 2f);
        crackDarkening = Mathf.Clamp(crackDarkening, 0f, 2f);
        cavityDarkening = Mathf.Clamp(cavityDarkening, 0f, 2f);
        edgeWearBrightness = Mathf.Clamp(edgeWearBrightness, 0f, 2f);
        aoStrength = Mathf.Clamp(aoStrength, 0f, 2f);
        roughnessScale = Mathf.Clamp(roughnessScale, 0f, 2f);
        specularStrength = Mathf.Clamp(specularStrength, 0f, 2f);
        wetness = Mathf.Clamp01(wetness);
        wetnessDarkening = Mathf.Clamp01(wetnessDarkening);
        rimStrength = Mathf.Clamp(rimStrength, 0f, 2f);
        rimPower = Mathf.Clamp(rimPower, 0.5f, 8f);
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

    private int ComputeBuildHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + size.GetHashCode();
            hash = hash * 31 + radialSegments;
            hash = hash * 31 + verticalSegments;
            hash = hash * 31 + surfaceDisplacement.GetHashCode();
            hash = hash * 31 + facetStrength.GetHashCode();
            hash = hash * 31 + baseFlattening.GetHashCode();
            hash = hash * 31 + baseSpread.GetHashCode();
            hash = hash * 31 + noiseScale.GetHashCode();
            hash = hash * 31 + seed;
            hash = hash * 31 + GetAssetHash(baseMap);
            hash = hash * 31 + GetAssetHash(normalMap);
            hash = hash * 31 + GetAssetHash(heightMap);
            hash = hash * 31 + GetAssetHash(packedMasksMap);
            return hash;
        }
    }

    private static int GetAssetHash(Object asset)
    {
        return asset != null ? asset.GetHashCode() : 0;
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
        rockRenderer = generatedRoot.GetComponentInChildren<MeshRenderer>(true);
        rockCollider = generatedRoot.GetComponentInChildren<MeshCollider>(true);
        MeshFilter filter = generatedRoot.GetComponentInChildren<MeshFilter>(true);
        rockMesh = filter != null ? filter.sharedMesh : null;
        return true;
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
        generatedRoot.transform.localPosition = Vector3.zero;
        generatedRoot.transform.localRotation = Quaternion.identity;
        generatedRoot.transform.localScale = Vector3.one;

        GameObject meshObject = new("RockMesh")
        {
            hideFlags = HideFlags.DontSave
        };
        meshObject.transform.SetParent(generatedRoot.transform, false);
        MeshFilter filter = meshObject.AddComponent<MeshFilter>();
        rockRenderer = meshObject.AddComponent<MeshRenderer>();
        rockRenderer.shadowCastingMode = ShadowCastingMode.On;
        rockRenderer.receiveShadows = true;

        rockMesh = BuildRockMesh();
        filter.sharedMesh = rockMesh;

        if (addMeshCollider)
        {
            rockCollider = meshObject.AddComponent<MeshCollider>();
            rockCollider.sharedMesh = rockMesh;
        }

        ApplyMaterial();
        RefreshLighting(ResolveCamera());
    }

    private Mesh BuildRockMesh()
    {
        List<Vector3> vertices = new((radialSegments + 1) * (verticalSegments + 1));
        List<Vector2> uvs = new(vertices.Capacity);
        List<int> triangles = new(radialSegments * verticalSegments * 6);
        float rx = size.x * 0.5f;
        float ry = size.y * 0.5f;
        float rz = size.z * 0.5f;
        float bottomPlane = -ry * Mathf.Lerp(0.55f, 0.92f, baseFlattening);
        float seedOffset = seed * 0.0137f;

        for (int yIndex = 0; yIndex <= verticalSegments; yIndex++)
        {
            float v = yIndex / (float)verticalSegments;
            float theta = v * Mathf.PI;
            float unitY = Mathf.Cos(theta);
            float ring = Mathf.Sin(theta);
            for (int xIndex = 0; xIndex <= radialSegments; xIndex++)
            {
                float u = xIndex / (float)radialSegments;
                float angle = u * Mathf.PI * 2f;
                Vector3 sphere = new(Mathf.Cos(angle) * ring, unitY, Mathf.Sin(angle) * ring);
                Vector3 normal = sphere.sqrMagnitude > 0.0001f ? sphere.normalized : Vector3.up;
                float broadNoise = FractalNoise(normal * noiseScale + new Vector3(seedOffset, seedOffset * 0.37f, seedOffset * 0.71f));
                float ridgeNoise = Mathf.Abs(FractalNoise(normal * (noiseScale * 2.35f) + new Vector3(seedOffset * 1.7f, seedOffset * 0.22f, seedOffset * 1.13f)) * 2f - 1f);
                float facetNoise = Mathf.Floor(FractalNoise(normal * (noiseScale * 0.85f) + Vector3.one * seedOffset) * 6f) / 5f;
                float displacement = (broadNoise - 0.5f) * surfaceDisplacement;
                displacement += (ridgeNoise - 0.5f) * surfaceDisplacement * 0.35f;
                displacement += (facetNoise - 0.5f) * surfaceDisplacement * facetStrength * 0.65f;

                Vector3 vertex = new(sphere.x * rx, sphere.y * ry, sphere.z * rz);
                vertex += normal * displacement;

                float baseBlend = Mathf.InverseLerp(bottomPlane + ry * 0.35f, bottomPlane, vertex.y);
                if (baseBlend > 0f)
                {
                    float spread = Mathf.Lerp(1f, baseSpread, baseBlend);
                    vertex.x *= spread;
                    vertex.z *= spread;
                    vertex.y = Mathf.Lerp(vertex.y, bottomPlane + Mathf.Sin(angle * 4f + seedOffset) * 0.025f, baseBlend * baseFlattening);
                }

                vertices.Add(vertex);
                uvs.Add(new Vector2(u, v));
            }
        }

        for (int yIndex = 0; yIndex < verticalSegments; yIndex++)
        {
            int row = yIndex * (radialSegments + 1);
            int nextRow = (yIndex + 1) * (radialSegments + 1);
            for (int xIndex = 0; xIndex < radialSegments; xIndex++)
            {
                int a = row + xIndex;
                int b = row + xIndex + 1;
                int c = nextRow + xIndex;
                int d = nextRow + xIndex + 1;
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        LiftToGround(vertices);

        Mesh mesh = new()
        {
            name = "Generated Big Rock Mesh",
            hideFlags = HideFlags.DontSave
        };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        return mesh;
    }

    private static void LiftToGround(List<Vector3> vertices)
    {
        if (vertices.Count == 0)
        {
            return;
        }

        float minY = vertices[0].y;
        for (int i = 1; i < vertices.Count; i++)
        {
            minY = Mathf.Min(minY, vertices[i].y);
        }

        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 vertex = vertices[i];
            vertex.y -= minY;
            vertices[i] = vertex;
        }
    }

    private static float FractalNoise(Vector3 p)
    {
        float value = 0f;
        float amplitude = 0.5f;
        float frequency = 1f;
        for (int i = 0; i < 4; i++)
        {
            float xy = Mathf.PerlinNoise(p.x * frequency + 19.71f, p.y * frequency + 3.17f);
            float yz = Mathf.PerlinNoise(p.y * frequency + 41.13f, p.z * frequency + 7.91f);
            float zx = Mathf.PerlinNoise(p.z * frequency + 11.47f, p.x * frequency + 29.37f);
            value += (xy + yz + zx) * (amplitude / 3f);
            frequency *= 2.03f;
            amplitude *= 0.5f;
        }

        return Mathf.Clamp01(value);
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
        DestroyUnityObject(rockMesh);
        rockMesh = null;
        rockRenderer = null;
        rockCollider = null;
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

    private void ApplyMaterial()
    {
        if (rockRenderer == null)
        {
            return;
        }

        rockMaterial ??= CreateRockMaterial();
        ApplyMaterialSettings(rockMaterial);
        rockRenderer.sharedMaterial = rockMaterial;
    }

    private Material CreateRockMaterial()
    {
        Shader shader = Shader.Find(RockShaderName)
            ?? Shader.Find("HDRP/Lit")
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");
        return new Material(shader)
        {
            name = "Runtime Big Rock",
            hideFlags = HideFlags.DontSave
        };
    }

    private void ApplyMaterialSettings(Material material)
    {
        if (material == null)
        {
            return;
        }

        SetTextureIfPresent(material, BaseMapId, baseMap);
        SetTextureIfPresent(material, NormalMapId, normalMap);
        SetTextureIfPresent(material, HeightMapId, heightMap);
        SetTextureIfPresent(material, PackedMasksId, packedMasksMap);
        SetTextureIfPresent(material, CavityMapId, cavityMap);
        SetColorIfPresent(material, BaseColorId, baseColorTint);
        SetVectorIfPresent(material, UvScaleId, new Vector4(materialUvScale.x, materialUvScale.y, 0f, 0f));
        SetColorIfPresent(material, MacroColorAId, macroColorLow);
        SetColorIfPresent(material, MacroColorBId, macroColorHigh);
        SetFloatIfPresent(material, MacroColorStrengthId, macroColorStrength);
        SetFloatIfPresent(material, NormalScaleId, normalScale);
        SetFloatIfPresent(material, MacroNormalStrengthId, macroNormalStrength);
        SetFloatIfPresent(material, HeightContrastId, heightContrast);
        SetFloatIfPresent(material, CrackDarkeningId, crackDarkening);
        SetFloatIfPresent(material, CavityDarkeningId, cavityDarkening);
        SetFloatIfPresent(material, EdgeWearBrightnessId, edgeWearBrightness);
        SetFloatIfPresent(material, AoStrengthId, aoStrength);
        SetFloatIfPresent(material, RoughnessScaleId, roughnessScale);
        SetFloatIfPresent(material, SpecularStrengthId, specularStrength);
        SetFloatIfPresent(material, WetnessId, wetness);
        SetFloatIfPresent(material, WetnessDarkeningId, wetnessDarkening);
        SetFloatIfPresent(material, RimStrengthId, rimStrength);
        SetFloatIfPresent(material, RimPowerId, rimPower);
        SetColorIfPresent(material, AmbientTopColorId, ResolveAmbientTop());
        SetColorIfPresent(material, AmbientBottomColorId, ResolveAmbientBottom());
        SetFloatIfPresent(material, AmbientIntensityId, ambientIntensity);
        SetFloatIfPresent(material, UseNormalMapId, normalMap != null ? 1f : 0f);
        SetFloatIfPresent(material, UseHeightMapId, heightMap != null ? 1f : 0f);
        SetFloatIfPresent(material, UsePackedMasksId, packedMasksMap != null ? 1f : 0f);
        SetFloatIfPresent(material, UseCavityMapId, cavityMap != null ? 1f : 0f);

        SetTextureIfPresent(material, "_BaseColorMap", baseMap);
        SetTextureIfPresent(material, "_MainTex", baseMap);
        SetTextureIfPresent(material, "_BaseMap", baseMap);
        SetTextureIfPresent(material, "_BumpMap", normalMap);
        SetTextureIfPresent(material, "_NormalMap", normalMap);
        SetFloatIfPresent(material, "_Smoothness", 1f - Mathf.Clamp01(0.82f * roughnessScale));
        SetFloatIfPresent(material, "_Metallic", 0f);
        if (normalMap != null)
        {
            material.EnableKeyword("_NORMALMAP");
        }
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

    private static void SetVectorIfPresent(Material material, int propertyId, Vector4 value)
    {
        if (material != null && material.HasProperty(propertyId))
        {
            material.SetVector(propertyId, value);
        }
    }

    private void UpdateRuntime(float time)
    {
        ApplyMaterial();
        if (time >= nextLightRefreshTime)
        {
            nextLightRefreshTime = time + lightRefreshInterval;
            RefreshLighting(ResolveCamera());
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

    private void RefreshLighting(Camera activeCamera)
    {
        Vector3 anchor = transform.position + Vector3.up * (size.y * 0.45f);
        if (activeCamera != null)
        {
            anchor = Vector3.Lerp(anchor, activeCamera.transform.position, 0.16f);
        }

        int lightCount = CollectBestLights(anchor);
        ApplyLightingToMaterial(rockMaterial, lightCount);
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

    private void DestroyRuntimeMaterial()
    {
        DestroyUnityObject(rockMaterial);
        rockMaterial = null;
    }
}

#if UNITY_EDITOR
[InitializeOnLoad]
internal static class RetroBigRockEditorPreviewBridge
{
    private const double UpdateInterval = 0.25;

    private static double nextUpdateTime;

    static RetroBigRockEditorPreviewBridge()
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
        RefreshVisibleRocks(false);
    }

    private static void FullRefresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        RefreshVisibleRocks(true);
        SceneView.RepaintAll();
    }

    private static void RefreshVisibleRocks(bool force)
    {
        RetroBigRock[] rocks = Object.FindObjectsByType<RetroBigRock>(FindObjectsInactive.Exclude);
        foreach (RetroBigRock rock in rocks)
        {
            if (rock == null)
            {
                continue;
            }

            rock.EnsureEditorPreview(force);
        }
    }
}
#endif
