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
    private const int Rows = 5;
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
        bool catDogBosses = bossesOnly
            && (request.IndexOf("cat", StringComparison.OrdinalIgnoreCase) >= 0
                || request.IndexOf("dog", StringComparison.OrdinalIgnoreCase) >= 0);

        Debug.Log(catDogBosses
            ? "Retro Imagen entity import request detected. Importing Cat, Dog, ButcherBoss, and AbominationMonster."
            : bossesOnly
            ? "Retro Imagen entity import request detected. Importing ButcherBoss and AbominationMonster."
            : "Retro Imagen entity import request detected. Importing Rat, Dog, Cat, ButcherBoss, and AbominationMonster.");
        File.Delete(requestPath);
        if (catDogBosses)
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
        ImportSpecs(selectAsset, null, "Rat, Dog, Cat, ButcherBoss, and AbominationMonster");
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
            for (int row = 0; row < Rows; row++)
            {
                AngleSpec angle = Angles[row];
                for (int column = 0; column < spec.SheetColumns; column++)
                {
                    RectInt rect = ResolveCell(sheet.width, sheet.height, spec.SheetColumns, column, row);
                    Color[] albedoPixels = BuildAlbedoPixels(sheet.GetPixels(rect.x, rect.y, rect.width, rect.height), rect.width, rect.height);
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

    private static RectInt ResolveCell(int width, int height, int columns, int column, int row)
    {
        int xMin = Mathf.RoundToInt(column * width / (float)columns);
        int xMax = Mathf.RoundToInt((column + 1) * width / (float)columns);
        int topMin = Mathf.RoundToInt(row * height / (float)Rows);
        int topMax = Mathf.RoundToInt((row + 1) * height / (float)Rows);
        int cellHeight = Mathf.Max(1, topMax - topMin);
        int y = height - topMax;
        return new RectInt(xMin, y, Mathf.Max(1, xMax - xMin), cellHeight);
    }

    private static Color[] BuildAlbedoPixels(Color[] source, int width, int height)
    {
        Color[] output = new Color[source.Length];
        bool[] transparent = new bool[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            Color color = source[i];
            if (IsChroma(color))
            {
                output[i] = new Color(0f, 0f, 0f, 0f);
                transparent[i] = true;
                continue;
            }

            color = SuppressMagentaSpill(color, 0.45f);
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

                float fringe = ChromaFringeScore(output[index]);
                if (fringe < 0.08f)
                {
                    continue;
                }

                if (HasTransparentNeighbor(transparent, width, height, x, y, fringe > 0.25f ? 5 : 3))
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

        for (int pass = 0; pass < 5; pass++)
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

                    float fringe = ChromaFringeScore(output[index]);
                    if (fringe < 0.12f)
                    {
                        continue;
                    }

                    if (HasTransparentNeighbor(transparent, width, height, x, y, fringe > 0.25f ? 5 : 3))
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

        RemoveSmallDetachedComponents(output, transparent, width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (transparent[index])
                {
                    continue;
                }

                float fringe = ChromaFringeScore(output[index]);
                if (fringe <= 0.025f || !HasTransparentNeighbor(transparent, width, height, x, y, 3))
                {
                    continue;
                }

                output[index] = SuppressMagentaSpill(output[index], Mathf.Lerp(0.48f, 0.9f, fringe));
                output[index].a = 1f;
            }
        }

        FeatherOpaqueEdges(output, transparent, width, height);
        DilateTransparentRgb(output, transparent, width, height);
        return output;
    }

    private static void RemoveSmallDetachedComponents(Color[] pixels, bool[] transparent, int width, int height)
    {
        int minComponentPixels = Mathf.Clamp((width * height) / 1800, 18, 90);
        bool[] visited = new bool[pixels.Length];
        int[] queue = new int[pixels.Length];

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
    }

    private static float ChromaFringeScore(Color color)
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

    private static void FeatherOpaqueEdges(Color[] pixels, bool[] transparent, int width, int height)
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

                float fringe = ChromaFringeScore(pixels[index]);
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

    private static bool IsChroma(Color color)
    {
        return color.r > 0.52f
            && color.b > 0.52f
            && color.g < 0.48f
            && color.r - color.g > 0.18f
            && color.b - color.g > 0.16f
            && Mathf.Abs(color.r - color.b) < 0.42f;
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
            new EntityImportSpec("Dog", 0.82f, "Trot", true)
            {
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
