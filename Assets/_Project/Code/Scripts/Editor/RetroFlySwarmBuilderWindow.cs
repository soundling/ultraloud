using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class RetroFlySwarmBuilderWindow : EditorWindow
{
    private const string ArtRootPath = "Assets/_Project/Art/Sprites/Entities/FlySwarm";
    private const string FramesRootPath = ArtRootPath + "/Frames";
    private const string GeneratedRootPath = ArtRootPath + "/Generated";
    private const string ContentRootPath = "Assets/_Project/Content/Actors/FlySwarm";
    private const string PrefabRootPath = ContentRootPath + "/Prefabs";
    private const string FlyAssetName = "FlyPest";
    private const string FlyPrefabPath = GeneratedRootPath + "/" + FlyAssetName + ".prefab";
    private const string SwarmPrefabPath = PrefabRootPath + "/FlySwarmCloud.prefab";

    [MenuItem("Tools/Ultraloud/Nature/Fly Swarm Builder")]
    private static void Open()
    {
        GetWindow<RetroFlySwarmBuilderWindow>("Fly Swarm");
    }

    [MenuItem("Tools/Ultraloud/Nature/Fly Swarm Builder/Build All Assets")]
    public static void BuildAllAssetsMenu()
    {
        BuildAllAssets(selectAsset: true);
    }

    [MenuItem("GameObject/Ultraloud/Nature/Fly Swarm", false, 14)]
    public static void CreateSceneFlySwarm(MenuCommand command)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwarmPrefabPath);
        if (prefab == null)
        {
            prefab = BuildAllAssets(selectAsset: false);
        }

        GameObject instance = prefab != null
            ? PrefabUtility.InstantiatePrefab(prefab) as GameObject
            : new GameObject("FlySwarmCloud");

        if (instance == null)
        {
            return;
        }

        if (command.context is GameObject parent)
        {
            Undo.SetTransformParent(instance.transform, parent.transform, "Create Fly Swarm");
        }

        Undo.RegisterCreatedObjectUndo(instance, "Create Fly Swarm");
        Selection.activeObject = instance;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Fly Swarm Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Builds a multi-angle fly directional sprite prefab from the generated frame folders, then creates a FlySwarmCloud prefab that spawns 50 pest flies and attacks nearby damageable targets.",
            MessageType.Info);

        DrawAssetStatus("Frames", new[] { FramesRootPath });
        DrawAssetStatus("Generated", new[] { FlyPrefabPath, SwarmPrefabPath });

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Build Fly Entity And Swarm Prefab", GUILayout.Height(32f)))
        {
            BuildAllAssets(selectAsset: true);
        }
    }

    private static GameObject BuildAllAssets(bool selectAsset)
    {
        EnsureAssetFolder(GeneratedRootPath);
        EnsureAssetFolder(PrefabRootPath);
        AssetDatabase.Refresh();

        DefaultAsset framesRoot = AssetDatabase.LoadAssetAtPath<DefaultAsset>(FramesRootPath);
        DefaultAsset outputRoot = AssetDatabase.LoadAssetAtPath<DefaultAsset>(GeneratedRootPath);
        if (framesRoot == null || outputRoot == null)
        {
            Debug.LogError($"Fly swarm frames are missing. Expected frames at {FramesRootPath}.");
            return null;
        }

        DirectionalSpriteFrameBuildResult result = DirectionalSpriteFrameBuilder.Build(
            framesRoot,
            outputRoot,
            new DirectionalSpriteFrameBuildOptions
            {
                assetName = FlyAssetName,
                buildPrefab = true,
                instantiateInScene = false,
                addLocomotion = false,
                worldScaleMultiplier = 0.16f
            });

        ConfigureDefinition(result.definition);
        ConfigureFlyPrefab(result.definition);
        GameObject swarmPrefab = CreateOrUpdateSwarmPrefab(result.prefabAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (selectAsset && swarmPrefab != null)
        {
            Selection.activeObject = swarmPrefab;
            EditorGUIUtility.PingObject(swarmPrefab);
        }

        return swarmPrefab;
    }

    private static void ConfigureDefinition(DirectionalSpriteDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        definition.defaultClipId = "Fly";
        if (definition.clips != null)
        {
            for (int i = 0; i < definition.clips.Count; i++)
            {
                DirectionalSpriteClip clip = definition.clips[i];
                if (clip == null)
                {
                    continue;
                }

                clip.clipId = string.IsNullOrWhiteSpace(clip.clipId) ? "Fly" : clip.clipId;
                clip.loop = true;
                clip.framesPerSecond = 18f;
            }
        }

        EditorUtility.SetDirty(definition);
    }

    private static void ConfigureFlyPrefab(DirectionalSpriteDefinition definition)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlyPrefabPath);
        if (prefab == null || definition == null)
        {
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(FlyPrefabPath);
        try
        {
            root.name = FlyAssetName;
            Transform quad = root.transform.Find("Quad");
            MeshRenderer quadRenderer = quad != null ? quad.GetComponent<MeshRenderer>() : root.GetComponentInChildren<MeshRenderer>(true);
            if (quadRenderer != null)
            {
                quadRenderer.shadowCastingMode = ShadowCastingMode.Off;
                quadRenderer.receiveShadows = false;
            }

            DirectionalSpriteLocomotion locomotion = root.GetComponent<DirectionalSpriteLocomotion>();
            if (locomotion != null)
            {
                Object.DestroyImmediate(locomotion, true);
            }

            DirectionalSpriteAnimator animator = GetOrAdd<DirectionalSpriteAnimator>(root);
            ConfigureAnimator(animator, definition, quad);

            DirectionalSpriteBillboardLitRenderer litRenderer = GetOrAdd<DirectionalSpriteBillboardLitRenderer>(root);
            ConfigureLitRenderer(litRenderer, animator, quadRenderer);

            DirectionalSpriteHitMask hitMask = GetOrAdd<DirectionalSpriteHitMask>(root);
            ConfigureHitMask(hitMask, animator, quadRenderer, quad);

            SphereCollider collider = GetOrAdd<SphereCollider>(root);
            collider.center = new Vector3(0f, 0f, 0f);
            collider.radius = 0.16f;
            collider.isTrigger = false;

            Rigidbody body = GetOrAdd<Rigidbody>(root);
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            ConfigureFlockAgent(GetOrAdd<RetroFlockAgent>(root));
            ConfigurePestAgent(GetOrAdd<RetroFlyPestAgent>(root), animator);
            ConfigureDamageable(GetOrAdd<RetroDamageable>(root));

            PrefabUtility.SaveAsPrefabAsset(root, FlyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject CreateOrUpdateSwarmPrefab(GameObject flyPrefab)
    {
        if (flyPrefab == null)
        {
            flyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlyPrefabPath);
        }

        GameObject root = new("FlySwarmCloud");
        try
        {
            RetroFlySwarm swarm = root.AddComponent<RetroFlySwarm>();
            SerializedObject serialized = new(swarm);
            SetObject(serialized, "agentPrefab", flyPrefab);
            SetInt(serialized, "count", 50);
            SetBool(serialized, "spawnOnStart", true);
            SetBool(serialized, "clearOnDisable", true);
            SetString(serialized, "groupId", "PestFlies");
            SetFloat(serialized, "spawnRadius", 3.4f);
            SetVector2(serialized, "speedRange", new Vector2(6.8f, 12f));
            SetVector2(serialized, "scaleRange", new Vector2(0.78f, 1.16f));
            SetInt(serialized, "seed", 88431);
            SetBool(serialized, "preferTaggedTarget", true);
            SetString(serialized, "preferredTargetTag", "Player");
            SetFloat(serialized, "targetSearchRadius", 34f);
            SetFloat(serialized, "targetRefreshInterval", 0.42f);
            SetFloat(serialized, "cloudMoveSpeed", 8.75f);
            SetFloat(serialized, "cloudAcceleration", 18f);
            SetFloat(serialized, "targetHoverHeight", 1.35f);
            SetFloat(serialized, "attackOrbitRadius", 1.55f);
            SetFloat(serialized, "orbitSpeed", 2.4f);
            SetFloat(serialized, "idleDriftRadius", 2.7f);
            SetFloat(serialized, "idleDriftSpeed", 0.58f);
            SetFloat(serialized, "biteRadius", 0.42f);
            SetFloat(serialized, "biteDamage", 1.15f);
            SetFloat(serialized, "biteInterval", 0.34f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            bool success;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, SwarmPrefabPath, out success);
            return success ? saved : null;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureAnimator(DirectionalSpriteAnimator animator, DirectionalSpriteDefinition definition, Transform quad)
    {
        SerializedObject serialized = new(animator);
        serialized.FindProperty("definition").objectReferenceValue = definition;
        serialized.FindProperty("initialClipId").stringValue = "Fly";
        serialized.FindProperty("playOnEnable").boolValue = true;
        serialized.FindProperty("animationSpeed").floatValue = 1f;
        serialized.FindProperty("freezeInitialClipInEditMode").boolValue = true;
        serialized.FindProperty("spriteRenderer").objectReferenceValue = null;
        serialized.FindProperty("billboardRoot").objectReferenceValue = quad;
        serialized.FindProperty("facingReference").objectReferenceValue = animator.transform;
        serialized.FindProperty("targetCamera").objectReferenceValue = null;
        serialized.FindProperty("viewAngleSource").enumValueIndex = (int)DirectionalSpriteViewAngleSource.CameraPosition;
        serialized.FindProperty("billboardMode").enumValueIndex = (int)DirectionalBillboardMode.YAxis;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(animator);
    }

    private static void ConfigureLitRenderer(DirectionalSpriteBillboardLitRenderer renderer, DirectionalSpriteAnimator animator, Renderer quadRenderer)
    {
        SerializedObject serialized = new(renderer);
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("targetRenderer").objectReferenceValue = quadRenderer;
        serialized.FindProperty("lightAnchor").objectReferenceValue = null;
        SetFloat(serialized, "alphaCutoff", 0.055f);
        SetFloat(serialized, "normalScale", 0.7f);
        SetFloat(serialized, "detailNormalInfluence", 0.32f);
        SetFloat(serialized, "macroNormalBend", 0.38f);
        SetFloat(serialized, "spriteAngleLightingInfluence", 0.2f);
        SetFloat(serialized, "wrapDiffuse", 0.46f);
        SetFloat(serialized, "ambientIntensity", 1.04f);
        SetFloat(serialized, "renderSettingsAmbientScale", 0.26f);
        SetFloat(serialized, "surfaceRoughness", 0.86f);
        SetFloat(serialized, "specularStrength", 0.08f);
        SetFloat(serialized, "rimStrength", 0.08f);
        SetFloat(serialized, "rimPower", 3.2f);
        SetColor(serialized, "rimColor", new Color(0.9f, 0.92f, 0.86f, 1f));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(renderer);
    }

    private static void ConfigureHitMask(DirectionalSpriteHitMask hitMask, DirectionalSpriteAnimator animator, Renderer quadRenderer, Transform quad)
    {
        SerializedObject serialized = new(hitMask);
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("visualRenderer").objectReferenceValue = quadRenderer;
        serialized.FindProperty("hitPlane").objectReferenceValue = quad;
        serialized.FindProperty("useAlphaMask").boolValue = true;
        serialized.FindProperty("useSpritePhysicsShapeFallback").boolValue = true;
        serialized.FindProperty("acceptWhenMaskUnavailable").boolValue = true;
        serialized.FindProperty("rejectHitsOutsideQuad").boolValue = true;
        SetFloat(serialized, "alphaThreshold", 0.08f);
        serialized.FindProperty("edgePaddingPixels").intValue = 2;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hitMask);
    }

    private static void ConfigureFlockAgent(RetroFlockAgent agent)
    {
        SerializedObject serialized = new(agent);
        SetString(serialized, "groupId", "PestFlies");
        SetFloat(serialized, "minSpeed", 5.8f);
        SetFloat(serialized, "maxSpeed", 12.5f);
        SetFloat(serialized, "maxForce", 24f);
        SetFloat(serialized, "turnResponsiveness", 13f);
        SetBool(serialized, "useCourseTargets", true);
        SetFloat(serialized, "courseWeight", 3.1f);
        SetVector2(serialized, "courseRetargetInterval", new Vector2(0.24f, 0.68f));
        SetFloat(serialized, "courseArrivalRadius", 0.55f);
        SetFloat(serialized, "courseVerticalInfluence", 0.84f);
        SetFloat(serialized, "courseRandomness", 0.92f);
        SetFloat(serialized, "neighborRadius", 2.8f);
        SetFloat(serialized, "separationRadius", 0.42f);
        SetFloat(serialized, "separationWeight", 3.4f);
        SetFloat(serialized, "alignmentWeight", 1.1f);
        SetFloat(serialized, "cohesionWeight", 0.92f);
        SetFloat(serialized, "wanderWeight", 1.2f);
        SetEnum(serialized, "boundsMode", (int)RetroFlockAgent.BoundsMode.Sphere);
        SetFloat(serialized, "homeRadius", 4.8f);
        SetVector3(serialized, "boundsHalfExtents", new Vector3(4.8f, 2.4f, 4.8f));
        SetFloat(serialized, "boundsWeight", 5.4f);
        SetBool(serialized, "keepWithinHeightBand", false);
        SetBool(serialized, "avoidObstacles", true);
        SetFloat(serialized, "obstacleProbeDistance", 1.8f);
        SetFloat(serialized, "obstacleWeight", 5.2f);
        SetBool(serialized, "drawGizmos", false);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(agent);
    }

    private static void ConfigurePestAgent(RetroFlyPestAgent agent, DirectionalSpriteAnimator animator)
    {
        SerializedObject serialized = new(agent);
        serialized.FindProperty("flockAgent").objectReferenceValue = agent.GetComponent<RetroFlockAgent>();
        serialized.FindProperty("animator").objectReferenceValue = animator;
        SetFloat(serialized, "biteRadius", 0.42f);
        SetFloat(serialized, "biteDamage", 1.15f);
        SetFloat(serialized, "biteInterval", 0.34f);
        SetBool(serialized, "biteExplicitTargetOnly", true);
        SetVector2(serialized, "animationSpeedRange", new Vector2(0.88f, 1.35f));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(agent);
    }

    private static void ConfigureDamageable(RetroDamageable damageable)
    {
        SerializedObject serialized = new(damageable);
        SetFloat(serialized, "maxHealth", 8f);
        SetBool(serialized, "destroyOnDeath", true);
        SetBool(serialized, "disableRenderersOnDeath", true);
        SetBool(serialized, "disableCollidersOnDeath", true);
        SetFloat(serialized, "destroyDelay", 0f);
        SetObject(serialized, "bloodSplatterSprite", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Effects/BloodSplatter_Impact.png"));
        SetObject(serialized, "bloodSpraySprite", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Effects/BloodSpray_Droplets.png"));
        SetColor(serialized, "bloodColor", new Color(0.18f, 0.02f, 0.012f, 0.78f));
        SetBool(serialized, "spawnBloodOnHit", true);
        SetBool(serialized, "spawnBloodOnDeath", true);
        SetBool(serialized, "ensureShootableFeedback", true);
        SetEnum(serialized, "shootableSurfaceKind", (int)RetroShootableSurfaceKind.Bird);
        SetFloat(serialized, "shootableFeedbackScale", 0.42f);
        SetFloat(serialized, "shootableDeathEffectMultiplier", 0.82f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(damageable);
    }

    private static void DrawAssetStatus(string label, string[] paths)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];
            bool exists = AssetDatabase.IsValidFolder(path) || File.Exists(path);
            EditorGUILayout.LabelField(exists ? "OK" : "Missing", path);
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

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
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

    private static void SetColor(SerializedObject target, string propertyName, Color value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.colorValue = value;
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
