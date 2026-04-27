using System;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(58)]
public sealed class RetroHybridBuilding : MonoBehaviour
{
    [Serializable]
    public sealed class FaceMaps
    {
        public Texture2D baseMap;
        public Texture2D normalMap;
        public Texture2D heightMap;
        public Texture2D packedMasksMap;
        public Texture2D emissionMap;
    }

    [Serializable]
    public sealed class InteriorPropDefinition
    {
        public string label;
        public FaceMaps maps = new();
        public Vector2 baseSize = new(1f, 1f);
        public Vector2 scaleRange = new(0.9f, 1.2f);
        public bool floorDecal;
        public bool preferWallPlacement;
        public bool castShadow = true;
    }

    private const string GeneratedRootName = "__HybridBuildingGenerated";
    private const string BuildingShaderName = "Ultraloud/Buildings/Hybrid Building Face HDRP";
    private const int MaxShaderLights = 4;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
    private static readonly int HeightMapId = Shader.PropertyToID("_HeightMap");
    private static readonly int PackedMasksId = Shader.PropertyToID("_PackedMasks");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int AlphaCutoffId = Shader.PropertyToID("_AlphaCutoff");
    private static readonly int NormalScaleId = Shader.PropertyToID("_NormalScale");
    private static readonly int HeightContrastId = Shader.PropertyToID("_HeightContrast");
    private static readonly int AoStrengthId = Shader.PropertyToID("_AoStrength");
    private static readonly int RoughnessScaleId = Shader.PropertyToID("_RoughnessScale");
    private static readonly int SpecularStrengthId = Shader.PropertyToID("_SpecularStrength");
    private static readonly int EdgeWearBrightnessId = Shader.PropertyToID("_EdgeWearBrightness");
    private static readonly int CrackDarkeningId = Shader.PropertyToID("_CrackDarkening");
    private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
    private static readonly int AmbientTopColorId = Shader.PropertyToID("_AmbientTopColor");
    private static readonly int AmbientBottomColorId = Shader.PropertyToID("_AmbientBottomColor");
    private static readonly int AmbientIntensityId = Shader.PropertyToID("_AmbientIntensity");
    private static readonly int UseNormalMapId = Shader.PropertyToID("_UseNormalMap");
    private static readonly int UseHeightMapId = Shader.PropertyToID("_UseHeightMap");
    private static readonly int UsePackedMasksId = Shader.PropertyToID("_UsePackedMasks");
    private static readonly int UseEmissionMapId = Shader.PropertyToID("_UseEmissionMap");
    private static readonly int ManualLightCountId = Shader.PropertyToID("_ManualLightCount");
    private static readonly int ManualLightPositionsId = Shader.PropertyToID("_ManualLightPositionWS");
    private static readonly int ManualLightDirectionsId = Shader.PropertyToID("_ManualLightDirectionWS");
    private static readonly int ManualLightColorsId = Shader.PropertyToID("_ManualLightColor");
    private static readonly int ManualLightData0Id = Shader.PropertyToID("_ManualLightData0");
    private static readonly int ManualLightData1Id = Shader.PropertyToID("_ManualLightData1");

    [Header("Face Sprites")]
    [SerializeField] private FaceMaps frontMaps = new();
    [SerializeField] private FaceMaps sideMaps = new();
    [SerializeField] private FaceMaps backMaps = new();
    [SerializeField] private FaceMaps roofMaps = new();
    [SerializeField] private FaceMaps doorMaps = new();
    [SerializeField] private FaceMaps interiorWallMaps = new();
    [SerializeField] private FaceMaps interiorFloorMaps = new();
    [SerializeField] private FaceMaps interiorCeilingMaps = new();
    [SerializeField] private FaceMaps interiorDoorMaps = new();

    [Header("Shape")]
    [SerializeField, Min(0.5f)] private float width = 6f;
    [SerializeField, Min(0.5f)] private float depth = 4.25f;
    [SerializeField, Min(0.5f)] private float wallHeight = 3.6f;
    [SerializeField, Min(0f)] private float roofHeight = 1.05f;
    [SerializeField, Min(0f)] private float roofOverhang = 0.22f;
    [SerializeField, Min(0.001f)] private float faceOffset = 0.018f;

    [Header("Door")]
    [SerializeField] private bool drawDoorSprite = true;
    [SerializeField] private float doorLocalX = 0.92f;
    [SerializeField, Min(0.1f)] private float doorWidth = 1.45f;
    [SerializeField, Min(0.1f)] private float doorHeight = 2.25f;
    [SerializeField, Min(0f)] private float doorBottom = 0.03f;
    [SerializeField, Min(0.01f)] private float doorColliderDepth = 0.34f;
    [SerializeField, Min(0.01f)] private float doorForwardOffset = 0.18f;
    [SerializeField] private Vector3 insideSpawnLocalPosition = new(0f, 0.08f, 0.82f);
    [SerializeField] private float insideSpawnYaw = 180f;
    [SerializeField] private Vector3 outsideSpawnLocalPosition = new(0.92f, 0.08f, -3.55f);
    [SerializeField] private float outsideSpawnYaw = 180f;

    [Header("Interior")]
    [SerializeField] private bool buildInterior = true;
    [SerializeField, Min(0.05f)] private float interiorInset = 0.08f;
    [SerializeField, Min(1f)] private float interiorHeight = 3.28f;
    [SerializeField] private bool drawInteriorDoorSprite = true;
    [SerializeField] private int interiorSeed = 48191;
    [SerializeField, Range(0, 32)] private int furniturePropCount = 12;
    [SerializeField, Range(0, 48)] private int dirtDecalCount = 20;
    [SerializeField] private InteriorPropDefinition[] furniturePropLibrary = Array.Empty<InteriorPropDefinition>();
    [SerializeField] private InteriorPropDefinition[] dirtDecalLibrary = Array.Empty<InteriorPropDefinition>();

    [Header("Collision")]
    [SerializeField] private bool addCollisionWalls = true;
    [SerializeField, Min(0.02f)] private float wallColliderThickness = 0.22f;
    [SerializeField] private bool blockNavigation = true;
    [SerializeField, Min(0f)] private float navObstaclePadding = 0.28f;

