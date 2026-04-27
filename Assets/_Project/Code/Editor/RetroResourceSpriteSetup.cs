using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RetroResourceSpriteSetup
{
    private static readonly string[] ResourceNames =
    {
        "Apple",
        "Mushroom",
        "SpecialWeed",
        "Wood",
        "Stone"
    };

    private static readonly string[] WorldSpritePaths =
    {
        "Assets/_Project/Art/Sprites/Resources/World/World_Apple.png",
        "Assets/_Project/Art/Sprites/Resources/World/World_Mushroom.png",
        "Assets/_Project/Art/Sprites/Resources/World/World_SpecialWeedPlant.png",
        "Assets/_Project/Art/Sprites/Resources/World/World_WoodStump.png",
        "Assets/_Project/Art/Sprites/Resources/World/World_StoneCluster.png"
    };

    private static readonly string[] IconSpritePaths =
    {
        "Assets/_Project/Art/Sprites/Resources/Icons/Icon_Apple.png",
        "Assets/_Project/Art/Sprites/Resources/Icons/Icon_Mushroom.png",
        "Assets/_Project/Art/Sprites/Resources/Icons/Icon_SpecialWeed.png",
        "Assets/_Project/Art/Sprites/Resources/Icons/Icon_Wood.png",
        "Assets/_Project/Art/Sprites/Resources/Icons/Icon_Stone.png"
    };

    private static readonly string[] EmissionMapPaths =
    {
        "Assets/_Project/Art/Sprites/Resources/Maps/Emission_Apple.png",
        "Assets/_Project/Art/Sprites/Resources/Maps/Emission_Mushroom.png",
        "Assets/_Project/Art/Sprites/Resources/Maps/Emission_SpecialWeed.png",
        "Assets/_Project/Art/Sprites/Resources/Maps/Emission_Wood.png",
        "Assets/_Project/Art/Sprites/Resources/Maps/Emission_Stone.png"
    };

    private static readonly string[] PrefabPaths =
    {
        "Assets/_Project/Content/Gameplay/Resources/Prefabs/ApplePickup.prefab",
        "Assets/_Project/Content/Gameplay/Resources/Prefabs/MushroomPickup.prefab",
        "Assets/_Project/Content/Gameplay/Resources/Prefabs/SpecialWeedPlant.prefab",
        "Assets/_Project/Content/Gameplay/Resources/Prefabs/WoodStumpGatherable.prefab",
        "Assets/_Project/Content/Gameplay/Resources/Prefabs/StoneClusterGatherable.prefab"
    };

    private static readonly Color[] AccentColors =
    {
        new(0.68f, 0.62f, 0.52f, 1f),
        new(0.66f, 0.58f, 0.50f, 1f),
        new(0.38f, 0.56f, 0.45f, 1f),
        new(0.62f, 0.52f, 0.42f, 1f),
        new(0.42f, 0.48f, 0.52f, 1f)
    };

    private static readonly float[] EmissionStrength =
    {
        0.00f,
        0.00f,
        0.10f,
        0.00f,
        0.05f
    };

    [MenuItem("Ultraloud/Resources/Rebuild Resource Sprite Materials")]
    public static void Run()
    {
        EnsureFolder("Assets/_Project/Art", "Materials");
        EnsureFolder("Assets/_Project/Art/Materials", "Resources");
        EnsureFolder("Assets/_Project/Art/Sprites/Resources", "Maps");

        for (int i = 0; i < ResourceNames.Length; i++)
        {
            ConfigureSpriteImporter(WorldSpritePaths[i], 256, 256f);
            ConfigureSpriteImporter(IconSpritePaths[i], 128, 128f);
            ConfigureMapImporter(EmissionMapPaths[i]);
        }

        AssetDatabase.ImportAsset("Assets/_Project/Art/Shaders/RetroResourceSpriteGlowHDRP.shader", ImportAssetOptions.ForceUpdate);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Art/Shaders/RetroResourceSpriteGlowHDRP.shader");
        if (shader == null)
        {
            throw new FileNotFoundException("Missing resource sprite glow shader.");
        }

        for (int i = 0; i < ResourceNames.Length; i++)
        {
            ImportResourceAssets(i);
            Material material = CreateOrUpdateMaterial(i, shader);
            AssignPrefabMaterial(PrefabPaths[i], material);
        }

        AssignSceneMaterials();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Rebuilt resource sprite materials and assigned resource prefabs.");
    }

    public static void Verify()
    {
        int invalid = 0;

        for (int i = 0; i < ResourceNames.Length; i++)
        {
            TextureImporter worldImporter = AssetImporter.GetAtPath(WorldSpritePaths[i]) as TextureImporter;
            TextureImporter iconImporter = AssetImporter.GetAtPath(IconSpritePaths[i]) as TextureImporter;
            TextureImporter emissionImporter = AssetImporter.GetAtPath(EmissionMapPaths[i]) as TextureImporter;
            Sprite worldSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WorldSpritePaths[i]);
            Material material = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/Art/Materials/Resources/{ResourceNames[i]}ResourceSprite.mat");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPaths[i]);
            SpriteRenderer prefabRenderer = prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>(true) : null;

            bool importerValid = worldImporter != null
                && Mathf.Approximately(worldImporter.spritePixelsPerUnit, 256f)
                && iconImporter != null
                && Mathf.Approximately(iconImporter.spritePixelsPerUnit, 128f)
                && emissionImporter != null
                && emissionImporter.textureCompression == TextureImporterCompression.CompressedHQ;
            bool spriteValid = worldSprite != null
                && worldSprite.bounds.size.x <= 1.05f
                && worldSprite.bounds.size.y <= 1.05f;
            bool materialValid = material != null
                && material.shader != null
                && material.shader.name == "Ultraloud/Resources/Sprite Glow HDRP";
            bool prefabValid = prefabRenderer != null
                && prefabRenderer.sprite == worldSprite
                && prefabRenderer.sharedMaterial == material;

            if (!importerValid || !spriteValid || !materialValid || !prefabValid)
            {
                invalid++;
                Debug.LogError($"Invalid resource sprite setup for {ResourceNames[i]}: importer={importerValid}, sprite={spriteValid}, material={materialValid}, prefab={prefabValid}");
            }
        }

        Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity", OpenSceneMode.Single);
        GameObject root = GameObject.Find("Resource Pickups");
        int childCount = root != null ? root.transform.childCount : 0;
        int sceneInvalid = 0;
        if (root == null || childCount != 25)
        {
            sceneInvalid++;
        }
        else
        {
            for (int i = 0; i < root.transform.childCount; i++)
            {
                Transform child = root.transform.GetChild(i);
                SpriteRenderer renderer = child.GetComponentInChildren<SpriteRenderer>(true);
                bool isArtifactPickup = child.name.StartsWith("MonaLisaArtifact", System.StringComparison.Ordinal)
                    || child.name.StartsWith("ExcaliburArtifact", System.StringComparison.Ordinal);
                string expectedShaderName = isArtifactPickup
                    ? "Ultraloud/Pickups/Artifact Pickup HDRP"
                    : "Ultraloud/Resources/Sprite Glow HDRP";
                if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null || renderer.sharedMaterial.shader.name != expectedShaderName)
                {
                    sceneInvalid++;
                }
            }
        }

        invalid += sceneInvalid;
        Debug.Log($"Verified resource sprite setup: resources={ResourceNames.Length}, sceneChildren={childCount}, invalid={invalid}, scene={scene.path}");

        if (invalid > 0)
        {
            throw new System.InvalidOperationException($"Resource sprite setup verification failed with {invalid} invalid item(s).");
        }
    }

    private static void ImportResourceAssets(int index)
    {
        AssetDatabase.ImportAsset(WorldSpritePaths[index], ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(IconSpritePaths[index], ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(EmissionMapPaths[index], ImportAssetOptions.ForceUpdate);
    }

    private static Material CreateOrUpdateMaterial(int index, Shader shader)
    {
        Texture2D worldTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(WorldSpritePaths[index]);
        Texture2D emissionTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionMapPaths[index]);
        if (worldTexture == null || emissionTexture == null)
        {
            throw new FileNotFoundException($"Missing imported resource textures for {ResourceNames[index]}.");
        }

        string materialPath = $"Assets/_Project/Art/Materials/Resources/{ResourceNames[index]}ResourceSprite.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.shader = shader;
        material.SetTexture("_MainTex", worldTexture);
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_EmissionMap", emissionTexture);
        material.SetColor("_EmissionColor", Color.white);
        material.SetColor("_RimColor", AccentColors[index]);
        material.SetFloat("_EmissionStrength", EmissionStrength[index]);
        material.SetColor("_AmbientColor", new Color(1.08f, 1.02f, 0.88f, 1f));
        material.SetColor("_LightColor", new Color(1.08f, 1.00f, 0.84f, 1f));
        material.SetFloat("_RimStrength", 0.015f);
        material.SetFloat("_RimPower", 4.5f);
        material.SetFloat("_SpecularStrength", 0.04f);
        material.SetFloat("_WrapDiffuse", 0.72f);
        material.SetFloat("_AlphaCutoff", 0.035f);
        material.SetFloat("_CoverageSoftness", 0.025f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AssignPrefabMaterial(string prefabPath, Material material)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            throw new FileNotFoundException($"Missing resource prefab {prefabPath}.");
        }

        SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer == null)
        {
            throw new MissingComponentException($"Missing SpriteRenderer on {prefabPath}.");
        }

        renderer.sharedMaterial = material;
        EditorUtility.SetDirty(renderer);
        PrefabUtility.SavePrefabAsset(prefab);
    }

    private static void AssignSceneMaterials()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity", OpenSceneMode.Single);
        GameObject root = GameObject.Find("Resource Pickups");
        if (root == null)
        {
            return;
        }

        for (int i = 0; i < root.transform.childCount; i++)
        {
            Transform child = root.transform.GetChild(i);
            int resourceIndex = ResolveResourceIndex(child.name);
            if (resourceIndex < 0)
            {
                continue;
            }

            SpriteRenderer renderer = child.GetComponentInChildren<SpriteRenderer>(true);
            Material material = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/Art/Materials/Resources/{ResourceNames[resourceIndex]}ResourceSprite.mat");
            if (renderer == null || material == null)
            {
                continue;
            }

            renderer.sharedMaterial = material;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static int ResolveResourceIndex(string objectName)
    {
        if (objectName.StartsWith("Apple")) return 0;
        if (objectName.StartsWith("Mushroom")) return 1;
        if (objectName.StartsWith("SpecialWeed")) return 2;
        if (objectName.StartsWith("Wood")) return 3;
        if (objectName.StartsWith("Stone")) return 4;
        return -1;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static void ConfigureSpriteImporter(string path, int maxSize, float pixelsPerUnit)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize = maxSize;
        importer.SaveAndReimport();
    }

    private static void ConfigureMapImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.maxTextureSize = 256;
        importer.SaveAndReimport();
    }
}
