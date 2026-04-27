using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class RetroVegetationBuilderWindow : EditorWindow
{
    private const string GroundPrefabPath = "Assets/_Project/Content/World/Nature/Vegetation/Prefabs/GroundVegetationPatch.prefab";
    private const string BushPrefabPath = "Assets/_Project/Content/World/Nature/Vegetation/Prefabs/BushVegetationPatch.prefab";
    private const string MixedPrefabPath = "Assets/_Project/Content/World/Nature/Vegetation/Prefabs/MixedVegetationPatch.prefab";
    private const string DiverseTreePrefabPath = "Assets/_Project/Content/World/Nature/Trees/HybridTree/Prefabs/HybridTreeDiverse.prefab";

    private static readonly (string propertyName, string assetPath)[] GroundTextureBindings =
    {
        ("baseMap", "Assets/_Project/Content/World/Nature/Vegetation/Ground/Textures/GroundVegetation_Base.png"),
        ("normalMap", "Assets/_Project/Content/World/Nature/Vegetation/Ground/Textures/GroundVegetation_Normal.png"),
        ("depthMap", "Assets/_Project/Content/World/Nature/Vegetation/Ground/Textures/GroundVegetation_Depth.png"),
        ("thicknessMap", "Assets/_Project/Content/World/Nature/Vegetation/Ground/Textures/GroundVegetation_Thickness.png"),
        ("densityMap", "Assets/_Project/Content/World/Nature/Vegetation/Ground/Textures/GroundVegetation_Density.png"),
        ("windMap", "Assets/_Project/Content/World/Nature/Vegetation/Ground/Textures/GroundVegetation_Wind.png")
    };

    private static readonly (string propertyName, string assetPath)[] BushTextureBindings =
    {
        ("baseMap", "Assets/_Project/Content/World/Nature/Vegetation/Bush/Textures/BushVegetation_Base.png"),
        ("normalMap", "Assets/_Project/Content/World/Nature/Vegetation/Bush/Textures/BushVegetation_Normal.png"),
        ("depthMap", "Assets/_Project/Content/World/Nature/Vegetation/Bush/Textures/BushVegetation_Depth.png"),
        ("thicknessMap", "Assets/_Project/Content/World/Nature/Vegetation/Bush/Textures/BushVegetation_Thickness.png"),
        ("densityMap", "Assets/_Project/Content/World/Nature/Vegetation/Bush/Textures/BushVegetation_Density.png"),
        ("windMap", "Assets/_Project/Content/World/Nature/Vegetation/Bush/Textures/BushVegetation_Wind.png")
    };

    private static readonly (string propertyName, string assetPath)[] DiverseTreeLeafBindings =
    {
        ("leafBaseMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/Variants/HybridTree_Leaves_Diverse_Base.png"),
        ("leafNormalMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/Variants/HybridTree_Leaves_Diverse_Normal.png"),
        ("leafDepthMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/Variants/HybridTree_Leaves_Diverse_Depth.png"),
        ("leafThicknessMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/Variants/HybridTree_Leaves_Diverse_Thickness.png"),
        ("leafDensityMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/Variants/HybridTree_Leaves_Diverse_Density.png"),
        ("leafWindMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/Variants/HybridTree_Leaves_Diverse_Wind.png")
    };

    private static readonly (string basePath, string normalPath)[] BarkVariants =
    {
        ("Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/Variants/HybridTree_Bark_Dark_Base.png", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/Variants/HybridTree_Bark_Dark_Normal.png"),
        ("Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/Variants/HybridTree_Bark_Pale_Base.png", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/Variants/HybridTree_Bark_Pale_Normal.png"),
        ("Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/Variants/HybridTree_Bark_Mossy_Base.png", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/Variants/HybridTree_Bark_Mossy_Normal.png"),
        ("Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/Variants/HybridTree_Bark_Redwood_Base.png", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/Variants/HybridTree_Bark_Redwood_Normal.png")
    };

    private Vector2 scroll;
    private bool selectCreatedAsset = true;
    private int clusterPatchCount = 18;
    private int clusterTreeCount = 5;
    private float clusterRadius = 14f;

    [MenuItem("Tools/Ultraloud/Nature/Vegetation Builder")]
    public static void Open()
    {
        RetroVegetationBuilderWindow window = GetWindow<RetroVegetationBuilderWindow>("Vegetation");
        window.minSize = new Vector2(500f, 390f);
    }

    [MenuItem("GameObject/Ultraloud/Nature/Ground Vegetation Patch", false, 14)]
    public static void CreateSceneGroundPatch(MenuCommand command)
    {
        CreateScenePatch(command, RetroVegetationPatchKind.GroundCover, "GroundVegetationPatch");
    }

    [MenuItem("GameObject/Ultraloud/Nature/Bush Vegetation Patch", false, 15)]
    public static void CreateSceneBushPatch(MenuCommand command)
    {
        CreateScenePatch(command, RetroVegetationPatchKind.Bush, "BushVegetationPatch");
    }

    [MenuItem("GameObject/Ultraloud/Nature/Mixed Vegetation Patch", false, 16)]
    public static void CreateSceneMixedPatch(MenuCommand command)
    {
        CreateScenePatch(command, RetroVegetationPatchKind.Mixed, "MixedVegetationPatch");
    }

    [MenuItem("GameObject/Ultraloud/Nature/Random Hybrid Tree Variant", false, 17)]
    public static void CreateSceneRandomTree(MenuCommand command)
    {
        GameObject tree = CreateDiverseTreeObject("HybridTreeDiverse", Random.Range(1000, 999999));
        PlaceCreatedObject(command, tree, "Create Hybrid Tree Variant");
        RetroHybridTree hybridTree = tree.GetComponent<RetroHybridTree>();
        hybridTree.RebuildTreeNow();
    }

    [MenuItem("Assets/Create/Ultraloud/Nature/Vegetation Prefab Pack")]
    public static void CreatePrefabPackFromAssetsMenu()
    {
        CreateOrReplacePrefabPack(true);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        using (EditorGUILayout.ScrollViewScope scope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scope.scrollPosition;
            EditorGUILayout.LabelField("Vegetation Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Builds procedural ground vegetation, bushes, mixed patches, and diverse hybrid-tree variants from generated atlases. Patches use randomized mesh cards, atlas-cell variation, HDRP foliage maps, manual scene-light sampling, and wind animation.", MessageType.Info);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ground Patch", GUILayout.Height(30f)))
                {
                    CreateSceneGroundPatch(new MenuCommand(Selection.activeGameObject));
                }

                if (GUILayout.Button("Bush Patch", GUILayout.Height(30f)))
                {
                    CreateSceneBushPatch(new MenuCommand(Selection.activeGameObject));
                }

                if (GUILayout.Button("Mixed Patch", GUILayout.Height(30f)))
                {
                    CreateSceneMixedPatch(new MenuCommand(Selection.activeGameObject));
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Random Tree Variant", GUILayout.Height(30f)))
                {
                    CreateSceneRandomTree(new MenuCommand(Selection.activeGameObject));
                }

                if (GUILayout.Button("Create / Replace Prefab Pack", GUILayout.Height(30f)))
                {
                    CreateOrReplacePrefabPack(selectCreatedAsset);
                }
            }

            EditorGUILayout.Space(8f);
            selectCreatedAsset = EditorGUILayout.Toggle("Select Created Asset", selectCreatedAsset);
            clusterPatchCount = EditorGUILayout.IntSlider("Cluster Patches", clusterPatchCount, 1, 80);
            clusterTreeCount = EditorGUILayout.IntSlider("Cluster Trees", clusterTreeCount, 0, 24);
            clusterRadius = EditorGUILayout.Slider("Cluster Radius", clusterRadius, 3f, 60f);
            if (GUILayout.Button("Create Procedural Vegetation Cluster", GUILayout.Height(30f)))
            {
                GameObject cluster = CreateVegetationCluster("VegetationCluster", clusterPatchCount, clusterTreeCount, clusterRadius);
                Undo.RegisterCreatedObjectUndo(cluster, "Create Vegetation Cluster");
                Selection.activeGameObject = cluster;
                EditorSceneManager.MarkSceneDirty(cluster.scene);
            }

            EditorGUILayout.Space(10f);
            DrawAssetStatus("Vegetation Prefabs", new[] { GroundPrefabPath, BushPrefabPath, MixedPrefabPath, DiverseTreePrefabPath });
            DrawTextureStatus("Ground Maps", GroundTextureBindings);
            DrawTextureStatus("Bush Maps", BushTextureBindings);
            DrawTextureStatus("Tree Leaf Variant Maps", DiverseTreeLeafBindings);
            DrawBarkStatus();
        }
    }

    public static void CreateOrReplacePrefabPack(bool selectAsset)
    {
        ConfigureTextureImporters();
        EnsureAssetFolder(Path.GetDirectoryName(GroundPrefabPath)?.Replace('\\', '/'));
        EnsureAssetFolder(Path.GetDirectoryName(DiverseTreePrefabPath)?.Replace('\\', '/'));

        GameObject ground = CreateConfiguredPatchObject("GroundVegetationPatch", RetroVegetationPatchKind.GroundCover, 7151);
        SavePrefab(ground, GroundPrefabPath);

        GameObject bush = CreateConfiguredPatchObject("BushVegetationPatch", RetroVegetationPatchKind.Bush, 8167);
        SavePrefab(bush, BushPrefabPath);

        GameObject mixed = CreateConfiguredPatchObject("MixedVegetationPatch", RetroVegetationPatchKind.Mixed, 9271);
        SavePrefab(mixed, MixedPrefabPath);

        GameObject tree = CreateDiverseTreeObject("HybridTreeDiverse", 31277);
        SavePrefab(tree, DiverseTreePrefabPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (selectAsset)
        {
            Object selected = AssetDatabase.LoadAssetAtPath<Object>(MixedPrefabPath);
            Selection.activeObject = selected;
            EditorGUIUtility.PingObject(selected);
        }
    }

    public static GameObject CreateConfiguredPatchObject(string objectName, RetroVegetationPatchKind kind, int seed)
    {
        ConfigureTextureImporters();
        GameObject patchObject = new GameObject(objectName);
        RetroVegetationPatch patch = patchObject.AddComponent<RetroVegetationPatch>();
        AssignPatchDefaults(patch, kind, seed);
        patch.RebuildVegetationNow();
        return patchObject;
    }

    public static GameObject CreateDiverseTreeObject(string objectName, int seed)
    {
        ConfigureTextureImporters();
        GameObject treeObject = RetroHybridTreeBuilderWindow.CreateConfiguredTreeObject(objectName);
        ApplyDiverseTreeVariation(treeObject.GetComponent<RetroHybridTree>(), seed);
        RetroShootablePrefabUtility.ConfigureHybridTree(treeObject);
        return treeObject;
    }

    public static void RebuildPatch(RetroVegetationPatch patch)
    {
        if (patch == null)
        {
            return;
        }

        ConfigureTextureImporters();
        patch.RebuildVegetationNow();
        EditorUtility.SetDirty(patch);
        if (!Application.isPlaying && patch.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(patch.gameObject.scene);
        }
    }

    private static void CreateScenePatch(MenuCommand command, RetroVegetationPatchKind kind, string objectName)
    {
        GameObject patch = CreateConfiguredPatchObject(objectName, kind, Random.Range(1000, 999999));
        PlaceCreatedObject(command, patch, $"Create {objectName}");
    }

    private static void PlaceCreatedObject(MenuCommand command, GameObject createdObject, string undoName)
    {
        GameObject parent = command.context as GameObject;
        if (parent != null)
        {
            Undo.SetTransformParent(createdObject.transform, parent.transform, undoName);
            createdObject.transform.localPosition = Vector3.zero;
            createdObject.transform.localRotation = Quaternion.identity;
        }
        else if (SceneView.lastActiveSceneView != null)
        {
            createdObject.transform.position = SceneView.lastActiveSceneView.pivot;
        }

        Undo.RegisterCreatedObjectUndo(createdObject, undoName);
        Selection.activeGameObject = createdObject;
        EditorSceneManager.MarkSceneDirty(createdObject.scene);
    }

    private static void AssignPatchDefaults(RetroVegetationPatch patch, RetroVegetationPatchKind kind, int seed)
    {
        if (patch == null)
        {
            return;
        }

        SerializedObject serializedPatch = new SerializedObject(patch);
        serializedPatch.Update();
        (string propertyName, string assetPath)[] bindings = kind == RetroVegetationPatchKind.GroundCover
            ? GroundTextureBindings
            : BushTextureBindings;
        foreach ((string propertyName, string assetPath) in bindings)
        {
            SetObject(serializedPatch, propertyName, AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath));
        }

        SetInt(serializedPatch, "atlasColumns", 4);
        SetInt(serializedPatch, "atlasRows", 4);
        SetInt(serializedPatch, "seed", seed);
        SetEnum(serializedPatch, "patchKind", (int)kind);

        switch (kind)
        {
            case RetroVegetationPatchKind.GroundCover:
                SetInt(serializedPatch, "cardCount", 84);
                SetFloat(serializedPatch, "patchRadius", 3.1f);
                SetFloat(serializedPatch, "patchHeight", 0.85f);
                SetFloat(serializedPatch, "minCardSize", 0.32f);
                SetFloat(serializedPatch, "maxCardSize", 0.9f);
                SetFloat(serializedPatch, "horizontalCardRatio", 0.62f);
                SetFloat(serializedPatch, "centerDensity", 0.52f);
                SetColor(serializedPatch, "tint", new Color(0.92f, 0.98f, 0.86f, 1f));
                SetFloat(serializedPatch, "windStrength", 0.08f);
                break;
            case RetroVegetationPatchKind.Bush:
                SetInt(serializedPatch, "cardCount", 128);
                SetFloat(serializedPatch, "patchRadius", 2.35f);
                SetFloat(serializedPatch, "patchHeight", 1.65f);
                SetFloat(serializedPatch, "minCardSize", 0.46f);
                SetFloat(serializedPatch, "maxCardSize", 1.28f);
                SetFloat(serializedPatch, "horizontalCardRatio", 0.08f);
                SetFloat(serializedPatch, "centerDensity", 0.82f);
                SetColor(serializedPatch, "tint", new Color(0.92f, 0.96f, 0.82f, 1f));
                SetFloat(serializedPatch, "windStrength", 0.1f);
                break;
            default:
                SetInt(serializedPatch, "cardCount", 148);
                SetFloat(serializedPatch, "patchRadius", 3.8f);
                SetFloat(serializedPatch, "patchHeight", 1.45f);
                SetFloat(serializedPatch, "minCardSize", 0.35f);
                SetFloat(serializedPatch, "maxCardSize", 1.15f);
                SetFloat(serializedPatch, "horizontalCardRatio", 0.34f);
                SetFloat(serializedPatch, "centerDensity", 0.68f);
                SetColor(serializedPatch, "tint", new Color(0.95f, 0.98f, 0.88f, 1f));
                SetFloat(serializedPatch, "windStrength", 0.11f);
                break;
        }

        SetFloat(serializedPatch, "windSpeed", 1.6f);
        SetFloat(serializedPatch, "gustVariation", 0.52f);
        SetFloat(serializedPatch, "alphaCutoff", 0.08f);
        SetFloat(serializedPatch, "coverageSoftness", 0.045f);
        SetFloat(serializedPatch, "normalScale", 1f);
        SetFloat(serializedPatch, "normalBend", 0.32f);
        SetFloat(serializedPatch, "wrapDiffuse", 0.58f);
        SetFloat(serializedPatch, "densityShadowStrength", 0.7f);
        SetFloat(serializedPatch, "depthSelfShadowStrength", 0.3f);
        SetFloat(serializedPatch, "surfaceRoughness", 0.84f);
        SetFloat(serializedPatch, "specularStrength", 0.1f);
        SetFloat(serializedPatch, "rimStrength", 0.13f);
        SetFloat(serializedPatch, "ambientIntensity", 1.05f);
        serializedPatch.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(patch);
    }

    private static void ApplyDiverseTreeVariation(RetroHybridTree tree, int seed)
    {
        if (tree == null)
        {
            return;
        }

        System.Random random = new System.Random(seed);
        SerializedObject serializedTree = new SerializedObject(tree);
        serializedTree.Update();

        foreach ((string propertyName, string assetPath) in DiverseTreeLeafBindings)
        {
            SetObject(serializedTree, propertyName, AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath));
        }

        int barkIndex = Mathf.Clamp((int)(random.NextDouble() * BarkVariants.Length), 0, BarkVariants.Length - 1);
        SetObject(serializedTree, "barkBaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BarkVariants[barkIndex].basePath));
        SetObject(serializedTree, "barkNormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BarkVariants[barkIndex].normalPath));

        float height = Lerp(random, 4.6f, 7.2f);
        SetFloat(serializedTree, "treeHeight", height);
        SetFloat(serializedTree, "trunkHeight", height * Lerp(random, 0.46f, 0.66f));
        SetFloat(serializedTree, "trunkRadius", Lerp(random, 0.16f, 0.32f));
        SetFloat(serializedTree, "branchRadius", Lerp(random, 0.045f, 0.095f));
        SetInt(serializedTree, "branchCount", Mathf.RoundToInt(Lerp(random, 7f, 16f)));
        SetFloat(serializedTree, "canopyRadius", Lerp(random, 1.45f, 2.75f));
        SetFloat(serializedTree, "canopyHeight", Lerp(random, 1.55f, 3.15f));
        SetInt(serializedTree, "leafCardCount", Mathf.RoundToInt(Lerp(random, 82f, 172f)));
        SetFloat(serializedTree, "leafCardMinSize", Lerp(random, 0.62f, 0.94f));
        SetFloat(serializedTree, "leafCardMaxSize", Lerp(random, 1.1f, 1.82f));
        SetInt(serializedTree, "seed", seed);
        SetFloat(serializedTree, "windStrength", Lerp(random, 0.11f, 0.24f));
        SetFloat(serializedTree, "windSpeed", Lerp(random, 1.0f, 1.85f));
        SetColor(serializedTree, "leafTint", Color.Lerp(new Color(0.82f, 0.94f, 0.72f, 1f), new Color(1.06f, 0.94f, 0.72f, 1f), (float)random.NextDouble()));
        SetFloat(serializedTree, "transmissionStrength", Lerp(random, 0.55f, 1.08f));
        SetBool(serializedTree, "enableImpostorLod", false);
        serializedTree.ApplyModifiedPropertiesWithoutUndo();
        tree.RebuildTreeNow();
        EditorUtility.SetDirty(tree);
    }

    private static GameObject CreateVegetationCluster(string objectName, int patchCount, int treeCount, float radius)
    {
        ConfigureTextureImporters();
        GameObject root = new GameObject(objectName);
        System.Random random = new System.Random(Random.Range(1, 999999));
        int safePatchCount = Mathf.Max(1, patchCount);
        for (int i = 0; i < safePatchCount; i++)
        {
            RetroVegetationPatchKind kind = (RetroVegetationPatchKind)(i % 3);
            if (random.NextDouble() > 0.56)
            {
                kind = RetroVegetationPatchKind.Mixed;
            }

            GameObject patch = CreateConfiguredPatchObject($"VegetationPatch_{i:00}", kind, random.Next(1000, 999999));
            patch.transform.SetParent(root.transform, false);
            patch.transform.localPosition = SampleClusterPosition(random, radius);
            patch.transform.localRotation = Quaternion.Euler(0f, Lerp(random, 0f, 360f), 0f);
            patch.transform.localScale = Vector3.one * Lerp(random, 0.72f, 1.42f);
        }

        for (int i = 0; i < Mathf.Max(0, treeCount); i++)
        {
            GameObject tree = CreateDiverseTreeObject($"HybridTreeVariant_{i:00}", random.Next(1000, 999999));
            tree.transform.SetParent(root.transform, false);
            tree.transform.localPosition = SampleClusterPosition(random, radius);
            tree.transform.localRotation = Quaternion.Euler(0f, Lerp(random, 0f, 360f), 0f);
            tree.transform.localScale = Vector3.one * Lerp(random, 0.22f, 0.42f);
        }

        return root;
    }

    private static Vector3 SampleClusterPosition(System.Random random, float radius)
    {
        float angle = Lerp(random, 0f, Mathf.PI * 2f);
        float distance = Mathf.Sqrt((float)random.NextDouble()) * Mathf.Max(0.1f, radius);
        return new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
    }

    private static void SavePrefab(GameObject prefabRoot, string path)
    {
        EnsureAssetFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
        StripGeneratedChildren(prefabRoot.transform);
        bool success;
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, path, out success);
        Object.DestroyImmediate(prefabRoot);
        if (!success || savedPrefab == null)
        {
            Debug.LogError($"Failed to create vegetation prefab at {path}.");
        }
    }

    private static void StripGeneratedChildren(Transform root)
    {
        Transform vegetationGenerated = root.Find("__VegetationPatchGenerated");
        if (vegetationGenerated != null)
        {
            Object.DestroyImmediate(vegetationGenerated.gameObject);
        }

        Transform treeGenerated = root.Find("__HybridTreeGenerated");
        if (treeGenerated != null)
        {
            Object.DestroyImmediate(treeGenerated.gameObject);
        }
    }

    public static void ConfigureTextureImporters()
    {
        foreach ((string propertyName, string assetPath) in GroundTextureBindings)
        {
            ConfigureTextureImporter(assetPath, propertyName);
        }

        foreach ((string propertyName, string assetPath) in BushTextureBindings)
        {
            ConfigureTextureImporter(assetPath, propertyName);
        }

        foreach ((string propertyName, string assetPath) in DiverseTreeLeafBindings)
        {
            ConfigureTextureImporter(assetPath, propertyName);
        }

        foreach ((string basePath, string normalPath) in BarkVariants)
        {
            ConfigureTextureImporter(basePath, "barkBaseMap");
            ConfigureTextureImporter(normalPath, "barkNormalMap");
        }
    }

    private static void ConfigureTextureImporter(string assetPath, string propertyName)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        bool isNormal = propertyName.Contains("normal", System.StringComparison.OrdinalIgnoreCase);
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = !isNormal;
        importer.mipMapsPreserveCoverage = !isNormal;
        importer.sRGBTexture = !isNormal && !propertyName.Contains("depth", System.StringComparison.OrdinalIgnoreCase)
            && !propertyName.Contains("density", System.StringComparison.OrdinalIgnoreCase)
            && !propertyName.Contains("thickness", System.StringComparison.OrdinalIgnoreCase)
            && !propertyName.Contains("wind", System.StringComparison.OrdinalIgnoreCase);
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.anisoLevel = 4;
        importer.wrapMode = propertyName.StartsWith("bark", System.StringComparison.OrdinalIgnoreCase)
            ? TextureWrapMode.Repeat
            : TextureWrapMode.Clamp;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static void DrawTextureStatus(string title, (string propertyName, string assetPath)[] bindings)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        foreach ((string propertyName, string assetPath) in bindings)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(propertyName, GUILayout.Width(145f));
                EditorGUILayout.ObjectField(asset, typeof(Object), false);
            }
        }
    }

    private static void DrawBarkStatus()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Tree Bark Variants", EditorStyles.boldLabel);
        foreach ((string basePath, string normalPath) in BarkVariants)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(AssetDatabase.LoadAssetAtPath<Object>(basePath), typeof(Object), false);
                EditorGUILayout.ObjectField(AssetDatabase.LoadAssetAtPath<Object>(normalPath), typeof(Object), false);
            }
        }
    }

    private static void DrawAssetStatus(string title, string[] assetPaths)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        foreach (string assetPath in assetPaths)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(assetPath, GUILayout.Width(350f));
                EditorGUILayout.ObjectField(asset, typeof(Object), false);
            }
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

    private static float Lerp(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private static void SetObject(SerializedObject target, string propertyName, Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
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

    private static void SetEnum(SerializedObject target, string propertyName, int value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.enumValueIndex = value;
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

    private static void SetFloat(SerializedObject target, string propertyName, float value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetColor(SerializedObject target, string propertyName, Color value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.colorValue = value;
        }
    }
}

[CustomEditor(typeof(RetroVegetationPatch))]
public sealed class RetroVegetationPatchEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Builder", EditorStyles.boldLabel);
        if (GUILayout.Button("Build / Rebuild Vegetation Now"))
        {
            foreach (Object targetObject in targets)
            {
                RetroVegetationBuilderWindow.RebuildPatch((RetroVegetationPatch)targetObject);
            }
        }

        if (GUILayout.Button("Create / Replace Vegetation Prefab Pack"))
        {
            RetroVegetationBuilderWindow.CreateOrReplacePrefabPack(true);
        }
    }
}
