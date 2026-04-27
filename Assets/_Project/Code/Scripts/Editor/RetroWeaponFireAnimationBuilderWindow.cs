using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class RetroWeaponFireAnimationBuilderWindow : EditorWindow
{
    private const int FrameCount = 4;
    private const byte ViewOffsetAlphaThreshold = 16;
    private const float ViewOffsetIgnoredTopFraction = 0.33f;
    private const string SpriteRootPath = "Assets/_Project/Art/Sprites/Weapons/Viewmodels";
    private const string MapSetRootPath = "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation";

    private static readonly WeaponFireAnimationSpec[] Specs =
    {
        new WeaponFireAnimationSpec(
            "Pistol",
            "Assets/_Project/Content/Gameplay/Weapons/Definitions/Pistol.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/PistolViewmodelMapSet.asset",
            0.024f,
            new Vector3(0f, 0.007f, -0.068f),
            new Vector3(5.6f, 1.35f, 1.9f),
            new Color(1f, 0.58f, 0.18f, 1f)),
        new WeaponFireAnimationSpec(
            "Rifle",
            "Assets/_Project/Content/Gameplay/Weapons/Definitions/Rifle.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/RifleViewmodelMapSet.asset",
            0.018f,
            new Vector3(0f, 0.009f, -0.044f),
            new Vector3(3.25f, 0.8f, 1.35f),
            new Color(1f, 0.64f, 0.16f, 1f)),
        new WeaponFireAnimationSpec(
            "Shotgun",
            "Assets/_Project/Content/Gameplay/Weapons/Definitions/Shotgun.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/ShotgunViewmodelMapSet.asset",
            0.032f,
            new Vector3(0f, 0.012f, -0.11f),
            new Vector3(9.5f, 1.7f, 2.35f),
            new Color(1f, 0.5f, 0.12f, 1f)),
        new WeaponFireAnimationSpec(
            "GrenadeLauncher",
            "Assets/_Project/Content/Gameplay/Weapons/Definitions/GrenadeLauncher.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/GrenadeLauncherViewmodelMapSet.asset",
            0.034f,
            new Vector3(0f, 0.018f, -0.145f),
            new Vector3(12f, 2.45f, 3f),
            new Color(1f, 0.62f, 0.13f, 1f))
    };

    private Vector2 scroll;

    [MenuItem("Tools/Ultraloud/Weapons/Build Fire Animation Viewmodels")]
    public static void Open()
    {
        RetroWeaponFireAnimationBuilderWindow window = GetWindow<RetroWeaponFireAnimationBuilderWindow>("Weapon Fire Anim");
        window.minSize = new Vector2(640f, 480f);
    }

    [MenuItem("Tools/Ultraloud/Weapons/Build Fire Animation Viewmodels/Build All Assets")]
    public static void BuildAllAssetsMenu()
    {
        int mapSetCount = BuildAllAssets();
        EditorUtility.DisplayDialog("Weapon Fire Animation", $"Built {mapSetCount} fire animation map sets and assigned weapon definitions.", "OK");
    }

    public static int BuildAllAssets()
    {
        EnsureAssetFolderExists(MapSetRootPath);
        AssetDatabase.Refresh();

        int mapSetCount = 0;
        for (int i = 0; i < Specs.Length; i++)
        {
            mapSetCount += BuildSpec(Specs[i]);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return mapSetCount;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Weapon Fire Animation Viewmodels", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Builds 4-frame sprite-volume firing animations from the ImageGen 2x2 sheets, creates map set assets, and assigns them to the weapon definitions.", MessageType.Info);

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Build All Assets", EditorStyles.toolbarButton, GUILayout.Width(112f)))
            {
                BuildAllAssetsMenu();
            }

            if (GUILayout.Button("Ping Output Folder", EditorStyles.toolbarButton, GUILayout.Width(124f)))
            {
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MapSetRootPath));
            }
        }

        EditorGUILayout.Space(8f);
        using (EditorGUILayout.ScrollViewScope scope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scope.scrollPosition;
            for (int i = 0; i < Specs.Length; i++)
            {
                DrawSpec(Specs[i]);
            }
        }
    }

    private static void DrawSpec(WeaponFireAnimationSpec spec)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(spec.Name, EditorStyles.boldLabel);
        EditorGUILayout.ObjectField("Definition", AssetDatabase.LoadAssetAtPath<RetroWeaponDefinition>(spec.DefinitionPath), typeof(RetroWeaponDefinition), false);
        EditorGUILayout.ObjectField("Base Map Set", AssetDatabase.LoadAssetAtPath<FirstPersonSpriteVolumeMapSet>(spec.ReferenceMapSetPath), typeof(FirstPersonSpriteVolumeMapSet), false);
        EditorGUILayout.LabelField("Source Sheet", BuildSourcePath(spec));
        EditorGUILayout.LabelField("Frame Duration", $"{spec.FrameDuration:0.000}s");
    }

    private static int BuildSpec(WeaponFireAnimationSpec spec)
    {
        string sourcePath = BuildSourcePath(spec);
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath) == null)
        {
            Debug.LogWarning($"Missing source fire animation sheet for {spec.Name}: {sourcePath}");
        }

        ConfigureTextureImporter(sourcePath, normalMap: false, depthMap: false, alpha: true);

        FirstPersonSpriteVolumeMapSet[] mapSets = new FirstPersonSpriteVolumeMapSet[FrameCount];
        for (int frame = 0; frame < FrameCount; frame++)
        {
            ConfigureFrameImporters(spec, frame);
            mapSets[frame] = CreateOrUpdateMapSet(spec, frame);
        }

        AssignDefinition(spec, mapSets);
        return FrameCount;
    }

    private static void ConfigureFrameImporters(WeaponFireAnimationSpec spec, int frame)
    {
        ConfigureTextureImporter(BuildTexturePath(spec, frame, "Base"), normalMap: false, depthMap: false, alpha: true);
        ConfigureTextureImporter(BuildTexturePath(spec, frame, "Normal"), normalMap: true, depthMap: false, alpha: false);
        ConfigureTextureImporter(BuildTexturePath(spec, frame, "Depth"), normalMap: false, depthMap: true, alpha: true);
        ConfigureTextureImporter(BuildTexturePath(spec, frame, "Emission"), normalMap: false, depthMap: false, alpha: true);
    }

    private static FirstPersonSpriteVolumeMapSet CreateOrUpdateMapSet(WeaponFireAnimationSpec spec, int frame)
    {
        string path = BuildMapSetPath(spec, frame);
        EnsureAssetFolderExists(Path.GetDirectoryName(path)?.Replace('\\', '/'));

        FirstPersonSpriteVolumeMapSet mapSet = AssetDatabase.LoadAssetAtPath<FirstPersonSpriteVolumeMapSet>(path);
        if (mapSet == null)
        {
            mapSet = CreateInstance<FirstPersonSpriteVolumeMapSet>();
            AssetDatabase.CreateAsset(mapSet, path);
        }

        FirstPersonSpriteVolumeMapSet reference = AssetDatabase.LoadAssetAtPath<FirstPersonSpriteVolumeMapSet>(spec.ReferenceMapSetPath);
        if (reference != null)
        {
            CopyMapSetMaterial(reference, mapSet);
        }

        mapSet.baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BuildTexturePath(spec, frame, "Base"));
        mapSet.normal = AssetDatabase.LoadAssetAtPath<Texture2D>(BuildTexturePath(spec, frame, "Normal"));
        mapSet.frontDepth = AssetDatabase.LoadAssetAtPath<Texture2D>(BuildTexturePath(spec, frame, "Depth"));
        mapSet.emissive = AssetDatabase.LoadAssetAtPath<Texture2D>(BuildTexturePath(spec, frame, "Emission"));
        mapSet.viewOffset = CalculateFrameViewOffset(reference, mapSet.baseColor);
        mapSet.emissiveColor = spec.EmissionColor;
        mapSet.normalScale = Mathf.Max(mapSet.normalScale, 1.08f);
        mapSet.transmissionStrength = Mathf.Max(mapSet.transmissionStrength, 0.12f);
        mapSet.specularStrength = Mathf.Max(mapSet.specularStrength, 1.05f);
        EditorUtility.SetDirty(mapSet);
        return mapSet;
    }

    private static void CopyMapSetMaterial(FirstPersonSpriteVolumeMapSet source, FirstPersonSpriteVolumeMapSet target)
    {
        target.volumeThickness = source.volumeThickness;
        target.parallaxScale = source.parallaxScale;
        target.invertFrontDepth = source.invertFrontDepth;
        target.baseColorTint = source.baseColorTint;
        target.emissiveColor = source.emissiveColor;
        target.normalScale = source.normalScale;
        target.alphaCutoff = source.alphaCutoff;
        target.preserveBaseCoverage = source.preserveBaseCoverage;
        target.coverageThreshold = source.coverageThreshold;
        target.selfShadowStrength = source.selfShadowStrength;
        target.transmissionStrength = source.transmissionStrength;
        target.ambientOcclusionStrength = source.ambientOcclusionStrength;
        target.specularStrength = source.specularStrength;
        target.ambientOcclusion = source.ambientOcclusion;
        target.roughness = source.roughness;
        target.metallic = source.metallic;
        target.materialThickness = source.materialThickness;
    }

    private static Vector2 CalculateFrameViewOffset(FirstPersonSpriteVolumeMapSet reference, Texture2D frameBaseColor)
    {
        if (reference == null || reference.baseColor == null || frameBaseColor == null)
        {
            return Vector2.zero;
        }

        if (!TryReadTextureAlphaBounds(reference.baseColor, out TextureAlphaBounds referenceBounds)
            || !TryReadTextureAlphaBounds(frameBaseColor, out TextureAlphaBounds frameBounds))
        {
            return reference.viewOffset;
        }

        if (referenceBounds.Width <= 0
            || referenceBounds.Height <= 0
            || referenceBounds.Width != frameBounds.Width
            || referenceBounds.Height != frameBounds.Height)
        {
            return reference.viewOffset;
        }

        return reference.viewOffset + new Vector2(
            (referenceBounds.CenterX - frameBounds.CenterX) / referenceBounds.Width,
            (referenceBounds.CenterY - frameBounds.CenterY) / referenceBounds.Height);
    }

    private static bool TryReadTextureAlphaBounds(Texture2D texture, out TextureAlphaBounds bounds)
    {
        bounds = default;
        if (texture == null)
        {
            return false;
        }

        string assetPath = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
        {
            return false;
        }

        byte[] bytes = File.ReadAllBytes(assetPath);
        Texture2D readableTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        try
        {
            if (!readableTexture.LoadImage(bytes, false))
            {
                return false;
            }

            Color32[] pixels = readableTexture.GetPixels32();
            int width = readableTexture.width;
            int height = readableTexture.height;
            int trimmedTopYExclusive = Mathf.Clamp(
                Mathf.CeilToInt(height * (1f - ViewOffsetIgnoredTopFraction)),
                1,
                height);

            return TryFindAlphaBounds(pixels, width, height, trimmedTopYExclusive, out bounds)
                || TryFindAlphaBounds(pixels, width, height, height, out bounds);
        }
        finally
        {
            DestroyImmediate(readableTexture);
        }
    }

    private static bool TryFindAlphaBounds(Color32[] pixels, int width, int height, int maxYExclusive, out TextureAlphaBounds bounds)
    {
        bounds = default;
        if (pixels == null || width <= 0 || height <= 0 || maxYExclusive <= 0)
        {
            return false;
        }

        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;
        int safeMaxYExclusive = Mathf.Clamp(maxYExclusive, 1, height);

        for (int y = 0; y < safeMaxYExclusive; y++)
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                if (pixels[rowOffset + x].a <= ViewOffsetAlphaThreshold)
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            return false;
        }

        bounds = new TextureAlphaBounds(width, height, minX, minY, maxX, maxY);
        return true;
    }

    private static void AssignDefinition(WeaponFireAnimationSpec spec, FirstPersonSpriteVolumeMapSet[] mapSets)
    {
        RetroWeaponDefinition definition = AssetDatabase.LoadAssetAtPath<RetroWeaponDefinition>(spec.DefinitionPath);
        if (definition == null)
        {
            Debug.LogWarning($"Missing weapon definition for fire animation assignment: {spec.DefinitionPath}");
            return;
        }

        definition.fireAnimationMapSets = mapSets;
        definition.fireAnimationFrameDuration = spec.FrameDuration;
        definition.recoilPosition = spec.RecoilPosition;
        definition.recoilEuler = spec.RecoilEuler;
        EditorUtility.SetDirty(definition);
    }

    private static void ConfigureTextureImporter(string assetPath, bool normalMap, bool depthMap, bool alpha)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return;
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = alpha && !normalMap;
        importer.mipMapsPreserveCoverage = alpha && !normalMap;
        importer.sRGBTexture = !normalMap && !depthMap;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    private static string BuildSourcePath(WeaponFireAnimationSpec spec)
    {
        return $"{SpriteRootPath}/{spec.Name}/FireAnimation/Source/{spec.Name}_Fire_2x2_Imagen.png";
    }

    private static string BuildTexturePath(WeaponFireAnimationSpec spec, int frame, string suffix)
    {
        return $"{SpriteRootPath}/{spec.Name}/FireAnimation/Textures/{spec.Name}_Fire_{frame:00}_{suffix}.png";
    }

    private static string BuildMapSetPath(WeaponFireAnimationSpec spec, int frame)
    {
        return $"{MapSetRootPath}/{spec.Name}/{spec.Name}FireFrame{frame:00}.asset";
    }

    private static void EnsureAssetFolderExists(string assetFolderPath)
    {
        if (string.IsNullOrWhiteSpace(assetFolderPath) || AssetDatabase.IsValidFolder(assetFolderPath))
        {
            return;
        }

        string normalizedPath = assetFolderPath.Replace('\\', '/');
        string parentPath = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(normalizedPath);

        if (!string.IsNullOrWhiteSpace(parentPath) && !AssetDatabase.IsValidFolder(parentPath))
        {
            EnsureAssetFolderExists(parentPath);
        }

        if (!string.IsNullOrWhiteSpace(parentPath) && !string.IsNullOrWhiteSpace(folderName))
        {
            AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }

    private readonly struct TextureAlphaBounds
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float CenterX;
        public readonly float CenterY;

        public TextureAlphaBounds(int width, int height, int minX, int minY, int maxX, int maxY)
        {
            Width = width;
            Height = height;
            CenterX = (minX + maxX) * 0.5f;
            CenterY = (minY + maxY) * 0.5f;
        }
    }

    [Serializable]
    private sealed class WeaponFireAnimationSpec
    {
        public readonly string Name;
        public readonly string DefinitionPath;
        public readonly string ReferenceMapSetPath;
        public readonly float FrameDuration;
        public readonly Vector3 RecoilPosition;
        public readonly Vector3 RecoilEuler;
        public readonly Color EmissionColor;

        public WeaponFireAnimationSpec(
            string name,
            string definitionPath,
            string referenceMapSetPath,
            float frameDuration,
            Vector3 recoilPosition,
            Vector3 recoilEuler,
            Color emissionColor)
        {
            Name = name;
            DefinitionPath = definitionPath;
            ReferenceMapSetPath = referenceMapSetPath;
            FrameDuration = frameDuration;
            RecoilPosition = recoilPosition;
            RecoilEuler = recoilEuler;
            EmissionColor = emissionColor;
        }
    }
}