    [Header("Shading")]
    [SerializeField] private Color baseColorTint = Color.white;
    [SerializeField, Range(0f, 1f)] private float alphaCutoff = 0.08f;
    [SerializeField, Range(0f, 3f)] private float normalScale = 1.15f;
    [SerializeField, Range(0f, 2f)] private float heightContrast = 0.55f;
    [SerializeField, Range(0f, 2f)] private float aoStrength = 0.88f;
    [SerializeField, Range(0f, 2f)] private float roughnessScale = 1f;
    [SerializeField, Range(0f, 2f)] private float specularStrength = 0.13f;
    [SerializeField, Range(0f, 2f)] private float edgeWearBrightness = 0.34f;
    [SerializeField, Range(0f, 2f)] private float crackDarkening = 0.82f;
    [SerializeField, Range(0f, 6f)] private float emissionStrength = 1.65f;
    [SerializeField] private Color ambientTopColor = new(0.58f, 0.62f, 0.64f, 1f);
    [SerializeField] private Color ambientBottomColor = new(0.13f, 0.12f, 0.10f, 1f);
    [SerializeField, Min(0f)] private float ambientIntensity = 1f;
    [SerializeField] private bool includeRenderSettingsAmbient = true;
    [SerializeField, Min(0f)] private float renderSettingsAmbientScale = 0.24f;

    [Header("Scene Lights")]
    [SerializeField, Range(1, MaxShaderLights)] private int maxLights = MaxShaderLights;
    [SerializeField, Min(0.02f)] private float lightRefreshInterval = 0.18f;
    [SerializeField, Min(0f)] private float directionalLightScale = 0.00001f;
    [SerializeField, Min(0f)] private float punctualLightScale = 0.01f;
    [SerializeField, Min(0f)] private float maxNormalizedLightIntensity = 1.55f;

    private readonly Vector4[] manualLightPositions = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightDirections = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightColors = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightData0 = new Vector4[MaxShaderLights];
    private readonly Vector4[] manualLightData1 = new Vector4[MaxShaderLights];
    private readonly Light[] bestLights = new Light[MaxShaderLights];
    private readonly float[] bestLightScores = new float[MaxShaderLights];
    private readonly List<Mesh> generatedMeshes = new();

    private GameObject generatedRoot;
    private Material frontMaterial;
    private Material sideMaterial;
    private Material backMaterial;
    private Material roofMaterial;
    private Material doorMaterial;
    private Material interiorWallMaterial;
    private Material interiorFloorMaterial;
    private Material interiorCeilingMaterial;
    private Material interiorDoorMaterial;
    private readonly List<Material> propMaterials = new();
    private double nextLightRefreshTime = double.NegativeInfinity;
    private bool rebuildRequested = true;
    private int lastBuildHash;
#if UNITY_EDITOR
    private bool deferredRebuildQueued;
#endif

    public Vector3 DoorColliderLocalCenter => ExteriorDoorColliderLocalCenter;
    public Vector3 ExteriorDoorColliderLocalCenter => new(doorLocalX, doorBottom + doorHeight * 0.5f, -depth * 0.5f - doorForwardOffset);
    public Vector3 InteriorDoorColliderLocalCenter => new(doorLocalX, doorBottom + doorHeight * 0.5f, -depth * 0.5f + doorForwardOffset);
    public Vector3 DoorColliderSize => new(doorWidth, doorHeight, doorColliderDepth);
    public Vector3 InsideSpawnLocalPosition => insideSpawnLocalPosition;
    public Quaternion InsideSpawnLocalRotation => Quaternion.Euler(0f, insideSpawnYaw, 0f);
    public Vector3 OutsideSpawnLocalPosition => outsideSpawnLocalPosition;
    public Quaternion OutsideSpawnLocalRotation => Quaternion.Euler(0f, outsideSpawnYaw, 0f);
    public bool BuildsInterior => buildInterior;

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

    [ContextMenu("Rebuild Building Now")]
    public void RebuildBuildingNow()
    {
        rebuildRequested = true;
        RebuildIfNeeded(true);
    }

    private void ClampSettings()
    {
        width = Mathf.Max(0.5f, width);
        depth = Mathf.Max(0.5f, depth);
        wallHeight = Mathf.Max(0.5f, wallHeight);
        roofHeight = Mathf.Max(0f, roofHeight);
        roofOverhang = Mathf.Max(0f, roofOverhang);
        faceOffset = Mathf.Max(0.001f, faceOffset);
        interiorInset = Mathf.Clamp(interiorInset, 0.02f, Mathf.Min(width, depth) * 0.22f);
        interiorHeight = Mathf.Clamp(interiorHeight, 1f, Mathf.Max(1f, wallHeight - 0.05f));
        doorWidth = Mathf.Clamp(doorWidth, 0.1f, Mathf.Max(0.1f, width - 0.4f));
        doorHeight = Mathf.Clamp(doorHeight, 0.1f, Mathf.Max(0.1f, wallHeight - doorBottom));
        doorBottom = Mathf.Clamp(doorBottom, 0f, Mathf.Max(0f, wallHeight - doorHeight));
        float horizontalLimit = Mathf.Max(0f, (width - doorWidth) * 0.5f);
        doorLocalX = Mathf.Clamp(doorLocalX, -horizontalLimit, horizontalLimit);
        doorColliderDepth = Mathf.Max(0.01f, doorColliderDepth);
        doorForwardOffset = Mathf.Max(0.01f, doorForwardOffset);
        wallColliderThickness = Mathf.Max(0.02f, wallColliderThickness);
        navObstaclePadding = Mathf.Max(0f, navObstaclePadding);
        alphaCutoff = Mathf.Clamp01(alphaCutoff);
        normalScale = Mathf.Clamp(normalScale, 0f, 3f);
        heightContrast = Mathf.Clamp(heightContrast, 0f, 2f);
        aoStrength = Mathf.Clamp(aoStrength, 0f, 2f);
        roughnessScale = Mathf.Clamp(roughnessScale, 0f, 2f);
        specularStrength = Mathf.Clamp(specularStrength, 0f, 2f);
        edgeWearBrightness = Mathf.Clamp(edgeWearBrightness, 0f, 2f);
        crackDarkening = Mathf.Clamp(crackDarkening, 0f, 2f);
        emissionStrength = Mathf.Clamp(emissionStrength, 0f, 6f);
        ambientIntensity = Mathf.Max(0f, ambientIntensity);
        renderSettingsAmbientScale = Mathf.Max(0f, renderSettingsAmbientScale);
        maxLights = Mathf.Clamp(maxLights, 1, MaxShaderLights);
        lightRefreshInterval = Mathf.Max(0.02f, lightRefreshInterval);
        directionalLightScale = Mathf.Max(0f, directionalLightScale);
        punctualLightScale = Mathf.Max(0f, punctualLightScale);
        maxNormalizedLightIntensity = Mathf.Max(0f, maxNormalizedLightIntensity);
        SanitizePropLibrary(furniturePropLibrary);
        SanitizePropLibrary(dirtDecalLibrary);
    }

