using System;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class RetroCursedParkGenerator : MonoBehaviour
{
    private const string GeneratedRootName = "__CursedParkGenerated";

    [Header("Assets")]
    [SerializeField] private RetroCursedParkAssetLibrary assetLibrary;
    [SerializeField] private Material spriteMaterial;
    [SerializeField] private Material groundMaterial;
    [SerializeField] private Material pathMaterial;

    [Header("Layout")]
    [SerializeField] private int seed = 666013;
    [SerializeField, Min(12f)] private float parkRadius = 48f;
    [SerializeField, Min(4f)] private float innerDeadZoneRadius = 8f;
    [SerializeField, Min(1f)] private float midwayWidth = 7f;
    [SerializeField, Min(0)] private int majorAttractionCount = 14;
    [SerializeField, Min(0)] private int machineCount = 30;
    [SerializeField, Min(0)] private int automatonCount = 24;
    [SerializeField, Min(0)] private int signageClutterCount = 78;
    [SerializeField, Min(0)] private int groundDecalCount = 120;
    [SerializeField, Min(0)] private int fenceSegmentCount = 54;
    [SerializeField, Min(0f)] private float heightJitter = 0.08f;
    [SerializeField] private bool rebuildOnStart;

    [Header("Atmosphere")]
    [SerializeField] private Color groundColor = new(0.58f, 0.47f, 0.31f, 1f);
    [SerializeField] private Color pathColor = new(0.24f, 0.19f, 0.15f, 1f);
    [SerializeField] private Color cursedGlowColor = new(1f, 0.34f, 0.08f, 1f);

    public RetroCursedParkAssetLibrary AssetLibrary
    {
        get => assetLibrary;
        set => assetLibrary = value;
    }

    public Material SpriteMaterial
    {
        get => spriteMaterial;
        set => spriteMaterial = value;
    }

    public Material GroundMaterial
    {
        get => groundMaterial;
        set => groundMaterial = value;
    }

    public Material PathMaterial
    {
        get => pathMaterial;
        set => pathMaterial = value;
    }

    private void Start()
    {
        if (Application.isPlaying && rebuildOnStart)
        {
            RebuildParkNow();
        }
    }

    private void OnValidate()
    {
        parkRadius = Mathf.Max(12f, parkRadius);
        innerDeadZoneRadius = Mathf.Clamp(innerDeadZoneRadius, 1f, parkRadius * 0.7f);
        midwayWidth = Mathf.Max(1f, midwayWidth);
        majorAttractionCount = Mathf.Max(0, majorAttractionCount);
        machineCount = Mathf.Max(0, machineCount);
        automatonCount = Mathf.Max(0, automatonCount);
        signageClutterCount = Mathf.Max(0, signageClutterCount);
        groundDecalCount = Mathf.Max(0, groundDecalCount);
        fenceSegmentCount = Mathf.Max(0, fenceSegmentCount);
        heightJitter = Mathf.Max(0f, heightJitter);
    }

    [ContextMenu("Rebuild Cursed Park Now")]
    public void RebuildParkNow()
    {
        ClearGenerated();
        Transform generatedRoot = CreateGeneratedRoot();
        System.Random random = new(seed);

        CreateGround(generatedRoot);
        CreatePathNetwork(generatedRoot, random);
        SpawnPerimeterFence(generatedRoot, random);
        SpawnEntrance(generatedRoot, random);
        SpawnMajorAttractions(generatedRoot, random);
        SpawnWeightedCategory(generatedRoot, random, RetroCursedParkAssetCategory.Machine, machineCount, innerDeadZoneRadius + 4f, parkRadius * 0.72f, "Play", 36);
        SpawnWeightedCategory(generatedRoot, random, RetroCursedParkAssetCategory.Automaton, automatonCount, innerDeadZoneRadius + 2f, parkRadius * 0.76f, "Wake", 42);
        SpawnWeightedCategory(generatedRoot, random, RetroCursedParkAssetCategory.SignageClutter, signageClutterCount, innerDeadZoneRadius, parkRadius * 0.95f, "Inspect", 18);
        SpawnGroundDecals(generatedRoot, random);
    }

    public void ClearGenerated()
    {
        Transform generated = transform.Find(GeneratedRootName);
        if (generated == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(generated.gameObject);
            return;
        }

        DestroyImmediate(generated.gameObject);
    }

    private Transform CreateGeneratedRoot()
    {
        GameObject root = new(GeneratedRootName);
        root.transform.SetParent(transform, false);
        return root.transform;
    }

    private void CreateGround(Transform parent)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ground.name = "DesertBasin";
        ground.transform.SetParent(parent, false);
        ground.transform.localPosition = new Vector3(0f, -0.035f, 0f);
        ground.transform.localScale = new Vector3(parkRadius * 2.08f, 0.035f, parkRadius * 2.08f);
        Collider collider = ground.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }

        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = groundMaterial != null ? groundMaterial : CreateFallbackMaterial("CursedPark_Ground", groundColor);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }
    }

    private void CreatePathNetwork(Transform parent, System.Random random)
    {
        float loopRadius = Mathf.Lerp(innerDeadZoneRadius, parkRadius, 0.5f);
        int loopSegments = Mathf.Max(24, Mathf.RoundToInt(parkRadius * 0.9f));
        for (int i = 0; i < loopSegments; i++)
        {
            float a0 = (i / (float)loopSegments) * Mathf.PI * 2f;
            float a1 = ((i + 1) / (float)loopSegments) * Mathf.PI * 2f;
            Vector3 p0 = new(Mathf.Cos(a0) * loopRadius, 0.012f, Mathf.Sin(a0) * loopRadius);
            Vector3 p1 = new(Mathf.Cos(a1) * loopRadius, 0.012f, Mathf.Sin(a1) * loopRadius);
            CreatePathStrip(parent, $"LoopPath_{i:00}", (p0 + p1) * 0.5f, p1 - p0, midwayWidth * Lerp(random, 0.72f, 1.12f));
        }

        int spokes = 7;
        for (int i = 0; i < spokes; i++)
        {
            float angle = (i / (float)spokes) * Mathf.PI * 2f + Lerp(random, -0.13f, 0.13f);
            Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            float length = parkRadius * Lerp(random, 0.62f, 0.95f);
            Vector3 center = direction * (innerDeadZoneRadius + length * 0.45f);
            CreatePathStrip(parent, $"SpokePath_{i:00}", center + Vector3.up * 0.014f, direction * length, midwayWidth * Lerp(random, 0.45f, 0.72f));
        }
    }

    private void CreatePathStrip(Transform parent, string name, Vector3 center, Vector3 direction, float width)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        GameObject path = GameObject.CreatePrimitive(PrimitiveType.Quad);
        path.name = name;
        path.transform.SetParent(parent, false);
        path.transform.localPosition = center;
        path.transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
        path.transform.localScale = new Vector3(width, direction.magnitude, 1f);

        Collider collider = path.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }

        Renderer renderer = path.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = pathMaterial != null ? pathMaterial : CreateFallbackMaterial("CursedPark_Path", pathColor);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }
    }

    private void SpawnPerimeterFence(Transform parent, System.Random random)
    {
        RetroCursedParkSpriteAsset fence = assetLibrary != null ? assetLibrary.FindById("BarbedFenceSegment") : null;
        if (fence == null || fenceSegmentCount <= 0)
        {
            return;
        }

        for (int i = 0; i < fenceSegmentCount; i++)
        {
            float angle = (i / (float)fenceSegmentCount) * Mathf.PI * 2f;
            float radius = parkRadius * Lerp(random, 0.96f, 1.03f);
            Vector3 position = new(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            float yaw = -angle * Mathf.Rad2Deg + 90f + Lerp(random, -5f, 5f);
            SpawnSprite(parent, fence, position, yaw, Lerp(random, 1.2f, 1.9f), false, "Fence", 0);
        }
    }

    private void SpawnEntrance(Transform parent, System.Random random)
    {
        RetroCursedParkSpriteAsset entrance = assetLibrary != null ? assetLibrary.FindById("EntranceSignFrame") : null;
        if (entrance == null)
        {
            return;
        }

        Vector3 position = new(0f, 0f, -parkRadius * 0.98f);
        SpawnSprite(parent, entrance, position, 180f, Lerp(random, 2.4f, 3.1f), true, "Enter", 60);
    }

    private void SpawnMajorAttractions(Transform parent, System.Random random)
    {
        int count = Mathf.Max(0, majorAttractionCount);
        for (int i = 0; i < count; i++)
        {
            if (!TryPick(RetroCursedParkAssetCategory.MajorAttraction, random, out RetroCursedParkSpriteAsset asset))
            {
                return;
            }

            float baseAngle = (i / Mathf.Max(1f, count)) * Mathf.PI * 2f;
            float angle = baseAngle + Lerp(random, -0.2f, 0.2f);
            float radius = Lerp(random, parkRadius * 0.42f, parkRadius * 0.82f);
            Vector3 position = new(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            float yaw = YawToward(position, Vector3.zero) + Lerp(random, -18f, 18f);
            SpawnSprite(parent, asset, position, yaw, Lerp(random, 0.92f, 1.42f), true, "Enter", 48);
        }
    }

    private void SpawnWeightedCategory(
        Transform parent,
        System.Random random,
        RetroCursedParkAssetCategory category,
        int count,
        float minRadius,
        float maxRadius,
        string verb,
        int priority)
    {
        for (int i = 0; i < count; i++)
        {
            if (!TryPick(category, random, out RetroCursedParkSpriteAsset asset))
            {
                return;
            }

            Vector3 position = SampleAnnulus(random, minRadius, maxRadius);
            float yaw = YawToward(position, Vector3.zero) + Lerp(random, -28f, 28f);
            float scale = Lerp(random, asset.ScaleRange.x, asset.ScaleRange.y);
            bool interactive = asset.InteractableCandidate && random.NextDouble() > 0.12;
            SpawnSprite(parent, asset, position, yaw, scale, interactive, verb, priority);
        }
    }

    private void SpawnGroundDecals(Transform parent, System.Random random)
    {
        for (int i = 0; i < groundDecalCount; i++)
        {
            if (!TryPick(RetroCursedParkAssetCategory.GroundDecal, random, out RetroCursedParkSpriteAsset asset))
            {
                return;
            }

            Vector3 position = SampleAnnulus(random, innerDeadZoneRadius * 0.45f, parkRadius * 0.92f);
            float yaw = Lerp(random, 0f, 360f);
            float scale = Lerp(random, asset.ScaleRange.x, asset.ScaleRange.y);
            SpawnSprite(parent, asset, position, yaw, scale, false, "Inspect", 0);
        }
    }

    private GameObject SpawnSprite(
        Transform parent,
        RetroCursedParkSpriteAsset asset,
        Vector3 position,
        float yawDegrees,
        float scale,
        bool interactive,
        string verb,
        int priority)
    {
        if (asset == null || asset.BaseMap == null)
        {
            return null;
        }

        bool groundDecal = asset.GroundDecal;
        Vector2 safeSize = new Vector2(Mathf.Max(0.1f, asset.BaseSize.x), Mathf.Max(0.1f, asset.BaseSize.y)) * Mathf.Max(0.01f, scale);
        GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Quad);
        prop.name = asset.Id;
        prop.transform.SetParent(parent, false);
        prop.transform.localPosition = groundDecal
            ? new Vector3(position.x, 0.025f + Lerp01(asset.Id) * 0.012f, position.z)
            : new Vector3(position.x, safeSize.y * 0.5f + Lerp01(asset.Id) * heightJitter, position.z);
        prop.transform.localRotation = groundDecal
            ? Quaternion.Euler(90f, yawDegrees, 0f)
            : Quaternion.Euler(0f, yawDegrees, 0f);
        prop.transform.localScale = new Vector3(safeSize.x, safeSize.y, 1f);

        Collider primitiveCollider = prop.GetComponent<Collider>();
        if (primitiveCollider != null)
        {
            if (Application.isPlaying)
            {
                Destroy(primitiveCollider);
            }
            else
            {
                DestroyImmediate(primitiveCollider);
            }
        }

        Renderer renderer = prop.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = spriteMaterial != null ? spriteMaterial : CreateFallbackMaterial("CursedPark_Sprite", Color.white);
            renderer.shadowCastingMode = asset.CastShadow && !groundDecal ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = asset.ReceiveShadow;
            renderer.sortingOrder = groundDecal ? -20 : 0;
        }

        RetroCursedParkSpriteProp spriteProp = prop.AddComponent<RetroCursedParkSpriteProp>();
        spriteProp.Configure(asset, renderer);

        if (interactive && !groundDecal)
        {
            BoxCollider collider = prop.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = Vector3.zero;
            collider.size = new Vector3(1f, 1f, 0.22f);

            RetroCursedParkInteractableAnchor anchor = prop.AddComponent<RetroCursedParkInteractableAnchor>();
            anchor.Configure(asset.DisplayName, verb, BuildInteractionLine(asset), Mathf.Max(3.1f, safeSize.x * 0.45f), priority);
        }

        return prop;
    }

    private bool TryPick(RetroCursedParkAssetCategory category, System.Random random, out RetroCursedParkSpriteAsset asset)
    {
        asset = null;
        return assetLibrary != null && assetLibrary.TryGetWeighted(category, random, out asset);
    }

    private Vector3 SampleAnnulus(System.Random random, float minRadius, float maxRadius)
    {
        float angle = Lerp(random, 0f, Mathf.PI * 2f);
        float min = Mathf.Max(0f, minRadius);
        float max = Mathf.Max(min + 0.1f, maxRadius);
        float radius = Mathf.Sqrt(Lerp(random, min * min, max * max));
        return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    private string BuildInteractionLine(RetroCursedParkSpriteAsset asset)
    {
        return asset.Category switch
        {
            RetroCursedParkAssetCategory.Machine => "The mechanism coughs, flashes, and waits for rules it has not learned yet.",
            RetroCursedParkAssetCategory.Automaton => "Its joints twitch in a rehearsed way, like a future behavior slot.",
            RetroCursedParkAssetCategory.MajorAttraction => "The entrance breathes hot dust. Ride logic can be attached here.",
            _ => "It is part of the park's curse, currently decorative."
        };
    }

    private static float YawToward(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        return Quaternion.LookRotation(direction.normalized, Vector3.up).eulerAngles.y;
    }

    private static float Lerp(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private static float Lerp01(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return 0f;
        }

        unchecked
        {
            int hash = 17;
            for (int i = 0; i < key.Length; i++)
            {
                hash = hash * 31 + key[i];
            }

            return (hash & 1023) / 1023f;
        }
    }

    private static Material CreateFallbackMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        shader ??= Shader.Find("Unlit/Color");
        shader ??= Shader.Find("Standard");
        Material material = new(shader)
        {
            name = materialName
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        return material;
    }
}
