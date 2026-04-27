using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class RetroBirdBuilderWindow : EditorWindow
{
    private const string DefaultPrefabPath = "Assets/_Project/Content/World/Nature/Birds/SmallBird/Prefabs/SmallBird.prefab";
    private const string GeneratedRootName = "__BirdGenerated";

    private static readonly (string propertyName, string assetPath)[] DefaultTextureBindings =
    {
        ("baseMap", "Assets/_Project/Content/World/Nature/Birds/SmallBird/Textures/SmallBird_Atlas_Base.png"),
        ("normalMap", "Assets/_Project/Content/World/Nature/Birds/SmallBird/Textures/SmallBird_Atlas_Normal.png"),
        ("thicknessMap", "Assets/_Project/Content/World/Nature/Birds/SmallBird/Textures/SmallBird_Atlas_Thickness.png"),
        ("packedMasksMap", "Assets/_Project/Content/World/Nature/Birds/SmallBird/Textures/SmallBird_Atlas_PackedMasks.png")
    };

    private Vector2 scroll;
    private string prefabPath = DefaultPrefabPath;
    private bool selectCreatedObject = true;
    private int flockCount = 28;
    private float flockRadius = 18f;
    private float flockHeight = 7f;

    [MenuItem("Tools/Ultraloud/Nature/Bird Builder")]
    public static void Open()
    {
        RetroBirdBuilderWindow window = GetWindow<RetroBirdBuilderWindow>("Birds");
        window.minSize = new Vector2(460f, 330f);
    }

    [MenuItem("GameObject/Ultraloud/Nature/Bird", false, 12)]
    public static void CreateSceneBird(MenuCommand command)
    {
        GameObject birdObject = CreateConfiguredBirdObject("SmallBird", "Birds");
        GameObject parent = command.context as GameObject;
        if (parent != null)
        {
            Undo.SetTransformParent(birdObject.transform, parent.transform, "Create Bird");
            birdObject.transform.localPosition = Vector3.zero;
            birdObject.transform.localRotation = Quaternion.identity;
        }

        Undo.RegisterCreatedObjectUndo(birdObject, "Create Bird");
        RetroBirdRenderer bird = birdObject.GetComponent<RetroBirdRenderer>();
        bird.RebuildBirdNow();
        Selection.activeGameObject = birdObject;
        EditorSceneManager.MarkSceneDirty(birdObject.scene);
    }

    [MenuItem("GameObject/Ultraloud/Nature/Bird Flock", false, 13)]
    public static void CreateSceneFlock(MenuCommand command)
    {
        GameObject parent = command.context as GameObject;
        GameObject flockRoot = CreateConfiguredFlockObject("BirdFlock", 28, 18f, 7f);
        if (parent != null)
        {
            Undo.SetTransformParent(flockRoot.transform, parent.transform, "Create Bird Flock");
            flockRoot.transform.localPosition = Vector3.zero;
            flockRoot.transform.localRotation = Quaternion.identity;
        }

        Undo.RegisterCreatedObjectUndo(flockRoot, "Create Bird Flock");
        Selection.activeGameObject = flockRoot;
        EditorSceneManager.MarkSceneDirty(flockRoot.scene);
    }

    [MenuItem("Assets/Create/Ultraloud/Nature/Bird Prefab")]
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
            EditorGUILayout.LabelField("Bird Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Creates animated shootable bird billboards with generated atlas maps, scene-light shading, wing transmission, feather/blood impact feedback, and optional generic flocking.", MessageType.Info);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Bird In Scene", GUILayout.Height(32f)))
                {
                    CreateSceneBird(new MenuCommand(Selection.activeGameObject));
                }

                if (GUILayout.Button("Rebuild Selected", GUILayout.Height(32f)))
                {
                    RebuildSelectedBirds();
                }
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Flock", EditorStyles.boldLabel);
            flockCount = EditorGUILayout.IntSlider("Count", flockCount, 1, 128);
            flockRadius = EditorGUILayout.Slider("Radius", flockRadius, 2f, 80f);
            flockHeight = EditorGUILayout.Slider("Height Spread", flockHeight, 0.5f, 30f);
            if (GUILayout.Button("Create Flock In Scene", GUILayout.Height(30f)))
            {
                GameObject root = CreateConfiguredFlockObject("BirdFlock", flockCount, flockRadius, flockHeight);
                Undo.RegisterCreatedObjectUndo(root, "Create Bird Flock");
                Selection.activeGameObject = root;
                EditorSceneManager.MarkSceneDirty(root.scene);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Prefab", EditorStyles.boldLabel);
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

    public static void AssignDefaultMaps(RetroBirdRenderer bird)
    {
        if (bird == null)
        {
            return;
        }

        SerializedObject serializedBird = new(bird);
        serializedBird.Update();
        foreach ((string propertyName, string assetPath) in DefaultTextureBindings)
        {
            SerializedProperty property = serializedBird.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"RetroBirdRenderer is missing serialized property '{propertyName}'.", bird);
                continue;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                Debug.LogWarning($"Small bird map is missing at {assetPath}.", bird);
                continue;
            }

            property.objectReferenceValue = texture;
        }

        serializedBird.ApplyModifiedProperties();
        EditorUtility.SetDirty(bird);
    }

    public static void RebuildBird(RetroBirdRenderer bird)
    {
        if (bird == null)
        {
            return;
        }

        AssignDefaultMaps(bird);
        RetroShootablePrefabUtility.ConfigureSmallBird(bird.gameObject);
        bird.RebuildBirdNow();
        EditorUtility.SetDirty(bird);
        if (!Application.isPlaying && bird.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(bird.gameObject.scene);
        }
    }

    public static GameObject CreateConfiguredBirdObject(string objectName, string groupId)
    {
        GameObject birdObject = new(objectName);
        RetroBirdRenderer bird = birdObject.AddComponent<RetroBirdRenderer>();
        AssignDefaultMaps(bird);

        RetroFlockAgent agent = birdObject.AddComponent<RetroFlockAgent>();
        ConfigureAgentDefaults(agent, groupId, birdObject.transform.position, birdObject.transform.forward * 4.5f);
        RetroShootablePrefabUtility.ConfigureSmallBird(birdObject);
        return birdObject;
    }

    public static GameObject CreateConfiguredFlockObject(string objectName, int count, float radius, float height)
    {
        GameObject root = new(objectName);
        string groupId = $"{objectName}_{Random.Range(1000, 9999)}";
        System.Random random = new(31415);
        for (int i = 0; i < Mathf.Max(1, count); i++)
        {
            GameObject birdObject = CreateConfiguredBirdObject($"SmallBird_{i:00}", groupId);
            birdObject.transform.SetParent(root.transform, false);
            Vector3 offset = SampleFlockOffset(random, radius, height);
            birdObject.transform.localPosition = offset;
            birdObject.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            float scale = Mathf.Lerp(0.82f, 1.18f, (float)random.NextDouble());
            birdObject.transform.localScale = Vector3.one * scale;

            RetroFlockAgent agent = birdObject.GetComponent<RetroFlockAgent>();
            Vector3 velocity = RandomDirection(random);
            velocity.y *= 0.2f;
            if (velocity.sqrMagnitude < 0.001f)
            {
                velocity = Vector3.forward;
            }

            ConfigureAgentDefaults(agent, groupId, root.transform.position, velocity.normalized * Mathf.Lerp(3.5f, 7f, (float)random.NextDouble()));
            agent.RandomizePhase(i * 37.3f + (float)random.NextDouble() * 100f);

            RetroBirdRenderer bird = birdObject.GetComponent<RetroBirdRenderer>();
            bird.RebuildBirdNow();
        }

        RetroFlockSpawner spawner = root.AddComponent<RetroFlockSpawner>();
        SerializedObject serializedSpawner = new(spawner);
        serializedSpawner.Update();
        SetObject(serializedSpawner, "agentPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPrefabPath));
        SetInt(serializedSpawner, "count", Mathf.Max(1, count));
        SetString(serializedSpawner, "groupId", groupId);
        SetFloat(serializedSpawner, "spawnRadius", radius);
        SetVector3(serializedSpawner, "spawnExtents", new Vector3(radius, height, radius));
        SetBool(serializedSpawner, "spawnOnStart", false);
        serializedSpawner.ApplyModifiedPropertiesWithoutUndo();

        return root;
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
            Debug.LogError("Small bird prefab path must be inside Assets.");
            return;
        }

        EnsureAssetFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));

        GameObject prefabRoot = CreateConfiguredBirdObject(Path.GetFileNameWithoutExtension(path), "Birds");
        RetroBirdRenderer bird = prefabRoot.GetComponent<RetroBirdRenderer>();
        RetroShootablePrefabUtility.ConfigureSmallBird(prefabRoot);
        bird.RebuildBirdNow();
        StripGeneratedChildren(prefabRoot.transform);

        bool success;
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, path, out success);
        Object.DestroyImmediate(prefabRoot);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!success || savedPrefab == null)
        {
            Debug.LogError($"Failed to create small bird prefab at {path}.");
            return;
        }

        if (selectAsset)
        {
            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);
        }
    }

    private static void ConfigureAgentDefaults(RetroFlockAgent agent, string groupId, Vector3 homePosition, Vector3 velocity)
    {
        if (agent == null)
        {
            return;
        }

        SerializedObject serializedAgent = new(agent);
        serializedAgent.Update();
        SetString(serializedAgent, "groupId", string.IsNullOrWhiteSpace(groupId) ? "Birds" : groupId);
        SetFloat(serializedAgent, "minSpeed", 2.8f);
        SetFloat(serializedAgent, "maxSpeed", 7.5f);
        SetFloat(serializedAgent, "maxForce", 9.5f);
        SetFloat(serializedAgent, "turnResponsiveness", 8f);
        SetBool(serializedAgent, "useCourseTargets", true);
        SetFloat(serializedAgent, "courseWeight", 1.65f);
        SetVector2(serializedAgent, "courseRetargetInterval", new Vector2(3.4f, 6.8f));
        SetFloat(serializedAgent, "courseArrivalRadius", 3.2f);
        SetFloat(serializedAgent, "courseVerticalInfluence", 0.38f);
        SetFloat(serializedAgent, "courseRandomness", 0.28f);
        SetFloat(serializedAgent, "neighborRadius", 7.5f);
        SetFloat(serializedAgent, "separationRadius", 2.1f);
        SetFloat(serializedAgent, "separationWeight", 2.8f);
        SetFloat(serializedAgent, "alignmentWeight", 0.85f);
        SetFloat(serializedAgent, "cohesionWeight", 0.72f);
        SetFloat(serializedAgent, "wanderWeight", 0.24f);
        SetFloat(serializedAgent, "homeRadius", 28f);
        SetVector3(serializedAgent, "boundsHalfExtents", new Vector3(26f, 8f, 26f));
        SetFloat(serializedAgent, "boundsWeight", 2f);
        SetBool(serializedAgent, "keepWithinHeightBand", true);
        SetVector2(serializedAgent, "heightBand", new Vector2(3f, 16f));
        SetFloat(serializedAgent, "heightWeight", 1.15f);
        SetBool(serializedAgent, "avoidObstacles", true);
        SetFloat(serializedAgent, "obstacleProbeDistance", 5f);
        SetFloat(serializedAgent, "obstacleWeight", 3.2f);
        SetVector3(serializedAgent, "initialVelocity", velocity);
        serializedAgent.ApplyModifiedPropertiesWithoutUndo();

        agent.GroupId = groupId;
        agent.SetHome(homePosition);
        agent.SetVelocity(velocity);
        EditorUtility.SetDirty(agent);
    }

    private static void RebuildSelectedBirds()
    {
        bool rebuiltAny = false;
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            if (selectedObject == null)
            {
                continue;
            }

            RetroBirdRenderer[] birds = selectedObject.GetComponentsInChildren<RetroBirdRenderer>(true);
            foreach (RetroBirdRenderer bird in birds)
            {
                RebuildBird(bird);
                rebuiltAny = true;
            }
        }

        if (!rebuiltAny)
        {
            Debug.LogWarning("Select a RetroBirdRenderer object first.");
        }
    }

    private static Vector3 SampleFlockOffset(System.Random random, float radius, float height)
    {
        Vector3 direction = RandomDirection(random);
        float distance = Mathf.Pow((float)random.NextDouble(), 1f / 3f) * Mathf.Max(0.1f, radius);
        return new Vector3(direction.x * distance, direction.y * Mathf.Max(0.1f, height), direction.z * distance);
    }

    private static Vector3 RandomDirection(System.Random random)
    {
        double z = random.NextDouble() * 2.0 - 1.0;
        double angle = random.NextDouble() * Mathf.PI * 2.0;
        double radius = System.Math.Sqrt(System.Math.Max(0.0, 1.0 - z * z));
        return new Vector3((float)(radius * System.Math.Cos(angle)), (float)z, (float)(radius * System.Math.Sin(angle)));
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

    private static void SetString(SerializedObject target, string propertyName, string value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
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

    private static void SetInt(SerializedObject target, string propertyName, int value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
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

    private static void SetFloat(SerializedObject target, string propertyName, float value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetVector2(SerializedObject target, string propertyName, Vector2 value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.vector2Value = value;
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
}

[CustomEditor(typeof(RetroBirdRenderer))]
public sealed class RetroBirdRendererEditor : Editor
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
                RetroBirdBuilderWindow.AssignDefaultMaps((RetroBirdRenderer)targetObject);
            }
        }

        if (GUILayout.Button("Build / Rebuild Bird Now"))
        {
            foreach (Object targetObject in targets)
            {
                RetroBirdBuilderWindow.RebuildBird((RetroBirdRenderer)targetObject);
            }
        }

        if (GUILayout.Button("Create / Replace Default Prefab Asset"))
        {
            RetroBirdBuilderWindow.CreateOrReplacePrefab("Assets/_Project/Content/World/Nature/Birds/SmallBird/Prefabs/SmallBird.prefab", true);
        }
    }
}

[CustomEditor(typeof(RetroFlockSpawner))]
public sealed class RetroFlockSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Spawner", EditorStyles.boldLabel);
        if (GUILayout.Button("Spawn Now"))
        {
            foreach (Object targetObject in targets)
            {
                RetroFlockSpawner spawner = (RetroFlockSpawner)targetObject;
                spawner.SpawnNow();
                EditorUtility.SetDirty(spawner);
            }
        }

        if (GUILayout.Button("Clear Spawned"))
        {
            foreach (Object targetObject in targets)
            {
                RetroFlockSpawner spawner = (RetroFlockSpawner)targetObject;
                spawner.ClearSpawned();
                EditorUtility.SetDirty(spawner);
            }
        }
    }
}