    private static void SanitizePropLibrary(InteriorPropDefinition[] library)
    {
        if (library == null)
        {
            return;
        }

        for (int i = 0; i < library.Length; i++)
        {
            InteriorPropDefinition prop = library[i];
            if (prop == null)
            {
                continue;
            }

            prop.baseSize.x = Mathf.Max(0.05f, prop.baseSize.x);
            prop.baseSize.y = Mathf.Max(0.05f, prop.baseSize.y);
            prop.scaleRange.x = Mathf.Max(0.05f, prop.scaleRange.x);
            prop.scaleRange.y = Mathf.Max(prop.scaleRange.x, prop.scaleRange.y);
        }
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
            hash = hash * 31 + width.GetHashCode();
            hash = hash * 31 + depth.GetHashCode();
            hash = hash * 31 + wallHeight.GetHashCode();
            hash = hash * 31 + roofHeight.GetHashCode();
            hash = hash * 31 + roofOverhang.GetHashCode();
            hash = hash * 31 + doorLocalX.GetHashCode();
            hash = hash * 31 + doorWidth.GetHashCode();
            hash = hash * 31 + doorHeight.GetHashCode();
            hash = hash * 31 + doorBottom.GetHashCode();
            hash = hash * 31 + addCollisionWalls.GetHashCode();
            hash = hash * 31 + buildInterior.GetHashCode();
            hash = hash * 31 + interiorInset.GetHashCode();
            hash = hash * 31 + interiorHeight.GetHashCode();
            hash = hash * 31 + drawInteriorDoorSprite.GetHashCode();
            hash = hash * 31 + interiorSeed;
            hash = hash * 31 + furniturePropCount;
            hash = hash * 31 + dirtDecalCount;
            hash = hash * 31 + GetFaceHash(frontMaps);
            hash = hash * 31 + GetFaceHash(sideMaps);
            hash = hash * 31 + GetFaceHash(backMaps);
            hash = hash * 31 + GetFaceHash(roofMaps);
            hash = hash * 31 + GetFaceHash(doorMaps);
            hash = hash * 31 + GetFaceHash(interiorWallMaps);
            hash = hash * 31 + GetFaceHash(interiorFloorMaps);
            hash = hash * 31 + GetFaceHash(interiorCeilingMaps);
            hash = hash * 31 + GetFaceHash(interiorDoorMaps);
            hash = hash * 31 + GetPropLibraryHash(furniturePropLibrary);
            hash = hash * 31 + GetPropLibraryHash(dirtDecalLibrary);
            return hash;
        }
    }

    private static int GetPropLibraryHash(InteriorPropDefinition[] library)
    {
        unchecked
        {
            int hash = 17;
            if (library == null)
            {
                return hash;
            }

            hash = hash * 31 + library.Length;
            for (int i = 0; i < library.Length; i++)
            {
                InteriorPropDefinition prop = library[i];
                if (prop == null)
                {
                    continue;
                }

                hash = hash * 31 + GetFaceHash(prop.maps);
                hash = hash * 31 + prop.baseSize.GetHashCode();
                hash = hash * 31 + prop.scaleRange.GetHashCode();
                hash = hash * 31 + prop.floorDecal.GetHashCode();
                hash = hash * 31 + prop.preferWallPlacement.GetHashCode();
            }

            return hash;
        }
    }

    private static int GetFaceHash(FaceMaps maps)
    {
        unchecked
        {
            int hash = 17;
            if (maps == null)
            {
                return hash;
            }

            hash = hash * 31 + GetAssetHash(maps.baseMap);
            hash = hash * 31 + GetAssetHash(maps.normalMap);
            hash = hash * 31 + GetAssetHash(maps.heightMap);
            hash = hash * 31 + GetAssetHash(maps.packedMasksMap);
            hash = hash * 31 + GetAssetHash(maps.emissionMap);
            return hash;
        }
    }

    private static int GetAssetHash(Object asset)
    {
        return asset != null ? asset.GetHashCode() : 0;
    }

    public bool ContainsInteriorWorldPosition(Vector3 worldPosition, float padding = 0.02f)
    {
        if (!buildInterior)
        {
            return false;
        }

        float safePadding = Mathf.Max(0f, padding);
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        float halfWidth = Mathf.Max(0.05f, width * 0.5f - interiorInset) + safePadding;
        float halfDepth = Mathf.Max(0.05f, depth * 0.5f - interiorInset) + safePadding;
        return local.x >= -halfWidth
            && local.x <= halfWidth
            && local.z >= -halfDepth
            && local.z <= halfDepth
            && local.y >= -safePadding
            && local.y <= interiorHeight + safePadding;
    }

    public Vector3 ClampInteriorWorldPosition(Vector3 worldPosition, float margin = 0.45f)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        return transform.TransformPoint(ClampInteriorLocalPosition(local, margin));
    }

    public Vector3 GetRandomInteriorWorldPosition(float margin = 0.45f)
    {
        float safeMargin = Mathf.Max(0f, margin);
        float halfWidth = Mathf.Max(0.08f, width * 0.5f - interiorInset - safeMargin);
        float halfDepth = Mathf.Max(0.08f, depth * 0.5f - interiorInset - safeMargin);
        Vector3 local = new(
            UnityEngine.Random.Range(-halfWidth, halfWidth),
            Mathf.Max(0.06f, insideSpawnLocalPosition.y),
            UnityEngine.Random.Range(-halfDepth, halfDepth));
        return transform.TransformPoint(local);
    }

    public static RetroHybridBuilding FindInteriorContaining(Vector3 worldPosition, float padding = 0.02f)
    {
        RetroHybridBuilding[] buildings = Object.FindObjectsByType<RetroHybridBuilding>(FindObjectsInactive.Exclude);
        for (int i = 0; i < buildings.Length; i++)
        {
            RetroHybridBuilding building = buildings[i];
            if (building != null && building.ContainsInteriorWorldPosition(worldPosition, padding))
            {
                return building;
            }
        }

        return null;
    }

    private Vector3 ClampInteriorLocalPosition(Vector3 localPosition, float margin)
    {
        float safeMargin = Mathf.Max(0f, margin);
        float halfWidth = Mathf.Max(0.08f, width * 0.5f - interiorInset - safeMargin);
        float halfDepth = Mathf.Max(0.08f, depth * 0.5f - interiorInset - safeMargin);
        localPosition.x = Mathf.Clamp(localPosition.x, -halfWidth, halfWidth);
        localPosition.y = Mathf.Clamp(localPosition.y, Mathf.Max(0.04f, insideSpawnLocalPosition.y), Mathf.Max(0.05f, interiorHeight - 0.15f));
        localPosition.z = Mathf.Clamp(localPosition.z, -halfDepth, halfDepth);
        return localPosition;
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

    private void RebuildInternal()
    {
        DestroyRuntimeMaterials();
        DestroyGeneratedRoot();

        generatedRoot = new GameObject(GeneratedRootName)
        {
            hideFlags = HideFlags.DontSave
        };
        generatedRoot.transform.SetParent(transform, false);
        generatedRoot.transform.localPosition = Vector3.zero;
        generatedRoot.transform.localRotation = Quaternion.identity;
        generatedRoot.transform.localScale = Vector3.one;

        BuildFaces(generatedRoot.transform);
        if (buildInterior)
        {
            BuildInterior(generatedRoot.transform);
        }

        if (drawDoorSprite)
        {
            BuildDoorSprite(generatedRoot.transform);
        }

        if (addCollisionWalls)
        {
            BuildCollisionWalls(generatedRoot.transform);
        }

        ConfigureNavigationObstacle();
        ApplyMaterials();
        RefreshLighting(ResolveCamera());
    }

    private void BuildFaces(Transform parent)
    {
        float halfWidth = width * 0.5f;
        float halfDepth = depth * 0.5f;
        float frontZ = -halfDepth - faceOffset;
        float backZ = halfDepth + faceOffset;
        float leftX = -halfWidth - faceOffset;
        float rightX = halfWidth + faceOffset;

        CreateSurface(
            "FrontFace",
            parent,
            new[]
            {
                new Vector3(-halfWidth, 0f, frontZ),
                new Vector3(halfWidth, 0f, frontZ),
                new Vector3(halfWidth, wallHeight, frontZ),
                new Vector3(-halfWidth, wallHeight, frontZ)
            },
            Vector3.back,
            Vector3.right,
            frontMaps,
            ref frontMaterial);

        CreateSurface(
            "BackFace",
            parent,
            new[]
            {
                new Vector3(halfWidth, 0f, backZ),
                new Vector3(-halfWidth, 0f, backZ),
                new Vector3(-halfWidth, wallHeight, backZ),
                new Vector3(halfWidth, wallHeight, backZ)
            },
            Vector3.forward,
            Vector3.left,
            backMaps,
            ref backMaterial);

        CreateSurface(
            "LeftFace",
            parent,
            new[]
            {
                new Vector3(leftX, 0f, halfDepth),
                new Vector3(leftX, 0f, -halfDepth),
                new Vector3(leftX, wallHeight, -halfDepth),
                new Vector3(leftX, wallHeight, halfDepth)
            },
            Vector3.left,
            Vector3.back,
            sideMaps,
            ref sideMaterial);

        CreateSurface(
            "RightFace",
            parent,
            new[]
            {
                new Vector3(rightX, 0f, -halfDepth),
                new Vector3(rightX, 0f, halfDepth),
                new Vector3(rightX, wallHeight, halfDepth),
                new Vector3(rightX, wallHeight, -halfDepth)
            },
            Vector3.right,
            Vector3.forward,
            sideMaps,
            ref sideMaterial);

        if (roofHeight <= 0.001f)
        {
            return;
        }

        float roofFrontZ = -halfDepth - roofOverhang;
        float roofBackZ = halfDepth + roofOverhang;
        float roofLeftX = -halfWidth - roofOverhang;
        float roofRightX = halfWidth + roofOverhang;
        float ridgeY = wallHeight + roofHeight;

        CreateSurface(
            "LeftRoofSlope",
            parent,
            new[]
            {
                new Vector3(roofLeftX, wallHeight, roofFrontZ),
                new Vector3(roofLeftX, wallHeight, roofBackZ),
                new Vector3(0f, ridgeY, roofBackZ),
                new Vector3(0f, ridgeY, roofFrontZ)
            },
            new Vector3(-roofHeight, halfWidth + roofOverhang, 0f).normalized,
            Vector3.forward,
            roofMaps,
            ref roofMaterial);

        CreateSurface(
            "RightRoofSlope",
            parent,
            new[]
            {
                new Vector3(0f, ridgeY, roofFrontZ),
                new Vector3(0f, ridgeY, roofBackZ),
                new Vector3(roofRightX, wallHeight, roofBackZ),
                new Vector3(roofRightX, wallHeight, roofFrontZ)
            },
            new Vector3(roofHeight, halfWidth + roofOverhang, 0f).normalized,
            Vector3.forward,
            roofMaps,
            ref roofMaterial);

        CreateTriangle(
            "FrontGable",
            parent,
            new[]
            {
                new Vector3(-halfWidth, wallHeight, frontZ),
                new Vector3(halfWidth, wallHeight, frontZ),
                new Vector3(0f, ridgeY, frontZ)
            },
            Vector3.back,
            Vector3.right,
            frontMaps,
            ref frontMaterial);

        CreateTriangle(
            "BackGable",
            parent,
            new[]
            {
                new Vector3(halfWidth, wallHeight, backZ),
                new Vector3(-halfWidth, wallHeight, backZ),
                new Vector3(0f, ridgeY, backZ)
            },
            Vector3.forward,
            Vector3.left,
            backMaps,
            ref backMaterial);
    }

    private void BuildDoorSprite(Transform parent)
    {
        Vector3 center = new(doorLocalX, doorBottom + doorHeight * 0.5f, -depth * 0.5f - faceOffset * 2.5f);
        float halfDoorWidth = doorWidth * 0.5f;
        float halfDoorHeight = doorHeight * 0.5f;
        CreateSurface(
            "DoorSprite",
            parent,
            new[]
            {
                center + new Vector3(-halfDoorWidth, -halfDoorHeight, 0f),
                center + new Vector3(halfDoorWidth, -halfDoorHeight, 0f),
                center + new Vector3(halfDoorWidth, halfDoorHeight, 0f),
                center + new Vector3(-halfDoorWidth, halfDoorHeight, 0f)
            },
            Vector3.back,
            Vector3.right,
            doorMaps,
            ref doorMaterial);
    }

    private void BuildInterior(Transform parent)
    {
        float halfWidth = width * 0.5f - interiorInset;
        float halfDepth = depth * 0.5f - interiorInset;
        float frontZ = -halfDepth;
        float backZ = halfDepth;
        float leftX = -halfWidth;
        float rightX = halfWidth;
        float floorY = 0.025f;
        float ceilingY = interiorHeight;

        CreateSurface(
            "InteriorFrontWall",
            parent,
            new[]
            {
                new Vector3(halfWidth, 0f, frontZ),
                new Vector3(-halfWidth, 0f, frontZ),
                new Vector3(-halfWidth, interiorHeight, frontZ),
                new Vector3(halfWidth, interiorHeight, frontZ)
            },
            Vector3.forward,
            Vector3.left,
            interiorWallMaps,
            ref interiorWallMaterial);

        CreateSurface(
            "InteriorBackWall",
            parent,
            new[]
            {
                new Vector3(-halfWidth, 0f, backZ),
                new Vector3(halfWidth, 0f, backZ),
                new Vector3(halfWidth, interiorHeight, backZ),
                new Vector3(-halfWidth, interiorHeight, backZ)
            },
            Vector3.back,
            Vector3.right,
            interiorWallMaps,
            ref interiorWallMaterial);

        CreateSurface(
            "InteriorLeftWall",
            parent,
            new[]
            {
                new Vector3(leftX, 0f, -halfDepth),
                new Vector3(leftX, 0f, halfDepth),
                new Vector3(leftX, interiorHeight, halfDepth),
                new Vector3(leftX, interiorHeight, -halfDepth)
            },
            Vector3.right,
            Vector3.forward,
            interiorWallMaps,
            ref interiorWallMaterial);

        CreateSurface(
            "InteriorRightWall",
            parent,
            new[]
            {
                new Vector3(rightX, 0f, halfDepth),
                new Vector3(rightX, 0f, -halfDepth),
                new Vector3(rightX, interiorHeight, -halfDepth),
                new Vector3(rightX, interiorHeight, halfDepth)
            },
            Vector3.left,
            Vector3.back,
            interiorWallMaps,
            ref interiorWallMaterial);

        CreateSurface(
            "InteriorFloor",
            parent,
            new[]
            {
                new Vector3(-halfWidth, floorY, -halfDepth),
                new Vector3(halfWidth, floorY, -halfDepth),
                new Vector3(halfWidth, floorY, halfDepth),
                new Vector3(-halfWidth, floorY, halfDepth)
            },
            Vector3.up,
            Vector3.right,
            interiorFloorMaps,
            ref interiorFloorMaterial);

        CreateSurface(
            "InteriorCeiling",
            parent,
            new[]
            {
                new Vector3(-halfWidth, ceilingY, halfDepth),
                new Vector3(halfWidth, ceilingY, halfDepth),
                new Vector3(halfWidth, ceilingY, -halfDepth),
                new Vector3(-halfWidth, ceilingY, -halfDepth)
            },
            Vector3.down,
            Vector3.right,
            interiorCeilingMaps,
            ref interiorCeilingMaterial);

        if (drawInteriorDoorSprite)
        {
            BuildInteriorDoorSprite(parent);
        }

        BuildInteriorProps(parent, halfWidth, halfDepth);
    }

    private void BuildInteriorDoorSprite(Transform parent)
    {
        Vector3 center = new(doorLocalX, doorBottom + doorHeight * 0.5f, -depth * 0.5f + interiorInset + faceOffset * 2.5f);
        float halfDoorWidth = doorWidth * 0.5f;
        float halfDoorHeight = doorHeight * 0.5f;
        CreateSurface(
            "InteriorExitDoorSprite",
            parent,
            new[]
            {
                center + new Vector3(halfDoorWidth, -halfDoorHeight, 0f),
                center + new Vector3(-halfDoorWidth, -halfDoorHeight, 0f),
                center + new Vector3(-halfDoorWidth, halfDoorHeight, 0f),
                center + new Vector3(halfDoorWidth, halfDoorHeight, 0f)
            },
            Vector3.forward,
            Vector3.left,
            interiorDoorMaps,
            ref interiorDoorMaterial);
    }

    private void BuildInteriorProps(Transform parent, float halfWidth, float halfDepth)
    {
        GameObject propRoot = new("InteriorProceduralProps")
        {
            hideFlags = HideFlags.DontSave
        };
        propRoot.transform.SetParent(parent, false);

        System.Random random = new(interiorSeed);
        for (int i = 0; i < furniturePropCount; i++)
        {
            InteriorPropDefinition prop = PickProp(furniturePropLibrary, random);
            if (prop == null)
            {
                break;
            }

            AddFurnitureProp(propRoot.transform, prop, random, i, halfWidth, halfDepth);
        }

        for (int i = 0; i < dirtDecalCount; i++)
        {
            InteriorPropDefinition prop = PickProp(dirtDecalLibrary, random);
            if (prop == null)
            {
                break;
            }

            AddFloorDecal(propRoot.transform, prop, random, i, halfWidth, halfDepth);
        }
    }

    private static InteriorPropDefinition PickProp(InteriorPropDefinition[] library, System.Random random)
    {
        if (library == null || library.Length == 0)
        {
            return null;
        }

        int safeIndex = Mathf.Clamp((int)(RandomRange(random, 0f, 0.9999f) * library.Length), 0, library.Length - 1);
        return library[safeIndex];
    }

    private void AddFurnitureProp(Transform parent, InteriorPropDefinition prop, System.Random random, int index, float halfWidth, float halfDepth)
    {
        float scale = RandomRange(random, prop.scaleRange.x, prop.scaleRange.y);
        float propWidth = prop.baseSize.x * scale;
        float propHeight = prop.baseSize.y * scale;
        bool wallPlacement = prop.preferWallPlacement || random.NextDouble() < 0.58;
        Vector3 center;
        float yaw;
        float clearance = 0.18f;

        if (wallPlacement)
        {
            int wall = random.Next(0, 4);
            float x = RandomRange(random, -halfWidth + propWidth * 0.5f + clearance, halfWidth - propWidth * 0.5f - clearance);
            float z = RandomRange(random, -halfDepth + propWidth * 0.5f + clearance, halfDepth - propWidth * 0.5f - clearance);
            switch (wall)
            {
                case 0:
                    center = new Vector3(x, 0.035f + propHeight * 0.5f, -halfDepth + 0.055f);
                    yaw = 0f;
                    break;
                case 1:
                    center = new Vector3(x, 0.035f + propHeight * 0.5f, halfDepth - 0.055f);
                    yaw = 180f;
                    break;
                case 2:
                    center = new Vector3(-halfWidth + 0.055f, 0.035f + propHeight * 0.5f, z);
                    yaw = 90f;
                    break;
                default:
                    center = new Vector3(halfWidth - 0.055f, 0.035f + propHeight * 0.5f, z);
                    yaw = -90f;
                    break;
            }
        }
        else
        {
            center = new Vector3(
                RandomRange(random, -halfWidth + propWidth * 0.6f, halfWidth - propWidth * 0.6f),
                0.035f + propHeight * 0.5f,
                RandomRange(random, -halfDepth + propWidth * 0.6f, halfDepth - propWidth * 0.6f));
            yaw = RandomRange(random, 0f, 360f);
        }

        CreatePropQuad(
            $"Furniture_{index:00}_{SanitizeObjectName(prop.label)}",
            parent,
            center,
            new Vector2(propWidth, propHeight),
            Quaternion.Euler(0f, yaw, 0f),
            prop.maps,
            prop.castShadow);
    }

    private void AddFloorDecal(Transform parent, InteriorPropDefinition prop, System.Random random, int index, float halfWidth, float halfDepth)
    {
        float scale = RandomRange(random, prop.scaleRange.x, prop.scaleRange.y);
        Vector2 size = prop.baseSize * scale;
        Vector3 center = new(
            RandomRange(random, -halfWidth + size.x * 0.5f, halfWidth - size.x * 0.5f),
            0.045f + index * 0.0004f,
            RandomRange(random, -halfDepth + size.y * 0.5f, halfDepth - size.y * 0.5f));
        CreateFloorDecalQuad(
            $"DirtDecal_{index:00}_{SanitizeObjectName(prop.label)}",
            parent,
            center,
            size,
            RandomRange(random, 0f, 360f),
            prop.maps);
    }

    private void CreatePropQuad(
        string objectName,
        Transform parent,
        Vector3 center,
        Vector2 size,
        Quaternion rotation,
        FaceMaps maps,
        bool castShadow)
    {
        Vector3 right = rotation * Vector3.right;
        Vector3 up = Vector3.up;
        Vector3 normal = rotation * Vector3.back;
        Vector3 halfRight = right * (size.x * 0.5f);
        Vector3 halfUp = up * (size.y * 0.5f);
        CreateUniqueSurface(
            objectName,
            parent,
            new[]
            {
                center - halfRight - halfUp,
                center + halfRight - halfUp,
                center + halfRight + halfUp,
                center - halfRight + halfUp
            },
            normal,
            right,
            maps,
            castShadow);
    }

    private void CreateFloorDecalQuad(
        string objectName,
        Transform parent,
        Vector3 center,
        Vector2 size,
        float yaw,
        FaceMaps maps)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 right = rotation * Vector3.right;
        Vector3 forward = rotation * Vector3.forward;
        Vector3 halfRight = right * (size.x * 0.5f);
        Vector3 halfForward = forward * (size.y * 0.5f);
        CreateUniqueSurface(
            objectName,
            parent,
            new[]
            {
                center - halfRight - halfForward,
                center + halfRight - halfForward,
                center + halfRight + halfForward,
                center - halfRight + halfForward
            },
            Vector3.up,
            right,
            maps,
            false);
    }

    private void CreateUniqueSurface(
        string objectName,
        Transform parent,
        Vector3[] vertices,
        Vector3 normal,
        Vector3 tangent,
        FaceMaps maps,
        bool castShadow)
    {
        Mesh mesh = BuildMesh($"Generated {objectName}", vertices, normal, tangent, new[] { 0, 1, 2, 0, 2, 3 });
        GameObject surface = new(objectName)
        {
            hideFlags = HideFlags.DontSave
        };
        surface.transform.SetParent(parent, false);
        MeshFilter filter = surface.AddComponent<MeshFilter>();
        MeshRenderer renderer = surface.AddComponent<MeshRenderer>();
        filter.sharedMesh = mesh;
        renderer.shadowCastingMode = castShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Material material = CreateFaceMaterial(objectName, maps);
        propMaterials.Add(material);
        renderer.sharedMaterial = material;
    }

    private static string SanitizeObjectName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Prop";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = value.Trim();
        for (int i = 0; i < invalid.Length; i++)
        {
            sanitized = sanitized.Replace(invalid[i], '_');
        }

        return sanitized.Replace(' ', '_');
    }

    private void CreateSurface(
        string objectName,
        Transform parent,
        Vector3[] vertices,
        Vector3 normal,
        Vector3 tangent,
        FaceMaps maps,
        ref Material material)
    {
        Mesh mesh = BuildMesh($"Generated {objectName}", vertices, normal, tangent, new[] { 0, 1, 2, 0, 2, 3 });
        GameObject surface = new(objectName)
        {
            hideFlags = HideFlags.DontSave
        };
        surface.transform.SetParent(parent, false);
        MeshFilter filter = surface.AddComponent<MeshFilter>();
        MeshRenderer renderer = surface.AddComponent<MeshRenderer>();
        filter.sharedMesh = mesh;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        material ??= CreateFaceMaterial(objectName, maps);
        renderer.sharedMaterial = material;
    }

    private void CreateTriangle(
        string objectName,
        Transform parent,
        Vector3[] vertices,
        Vector3 normal,
        Vector3 tangent,
        FaceMaps maps,
        ref Material material)
    {
        Mesh mesh = BuildMesh($"Generated {objectName}", vertices, normal, tangent, new[] { 0, 1, 2 });
        GameObject surface = new(objectName)
        {
            hideFlags = HideFlags.DontSave
        };
        surface.transform.SetParent(parent, false);
        MeshFilter filter = surface.AddComponent<MeshFilter>();
        MeshRenderer renderer = surface.AddComponent<MeshRenderer>();
        filter.sharedMesh = mesh;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        material ??= CreateFaceMaterial(objectName, maps);
        renderer.sharedMaterial = material;
    }

    private Mesh BuildMesh(string meshName, Vector3[] vertices, Vector3 normal, Vector3 tangent, int[] triangles)
    {
        Mesh mesh = new()
        {
            name = meshName,
            hideFlags = HideFlags.DontSave
        };
        Vector2[] uvs = vertices.Length == 3
            ? new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f) }
            : new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        Vector3 safeNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        Vector3 safeTangent = tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.right;
        Vector3[] normals = new Vector3[vertices.Length];
        Vector4[] tangents = new Vector4[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            normals[i] = safeNormal;
            tangents[i] = new Vector4(safeTangent.x, safeTangent.y, safeTangent.z, 1f);
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        generatedMeshes.Add(mesh);
        return mesh;
    }

    private void BuildCollisionWalls(Transform parent)
    {
        float halfWidth = width * 0.5f;
        float halfDepth = depth * 0.5f;
        float halfThickness = wallColliderThickness * 0.5f;
        CreateBoxCollider(parent, "FrontWallCollider", new Vector3(0f, wallHeight * 0.5f, -halfDepth), new Vector3(width, wallHeight, wallColliderThickness));
        CreateBoxCollider(parent, "BackWallCollider", new Vector3(0f, wallHeight * 0.5f, halfDepth), new Vector3(width, wallHeight, wallColliderThickness));
        CreateBoxCollider(parent, "LeftWallCollider", new Vector3(-halfWidth, wallHeight * 0.5f, 0f), new Vector3(wallColliderThickness, wallHeight, depth + wallColliderThickness));
        CreateBoxCollider(parent, "RightWallCollider", new Vector3(halfWidth, wallHeight * 0.5f, 0f), new Vector3(wallColliderThickness, wallHeight, depth + wallColliderThickness));
        if (roofHeight > 0.001f)
        {
            CreateBoxCollider(
                parent,
                "RoofCollider",
                new Vector3(0f, wallHeight + roofHeight * 0.5f, 0f),
                new Vector3(width + roofOverhang * 2f, roofHeight + halfThickness, depth + roofOverhang * 2f));
        }
    }

    private static void CreateBoxCollider(Transform parent, string objectName, Vector3 center, Vector3 size)
    {
        GameObject colliderObject = new(objectName)
        {
            hideFlags = HideFlags.DontSave
        };
        colliderObject.transform.SetParent(parent, false);
        BoxCollider box = colliderObject.AddComponent<BoxCollider>();
        box.center = center;
        box.size = SanitizeVector(size, 0.01f);
    }

    private void ConfigureNavigationObstacle()
    {
        if (!blockNavigation || generatedRoot == null)
        {
            return;
        }

        RetroNavMeshDynamicObstacle dynamicObstacle = generatedRoot.GetComponent<RetroNavMeshDynamicObstacle>();
        if (dynamicObstacle == null)
        {
            dynamicObstacle = generatedRoot.AddComponent<RetroNavMeshDynamicObstacle>();
        }

        Vector3 obstacleSize = new(
            width + navObstaclePadding * 2f,
            Mathf.Max(0.5f, wallHeight + roofHeight),
            depth + navObstaclePadding * 2f);
        dynamicObstacle.ConfigureBox(obstacleSize, new Vector3(0f, obstacleSize.y * 0.5f, 0f), Application.isPlaying);
    }

    private Material CreateFaceMaterial(string materialName, FaceMaps maps)
    {
        Shader shader = Shader.Find(BuildingShaderName)
            ?? Shader.Find("HDRP/Lit")
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");
        Material material = new(shader)
        {
            name = $"Runtime Hybrid Building {materialName}",
            hideFlags = HideFlags.DontSave
        };
        ApplyFaceMaterialSettings(material, maps);
        return material;
    }

    private void ApplyMaterials()
    {
        ApplyFaceMaterialSettings(frontMaterial, frontMaps);
        ApplyFaceMaterialSettings(sideMaterial, sideMaps);
        ApplyFaceMaterialSettings(backMaterial, backMaps);
        ApplyFaceMaterialSettings(roofMaterial, roofMaps);
        ApplyFaceMaterialSettings(doorMaterial, doorMaps);
        ApplyFaceMaterialSettings(interiorWallMaterial, interiorWallMaps);
        ApplyFaceMaterialSettings(interiorFloorMaterial, interiorFloorMaps);
        ApplyFaceMaterialSettings(interiorCeilingMaterial, interiorCeilingMaps);
        ApplyFaceMaterialSettings(interiorDoorMaterial, interiorDoorMaps);
    }

    private void ApplyFaceMaterialSettings(Material material, FaceMaps maps)
    {
        if (material == null)
        {
            return;
        }

        Texture2D baseMap = maps != null ? maps.baseMap : null;
        Texture2D normalMap = maps != null ? maps.normalMap : null;
        Texture2D heightMap = maps != null ? maps.heightMap : null;
        Texture2D packedMasksMap = maps != null ? maps.packedMasksMap : null;
        Texture2D emissionMap = maps != null ? maps.emissionMap : null;

        SetTextureIfPresent(material, BaseMapId, baseMap);
        SetTextureIfPresent(material, NormalMapId, normalMap);
        SetTextureIfPresent(material, HeightMapId, heightMap);
        SetTextureIfPresent(material, PackedMasksId, packedMasksMap);
        SetTextureIfPresent(material, EmissionMapId, emissionMap);
        SetColorIfPresent(material, BaseColorId, baseColorTint);
        SetFloatIfPresent(material, AlphaCutoffId, alphaCutoff);
        SetFloatIfPresent(material, NormalScaleId, normalScale);
        SetFloatIfPresent(material, HeightContrastId, heightContrast);
        SetFloatIfPresent(material, AoStrengthId, aoStrength);
        SetFloatIfPresent(material, RoughnessScaleId, roughnessScale);
        SetFloatIfPresent(material, SpecularStrengthId, specularStrength);
        SetFloatIfPresent(material, EdgeWearBrightnessId, edgeWearBrightness);
        SetFloatIfPresent(material, CrackDarkeningId, crackDarkening);
        SetFloatIfPresent(material, EmissionStrengthId, emissionStrength);
        SetColorIfPresent(material, AmbientTopColorId, ResolveAmbientTop());
        SetColorIfPresent(material, AmbientBottomColorId, ResolveAmbientBottom());
        SetFloatIfPresent(material, AmbientIntensityId, ambientIntensity);
        SetFloatIfPresent(material, UseNormalMapId, normalMap != null ? 1f : 0f);
        SetFloatIfPresent(material, UseHeightMapId, heightMap != null ? 1f : 0f);
        SetFloatIfPresent(material, UsePackedMasksId, packedMasksMap != null ? 1f : 0f);
        SetFloatIfPresent(material, UseEmissionMapId, emissionMap != null ? 1f : 0f);

        SetTextureIfPresent(material, "_BaseColorMap", baseMap);
        SetTextureIfPresent(material, "_MainTex", baseMap);
        SetTextureIfPresent(material, "_BaseMap", baseMap);
        SetTextureIfPresent(material, "_BumpMap", normalMap);
        SetTextureIfPresent(material, "_NormalMap", normalMap);
        SetFloatIfPresent(material, "_Smoothness", 1f - Mathf.Clamp01(0.78f * roughnessScale));
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

    private void UpdateRuntime(float time)
    {
        ApplyMaterials();
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
        Vector3 anchor = transform.position + Vector3.up * (wallHeight * 0.58f);
        if (activeCamera != null)
        {
            anchor = Vector3.Lerp(anchor, activeCamera.transform.position, 0.12f);
        }

        int lightCount = CollectBestLights(anchor);
        ApplyLightingToMaterial(frontMaterial, lightCount);
        ApplyLightingToMaterial(sideMaterial, lightCount);
        ApplyLightingToMaterial(backMaterial, lightCount);
        ApplyLightingToMaterial(roofMaterial, lightCount);
        ApplyLightingToMaterial(doorMaterial, lightCount);
        ApplyLightingToMaterial(interiorWallMaterial, lightCount);
        ApplyLightingToMaterial(interiorFloorMaterial, lightCount);
        ApplyLightingToMaterial(interiorCeilingMaterial, lightCount);
        ApplyLightingToMaterial(interiorDoorMaterial, lightCount);
        for (int i = 0; i < propMaterials.Count; i++)
        {
            ApplyLightingToMaterial(propMaterials[i], lightCount);
        }
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
        for (int i = 0; i < generatedMeshes.Count; i++)
        {
            DestroyUnityObject(generatedMeshes[i]);
        }

        generatedMeshes.Clear();
    }

    private void DestroyRuntimeMaterials()
    {
        DestroyUnityObject(frontMaterial);
        DestroyUnityObject(sideMaterial);
        DestroyUnityObject(backMaterial);
        DestroyUnityObject(roofMaterial);
        DestroyUnityObject(doorMaterial);
        DestroyUnityObject(interiorWallMaterial);
        DestroyUnityObject(interiorFloorMaterial);
        DestroyUnityObject(interiorCeilingMaterial);
        DestroyUnityObject(interiorDoorMaterial);
        for (int i = 0; i < propMaterials.Count; i++)
        {
            DestroyUnityObject(propMaterials[i]);
        }

        propMaterials.Clear();
        frontMaterial = null;
        sideMaterial = null;
        backMaterial = null;
        roofMaterial = null;
        doorMaterial = null;
        interiorWallMaterial = null;
        interiorFloorMaterial = null;
        interiorCeilingMaterial = null;
        interiorDoorMaterial = null;
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

    private static Vector3 SanitizeVector(Vector3 value, float minimum)
    {
        value.x = Mathf.Max(minimum, value.x);
        value.y = Mathf.Max(minimum, value.y);
        value.z = Mathf.Max(minimum, value.z);
        return value;
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        if (max <= min)
        {
            return min;
        }

        return Mathf.Lerp(min, max, (float)random.NextDouble());
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
internal static class RetroHybridBuildingEditorPreviewBridge
{
    private const double UpdateInterval = 0.25;

    private static double nextUpdateTime;

    static RetroHybridBuildingEditorPreviewBridge()
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
        RefreshVisibleBuildings(false);
    }

    private static void FullRefresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        RefreshVisibleBuildings(true);
        SceneView.RepaintAll();
    }

    private static void RefreshVisibleBuildings(bool force)
    {
        RetroHybridBuilding[] buildings = Object.FindObjectsByType<RetroHybridBuilding>(FindObjectsInactive.Exclude);
        foreach (RetroHybridBuilding building in buildings)
        {
            if (building == null)
            {
                continue;
            }

            building.EnsureEditorPreview(force);
        }
    }
}
#endif
