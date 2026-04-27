using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class RetroHybridTreeBuilderWindow : EditorWindow
{
    private const string DefaultPrefabPath = "Assets/_Project/Content/World/Nature/Trees/HybridTree/Prefabs/HybridTree.prefab";
    private const string GeneratedRootName = "__HybridTreeGenerated";

    private static readonly (string propertyName, string assetPath)[] DefaultTextureBindings =
    {
        ("leafBaseMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/HybridTree_Leaves_Base.png"),
        ("leafNormalMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/HybridTree_Leaves_Normal.png"),
        ("leafDepthMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/HybridTree_Leaves_Depth.png"),
        ("leafThicknessMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/HybridTree_Leaves_Thickness.png"),
        ("leafDensityMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/HybridTree_Leaves_Density.png"),
        ("leafWindMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/HybridTree_Leaves_Wind.png"),
        ("barkBaseMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/HybridTree_Bark_Base.png"),
        ("barkNormalMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/HybridTree_Bark_Normal.png"),
        ("impostorBaseMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/HybridTree_Impostor_Base.png"),
        ("impostorNormalMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/HybridTree_Impostor_Normal.png"),
        ("impostorDepthMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/HybridTree_Impostor_Depth.png"),
        ("impostorThicknessMap", "Assets/_Project/Content/World/Nature/Trees/HybridTree/Textures/HybridTree_Impostor_Thickness.png")
    };

    private Vector2 scroll;
    private string prefabPath = DefaultPrefabPath;
    private bool selectCreatedObject = true;

    [MenuItem("Tools/Ultraloud/Nature/Hybrid Tree Builder")]
    public static void Open()
    {
        RetroHybridTreeBuilderWindow window = GetWindow<RetroHybridTreeBuilderWindow>("Hybrid Tree");
        window.minSize = new Vector2(440f, 280f);
    }

    [MenuItem("GameObject/Ultraloud/Nature/Hybrid Tree", false, 10)]
    public static void CreateSceneTree(MenuCommand command)
    {
        GameObject treeObject = CreateConfiguredTreeObject("HybridTree");
        GameObject parent = command.context as GameObject;
        if (parent != null)
        {
            Undo.SetTransformParent(treeObject.transform, parent.transform, "Create Hybrid Tree");
            treeObject.transform.localPosition = Vector3.zero;
            treeObject.transform.localRotation = Quaternion.identity;
        }

        Undo.RegisterCreatedObjectUndo(treeObject, "Create Hybrid Tree");
        RetroHybridTree tree = treeObject.GetComponent<RetroHybridTree>();
        tree.RebuildTreeNow();
        Selection.activeGameObject = treeObject;
        EditorSceneManager.MarkSceneDirty(treeObject.scene);
    }

    [MenuItem("Assets/Create/Ultraloud/Nature/Hybrid Tree Prefab")]
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
            EditorGUILayout.LabelField("Hybrid Tree Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Creates a clean RetroHybridTree object with generated maps, shootable collision, wood/leaf impact feedback, and non-saved procedural render cache children.", MessageType.Info);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Tree In Scene", GUILayout.Height(32f)))
                {
                    CreateSceneTree(new MenuCommand(Selection.activeGameObject));
                }

                if (GUILayout.Button("Rebuild Selected", GUILayout.Height(32f)))
                {
                    RebuildSelectedTrees();
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
            EditorGUILayout.LabelField("Default Map Check", EditorStyles.boldLabel);
            foreach ((string propertyName, string assetPath) in DefaultTextureBindings)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(propertyName, GUILayout.Width(170f));
                    EditorGUILayout.ObjectField(texture, typeof(Texture2D), false);
                }
            }
        }
    }

    public static void AssignDefaultMaps(RetroHybridTree tree)
    {
        if (tree == null)
        {
            return;
        }

        SerializedObject serializedTree = new(tree);
        serializedTree.Update();
        foreach ((string propertyName, string assetPath) in DefaultTextureBindings)
        {
            SerializedProperty property = serializedTree.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"RetroHybridTree is missing serialized property '{propertyName}'.", tree);
                continue;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                Debug.LogWarning($"Hybrid tree map is missing at {assetPath}.", tree);
                continue;
            }

            property.objectReferenceValue = texture;
        }

        serializedTree.ApplyModifiedProperties();
        EditorUtility.SetDirty(tree);
    }

    public static void RebuildTree(RetroHybridTree tree)
    {
        if (tree == null)
        {
            return;
        }

        AssignDefaultMaps(tree);
        RetroShootablePrefabUtility.ConfigureHybridTree(tree.gameObject);
        tree.RebuildTreeNow();
        EditorUtility.SetDirty(tree);
        if (!Application.isPlaying && tree.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(tree.gameObject.scene);
        }
    }

    public static GameObject CreateConfiguredTreeObject(string objectName)
    {
        GameObject treeObject = new(objectName);
        RetroHybridTree tree = treeObject.AddComponent<RetroHybridTree>();
        AssignDefaultMaps(tree);
        RetroShootablePrefabUtility.ConfigureHybridTree(treeObject);
        return treeObject;
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
            Debug.LogError("Hybrid tree prefab path must be inside Assets.");
            return;
        }

        EnsureAssetFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));

        GameObject prefabRoot = CreateConfiguredTreeObject(Path.GetFileNameWithoutExtension(path));
        RetroHybridTree tree = prefabRoot.GetComponent<RetroHybridTree>();
        RetroShootablePrefabUtility.ConfigureHybridTree(prefabRoot);
        tree.RebuildTreeNow();
        StripGeneratedChildren(prefabRoot.transform);

        bool success;
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, path, out success);
        Object.DestroyImmediate(prefabRoot);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!success || savedPrefab == null)
        {
            Debug.LogError($"Failed to create hybrid tree prefab at {path}.");
            return;
        }

        if (selectAsset)
        {
            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);
        }
    }

    private static void RebuildSelectedTrees()
    {
        bool rebuiltAny = false;
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            if (selectedObject == null)
            {
                continue;
            }

            RetroHybridTree[] trees = selectedObject.GetComponentsInChildren<RetroHybridTree>(true);
            foreach (RetroHybridTree tree in trees)
            {
                RebuildTree(tree);
                rebuiltAny = true;
            }
        }

        if (!rebuiltAny)
        {
            Debug.LogWarning("Select a RetroHybridTree object first.");
        }
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
}

[CustomEditor(typeof(RetroHybridTree))]
public sealed class RetroHybridTreeEditor : Editor
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
                RetroHybridTreeBuilderWindow.AssignDefaultMaps((RetroHybridTree)targetObject);
            }
        }

        if (GUILayout.Button("Build / Rebuild Tree Now"))
        {
            foreach (Object targetObject in targets)
            {
                RetroHybridTreeBuilderWindow.RebuildTree((RetroHybridTree)targetObject);
            }
        }

        if (GUILayout.Button("Create / Replace Default Prefab Asset"))
        {
            RetroHybridTreeBuilderWindow.CreateOrReplacePrefab("Assets/_Project/Content/World/Nature/Trees/HybridTree/Prefabs/HybridTree.prefab", true);
        }
    }
}
