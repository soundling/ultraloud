using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class DirectionalSpriteSheetImporterWindow : EditorWindow
{
    private Texture2D sourceSheet;
    private Texture2D normalSheet;
    private DirectionalSpriteImportProfile profile;
    private DefaultAsset outputFolder;
    private string assetName = "DirectionalSpriteDefinition";
    private bool createOrUpdatePrefab = true;
    private bool instantiateInScene;
    private bool addLocomotion = true;
    private float worldScaleMultiplier = 1f;
    private Transform sceneParent;
    private Vector3 scenePosition;

    [MenuItem("Tools/Directional Sprites/Import Sheet (Legacy)")]
    private static void OpenWindow()
    {
        GetWindow<DirectionalSpriteSheetImporterWindow>("Directional Sheet Import (Legacy)");
    }

    private void OnEnable()
    {
        if (sceneParent == null && Selection.activeTransform != null && Selection.activeTransform.gameObject.scene.IsValid())
        {
            sceneParent = Selection.activeTransform;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sheet Import", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Legacy sheet slicer. Prefer the frame-based builder when working with per-frame albedo and normal assets.",
            MessageType.Info);

        using (new EditorGUI.ChangeCheckScope())
        {
            sourceSheet = (Texture2D)EditorGUILayout.ObjectField("Albedo Sheet", sourceSheet, typeof(Texture2D), false);
            normalSheet = (Texture2D)EditorGUILayout.ObjectField("Normal Sheet", normalSheet, typeof(Texture2D), false);
            profile = (DirectionalSpriteImportProfile)EditorGUILayout.ObjectField("Import Profile", profile, typeof(DirectionalSpriteImportProfile), false);
            outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);

            if (sourceSheet != null && string.Equals(assetName, "DirectionalSpriteDefinition", StringComparison.Ordinal))
            {
                assetName = sourceSheet.name;
            }
        }

        assetName = EditorGUILayout.TextField("Asset Name", assetName);

        string resolvedFolder = DirectionalSpriteSheetImporter.ResolveOutputFolder(sourceSheet, profile, outputFolder);
        EditorGUILayout.LabelField("Resolved Folder", string.IsNullOrWhiteSpace(resolvedFolder) ? "(assign sheet + profile)" : resolvedFolder);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Prefab", EditorStyles.boldLabel);
        createOrUpdatePrefab = EditorGUILayout.Toggle("Build Prefab Asset", createOrUpdatePrefab);
        using (new EditorGUI.DisabledScope(!createOrUpdatePrefab))
        {
            addLocomotion = EditorGUILayout.Toggle("Add Locomotion Helper", addLocomotion);
            worldScaleMultiplier = EditorGUILayout.FloatField("World Scale", worldScaleMultiplier);
            instantiateInScene = EditorGUILayout.Toggle("Instantiate In Scene", instantiateInScene);
        }

        using (new EditorGUI.DisabledScope(!createOrUpdatePrefab || !instantiateInScene))
        {
            sceneParent = (Transform)EditorGUILayout.ObjectField("Scene Parent", sceneParent, typeof(Transform), true);
            scenePosition = EditorGUILayout.Vector3Field("Scene Position", scenePosition);
        }

        worldScaleMultiplier = Mathf.Max(0.01f, worldScaleMultiplier);
        if (instantiateInScene)
        {
            createOrUpdatePrefab = true;
        }

        using (new EditorGUI.DisabledScope(sourceSheet == null || profile == null))
        {
            if (GUILayout.Button(GetImportButtonLabel()))
            {
                try
                {
                    DirectionalSpriteImportResult result = DirectionalSpriteSheetImporter.Import(
                        sourceSheet,
                        normalSheet,
                        profile,
                        outputFolder,
                        new DirectionalSpriteImportOptions
                        {
                            assetName = string.IsNullOrWhiteSpace(assetName) ? sourceSheet.name : assetName,
                            createOrUpdatePrefab = createOrUpdatePrefab,
                            instantiateInScene = instantiateInScene,
                            addLocomotion = addLocomotion,
                            worldScaleMultiplier = worldScaleMultiplier,
                            sceneParent = sceneParent,
                            scenePosition = scenePosition
                        });

                    SelectResult(result);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog("Directional Sprite Import Failed", exception.Message, "OK");
                }
            }
        }
    }

    private string GetImportButtonLabel()
    {
        if (instantiateInScene)
        {
            return "Import, Build Prefab, And Spawn";
        }

        if (createOrUpdatePrefab)
        {
            return "Import And Build Prefab";
        }

        return "Import Assets";
    }

    private static void SelectResult(DirectionalSpriteImportResult result)
    {
        if (result == null)
        {
            return;
        }

        UnityEngine.Object target = result.sceneInstance != null
            ? result.sceneInstance
            : result.prefabAsset != null
                ? result.prefabAsset
                : result.definition;

        if (target != null)
        {
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }
}

