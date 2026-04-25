using UnityEditor;
using UnityEngine;

public static class RetroShootablePrefabUtility
{
    public static void ConfigureBigRock(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        BoxCollider collider = GetOrAddSingleComponent<BoxCollider>(root);
        collider.isTrigger = false;
        collider.center = new Vector3(0f, 1.175f, 0f);
        collider.size = new Vector3(4.2f, 2.35f, 3.15f);

        RetroDamageable damageable = GetOrAddSingleComponent<RetroDamageable>(root);
        ConfigureDamageable(
            damageable,
            maxHealth: 9999f,
            destroyOnDeath: false,
            disableRenderersOnDeath: false,
            disableCollidersOnDeath: false,
            destroyDelay: 0f);

        RetroShootableFeedback feedback = GetOrAddSingleComponent<RetroShootableFeedback>(root);
        ConfigureFeedback(
            feedback,
            damageable,
            RetroShootableSurfaceKind.Stone,
            visualKickDistance: 0.018f,
            visualKickAngle: 1.5f,
            visualKickReturnSpeed: 18f,
            effectScale: 1.1f,
            deathEffectMultiplier: 2.2f,
            disableFlockAgentOnDeath: false);

        MarkDirty(root, collider, damageable, feedback);
    }

    public static void ConfigureHybridTree(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        CapsuleCollider collider = GetOrAddSingleComponent<CapsuleCollider>(root);
        collider.isTrigger = false;
        collider.direction = 1;
        collider.center = new Vector3(0f, 2.9f, 0f);
        collider.radius = 1.8f;
        collider.height = 5.8f;

        RetroDamageable damageable = GetOrAddSingleComponent<RetroDamageable>(root);
        ConfigureDamageable(
            damageable,
            maxHealth: 1800f,
            destroyOnDeath: false,
            disableRenderersOnDeath: false,
            disableCollidersOnDeath: false,
            destroyDelay: 0f);

        RetroShootableFeedback feedback = GetOrAddSingleComponent<RetroShootableFeedback>(root);
        ConfigureFeedback(
            feedback,
            damageable,
            RetroShootableSurfaceKind.Wood,
            visualKickDistance: 0.05f,
            visualKickAngle: 4.5f,
            visualKickReturnSpeed: 16f,
            effectScale: 1f,
            deathEffectMultiplier: 2.4f,
            disableFlockAgentOnDeath: false);

        MarkDirty(root, collider, damageable, feedback);
    }

    public static void ConfigureSmallBird(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        BoxCollider collider = GetOrAddSingleComponent<BoxCollider>(root);
        collider.isTrigger = false;
        collider.center = Vector3.zero;
        collider.size = new Vector3(1.05f, 0.58f, 0.55f);

        RetroDamageable damageable = GetOrAddSingleComponent<RetroDamageable>(root);
        ConfigureDamageable(
            damageable,
            maxHealth: 16f,
            destroyOnDeath: true,
            disableRenderersOnDeath: true,
            disableCollidersOnDeath: true,
            destroyDelay: 0.15f);

        RetroShootableFeedback feedback = GetOrAddSingleComponent<RetroShootableFeedback>(root);
        ConfigureFeedback(
            feedback,
            damageable,
            RetroShootableSurfaceKind.Bird,
            visualKickDistance: 0.075f,
            visualKickAngle: 9f,
            visualKickReturnSpeed: 18f,
            effectScale: 0.75f,
            deathEffectMultiplier: 3f,
            disableFlockAgentOnDeath: true);

        MarkDirty(root, collider, damageable, feedback);
    }

    private static T GetOrAddSingleComponent<T>(GameObject root) where T : Component
    {
        T[] components = root.GetComponents<T>();
        T component = components.Length > 0 ? components[0] : root.AddComponent<T>();
        for (int i = 1; i < components.Length; i++)
        {
            Object.DestroyImmediate(components[i]);
        }

        return component;
    }

    private static void ConfigureDamageable(
        RetroDamageable damageable,
        float maxHealth,
        bool destroyOnDeath,
        bool disableRenderersOnDeath,
        bool disableCollidersOnDeath,
        float destroyDelay)
    {
        if (damageable == null)
        {
            return;
        }

        SerializedObject serializedDamageable = new(damageable);
        serializedDamageable.Update();
        SetFloat(serializedDamageable, "maxHealth", maxHealth);
        SetBool(serializedDamageable, "destroyOnDeath", destroyOnDeath);
        SetBool(serializedDamageable, "disableRenderersOnDeath", disableRenderersOnDeath);
        SetBool(serializedDamageable, "disableCollidersOnDeath", disableCollidersOnDeath);
        SetFloat(serializedDamageable, "destroyDelay", destroyDelay);
        SetObject(serializedDamageable, "bloodSplatterSprite", null);
        SetObject(serializedDamageable, "bloodSpraySprite", null);
        SetBool(serializedDamageable, "spawnBloodOnHit", false);
        SetBool(serializedDamageable, "spawnBloodOnDeath", false);
        serializedDamageable.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureFeedback(
        RetroShootableFeedback feedback,
        RetroDamageable damageable,
        RetroShootableSurfaceKind surfaceKind,
        float visualKickDistance,
        float visualKickAngle,
        float visualKickReturnSpeed,
        float effectScale,
        float deathEffectMultiplier,
        bool disableFlockAgentOnDeath)
    {
        if (feedback == null)
        {
            return;
        }

        SerializedObject serializedFeedback = new(feedback);
        serializedFeedback.Update();
        SetObject(serializedFeedback, "damageable", damageable);
        SetEnum(serializedFeedback, "surfaceKind", (int)surfaceKind);
        SetBool(serializedFeedback, "spawnImpactEffects", true);
        SetBool(serializedFeedback, "spawnDeathEffects", true);
        SetBool(serializedFeedback, "useImpactFlash", true);
        SetObject(serializedFeedback, "visualRoot", null);
        SetBool(serializedFeedback, "kickVisualRoot", true);
        SetFloat(serializedFeedback, "visualKickDistance", visualKickDistance);
        SetFloat(serializedFeedback, "visualKickAngle", visualKickAngle);
        SetFloat(serializedFeedback, "visualKickReturnSpeed", visualKickReturnSpeed);
        SetFloat(serializedFeedback, "effectScale", effectScale);
        SetFloat(serializedFeedback, "deathEffectMultiplier", deathEffectMultiplier);
        SetBool(serializedFeedback, "disableFlockAgentOnDeath", disableFlockAgentOnDeath);
        serializedFeedback.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void MarkDirty(GameObject root, params Object[] objects)
    {
        if (root != null)
        {
            EditorUtility.SetDirty(root);
        }

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
            {
                EditorUtility.SetDirty(objects[i]);
            }
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

    private static void SetFloat(SerializedObject target, string propertyName, float value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetObject(SerializedObject target, string propertyName, Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
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
}
