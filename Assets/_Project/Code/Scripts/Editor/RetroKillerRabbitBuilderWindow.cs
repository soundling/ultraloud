using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class RetroKillerRabbitBuilderWindow : EditorWindow
{
    private const string ArtRootPath = "Assets/_Project/Art/Sprites/Entities/KillerRabbit";
    private const string FramesRootPath = ArtRootPath + "/Frames";
    private const string GeneratedRootPath = ArtRootPath + "/Generated";
    private const string SpriteAssetName = "KillerRabbitSprite";
    private const string SpritePrefabPath = GeneratedRootPath + "/" + SpriteAssetName + ".prefab";
    private const string DefinitionPath = GeneratedRootPath + "/" + SpriteAssetName + ".asset";
    private const string ContentRootPath = "Assets/_Project/Content/Actors/KillerRabbit";
    private const string PrefabRootPath = ContentRootPath + "/Prefabs";
    private const string ProfileRootPath = ContentRootPath + "/Profiles";
    private const string RabbitPrefabPath = PrefabRootPath + "/KillerRabbit.prefab";
    private const string GoreProfilePath = ProfileRootPath + "/KillerRabbitGoreProfile.asset";

    [MenuItem("Tools/Ultraloud/Nature/Killer Rabbit Builder")]
    private static void Open()
    {
        GetWindow<RetroKillerRabbitBuilderWindow>("Killer Rabbit");
    }

    [MenuItem("Tools/Ultraloud/Nature/Killer Rabbit Builder/Build All Assets")]
    public static void BuildAllAssetsMenu()
    {
        BuildAllAssets(selectAsset: true);
    }

    [MenuItem("GameObject/Ultraloud/Nature/Killer Rabbit", false, 15)]
    public static void CreateSceneRabbit(MenuCommand command)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RabbitPrefabPath);
        if (prefab == null)
        {
            prefab = BuildAllAssets(selectAsset: false);
        }

        GameObject instance = prefab != null
            ? PrefabUtility.InstantiatePrefab(prefab) as GameObject
            : new GameObject("KillerRabbit");

        if (instance == null)
        {
            return;
        }

        if (command.context is GameObject parent)
        {
            Undo.SetTransformParent(instance.transform, parent.transform, "Create Killer Rabbit");
        }

        Undo.RegisterCreatedObjectUndo(instance, "Create Killer Rabbit");
        Selection.activeObject = instance;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Killer Rabbit Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Builds the generated multi-angle rabbit frames into a fast leaping enemy prefab with rapid bite attacks and explicit gore bursts on every successful hit.",
            MessageType.Info);

        DrawAssetStatus("Frames", new[] { FramesRootPath });
        DrawAssetStatus("Generated", new[] { DefinitionPath, SpritePrefabPath, RabbitPrefabPath, GoreProfilePath });

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Build Killer Rabbit Prefab", GUILayout.Height(32f)))
        {
            BuildAllAssets(selectAsset: true);
        }
    }

    private static GameObject BuildAllAssets(bool selectAsset)
    {
        EnsureAssetFolder(GeneratedRootPath);
        EnsureAssetFolder(PrefabRootPath);
        EnsureAssetFolder(ProfileRootPath);
        AssetDatabase.Refresh();

        DefaultAsset framesRoot = AssetDatabase.LoadAssetAtPath<DefaultAsset>(FramesRootPath);
        DefaultAsset outputRoot = AssetDatabase.LoadAssetAtPath<DefaultAsset>(GeneratedRootPath);
        if (framesRoot == null || outputRoot == null)
        {
            Debug.LogError($"Killer rabbit frames are missing. Expected frames at {FramesRootPath}.");
            return null;
        }

        DirectionalSpriteFrameBuildResult result = DirectionalSpriteFrameBuilder.Build(
            framesRoot,
            outputRoot,
            new DirectionalSpriteFrameBuildOptions
            {
                assetName = SpriteAssetName,
                buildPrefab = true,
                instantiateInScene = false,
                addLocomotion = false,
                worldScaleMultiplier = 0.58f
            });

        ConfigureDefinition(result.definition);
        RetroGoreProfile goreProfile = CreateOrUpdateGoreProfile();
        GameObject rabbitPrefab = CreateOrUpdateRabbitPrefab(result.definition, goreProfile);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (selectAsset && rabbitPrefab != null)
        {
            Selection.activeObject = rabbitPrefab;
            EditorGUIUtility.PingObject(rabbitPrefab);
        }

        return rabbitPrefab;
    }

    private static void ConfigureDefinition(DirectionalSpriteDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        definition.name = SpriteAssetName;
        definition.defaultClipId = "Sprint";
        if (definition.clips != null)
        {
            for (int i = 0; i < definition.clips.Count; i++)
            {
                DirectionalSpriteClip clip = definition.clips[i];
                if (clip == null)
                {
                    continue;
                }

                clip.loop = true;
                clip.framesPerSecond = clip.clipId switch
                {
                    "Attack" => 20f,
                    "Sprint" => 14f,
                    _ => 12f
                };
            }
        }

        EditorUtility.SetDirty(definition);
    }

    private static RetroGoreProfile CreateOrUpdateGoreProfile()
    {
        RetroGoreProfile profile = AssetDatabase.LoadAssetAtPath<RetroGoreProfile>(GoreProfilePath);
        if (profile == null)
        {
            profile = CreateInstance<RetroGoreProfile>();
            AssetDatabase.CreateAsset(profile, GoreProfilePath);
        }

        SerializedObject serialized = new(profile);
        SetObject(serialized, "baseAtlas", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Sprites/Effects/Gore/GoreBurst_Atlas_Base.png"));
        SetObject(serialized, "normalAtlas", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Sprites/Effects/Gore/GoreBurst_Atlas_Normal.png"));
        SetObject(serialized, "packedMasksAtlas", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Sprites/Effects/Gore/GoreBurst_Atlas_Masks.png"));
        SetObject(serialized, "emissionAtlas", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Sprites/Effects/Gore/GoreBurst_Atlas_Emission.png"));
        SetInt(serialized, "atlasColumns", 4);
        SetInt(serialized, "atlasRows", 4);
        SetInt(serialized, "frameCount", 16);
        SetFloat(serialized, "damageWindow", 0.72f);
        SetFloat(serialized, "clusterWindow", 0.12f);
        SetFloat(serialized, "minimumRecentDamage", 26f);
        SetFloat(serialized, "minimumClusterDamage", 18f);
        SetInt(serialized, "minimumClusterHits", 1);
        SetFloat(serialized, "highSingleHitDamage", 34f);
        SetFloat(serialized, "gibCooldown", 0.02f);
        SetInt(serialized, "bloodPuffCount", 36);
        SetInt(serialized, "streakCount", 34);
        SetInt(serialized, "spriteChunkCount", 20);
        SetInt(serialized, "boneSpriteCount", 8);
        SetInt(serialized, "decalCount", 24);
        SetVector2(serialized, "puffSizeRange", new Vector2(0.46f, 1.28f));
        SetVector2(serialized, "streakSizeRange", new Vector2(0.42f, 1.72f));
        SetVector2(serialized, "chunkSizeRange", new Vector2(0.18f, 0.62f));
        SetVector2(serialized, "decalSizeRange", new Vector2(0.78f, 3.2f));
        SetVector2(serialized, "spriteLifetimeRange", new Vector2(0.5f, 1.35f));
        SetVector2(serialized, "decalLifetimeRange", new Vector2(9f, 22f));
        SetInt(serialized, "meshChunkCount", 18);
        SetVector2(serialized, "meshChunkSizeRange", new Vector2(0.045f, 0.18f));
        SetVector2(serialized, "meshChunkLifetimeRange", new Vector2(2.4f, 5.8f));
        SetColor(serialized, "meatChunkColor", new Color(0.54f, 0.008f, 0.012f, 1f));
        SetColor(serialized, "skinChunkColor", new Color(0.94f, 0.86f, 0.78f, 1f));
        SetColor(serialized, "boneChunkColor", new Color(0.96f, 0.86f, 0.64f, 1f));
        SetVector2(serialized, "radialSpeedRange", new Vector2(5.8f, 18f));
        SetVector2(serialized, "upwardSpeedRange", new Vector2(1.1f, 7.5f));
        SetVector2(serialized, "gravityRange", new Vector2(8f, 18f));
        SetFloat(serialized, "forwardBias", 0.86f);
        SetFloat(serialized, "intensityScale", 1.45f);
        SetFloat(serialized, "spawnRadius", 0.22f);
        SetFloat(serialized, "screenFlashRadius", 1.2f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static GameObject CreateOrUpdateRabbitPrefab(DirectionalSpriteDefinition definition, RetroGoreProfile goreProfile)
    {
        GameObject spritePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpritePrefabPath);
        if (spritePrefab == null || definition == null)
        {
            return null;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(SpritePrefabPath);
        try
        {
            root.name = "KillerRabbit";
            Transform quad = root.transform.Find("Quad");
            MeshRenderer quadRenderer = quad != null ? quad.GetComponent<MeshRenderer>() : root.GetComponentInChildren<MeshRenderer>(true);
            if (quadRenderer != null)
            {
                quadRenderer.shadowCastingMode = ShadowCastingMode.On;
                quadRenderer.receiveShadows = true;
            }

            DirectionalSpriteLocomotion locomotion = root.GetComponent<DirectionalSpriteLocomotion>();
            if (locomotion != null)
            {
                DestroyImmediate(locomotion, true);
            }

            DirectionalSpriteAnimator animator = GetOrAdd<DirectionalSpriteAnimator>(root);
            ConfigureAnimator(animator, definition, quad);

            DirectionalSpriteBillboardLitRenderer litRenderer = GetOrAdd<DirectionalSpriteBillboardLitRenderer>(root);
            ConfigureLitRenderer(litRenderer, animator, quadRenderer);

            DirectionalSpriteHitMask hitMask = GetOrAdd<DirectionalSpriteHitMask>(root);
            ConfigureHitMask(hitMask, animator, quadRenderer, quad);

            CapsuleCollider collider = GetOrAdd<CapsuleCollider>(root);
            collider.center = new Vector3(0f, 0.34f, 0f);
            collider.radius = 0.34f;
            collider.height = 0.78f;
            collider.direction = 1;
            collider.isTrigger = false;

            Rigidbody body = GetOrAdd<Rigidbody>(root);
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            RetroDamageable damageable = GetOrAdd<RetroDamageable>(root);
            ConfigureDamageable(damageable);

            RetroGibOnDeath gib = GetOrAdd<RetroGibOnDeath>(root);
            ConfigureGib(gib, damageable, goreProfile);

            RetroKillerRabbit rabbit = GetOrAdd<RetroKillerRabbit>(root);
            ConfigureRabbit(rabbit, damageable, animator, body, quad, goreProfile);

            return PrefabUtility.SaveAsPrefabAsset(root, RabbitPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureAnimator(DirectionalSpriteAnimator animator, DirectionalSpriteDefinition definition, Transform quad)
    {
        SerializedObject serialized = new(animator);
        serialized.FindProperty("definition").objectReferenceValue = definition;
        serialized.FindProperty("initialClipId").stringValue = "Sprint";
        serialized.FindProperty("playOnEnable").boolValue = true;
        serialized.FindProperty("animationSpeed").floatValue = 1.08f;
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
        SetFloat(serialized, "normalScale", 0.82f);
        SetFloat(serialized, "detailNormalInfluence", 0.52f);
        SetFloat(serialized, "macroNormalBend", 0.44f);
        SetFloat(serialized, "spriteAngleLightingInfluence", 0.24f);
        SetFloat(serialized, "wrapDiffuse", 0.52f);
        SetFloat(serialized, "ambientIntensity", 1.08f);
        SetFloat(serialized, "renderSettingsAmbientScale", 0.22f);
        SetFloat(serialized, "surfaceRoughness", 0.92f);
        SetFloat(serialized, "specularStrength", 0.08f);
        SetFloat(serialized, "rimStrength", 0.12f);
        SetFloat(serialized, "rimPower", 3.4f);
        SetColor(serialized, "rimColor", new Color(1f, 0.88f, 0.78f, 1f));
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

    private static void ConfigureDamageable(RetroDamageable damageable)
    {
        SerializedObject serialized = new(damageable);
        SetFloat(serialized, "maxHealth", 72f);
        SetBool(serialized, "destroyOnDeath", true);
        SetBool(serialized, "disableRenderersOnDeath", true);
        SetBool(serialized, "disableCollidersOnDeath", true);
        SetFloat(serialized, "destroyDelay", 0.04f);
        SetObject(serialized, "bloodSplatterSprite", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Effects/BloodSplatter_Impact.png"));
        SetObject(serialized, "bloodSpraySprite", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Effects/BloodSpray_Droplets.png"));
        SetColor(serialized, "bloodColor", new Color(0.58f, 0.012f, 0.01f, 0.94f));
        SetBool(serialized, "spawnBloodOnHit", true);
        SetBool(serialized, "spawnBloodOnDeath", true);
        SetVector2(serialized, "bloodSplatterScaleRange", new Vector2(0.45f, 0.92f));
        SetInt(serialized, "bloodDecalCount", 6);
        SetInt(serialized, "bloodSprayParticleCount", 18);
        SetVector2(serialized, "bloodSprayScaleRange", new Vector2(0.08f, 0.21f));
        SetVector2(serialized, "bloodSpraySpeedRange", new Vector2(2.2f, 6.8f));
        SetFloat(serialized, "deathBloodScaleMultiplier", 2.7f);
        SetBool(serialized, "ensureShootableFeedback", true);
        SetEnum(serialized, "shootableSurfaceKind", (int)RetroShootableSurfaceKind.Flesh);
        SetFloat(serialized, "shootableFeedbackScale", 0.72f);
        SetFloat(serialized, "shootableDeathEffectMultiplier", 3.4f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(damageable);
    }

    private static void ConfigureGib(RetroGibOnDeath gib, RetroDamageable damageable, RetroGoreProfile goreProfile)
    {
        SerializedObject serialized = new(gib);
        serialized.FindProperty("damageable").objectReferenceValue = damageable;
        serialized.FindProperty("goreProfile").objectReferenceValue = goreProfile;
        SetBool(serialized, "useProfileThresholds", true);
        SetVector3(serialized, "localCenterOffset", new Vector3(0f, 0.34f, 0f));
        SetFloat(serialized, "intensityMultiplier", 1.35f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gib);
    }

    private static void ConfigureRabbit(
        RetroKillerRabbit rabbit,
        RetroDamageable damageable,
        DirectionalSpriteAnimator animator,
        Rigidbody body,
        Transform quad,
        RetroGoreProfile goreProfile)
    {
        SerializedObject serialized = new(rabbit);
        serialized.FindProperty("damageable").objectReferenceValue = damageable;
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("movementBody").objectReferenceValue = body;
        serialized.FindProperty("visualRoot").objectReferenceValue = quad;
        serialized.FindProperty("goreProfile").objectReferenceValue = goreProfile;
        SetBool(serialized, "preferTaggedTarget", true);
        SetString(serialized, "preferredTargetTag", "Player");
        SetFloat(serialized, "targetSearchRadius", 38f);
        SetFloat(serialized, "targetRefreshInterval", 0.22f);
        SetFloat(serialized, "chaseSpeed", 9.6f);
        SetFloat(serialized, "acceleration", 48f);
        SetFloat(serialized, "turnSpeed", 1120f);
        SetFloat(serialized, "stopDistance", 2.65f);
        SetFloat(serialized, "idleWanderRadius", 4.5f);
        SetFloat(serialized, "idleWanderSpeed", 2.2f);
        SetFloat(serialized, "hopAmplitude", 0.13f);
        SetFloat(serialized, "hopFrequency", 14f);
        SetFloat(serialized, "attackDamage", 28f);
        SetFloat(serialized, "attackCooldown", 0.2f);
        SetFloat(serialized, "lungeDuration", 0.28f);
        SetFloat(serialized, "lungeSpeed", 16.5f);
        SetFloat(serialized, "biteWindowStart", 0.18f);
        SetFloat(serialized, "biteWindowEnd", 0.86f);
        SetFloat(serialized, "biteRadius", 0.68f);
        SetFloat(serialized, "biteForwardOffset", 0.52f);
        SetFloat(serialized, "biteHeight", 0.42f);
        SetFloat(serialized, "biteKnockback", 8.4f);
        SetFloat(serialized, "chainAttackChance", 0.9f);
        SetFloat(serialized, "biteGoreIntensity", 1.9f);
        SetFloat(serialized, "biteDecalSize", 0.8f);
        SetString(serialized, "sprintClipId", "Sprint");
        SetString(serialized, "attackClipId", "Attack");
        SetVector2(serialized, "animationSpeedRange", new Vector2(0.96f, 1.24f));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rabbit);
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

    private static void SetFloat(SerializedObject target, string propertyName, float value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
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
