using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class RetroMotocrossBuilder
{
    private const string BikeFramesFolderPath = "Assets/_Project/Art/Sprites/NPCs/Motocross/Frames";
    private const string BikeGeneratedFolderPath = "Assets/_Project/Art/Sprites/NPCs/Motocross/Generated";
    private const string BikePrefabPath = BikeGeneratedFolderPath + "/Motocross.prefab";
    private const string BikeAssetName = "Motocross";
    private const string MountedFramesFolderPath = "Assets/_Project/Art/Sprites/NPCs/MotocrossMerchant/Frames";
    private const string MountedGeneratedFolderPath = "Assets/_Project/Art/Sprites/NPCs/MotocrossMerchant/Generated";
    private const string MountedPrefabPath = MountedGeneratedFolderPath + "/MotocrossMerchant.prefab";
    private const string MountedAssetName = "MotocrossMerchant";
    private const string MerchantPrefabPath = "Assets/_Project/Art/Sprites/NPCs/Merchant/Generated/Merchant.prefab";
    private const string FirstPersonFramesFolderPath = "Assets/_Project/Art/Sprites/NPCs/Motocross/FirstPerson/Frames";

    [MenuItem("Tools/Ultraloud/Entities/Build Motocrosses")]
    public static void BuildOrReplace()
    {
        EnsureFolder(BikeGeneratedFolderPath);
        EnsureFolder(MountedGeneratedFolderPath);

        DirectionalSpriteFrameBuildResult bikeBuild = BuildDirectionalSprite(BikeFramesFolderPath, BikeGeneratedFolderPath, BikeAssetName);
        DirectionalSpriteFrameBuildResult mountedBuild = BuildDirectionalSprite(MountedFramesFolderPath, MountedGeneratedFolderPath, MountedAssetName);
        ConfigureDefinition(bikeBuild.definition, BikeAssetName);
        ConfigureDefinition(mountedBuild.definition, MountedAssetName);
        Sprite[] firstPersonFrames = LoadFirstPersonFrames();

        ConfigureBikePrefab(bikeBuild.prefabAsset, bikeBuild.definition, mountedBuild.definition, firstPersonFrames);
        ConfigureMountedPreviewPrefab(mountedBuild.prefabAsset);
        ConfigureMerchantRiderDefaults();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Motocrosses rebuilt. Riderless prefab='{BikePrefabPath}', mounted merchant sprite prefab='{MountedPrefabPath}'.", bikeBuild.prefabAsset);
    }

    private static DirectionalSpriteFrameBuildResult BuildDirectionalSprite(string framesFolderPath, string generatedFolderPath, string assetName)
    {
        DefaultAsset framesFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(framesFolderPath);
        DefaultAsset generatedFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(generatedFolderPath);
        if (framesFolder == null || generatedFolder == null)
        {
            throw new System.InvalidOperationException($"Motocross builder expected '{framesFolderPath}' and '{generatedFolderPath}' to exist.");
        }

        return DirectionalSpriteFrameBuilder.Build(
            framesFolder,
            generatedFolder,
            new DirectionalSpriteFrameBuildOptions
            {
                assetName = assetName,
                buildPrefab = true,
                instantiateInScene = false,
                addLocomotion = false,
                worldScaleMultiplier = 1f
            });
    }

    private static void ConfigureDefinition(DirectionalSpriteDefinition definition, string assetName)
    {
        if (definition == null || definition.clips == null)
        {
            return;
        }

        definition.name = assetName;
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
                "Wheelie" => 12.5f,
                "Ride" => 13.5f,
                "Idle" => 0.75f,
                _ => 1f
            };
        }

        EditorUtility.SetDirty(definition);
    }

    private static void ConfigureBikePrefab(
        GameObject prefab,
        DirectionalSpriteDefinition bikeDefinition,
        DirectionalSpriteDefinition mountedDefinition,
        Sprite[] firstPersonFrames)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(BikePrefabPath);
        try
        {
            root.name = BikeAssetName;
            Transform quad = root.transform.Find("Quad");
            MeshRenderer quadRenderer = quad != null ? quad.GetComponent<MeshRenderer>() : root.GetComponentInChildren<MeshRenderer>(true);
            ConfigureVisual(quad, quadRenderer);

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

            BoxCollider collider = GetOrAdd<BoxCollider>(root);
            collider.center = new Vector3(0f, 1.02f, 0f);
            collider.size = new Vector3(1.45f, 2.05f, 2.45f);
            collider.isTrigger = false;

            Rigidbody body = GetOrAdd<Rigidbody>(root);
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            RetroDamageable damageable = GetOrAdd<RetroDamageable>(root);
            ConfigureDamageable(damageable);

            Transform seat = EnsureChild(root.transform, "SeatAnchor", new Vector3(0f, 1.28f, -0.04f));
            Transform dismount = EnsureChild(root.transform, "DismountAnchor", new Vector3(1.28f, 0.08f, -0.12f));

            RetroHorseMount mount = GetOrAdd<RetroHorseMount>(root);
            ConfigureMount(mount, damageable, animator, body, quadRenderer, quad, seat, dismount, bikeDefinition, mountedDefinition, firstPersonFrames);

            PrefabUtility.SaveAsPrefabAsset(root, BikePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureMountedPreviewPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(MountedPrefabPath);
        try
        {
            root.name = MountedAssetName;
            Transform quad = root.transform.Find("Quad");
            MeshRenderer quadRenderer = quad != null ? quad.GetComponent<MeshRenderer>() : root.GetComponentInChildren<MeshRenderer>(true);
            ConfigureVisual(quad, quadRenderer);

            DirectionalSpriteAnimator animator = GetOrAdd<DirectionalSpriteAnimator>(root);
            ConfigureAnimator(animator, quad);

            DirectionalSpriteBillboardLitRenderer litRenderer = GetOrAdd<DirectionalSpriteBillboardLitRenderer>(root);
            ConfigureLitRenderer(litRenderer, animator, quadRenderer);

            DirectionalSpriteHitMask hitMask = GetOrAdd<DirectionalSpriteHitMask>(root);
            ConfigureHitMask(hitMask, animator, quadRenderer, quad);

            BoxCollider collider = GetOrAdd<BoxCollider>(root);
            collider.center = new Vector3(0f, 1.25f, 0f);
            collider.size = new Vector3(1.65f, 2.5f, 2.65f);

            RetroDamageable damageable = GetOrAdd<RetroDamageable>(root);
            ConfigureDamageable(damageable);

            PrefabUtility.SaveAsPrefabAsset(root, MountedPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureMerchantRiderDefaults()
    {
        GameObject merchantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MerchantPrefabPath);
        if (merchantPrefab == null)
        {
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(MerchantPrefabPath);
        try
        {
            RetroHorseNpcRider rider = GetOrAdd<RetroHorseNpcRider>(root);
            SerializedObject serialized = new(rider);
            SetBool(serialized, "preferMountDefaultMountedDefinition", true);
            SetFloat(serialized, "searchRadius", 26f);
            SetFloat(serialized, "searchInterval", 0.95f);
            SetBool(serialized, "autoMountOnEnable", false);
            SetFloat(serialized, "targetSearchRadius", 38f);
            SetFloat(serialized, "chaseDistance", 8.2f);
            SetFloat(serialized, "orbitDistance", 3.8f);
            SetFloat(serialized, "wanderThrottle", 0.46f);
            SetFloat(serialized, "chaseThrottle", 0.95f);
            SetFloat(serialized, "chaos", 0.38f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rider);

            PrefabUtility.SaveAsPrefabAsset(root, MerchantPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureVisual(Transform quad, MeshRenderer renderer)
    {
        if (quad != null)
        {
            float y = Mathf.Max(1.55f, quad.localScale.y * 0.5f - 0.04f);
            quad.localPosition = new Vector3(0f, y, 0f);
            quad.localRotation = Quaternion.identity;
        }

        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private static void ConfigureAnimator(DirectionalSpriteAnimator animator, Transform quad)
    {
        SerializedObject serialized = new(animator);
        serialized.FindProperty("initialClipId").stringValue = "Idle";
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
        SetFloat(serialized, "alphaCutoff", 0.08f);
        SetFloat(serialized, "normalScale", 0.95f);
        SetFloat(serialized, "detailNormalInfluence", 0.52f);
        SetFloat(serialized, "macroNormalBend", 0.55f);
        SetFloat(serialized, "spriteAngleLightingInfluence", 0.32f);
        SetFloat(serialized, "wrapDiffuse", 0.34f);
        SetFloat(serialized, "ambientIntensity", 0.9f);
        SetFloat(serialized, "renderSettingsAmbientScale", 0.2f);
        SetFloat(serialized, "surfaceRoughness", 0.94f);
        SetFloat(serialized, "specularStrength", 0.03f);
        SetFloat(serialized, "rimStrength", 0.03f);
        SetFloat(serialized, "rimPower", 4.4f);
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
        SetFloat(serialized, "maxHealth", 135f);
        SetBool(serialized, "destroyOnDeath", true);
        SetBool(serialized, "disableRenderersOnDeath", true);
        SetBool(serialized, "disableCollidersOnDeath", true);
        SetFloat(serialized, "destroyDelay", 0.12f);
        SetObject(serialized, "bloodSplatterSprite", null);
        SetObject(serialized, "bloodSpraySprite", null);
        SetColor(serialized, "bloodColor", new Color(0.42f, 0.28f, 0.16f, 0.88f));
        SetBool(serialized, "spawnBloodOnHit", false);
        SetBool(serialized, "spawnBloodOnDeath", false);
        SetBool(serialized, "ensureShootableFeedback", true);
        SetEnum(serialized, "shootableSurfaceKind", (int)RetroShootableSurfaceKind.Metal);
        SetFloat(serialized, "shootableFeedbackScale", 0.95f);
        SetFloat(serialized, "shootableDeathEffectMultiplier", 2.35f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(damageable);
    }

    private static void ConfigureMount(
        RetroHorseMount mount,
        RetroDamageable damageable,
        DirectionalSpriteAnimator animator,
        Rigidbody body,
        Renderer visualRenderer,
        Transform visualRoot,
        Transform seat,
        Transform dismount,
        DirectionalSpriteDefinition riderlessDefinition,
        DirectionalSpriteDefinition mountedDefinition,
        Sprite[] firstPersonFrames)
    {
        SerializedObject serialized = new(mount);
        SetBool(serialized, "interactionEnabled", true);
        SetString(serialized, "interactionName", "Motocross");
        SetString(serialized, "interactionVerb", "Ride");
        SetFloat(serialized, "interactionMaxDistance", 4.5f);
        SetInt(serialized, "interactionPriority", 35);
        SetString(serialized, "mountDisplayName", "Motocross");
        serialized.FindProperty("damageable").objectReferenceValue = damageable;
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("movementBody").objectReferenceValue = body;
        serialized.FindProperty("visualRenderer").objectReferenceValue = visualRenderer;
        serialized.FindProperty("visualRoot").objectReferenceValue = visualRoot;
        serialized.FindProperty("seatAnchor").objectReferenceValue = seat;
        serialized.FindProperty("dismountAnchor").objectReferenceValue = dismount;
        serialized.FindProperty("riderlessDefinition").objectReferenceValue = riderlessDefinition;
        serialized.FindProperty("defaultMountedNpcDefinition").objectReferenceValue = mountedDefinition;

        SerializedProperty framesProperty = serialized.FindProperty("firstPersonRidingFrames");
        framesProperty.arraySize = firstPersonFrames != null ? firstPersonFrames.Length : 0;
        for (int i = 0; i < framesProperty.arraySize; i++)
        {
            framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = firstPersonFrames[i];
        }

        SetString(serialized, "idleClipId", "Idle");
        SetString(serialized, "walkClipId", "Ride");
        SetString(serialized, "gallopClipId", "Wheelie");
        SetFloat(serialized, "maxLookYawOffset", 150f);
        SetFloat(serialized, "walkSpeed", 8.8f);
        SetFloat(serialized, "gallopSpeed", 17.4f);
        SetFloat(serialized, "reverseSpeed", 4.4f);
        SetFloat(serialized, "acceleration", 58f);
        SetFloat(serialized, "braking", 66f);
        SetFloat(serialized, "turnSpeed", 345f);
        SetFloat(serialized, "emptyWanderSpeed", 0f);
        SetBool(serialized, "wanderWhenEmpty", false);
        SetFloat(serialized, "cameraBobAmplitude", 0.075f);
        SetFloat(serialized, "cameraBobFrequency", 17f);
        SetFloat(serialized, "cameraRollDegrees", 7.5f);
        SetFloat(serialized, "gallopFovBoost", 12.5f);
        serialized.FindProperty("cameraMountedLocalOffset").vector3Value = new Vector3(0f, -0.04f, 0.08f);
        serialized.FindProperty("firstPersonOverlayLocalPosition").vector3Value = new Vector3(0f, -0.32f, 0.84f);
        serialized.FindProperty("firstPersonOverlaySize").vector2Value = new Vector2(0.94f, 0.94f);
        SetFloat(serialized, "firstPersonFrameDuration", 0.045f);
        SetFloat(serialized, "impactDamage", 28f);
        SetFloat(serialized, "impactRadius", 1.22f);
        SetFloat(serialized, "impactForwardOffset", 1.45f);
        SetFloat(serialized, "impactKnockback", 12f);
        SetFloat(serialized, "dustTrailInterval", 0.04f);
        SetColor(serialized, "dustTrailColor", new Color(0.86f, 0.48f, 0.2f, 0.58f));
        SetColor(serialized, "speedRimColor", new Color(0.9f, 0.84f, 0.68f, 1f));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mount);
    }

    private static Sprite[] LoadFirstPersonFrames()
    {
        Sprite[] frames = new Sprite[4];
        for (int i = 0; i < frames.Length; i++)
        {
            string path = $"{FirstPersonFramesFolderPath}/MotocrossRidingView_{i:D2}.png";
            EnsureSpriteImport(path, 100f);
            frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        return frames;
    }

    private static void EnsureSpriteImport(string assetPath, float pixelsPerUnit)
    {
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
        {
            return;
        }

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (!Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit))
        {
            importer.spritePixelsPerUnit = pixelsPerUnit;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static Transform EnsureChild(Transform parent, string name, Vector3 localPosition)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            GameObject childObject = new(name);
            child = childObject.transform;
            child.SetParent(parent, false);
        }

        child.localPosition = localPosition;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        return child;
    }

    private static void EnsureFolder(string assetFolderPath)
    {
        string normalizedPath = assetFolderPath.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(normalizedPath))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
        string folderName = System.IO.Path.GetFileName(normalizedPath);
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
        {
            throw new System.InvalidOperationException($"Invalid folder path '{assetFolderPath}'.");
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
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

    private static void SetInt(SerializedObject serialized, string propertyName, int value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.intValue = value;
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
