using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class RetroHybridBuildingBuilderWindow : EditorWindow
{
    private const string DefaultPrefabPath = "Assets/Buildings/SettlementHouse/Prefabs/SettlementHouse.prefab";
    private const string GeneratedRootName = "__HybridBuildingGenerated";
    private const string ExteriorDoorObjectName = "ExteriorDoorInteraction";
    private const string InteriorDoorObjectName = "InteriorExitInteraction";
    private const string InsideSpawnName = "InsideSpawn";
    private const string OutsideSpawnName = "OutsideSpawn";

    private static readonly (string propertyPath, string assetPath)[] DefaultTextureBindings =
    {
        ("frontMaps.baseMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Front_Base.png"),
        ("frontMaps.normalMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Front_Normal.png"),
        ("frontMaps.heightMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Front_Height.png"),
        ("frontMaps.packedMasksMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Front_PackedMasks.png"),
        ("frontMaps.emissionMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Front_Emission.png"),

        ("sideMaps.baseMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Side_Base.png"),
        ("sideMaps.normalMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Side_Normal.png"),
        ("sideMaps.heightMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Side_Height.png"),
        ("sideMaps.packedMasksMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Side_PackedMasks.png"),
        ("sideMaps.emissionMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Side_Emission.png"),

        ("backMaps.baseMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Back_Base.png"),
        ("backMaps.normalMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Back_Normal.png"),
        ("backMaps.heightMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Back_Height.png"),
        ("backMaps.packedMasksMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Back_PackedMasks.png"),
        ("backMaps.emissionMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Back_Emission.png"),

        ("roofMaps.baseMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Roof_Base.png"),
        ("roofMaps.normalMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Roof_Normal.png"),
        ("roofMaps.heightMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Roof_Height.png"),
        ("roofMaps.packedMasksMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Roof_PackedMasks.png"),
        ("roofMaps.emissionMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Roof_Emission.png"),

        ("doorMaps.baseMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Door_Base.png"),
        ("doorMaps.normalMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Door_Normal.png"),
        ("doorMaps.heightMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Door_Height.png"),
        ("doorMaps.packedMasksMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Door_PackedMasks.png"),
        ("doorMaps.emissionMap", "Assets/Buildings/SettlementHouse/Textures/SettlementHouse_Door_Emission.png"),

        ("interiorWallMaps.baseMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorWall_Base.png"),
        ("interiorWallMaps.normalMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorWall_Normal.png"),
        ("interiorWallMaps.heightMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorWall_Height.png"),
        ("interiorWallMaps.packedMasksMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorWall_PackedMasks.png"),
        ("interiorWallMaps.emissionMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorWall_Emission.png"),

        ("interiorFloorMaps.baseMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorFloor_Base.png"),
        ("interiorFloorMaps.normalMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorFloor_Normal.png"),
        ("interiorFloorMaps.heightMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorFloor_Height.png"),
        ("interiorFloorMaps.packedMasksMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorFloor_PackedMasks.png"),
        ("interiorFloorMaps.emissionMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorFloor_Emission.png"),

        ("interiorCeilingMaps.baseMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorCeiling_Base.png"),
        ("interiorCeilingMaps.normalMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorCeiling_Normal.png"),
        ("interiorCeilingMaps.heightMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorCeiling_Height.png"),
        ("interiorCeilingMaps.packedMasksMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorCeiling_PackedMasks.png"),
        ("interiorCeilingMaps.emissionMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorCeiling_Emission.png"),

        ("interiorDoorMaps.baseMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorDoor_Base.png"),
        ("interiorDoorMaps.normalMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorDoor_Normal.png"),
        ("interiorDoorMaps.heightMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorDoor_Height.png"),
        ("interiorDoorMaps.packedMasksMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorDoor_PackedMasks.png"),
        ("interiorDoorMaps.emissionMap", "Assets/Buildings/SettlementHouse/Interior/Textures/InteriorDoor_Emission.png")
    };

    private static readonly (string label, string folder, Vector2 size, Vector2 scaleRange, bool preferWall, bool castShadow)[] FurnitureProps =
    {
        ("TornSofa", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/TornSofa", new Vector2(1.85f, 0.92f), new Vector2(0.9f, 1.18f), true, true),
        ("DirtyTable", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/DirtyTable", new Vector2(1.28f, 0.84f), new Vector2(0.85f, 1.16f), false, true),
        ("BrokenChair", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/BrokenChair", new Vector2(0.72f, 1.02f), new Vector2(0.82f, 1.15f), false, true),
        ("SmallStool", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/SmallStool", new Vector2(0.62f, 0.58f), new Vector2(0.75f, 1.1f), false, true),
        ("RottenCabinet", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/RottenCabinet", new Vector2(1.1f, 1.75f), new Vector2(0.86f, 1.12f), true, true),
        ("CrookedBookshelf", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/CrookedBookshelf", new Vector2(1.18f, 1.95f), new Vector2(0.82f, 1.08f), true, true),
        ("RustyStove", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/RustyStove", new Vector2(0.95f, 1.2f), new Vector2(0.86f, 1.08f), true, true),
        ("OilLamp", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/OilLamp", new Vector2(0.45f, 0.72f), new Vector2(0.8f, 1.25f), false, true),
        ("CrateStack", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/CrateStack", new Vector2(1.05f, 1.05f), new Vector2(0.85f, 1.2f), true, true),
        ("Barrel", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/Barrel", new Vector2(0.82f, 1.15f), new Vector2(0.85f, 1.2f), true, true),
        ("TrashBags", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/TrashBags", new Vector2(1.15f, 0.78f), new Vector2(0.82f, 1.25f), true, true),
        ("RolledRug", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/RolledRug", new Vector2(1.18f, 0.55f), new Vector2(0.85f, 1.28f), false, true),
        ("FilthyMattress", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/FilthyMattress", new Vector2(1.72f, 0.72f), new Vector2(0.88f, 1.18f), true, true),
        ("BrokenFrame", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/BrokenFrame", new Vector2(0.72f, 0.92f), new Vector2(0.82f, 1.14f), true, false),
        ("Bucket", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/Bucket", new Vector2(0.58f, 0.68f), new Vector2(0.82f, 1.22f), false, true),
        ("SideTableBottles", "Assets/Buildings/SettlementHouse/Interior/Props/Furniture/SideTableBottles", new Vector2(0.82f, 0.95f), new Vector2(0.82f, 1.18f), false, true)
    };

    private static readonly (string label, string folder, Vector2 size, Vector2 scaleRange)[] DirtDecals =
    {
        ("MudStain", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/MudStain", new Vector2(1.15f, 0.85f), new Vector2(0.8f, 1.45f)),
        ("OilStain", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/OilStain", new Vector2(1.05f, 0.8f), new Vector2(0.75f, 1.35f)),
        ("DustPile", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/DustPile", new Vector2(0.9f, 0.72f), new Vector2(0.75f, 1.3f)),
        ("GlassShards", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/GlassShards", new Vector2(0.8f, 0.72f), new Vector2(0.7f, 1.18f)),
        ("TornPapers", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/TornPapers", new Vector2(0.92f, 0.72f), new Vector2(0.75f, 1.25f)),
        ("ClothScrap", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/ClothScrap", new Vector2(0.96f, 0.72f), new Vector2(0.75f, 1.22f)),
        ("RubblePile", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/RubblePile", new Vector2(0.95f, 0.78f), new Vector2(0.78f, 1.28f)),
        ("SpilledAsh", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/SpilledAsh", new Vector2(1.05f, 0.75f), new Vector2(0.78f, 1.35f)),
        ("MoldPatch", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/MoldPatch", new Vector2(0.95f, 0.82f), new Vector2(0.75f, 1.28f)),
        ("Footprints", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/Footprints", new Vector2(1.05f, 0.85f), new Vector2(0.72f, 1.2f)),
        ("RopeCoil", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/RopeCoil", new Vector2(0.8f, 0.8f), new Vector2(0.75f, 1.18f)),
        ("SawdustPile", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/SawdustPile", new Vector2(1.05f, 0.72f), new Vector2(0.8f, 1.35f)),
        ("CrackedBoard", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/CrackedBoard", new Vector2(1.05f, 0.82f), new Vector2(0.75f, 1.25f)),
        ("TinyDebris", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/TinyDebris", new Vector2(0.85f, 0.7f), new Vector2(0.72f, 1.2f)),
        ("CircularRugStain", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/CircularRugStain", new Vector2(1.15f, 1.0f), new Vector2(0.78f, 1.32f)),
        ("BottleShards", "Assets/Buildings/SettlementHouse/Interior/Props/Decals/BottleShards", new Vector2(0.82f, 0.72f), new Vector2(0.72f, 1.18f))
    };

    private Vector2 scroll;
    private string prefabPath = DefaultPrefabPath;
    private bool selectCreatedObject = true;

    [MenuItem("Tools/Ultraloud/Buildings/Hybrid Building Builder")]
    public static void Open()
    {
        RetroHybridBuildingBuilderWindow window = GetWindow<RetroHybridBuildingBuilderWindow>("Hybrid Building");
        window.minSize = new Vector2(500f, 330f);
    }

    [MenuItem("GameObject/Ultraloud/Buildings/Settlement House", false, 23)]
    public static void CreateSceneBuilding(MenuCommand command)
    {
        GameObject buildingObject = CreateConfiguredBuildingObject("SettlementHouse");
        GameObject parent = command.context as GameObject;
        if (parent != null)
        {
            Undo.SetTransformParent(buildingObject.transform, parent.transform, "Create Settlement House");
            buildingObject.transform.localPosition = Vector3.zero;
            buildingObject.transform.localRotation = Quaternion.identity;
        }
        else if (SceneView.lastActiveSceneView != null)
        {
            buildingObject.transform.position = SceneView.lastActiveSceneView.pivot;
        }

        Undo.RegisterCreatedObjectUndo(buildingObject, "Create Settlement House");
        RebuildBuilding(buildingObject.GetComponent<RetroHybridBuilding>());
        Selection.activeGameObject = buildingObject;
        EditorSceneManager.MarkSceneDirty(buildingObject.scene);
    }

    [MenuItem("Assets/Create/Ultraloud/Buildings/Settlement House Prefab")]
    public static void CreateDefaultPrefabFromAssetsMenu()
    {
        CreateOrReplacePrefab(DefaultPrefabPath, true);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        using (EditorGUILayout.ScrollViewScope scope = new(scroll))
        {
            scroll = scope.scrollPosition;
            EditorGUILayout.LabelField("Hybrid Building Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Builds a hollow 3D shell from whole-face ImageGen sprites. Front, side, back, roof, and door are separate sprite sources, so authoring stays precise and the door collider is not derived from atlas cuts.", MessageType.Info);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Building In Scene", GUILayout.Height(32f)))
                {
                    CreateSceneBuilding(new MenuCommand(Selection.activeGameObject));
                }

                if (GUILayout.Button("Rebuild Selected", GUILayout.Height(32f)))
                {
                    RebuildSelectedBuildings();
                }
            }

            EditorGUILayout.Space(10f);
            prefabPath = EditorGUILayout.TextField("Prefab Path", prefabPath);
            selectCreatedObject = EditorGUILayout.Toggle("Select Created Asset", selectCreatedObject);
            if (GUILayout.Button("Create / Replace Prefab Asset", GUILayout.Height(30f)))
            {
                CreateOrReplacePrefab(prefabPath, selectCreatedObject);
            }

            EditorGUILayout.Space(10f);
            DrawAssetStatus("Default Sprite Maps", DefaultTextureBindings);
        }
    }

    public static GameObject CreateConfiguredBuildingObject(string objectName)
    {
        ConfigureTextureImporters();
        GameObject buildingObject = new(objectName);
        RetroHybridBuilding building = buildingObject.AddComponent<RetroHybridBuilding>();
        AssignDefaultMaps(building);
        ApplyExampleDefaults(building);
        SyncDoorAndSpawn(building);
        return buildingObject;
    }

    public static void AssignDefaultMaps(RetroHybridBuilding building)
    {
        if (building == null)
        {
            return;
        }

        ConfigureTextureImporters();
        SerializedObject serializedBuilding = new(building);
        serializedBuilding.Update();
        foreach ((string propertyPath, string assetPath) in DefaultTextureBindings)
        {
            SerializedProperty property = serializedBuilding.FindProperty(propertyPath);
            if (property == null)
            {
                Debug.LogWarning($"RetroHybridBuilding is missing serialized property '{propertyPath}'.", building);
                continue;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                Debug.LogWarning($"Building texture map is missing at {assetPath}.", building);
                continue;
            }

            property.objectReferenceValue = texture;
        }

        AssignFurniturePropLibrary(serializedBuilding);
        AssignDirtDecalLibrary(serializedBuilding);
        serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(building);
    }

    public static void RebuildBuilding(RetroHybridBuilding building)
    {
        if (building == null)
        {
            return;
        }

        ConfigureTextureImporters();
        AssignDefaultMaps(building);
        SyncDoorAndSpawn(building);
        building.RebuildBuildingNow();
        EditorUtility.SetDirty(building);
        if (!Application.isPlaying && building.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
        }
    }

    public static void SyncDoorAndSpawn(RetroHybridBuilding building)
    {
        if (building == null)
        {
            return;
        }

        Transform root = building.transform;
        Transform insideSpawn = FindOrCreateChild(root, InsideSpawnName);
        insideSpawn.localPosition = building.InsideSpawnLocalPosition;
        insideSpawn.localRotation = building.InsideSpawnLocalRotation;
        insideSpawn.localScale = Vector3.one;

        Transform outsideSpawn = FindOrCreateChild(root, OutsideSpawnName);
        outsideSpawn.localPosition = building.OutsideSpawnLocalPosition;
        outsideSpawn.localRotation = building.OutsideSpawnLocalRotation;
        outsideSpawn.localScale = Vector3.one;

        Transform exteriorDoor = FindOrCreateChild(root, ExteriorDoorObjectName);
        RetroBuildingDoorInteractable exteriorDoorInteractable = ConfigureDoorInteraction(
            exteriorDoor,
            building,
            RetroBuildingDoorSide.Exterior,
            building.ExteriorDoorColliderLocalCenter,
            building.DoorColliderSize,
            insideSpawn,
            outsideSpawn,
            "Enter",
            "You step inside.");

        Transform interiorDoor = FindOrCreateChild(root, InteriorDoorObjectName);
        RetroBuildingDoorInteractable interiorDoorInteractable = ConfigureDoorInteraction(
            interiorDoor,
            building,
            RetroBuildingDoorSide.Interior,
            building.InteriorDoorColliderLocalCenter,
            building.DoorColliderSize,
            outsideSpawn,
            insideSpawn,
            "Exit",
            "You step outside.");
        if (exteriorDoorInteractable != null && interiorDoorInteractable != null)
        {
            SerializedObject exteriorSerialized = new(exteriorDoorInteractable);
            SetObject(exteriorSerialized, "pairedDoor", interiorDoorInteractable);
            exteriorSerialized.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject interiorSerialized = new(interiorDoorInteractable);
            SetObject(interiorSerialized, "pairedDoor", exteriorDoorInteractable);
            interiorSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(insideSpawn);
        EditorUtility.SetDirty(outsideSpawn);
    }

    public static void CreateOrReplacePrefab(string path, bool selectAsset)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            path = DefaultPrefabPath;
        }

        path = path.Replace('\\', '/');
        if (!path.StartsWith("Assets/"))
        {
            Debug.LogError("Building prefab path must be inside Assets.");
            return;
        }

        EnsureAssetFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
        GameObject prefabRoot = CreateConfiguredBuildingObject(Path.GetFileNameWithoutExtension(path));
        RetroHybridBuilding building = prefabRoot.GetComponent<RetroHybridBuilding>();
        building.RebuildBuildingNow();
        StripGeneratedChildren(prefabRoot.transform);

        bool success;
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, path, out success);
        Object.DestroyImmediate(prefabRoot);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!success || savedPrefab == null)
        {
            Debug.LogError($"Failed to create building prefab at {path}.");
            return;
        }

        if (selectAsset)
        {
            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);
        }
    }

    public static void ConfigureTextureImporters()
    {
        foreach ((string propertyPath, string assetPath) in DefaultTextureBindings)
        {
            ConfigureTextureImporter(assetPath, propertyPath);
        }

        foreach (var prop in FurnitureProps)
        {
            ConfigurePropTextureImporters(prop.folder, prop.label);
        }

        foreach (var prop in DirtDecals)
        {
            ConfigurePropTextureImporters(prop.folder, prop.label);
        }
    }

    private static void ApplyExampleDefaults(RetroHybridBuilding building)
    {
        SerializedObject serializedBuilding = new(building);
        serializedBuilding.Update();
        SetFloat(serializedBuilding, "width", 6f);
        SetFloat(serializedBuilding, "depth", 4.25f);
        SetFloat(serializedBuilding, "wallHeight", 3.6f);
        SetFloat(serializedBuilding, "roofHeight", 1.05f);
        SetFloat(serializedBuilding, "roofOverhang", 0.22f);
        SetFloat(serializedBuilding, "doorLocalX", 0.92f);
        SetFloat(serializedBuilding, "doorWidth", 1.45f);
        SetFloat(serializedBuilding, "doorHeight", 2.25f);
        SetFloat(serializedBuilding, "doorBottom", 0.03f);
        SetBool(serializedBuilding, "drawDoorSprite", true);
        SetBool(serializedBuilding, "addCollisionWalls", true);
        SetVector3(serializedBuilding, "insideSpawnLocalPosition", new Vector3(0f, 0.08f, 0.82f));
        SetFloat(serializedBuilding, "insideSpawnYaw", 180f);
        SetVector3(serializedBuilding, "outsideSpawnLocalPosition", new Vector3(0.92f, 0.08f, -3.55f));
        SetFloat(serializedBuilding, "outsideSpawnYaw", 180f);
        SetBool(serializedBuilding, "buildInterior", true);
        SetFloat(serializedBuilding, "interiorInset", 0.08f);
        SetFloat(serializedBuilding, "interiorHeight", 3.28f);
        SetBool(serializedBuilding, "drawInteriorDoorSprite", true);
        SetInt(serializedBuilding, "interiorSeed", 48191);
        SetInt(serializedBuilding, "furniturePropCount", 12);
        SetInt(serializedBuilding, "dirtDecalCount", 20);
        serializedBuilding.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(building);
    }

    private static void ConfigureTextureImporter(string assetPath, string propertyPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        string normalizedPath = assetPath.Replace('\\', '/');
        bool isNormal = propertyPath.Contains("normalMap") || normalizedPath.EndsWith("_Normal.png");
        bool isLinear = isNormal || propertyPath.Contains("heightMap") || propertyPath.Contains("packedMasksMap") || normalizedPath.EndsWith("_Height.png") || normalizedPath.EndsWith("_PackedMasks.png");
        bool isBase = propertyPath.Contains("baseMap") || normalizedPath.EndsWith("_Base.png");
        bool hasAlpha = isBase && (normalizedPath.Contains("/Props/") || normalizedPath.Contains("InteriorDoor_Base") || normalizedPath.Contains("_Door_Base"));
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = hasAlpha;
        importer.mipMapsPreserveCoverage = hasAlpha;
        importer.sRGBTexture = !isLinear;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.anisoLevel = 4;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static void ConfigurePropTextureImporters(string folder, string label)
    {
        ConfigureTextureImporter(BuildPropMapPath(folder, label, "Base"), "maps.baseMap");
        ConfigureTextureImporter(BuildPropMapPath(folder, label, "Normal"), "maps.normalMap");
        ConfigureTextureImporter(BuildPropMapPath(folder, label, "Height"), "maps.heightMap");
        ConfigureTextureImporter(BuildPropMapPath(folder, label, "PackedMasks"), "maps.packedMasksMap");
        ConfigureTextureImporter(BuildPropMapPath(folder, label, "Emission"), "maps.emissionMap");
    }

    private static void AssignFurniturePropLibrary(SerializedObject serializedBuilding)
    {
        SerializedProperty library = serializedBuilding.FindProperty("furniturePropLibrary");
        if (library == null)
        {
            return;
        }

        library.arraySize = FurnitureProps.Length;
        for (int i = 0; i < FurnitureProps.Length; i++)
        {
            (string label, string folder, Vector2 size, Vector2 scaleRange, bool preferWall, bool castShadow) = FurnitureProps[i];
            SerializedProperty element = library.GetArrayElementAtIndex(i);
            AssignPropDefinition(element, label, folder, size, scaleRange, false, preferWall, castShadow);
        }
    }

    private static void AssignDirtDecalLibrary(SerializedObject serializedBuilding)
    {
        SerializedProperty library = serializedBuilding.FindProperty("dirtDecalLibrary");
        if (library == null)
        {
            return;
        }

        library.arraySize = DirtDecals.Length;
        for (int i = 0; i < DirtDecals.Length; i++)
        {
            (string label, string folder, Vector2 size, Vector2 scaleRange) = DirtDecals[i];
            SerializedProperty element = library.GetArrayElementAtIndex(i);
            AssignPropDefinition(element, label, folder, size, scaleRange, true, false, false);
        }
    }

    private static void AssignPropDefinition(
        SerializedProperty element,
        string label,
        string folder,
        Vector2 size,
        Vector2 scaleRange,
        bool floorDecal,
        bool preferWall,
        bool castShadow)
    {
        SetString(element, "label", label);
        SetVector2(element, "baseSize", size);
        SetVector2(element, "scaleRange", scaleRange);
        SetBool(element, "floorDecal", floorDecal);
        SetBool(element, "preferWallPlacement", preferWall);
        SetBool(element, "castShadow", castShadow);
        SetObject(element, "maps.baseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BuildPropMapPath(folder, label, "Base")));
        SetObject(element, "maps.normalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BuildPropMapPath(folder, label, "Normal")));
        SetObject(element, "maps.heightMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BuildPropMapPath(folder, label, "Height")));
        SetObject(element, "maps.packedMasksMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BuildPropMapPath(folder, label, "PackedMasks")));
        SetObject(element, "maps.emissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BuildPropMapPath(folder, label, "Emission")));
    }

    private static string BuildPropMapPath(string folder, string label, string suffix)
    {
        return $"{folder}/{label}_{suffix}.png";
    }

    private static void RebuildSelectedBuildings()
    {
        bool rebuiltAny = false;
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            if (selectedObject == null)
            {
                continue;
            }

            RetroHybridBuilding[] buildings = selectedObject.GetComponentsInChildren<RetroHybridBuilding>(true);
            foreach (RetroHybridBuilding building in buildings)
            {
                RebuildBuilding(building);
                rebuiltAny = true;
            }
        }

        if (!rebuiltAny)
        {
            Debug.LogWarning("Select a RetroHybridBuilding object first.");
        }
    }

    private static void DrawAssetStatus(string title, (string propertyPath, string assetPath)[] bindings)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        foreach ((string propertyPath, string assetPath) in bindings)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(propertyPath, GUILayout.Width(190f));
                EditorGUILayout.ObjectField(asset, typeof(Object), false);
            }
        }
    }

    private static Transform FindOrCreateChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            return existing;
        }

        GameObject childObject = new(childName);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private static RetroBuildingDoorInteractable ConfigureDoorInteraction(
        Transform door,
        RetroHybridBuilding building,
        RetroBuildingDoorSide doorSide,
        Vector3 localCenter,
        Vector3 colliderSize,
        Transform target,
        Transform npcApproachTarget,
        string verb,
        string message)
    {
        if (door == null)
        {
            return null;
        }

        door.localPosition = localCenter;
        door.localRotation = Quaternion.identity;
        door.localScale = Vector3.one;

        BoxCollider doorCollider = GetOrAddComponent<BoxCollider>(door.gameObject);
        doorCollider.isTrigger = true;
        doorCollider.center = Vector3.zero;
        doorCollider.size = colliderSize;

        RetroBuildingDoorInteractable interactable = GetOrAddComponent<RetroBuildingDoorInteractable>(door.gameObject);
        SerializedObject serializedDoor = new(interactable);
        serializedDoor.Update();
        SetObject(serializedDoor, "building", building);
        SetEnum(serializedDoor, "doorSide", (int)doorSide);
        SetObject(serializedDoor, "teleportTarget", target);
        SetObject(serializedDoor, "npcApproachTarget", npcApproachTarget);
        SetBool(serializedDoor, "allowNpcUse", true);
        SetBool(serializedDoor, "allowNpcUseWhenLocked", true);
        SetEnum(serializedDoor, "lockMode", (int)RetroBuildingDoorLockMode.None);
        SetBool(serializedDoor, "locked", false);
        SetString(serializedDoor, "interactionName", "settlement house");
        SetString(serializedDoor, "interactionVerb", verb);
        SetFloat(serializedDoor, "interactionMaxDistance", 3.35f);
        SetInt(serializedDoor, "interactionPriority", 35);
        SetString(serializedDoor, "enteredMessage", message);
        serializedDoor.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(door);
        EditorUtility.SetDirty(doorCollider);
        EditorUtility.SetDirty(interactable);
        return interactable;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        return target.AddComponent<T>();
    }

    private static void StripGeneratedChildren(Transform root)
    {
        Transform generated = root.Find(GeneratedRootName);
        if (generated != null)
        {
            Object.DestroyImmediate(generated.gameObject);
        }
    }

    private static void EnsureAssetFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        EnsureAssetFolder(parent);
        string leaf = Path.GetFileName(folder);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf) && !AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }

    private static void SetObject(SerializedObject target, string propertyName, Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetObject(SerializedProperty target, string relativePropertyName, Object value)
    {
        SerializedProperty property = target.FindPropertyRelative(relativePropertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetString(SerializedObject target, string propertyName, string value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }

    private static void SetString(SerializedProperty target, string relativePropertyName, string value)
    {
        SerializedProperty property = target.FindPropertyRelative(relativePropertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }

    private static void SetBool(SerializedObject target, string propertyName, bool value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetBool(SerializedProperty target, string relativePropertyName, bool value)
    {
        SerializedProperty property = target.FindPropertyRelative(relativePropertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetInt(SerializedObject target, string propertyName, int value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetEnum(SerializedObject target, string propertyName, int value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.enumValueIndex = value;
        }
    }

    private static void SetFloat(SerializedObject target, string propertyName, float value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetVector3(SerializedObject target, string propertyName, Vector3 value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.vector3Value = value;
        }
    }

    private static void SetVector2(SerializedProperty target, string relativePropertyName, Vector2 value)
    {
        SerializedProperty property = target.FindPropertyRelative(relativePropertyName);
        if (property != null)
        {
            property.vector2Value = value;
        }
    }
}

[CustomEditor(typeof(RetroHybridBuilding))]
public sealed class RetroHybridBuildingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Builder", EditorStyles.boldLabel);
        if (GUILayout.Button("Assign Default Maps"))
        {
            foreach (Object targetObject in targets)
            {
                RetroHybridBuildingBuilderWindow.AssignDefaultMaps((RetroHybridBuilding)targetObject);
            }
        }

        if (GUILayout.Button("Sync Door And Spawn"))
        {
            foreach (Object targetObject in targets)
            {
                RetroHybridBuildingBuilderWindow.SyncDoorAndSpawn((RetroHybridBuilding)targetObject);
            }
        }

        if (GUILayout.Button("Build / Rebuild Building Now"))
        {
            foreach (Object targetObject in targets)
            {
                RetroHybridBuildingBuilderWindow.RebuildBuilding((RetroHybridBuilding)targetObject);
            }
        }

        if (GUILayout.Button("Create / Replace Default Prefab Asset"))
        {
            RetroHybridBuildingBuilderWindow.CreateOrReplacePrefab("Assets/Buildings/SettlementHouse/Prefabs/SettlementHouse.prefab", true);
        }
    }
}