internal static class DirectionalSpriteSheetImporter
{
    public static DirectionalSpriteDefinition ImportSheet(
        Texture2D sourceSheet,
        Texture2D normalSheet,
        DirectionalSpriteImportProfile profile,
        DefaultAsset outputFolderAsset,
        string assetName)
    {
        DirectionalSpriteImportResult result = Import(
            sourceSheet,
            normalSheet,
            profile,
            outputFolderAsset,
            new DirectionalSpriteImportOptions
            {
                assetName = assetName
            });

        return result.definition;
    }

    public static DirectionalSpriteImportResult Import(
        Texture2D sourceSheet,
        Texture2D normalSheet,
        DirectionalSpriteImportProfile profile,
        DefaultAsset outputFolderAsset,
        DirectionalSpriteImportOptions options)
    {
        if (sourceSheet == null)
        {
            throw new InvalidOperationException("A source sheet is required.");
        }

        if (profile == null)
        {
            throw new InvalidOperationException("An import profile is required.");
        }

        options ??= new DirectionalSpriteImportOptions();
        options.assetName = string.IsNullOrWhiteSpace(options.assetName) ? sourceSheet.name : options.assetName;
        options.worldScaleMultiplier = Mathf.Max(0.01f, options.worldScaleMultiplier);

        ValidateProfile(profile);

        string sourceSheetPath = AssetDatabase.GetAssetPath(sourceSheet);
        if (string.IsNullOrWhiteSpace(sourceSheetPath))
        {
            throw new InvalidOperationException("The selected source sheet must be an imported asset inside the Unity project.");
        }

        string outputFolder = ResolveOutputFolder(sourceSheet, profile, outputFolderAsset);
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            throw new InvalidOperationException("Could not resolve an output folder under Assets.");
        }

        EnsureFolderExists(outputFolder);
        string framesFolder = $"{outputFolder}/Frames";
        EnsureFolderExists(framesFolder);
        string normalFramesFolder = normalSheet != null ? $"{outputFolder}/Normals" : null;
        if (!string.IsNullOrWhiteSpace(normalFramesFolder))
        {
            EnsureFolderExists(normalFramesFolder);
        }

        TextureReadState sourceState = PrepareReadableTexture(sourceSheet);
        TextureReadState normalState = PrepareReadableTexture(normalSheet);

