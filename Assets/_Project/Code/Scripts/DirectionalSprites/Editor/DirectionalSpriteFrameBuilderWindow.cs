using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public sealed class DirectionalSpriteFrameBuilderWindow : EditorWindow
{
    private DefaultAsset framesRootFolder;
    private DefaultAsset outputFolder;
    private string assetName = "DirectionalSpriteDefinition";
    private bool buildPrefab = true;
    private bool instantiateInScene;
    private bool addLocomotion = true;
    private float worldScaleMultiplier = 1f;
    private Transform sceneParent;
    private Vector3 scenePosition;

    [MenuItem("Tools/Directional Sprites/Build From Frames")]
    private static void OpenWindow()
    {
        GetWindow<DirectionalSpriteFrameBuilderWindow>("Directional Frame Builder");
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
        EditorGUILayout.LabelField("Frame Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Build a directional NPC from per-frame albedo and normal assets.\n"
            + "The builder will auto-fix albedo frames to Sprite imports and normal frames to linear textures when needed.\n\n"
            + "You can select either the NPC root folder or its Frames folder. If a sibling Generated folder exists, it will be used automatically.\n\n"
            + "Supported folder conventions under the selected root:\n"
            + "1. Clip/Angle/00_Albedo.png and 00_Normal.png\n"
            + "2. Clip/Angle/Albedo/00.png and Normal/00.png\n\n"
            + "Supported angles: Front, FrontRight, FrontSideRight, Right, BackSideRight, BackRight, Back, BackLeft, Left, FrontLeft.",
            MessageType.Info);

        using (new EditorGUI.ChangeCheckScope())
        {
            framesRootFolder = (DefaultAsset)EditorGUILayout.ObjectField("Frames Root Folder", framesRootFolder, typeof(DefaultAsset), false);
            outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);

            if (framesRootFolder != null && string.Equals(assetName, "DirectionalSpriteDefinition", StringComparison.Ordinal))
            {
                assetName = DirectionalSpriteFrameBuilder.SuggestAssetName(framesRootFolder);
            }
        }

        assetName = EditorGUILayout.TextField("Asset Name", assetName);

        string resolvedOutputFolder = DirectionalSpriteFrameBuilder.ResolveOutputFolder(framesRootFolder, outputFolder);
        EditorGUILayout.LabelField("Resolved Output", string.IsNullOrWhiteSpace(resolvedOutputFolder) ? "(assign root folder)" : resolvedOutputFolder);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Prefab", EditorStyles.boldLabel);
        buildPrefab = EditorGUILayout.Toggle("Build Prefab Asset", buildPrefab);
        using (new EditorGUI.DisabledScope(!buildPrefab))
        {
            addLocomotion = EditorGUILayout.Toggle("Add Locomotion Helper", addLocomotion);
            worldScaleMultiplier = EditorGUILayout.FloatField("World Scale", worldScaleMultiplier);
            instantiateInScene = EditorGUILayout.Toggle("Instantiate In Scene", instantiateInScene);
        }

        using (new EditorGUI.DisabledScope(!buildPrefab || !instantiateInScene))
        {
            sceneParent = (Transform)EditorGUILayout.ObjectField("Scene Parent", sceneParent, typeof(Transform), true);
            scenePosition = EditorGUILayout.Vector3Field("Scene Position", scenePosition);
        }

        worldScaleMultiplier = Mathf.Max(0.01f, worldScaleMultiplier);
        if (instantiateInScene)
        {
            buildPrefab = true;
        }

        using (new EditorGUI.DisabledScope(framesRootFolder == null))
        {
            if (GUILayout.Button(GetButtonLabel()))
            {
                try
                {
                    DirectionalSpriteFrameBuildResult result = DirectionalSpriteFrameBuilder.Build(
                        framesRootFolder,
                        outputFolder,
                        new DirectionalSpriteFrameBuildOptions
                        {
                            assetName = string.IsNullOrWhiteSpace(assetName) ? framesRootFolder.name : assetName,
                            buildPrefab = buildPrefab,
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
                    EditorUtility.DisplayDialog("Directional Frame Build Failed", exception.Message, "OK");
                }
            }
        }
    }

    private string GetButtonLabel()
    {
        if (instantiateInScene)
        {
            return "Build Definition, Prefab, And Spawn";
        }

        if (buildPrefab)
        {
            return "Build Definition And Prefab";
        }

        return "Build Definition";
    }

    private static void SelectResult(DirectionalSpriteFrameBuildResult result)
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

internal static class DirectionalSpriteFrameBuilder
{
    public static DirectionalSpriteFrameBuildResult Build(DefaultAsset framesRootFolderAsset, DefaultAsset outputFolderAsset, DirectionalSpriteFrameBuildOptions options)
    {
        if (framesRootFolderAsset == null)
        {
            throw new InvalidOperationException("A frames root folder is required.");
        }

        string framesRootFolder = ResolveFramesRootFolder(framesRootFolderAsset);
        if (!AssetDatabase.IsValidFolder(framesRootFolder))
        {
            throw new InvalidOperationException("The selected folder must be a valid NPC root or Frames folder under Assets.");
        }

        string resolvedOutputFolder = ResolveOutputFolder(framesRootFolderAsset, outputFolderAsset);
        if (string.IsNullOrWhiteSpace(resolvedOutputFolder))
        {
            throw new InvalidOperationException("Could not resolve an output folder under Assets.");
        }

        options ??= new DirectionalSpriteFrameBuildOptions();
        options.assetName = string.IsNullOrWhiteSpace(options.assetName) ? SuggestAssetName(framesRootFolderAsset) : options.assetName;
        options.worldScaleMultiplier = Mathf.Max(0.01f, options.worldScaleMultiplier);

        EnsureFolderExists(resolvedOutputFolder);
        List<DiscoveredFramePair> discoveredPairs = DiscoverFramePairs(framesRootFolder);
        if (discoveredPairs.Count == 0)
        {
            throw new InvalidOperationException("No valid frame pairs were found. Use Clip/Angle/00_Albedo.png + 00_Normal.png or Clip/Angle/Albedo/00.png + Normal/00.png.");
        }

        DirectionalSpriteDefinition definition = CreateOrUpdateDefinition(discoveredPairs, resolvedOutputFolder, options.assetName);
        GameObject prefabAsset = null;
        if (options.buildPrefab || options.instantiateInScene)
        {
            prefabAsset = CreateOrUpdatePrefab(definition, resolvedOutputFolder, options);
        }

        GameObject sceneInstance = null;
        if (options.instantiateInScene && prefabAsset != null)
        {
            sceneInstance = InstantiatePrefabInScene(prefabAsset, options);
        }

        Debug.Log(
            $"Directional frame build completed. Definition='{AssetDatabase.GetAssetPath(definition)}'"
            + (prefabAsset != null ? $", Prefab='{AssetDatabase.GetAssetPath(prefabAsset)}'" : string.Empty)
            + (sceneInstance != null ? $", Spawned='{sceneInstance.name}'." : "."),
            definition);

        return new DirectionalSpriteFrameBuildResult
        {
            definition = definition,
            prefabAsset = prefabAsset,
            sceneInstance = sceneInstance,
            outputFolder = resolvedOutputFolder
        };
    }

    public static string ResolveOutputFolder(DefaultAsset framesRootFolderAsset, DefaultAsset outputFolderAsset)
    {
        if (outputFolderAsset != null)
        {
            string selectedPath = AssetDatabase.GetAssetPath(outputFolderAsset)?.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(selectedPath))
            {
                return selectedPath;
            }
        }

        if (framesRootFolderAsset == null)
        {
            return string.Empty;
        }

        string selectedFolder = AssetDatabase.GetAssetPath(framesRootFolderAsset)?.Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(selectedFolder))
        {
            return string.Empty;
        }

        string framesRootFolder = ResolveFramesRootFolder(framesRootFolderAsset);
        string siblingGenerated = GetGeneratedFolderCandidate(selectedFolder, framesRootFolder);
        return !string.IsNullOrWhiteSpace(siblingGenerated) ? siblingGenerated : framesRootFolder;
    }

    public static string SuggestAssetName(DefaultAsset framesRootFolderAsset)
    {
        string selectedFolder = AssetDatabase.GetAssetPath(framesRootFolderAsset)?.Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(selectedFolder))
        {
            return "DirectionalSpriteDefinition";
        }

        string folderName = Path.GetFileName(selectedFolder);
        if (string.Equals(folderName, "Frames", StringComparison.OrdinalIgnoreCase))
        {
            string parentName = Path.GetFileName(Path.GetDirectoryName(selectedFolder)?.Replace('\\', '/'));
            return string.IsNullOrWhiteSpace(parentName) ? folderName : parentName;
        }

        string nestedFramesFolder = $"{selectedFolder}/Frames";
        if (AssetDatabase.IsValidFolder(nestedFramesFolder))
        {
            return folderName;
        }

        return folderName;
    }

    private static string ResolveFramesRootFolder(DefaultAsset framesRootFolderAsset)
    {
        string selectedFolder = AssetDatabase.GetAssetPath(framesRootFolderAsset)?.Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(selectedFolder))
        {
            return string.Empty;
        }

        string nestedFramesFolder = $"{selectedFolder}/Frames";
        if (AssetDatabase.IsValidFolder(nestedFramesFolder))
        {
            return nestedFramesFolder;
        }

        return selectedFolder;
    }

    private static string GetGeneratedFolderCandidate(string selectedFolder, string resolvedFramesFolder)
    {
        string nestedGeneratedFolder = $"{selectedFolder}/Generated";
        if (AssetDatabase.IsValidFolder(nestedGeneratedFolder))
        {
            return nestedGeneratedFolder;
        }

        if (string.Equals(Path.GetFileName(resolvedFramesFolder), "Frames", StringComparison.OrdinalIgnoreCase))
        {
            string parentFolder = Path.GetDirectoryName(resolvedFramesFolder)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(parentFolder))
            {
                return $"{parentFolder}/Generated";
            }
        }

        return string.Empty;
    }

    private static List<DiscoveredFramePair> DiscoverFramePairs(string framesRootFolder)
    {
        string absoluteRoot = Path.GetFullPath(framesRootFolder);
        Dictionary<string, DiscoveredFramePair> pairs = new(StringComparer.OrdinalIgnoreCase);

        foreach (string absoluteFilePath in Directory.GetFiles(absoluteRoot, "*.*", SearchOption.AllDirectories))
        {
            if (!IsSupportedImageFile(absoluteFilePath))
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(absoluteRoot, absoluteFilePath).Replace('\\', '/');
            if (!TryParseFramePath(relativePath, out string clipId, out string angleLabel, out string frameKey, out DiscoveredLayerKind layerKind))
            {
                continue;
            }

            string assetPath = $"{framesRootFolder}/{relativePath}".Replace('\\', '/');
            string pairKey = $"{clipId}|{angleLabel}|{frameKey}";
            if (!pairs.TryGetValue(pairKey, out DiscoveredFramePair pair))
            {
                pair = new DiscoveredFramePair
                {
                    clipId = clipId,
                    angleLabel = angleLabel,
                    frameKey = frameKey
                };
                pairs.Add(pairKey, pair);
            }

            switch (layerKind)
            {
                case DiscoveredLayerKind.Albedo:
                    EnsureAlbedoFrameImport(assetPath);
                    pair.albedo = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (pair.albedo == null)
                    {
                        throw new InvalidOperationException($"'{assetPath}' was parsed as an albedo frame but is not imported as a Sprite.");
                    }
                    break;

                case DiscoveredLayerKind.Normal:
                    EnsureNormalFrameImport(assetPath);
                    pair.normal = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (pair.normal == null)
                    {
                        throw new InvalidOperationException($"'{assetPath}' was parsed as a normal frame but is not imported as a Texture2D.");
                    }
                    break;

                case DiscoveredLayerKind.PackedMasks:
                    EnsureAuxiliaryFrameImport(assetPath, linear: true);
                    pair.packedMasks = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (pair.packedMasks == null)
                    {
                        throw new InvalidOperationException($"'{assetPath}' was parsed as a packed masks frame but is not imported as a Texture2D.");
                    }
                    break;

                case DiscoveredLayerKind.Emission:
                    EnsureAuxiliaryFrameImport(assetPath, linear: false);
                    pair.emission = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (pair.emission == null)
                    {
                        throw new InvalidOperationException($"'{assetPath}' was parsed as an emission frame but is not imported as a Texture2D.");
                    }
                    break;
            }
        }

        return pairs.Values
            .Where(static pair => pair.albedo != null)
            .OrderBy(static pair => GetClipSortOrder(pair.clipId))
            .ThenBy(static pair => pair.clipId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static pair => GetAngleSortOrder(pair.angleLabel))
            .ThenBy(static pair => pair.angleLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static pair => GetFrameSortKey(pair.frameKey))
            .ThenBy(static pair => pair.frameKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void EnsureAlbedoFrameImport(string assetPath)
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

        if (!importer.sRGBTexture)
        {
            importer.sRGBTexture = true;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static void EnsureNormalFrameImport(string assetPath)
    {
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
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

        if (importer.sRGBTexture)
        {
            importer.sRGBTexture = false;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static void EnsureAuxiliaryFrameImport(string assetPath, bool linear)
    {
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
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

        bool srgb = !linear;
        if (importer.sRGBTexture != srgb)
        {
            importer.sRGBTexture = srgb;
            changed = true;
        }

        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static DirectionalSpriteDefinition CreateOrUpdateDefinition(List<DiscoveredFramePair> discoveredPairs, string outputFolder, string assetName)
    {
        string safeAssetName = SanitizePathToken(assetName);
        DirectionalSpriteDefinition tempDefinition = ScriptableObject.CreateInstance<DirectionalSpriteDefinition>();
        tempDefinition.name = safeAssetName;
        tempDefinition.defaultClipId = discoveredPairs.Select(static pair => pair.clipId).FirstOrDefault() ?? "Idle";
        tempDefinition.clips = new List<DirectionalSpriteClip>();

        foreach (IGrouping<string, DiscoveredFramePair> clipGroup in discoveredPairs.GroupBy(static pair => pair.clipId, StringComparer.OrdinalIgnoreCase))
        {
            DirectionalSpriteClip clip = new DirectionalSpriteClip
            {
                clipId = clipGroup.First().clipId,
                loop = true,
                framesPerSecond = GuessFramesPerSecond(clipGroup.Key),
                angles = new List<DirectionalSpriteAngleSet>()
            };

            foreach (IGrouping<string, DiscoveredFramePair> angleGroup in clipGroup.GroupBy(static pair => pair.angleLabel, StringComparer.OrdinalIgnoreCase))
            {
                DirectionalSpriteAngleSet angle = CreateAngleSet(angleGroup.First().angleLabel, angleGroup.ToList());
                clip.angles.Add(angle);
            }

            tempDefinition.clips.Add(clip);
        }

        string definitionPath = $"{outputFolder}/{safeAssetName}.asset";
        DirectionalSpriteDefinition definition = AssetDatabase.LoadAssetAtPath<DirectionalSpriteDefinition>(definitionPath);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<DirectionalSpriteDefinition>();
            definition.name = safeAssetName;
            AssetDatabase.CreateAsset(definition, definitionPath);
        }

        EditorUtility.CopySerialized(tempDefinition, definition);
        definition.name = safeAssetName;
        EditorUtility.SetDirty(definition);
        UnityEngine.Object.DestroyImmediate(tempDefinition);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return definition;
    }

    private static DirectionalSpriteAngleSet CreateAngleSet(string rawAngleLabel, List<DiscoveredFramePair> pairs)
    {
        ResolveAngleMetadata(rawAngleLabel, out float yawDegrees, out DirectionalSpriteSymmetry symmetry);
        DirectionalSpriteAngleSet angle = new DirectionalSpriteAngleSet
        {
            label = rawAngleLabel,
            yawDegrees = yawDegrees,
            symmetry = symmetry,
            flipX = false,
            frames = new List<Sprite>(pairs.Count),
            normalFrames = new List<Texture2D>(pairs.Count),
            packedMaskFrames = new List<Texture2D>(),
            emissionFrames = new List<Texture2D>()
        };

        bool hasPackedMasks = pairs.Any(static pair => pair.packedMasks != null);
        bool hasEmission = pairs.Any(static pair => pair.emission != null);
        if (hasPackedMasks)
        {
            angle.packedMaskFrames = new List<Texture2D>(pairs.Count);
        }

        if (hasEmission)
        {
            angle.emissionFrames = new List<Texture2D>(pairs.Count);
        }

        foreach (DiscoveredFramePair pair in pairs.OrderBy(static pair => GetFrameSortKey(pair.frameKey)).ThenBy(static pair => pair.frameKey, StringComparer.OrdinalIgnoreCase))
        {
            angle.frames.Add(pair.albedo);
            angle.normalFrames.Add(pair.normal);
            if (hasPackedMasks)
            {
                angle.packedMaskFrames.Add(pair.packedMasks);
            }

            if (hasEmission)
            {
                angle.emissionFrames.Add(pair.emission);
            }
        }

        return angle;
    }

    private static GameObject CreateOrUpdatePrefab(DirectionalSpriteDefinition definition, string outputFolder, DirectionalSpriteFrameBuildOptions options)
    {
        string safeAssetName = SanitizePathToken(options.assetName);
        string prefabPath = $"{outputFolder}/{safeAssetName}.prefab";

        GameObject prefabRoot = new GameObject(safeAssetName);
        try
        {
            ConfigurePrefabRoot(prefabRoot, definition, options);
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

    private static void ConfigurePrefabRoot(GameObject root, DirectionalSpriteDefinition definition, DirectionalSpriteFrameBuildOptions options)
    {
        Sprite referenceSprite = FindReferenceSprite(definition);
        if (referenceSprite == null)
        {
            throw new InvalidOperationException("The generated definition has no albedo frames, so a prefab cannot be built.");
        }

        Vector2 spriteSize = new(referenceSprite.rect.width / referenceSprite.pixelsPerUnit, referenceSprite.rect.height / referenceSprite.pixelsPerUnit);
        Vector2 normalizedPivot = new(referenceSprite.pivot.x / referenceSprite.rect.width, referenceSprite.pivot.y / referenceSprite.rect.height);

        GameObject quadObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadObject.name = "Quad";
        quadObject.transform.SetParent(root.transform, false);

        if (quadObject.TryGetComponent(out Collider quadCollider))
        {
            UnityEngine.Object.DestroyImmediate(quadCollider);
        }

        Vector2 scaledSize = spriteSize * options.worldScaleMultiplier;
        quadObject.transform.localPosition = new Vector3((0.5f - normalizedPivot.x) * scaledSize.x, (0.5f - normalizedPivot.y) * scaledSize.y, 0f);
        quadObject.transform.localRotation = Quaternion.identity;
        quadObject.transform.localScale = new Vector3(scaledSize.x, scaledSize.y, 1f);

        MeshRenderer quadRenderer = quadObject.GetComponent<MeshRenderer>();
        quadRenderer.shadowCastingMode = ShadowCastingMode.On;
        quadRenderer.receiveShadows = true;

        DirectionalSpriteAnimator animator = GetOrAddComponent<DirectionalSpriteAnimator>(root);
        DirectionalSpriteBillboardLitRenderer billboardRenderer = GetOrAddComponent<DirectionalSpriteBillboardLitRenderer>(root);
        DirectionalSpriteLocomotion locomotion = options.addLocomotion ? GetOrAddComponent<DirectionalSpriteLocomotion>(root) : root.GetComponent<DirectionalSpriteLocomotion>();
        if (!options.addLocomotion && locomotion != null)
        {
            UnityEngine.Object.DestroyImmediate(locomotion);
            locomotion = null;
        }

        ApplyAnimatorConfiguration(animator, definition, quadObject.transform);
        ApplyBillboardRendererConfiguration(billboardRenderer, animator, quadRenderer);
        ApplyLocomotionConfiguration(locomotion, animator, definition);
    }

    private static GameObject InstantiatePrefabInScene(GameObject prefabAsset, DirectionalSpriteFrameBuildOptions options)
    {
        ReplaceExistingSceneInstance(prefabAsset, options);

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

    private static void ReplaceExistingSceneInstance(GameObject prefabAsset, DirectionalSpriteFrameBuildOptions options)
    {
        GameObject existingInstance = FindExistingSceneInstance(prefabAsset, options);
        if (existingInstance != null)
        {
            Undo.DestroyObjectImmediate(existingInstance);
        }
    }

    private static GameObject FindExistingSceneInstance(GameObject prefabAsset, DirectionalSpriteFrameBuildOptions options)
    {
        if (prefabAsset == null)
        {
            return null;
        }

        string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
        if (string.IsNullOrWhiteSpace(prefabPath))
        {
            return null;
        }

        if (options.sceneParent != null && options.sceneParent.gameObject.scene.IsValid())
        {
            for (int childIndex = 0; childIndex < options.sceneParent.childCount; childIndex++)
            {
                GameObject candidate = options.sceneParent.GetChild(childIndex).gameObject;
                if (IsSceneInstanceOfPrefab(candidate, prefabPath))
                {
                    return candidate;
                }
            }

            return null;
        }

        foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (IsSceneInstanceOfPrefab(rootObject, prefabPath))
            {
                return rootObject;
            }
        }

        return null;
    }

    private static bool IsSceneInstanceOfPrefab(GameObject candidate, string prefabPath)
    {
        if (candidate == null || !candidate.scene.IsValid())
        {
            return false;
        }

        GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(candidate);
        if (prefabSource == null)
        {
            return false;
        }

        string candidatePrefabPath = AssetDatabase.GetAssetPath(prefabSource);
        return string.Equals(candidatePrefabPath, prefabPath, StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyAnimatorConfiguration(DirectionalSpriteAnimator animator, DirectionalSpriteDefinition definition, Transform billboardRoot)
    {
        SerializedObject serializedAnimator = new SerializedObject(animator);
        serializedAnimator.FindProperty("definition").objectReferenceValue = definition;
        serializedAnimator.FindProperty("initialClipId").stringValue = !string.IsNullOrWhiteSpace(definition.defaultClipId) ? definition.defaultClipId : "Idle";
        serializedAnimator.FindProperty("spriteRenderer").objectReferenceValue = null;
        serializedAnimator.FindProperty("billboardRoot").objectReferenceValue = billboardRoot;
        serializedAnimator.FindProperty("facingReference").objectReferenceValue = animator.transform;
        serializedAnimator.FindProperty("targetCamera").objectReferenceValue = null;
        serializedAnimator.FindProperty("viewAngleSource").enumValueIndex = (int)DirectionalSpriteViewAngleSource.CameraPosition;
        serializedAnimator.FindProperty("billboardMode").enumValueIndex = (int)DirectionalBillboardMode.YAxis;
        serializedAnimator.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(animator);
    }

    private static void ApplyBillboardRendererConfiguration(DirectionalSpriteBillboardLitRenderer renderer, DirectionalSpriteAnimator animator, MeshRenderer quadRenderer)
    {
        SerializedObject serializedRenderer = new SerializedObject(renderer);
        serializedRenderer.FindProperty("animator").objectReferenceValue = animator;
        serializedRenderer.FindProperty("targetRenderer").objectReferenceValue = quadRenderer;
        serializedRenderer.FindProperty("lightAnchor").objectReferenceValue = null;
        SetFloatIfFound(serializedRenderer, "alphaCutoff", 0.1f);
        SetFloatIfFound(serializedRenderer, "normalScale", 0.9f);
        SetFloatIfFound(serializedRenderer, "detailNormalInfluence", 0.48f);
        SetFloatIfFound(serializedRenderer, "macroNormalBend", 0.55f);
        SetFloatIfFound(serializedRenderer, "spriteAngleLightingInfluence", 0.28f);
        SetFloatIfFound(serializedRenderer, "wrapDiffuse", 0.34f);
        SetFloatIfFound(serializedRenderer, "ambientIntensity", 0.92f);
        SetFloatIfFound(serializedRenderer, "renderSettingsAmbientScale", 0.2f);
        SetFloatIfFound(serializedRenderer, "surfaceRoughness", 0.94f);
        SetFloatIfFound(serializedRenderer, "specularStrength", 0.025f);
        SetFloatIfFound(serializedRenderer, "minSpecularPower", 8f);
        SetFloatIfFound(serializedRenderer, "maxSpecularPower", 14f);
        SetFloatIfFound(serializedRenderer, "rimStrength", 0.025f);
        SetFloatIfFound(serializedRenderer, "rimPower", 4.2f);
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

    private static void ApplyLocomotionConfiguration(DirectionalSpriteLocomotion locomotion, DirectionalSpriteAnimator animator, DirectionalSpriteDefinition definition)
    {
        if (locomotion == null)
        {
            return;
        }

        string idleClipId = ResolvePreferredClipId(definition, "Idle");
        if (string.IsNullOrWhiteSpace(idleClipId))
        {
            idleClipId = "Idle";
        }

        string walkClipId = ResolvePreferredClipId(definition, "Walk", idleClipId);

        SerializedObject serializedLocomotion = new SerializedObject(locomotion);
        serializedLocomotion.FindProperty("animator").objectReferenceValue = animator;
        serializedLocomotion.FindProperty("movementBody").objectReferenceValue = null;
        serializedLocomotion.FindProperty("movementReference").objectReferenceValue = locomotion.transform;
        serializedLocomotion.FindProperty("idleClipId").stringValue = idleClipId;
        serializedLocomotion.FindProperty("walkClipId").stringValue = string.IsNullOrWhiteSpace(walkClipId) ? idleClipId : walkClipId;
        serializedLocomotion.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(locomotion);
    }

    private static string ResolvePreferredClipId(DirectionalSpriteDefinition definition, string preferredClipId, string fallbackClipId = null)
    {
        if (definition == null || definition.clips == null || definition.clips.Count == 0)
        {
            return fallbackClipId ?? string.Empty;
        }

        DirectionalSpriteClip preferred = definition.FindClip(preferredClipId);
        if (preferred != null && !string.IsNullOrWhiteSpace(preferred.clipId))
        {
            return preferred.clipId;
        }

        DirectionalSpriteClip fallback = !string.IsNullOrWhiteSpace(fallbackClipId) ? definition.FindClip(fallbackClipId) : null;
        if (fallback != null && !string.IsNullOrWhiteSpace(fallback.clipId))
        {
            return fallback.clipId;
        }

        DirectionalSpriteClip defaultClip = definition.GetDefaultClip();
        if (defaultClip != null && !string.IsNullOrWhiteSpace(defaultClip.clipId))
        {
            return defaultClip.clipId;
        }

        return definition.clips.FirstOrDefault(static clip => clip != null && !string.IsNullOrWhiteSpace(clip.clipId))?.clipId ?? string.Empty;
    }

    private static Sprite FindReferenceSprite(DirectionalSpriteDefinition definition)
    {
        foreach (DirectionalSpriteClip clip in definition.clips)
        {
            if (clip?.angles == null)
            {
                continue;
            }

            foreach (DirectionalSpriteAngleSet angle in clip.angles)
            {
                if (angle?.frames != null && angle.frames.Count > 0 && angle.frames[0] != null)
                {
                    return angle.frames[0];
                }
            }
        }

        return null;
    }

    private static void ResolveAngleMetadata(string rawAngleLabel, out float yawDegrees, out DirectionalSpriteSymmetry symmetry)
    {
        string key = NormalizeToken(rawAngleLabel);
        switch (key)
        {
            case "front": yawDegrees = 0f; symmetry = DirectionalSpriteSymmetry.Unique; return;
            case "frontright": yawDegrees = 45f; symmetry = DirectionalSpriteSymmetry.MirrorToOppositeSide; return;
            case "frontsideright": yawDegrees = 67.5f; symmetry = DirectionalSpriteSymmetry.MirrorToOppositeSide; return;
            case "right": yawDegrees = 90f; symmetry = DirectionalSpriteSymmetry.MirrorToOppositeSide; return;
            case "backsideright": yawDegrees = 112.5f; symmetry = DirectionalSpriteSymmetry.MirrorToOppositeSide; return;
            case "backright": yawDegrees = 135f; symmetry = DirectionalSpriteSymmetry.MirrorToOppositeSide; return;
            case "back": yawDegrees = 180f; symmetry = DirectionalSpriteSymmetry.Unique; return;
            case "backleft": yawDegrees = -135f; symmetry = DirectionalSpriteSymmetry.Unique; return;
            case "left": yawDegrees = -90f; symmetry = DirectionalSpriteSymmetry.Unique; return;
            case "frontleft": yawDegrees = -45f; symmetry = DirectionalSpriteSymmetry.Unique; return;
        }

        throw new InvalidOperationException($"Unsupported angle label '{rawAngleLabel}'. Use Front, FrontRight, FrontSideRight, Right, BackSideRight, BackRight, Back, BackLeft, Left, or FrontLeft.");
    }

    private static bool TryParseFramePath(string relativePath, out string clipId, out string angleLabel, out string frameKey, out DiscoveredLayerKind layerKind)
    {
        clipId = null;
        angleLabel = null;
        frameKey = null;
        layerKind = default;

        string[] segments = relativePath.Split('/');
        if (segments.Length < 3)
        {
            return false;
        }

        clipId = segments[0];
        angleLabel = segments[1];
        string fileName = Path.GetFileNameWithoutExtension(relativePath);

        if (segments.Length >= 4)
        {
            string folderKind = NormalizeToken(segments[2]);
            if (folderKind == "albedo" || folderKind == "normal")
            {
                frameKey = fileName;
                layerKind = folderKind == "albedo" ? DiscoveredLayerKind.Albedo : DiscoveredLayerKind.Normal;
                return true;
            }

            if (folderKind == "packedmasks" || folderKind == "packedmask" || folderKind == "masks" || folderKind == "mask")
            {
                frameKey = fileName;
                layerKind = DiscoveredLayerKind.PackedMasks;
                return true;
            }

            if (folderKind == "emission" || folderKind == "emissive" || folderKind == "emit")
            {
                frameKey = fileName;
                layerKind = DiscoveredLayerKind.Emission;
                return true;
            }
        }

        int separatorIndex = fileName.LastIndexOf('_');
        if (separatorIndex <= 0 || separatorIndex >= fileName.Length - 1)
        {
            return false;
        }

        string suffix = NormalizeToken(fileName[(separatorIndex + 1)..]);
        frameKey = fileName[..separatorIndex];
        switch (suffix)
        {
            case "albedo":
                layerKind = DiscoveredLayerKind.Albedo;
                return true;
            case "normal":
                layerKind = DiscoveredLayerKind.Normal;
                return true;
            case "packedmasks":
            case "packedmask":
            case "masks":
            case "mask":
                layerKind = DiscoveredLayerKind.PackedMasks;
                return true;
            case "emission":
            case "emissive":
            case "emit":
                layerKind = DiscoveredLayerKind.Emission;
                return true;
            default:
                return false;
        }
    }

    private static bool IsSupportedImageFile(string absoluteFilePath)
    {
        string extension = Path.GetExtension(absoluteFilePath);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tga", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".psd", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetClipSortOrder(string clipId)
    {
        string key = NormalizeToken(clipId);
        return key switch
        {
            "idle" => 0,
            "walk" => 1,
            "run" => 2,
            _ => 10
        };
    }

    private static int GetAngleSortOrder(string angleLabel)
    {
        string key = NormalizeToken(angleLabel);
        return key switch
        {
            "front" => 0,
            "frontright" => 1,
            "frontsideright" => 2,
            "right" => 3,
            "backsideright" => 4,
            "backright" => 5,
            "back" => 6,
            "backleft" => 7,
            "left" => 8,
            "frontleft" => 9,
            _ => 99
        };
    }

    private static int GetFrameSortKey(string frameKey)
    {
        return int.TryParse(frameKey, out int value) ? value : int.MaxValue;
    }

    private static float GuessFramesPerSecond(string clipId)
    {
        return NormalizeToken(clipId) == "idle" ? 4f : 8f;
    }

    private static string NormalizeToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).Trim().ToLowerInvariant();
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
        return existing != null ? existing : target.AddComponent<T>();
    }

    private static string SanitizePathToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unnamed";
        }

        string sanitized = value.Trim();
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalidCharacter, '_');
        }

        return sanitized.Replace(' ', '_');
    }

    private sealed class DiscoveredFramePair
    {
        public string clipId;
        public string angleLabel;
        public string frameKey;
        public Sprite albedo;
        public Texture2D normal;
        public Texture2D packedMasks;
        public Texture2D emission;
    }

    private enum DiscoveredLayerKind
    {
        Albedo = 0,
        Normal = 1,
        PackedMasks = 2,
        Emission = 3
    }
}

internal sealed class DirectionalSpriteFrameBuildOptions
{
    public string assetName;
    public bool buildPrefab = true;
    public bool instantiateInScene;
    public bool addLocomotion = true;
    public float worldScaleMultiplier = 1f;
    public Transform sceneParent;
    public Vector3 scenePosition;
}

internal sealed class DirectionalSpriteFrameBuildResult
{
    public DirectionalSpriteDefinition definition;
    public GameObject prefabAsset;
    public GameObject sceneInstance;
    public string outputFolder;
}
