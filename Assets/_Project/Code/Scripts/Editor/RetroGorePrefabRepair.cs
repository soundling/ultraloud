using UnityEditor;
using UnityEngine;

public static class RetroGorePrefabRepair
{
    private const string DefaultGoreProfilePath = "Assets/_Project/Art/Sprites/Effects/Gore/PigGoreProfile.asset";
    private const string BloodSplatterPath = "Assets/_Project/Art/Sprites/Effects/BloodSplatter_Impact.png";
    private const string BloodSprayPath = "Assets/_Project/Art/Sprites/Effects/BloodSpray_Droplets.png";

    private readonly struct GorePrefabRule
    {
        public readonly string Path;
        public readonly RetroShootableSurfaceKind SurfaceKind;
        public readonly float FeedbackScale;
        public readonly float IntensityMultiplier;
        public readonly Vector3 CenterOffset;
        public readonly bool AssignBloodSprites;
        public readonly string GoreProfilePath;

        public GorePrefabRule(
            string path,
            RetroShootableSurfaceKind surfaceKind,
            float feedbackScale,
            float intensityMultiplier,
            Vector3 centerOffset,
            bool assignBloodSprites,
            string goreProfilePath = DefaultGoreProfilePath)
        {
            Path = path;
            SurfaceKind = surfaceKind;
            FeedbackScale = feedbackScale;
            IntensityMultiplier = intensityMultiplier;
            CenterOffset = centerOffset;
            AssignBloodSprites = assignBloodSprites;
            GoreProfilePath = goreProfilePath;
        }
    }

    private static readonly GorePrefabRule[] GorePrefabRules =
    {
        new("Assets/_Project/Art/Sprites/NPCs/Merchant/Generated/Merchant.prefab", RetroShootableSurfaceKind.Flesh, 0.9f, 1.35f, new Vector3(0f, 0.42f, 0f), true),
        new("Assets/_Project/Art/Sprites/NPCs/BritishGhoul/Generated/BritishGhoul.prefab", RetroShootableSurfaceKind.Flesh, 1f, 1.45f, new Vector3(0f, 0.45f, 0f), true),
        new("Assets/_Project/Art/Sprites/NPCs/PinUp/Generated/PinUp.prefab", RetroShootableSurfaceKind.Flesh, 0.85f, 1f, new Vector3(0f, 0.36f, 0f), true),
        new("Assets/_Project/Art/Sprites/NPCs/Pig/Generated/Pig.prefab", RetroShootableSurfaceKind.Flesh, 1f, 1f, new Vector3(0f, 0.35f, 0f), true),
        new("Assets/_Project/Art/Sprites/NPCs/Horse/Generated/Horse.prefab", RetroShootableSurfaceKind.Flesh, 1.05f, 1.35f, new Vector3(0f, 0.48f, 0f), true),
        new("Assets/_Project/Art/Sprites/NPCs/HorseMerchant/Generated/HorseMerchant.prefab", RetroShootableSurfaceKind.Flesh, 1.08f, 1.45f, new Vector3(0f, 0.5f, 0f), true),
        new("Assets/_Project/Art/Sprites/NPCs/MotocrossMerchant/Generated/MotocrossMerchant.prefab", RetroShootableSurfaceKind.Flesh, 1f, 1.35f, new Vector3(0f, 0.44f, 0f), true),
        new("Assets/_Project/Art/Sprites/NPCs/SkeletonMotocross/Generated/SkeletonMotocross.prefab", RetroShootableSurfaceKind.Bone, 1.05f, 0.82f, new Vector3(0f, 0.35f, 0f), false),
        new("Assets/_Project/Content/Actors/KillerRabbit/Prefabs/KillerRabbit.prefab", RetroShootableSurfaceKind.Flesh, 0.75f, 1.35f, new Vector3(0f, 0.34f, 0f), true, "Assets/_Project/Content/Actors/KillerRabbit/Profiles/KillerRabbitGoreProfile.asset")
    };