        if (sourceState.changed)
        {
            sourceSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceState.assetPath);
        }

        if (normalState.changed)
        {
            normalSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(normalState.assetPath);
        }

        try
        {
            string safeAssetName = SanitizePathToken(options.assetName);
            DirectionalSpriteDefinition tempDefinition = ScriptableObject.CreateInstance<DirectionalSpriteDefinition>();
            tempDefinition.defaultClipId = profile.clips.Count > 0 ? GetClipId(profile.clips[0], 0) : "Idle";
            tempDefinition.clips = new List<DirectionalSpriteClip>(profile.clips.Count);

            for (int clipIndex = 0; clipIndex < profile.clips.Count; clipIndex++)
            {
                DirectionalSpriteImportClipTemplate clipTemplate = profile.clips[clipIndex];
                if (clipTemplate == null)
                {
                    continue;
                }

                DirectionalSpriteClip clip = new DirectionalSpriteClip
                {
                    clipId = GetClipId(clipTemplate, clipIndex),
                    loop = clipTemplate.loop,
                    framesPerSecond = clipTemplate.framesPerSecond > 0f
                        ? clipTemplate.framesPerSecond
                        : Mathf.Max(1f, profile.defaultFramesPerSecond),
                    angles = new List<DirectionalSpriteAngleSet>(clipTemplate.angles != null ? clipTemplate.angles.Count : 0)
                };

                if (clipTemplate.angles == null)
                {
                    tempDefinition.clips.Add(clip);
                    continue;
                }

                for (int angleIndex = 0; angleIndex < clipTemplate.angles.Count; angleIndex++)
                {
                    DirectionalSpriteImportAngleTemplate angleTemplate = clipTemplate.angles[angleIndex];
                    if (angleTemplate == null)
                    {
                        continue;
                    }

                    DirectionalSpriteAngleSet angle = new DirectionalSpriteAngleSet
                    {
                        label = GetAngleLabel(angleTemplate, angleIndex),
                        yawDegrees = angleTemplate.yawDegrees,
                        symmetry = angleTemplate.symmetry,
                        flipX = angleTemplate.flipX,
                        frames = new List<Sprite>(Mathf.Max(1, angleTemplate.frameCount)),
                        normalFrames = new List<Texture2D>(Mathf.Max(0, angleTemplate.frameCount))
                    };

                    for (int frameIndex = 0; frameIndex < Mathf.Max(1, angleTemplate.frameCount); frameIndex++)
                    {
                        Vector2Int cell = angleTemplate.startCell + angleTemplate.frameStep * frameIndex;
                        RectInt pixelRect = GetPixelRect(sourceSheet, profile, cell);

                        string frameAssetName = $"{safeAssetName}_{SanitizePathToken(clip.clipId)}_{SanitizePathToken(angle.label)}_{frameIndex:D2}.png";
                        string frameAssetPath = $"{framesFolder}/{frameAssetName}";
                        if (!profile.overwriteGeneratedFiles)
                        {
                            frameAssetPath = AssetDatabase.GenerateUniqueAssetPath(frameAssetPath);
                        }

                        Sprite sprite = WriteSpriteFrame(sourceSheet, pixelRect, frameAssetPath, profile);
                        if (sprite != null)
                        {
                            angle.frames.Add(sprite);
                        }

                        if (normalSheet != null)
                        {
                            string normalAssetName = $"{safeAssetName}_{SanitizePathToken(clip.clipId)}_{SanitizePathToken(angle.label)}_{frameIndex:D2}_Normal.png";
                            string normalAssetPath = $"{normalFramesFolder}/{normalAssetName}";
                            if (!profile.overwriteGeneratedFiles)
                            {
                                normalAssetPath = AssetDatabase.GenerateUniqueAssetPath(normalAssetPath);
                            }

                            Texture2D normalFrame = WriteTextureFrame(normalSheet, pixelRect, normalAssetPath, profile, linear: true);
                            if (normalFrame != null)
                            {
                                angle.normalFrames.Add(normalFrame);
                            }
                        }
                    }

                    clip.angles.Add(angle);
                }

                tempDefinition.clips.Add(clip);
            }

            string definitionPath = $"{outputFolder}/{safeAssetName}.asset";
            if (!profile.overwriteGeneratedFiles)
            {
                definitionPath = AssetDatabase.GenerateUniqueAssetPath(definitionPath);
            }

            DirectionalSpriteDefinition definition = AssetDatabase.LoadAssetAtPath<DirectionalSpriteDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<DirectionalSpriteDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            EditorUtility.CopySerialized(tempDefinition, definition);
            EditorUtility.SetDirty(definition);
            UnityEngine.Object.DestroyImmediate(tempDefinition);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject prefabAsset = null;
            if (options.createOrUpdatePrefab || options.instantiateInScene)
            {
                prefabAsset = CreateOrUpdatePrefab(definition, profile, outputFolder, safeAssetName, options);
            }

            GameObject sceneInstance = null;
            if (options.instantiateInScene && prefabAsset != null)
            {
                sceneInstance = InstantiatePrefabInScene(prefabAsset, options);
            }

            Debug.Log(
                $"Directional sprite import completed. Definition='{AssetDatabase.GetAssetPath(definition)}'"
                + (prefabAsset != null ? $", Prefab='{AssetDatabase.GetAssetPath(prefabAsset)}'" : string.Empty)
                + (sceneInstance != null ? $", Spawned='{sceneInstance.name}'." : "."),
                definition);

            return new DirectionalSpriteImportResult
            {
                definition = definition,
                prefabAsset = prefabAsset,
                sceneInstance = sceneInstance,
                outputFolder = outputFolder
            };
        }
        finally
        {
            RestoreTextureState(sourceState);
            RestoreTextureState(normalState);
        }
    }

    public static string ResolveOutputFolder(
        Texture2D sourceSheet,
        DirectionalSpriteImportProfile profile,
        DefaultAsset outputFolderAsset)
    {
        if (outputFolderAsset != null)
        {
            string selectedPath = AssetDatabase.GetAssetPath(outputFolderAsset);
            if (AssetDatabase.IsValidFolder(selectedPath))
            {
                return selectedPath.Replace('\\', '/');
            }
        }

        if (sourceSheet == null)
        {
            return string.Empty;
        }

        string sourceSheetPath = AssetDatabase.GetAssetPath(sourceSheet);
        if (string.IsNullOrWhiteSpace(sourceSheetPath))
        {
            return string.Empty;
        }

        string sourceDirectory = Path.GetDirectoryName(sourceSheetPath)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            return string.Empty;
        }

        string suffix = profile != null && !string.IsNullOrWhiteSpace(profile.outputFolderSuffix)
            ? profile.outputFolderSuffix
            : "_Directional";

        return $"{sourceDirectory}/{SanitizePathToken(sourceSheet.name)}{suffix}";
    }

    private static GameObject CreateOrUpdatePrefab(
        DirectionalSpriteDefinition definition,
        DirectionalSpriteImportProfile profile,
        string outputFolder,
        string safeAssetName,
        DirectionalSpriteImportOptions options)
    {
        string prefabPath = $"{outputFolder}/{safeAssetName}.prefab";
        if (!profile.overwriteGeneratedFiles)
        {
            prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
        }

        GameObject prefabRoot = new GameObject(safeAssetName);
        try
        {
            ConfigurePrefabRoot(prefabRoot, definition, profile, options);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    private static void ConfigurePrefabRoot(
        GameObject root,
        DirectionalSpriteDefinition definition,
        DirectionalSpriteImportProfile profile,
        DirectionalSpriteImportOptions options)
    {
        root.name = string.IsNullOrWhiteSpace(options.assetName) ? definition.name : options.assetName;
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject quadObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadObject.name = "Quad";
        quadObject.transform.SetParent(root.transform, false);

        if (quadObject.TryGetComponent(out Collider quadCollider))
        {
            UnityEngine.Object.DestroyImmediate(quadCollider);
        }

        Vector2 quadSize = new(
            profile.cellSize.x / (float)profile.pixelsPerUnit * options.worldScaleMultiplier,
            profile.cellSize.y / (float)profile.pixelsPerUnit * options.worldScaleMultiplier);
        Vector3 quadOffset = new(
            (0.5f - profile.pivot.x) * quadSize.x,
            (0.5f - profile.pivot.y) * quadSize.y,
            0f);

        quadObject.transform.localPosition = quadOffset;
        quadObject.transform.localRotation = Quaternion.identity;
        quadObject.transform.localScale = new Vector3(quadSize.x, quadSize.y, 1f);

        MeshRenderer quadRenderer = quadObject.GetComponent<MeshRenderer>();
        if (quadRenderer != null)
        {
            quadRenderer.shadowCastingMode = ShadowCastingMode.On;
            quadRenderer.receiveShadows = true;
        }

        DirectionalSpriteAnimator animator = GetOrAddComponent<DirectionalSpriteAnimator>(root);
        DirectionalSpriteBillboardLitRenderer billboardRenderer = GetOrAddComponent<DirectionalSpriteBillboardLitRenderer>(root);
        DirectionalSpriteLocomotion locomotion = options.addLocomotion
            ? GetOrAddComponent<DirectionalSpriteLocomotion>(root)
            : root.GetComponent<DirectionalSpriteLocomotion>();

        if (!options.addLocomotion && locomotion != null)
        {
            UnityEngine.Object.DestroyImmediate(locomotion);
            locomotion = null;
        }

        ApplyAnimatorConfiguration(animator, definition, quadObject.transform);
        ApplyBillboardRendererConfiguration(billboardRenderer, animator, quadRenderer);
        ApplyLocomotionConfiguration(locomotion, animator);
    }

    private static GameObject InstantiatePrefabInScene(GameObject prefabAsset, DirectionalSpriteImportOptions options)
    {
        GameObject sceneInstance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
        if (sceneInstance == null)
        {
            return null;
        }

        Undo.RegisterCreatedObjectUndo(sceneInstance, "Instantiate Directional Sprite Prefab");

        if (options.sceneParent != null && options.sceneParent.gameObject.scene.IsValid())
        {
            sceneInstance.transform.SetParent(options.sceneParent, false);
        }

        sceneInstance.transform.position = options.scenePosition;
        sceneInstance.transform.rotation = Quaternion.identity;
        sceneInstance.transform.localScale = Vector3.one;

        if (sceneInstance.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(sceneInstance.scene);
        }

        return sceneInstance;
    }

    private static void ValidateProfile(DirectionalSpriteImportProfile profile)
    {
        if (profile.cellSize.x <= 0 || profile.cellSize.y <= 0)
        {
            throw new InvalidOperationException("Cell size must be greater than zero.");
        }

        if (profile.pixelsPerUnit <= 0)
        {
            throw new InvalidOperationException("Pixels Per Unit must be greater than zero.");
        }

        if (profile.clips == null || profile.clips.Count == 0)
        {
            throw new InvalidOperationException("The import profile must define at least one clip.");
        }
    }

    private static void ApplyAnimatorConfiguration(
        DirectionalSpriteAnimator animator,
        DirectionalSpriteDefinition definition,
        Transform billboardRoot)
    {
        SerializedObject serializedAnimator = new SerializedObject(animator);
        serializedAnimator.FindProperty("definition").objectReferenceValue = definition;
        serializedAnimator.FindProperty("initialClipId").stringValue = !string.IsNullOrWhiteSpace(definition.defaultClipId) ? definition.defaultClipId : "Idle";
        serializedAnimator.FindProperty("playOnEnable").boolValue = true;
        serializedAnimator.FindProperty("useUnscaledTime").boolValue = false;
        serializedAnimator.FindProperty("animationSpeed").floatValue = 1f;
        serializedAnimator.FindProperty("spriteRenderer").objectReferenceValue = null;
        serializedAnimator.FindProperty("billboardRoot").objectReferenceValue = billboardRoot;
        serializedAnimator.FindProperty("facingReference").objectReferenceValue = animator.transform;
        serializedAnimator.FindProperty("targetCamera").objectReferenceValue = null;
        serializedAnimator.FindProperty("viewAngleSource").enumValueIndex = (int)DirectionalSpriteViewAngleSource.CameraPosition;
        serializedAnimator.FindProperty("billboardMode").enumValueIndex = (int)DirectionalBillboardMode.YAxis;
        serializedAnimator.FindProperty("billboardEulerOffset").vector3Value = Vector3.zero;
        serializedAnimator.FindProperty("logicalFacingOffset").floatValue = 0f;
        serializedAnimator.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(animator);
    }

    private static void ApplyBillboardRendererConfiguration(
        DirectionalSpriteBillboardLitRenderer renderer,
        DirectionalSpriteAnimator animator,
        MeshRenderer quadRenderer)
    {
        SerializedObject serializedRenderer = new SerializedObject(renderer);
        serializedRenderer.FindProperty("animator").objectReferenceValue = animator;
        serializedRenderer.FindProperty("targetRenderer").objectReferenceValue = quadRenderer;
        serializedRenderer.FindProperty("lightAnchor").objectReferenceValue = null;
        SetFloatIfFound(serializedRenderer, "alphaCutoff", 0.1f);
        SetFloatIfFound(serializedRenderer, "normalScale", 1.25f);
        SetFloatIfFound(serializedRenderer, "detailNormalInfluence", 0.78f);
        SetFloatIfFound(serializedRenderer, "macroNormalBend", 1.1f);
        SetFloatIfFound(serializedRenderer, "spriteAngleLightingInfluence", 0.5f);
        SetFloatIfFound(serializedRenderer, "wrapDiffuse", 0.18f);
        SetFloatIfFound(serializedRenderer, "ambientIntensity", 0.9f);
        SetFloatIfFound(serializedRenderer, "renderSettingsAmbientScale", 0.2f);
        SetFloatIfFound(serializedRenderer, "surfaceRoughness", 0.86f);
        SetFloatIfFound(serializedRenderer, "specularStrength", 0f);
        SetFloatIfFound(serializedRenderer, "maxSpecularPower", 18f);
        SetFloatIfFound(serializedRenderer, "rimStrength", 0f);
        SetFloatIfFound(serializedRenderer, "rimPower", 3.2f);
        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(renderer);
    }

    private static void SetFloatIfFound(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void ApplyLocomotionConfiguration(
        DirectionalSpriteLocomotion locomotion,
        DirectionalSpriteAnimator animator)
    {
        if (locomotion == null)
        {
            return;
        }

        SerializedObject serializedLocomotion = new SerializedObject(locomotion);
        serializedLocomotion.FindProperty("animator").objectReferenceValue = animator;
        serializedLocomotion.FindProperty("movementBody").objectReferenceValue = null;
        serializedLocomotion.FindProperty("movementReference").objectReferenceValue = locomotion.transform;
        serializedLocomotion.FindProperty("idleClipId").stringValue = "Idle";
        serializedLocomotion.FindProperty("walkClipId").stringValue = "Walk";
        serializedLocomotion.FindProperty("horizontalOnly").boolValue = true;
        serializedLocomotion.FindProperty("walkThreshold").floatValue = 0.05f;
        serializedLocomotion.FindProperty("speedSmoothing").floatValue = 12f;
        serializedLocomotion.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(locomotion);
    }

    private static TextureReadState PrepareReadableTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return default;
        }

        string assetPath = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            throw new InvalidOperationException("All source sheets must be imported assets inside the Unity project.");
        }

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return new TextureReadState
            {
                assetPath = assetPath
            };
        }

        TextureReadState state = new TextureReadState
        {
            assetPath = assetPath,
            previousReadable = importer.isReadable,
            previousCompression = importer.textureCompression
        };

        bool importerChanged = false;
        if (!importer.isReadable)
        {
            importer.isReadable = true;
            importerChanged = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importerChanged = true;
        }

        state.changed = importerChanged;
        if (importerChanged)
        {
            importer.SaveAndReimport();
        }

        return state;
    }

    private static void RestoreTextureState(TextureReadState state)
    {
        if (string.IsNullOrWhiteSpace(state.assetPath))
        {
            return;
        }

        TextureImporter importer = AssetImporter.GetAtPath(state.assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        bool importerChanged = false;
        if (importer.isReadable != state.previousReadable)
        {
            importer.isReadable = state.previousReadable;
            importerChanged = true;
        }

        if (importer.textureCompression != state.previousCompression)
        {
            importer.textureCompression = state.previousCompression;
            importerChanged = true;
        }

        if (importerChanged)
        {
            importer.SaveAndReimport();
        }
    }

    private static Sprite WriteSpriteFrame(
        Texture2D sourceSheet,
        RectInt pixelRect,
        string assetPath,
        DirectionalSpriteImportProfile profile)
    {
        Color[] pixels = sourceSheet.GetPixels(pixelRect.x, pixelRect.y, pixelRect.width, pixelRect.height);
        Texture2D frameTexture = new Texture2D(pixelRect.width, pixelRect.height, TextureFormat.RGBA32, false);
        frameTexture.SetPixels(pixels);
        frameTexture.Apply(false, false);

        byte[] pngBytes = frameTexture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(frameTexture);

        string normalizedPath = assetPath.Replace('\\', '/');
        File.WriteAllBytes(Path.GetFullPath(normalizedPath), pngBytes);
        AssetDatabase.ImportAsset(normalizedPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(normalizedPath) as TextureImporter;
        if (importer == null)
        {
            return null;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = profile.filterMode;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.spritePixelsPerUnit = profile.pixelsPerUnit;

        TextureImporterSettings textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
        textureSettings.spritePivot = profile.pivot;
        importer.SetTextureSettings(textureSettings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(normalizedPath);
    }

    private static Texture2D WriteTextureFrame(
        Texture2D sourceSheet,
        RectInt pixelRect,
        string assetPath,
        DirectionalSpriteImportProfile profile,
        bool linear)
    {
        Color[] pixels = sourceSheet.GetPixels(pixelRect.x, pixelRect.y, pixelRect.width, pixelRect.height);
        Texture2D frameTexture = new Texture2D(pixelRect.width, pixelRect.height, TextureFormat.RGBA32, false);
        frameTexture.SetPixels(pixels);
        frameTexture.Apply(false, false);

        byte[] pngBytes = frameTexture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(frameTexture);

        string normalizedPath = assetPath.Replace('\\', '/');
        File.WriteAllBytes(Path.GetFullPath(normalizedPath), pngBytes);
        AssetDatabase.ImportAsset(normalizedPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(normalizedPath) as TextureImporter;
        if (importer == null)
        {
            return null;
        }

        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.sRGBTexture = !linear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = profile.filterMode;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(normalizedPath);
    }

    private static RectInt GetPixelRect(Texture2D sourceSheet, DirectionalSpriteImportProfile profile, Vector2Int cell)
    {
        int x = profile.sheetMargin.x + cell.x * (profile.cellSize.x + profile.cellPadding.x);
        int y;

        if (profile.sheetOrigin == DirectionalSheetOrigin.TopLeft)
        {
            int topOffset = profile.sheetMargin.y + cell.y * (profile.cellSize.y + profile.cellPadding.y);
            y = sourceSheet.height - topOffset - profile.cellSize.y;
        }
        else
        {
            y = profile.sheetMargin.y + cell.y * (profile.cellSize.y + profile.cellPadding.y);
        }

        RectInt pixelRect = new RectInt(x, y, profile.cellSize.x, profile.cellSize.y);
        RectInt sheetRect = new RectInt(0, 0, sourceSheet.width, sourceSheet.height);
        if (!sheetRect.Contains(new Vector2Int(pixelRect.xMin, pixelRect.yMin))
            || !sheetRect.Contains(new Vector2Int(pixelRect.xMax - 1, pixelRect.yMax - 1)))
        {
            throw new InvalidOperationException(
                $"Profile requested cell {cell} which resolves to pixel rect {pixelRect} outside source sheet bounds {sheetRect}.");
        }

        return pixelRect;
    }

    private static void EnsureFolderExists(string assetFolderPath)
    {
        string normalizedPath = assetFolderPath.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(normalizedPath))
        {
            return;
        }

        string parentPath = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(normalizedPath);

        if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(folderName))
        {
            throw new InvalidOperationException($"Invalid asset folder path '{assetFolderPath}'.");
        }

        EnsureFolderExists(parentPath);
        AssetDatabase.CreateFolder(parentPath, folderName);
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        if (existing != null)
        {
            return existing;
        }

        return target.AddComponent<T>();
    }

    private static string SanitizePathToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unnamed";
        }

        string sanitized = value.Trim();
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidCharacters.Length; i++)
        {
            sanitized = sanitized.Replace(invalidCharacters[i], '_');
        }

        sanitized = sanitized.Replace(' ', '_');
        return sanitized;
    }

    private static string GetClipId(DirectionalSpriteImportClipTemplate template, int index)
    {
        if (!string.IsNullOrWhiteSpace(template.clipId))
        {
            return template.clipId;
        }

        return $"Clip_{index:D2}";
    }

    private static string GetAngleLabel(DirectionalSpriteImportAngleTemplate template, int index)
    {
        if (!string.IsNullOrWhiteSpace(template.label))
        {
            return template.label;
        }

        return $"Angle_{index:D2}";
    }

    private struct TextureReadState
    {
        public string assetPath;
        public bool previousReadable;
        public TextureImporterCompression previousCompression;
        public bool changed;
    }
}

internal sealed class DirectionalSpriteImportOptions
{
    public string assetName;
    public bool createOrUpdatePrefab;
    public bool instantiateInScene;
    public bool addLocomotion = true;
    public float worldScaleMultiplier = 1f;
    public Transform sceneParent;
    public Vector3 scenePosition = Vector3.zero;
}

internal sealed class DirectionalSpriteImportResult
{
    public DirectionalSpriteDefinition definition;
    public GameObject prefabAsset;
    public GameObject sceneInstance;
    public string outputFolder;
}
