using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class RetroB2BomberBuilderWindow : EditorWindow
{
    private const string PrefabRoot = "Assets/Prefabs/B2Bomber";
    private const string RaidPrefabPath = PrefabRoot + "/B2BomberRaid.prefab";
    private const string ActorPrefabPath = PrefabRoot + "/B2BomberActor.prefab";
    private const string BombPrefabPath = PrefabRoot + "/B2Bomb.prefab";
    private const string ExplosionPrefabPath = PrefabRoot + "/B2Explosion.prefab";

    private static readonly string[] BomberSpritePaths =
    {
        "Assets/Sprites/Entities/B2Bomber/Bomber/00.png",
        "Assets/Sprites/Entities/B2Bomber/Bomber/01.png",
        "Assets/Sprites/Entities/B2Bomber/Bomber/02.png"
    };

    private static readonly string[] BombSpritePaths =
    {
        "Assets/Sprites/Entities/B2Bomber/Bomb/00.png",
        "Assets/Sprites/Entities/B2Bomber/Bomb/01.png",
        "Assets/Sprites/Entities/B2Bomber/Bomb/02.png",
        "Assets/Sprites/Entities/B2Bomber/Bomb/03.png"
    };

    private static readonly string[] ExplosionSpritePaths =
    {
        "Assets/Sprites/Entities/B2Bomber/Explosion/00.png",
        "Assets/Sprites/Entities/B2Bomber/Explosion/01.png",
        "Assets/Sprites/Entities/B2Bomber/Explosion/02.png",
        "Assets/Sprites/Entities/B2Bomber/Explosion/03.png",
        "Assets/Sprites/Entities/B2Bomber/Explosion/04.png",
        "Assets/Sprites/Entities/B2Bomber/Explosion/05.png"
    };

    private Vector2 scroll;
    private bool selectCreatedAsset = true;

    [MenuItem("Tools/Ultraloud/Enemies/B2 Bomber Builder")]
    public static void Open()
    {
        RetroB2BomberBuilderWindow window = GetWindow<RetroB2BomberBuilderWindow>("B2 Bomber");
        window.minSize = new Vector2(470f, 330f);
    }

    [MenuItem("GameObject/Ultraloud/Enemies/B2 Bomber Raid", false, 31)]
    public static void CreateSceneRaid(MenuCommand command)
    {
        GameObject prefab = EnsurePrefabAssets(false);
        if (prefab == null)
        {
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            Debug.LogError($"Failed to instantiate B2 bomber raid prefab at {RaidPrefabPath}.");
            return;
        }

        GameObject parent = command.context as GameObject;
        if (parent != null)
        {
            Undo.SetTransformParent(instance.transform, parent.transform, "Create B2 Bomber Raid");
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
        }
        else if (SceneView.lastActiveSceneView != null)
        {
            instance.transform.position = SceneView.lastActiveSceneView.pivot;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Create B2 Bomber Raid");
        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(instance.scene);
    }

    [MenuItem("Assets/Create/Ultraloud/Enemies/B2 Bomber Prefabs")]
    public static void CreatePrefabAssetsFromAssetsMenu()
    {
        EnsurePrefabAssets(true);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        using (EditorGUILayout.ScrollViewScope scope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scope.scrollPosition;
            EditorGUILayout.LabelField("B2 Bomber Raid", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Creates a sky pass spawner, animated bomber, slow dodgeable bombs, warning rings, projectile trails, radius damage, rumble/whistle audio, and bitmap explosion VFX from the generated sprites.", MessageType.Info);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Raid In Scene", GUILayout.Height(32f)))
                {
                    CreateSceneRaid(new MenuCommand(Selection.activeGameObject));
                }

                if (GUILayout.Button("Create / Replace Prefabs", GUILayout.Height(32f)))
                {
                    EnsurePrefabAssets(selectCreatedAsset);
                }
            }

            selectCreatedAsset = EditorGUILayout.Toggle("Select Created Raid Prefab", selectCreatedAsset);

            EditorGUILayout.Space(10f);
            DrawAssetStatus("Prefabs", new[] { RaidPrefabPath, ActorPrefabPath, BombPrefabPath, ExplosionPrefabPath });
            EditorGUILayout.Space(10f);
            DrawAssetStatus("Bomber Sprites", BomberSpritePaths);
            DrawAssetStatus("Bomb Sprites", BombSpritePaths);
            DrawAssetStatus("Explosion Sprites", ExplosionSpritePaths);
        }
    }

    public static GameObject EnsurePrefabAssets(bool selectRaidPrefab)
    {
        if (!ValidateSpriteAssets())
        {
            return null;
        }

        EnsureAssetFolder(PrefabRoot);
        Sprite[] bomberSprites = LoadSprites(BomberSpritePaths);
        Sprite[] bombSprites = LoadSprites(BombSpritePaths);
        Sprite[] explosionSprites = LoadSprites(ExplosionSpritePaths);

        RetroB2ExplosionVfx explosionPrefab = CreateExplosionPrefab(explosionSprites);
        RetroB2BombProjectile bombPrefab = CreateBombPrefab(bombSprites, explosionPrefab);
        RetroB2BomberActor actorPrefab = CreateActorPrefab(bomberSprites, bombPrefab);
        GameObject raidPrefab = CreateRaidPrefab(actorPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (selectRaidPrefab && raidPrefab != null)
        {
            Selection.activeObject = raidPrefab;
            EditorGUIUtility.PingObject(raidPrefab);
        }

        return raidPrefab;
    }

    private static RetroB2ExplosionVfx CreateExplosionPrefab(Sprite[] sprites)
    {
        GameObject root = new GameObject("B2Explosion");
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprites.Length > 0 ? sprites[0] : null;
        renderer.sortingOrder = 35;
        RetroB2ExplosionVfx explosion = root.AddComponent<RetroB2ExplosionVfx>();

        SerializedObject serialized = new SerializedObject(explosion);
        serialized.Update();
        SetObject(serialized, "spriteRenderer", renderer);
        SetSpriteArray(serialized, "animationFrames", sprites);
        SetFloat(serialized, "framesPerSecond", 16f);
        SetFloat(serialized, "lifetime", 0.78f);
        SetFloat(serialized, "spriteScaleMultiplier", 0.42f);
        SetBool(serialized, "spawnShockwave", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        RetroB2ExplosionVfx saved = SavePrefabComponent<RetroB2ExplosionVfx>(root, ExplosionPrefabPath);
        return saved;
    }

    private static RetroB2BombProjectile CreateBombPrefab(Sprite[] sprites, RetroB2ExplosionVfx explosionPrefab)
    {
        GameObject root = new GameObject("B2Bomb");
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprites.Length > 0 ? sprites[0] : null;
        renderer.sortingOrder = 28;
        RetroB2BombProjectile bomb = root.AddComponent<RetroB2BombProjectile>();

        SerializedObject serialized = new SerializedObject(bomb);
        serialized.Update();
        SetObject(serialized, "spriteRenderer", renderer);
        SetSpriteArray(serialized, "animationFrames", sprites);
        SetObject(serialized, "explosionPrefab", explosionPrefab);
        SetFloat(serialized, "animationFramesPerSecond", 9f);
        SetFloat(serialized, "spriteScale", 1.1f);
        SetFloat(serialized, "trailInterval", 0.065f);
        SetFloat(serialized, "fallDuration", 4.65f);
        SetFloat(serialized, "fallSpeed", 13.5f);
        SetFloat(serialized, "maximumFallDuration", 7.5f);
        SetFloat(serialized, "impactHoverHeight", 0.25f);
        SetFloat(serialized, "explosionRadius", 7.5f);
        SetFloat(serialized, "damage", 88f);
        SetFloat(serialized, "explosionForce", 19f);
        SetFloat(serialized, "whistleVolume", 0.58f);
        SetFloat(serialized, "explosionVolume", 1f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        RetroB2BombProjectile saved = SavePrefabComponent<RetroB2BombProjectile>(root, BombPrefabPath);
        return saved;
    }

    private static RetroB2BomberActor CreateActorPrefab(Sprite[] sprites, RetroB2BombProjectile bombPrefab)
    {
        GameObject root = new GameObject("B2BomberActor");
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprites.Length > 0 ? sprites[0] : null;
        renderer.sortingOrder = 24;
        RetroB2BomberActor actor = root.AddComponent<RetroB2BomberActor>();

        SerializedObject serialized = new SerializedObject(actor);
        serialized.Update();
        SetObject(serialized, "spriteRenderer", renderer);
        SetSpriteArray(serialized, "animationFrames", sprites);
        SetObject(serialized, "bombPrefab", bombPrefab);
        SetFloat(serialized, "animationFramesPerSecond", 7f);
        SetFloat(serialized, "spriteScale", 11.5f);
        SetVector3(serialized, "visualOffset", Vector3.zero);
        SetVector3(serialized, "bombSpawnOffset", new Vector3(0f, -1.25f, 0f));
        SetFloat(serialized, "engineVolume", 0.38f);
        SetFloat(serialized, "engineMaxDistance", 165f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        RetroB2BomberActor saved = SavePrefabComponent<RetroB2BomberActor>(root, ActorPrefabPath);
        return saved;
    }

    private static GameObject CreateRaidPrefab(RetroB2BomberActor actorPrefab)
    {
        GameObject root = new GameObject("B2BomberRaid");
        RetroB2BomberRaid raid = root.AddComponent<RetroB2BomberRaid>();

        SerializedObject serialized = new SerializedObject(raid);
        serialized.Update();
        SetObject(serialized, "bomberPrefab", actorPrefab);
        SetBool(serialized, "automaticRaids", true);
        SetVector2(serialized, "firstPassDelayRange", new Vector2(8f, 18f));
        SetVector2(serialized, "passIntervalRange", new Vector2(28f, 55f));
        SetFloat(serialized, "passHeight", 68f);
        SetFloat(serialized, "passLength", 340f);
        SetFloat(serialized, "passSpeed", 18f);
        SetFloat(serialized, "lateralJitter", 22f);
        SetVector2Int(serialized, "bombsPerPassRange", new Vector2Int(4, 6));
        SetFloat(serialized, "bombLineLength", 92f);
        SetFloat(serialized, "targetScatterRadius", 22f);
        SetFloat(serialized, "minimumPlayerMissDistance", 8f);
        SetBool(serialized, "avoidDirectPlayerCenter", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        bool success;
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, RaidPrefabPath, out success);
        Object.DestroyImmediate(root);
        if (!success || savedPrefab == null)
        {
            Debug.LogError($"Failed to create B2 bomber raid prefab at {RaidPrefabPath}.");
            return null;
        }

        return savedPrefab;
    }

    private static T SavePrefabComponent<T>(GameObject root, string path) where T : Component
    {
        bool success;
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, path, out success);
        Object.DestroyImmediate(root);
        if (!success || savedPrefab == null)
        {
            Debug.LogError($"Failed to create prefab at {path}.");
            return null;
        }

        return savedPrefab.GetComponent<T>();
    }

    private static bool ValidateSpriteAssets()
    {
        bool valid = true;
        valid &= ValidateSpriteArray(BomberSpritePaths);
        valid &= ValidateSpriteArray(BombSpritePaths);
        valid &= ValidateSpriteArray(ExplosionSpritePaths);
        return valid;
    }

    private static bool ValidateSpriteArray(string[] assetPaths)
    {
        bool valid = true;
        for (int i = 0; i < assetPaths.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(assetPaths[i]) == null)
            {
                Debug.LogError($"Missing B2 bomber sprite at {assetPaths[i]}.");
                valid = false;
            }
        }

        return valid;
    }

    private static Sprite[] LoadSprites(string[] assetPaths)
    {
        Sprite[] sprites = new Sprite[assetPaths.Length];
        for (int i = 0; i < assetPaths.Length; i++)
        {
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(assetPaths[i]);
        }

        return sprites;
    }

    private static void DrawAssetStatus(string title, string[] assetPaths)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        for (int i = 0; i < assetPaths.Length; i++)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPaths[i]);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(assetPaths[i], GUILayout.Width(350f));
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

    private static void SetObject(SerializedObject target, string propertyName, Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetSpriteArray(SerializedObject target, string propertyName, Sprite[] sprites)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
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

    private static void SetVector2Int(SerializedObject target, string propertyName, Vector2Int value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.vector2IntValue = value;
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

[CustomEditor(typeof(RetroB2BomberRaid))]
public sealed class RetroB2BomberRaidEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Builder", EditorStyles.boldLabel);
        if (GUILayout.Button("Create / Replace Prefab Assets"))
        {
            RetroB2BomberBuilderWindow.EnsurePrefabAssets(true);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Trigger Raid Now"))
            {
                foreach (Object targetObject in targets)
                {
                    RetroB2BomberRaid raid = (RetroB2BomberRaid)targetObject;
                    raid.TriggerRaidNow();
                }
            }
        }
    }
}