    [MenuItem("Tools/Ultraloud/VFX/Repair NPC Gore Prefabs")]
    public static void RepairNpcGorePrefabs()
    {
        int changed = 0;
        for (int i = 0; i < GorePrefabRules.Length; i++)
        {
            if (RepairPrefab(GorePrefabRules[i]))
            {
                changed++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"NPC gore prefab repair complete. Updated {changed} prefab(s).");
    }

    public static void ConfigurePrefabGore(
        GameObject root,
        RetroDamageable damageable,
        RetroShootableSurfaceKind surfaceKind,
        float feedbackScale,
        float intensityMultiplier,
        Vector3 centerOffset,
        bool assignBloodSprites)
    {
        ConfigurePrefabGore(root, damageable, surfaceKind, feedbackScale, intensityMultiplier, centerOffset, assignBloodSprites, DefaultGoreProfilePath);
    }

    public static void ConfigurePrefabGore(
        GameObject root,
        RetroDamageable damageable,
        RetroShootableSurfaceKind surfaceKind,
        float feedbackScale,
        float intensityMultiplier,
        Vector3 centerOffset,
        bool assignBloodSprites,
        string goreProfilePath)
    {
        if (root == null || damageable == null)
        {
            return;
        }

        ConfigureDamageableFeedback(damageable, surfaceKind, feedbackScale, assignBloodSprites);
        RetroGibOnDeath gib = GetOrAdd<RetroGibOnDeath>(root);
        ConfigureGib(gib, damageable, intensityMultiplier, centerOffset, goreProfilePath);
    }

    private static bool RepairPrefab(GorePrefabRule rule)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rule.Path);
        if (prefab == null)
        {
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(rule.Path);
        try
        {
            RetroDamageable damageable = root.GetComponent<RetroDamageable>();
            if (damageable == null)
            {
                return false;
            }

            ConfigurePrefabGore(
                root,
                damageable,
                rule.SurfaceKind,
                rule.FeedbackScale,
                rule.IntensityMultiplier,
                rule.CenterOffset,
                rule.AssignBloodSprites,
                rule.GoreProfilePath);
            PrefabUtility.SaveAsPrefabAsset(root, rule.Path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureDamageableFeedback(
        RetroDamageable damageable,
        RetroShootableSurfaceKind surfaceKind,
        float feedbackScale,
        bool assignBloodSprites)
    {
        SerializedObject serialized = new(damageable);
        if (assignBloodSprites)
        {
            SetObject(serialized, "bloodSplatterSprite", AssetDatabase.LoadAssetAtPath<Sprite>(BloodSplatterPath));
            SetObject(serialized, "bloodSpraySprite", AssetDatabase.LoadAssetAtPath<Sprite>(BloodSprayPath));
            SetBool(serialized, "spawnBloodOnHit", true);
            SetBool(serialized, "spawnBloodOnDeath", true);
        }

        SetBool(serialized, "ensureShootableFeedback", true);
        SetEnum(serialized, "shootableSurfaceKind", (int)surfaceKind);
        SetFloat(serialized, "shootableFeedbackScale", feedbackScale);
        SetFloat(serialized, "shootableDeathEffectMultiplier", 0.2f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(damageable);
    }

    private static void ConfigureGib(RetroGibOnDeath gib, RetroDamageable damageable, float intensityMultiplier, Vector3 centerOffset, string goreProfilePath)
    {
        SerializedObject serialized = new(gib);
        serialized.FindProperty("damageable").objectReferenceValue = damageable;
        serialized.FindProperty("goreProfile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RetroGoreProfile>(goreProfilePath);
        SetBool(serialized, "alwaysGibOnDeath", true);
        SetBool(serialized, "useProfileThresholds", true);
        SetBool(serialized, "spawnAtRendererCenter", true);
        serialized.FindProperty("localCenterOffset").vector3Value = centerOffset;
        SetFloat(serialized, "intensityMultiplier", intensityMultiplier);
        SetBool(serialized, "debugDecision", false);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gib);
    }

    private static T GetOrAdd<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        return component != null ? component : root.AddComponent<T>();
    }

    private static void SetBool(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetFloat(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
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

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }
}
