using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class RetroSkeletonMotocrossBuilder
{
    private const string FramesFolderPath = "Assets/Sprites/NPCs/SkeletonMotocross/Frames";
    private const string GeneratedFolderPath = "Assets/Sprites/NPCs/SkeletonMotocross/Generated";
    private const string PrefabPath = GeneratedFolderPath + "/SkeletonMotocross.prefab";
    private const string AssetName = "SkeletonMotocross";
    private const float VisualGroundOffset = 3.2f;

    [MenuItem("Tools/Ultraloud/Entities/Build Skeleton Motocross Rider")]
    public static void BuildOrReplace()
    {
        DefaultAsset framesFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(FramesFolderPath);
        DefaultAsset generatedFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(GeneratedFolderPath);
        if (framesFolder == null || generatedFolder == null)
        {
            Debug.LogError("Skeleton motocross folders are missing. Expected Frames and Generated folders under Assets/Sprites/NPCs/SkeletonMotocross.");
            return;
        }

        DirectionalSpriteFrameBuildResult build = DirectionalSpriteFrameBuilder.Build(
            framesFolder,
            generatedFolder,
            new DirectionalSpriteFrameBuildOptions
            {
                assetName = AssetName,
                buildPrefab = true,
                instantiateInScene = false,
                addLocomotion = false,
                worldScaleMultiplier = 1f
            });

        ConfigureDefinition(build.definition);
        ConfigurePrefab(build.prefabAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Skeleton motocross rider rebuilt at {PrefabPath}.", build.prefabAsset);
    }

    private static void ConfigureDefinition(DirectionalSpriteDefinition definition)
    {
        if (definition == null || definition.clips == null)
        {
            return;
        }

        definition.name = AssetName;
        definition.defaultClipId = "Idle";
        foreach (DirectionalSpriteClip clip in definition.clips)
        {
            if (clip == null)
            {
                continue;
            }

            clip.loop = true;
            clip.framesPerSecond = clip.clipId switch
            {
                "Ride" => 13f,
                "Wheelie" => 11f,
                "Attack" => 12f,
                "Idle" => 1.05f,
                _ => 1f
            };
        }

        EditorUtility.SetDirty(definition);
    }

    private static void ConfigurePrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            root.name = AssetName;
            Transform quad = root.transform.Find("Quad");
            MeshRenderer quadRenderer = quad != null ? quad.GetComponent<MeshRenderer>() : root.GetComponentInChildren<MeshRenderer>(true);
            if (quad != null)
            {
                quad.localPosition = new Vector3(0f, VisualGroundOffset, 0f);
                quad.localRotation = Quaternion.identity;
            }

            if (quadRenderer != null)
            {
                quadRenderer.shadowCastingMode = ShadowCastingMode.On;
                quadRenderer.receiveShadows = true;
            }

            DirectionalSpriteLocomotion locomotion = root.GetComponent<DirectionalSpriteLocomotion>();
            if (locomotion != null)
            {
                Object.DestroyImmediate(locomotion, true);
            }

            DirectionalSpriteAnimator animator = GetOrAdd<DirectionalSpriteAnimator>(root);
            ConfigureAnimator(animator, quad);

            DirectionalSpriteBillboardLitRenderer litRenderer = GetOrAdd<DirectionalSpriteBillboardLitRenderer>(root);
            ConfigureLitRenderer(litRenderer, animator, quadRenderer);

            DirectionalSpriteHitMask hitMask = GetOrAdd<DirectionalSpriteHitMask>(root);
            ConfigureHitMask(hitMask, animator, quadRenderer, quad);

            BoxCollider box = GetOrAdd<BoxCollider>(root);
            box.center = new Vector3(0f, 2.65f, 0f);
            box.size = new Vector3(5.7f, 5.3f, 2.6f);
            box.isTrigger = false;

            Rigidbody body = GetOrAdd<Rigidbody>(root);
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            RetroDamageable damageable = GetOrAdd<RetroDamageable>(root);
            ConfigureDamageable(damageable);

            RetroGibOnDeath gib = GetOrAdd<RetroGibOnDeath>(root);
            ConfigureGib(gib, damageable);

            RetroSkeletonMotocrossRider rider = GetOrAdd<RetroSkeletonMotocrossRider>(root);
            ConfigureRider(rider, damageable, animator, body, quadRenderer, quad);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureAnimator(DirectionalSpriteAnimator animator, Transform quad)
    {
        SerializedObject serialized = new(animator);
        serialized.FindProperty("initialClipId").stringValue = "Idle";
        serialized.FindProperty("playOnEnable").boolValue = true;
        serialized.FindProperty("animationSpeed").floatValue = 1.16f;
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
        SetFloat(serialized, "alphaCutoff", 0.08f);
        SetFloat(serialized, "normalScale", 0.9f);
        SetFloat(serialized, "detailNormalInfluence", 0.5f);
        SetFloat(serialized, "macroNormalBend", 0.55f);
        SetFloat(serialized, "spriteAngleLightingInfluence", 0.32f);
        SetFloat(serialized, "wrapDiffuse", 0.34f);
        SetFloat(serialized, "ambientIntensity", 0.9f);
        SetFloat(serialized, "renderSettingsAmbientScale", 0.2f);
        SetFloat(serialized, "surfaceRoughness", 0.94f);
        SetFloat(serialized, "specularStrength", 0.035f);
        SetFloat(serialized, "rimStrength", 0.035f);
        SetFloat(serialized, "rimPower", 4.2f);
        SetColor(serialized, "rimColor", new Color(0.92f, 0.86f, 0.74f, 1f));
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
        serialized.FindProperty("edgePaddingPixels").intValue = 3;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hitMask);
    }

    private static void ConfigureDamageable(RetroDamageable damageable)
    {
        SerializedObject serialized = new(damageable);
        SetFloat(serialized, "maxHealth", 220f);
        SetBool(serialized, "destroyOnDeath", true);
        SetBool(serialized, "disableRenderersOnDeath", true);
        SetBool(serialized, "disableCollidersOnDeath", true);
        SetFloat(serialized, "destroyDelay", 0.15f);
        SetObject(serialized, "bloodSplatterSprite", null);
        SetObject(serialized, "bloodSpraySprite", null);
        SetColor(serialized, "bloodColor", new Color(0.72f, 0.66f, 0.49f, 0.88f));
        SetBool(serialized, "spawnBloodOnHit", false);
        SetBool(serialized, "spawnBloodOnDeath", false);
        SetBool(serialized, "ensureShootableFeedback", true);
        SetEnum(serialized, "shootableSurfaceKind", (int)RetroShootableSurfaceKind.Bone);
        SetFloat(serialized, "shootableFeedbackScale", 1.05f);
        SetFloat(serialized, "shootableDeathEffectMultiplier", 2.75f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(damageable);
    }

    private static void ConfigureGib(RetroGibOnDeath gib, RetroDamageable damageable)
    {
        SerializedObject serialized = new(gib);
        serialized.FindProperty("damageable").objectReferenceValue = damageable;
        serialized.FindProperty("goreProfile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RetroGoreProfile>("Assets/Sprites/Effects/Gore/PigGoreProfile.asset");
        SetFloat(serialized, "intensityMultiplier", 0.74f);
        SetFloat(serialized, "minimumRecentDamage", 60f);
        SetFloat(serialized, "highSingleHitDamage", 76f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gib);
    }

    private static void ConfigureRider(
        RetroSkeletonMotocrossRider rider,
        RetroDamageable damageable,
        DirectionalSpriteAnimator animator,
        Rigidbody body,
        Renderer renderer,
        Transform quad)
    {
        SerializedObject serialized = new(rider);
        serialized.FindProperty("damageable").objectReferenceValue = damageable;
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("movementBody").objectReferenceValue = body;
        serialized.FindProperty("visualRenderer").objectReferenceValue = renderer;
        serialized.FindProperty("visualRoot").objectReferenceValue = quad;
        SetString(serialized, "idleClipId", "Idle");
        SetString(serialized, "rideClipId", "Ride");
        SetString(serialized, "wheelieClipId", "Wheelie");
        SetString(serialized, "attackClipId", "Attack");
        SetFloat(serialized, "awarenessRadius", 34f);
        SetFloat(serialized, "retargetInterval", 0.72f);
        SetFloat(serialized, "randomTargetChance", 0.68f);
        SetBool(serialized, "attackAnythingDamageable", true);
        SetFloat(serialized, "cruiseSpeed", 7.8f);
        SetFloat(serialized, "burstSpeed", 14.2f);
        SetFloat(serialized, "reversePanicSpeed", 4.5f);
        SetFloat(serialized, "acceleration", 44f);
        SetFloat(serialized, "turnSpeed", 820f);
        SetFloat(serialized, "orbitRadius", 7.4f);
        SetFloat(serialized, "wanderRadius", 13f);
        SetFloat(serialized, "wheelieChance", 0.28f);
        SetFloat(serialized, "donutChance", 0.2f);
        SetFloat(serialized, "ramDamage", 31f);
        SetFloat(serialized, "ramRadius", 2.45f);
        SetFloat(serialized, "ramForwardOffset", 2.25f);
        SetFloat(serialized, "ramCooldown", 0.36f);
        SetFloat(serialized, "ramKnockback", 11f);
        SetFloat(serialized, "bobAmplitude", 0.17f);
        SetFloat(serialized, "bobFrequency", 15f);
        SetFloat(serialized, "leanDegrees", 16f);
        SetFloat(serialized, "exhaustTrailInterval", 0.052f);
        SetColor(serialized, "speedRimColor", new Color(0.9f, 0.84f, 0.68f, 1f));
        SetColor(serialized, "wheelieRimColor", new Color(1f, 0.9f, 0.58f, 1f));
        SetColor(serialized, "exhaustTrailColor", new Color(1f, 0.26f, 0.02f, 0.88f));
        SetColor(serialized, "boneDustTrailColor", new Color(0.74f, 0.68f, 0.48f, 0.6f));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rider);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void SetFloat(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetBool(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetString(SerializedObject serialized, string propertyName, string value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }

    private static void SetColor(SerializedObject serialized, string propertyName, Color value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.colorValue = value;
        }
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetEnum(SerializedObject serialized, string propertyName, int value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.enumValueIndex = value;
        }
    }
}
