using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class RetroBigRockBuilderWindow : EditorWindow
{
    private const string DefaultPrefabPath = "Assets/Nature/Rocks/BigRock/Prefabs/BigRock.prefab";
    private const string GeneratedRootName = "__BigRockGenerated";

    private static readonly (string propertyName, string assetPath)[] DefaultTextureBindings =
    {
        ("baseMap", "Assets/Nature/Rocks/BigRock/Textures/BigRock_Base.png"),
        ("normalMap", "Assets/Nature/Rocks/BigRock/Textures/BigRock_Normal.png"),
        ("heightMap", "Assets/Nature/Rocks/BigRock/Textures/BigRock_Height.png"),
        ("aoMap", "Assets/Nature/Rocks/BigRock/Textures/BigRock_AO.png"),
        ("roughnessMap", "Assets/Nature/Rocks/BigRock/Textures/BigRock_Roughness.png"),
        ("crackMaskMap", "Assets/Nature/Rocks/BigRock/Textures/BigRock_CrackMask.png"),
        ("edgeWearMap", "Assets/Nature/Rocks/BigRock/Textures/BigRock_EdgeWear.png"),
        ("cavityMap", "Assets/Nature/Rocks/BigRock/Textures/BigRock_Cavity.png"),
        ("displacementMap", "Assets/Nature/Rocks/BigRock/Textures/BigRock_Displacement.png"),
        ("packedMasksMap", "Assets/Nature/Rocks/BigRock/Textures/BigRock_PackedMasks.png")
    };

    private Vector2 scroll;
    private string prefabPath = DefaultPrefabPath;
    private bool selectCreatedObject = true;

    [MenuItem("Tools/Ultraloud/Nature/Big Rock Builder")]
    public static void Open()
    {
        RetroBigRockBuilderWindow window = GetWindow<RetroBigRockBuilderWindow>("Big Rock");
        window.minSize = new Vector2(440f, 280f);
    }

    [MenuItem("GameObject/Ultraloud/Nature/Big Rock", false, 11)]
    public static void CreateSceneRock(MenuCommand command)
    {
        GameObject rockObject = CreateConfiguredRockObject("BigRock");
        GameObject parent = command.context as GameObject;
        if (parent != null)
        {
            Undo.SetTransformParent(rockObject.transform, parent.transform, "Create Big Rock");
            rockObject.transform.localPosition = Vector3.zero;
            rockObject.transform.localRotation = Quaternion.identity;
        }

        Undo.RegisterCreatedObjectUndo(rockObject, "Create Big Rock");
        RetroBigRock rock = rockObject.GetComponent<RetroBigRock>();
        rock.RebuildRockNow();
        Selection.activeGameObject = rockObject;
        EditorSceneManager.MarkSceneDirty(rockObject.scene);
    }

    [MenuItem("Assets/Create/Ultraloud/Nature/Big Rock Prefab")]
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
            EditorGUILayout.LabelField("Big Rock Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Creates a clean RetroBigRock object with generated maps assigned. The irregular mesh, material, and optional collider are generated as non-saved cache children.", MessageType.Info);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Rock In Scene", GUILayout.Height(32f)))
                {
                    CreateSceneRock(new MenuCommand(Selection.activeGameObject));
                }

                if (GUILayout.Button("Rebuild Selected", GUILayout.Height(32f)))
                {
                    RebuildSelectedRocks();
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

    public static void AssignDefaultMaps(RetroBigRock rock)
    {
        if (rock == null)
        {
            return;
        }

        SerializedObject serializedRock = new(rock);
        serializedRock.Update();
        foreach ((string propertyName, string assetPath) in DefaultTextureBindings)
        {
            SerializedProperty property = serializedRock.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"RetroBigRock is missing serialized property '{propertyName}'.", rock);
                continue;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                Debug.LogWarning($"Big rock map is missing at {assetPath}.", rock);
                continue;
            }

            property.objectReferenceValue = texture;
        }

        serializedRock.ApplyModifiedProperties();
        EditorUtility.SetDirty(rock);
    }

    public static void RebuildRock(RetroBigRock rock)
    {
        if (rock == null)
        {
            return;
        }

        AssignDefaultMaps(rock);
        rock.RebuildRockNow();
        EditorUtility.SetDirty(rock);
        if (!Application.isPlaying && rock.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(rock.gameObject.scene);
        }
    }

    public static GameObject CreateConfiguredRockObject(string objectName)
    {
        GameObject rockObject = new(objectName);
        RetroBigRock rock = rockObject.AddComponent<RetroBigRock>();
        AssignDefaultMaps(rock);
        return rockObject;
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
            Debug.LogError("Big rock prefab path must be inside Assets.");
            return;
        }

        EnsureAssetFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));

        GameObject prefabRoot = CreateConfiguredRockObject(Path.GetFileNameWithoutExtension(path));
        RetroBigRock rock = prefabRoot.GetComponent<RetroBigRock>();
        rock.RebuildRockNow();
        StripGeneratedChildren(prefabRoot.transform);

        bool success;
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, path, out success);
        Object.DestroyImmediate(prefabRoot);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!success || savedPrefab == null)
        {
            Debug.LogError($"Failed to create big rock prefab at {path}.");
            return;
        }

        if (selectAsset)
        {
            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);
        }
    }

    private static void RebuildSelectedRocks()
    {
        bool rebuiltAny = false;
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            if (selectedObject == null)
            {
                continue;
            }

            RetroBigRock[] rocks = selectedObject.GetComponentsInChildren<RetroBigRock>(true);
            foreach (RetroBigRock rock in rocks)
            {
                RebuildRock(rock);
                rebuiltAny = true;
            }
        }

        if (!rebuiltAny)
        {
            Debug.LogWarning("Select a RetroBigRock object first.");
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

[CustomEditor(typeof(RetroBigRock))]
public sealed class RetroBigRockEditor : Editor
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
                RetroBigRockBuilderWindow.AssignDefaultMaps((RetroBigRock)targetObject);
            }
        }

        if (GUILayout.Button("Build / Rebuild Rock Now"))
        {
            foreach (Object targetObject in targets)
            {
                RetroBigRockBuilderWindow.RebuildRock((RetroBigRock)targetObject);
            }
        }

        if (GUILayout.Button("Create / Replace Default Prefab Asset"))
        {
            RetroBigRockBuilderWindow.CreateOrReplacePrefab("Assets/Nature/Rocks/BigRock/Prefabs/BigRock.prefab", true);
        }
    }
}
