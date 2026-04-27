using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class RetroLifeFountainBuilderWindow : EditorWindow
{
    private const string DefaultPrefabPath = "Assets/_Project/Art/Sprites/Props/LifeFountain/Generated/LifeFountain.prefab";

    private static readonly string[] DefaultAssetPaths =
    {
        "Assets/_Project/Art/Sprites/Props/LifeFountain/Generated/LifeFountain.asset",
        "Assets/_Project/Art/Sprites/Props/LifeFountain/Generated/LifeFountain.prefab",
        "Assets/_Project/Art/Sprites/Props/LifeFountain/Maps/LifeFountain_Height.png",
        "Assets/_Project/Art/Sprites/Props/LifeFountain/Maps/LifeFountain_Emission.png",
        "Assets/_Project/Art/Sprites/Props/LifeFountain/Maps/LifeFountain_PackedMasks.png",
        "Assets/_Project/Art/Sprites/Props/LifeFountain/Textures/LifeFountain_ContactShadow.png",
        "Assets/_Project/Art/Sprites/Props/LifeFountain/Textures/LifeFountain_Ripple.png",
        "Assets/_Project/Art/Sprites/Props/LifeFountain/Textures/LifeFountain_Mist.png",
        "Assets/_Project/Art/Sprites/Props/LifeFountain/Textures/LifeFountain_Spark.png"
    };

    private Vector2 scroll;

    [MenuItem("Tools/Ultraloud/Props/Life Fountain Builder")]
    public static void Open()
    {
        RetroLifeFountainBuilderWindow window = GetWindow<RetroLifeFountainBuilderWindow>("Life Fountain");
        window.minSize = new Vector2(460f, 300f);
    }

    [MenuItem("GameObject/Ultraloud/Props/Life Fountain", false, 21)]
    public static void CreateSceneFountain(MenuCommand command)
    {
        GameObject fountain = InstantiateFountainPrefab();
        if (fountain == null)
        {
            return;
        }

        GameObject parent = command.context as GameObject;
        if (parent != null)
        {
            Undo.SetTransformParent(fountain.transform, parent.transform, "Create Life Fountain");
            fountain.transform.localPosition = Vector3.zero;
            fountain.transform.localRotation = Quaternion.identity;
        }

        Undo.RegisterCreatedObjectUndo(fountain, "Create Life Fountain");
        RefreshFountain(fountain);
        Selection.activeGameObject = fountain;
        EditorSceneManager.MarkSceneDirty(fountain.scene);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        using (EditorGUILayout.ScrollViewScope scope = new(scroll))
        {
            scroll = scope.scrollPosition;
            EditorGUILayout.LabelField("Life Fountain", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Places the generated directional life fountain prefab. The prop combines lit sprite frames, normal-map relief, healing interaction, and runtime mesh glow/water effects.", MessageType.Info);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Fountain In Scene", GUILayout.Height(32f)))
                {
                    CreateSceneFountain(new MenuCommand(Selection.activeGameObject));
                }

                if (GUILayout.Button("Rebuild Selected FX", GUILayout.Height(32f)))
                {
                    RebuildSelectedFountains();
                }
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Generated Assets", EditorStyles.boldLabel);
            foreach (string assetPath in DefaultAssetPaths)
            {
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(assetPath, GUILayout.Width(330f));
                    EditorGUILayout.ObjectField(asset, typeof(Object), false);
                }
            }
        }
    }

    private static GameObject InstantiateFountainPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"Life fountain prefab is missing at {DefaultPrefabPath}.");
            return null;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
        {
            Debug.LogError($"Failed to instantiate life fountain prefab at {DefaultPrefabPath}.");
        }

        return instance;
    }

    private static void RebuildSelectedFountains()
    {
        bool rebuiltAny = false;
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            if (selectedObject == null)
            {
                continue;
            }

            RetroLifeFountainFx[] fountains = selectedObject.GetComponentsInChildren<RetroLifeFountainFx>(true);
            foreach (RetroLifeFountainFx fountain in fountains)
            {
                RefreshFountain(fountain.gameObject);
                rebuiltAny = true;
            }
        }

        if (!rebuiltAny)
        {
            Debug.LogWarning("Select a life fountain object to rebuild its generated FX.");
        }
    }

    private static void RefreshFountain(GameObject fountain)
    {
        if (fountain == null)
        {
            return;
        }

        RetroShootablePrefabUtility.ConfigureLifeFountain(fountain);

        foreach (DirectionalSpriteAnimator animator in fountain.GetComponentsInChildren<DirectionalSpriteAnimator>(true))
        {
            animator.RefreshNow();
            EditorUtility.SetDirty(animator);
        }

        foreach (RetroLifeFountainFx fx in fountain.GetComponentsInChildren<RetroLifeFountainFx>(true))
        {
            fx.RebuildFountainFxNow();
            EditorUtility.SetDirty(fx);
        }
    }
}
