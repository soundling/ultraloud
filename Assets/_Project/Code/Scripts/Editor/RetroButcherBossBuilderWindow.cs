using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class RetroButcherBossBuilderWindow : EditorWindow
{
    private const string ArtRootPath = "Assets/_Project/Art/Sprites/Entities/ButcherBoss";
    private const string FramesRootPath = ArtRootPath + "/Frames";
    private const string GeneratedRootPath = ArtRootPath + "/Generated";
    private const string SourceRootPath = ArtRootPath + "/Source";
    private const string SpriteAssetName = "ButcherBossSprite";
    private const string SpritePrefabPath = GeneratedRootPath + "/" + SpriteAssetName + ".prefab";
    private const string DefinitionPath = GeneratedRootPath + "/" + SpriteAssetName + ".asset";
    private const string ContentRootPath = "Assets/_Project/Content/Actors/ButcherBoss";
    private const string PrefabRootPath = ContentRootPath + "/Prefabs";
    private const string BossPrefabPath = PrefabRootPath + "/ButcherBoss.prefab";
    private const float PixelsPerUnit = 100f;
    private static readonly Vector2 SpritePivot = new(0.5f, 0.06f);

    [MenuItem("Tools/Ultraloud/Bosses/Butcher Boss Builder")]
    private static void Open()
    {
        GetWindow<RetroButcherBossBuilderWindow>("Butcher Boss");
    }

    [MenuItem("Tools/Ultraloud/Bosses/Butcher Boss Builder/Build All Assets")]
    public static void BuildAllAssetsMenu()
    {
        BuildAllAssets(selectAsset: true);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Butcher Boss Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Builds the generated multi-angle butcher boss frames into the standard directional sprite definition and a boss-scale visual prefab. Packed masks drive wet highlights, wound pulse, grime, and subtle surface crawl in the shared billboard shader.",
            MessageType.Info);

        DrawAssetStatus("Sources", new[]
        {
            SourceRootPath + "/ButcherBoss_Imagen_Concept.png",
            SourceRootPath + "/ButcherBoss_Imagen_AngleSheet_Chroma.png",
            SourceRootPath + "/SlicedAngles/Front_Source_Alpha.png"
        });
        DrawAssetStatus("Frames", new[] { FramesRootPath });
        DrawAssetStatus("Generated", new[] { DefinitionPath, SpritePrefabPath, BossPrefabPath });

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Build Butcher Boss Assets", GUILayout.Height(32f)))
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
            Debug.LogError($"Butcher boss frames are missing. Expected frames at {FramesRootPath}.");
            return null;
        }

        ConfigureFrameTextureImports();

        DirectionalSpriteFrameBuildResult result = DirectionalSpriteFrameBuilder.Build(
            framesRoot,
            outputRoot,
            new DirectionalSpriteFrameBuildOptions
            {
                assetName = SpriteAssetName,
                buildPrefab = true,
                instantiateInScene = false,
                addLocomotion = false,
                worldScaleMultiplier = 1.28f
            });

        ConfigureDefinition(result.definition);
        GameObject bossPrefab = CreateOrUpdateBossPrefab(result.definition);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (selectAsset && bossPrefab != null)
        {
            Selection.activeObject = bossPrefab;
            EditorGUIUtility.PingObject(bossPrefab);
        }

        return bossPrefab;
    }

    private static void ConfigureFrameTextureImports()
    {
        if (!AssetDatabase.IsValidFolder(FramesRootPath))
        {
            return;
        }

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { FramesRootPath });
        for (int i = 0; i < textureGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string normalized = path.Replace('\\', '/');
            if (normalized.Contains("/Albedo/", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureAlbedoImporter(normalized);
            }
            else if (normalized.Contains("/Normal/", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/PackedMasks/", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureLinearTextureImporter(normalized);
            }
            else if (normalized.Contains("/Emission/", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureEmissionImporter(normalized);
            }
        }
    }

    private static void ConfigureAlbedoImporter(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
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

        if (!importer.sRGBTexture)
        {
            importer.sRGBTexture = true;
            changed = true;
        }

        if (importer.filterMode != FilterMode.Bilinear)
        {
            importer.filterMode = FilterMode.Bilinear;
            changed = true;
        }

        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }
        if (!Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit))
        {
            importer.spritePixelsPerUnit = PixelsPerUnit;
            changed = true;
        }

        TextureImporterSettings textureSettings = new();
        importer.ReadTextureSettings(textureSettings);
        if (textureSettings.spriteAlignment != (int)SpriteAlignment.Custom || textureSettings.spritePivot != SpritePivot)
        {
            textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
            textureSettings.spritePivot = SpritePivot;
            importer.SetTextureSettings(textureSettings);
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static void ConfigureLinearTextureImporter(string path)
    {
        ConfigureDefaultTextureImporter(path, srgb: false);
    }

    private static void ConfigureEmissionImporter(string path)
    {
        ConfigureDefaultTextureImporter(path, srgb: true);
    }

    private static void ConfigureDefaultTextureImporter(string path, bool srgb)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
        {
            return;
        }

        bool changed = false;
        if (importer.textureType != TextureImporterType.Default)
        {
            importer.textureType = TextureImporterType.Default;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (importer.sRGBTexture != srgb)
        {
            importer.sRGBTexture = srgb;
            changed = true;
        }

        if (importer.filterMode != FilterMode.Bilinear)
        {
            importer.filterMode = FilterMode.Bilinear;
            changed = true;
        }

        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }
        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static void ConfigureDefinition(DirectionalSpriteDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        definition.name = SpriteAssetName;
        definition.defaultClipId = "Idle";
        if (definition.clips != null)
        {
            for (int i = 0; i < definition.clips.Count; i++)
            {
                DirectionalSpriteClip clip = definition.clips[i];
                if (clip == null)
                {
                    continue;
                }

                clip.loop = clip.clipId is "Idle" or "Walk";
                clip.framesPerSecond = clip.clipId switch
                {
                    "Idle" => 5.5f,
                    "Walk" => 8.5f,
                    "Cleaver" => 12f,
                    "Slam" => 10.5f,
                    "Roar" => 8f,
                    _ => 8f
                };
            }
        }

        EditorUtility.SetDirty(definition);
    }

    private static GameObject CreateOrUpdateBossPrefab(DirectionalSpriteDefinition definition)
    {
        GameObject spritePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpritePrefabPath);
        if (spritePrefab == null || definition == null)
        {
            return null;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(SpritePrefabPath);
        try
        {
            root.name = "ButcherBoss";
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
            collider.center = new Vector3(0f, 6.25f, 0f);
            collider.radius = 2.85f;
            collider.height = 12.5f;
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
            ConfigureGib(gib, damageable);

            return PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
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
        serialized.FindProperty("initialClipId").stringValue = "Idle";
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
        SetFloat(serialized, "alphaCutoff", 0.06f);
        SetFloat(serialized, "normalScale", 1.05f);
        SetFloat(serialized, "detailNormalInfluence", 0.68f);
        SetFloat(serialized, "macroNormalBend", 0.72f);
        SetFloat(serialized, "spriteAngleLightingInfluence", 0.32f);
        SetFloat(serialized, "wrapDiffuse", 0.5f);
        SetFloat(serialized, "ambientIntensity", 1.08f);
        SetFloat(serialized, "renderSettingsAmbientScale", 0.22f);
        SetFloat(serialized, "surfaceRoughness", 0.82f);
        SetFloat(serialized, "specularStrength", 0.12f);
        SetFloat(serialized, "minSpecularPower", 7f);
        SetFloat(serialized, "maxSpecularPower", 20f);
        SetFloat(serialized, "rimStrength", 0.18f);
        SetFloat(serialized, "rimPower", 3.1f);
        SetFloat(serialized, "emissionStrength", 1.3f);
        SetFloat(serialized, "wetSpecularBoost", 0.95f);
        SetFloat(serialized, "bloodPulseStrength", 0.52f);
        SetFloat(serialized, "surfaceCrawlStrength", 0.0045f);
        SetFloat(serialized, "surfaceCrawlSpeed", 1.35f);
        SetColor(serialized, "rimColor", new Color(1f, 0.5f, 0.32f, 1f));
        SetColor(serialized, "emissionColor", new Color(1f, 0.22f, 0.06f, 1f));
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
        SetFloat(serialized, "maxHealth", 900f);
        SetBool(serialized, "destroyOnDeath", true);
        SetBool(serialized, "disableRenderersOnDeath", true);
        SetBool(serialized, "disableCollidersOnDeath", true);
        SetFloat(serialized, "destroyDelay", 0.06f);
        SetObject(serialized, "bloodSplatterSprite", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Effects/BloodSplatter_Impact.png"));
        SetObject(serialized, "bloodSpraySprite", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Effects/BloodSpray_Droplets.png"));
        SetColor(serialized, "bloodColor", new Color(0.5f, 0.012f, 0.008f, 0.95f));
        SetBool(serialized, "spawnBloodOnHit", true);
        SetBool(serialized, "spawnBloodOnDeath", true);
        SetVector2(serialized, "bloodSplatterScaleRange", new Vector2(0.9f, 2.4f));
        SetInt(serialized, "bloodDecalCount", 12);
        SetInt(serialized, "bloodSprayParticleCount", 34);
        SetVector2(serialized, "bloodSprayScaleRange", new Vector2(0.14f, 0.42f));
        SetVector2(serialized, "bloodSpraySpeedRange", new Vector2(3.5f, 10.5f));
        SetFloat(serialized, "deathBloodScaleMultiplier", 4.2f);
        SetBool(serialized, "ensureShootableFeedback", true);
        SetEnum(serialized, "shootableSurfaceKind", (int)RetroShootableSurfaceKind.Flesh);
        SetFloat(serialized, "shootableFeedbackScale", 1.9f);
        SetFloat(serialized, "shootableDeathEffectMultiplier", 5.4f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(damageable);
    }

    private static void ConfigureGib(RetroGibOnDeath gib, RetroDamageable damageable)
    {
        SerializedObject serialized = new(gib);
        serialized.FindProperty("damageable").objectReferenceValue = damageable;
        serialized.FindProperty("goreProfile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RetroGoreProfile>("Assets/_Project/Art/Sprites/Effects/Gore/PigGoreProfile.asset");
        SetBool(serialized, "alwaysGibOnDeath", false);
        SetBool(serialized, "useProfileThresholds", true);
        SetVector3(serialized, "localCenterOffset", new Vector3(0f, 6.2f, 0f));
        SetFloat(serialized, "intensityMultiplier", 2.6f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gib);
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

    private static void SetObject(SerializedObject target, string propertyName, UnityEngine.Object value)
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
