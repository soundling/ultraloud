using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class DirectionalSpriteSheetImporterWindow : EditorWindow
{
    private Texture2D sourceSheet;
    private DirectionalSpriteImportProfile profile;
    private DefaultAsset outputFolder;
    private string assetName = "DirectionalSpriteDefinition";

    [MenuItem("Tools/Directional Sprites/Import Sheet")]
    private static void OpenWindow()
    {
        GetWindow<DirectionalSpriteSheetImporterWindow>("Directional Sprite Import");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sheet Import", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Pick a tilesheet and an import profile. The profile describes the grid layout and where each clip/angle/frame lives on that sheet.",
            MessageType.Info);

        using (new EditorGUI.ChangeCheckScope())
        {
            sourceSheet = (Texture2D)EditorGUILayout.ObjectField("Source Sheet", sourceSheet, typeof(Texture2D), false);
            profile = (DirectionalSpriteImportProfile)EditorGUILayout.ObjectField("Import Profile", profile, typeof(DirectionalSpriteImportProfile), false);
            outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);

            if (sourceSheet != null && string.Equals(assetName, "DirectionalSpriteDefinition", StringComparison.Ordinal))
            {
                assetName = sourceSheet.name;
            }
        }

        assetName = EditorGUILayout.TextField("Definition Name", assetName);

        string resolvedFolder = DirectionalSpriteSheetImporter.ResolveOutputFolder(sourceSheet, profile, outputFolder);
        EditorGUILayout.LabelField("Resolved Folder", string.IsNullOrWhiteSpace(resolvedFolder) ? "(assign sheet + profile)" : resolvedFolder);

        using (new EditorGUI.DisabledScope(sourceSheet == null || profile == null))
        {
            if (GUILayout.Button("Import"))
            {
                try
                {
                    DirectionalSpriteDefinition definition = DirectionalSpriteSheetImporter.ImportSheet(
                        sourceSheet,
                        profile,
                        outputFolder,
                        string.IsNullOrWhiteSpace(assetName) ? sourceSheet.name : assetName);

                    if (definition != null)
                    {
                        Selection.activeObject = definition;
                        EditorGUIUtility.PingObject(definition);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    EditorUtility.DisplayDialog("Directional Sprite Import Failed", exception.Message, "OK");
                }
            }
        }
    }
}

internal static class DirectionalSpriteSheetImporter
{
    public static DirectionalSpriteDefinition ImportSheet(
        Texture2D sourceSheet,
        DirectionalSpriteImportProfile profile,
        DefaultAsset outputFolderAsset,
        string assetName)
    {
        if (sourceSheet == null)
        {
            throw new InvalidOperationException("A source sheet is required.");
        }

        if (profile == null)
        {
            throw new InvalidOperationException("An import profile is required.");
        }

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

        TextureImporter sourceImporter = AssetImporter.GetAtPath(sourceSheetPath) as TextureImporter;
        bool previousReadable = false;
        TextureImporterCompression previousCompression = TextureImporterCompression.Uncompressed;

        if (sourceImporter != null)
        {
            previousReadable = sourceImporter.isReadable;
            previousCompression = sourceImporter.textureCompression;

            bool importerChanged = false;
            if (!sourceImporter.isReadable)
            {
                sourceImporter.isReadable = true;
                importerChanged = true;
            }

            if (sourceImporter.textureCompression != TextureImporterCompression.Uncompressed)
            {
                sourceImporter.textureCompression = TextureImporterCompression.Uncompressed;
                importerChanged = true;
            }

            if (importerChanged)
            {
                sourceImporter.SaveAndReimport();
                sourceSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceSheetPath);
            }
        }

        try
        {
            string safeAssetName = SanitizePathToken(string.IsNullOrWhiteSpace(assetName) ? sourceSheet.name : assetName);
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
                        frames = new List<Sprite>(Mathf.Max(1, angleTemplate.frameCount))
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
            return definition;
        }
        finally
        {
            sourceImporter = AssetImporter.GetAtPath(sourceSheetPath) as TextureImporter;
            if (sourceImporter != null)
            {
                bool importerChanged = false;

                if (sourceImporter.isReadable != previousReadable)
                {
                    sourceImporter.isReadable = previousReadable;
                    importerChanged = true;
                }

                if (sourceImporter.textureCompression != previousCompression)
                {
                    sourceImporter.textureCompression = previousCompression;
                    importerChanged = true;
                }

                if (importerChanged)
                {
                    sourceImporter.SaveAndReimport();
                }
            }
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
}
