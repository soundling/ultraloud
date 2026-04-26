using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class RetroHorseBuilder
{
    private const string HorseFramesFolderPath = "Assets/Sprites/NPCs/Horse/Frames";
    private const string HorseGeneratedFolderPath = "Assets/Sprites/NPCs/Horse/Generated";
    private const string HorsePrefabPath = HorseGeneratedFolderPath + "/Horse.prefab";
    private const string HorseAssetName = "Horse";
    private const string MountedFramesFolderPath = "Assets/Sprites/NPCs/HorseMerchant/Frames";
    private const string MountedGeneratedFolderPath = "Assets/Sprites/NPCs/HorseMerchant/Generated";
    private const string MountedPrefabPath = MountedGeneratedFolderPath + "/HorseMerchant.prefab";
    private const string MountedAssetName = "HorseMerchant";
    private const string MerchantPrefabPath = "Assets/Sprites/NPCs/Merchant/Generated/Merchant.prefab";
    private const string FirstPersonFramesFolderPath = "Assets/Sprites/NPCs/Horse/FirstPerson/Frames";

    [MenuItem("Tools/Ultraloud/Entities/Build Horses")]
    public static void BuildOrReplace()
    {
        EnsureFolder(HorseGeneratedFolderPath);
        EnsureFolder(MountedGeneratedFolderPath);

        DirectionalSpriteFrameBuildResult horseBuild = BuildDirectionalSprite(HorseFramesFolderPath, HorseGeneratedFolderPath, HorseAssetName, 1f);
        DirectionalSpriteFrameBuildResult mountedBuild = BuildDirectionalSprite(MountedFramesFolderPath, MountedGeneratedFolderPath, MountedAssetName, 1f);
        ConfigureDefinition(horseBuild.definition, HorseAssetName);
        ConfigureDefinition(mountedBuild.definition, MountedAssetName);
        Sprite[] firstPersonFrames = LoadFirstPersonFrames();

        ConfigureHorsePrefab(horseBuild.prefabAsset, horseBuild.definition, mountedBuild.definition, firstPersonFrames);
        ConfigureMountedPreviewPrefab(mountedBuild.prefabAsset);
        ConfigureMerchantPrefab(mountedBuild.definition);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Horse entities rebuilt. Riderless prefab='{HorsePrefabPath}', mounted merchant sprite prefab='{MountedPrefabPath}'.", horseBuild.prefabAsset);
    }

    private static DirectionalSpriteFrameBuildResult BuildDirectionalSprite(string framesFolderPath, string generatedFolderPath, string assetName, float worldScaleMultiplier)
    {
        DefaultAsset framesFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(framesFolderPath);
        DefaultAsset generatedFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(generatedFolderPath);
        if (framesFolder == null || generatedFolder == null)
        {
            throw new System.InvalidOperationException($"Horse builder expected '{framesFolderPath}' and '{generatedFolderPath}' to exist.");
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
                worldScaleMultiplier = worldScaleMultiplier
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
                "Gallop" => 12.5f,
                "Walk" => 8.5f,
                _ => 3.5f
            };
        }

        EditorUtility.SetDirty(definition);
    }

    private static void ConfigureHorsePrefab(
        GameObject prefab,
        DirectionalSpriteDefinition horseDefinition,
        DirectionalSpriteDefinition mountedDefinition,
        Sprite[] firstPersonFrames)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(HorsePrefabPath);
        try
        {
            root.name = HorseAssetName;
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
            collider.center = new Vector3(0f, 1.17f, 0f);
            collider.size = new Vector3(1.8f, 2.35f, 3.1f);
            collider.isTrigger = false;

            Rigidbody body = GetOrAdd<Rigidbody>(root);
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            RetroDamageable damageable = GetOrAdd<RetroDamageable>(root);
            ConfigureDamageable(damageable);

            Transform seat = EnsureChild(root.transform, "SeatAnchor", new Vector3(0f, 1.62f, -0.08f));
            Transform dismount = EnsureChild(root.transform, "DismountAnchor", new Vector3(1.55f, 0.08f, -0.2f));

            RetroHorseMount mount = GetOrAdd<RetroHorseMount>(root);
            ConfigureMount(mount, damageable, animator, body, quadRenderer, quad, seat, dismount, horseDefinition, mountedDefinition, firstPersonFrames);

            PrefabUtility.SaveAsPrefabAsset(root, HorsePrefabPath);
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

            BoxCollider collider = GetOrAdd<BoxCollider>(root);
            collider.center = new Vector3(0f, 1.35f, 0f);
            collider.size = new Vector3(1.95f, 2.7f, 3.25f);

            PrefabUtility.SaveAsPrefabAsset(root, MountedPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureMerchantPrefab(DirectionalSpriteDefinition mountedDefinition)
    {
        GameObject merchantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MerchantPrefabPath);
        if (merchantPrefab == null || mountedDefinition == null)
        {
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(MerchantPrefabPath);
        try
        {
            RetroHorseNpcRider rider = GetOrAdd<RetroHorseNpcRider>(root);
            SerializedObject serialized = new(rider);
            serialized.FindProperty("mountedHorseDefinition").objectReferenceValue = mountedDefinition;
            SetBool(serialized, "preferMountDefaultMountedDefinition", true);
            SetFloat(serialized, "searchRadius", 24f);
            SetFloat(serialized, "searchInterval", 1.1f);
            SetBool(serialized, "autoMountOnEnable", true);
            SetFloat(serialized, "targetSearchRadius", 34f);
            SetFloat(serialized, "targetRefreshInterval", 0.52f);
            SetFloat(serialized, "chaseDistance", 6.2f);
            SetFloat(serialized, "orbitDistance", 3.2f);
            SetFloat(serialized, "wanderThrottle", 0.38f);
            SetFloat(serialized, "chaseThrottle", 0.86f);
            SetFloat(serialized, "chaos", 0.28f);
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
            float y = Mathf.Max(2.2f, quad.localScale.y * 0.5f - 0.05f);
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
        serialized.FindProperty("animationSpeed").floatValue = 1f;
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
        SetFloat(serialized, "normalScale", 1.48f);
        SetFloat(serialized, "detailNormalInfluence", 0.86f);
        SetFloat(serialized, "macroNormalBend", 1.12f);
        SetFloat(serialized, "spriteAngleLightingInfluence", 0.58f);
        SetFloat(serialized, "wrapDiffuse", 0.24f);
        SetFloat(serialized, "ambientIntensity", 0.82f);
        SetFloat(serialized, "renderSettingsAmbientScale", 0.22f);
        SetFloat(serialized, "surfaceRoughness", 0.78f);
        SetFloat(serialized, "specularStrength", 0.25f);
        SetFloat(serialized, "rimStrength", 0.28f);
        SetFloat(serialized, "rimPower", 2.9f);
        SetColor(serialized, "rimColor", new Color(1f, 0.55f, 0.2f, 1f));
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
        SetFloat(serialized, "maxHealth", 180f);
        SetBool(serialized, "destroyOnDeath", true);
        SetBool(serialized, "disableRenderersOnDeath", true);
        SetBool(serialized, "disableCollidersOnDeath", true);
        SetFloat(serialized, "destroyDelay", 0.15f);
        SetColor(serialized, "bloodColor", new Color(0.62f, 0.09f, 0.035f, 0.88f));
        SetBool(serialized, "spawnBloodOnHit", true);
        SetBool(serialized, "spawnBloodOnDeath", true);
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
        SetString(serialized, "mountDisplayName", "Horse");
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
        SetString(serialized, "walkClipId", "Walk");
        SetString(serialized, "gallopClipId", "Gallop");
        SetFloat(serialized, "walkSpeed", 5.4f);
        SetFloat(serialized, "gallopSpeed", 11.2f);
        SetFloat(serialized, "reverseSpeed", 2.2f);
        SetFloat(serialized, "acceleration", 25f);
        SetFloat(serialized, "braking", 34f);
        SetFloat(serialized, "turnSpeed", 190f);
        SetFloat(serialized, "emptyWanderSpeed", 1.65f);
        SetFloat(serialized, "emptyWanderRadius", 8f);
        SetFloat(serialized, "emptyWanderRetargetTime", 2.6f);
        SetBool(serialized, "wanderWhenEmpty", true);
        SetFloat(serialized, "cameraBobAmplitude", 0.11f);
        SetFloat(serialized, "cameraBobFrequency", 10.8f);
        SetFloat(serialized, "gallopFovBoost", 8.5f);
        SetFloat(serialized, "firstPersonFrameDuration", 0.07f);
        SetFloat(serialized, "impactDamage", 20f);
        SetFloat(serialized, "impactRadius", 1.15f);
        SetFloat(serialized, "impactForwardOffset", 1.3f);
        SetFloat(serialized, "impactKnockback", 7.5f);
        SetFloat(serialized, "dustTrailInterval", 0.075f);
        SetColor(serialized, "dustTrailColor", new Color(0.72f, 0.62f, 0.43f, 0.52f));
        SetColor(serialized, "speedRimColor", new Color(1f, 0.58f, 0.18f, 1f));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mount);
    }

    private static Sprite[] LoadFirstPersonFrames()
    {
        Sprite[] frames = new Sprite[4];
        for (int i = 0; i < frames.Length; i++)
        {
            string path = $"{FirstPersonFramesFolderPath}/HorseRidingView_{i:D2}.png";
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

    private static void SetColor(SerializedObject serialized, string propertyName, Color value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.colorValue = value;
        }
    }
}
