using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class RetroImagenEntityImportBatch
{
    private const int DefaultColumns = 8;
    private const int DefaultRows = 5;
    private const float PixelsPerUnit = 100f;
    private const double AutoImportPollIntervalSeconds = 1.0;
    private const string AutoRunRequestPath = "ProjectSettings/RetroImagenEntityImport.request";
    private const string GoreProfilePath = "Assets/_Project/Art/Sprites/Effects/Gore/PigGoreProfile.asset";

    private static readonly AngleSpec[] Angles =
    {
        new("Front", 0),
        new("FrontRight", 45),
        new("Right", 90),
        new("BackRight", 135),
        new("Back", 180)
    };

    private static double nextAutoImportPollAt;

    static RetroImagenEntityImportBatch()
    {
        EditorApplication.delayCall += TryRunPendingAutoImport;
        EditorApplication.update += TryRunPendingAutoImportOnUpdate;
    }

    [MenuItem("Tools/Ultraloud/Entities/Import Imagen Entity Sprites/Import All")]
    public static void ImportAllMenu()
    {
        ImportAll(selectAsset: true);
    }

    [MenuItem("Tools/Ultraloud/Entities/Import Imagen Entity Sprites/Import Butcher And Abomination")]
    public static void ImportButcherAndAbominationMenu()
    {
        ImportButcherAndAbomination(selectAsset: true);
    }

    [MenuItem("Tools/Ultraloud/Entities/Import Imagen Entity Sprites/Import Cat Dog Butcher And Abomination")]
    public static void ImportCatDogButcherAndAbominationMenu()
    {
        ImportCatDogButcherAndAbomination(selectAsset: true);
    }

    [MenuItem("Tools/Ultraloud/Entities/Import Imagen Entity Sprites/Import Cat Dog Butcher And Abomination No Select")]
    public static void ImportCatDogButcherAndAbominationNoSelectMenu()
    {
        ImportCatDogButcherAndAbomination(selectAsset: false);
    }

    [MenuItem("Tools/Ultraloud/Entities/Import Imagen Entity Sprites/Import Cat Only")]
    public static void ImportCatMenu()
    {
        ImportCat(selectAsset: true);
    }

    [MenuItem("Tools/Ultraloud/Entities/Import Imagen Entity Sprites/Import Cat Only No Select")]
    public static void ImportCatNoSelectMenu()
    {
        ImportCat(selectAsset: false);
    }

    [MenuItem("Tools/Ultraloud/Entities/Import Imagen Entity Sprites/Import Dog Only")]
    public static void ImportDogMenu()
    {
        ImportDog(selectAsset: true);
    }

    [MenuItem("Tools/Ultraloud/Entities/Import Imagen Entity Sprites/Import Dog Only No Select")]
    public static void ImportDogNoSelectMenu()
    {
        ImportDog(selectAsset: false);
    }

    [MenuItem("Tools/Ultraloud/Entities/Import Imagen Entity Sprites/Import Rat Only")]
    public static void ImportRatMenu()
    {
        ImportRat(selectAsset: true);
    }

    [MenuItem("Tools/Ultraloud/Entities/Import Imagen Entity Sprites/Import Rat Only No Select")]
    public static void ImportRatNoSelectMenu()
    {
        ImportRat(selectAsset: false);
    }

    [MenuItem("Tools/Ultraloud/Entities/Import Imagen Entity Sprites/Import Mawtick Only")]
    public static void ImportMawtickMenu()
    {
        ImportMawtick(selectAsset: true);
    }

    [MenuItem("Tools/Ultraloud/Entities/Import Imagen Entity Sprites/Import Mawtick Only No Select")]
    public static void ImportMawtickNoSelectMenu()
    {
        ImportMawtick(selectAsset: false);
    }

    public static void ImportAllFromCommandLine()
    {
        try
        {
            ImportAll(selectAsset: false);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void ImportButcherAndAbominationFromCommandLine()
    {
        try
        {
            ImportButcherAndAbomination(selectAsset: false);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void ImportCatDogButcherAndAbominationFromCommandLine()
    {
        try
        {
            ImportCatDogButcherAndAbomination(selectAsset: false);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void ImportCatFromCommandLine()
    {
        try
        {
            ImportCat(selectAsset: false);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void ImportDogFromCommandLine()
    {
        try
        {
            ImportDog(selectAsset: false);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void ImportRatFromCommandLine()
    {
        try
        {
            ImportRat(selectAsset: false);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void ImportMawtickFromCommandLine()
    {
        try
        {
            ImportMawtick(selectAsset: false);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void TryRunPendingAutoImport()
    {
        string requestPath = Path.GetFullPath(AutoRunRequestPath);
        if (!File.Exists(requestPath))
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryRunPendingAutoImport;
            return;
        }

        string request = File.ReadAllText(requestPath);
        bool bossesOnly = request.IndexOf("butcher", StringComparison.OrdinalIgnoreCase) >= 0
            || request.IndexOf("abomination", StringComparison.OrdinalIgnoreCase) >= 0
            || request.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0;
        bool catOnly = request.IndexOf("cat", StringComparison.OrdinalIgnoreCase) >= 0
            && request.IndexOf("only", StringComparison.OrdinalIgnoreCase) >= 0;
        bool dogOnly = request.IndexOf("dog", StringComparison.OrdinalIgnoreCase) >= 0
            && request.IndexOf("only", StringComparison.OrdinalIgnoreCase) >= 0;
        bool ratOnly = request.IndexOf("rat", StringComparison.OrdinalIgnoreCase) >= 0
            && request.IndexOf("only", StringComparison.OrdinalIgnoreCase) >= 0;
        bool mawtickOnly = request.IndexOf("mawtick", StringComparison.OrdinalIgnoreCase) >= 0
            && request.IndexOf("only", StringComparison.OrdinalIgnoreCase) >= 0;
        bool catDogBosses = bossesOnly
            && (request.IndexOf("cat", StringComparison.OrdinalIgnoreCase) >= 0
                || request.IndexOf("dog", StringComparison.OrdinalIgnoreCase) >= 0);

        Debug.Log(catOnly
            ? "Retro Imagen entity import request detected. Importing Cat."
            : dogOnly
            ? "Retro Imagen entity import request detected. Importing Dog."
            : ratOnly
            ? "Retro Imagen entity import request detected. Importing Rat."
            : mawtickOnly
            ? "Retro Imagen entity import request detected. Importing Mawtick."
            : catDogBosses
            ? "Retro Imagen entity import request detected. Importing Cat, Dog, ButcherBoss, and AbominationMonster."
            : bossesOnly
            ? "Retro Imagen entity import request detected. Importing ButcherBoss and AbominationMonster."
            : "Retro Imagen entity import request detected. Importing Mawtick, Rat, Dog, Cat, ButcherBoss, and AbominationMonster.");
        File.Delete(requestPath);
        if (catOnly)
        {
            ImportCat(selectAsset: false);
        }
        else if (dogOnly)
        {
            ImportDog(selectAsset: false);
        }
        else if (ratOnly)
        {
            ImportRat(selectAsset: false);
        }
        else if (mawtickOnly)
        {
            ImportMawtick(selectAsset: false);
        }
        else if (catDogBosses)
        {
            ImportCatDogButcherAndAbomination(selectAsset: false);
        }
        else if (bossesOnly)
        {
            ImportButcherAndAbomination(selectAsset: false);
        }
        else
        {
            ImportAll(selectAsset: false);
        }
    }

    private static void TryRunPendingAutoImportOnUpdate()
    {
        if (EditorApplication.timeSinceStartup < nextAutoImportPollAt)
        {
            return;
        }

        nextAutoImportPollAt = EditorApplication.timeSinceStartup + AutoImportPollIntervalSeconds;

        if (!File.Exists(Path.GetFullPath(AutoRunRequestPath)))
        {
            return;
        }

        TryRunPendingAutoImport();
    }

    private static void ImportAll(bool selectAsset)
    {
        ImportSpecs(selectAsset, null, "Mawtick, Rat, Dog, Cat, ButcherBoss, and AbominationMonster");
    }

    private static void ImportButcherAndAbomination(bool selectAsset)
    {
        ImportSpecs(
            selectAsset,
            static spec => IsNamed(spec, "ButcherBoss", "AbominationMonster"),
            "ButcherBoss and AbominationMonster");
    }

    private static void ImportCatDogButcherAndAbomination(bool selectAsset)
    {
        ImportSpecs(
            selectAsset,
            static spec => IsNamed(spec, "Cat", "Dog", "ButcherBoss", "AbominationMonster"),
            "Cat, Dog, ButcherBoss, and AbominationMonster");
    }

    private static void ImportCat(bool selectAsset)
    {
        ImportSpecs(
            selectAsset,
            static spec => IsNamed(spec, "Cat"),
            "Cat");
    }

    private static void ImportDog(bool selectAsset)
    {
        ImportSpecs(
            selectAsset,
            static spec => IsNamed(spec, "Dog"),
            "Dog");
    }

    private static void ImportRat(bool selectAsset)
    {
        ImportSpecs(
            selectAsset,
            static spec => IsNamed(spec, "Rat"),
            "Rat");
    }

    private static void ImportMawtick(bool selectAsset)
    {
        ImportSpecs(
            selectAsset,
            static spec => IsNamed(spec, "Mawtick"),
            "Mawtick");
    }

    private static bool IsNamed(EntityImportSpec spec, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(spec.Name, names[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void ImportSpecs(bool selectAsset, Func<EntityImportSpec, bool> includeSpec, string label)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        GameObject lastPrefab = null;
        foreach (EntityImportSpec spec in CreateSpecs())
        {
            if (includeSpec != null && !includeSpec(spec))
            {
                continue;
            }

            GameObject prefab = ImportEntity(spec);
            if (prefab != null)
            {
                lastPrefab = prefab;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (selectAsset && lastPrefab != null)
        {
            Selection.activeObject = lastPrefab;
            EditorGUIUtility.PingObject(lastPrefab);
        }

        Debug.Log($"Imagen entity import completed for {label}.");
    }

    private static GameObject ImportEntity(EntityImportSpec spec)
    {
        string generatedRoot = $"{spec.ArtRoot}/Generated";
        string framesRoot = $"{generatedRoot}/Frames";
        EnsureAssetFolder(spec.ArtRoot);
        EnsureAssetFolder(generatedRoot);
        ResetGeneratedFrames(framesRoot);

        foreach (ClipSpec clip in spec.Clips)
        {
            string sheetPath = $"{spec.ArtRoot}/Source/{spec.SourcePrefix}_Imagen_{clip.Id}_Chroma.png";
            SliceSheet(spec, clip, sheetPath, framesRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        DefaultAsset framesRootAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(framesRoot);
        DefaultAsset outputRootAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(generatedRoot);
        DirectionalSpriteFrameBuildResult result = DirectionalSpriteFrameBuilder.Build(
            framesRootAsset,
            outputRootAsset,
            new DirectionalSpriteFrameBuildOptions
            {
                assetName = spec.SpriteAssetName,
                buildPrefab = true,
                instantiateInScene = false,
                addLocomotion = spec.AddLocomotion,
                worldScaleMultiplier = spec.WorldScaleMultiplier
            });

        ConfigureDefinition(result.definition, spec);
        ConfigureGeneratedSpritePrefab(result.prefabAsset, result.definition, spec);
        return CreateOrUpdateTestPrefab(result.prefabAsset, result.definition, spec);
    }

    private static void SliceSheet(EntityImportSpec spec, ClipSpec clip, string sheetPath, string framesRoot)
    {
        Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
        if (sheet == null)
        {
            throw new FileNotFoundException($"Missing Imagen source sheet for {spec.Name}/{clip.Id}: {sheetPath}");
        }

        TextureImportState readState = PrepareReadableTexture(sheetPath);
        sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);

        try
        {
            AngleSpec[] sheetAngles = spec.GetSheetAngles();
            List<ComponentBounds>[] rowComponents = spec.UseComponentAlignedCells
                ? DetectRowComponents(sheet, spec)
                : null;
            for (int row = 0; row < spec.SheetRows; row++)
            {
                AngleSpec angle = sheetAngles[row];
                for (int column = 0; column < spec.SheetColumns; column++)
                {
                    RectInt rect = ResolveCell(sheet.width, sheet.height, spec.SheetColumns, spec.SheetRows, column, row);
                    Color[] sourcePixels = spec.UseComponentAlignedCells
                        ? BuildComponentAlignedCell(sheet, spec, rowComponents[row], row, column, rect.width, rect.height)
                        : sheet.GetPixels(rect.x, rect.y, rect.width, rect.height);
                    Color[] albedoPixels = BuildAlbedoPixels(sourcePixels, rect.width, rect.height, spec);
                    string frameKey = column.ToString("D2");
                    string clipAngleRoot = $"{framesRoot}/{clip.Id}/{angle.Label}";

                    WriteTexture(
                        $"{clipAngleRoot}/Albedo/{frameKey}.png",
                        rect.width,
                        rect.height,
                        albedoPixels,
                        TextureImporterType.Sprite,
                        true,
                        spec.SpritePivot);

                    WriteTexture(
                        $"{clipAngleRoot}/Normal/{frameKey}.png",
                        rect.width,
                        rect.height,
                        BuildNormalPixels(albedoPixels, rect.width, rect.height),
                        TextureImporterType.Default,
                        false,
                        spec.SpritePivot);

                    WriteTexture(
                        $"{clipAngleRoot}/PackedMasks/{frameKey}.png",
                        rect.width,
                        rect.height,
                        BuildPackedMaskPixels(albedoPixels, spec),
                        TextureImporterType.Default,
                        false,
                        spec.SpritePivot);

                    WriteTexture(
                        $"{clipAngleRoot}/Emission/{frameKey}.png",
                        rect.width,
                        rect.height,
                        BuildEmissionPixels(albedoPixels, spec),
                        TextureImporterType.Default,
                        true,
                        spec.SpritePivot);
                }
            }
        }
        finally
        {
            RestoreTextureState(readState);
        }
    }

    private static RectInt ResolveCell(int width, int height, int columns, int rows, int column, int row)
    {
        int xMin = Mathf.RoundToInt(column * width / (float)columns);
        int xMax = Mathf.RoundToInt((column + 1) * width / (float)columns);
        int topMin = Mathf.RoundToInt(row * height / (float)rows);
        int topMax = Mathf.RoundToInt((row + 1) * height / (float)rows);
        int cellHeight = Mathf.Max(1, topMax - topMin);
        int y = height - topMax;
        return new RectInt(xMin, y, Mathf.Max(1, xMax - xMin), cellHeight);
    }

    private static RectInt ResolveRow(int width, int height, int rows, int row)
    {
        int topMin = Mathf.RoundToInt(row * height / (float)rows);
        int topMax = Mathf.RoundToInt((row + 1) * height / (float)rows);
        int y = height - topMax;
        return new RectInt(0, y, width, Mathf.Max(1, topMax - topMin));
    }

    private static List<ComponentBounds>[] DetectRowComponents(Texture2D sheet, EntityImportSpec spec)
    {
        List<ComponentBounds>[] rows = new List<ComponentBounds>[spec.SheetRows];
        for (int row = 0; row < spec.SheetRows; row++)
        {
            RectInt rowRect = ResolveRow(sheet.width, sheet.height, spec.SheetRows, row);
            Color[] pixels = sheet.GetPixels(rowRect.x, rowRect.y, rowRect.width, rowRect.height);
            List<ComponentBounds> components = DetectOpaqueComponents(pixels, rowRect.width, rowRect.height, spec.ChromaKey);
            components.Sort((left, right) => right.Pixels.CompareTo(left.Pixels));
            if (components.Count > spec.SheetColumns)
            {
                components.RemoveRange(spec.SheetColumns, components.Count - spec.SheetColumns);
            }

            components.Sort((left, right) => left.CenterX.CompareTo(right.CenterX));
            rows[row] = components;
        }

        return rows;
    }

    private static List<ComponentBounds> DetectOpaqueComponents(Color[] pixels, int width, int height, ChromaKeyKind chromaKey)
    {
        int minComponentPixels = Mathf.Clamp((width * height) / 3000, 80, 240);
        bool[] visited = new bool[pixels.Length];
        int[] queue = new int[pixels.Length];
        List<ComponentBounds> components = new();

        for (int start = 0; start < pixels.Length; start++)
        {
            if (visited[start] || IsChroma(pixels[start], chromaKey))
            {
                continue;
            }

            int head = 0;
            int tail = 0;
            int minX = width;
            int maxX = 0;
            int minY = height;
            int maxY = 0;
            visited[start] = true;
            queue[tail++] = start;

            while (head < tail)
            {
                int index = queue[head++];
                int x = index % width;
                int y = index / width;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);

                for (int yOffset = -1; yOffset <= 1; yOffset++)
                {
                    int neighborY = y + yOffset;
                    if (neighborY < 0 || neighborY >= height)
                    {
                        continue;
                    }

                    for (int xOffset = -1; xOffset <= 1; xOffset++)
                    {
                        if (xOffset == 0 && yOffset == 0)
                        {
                            continue;
                        }

                        int neighborX = x + xOffset;
                        if (neighborX < 0 || neighborX >= width)
                        {
                            continue;
                        }

                        int neighborIndex = neighborY * width + neighborX;
                        if (visited[neighborIndex] || IsChroma(pixels[neighborIndex], chromaKey))
                        {
                            continue;
                        }

                        visited[neighborIndex] = true;
                        queue[tail++] = neighborIndex;
                    }
                }
            }

            if (tail < minComponentPixels)
            {
                continue;
            }

            components.Add(new ComponentBounds(new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1), tail));
        }

        return components;
    }

    private static Color[] BuildComponentAlignedCell(Texture2D sheet, EntityImportSpec spec, List<ComponentBounds> components, int row, int column, int width, int height)
    {
        if (components == null || column >= components.Count)
        {
            Debug.LogWarning($"{spec.Name} row {row} has {components?.Count ?? 0} detected components; falling back to strict cell {column}.");
            RectInt fallback = ResolveCell(sheet.width, sheet.height, spec.SheetColumns, spec.SheetRows, column, row);
            return sheet.GetPixels(fallback.x, fallback.y, fallback.width, fallback.height);
        }

        RectInt rowRect = ResolveRow(sheet.width, sheet.height, spec.SheetRows, row);
        RectInt bounds = ExpandWithin(components[column].Rect, rowRect.width, rowRect.height, 8);
        Color[] crop = sheet.GetPixels(rowRect.x + bounds.x, rowRect.y + bounds.y, bounds.width, bounds.height);
        Color[] output = new Color[width * height];
        Color key = spec.ChromaKey == ChromaKeyKind.Green
            ? new Color(0f, 1f, 0f, 1f)
            : new Color(1f, 0f, 1f, 1f);

        for (int i = 0; i < output.Length; i++)
        {
            output[i] = key;
        }

        float maxWidth = width * 0.9f;
        float maxHeight = height * 0.9f;
        float scale = Mathf.Min(1f, Mathf.Min(maxWidth / bounds.width, maxHeight / bounds.height));
        int drawWidth = Mathf.Max(1, Mathf.RoundToInt(bounds.width * scale));
        int drawHeight = Mathf.Max(1, Mathf.RoundToInt(bounds.height * scale));
        int startX = Mathf.Clamp(Mathf.RoundToInt((width - drawWidth) * 0.5f), 0, width - 1);
        int startY = Mathf.Clamp(Mathf.RoundToInt((height - drawHeight) * 0.5f), 0, height - 1);

        for (int y = 0; y < drawHeight; y++)
        {
            int targetY = startY + y;
            if (targetY < 0 || targetY >= height)
            {
                continue;
            }

            for (int x = 0; x < drawWidth; x++)
            {
                int targetX = startX + x;
                if (targetX < 0 || targetX >= width)
                {
                    continue;
                }

                float sourceX = (x + 0.5f) / scale - 0.5f;
                float sourceY = (y + 0.5f) / scale - 0.5f;
                output[targetY * width + targetX] = BilinearSample(crop, bounds.width, bounds.height, sourceX, sourceY);
            }
        }

        return output;
    }

    private static RectInt ExpandWithin(RectInt rect, int width, int height, int padding)
    {
        int xMin = Mathf.Max(0, rect.xMin - padding);
        int xMax = Mathf.Min(width, rect.xMax + padding);
        int yMin = Mathf.Max(0, rect.yMin - padding);
        int yMax = Mathf.Min(height, rect.yMax + padding);
        return new RectInt(xMin, yMin, Mathf.Max(1, xMax - xMin), Mathf.Max(1, yMax - yMin));
    }

    private static Color BilinearSample(Color[] pixels, int width, int height, float x, float y)
    {
        x = Mathf.Clamp(x, 0f, width - 1f);
        y = Mathf.Clamp(y, 0f, height - 1f);
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        int x1 = Mathf.Min(width - 1, x0 + 1);
        int y1 = Mathf.Min(height - 1, y0 + 1);
        float tx = x - x0;
        float ty = y - y0;
        Color bottom = Color.Lerp(pixels[y0 * width + x0], pixels[y0 * width + x1], tx);
        Color top = Color.Lerp(pixels[y1 * width + x0], pixels[y1 * width + x1], tx);
        return Color.Lerp(bottom, top, ty);
    }

    private static Color[] BuildAlbedoPixels(Color[] source, int width, int height, EntityImportSpec spec)
    {
        ChromaKeyKind chromaKey = spec.ChromaKey;
        Color[] output = new Color[source.Length];
        bool[] transparent = new bool[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            Color color = source[i];
            if (IsChroma(color, chromaKey))
            {
                output[i] = new Color(0f, 0f, 0f, 0f);
                transparent[i] = true;
                continue;
            }

            color.a = 1f;
            output[i] = color;
        }

        bool[] fringeTransparent = new bool[source.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (transparent[index])
                {
                    continue;
                }

                float fringe = ChromaFringeScore(output[index], chromaKey);
                if (fringe < ChromaTransparentCutoff(chromaKey, firstPass: true))
                {
                    continue;
                }

                int neighborRadius = ResolveChromaNeighborRadius(fringe, chromaKey);
                if (HasTransparentNeighbor(transparent, width, height, x, y, neighborRadius))
                {
                    fringeTransparent[index] = true;
                }
            }
        }

        for (int i = 0; i < fringeTransparent.Length; i++)
        {
            if (!fringeTransparent[i])
            {
                continue;
            }

            transparent[i] = true;
            output[i] = new Color(0f, 0f, 0f, 0f);
        }

        int fringePassCount = chromaKey == ChromaKeyKind.Green ? 8 : 5;
        for (int pass = 0; pass < fringePassCount; pass++)
        {
            bool changed = false;
            Array.Clear(fringeTransparent, 0, fringeTransparent.Length);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (transparent[index])
                    {
                        continue;
                    }

                    float fringe = ChromaFringeScore(output[index], chromaKey);
                    if (fringe < ChromaTransparentCutoff(chromaKey, firstPass: false))
                    {
                        continue;
                    }

                    int neighborRadius = ResolveChromaNeighborRadius(fringe, chromaKey);
                    if (HasTransparentNeighbor(transparent, width, height, x, y, neighborRadius))
                    {
                        fringeTransparent[index] = true;
                    }
                }
            }

            for (int i = 0; i < fringeTransparent.Length; i++)
            {
                if (!fringeTransparent[i])
                {
                    continue;
                }

                changed = true;
                transparent[i] = true;
                output[i] = new Color(0f, 0f, 0f, 0f);
            }

            if (!changed)
            {
                break;
            }
        }

        RemoveSmallDetachedComponents(output, transparent, width, height, spec.KeepLargestOpaqueComponent);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (transparent[index])
                {
                    continue;
                }

                float fringe = ChromaFringeScore(output[index], chromaKey);
                if (fringe <= 0.025f || !HasTransparentNeighbor(transparent, width, height, x, y, 3))
                {
                    continue;
                }

                output[index] = SuppressChromaSpill(output[index], chromaKey, Mathf.Lerp(0.48f, 0.9f, fringe));
                output[index].a = 1f;
            }
        }

        RemoveResidualChromaEdgePixels(output, transparent, width, height, chromaKey);
        FeatherOpaqueEdges(output, transparent, width, height, chromaKey);
        DilateTransparentRgb(output, transparent, width, height);
        return output;
    }

    private static void RemoveSmallDetachedComponents(Color[] pixels, bool[] transparent, int width, int height, bool keepLargestComponent)
    {
        int minComponentPixels = Mathf.Clamp((width * height) / 1800, 18, 90);
        bool[] visited = new bool[pixels.Length];
        int[] queue = new int[pixels.Length];
        int[] componentIds = keepLargestComponent ? new int[pixels.Length] : null;
        List<int> componentSizes = keepLargestComponent ? new List<int>() : null;
        int largestComponentId = -1;
        int largestComponentPixels = 0;

        for (int start = 0; start < pixels.Length; start++)
        {
            if (transparent[start] || visited[start])
            {
                continue;
            }

            int head = 0;
            int tail = 0;
            visited[start] = true;
            queue[tail++] = start;

            while (head < tail)
            {
                int index = queue[head++];
                int x = index % width;
                int y = index / width;

                for (int yOffset = -1; yOffset <= 1; yOffset++)
                {
                    int neighborY = y + yOffset;
                    if (neighborY < 0 || neighborY >= height)
                    {
                        continue;
                    }

                    for (int xOffset = -1; xOffset <= 1; xOffset++)
                    {
                        if (xOffset == 0 && yOffset == 0)
                        {
                            continue;
                        }

                        int neighborX = x + xOffset;
                        if (neighborX < 0 || neighborX >= width)
                        {
                            continue;
                        }

                        int neighborIndex = neighborY * width + neighborX;
                        if (transparent[neighborIndex] || visited[neighborIndex])
                        {
                            continue;
                        }

                        visited[neighborIndex] = true;
                        queue[tail++] = neighborIndex;
                    }
                }
            }

            if (keepLargestComponent)
            {
                int componentId = componentSizes.Count;
                componentSizes.Add(tail);
                if (tail > largestComponentPixels)
                {
                    largestComponentPixels = tail;
                    largestComponentId = componentId;
                }

                for (int i = 0; i < tail; i++)
                {
                    componentIds[queue[i]] = componentId;
                }

                continue;
            }

            if (tail >= minComponentPixels)
            {
                continue;
            }

            for (int i = 0; i < tail; i++)
            {
                int index = queue[i];
                transparent[index] = true;
                pixels[index] = new Color(0f, 0f, 0f, 0f);
            }
        }

        if (!keepLargestComponent || largestComponentId < 0)
        {
            return;
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            if (transparent[i])
            {
                continue;
            }

            int componentId = componentIds[i];
            if (componentId == largestComponentId && componentSizes[componentId] >= minComponentPixels)
            {
                continue;
            }

            transparent[i] = true;
            pixels[i] = new Color(0f, 0f, 0f, 0f);
        }
    }

    private static void RemoveResidualChromaEdgePixels(Color[] pixels, bool[] transparent, int width, int height, ChromaKeyKind chromaKey)
    {
        if (chromaKey != ChromaKeyKind.Green)
        {
            return;
        }

        bool[] remove = new bool[pixels.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (transparent[index])
                {
                    continue;
                }

                float fringe = ChromaFringeScore(pixels[index], chromaKey);
                if (fringe <= 0.012f)
                {
                    continue;
                }

                int nearTransparent = CountTransparentNeighbors(transparent, width, height, x, y, 1);
                int wideTransparent = CountTransparentNeighbors(transparent, width, height, x, y, 2);
                if ((nearTransparent > 0 && fringe > 0.03f) || (wideTransparent >= 7 && fringe > 0.015f))
                {
                    remove[index] = true;
                }
            }
        }

        for (int i = 0; i < remove.Length; i++)
        {
            if (!remove[i])
            {
                continue;
            }

            transparent[i] = true;
            pixels[i] = new Color(0f, 0f, 0f, 0f);
        }
    }

    private static float ChromaFringeScore(Color color, ChromaKeyKind chromaKey)
    {
        return chromaKey == ChromaKeyKind.Green
            ? GreenFringeScore(color)
            : MagentaFringeScore(color);
    }

    private static float ChromaTransparentCutoff(ChromaKeyKind chromaKey, bool firstPass)
    {
        if (chromaKey == ChromaKeyKind.Green)
        {
            return firstPass ? 0.018f : 0.025f;
        }

        return firstPass ? 0.08f : 0.12f;
    }

    private static int ResolveChromaNeighborRadius(float fringe, ChromaKeyKind chromaKey)
    {
        if (chromaKey == ChromaKeyKind.Green)
        {
            return fringe > 0.07f ? 5 : 2;
        }

        return fringe > 0.25f ? 5 : 3;
    }

    private static float MagentaFringeScore(Color color)
    {
        float purpleFloor = Mathf.Min(color.r, color.b);
        float magentaBias = purpleFloor - color.g;
        float balance = 1f - Mathf.Clamp01(Mathf.Abs(color.r - color.b) * 2.6f);
        float greenSuppression = color.g < Mathf.Max(color.r, color.b) * 0.82f ? 1f : 0f;
        float level = Mathf.Clamp01((purpleFloor - 0.08f) * 4.2f);
        float bias = Mathf.Clamp01((magentaBias - 0.035f) * 5.5f);
        float brightFringe = level * bias * Mathf.Lerp(0.42f, 1f, balance) * greenSuppression;
        float darkLevel = Mathf.Clamp01((Mathf.Max(color.r, color.b) - 0.035f) * 11f);
        float darkBias = Mathf.Clamp01((magentaBias - 0.015f) * 10f);
        float darkBalance = Mathf.Lerp(0.55f, 1f, balance);
        float darkFringe = darkLevel * darkBias * darkBalance * greenSuppression;
        return Mathf.Clamp01(Mathf.Max(brightFringe, darkFringe));
    }

    private static float GreenFringeScore(Color color)
    {
        float sideFloor = Mathf.Max(color.r, color.b);
        float greenBias = color.g - sideFloor;
        float sideBalance = 1f - Mathf.Clamp01(Mathf.Abs(color.r - color.b) * 2.2f);
        float sideSuppression = color.g > sideFloor * 1.12f ? 1f : 0f;
        float level = Mathf.Clamp01((color.g - 0.08f) * 4.2f);
        float bias = Mathf.Clamp01((greenBias - 0.035f) * 5.5f);
        float brightFringe = level * bias * Mathf.Lerp(0.5f, 1f, sideBalance) * sideSuppression;
        float darkLevel = Mathf.Clamp01((color.g - 0.035f) * 11f);
        float darkBias = Mathf.Clamp01((greenBias - 0.015f) * 10f);
        float darkFringe = darkLevel * darkBias * Mathf.Lerp(0.6f, 1f, sideBalance) * sideSuppression;
        float luma = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        float oliveBias = color.g - Mathf.Min(color.r, color.b);
        float oliveDominance = color.g > color.r && color.g > color.b ? 1f : 0f;
        float darkOlive = oliveDominance
            * Mathf.Clamp01((oliveBias - 0.006f) * 12f)
            * Mathf.Clamp01((color.g - 0.015f) * 12f)
            * Mathf.Clamp01((0.55f - luma) * 2.25f);
        float yellowFloor = Mathf.Min(color.r, color.g);
        float yellowBias = yellowFloor - color.b;
        float yellowBalance = 1f - Mathf.Clamp01(Mathf.Abs(color.r - color.g) * 2.8f);
        float yellowGreenMatte = Mathf.Clamp01((yellowFloor - 0.1f) * 3.2f)
            * Mathf.Clamp01((yellowBias - 0.035f) * 4.5f)
            * Mathf.Lerp(0.32f, 1f, yellowBalance)
            * Mathf.Clamp01((0.78f - luma) * 1.7f);
        return Mathf.Clamp01(Mathf.Max(Mathf.Max(Mathf.Max(brightFringe, darkFringe), darkOlive), yellowGreenMatte));
    }

    private static Color SuppressChromaSpill(Color color, ChromaKeyKind chromaKey, float strength)
    {
        return chromaKey == ChromaKeyKind.Green
            ? SuppressGreenSpill(color, strength)
            : SuppressMagentaSpill(color, strength);
    }

    private static Color SuppressMagentaSpill(Color color, float strength)
    {
        float magentaBias = Mathf.Max(0f, Mathf.Min(color.r, color.b) - color.g);
        if (magentaBias <= 0.015f)
        {
            return color;
        }

        float correction = Mathf.Clamp01(strength) * Mathf.Clamp01((magentaBias - 0.015f) * 4.5f);
        float targetBlue = Mathf.Min(color.b, Mathf.Lerp(color.g, color.r, 0.28f));
        color.r = Mathf.Lerp(color.r, Mathf.Max(color.g, color.r - magentaBias * 0.7f), correction * 0.42f);
        color.b = Mathf.Lerp(color.b, targetBlue, correction);
        return color;
    }

    private static Color SuppressGreenSpill(Color color, float strength)
    {
        float sideFloor = Mathf.Max(color.r, color.b);
        float greenBias = Mathf.Max(0f, color.g - sideFloor);
        if (greenBias <= 0.015f)
        {
            return color;
        }

        float correction = Mathf.Clamp01(strength) * Mathf.Clamp01((greenBias - 0.015f) * 4.5f);
        float targetGreen = Mathf.Lerp(sideFloor, color.r * 0.58f + color.b * 0.42f, 0.4f);
        color.g = Mathf.Lerp(color.g, targetGreen, correction);
        return color;
    }

    private static bool HasTransparentNeighbor(bool[] transparent, int width, int height, int x, int y, int radius)
    {
        int xMin = Mathf.Max(0, x - radius);
        int xMax = Mathf.Min(width - 1, x + radius);
        int yMin = Mathf.Max(0, y - radius);
        int yMax = Mathf.Min(height - 1, y + radius);

        for (int yy = yMin; yy <= yMax; yy++)
        {
            int row = yy * width;
            for (int xx = xMin; xx <= xMax; xx++)
            {
                if (transparent[row + xx])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void FeatherOpaqueEdges(Color[] pixels, bool[] transparent, int width, int height, ChromaKeyKind chromaKey)
    {
        float[] alpha = new float[pixels.Length];
        for (int i = 0; i < alpha.Length; i++)
        {
            alpha[i] = transparent[i] ? 0f : pixels[i].a;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (transparent[index])
                {
                    continue;
                }

                int nearTransparent = CountTransparentNeighbors(transparent, width, height, x, y, 1);
                if (nearTransparent == 0)
                {
                    continue;
                }

                int wideTransparent = CountTransparentNeighbors(transparent, width, height, x, y, 2);
                float nearExposure = nearTransparent / 8f;
                float wideExposure = wideTransparent / 24f;
                float edgeExposure = Mathf.Max(nearExposure, wideExposure * 0.72f);
                float feather = Mathf.SmoothStep(0.12f, 0.74f, edgeExposure);
                float targetAlpha = Mathf.Lerp(1f, 0.38f, feather);

                float fringe = ChromaFringeScore(pixels[index], chromaKey);
                if (fringe > 0.02f)
                {
                    targetAlpha = Mathf.Min(targetAlpha, Mathf.Lerp(0.62f, 0.24f, Mathf.Clamp01(fringe)));
                }

                if (nearExposure >= 0.72f && wideExposure >= 0.5f)
                {
                    targetAlpha = Mathf.Min(targetAlpha, 0.22f);
                }

                alpha[index] = Mathf.Clamp(Mathf.Min(alpha[index], targetAlpha), 0.12f, 1f);
            }
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            if (transparent[i])
            {
                continue;
            }

            pixels[i].a = alpha[i];
        }
    }

    private static int CountTransparentNeighbors(bool[] transparent, int width, int height, int x, int y, int radius)
    {
        int count = 0;
        int xMin = x - radius;
        int xMax = x + radius;
        int yMin = y - radius;
        int yMax = y + radius;

        for (int yy = yMin; yy <= yMax; yy++)
        {
            for (int xx = xMin; xx <= xMax; xx++)
            {
                if (xx == x && yy == y)
                {
                    continue;
                }

                if (xx < 0 || yy < 0 || xx >= width || yy >= height)
                {
                    count++;
                    continue;
                }

                if (transparent[yy * width + xx])
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static void DilateTransparentRgb(Color[] pixels, bool[] transparent, int width, int height)
    {
        const int radius = 5;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (!transparent[index])
                {
                    continue;
                }

                Color nearest = default;
                int bestDistance = int.MaxValue;
                for (int searchRadius = 1; searchRadius <= radius && bestDistance == int.MaxValue; searchRadius++)
                {
                    int xMin = Mathf.Max(0, x - searchRadius);
                    int xMax = Mathf.Min(width - 1, x + searchRadius);
                    int yMin = Mathf.Max(0, y - searchRadius);
                    int yMax = Mathf.Min(height - 1, y + searchRadius);

                    for (int yy = yMin; yy <= yMax; yy++)
                    {
                        int row = yy * width;
                        for (int xx = xMin; xx <= xMax; xx++)
                        {
                            int sampleIndex = row + xx;
                            if (transparent[sampleIndex])
                            {
                                continue;
                            }

                            int distance = Mathf.Abs(xx - x) + Mathf.Abs(yy - y);
                            if (distance < bestDistance)
                            {
                                bestDistance = distance;
                                nearest = pixels[sampleIndex];
                            }
                        }
                    }
                }

                if (bestDistance != int.MaxValue)
                {
                    pixels[index] = new Color(nearest.r, nearest.g, nearest.b, 0f);
                }
            }
        }
    }

    private static Color[] BuildNormalPixels(Color[] albedo, int width, int height)
    {
        float[] heights = new float[albedo.Length];
        for (int i = 0; i < albedo.Length; i++)
        {
            Color color = albedo[i];
            float luma = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
            heights[i] = color.a * Mathf.Lerp(0.2f, 1f, luma);
        }

        Color[] normal = new Color[albedo.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (albedo[index].a <= 0.01f)
                {
                    normal[index] = new Color(0.5f, 0.5f, 1f, 0f);
                    continue;
                }

                float left = heights[y * width + Mathf.Max(0, x - 1)];
                float right = heights[y * width + Mathf.Min(width - 1, x + 1)];
                float down = heights[Mathf.Max(0, y - 1) * width + x];
                float up = heights[Mathf.Min(height - 1, y + 1) * width + x];
                Vector3 n = new Vector3((left - right) * 2.1f, (down - up) * 2.1f, 1f).normalized;
                normal[index] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, albedo[index].a);
            }
        }

        return normal;
    }

    private static Color[] BuildPackedMaskPixels(Color[] albedo, EntityImportSpec spec)
    {
        Color[] masks = new Color[albedo.Length];
        for (int i = 0; i < albedo.Length; i++)
        {
            Color color = albedo[i];
            if (color.a <= 0.01f)
            {
                masks[i] = Color.clear;
                continue;
            }

            float max = Mathf.Max(color.r, color.g, color.b);
            float min = Mathf.Min(color.r, color.g, color.b);
            float saturation = max <= 0.001f ? 0f : (max - min) / max;
            float luma = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
            float redTissue = Mathf.Clamp01((color.r - Mathf.Max(color.g, color.b) * 0.75f) * 2.4f);
            float acid = Mathf.Clamp01((color.g - color.b * 0.75f - color.r * 0.25f) * 2.1f);

            float wet = color.a * Mathf.Clamp01(saturation * 0.58f + luma * 0.22f) * spec.WetMaskStrength;
            float pulse = color.a * Mathf.Clamp01(Mathf.Max(redTissue, acid) + saturation * 0.12f) * spec.PulseMaskStrength;
            float grime = color.a * Mathf.Clamp01((1f - luma) * 0.8f + saturation * 0.1f) * spec.GrimeMaskStrength;
            float crawl = color.a * Mathf.Clamp01(0.22f + saturation * 0.62f) * spec.CrawlMaskStrength;
            masks[i] = new Color(wet, pulse, grime, crawl);
        }

        return masks;
    }

    private static Color[] BuildEmissionPixels(Color[] albedo, EntityImportSpec spec)
    {
        Color[] emission = new Color[albedo.Length];
        for (int i = 0; i < albedo.Length; i++)
        {
            Color color = albedo[i];
            if (color.a <= 0.01f)
            {
                emission[i] = Color.clear;
                continue;
            }

            float luma = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
            float greenGlow = Mathf.Clamp01((color.g - color.r * 0.35f - color.b * 0.35f) * 2.4f);
            float redGlow = Mathf.Clamp01((color.r - color.g * 0.55f - color.b * 0.55f) * 1.9f);
            float eyeGlow = Mathf.Clamp01((luma - 0.42f) * 2.2f) * Mathf.Clamp01((Mathf.Max(color.r, color.g) - color.b * 0.5f) * 1.7f);
            float glow = Mathf.Clamp01(Mathf.Max(greenGlow, Mathf.Max(redGlow * spec.RedEmissionStrength, eyeGlow)) * spec.EmissionMaskStrength);
            emission[i] = new Color(color.r * glow, color.g * glow, color.b * glow, color.a * glow);
        }

        return emission;
    }

    private static bool IsChroma(Color color, ChromaKeyKind chromaKey)
    {
        return chromaKey == ChromaKeyKind.Green
            ? IsGreenChroma(color)
            : IsMagentaChroma(color);
    }

    private static bool IsMagentaChroma(Color color)
    {
        return color.r > 0.52f
            && color.b > 0.52f
            && color.g < 0.48f
            && color.r - color.g > 0.18f
            && color.b - color.g > 0.16f
            && Mathf.Abs(color.r - color.b) < 0.42f;
    }

    private static bool IsGreenChroma(Color color)
    {
        return color.g > 0.5f
            && color.r < 0.5f
            && color.b < 0.5f
            && color.g - color.r > 0.16f
            && color.g - color.b > 0.16f;
    }

    private static void WriteTexture(string assetPath, int width, int height, Color[] pixels, TextureImporterType type, bool srgb, Vector2 pivot)
    {
        EnsureAssetFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false, !srgb);
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        File.WriteAllBytes(Path.GetFullPath(assetPath), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
        {
            return;
        }

        importer.textureType = type;
        importer.spriteImportMode = type == TextureImporterType.Sprite ? SpriteImportMode.Single : SpriteImportMode.None;
        importer.alphaIsTransparency = type == TextureImporterType.Sprite;
        importer.mipmapEnabled = false;
        importer.sRGBTexture = srgb;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.spritePixelsPerUnit = PixelsPerUnit;

        if (type == TextureImporterType.Sprite)
        {
            TextureImporterSettings settings = new();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
        }

        importer.SaveAndReimport();
    }

    private static TextureImportState PrepareReadableTexture(string assetPath)
    {
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
        {
            return default;
        }

        TextureImportState state = new()
        {
            AssetPath = assetPath,
            WasReadable = importer.isReadable,
            PreviousCompression = importer.textureCompression,
            PreviousMips = importer.mipmapEnabled
        };

        bool changed = false;
        if (!importer.isReadable)
        {
            importer.isReadable = true;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }

        state.Valid = true;
        return state;
    }

    private static void RestoreTextureState(TextureImportState state)
    {
        if (!state.Valid || AssetImporter.GetAtPath(state.AssetPath) is not TextureImporter importer)
        {
            return;
        }

        bool changed = false;
        if (importer.isReadable != state.WasReadable)
        {
            importer.isReadable = state.WasReadable;
            changed = true;
        }

        if (importer.textureCompression != state.PreviousCompression)
        {
            importer.textureCompression = state.PreviousCompression;
            changed = true;
        }

        if (importer.mipmapEnabled != state.PreviousMips)
        {
            importer.mipmapEnabled = state.PreviousMips;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static void ConfigureDefinition(DirectionalSpriteDefinition definition, EntityImportSpec spec)
    {
        if (definition == null)
        {
            return;
        }

        definition.defaultClipId = spec.DefaultClipId;
        if (definition.clips != null)
        {
            foreach (DirectionalSpriteClip clip in definition.clips)
            {
                ClipSpec clipSpec = spec.FindClip(clip.clipId);
                if (clipSpec == null)
                {
                    continue;
                }

                clip.loop = clipSpec.Loop;
                clip.framesPerSecond = clipSpec.FramesPerSecond;
            }
        }

        EditorUtility.SetDirty(definition);
    }

    private static void ConfigureGeneratedSpritePrefab(GameObject prefabAsset, DirectionalSpriteDefinition definition, EntityImportSpec spec)
    {
        if (prefabAsset == null)
        {
            return;
        }

        string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            ConfigureSharedComponents(root, definition, spec, contentPrefab: false);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject CreateOrUpdateTestPrefab(GameObject spritePrefabAsset, DirectionalSpriteDefinition definition, EntityImportSpec spec)
    {
        if (spritePrefabAsset == null)
        {
            return null;
        }

        string contentRoot = $"Assets/_Project/Content/Actors/{spec.Name}";
        string prefabRoot = $"{contentRoot}/Prefabs";
        EnsureAssetFolder(prefabRoot);

        string spritePrefabPath = AssetDatabase.GetAssetPath(spritePrefabAsset);
        GameObject root = PrefabUtility.LoadPrefabContents(spritePrefabPath);
        try
        {
            root.name = spec.Name;
            ConfigureSharedComponents(root, definition, spec, contentPrefab: true);
            ConfigurePhysicsAndDamage(root, spec);
            return PrefabUtility.SaveAsPrefabAsset(root, $"{prefabRoot}/{spec.Name}.prefab");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureSharedComponents(GameObject root, DirectionalSpriteDefinition definition, EntityImportSpec spec, bool contentPrefab)
    {
        Transform quad = root.transform.Find("Quad");
        MeshRenderer quadRenderer = quad != null ? quad.GetComponent<MeshRenderer>() : root.GetComponentInChildren<MeshRenderer>(true);
        if (quadRenderer != null)
        {
            quadRenderer.shadowCastingMode = ShadowCastingMode.On;
            quadRenderer.receiveShadows = true;
        }

        DirectionalSpriteAnimator animator = GetOrAdd<DirectionalSpriteAnimator>(root);
        SerializedObject serializedAnimator = new(animator);
        SetObject(serializedAnimator, "definition", definition);
        SetString(serializedAnimator, "initialClipId", spec.DefaultClipId);
        SetBool(serializedAnimator, "playOnEnable", true);
        SetFloat(serializedAnimator, "animationSpeed", 1f);
        SetBool(serializedAnimator, "freezeInitialClipInEditMode", contentPrefab);
        SetObject(serializedAnimator, "spriteRenderer", null);
        SetObject(serializedAnimator, "billboardRoot", quad);
        SetObject(serializedAnimator, "facingReference", animator.transform);
        SetObject(serializedAnimator, "targetCamera", null);
        SetEnum(serializedAnimator, "viewAngleSource", (int)DirectionalSpriteViewAngleSource.CameraPosition);
        SetEnum(serializedAnimator, "billboardMode", (int)DirectionalBillboardMode.YAxis);
        serializedAnimator.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(animator);

        DirectionalSpriteBillboardLitRenderer litRenderer = GetOrAdd<DirectionalSpriteBillboardLitRenderer>(root);
        SerializedObject serializedRenderer = new(litRenderer);
        SetObject(serializedRenderer, "animator", animator);
        SetObject(serializedRenderer, "targetRenderer", quadRenderer);
        SetObject(serializedRenderer, "lightAnchor", null);
        SetFloat(serializedRenderer, "alphaCutoff", spec.AlphaCutoff);
        SetFloat(serializedRenderer, "normalScale", spec.NormalScale);
        SetFloat(serializedRenderer, "detailNormalInfluence", spec.DetailNormalInfluence);
        SetFloat(serializedRenderer, "macroNormalBend", spec.MacroNormalBend);
        SetFloat(serializedRenderer, "spriteAngleLightingInfluence", spec.SpriteAngleLightingInfluence);
        SetFloat(serializedRenderer, "wrapDiffuse", spec.WrapDiffuse);
        SetFloat(serializedRenderer, "ambientIntensity", spec.AmbientIntensity);
        SetFloat(serializedRenderer, "renderSettingsAmbientScale", spec.RenderSettingsAmbientScale);
        SetFloat(serializedRenderer, "surfaceRoughness", spec.SurfaceRoughness);
        SetFloat(serializedRenderer, "specularStrength", spec.SpecularStrength);
        SetFloat(serializedRenderer, "minSpecularPower", spec.MinSpecularPower);
        SetFloat(serializedRenderer, "maxSpecularPower", spec.MaxSpecularPower);
        SetFloat(serializedRenderer, "rimStrength", spec.RimStrength);
        SetFloat(serializedRenderer, "rimPower", spec.RimPower);
        SetFloat(serializedRenderer, "emissionStrength", spec.EmissionStrength);
        SetFloat(serializedRenderer, "wetSpecularBoost", spec.WetSpecularBoost);
        SetFloat(serializedRenderer, "bloodPulseStrength", spec.BloodPulseStrength);
        SetFloat(serializedRenderer, "surfaceCrawlStrength", spec.SurfaceCrawlStrength);
        SetFloat(serializedRenderer, "surfaceCrawlSpeed", spec.SurfaceCrawlSpeed);
        SetColor(serializedRenderer, "rimColor", spec.RimColor);
        SetColor(serializedRenderer, "emissionColor", spec.EmissionColor);
        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(litRenderer);

        DirectionalSpriteLocomotion locomotion = root.GetComponent<DirectionalSpriteLocomotion>();
        if (spec.AddLocomotion)
        {
            locomotion = GetOrAdd<DirectionalSpriteLocomotion>(root);
            SerializedObject serializedLocomotion = new(locomotion);
            SetObject(serializedLocomotion, "animator", animator);
            SetObject(serializedLocomotion, "movementReference", root.transform);
            SetString(serializedLocomotion, "idleClipId", spec.DefaultClipId);
            SetString(serializedLocomotion, "walkClipId", spec.MovementClipId);
            SetBool(serializedLocomotion, "horizontalOnly", true);
            SetFloat(serializedLocomotion, "walkThreshold", 0.04f);
            SetFloat(serializedLocomotion, "speedSmoothing", 12f);
            serializedLocomotion.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(locomotion);
        }
        else if (locomotion != null)
        {
            UnityEngine.Object.DestroyImmediate(locomotion, true);
        }

        DirectionalSpriteHitMask hitMask = GetOrAdd<DirectionalSpriteHitMask>(root);
        SerializedObject serializedHitMask = new(hitMask);
        SetObject(serializedHitMask, "animator", animator);
        SetObject(serializedHitMask, "visualRenderer", quadRenderer);
        SetObject(serializedHitMask, "hitPlane", quad);
        SetBool(serializedHitMask, "useAlphaMask", true);
        SetBool(serializedHitMask, "useSpritePhysicsShapeFallback", true);
        SetBool(serializedHitMask, "acceptWhenMaskUnavailable", true);
        SetBool(serializedHitMask, "rejectHitsOutsideQuad", true);
        SetFloat(serializedHitMask, "alphaThreshold", spec.HitAlphaThreshold);
        SetInt(serializedHitMask, "edgePaddingPixels", 3);
        serializedHitMask.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hitMask);
    }

    private static void ConfigurePhysicsAndDamage(GameObject root, EntityImportSpec spec)
    {
        if (spec.UseCapsuleCollider)
        {
            BoxCollider oldBox = root.GetComponent<BoxCollider>();
            if (oldBox != null)
            {
                UnityEngine.Object.DestroyImmediate(oldBox, true);
            }

            CapsuleCollider collider = GetOrAdd<CapsuleCollider>(root);
            collider.center = spec.ColliderCenter;
            collider.radius = spec.CapsuleRadius;
            collider.height = spec.CapsuleHeight;
            collider.direction = 1;
            collider.isTrigger = false;
        }
        else
        {
            CapsuleCollider oldCapsule = root.GetComponent<CapsuleCollider>();
            if (oldCapsule != null)
            {
                UnityEngine.Object.DestroyImmediate(oldCapsule, true);
            }

            BoxCollider collider = GetOrAdd<BoxCollider>(root);
            collider.center = spec.ColliderCenter;
            collider.size = spec.BoxSize;
            collider.isTrigger = false;
        }

        Rigidbody body = GetOrAdd<Rigidbody>(root);
        body.useGravity = false;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        RetroDamageable damageable = GetOrAdd<RetroDamageable>(root);
        SerializedObject serializedDamageable = new(damageable);
        SetFloat(serializedDamageable, "maxHealth", spec.MaxHealth);
        SetBool(serializedDamageable, "destroyOnDeath", true);
        SetBool(serializedDamageable, "disableRenderersOnDeath", true);
        SetBool(serializedDamageable, "disableCollidersOnDeath", true);
        SetFloat(serializedDamageable, "destroyDelay", 0f);
        SetFloat(serializedDamageable, "shootableFeedbackScale", spec.ShootableFeedbackScale);
        SetFloat(serializedDamageable, "shootableDeathEffectMultiplier", spec.ShootableDeathEffectMultiplier);
        serializedDamageable.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(damageable);

        RetroGibOnDeath gib = GetOrAdd<RetroGibOnDeath>(root);
        SerializedObject serializedGib = new(gib);
        SetObject(serializedGib, "damageable", damageable);
        SetObject(serializedGib, "goreProfile", AssetDatabase.LoadAssetAtPath<RetroGoreProfile>(GoreProfilePath));
        SetBool(serializedGib, "alwaysGibOnDeath", spec.AlwaysGibOnDeath);
        SetFloat(serializedGib, "intensityMultiplier", spec.GibIntensityMultiplier);
        serializedGib.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gib);
    }

    private static void ResetGeneratedFrames(string framesRoot)
    {
        if (AssetDatabase.IsValidFolder(framesRoot))
        {
            AssetDatabase.DeleteAsset(framesRoot);
        }

        EnsureAssetFolder(framesRoot);
    }

    private static void EnsureAssetFolder(string assetFolderPath)
    {
        string normalizedPath = assetFolderPath?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalizedPath) || AssetDatabase.IsValidFolder(normalizedPath))
        {
            return;
        }

        string parentPath = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(normalizedPath);
        if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(folderName))
        {
            throw new InvalidOperationException($"Invalid asset folder path '{assetFolderPath}'.");
        }

        EnsureAssetFolder(parentPath);
        AssetDatabase.CreateFolder(parentPath, folderName);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        return existing != null ? existing : target.AddComponent<T>();
    }

    private static void SetFloat(SerializedObject target, string propertyName, float value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
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

    private static void SetBool(SerializedObject target, string propertyName, bool value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetString(SerializedObject target, string propertyName, string value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
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

    private static EntityImportSpec[] CreateSpecs()
    {
        return new[]
        {
            new EntityImportSpec("Rat", 0.48f, "Scurry", false)
            {
                SheetColumns = 6,
                SheetRows = 3,
                SheetAngleLabels = new[] { "Front", "Right", "Back" },
                ChromaKey = ChromaKeyKind.Green,
                KeepLargestOpaqueComponent = true,
                UseComponentAlignedCells = true,
                Clips = new[]
                {
                    new ClipSpec("Idle", true, 7f),
                    new ClipSpec("Scurry", true, 13f),
                    new ClipSpec("Pounce", false, 12f),
                    new ClipSpec("Bite", false, 13f),
                    new ClipSpec("Sniff", true, 6f)
                },
                SpritePivot = new Vector2(0.5f, 0.11f),
                BoxSize = new Vector3(0.95f, 0.56f, 0.75f),
                ColliderCenter = new Vector3(0f, 0.28f, 0f),
                MaxHealth = 28f,
                WetSpecularBoost = 0.55f,
                BloodPulseStrength = 0.14f,
                SurfaceCrawlStrength = 0.0025f,
                EmissionStrength = 0.65f,
                RimStrength = 0.07f,
                ShootableFeedbackScale = 0.55f,
                ShootableDeathEffectMultiplier = 1.35f,
                GibIntensityMultiplier = 0.45f
            },
            new EntityImportSpec("Mawtick", 0.68f, "Skitter", false)
            {
                SheetColumns = 6,
                SheetRows = 3,
                SheetAngleLabels = new[] { "Front", "Right", "Back" },
                ChromaKey = ChromaKeyKind.Green,
                KeepLargestOpaqueComponent = true,
                UseComponentAlignedCells = true,
                Clips = new[]
                {
                    new ClipSpec("Idle", true, 6.5f),
                    new ClipSpec("Skitter", true, 12.5f),
                    new ClipSpec("Leap", false, 13f),
                    new ClipSpec("Gnash", false, 13.5f),
                    new ClipSpec("Screech", false, 8f)
                },
                SpritePivot = new Vector2(0.5f, 0.12f),
                BoxSize = new Vector3(1.28f, 0.82f, 0.86f),
                ColliderCenter = new Vector3(0f, 0.41f, 0f),
                MaxHealth = 64f,
                AlphaCutoff = 0.07f,
                NormalScale = 1.03f,
                DetailNormalInfluence = 0.62f,
                MacroNormalBend = 0.58f,
                WrapDiffuse = 0.44f,
                SurfaceRoughness = 0.78f,
                SpecularStrength = 0.1f,
                MinSpecularPower = 6f,
                MaxSpecularPower = 18f,
                WetSpecularBoost = 0.86f,
                BloodPulseStrength = 0.09f,
                SurfaceCrawlStrength = 0.004f,
                SurfaceCrawlSpeed = 1.65f,
                EmissionStrength = 0.82f,
                EmissionMaskStrength = 1.12f,
                RedEmissionStrength = 0.24f,
                RimStrength = 0.028f,
                RimPower = 3.4f,
                RimColor = new Color(0.82f, 0.94f, 1f, 1f),
                EmissionColor = new Color(1f, 0.34f, 0.08f, 1f),
                ShootableFeedbackScale = 0.72f,
                ShootableDeathEffectMultiplier = 1.65f,
                GibIntensityMultiplier = 0.65f
            },
            new EntityImportSpec("Dog", 0.82f, "Trot", true)
            {
                SheetColumns = 6,
                SheetRows = 3,
                SheetAngleLabels = new[] { "Front", "Right", "Back" },
                ChromaKey = ChromaKeyKind.Green,
                KeepLargestOpaqueComponent = true,
                UseComponentAlignedCells = true,
                Clips = new[]
                {
                    new ClipSpec("Idle", true, 6f),
                    new ClipSpec("Trot", true, 10f),
                    new ClipSpec("Sprint", true, 14f),
                    new ClipSpec("Bite", false, 13f),
                    new ClipSpec("Howl", false, 7f)
                },
                SpritePivot = new Vector2(0.5f, 0.12f),
                CapsuleRadius = 0.45f,
                CapsuleHeight = 1.55f,
                ColliderCenter = new Vector3(0f, 0.78f, 0f),
                MaxHealth = 90f,
                WetSpecularBoost = 0.72f,
                BloodPulseStrength = 0.06f,
                SurfaceCrawlStrength = 0.0025f,
                EmissionStrength = 0.5f,
                RimStrength = 0.025f,
                RedEmissionStrength = 0.2f,
                ShootableFeedbackScale = 0.85f,
                ShootableDeathEffectMultiplier = 1.8f,
                GibIntensityMultiplier = 0.7f
            },
            new EntityImportSpec("Cat", 0.58f, "Stalk", false)
            {
                SheetColumns = 6,
                SheetRows = 3,
                SheetAngleLabels = new[] { "Front", "Right", "Back" },
                ChromaKey = ChromaKeyKind.Green,
                KeepLargestOpaqueComponent = true,
                Clips = new[]
                {
                    new ClipSpec("Idle", true, 7f),
                    new ClipSpec("Stalk", true, 10f),
                    new ClipSpec("Leap", false, 13f),
                    new ClipSpec("Scratch", false, 14f),
                    new ClipSpec("Hiss", false, 7f)
                },
                SpritePivot = new Vector2(0.5f, 0.13f),
                BoxSize = new Vector3(0.88f, 0.7f, 0.62f),
                ColliderCenter = new Vector3(0f, 0.35f, 0f),
                MaxHealth = 42f,
                WetSpecularBoost = 0.52f,
                BloodPulseStrength = 0.035f,
                SurfaceCrawlStrength = 0.002f,
                EmissionStrength = 0.45f,
                RimStrength = 0.025f,
                RedEmissionStrength = 0.12f,
                ShootableFeedbackScale = 0.62f,
                ShootableDeathEffectMultiplier = 1.45f,
                GibIntensityMultiplier = 0.5f
            },
            new EntityImportSpec("ButcherBoss", 4.35f, "Walk", true)
            {
                SheetColumns = 4,
                Clips = new[]
                {
                    new ClipSpec("Idle", true, 5.5f),
                    new ClipSpec("Walk", true, 8.5f),
                    new ClipSpec("Cleaver", false, 12f),
                    new ClipSpec("Slam", false, 10.5f),
                    new ClipSpec("Roar", false, 8f)
                },
                SpriteAssetName = "ButcherBossSprite",
                SpritePivot = new Vector2(0.5f, 0.06f),
                CapsuleRadius = 1.45f,
                CapsuleHeight = 9.25f,
                ColliderCenter = new Vector3(0f, 4.65f, 0f),
                MaxHealth = 900f,
                AlphaCutoff = 0.06f,
                NormalScale = 1.05f,
                DetailNormalInfluence = 0.68f,
                MacroNormalBend = 0.72f,
                WrapDiffuse = 0.5f,
                SurfaceRoughness = 0.82f,
                SpecularStrength = 0.12f,
                MinSpecularPower = 7f,
                MaxSpecularPower = 20f,
                RimStrength = 0.06f,
                RimPower = 3.1f,
                EmissionStrength = 0.7f,
                WetSpecularBoost = 0.95f,
                BloodPulseStrength = 0.18f,
                SurfaceCrawlStrength = 0.0045f,
                SurfaceCrawlSpeed = 1.35f,
                RimColor = new Color(1f, 0.82f, 0.68f, 1f),
                EmissionColor = new Color(1f, 0.22f, 0.06f, 1f),
                RedEmissionStrength = 0.28f,
                ShootableFeedbackScale = 2.2f,
                ShootableDeathEffectMultiplier = 3.1f,
                GibIntensityMultiplier = 1.8f,
                AlwaysGibOnDeath = true
            },
            new EntityImportSpec("AbominationMonster", 3.2f, "Scuttle", false)
            {
                SheetColumns = 4,
                Clips = new[]
                {
                    new ClipSpec("Idle", true, 7f),
                    new ClipSpec("Scuttle", true, 13f),
                    new ClipSpec("Lunge", false, 12f),
                    new ClipSpec("Spit", false, 9f),
                    new ClipSpec("Roar", false, 8f)
                },
                SpriteAssetName = "AbominationMonsterSprite",
                SpritePivot = new Vector2(0.5f, 0.09f),
                BoxSize = new Vector3(5.85f, 3.25f, 2.6f),
                ColliderCenter = new Vector3(0f, 1.62f, 0f),
                MaxHealth = 640f,
                AlphaCutoff = 0.055f,
                NormalScale = 1.18f,
                DetailNormalInfluence = 0.74f,
                MacroNormalBend = 0.64f,
                WrapDiffuse = 0.58f,
                SurfaceRoughness = 0.76f,
                SpecularStrength = 0.16f,
                MinSpecularPower = 5f,
                MaxSpecularPower = 18f,
                RimStrength = 0.07f,
                RimPower = 2.8f,
                EmissionStrength = 1.25f,
                WetSpecularBoost = 1.25f,
                BloodPulseStrength = 0.14f,
                SurfaceCrawlStrength = 0.0075f,
                SurfaceCrawlSpeed = 2.25f,
                RimColor = new Color(0.66f, 1f, 0.22f, 1f),
                EmissionColor = new Color(0.62f, 1f, 0.05f, 1f),
                RedEmissionStrength = 0.18f,
                EmissionMaskStrength = 1.25f,
                ShootableFeedbackScale = 1.8f,
                ShootableDeathEffectMultiplier = 2.75f,
                GibIntensityMultiplier = 1.45f,
                AlwaysGibOnDeath = true
            }
        };
    }

    private readonly struct AngleSpec
    {
        public AngleSpec(string label, float yawDegrees)
        {
            Label = label;
            YawDegrees = yawDegrees;
        }

        public readonly string Label;
        public readonly float YawDegrees;
    }

    private enum ChromaKeyKind
    {
        Magenta,
        Green
    }

    private readonly struct ComponentBounds
    {
        public ComponentBounds(RectInt rect, int pixels)
        {
            Rect = rect;
            Pixels = pixels;
        }

        public RectInt Rect { get; }
        public int Pixels { get; }
        public float CenterX => Rect.x + Rect.width * 0.5f;
    }

    private sealed class ClipSpec
    {
        public ClipSpec(string id, bool loop, float framesPerSecond)
        {
            Id = id;
            Loop = loop;
            FramesPerSecond = framesPerSecond;
        }

        public string Id { get; }
        public bool Loop { get; }
        public float FramesPerSecond { get; }
    }

    private sealed class EntityImportSpec
    {
        public EntityImportSpec(string name, float worldScaleMultiplier, string movementClipId, bool useCapsuleCollider)
        {
            Name = name;
            SourcePrefix = name;
            SpriteAssetName = name + "Sprite";
            WorldScaleMultiplier = worldScaleMultiplier;
            MovementClipId = movementClipId;
            UseCapsuleCollider = useCapsuleCollider;
        }

        public string Name;
        public string SourcePrefix;
        public string SpriteAssetName;
        public string DefaultClipId = "Idle";
        public string MovementClipId;
        public string ArtRoot => $"Assets/_Project/Art/Sprites/Entities/{Name}";
        public ClipSpec[] Clips;
        public int SheetColumns = DefaultColumns;
        public int SheetRows = DefaultRows;
        public string[] SheetAngleLabels;
        public ChromaKeyKind ChromaKey = ChromaKeyKind.Magenta;
        public bool KeepLargestOpaqueComponent;
        public bool UseComponentAlignedCells;
        public bool AddLocomotion = true;
        public bool UseCapsuleCollider;
        public float WorldScaleMultiplier;
        public Vector2 SpritePivot = new(0.5f, 0.1f);
        public Vector3 ColliderCenter = new(0f, 0.5f, 0f);
        public Vector3 BoxSize = Vector3.one;
        public float CapsuleRadius = 0.5f;
        public float CapsuleHeight = 1f;
        public float MaxHealth = 100f;
        public float AlphaCutoff = 0.08f;
        public float NormalScale = 0.92f;
        public float DetailNormalInfluence = 0.52f;
        public float MacroNormalBend = 0.58f;
        public float SpriteAngleLightingInfluence = 0.28f;
        public float WrapDiffuse = 0.38f;
        public float AmbientIntensity = 1.02f;
        public float RenderSettingsAmbientScale = 0.22f;
        public float SurfaceRoughness = 0.9f;
        public float SpecularStrength = 0.05f;
        public float MinSpecularPower = 7f;
        public float MaxSpecularPower = 16f;
        public float RimStrength = 0.06f;
        public float RimPower = 3.6f;
        public float EmissionStrength = 0.75f;
        public float WetSpecularBoost = 0.6f;
        public float BloodPulseStrength = 0.18f;
        public float SurfaceCrawlStrength = 0.002f;
        public float SurfaceCrawlSpeed = 1.2f;
        public Color RimColor = Color.white;
        public Color EmissionColor = new(1f, 0.35f, 0.08f, 1f);
        public float WetMaskStrength = 1f;
        public float PulseMaskStrength = 1f;
        public float GrimeMaskStrength = 1f;
        public float CrawlMaskStrength = 1f;
        public float EmissionMaskStrength = 1f;
        public float RedEmissionStrength = 0.7f;
        public float HitAlphaThreshold = 0.08f;
        public float ShootableFeedbackScale = 1f;
        public float ShootableDeathEffectMultiplier = 2f;
        public bool AlwaysGibOnDeath;
        public float GibIntensityMultiplier = 1f;

        public ClipSpec FindClip(string clipId)
        {
            if (Clips == null)
            {
                return null;
            }

            for (int i = 0; i < Clips.Length; i++)
            {
                if (string.Equals(Clips[i].Id, clipId, StringComparison.OrdinalIgnoreCase))
                {
                    return Clips[i];
                }
            }

            return null;
        }

        public AngleSpec[] GetSheetAngles()
        {
            if (SheetRows <= 0)
            {
                throw new InvalidOperationException($"{Name} must declare at least one sheet row.");
            }

            if (SheetAngleLabels == null || SheetAngleLabels.Length == 0)
            {
                if (SheetRows > Angles.Length)
                {
                    throw new InvalidOperationException($"{Name} requests {SheetRows} sheet rows, but only {Angles.Length} default angles are configured.");
                }

                AngleSpec[] defaults = new AngleSpec[SheetRows];
                Array.Copy(Angles, defaults, SheetRows);
                return defaults;
            }

            if (SheetAngleLabels.Length != SheetRows)
            {
                throw new InvalidOperationException($"{Name} declares {SheetRows} sheet rows but {SheetAngleLabels.Length} sheet angle labels.");
            }

            AngleSpec[] resolved = new AngleSpec[SheetRows];
            for (int i = 0; i < SheetAngleLabels.Length; i++)
            {
                resolved[i] = ResolveSheetAngle(SheetAngleLabels[i]);
            }

            return resolved;
        }

        private static AngleSpec ResolveSheetAngle(string label)
        {
            for (int i = 0; i < Angles.Length; i++)
            {
                if (string.Equals(Angles[i].Label, label, StringComparison.OrdinalIgnoreCase))
                {
                    return Angles[i];
                }
            }

            throw new InvalidOperationException($"Unsupported sheet angle label '{label}'.");
        }
    }

    private struct TextureImportState
    {
        public bool Valid;
        public string AssetPath;
        public bool WasReadable;
        public TextureImporterCompression PreviousCompression;
        public bool PreviousMips;
    }
}
