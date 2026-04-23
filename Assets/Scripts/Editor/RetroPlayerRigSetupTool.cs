using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public static class RetroPlayerRigSetupTool
{
    private const string DefaultPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
    private const string NoFrictionMaterialPath = "Assets/Settings/PlayerNoFriction.physicMaterial";
    private const string GunMaterialPath = "Assets/Sprites/Gun.mat";

    [MenuItem("Tools/Ultraloud/Player/Setup Selected Or Scene Player")]
    private static void SetupSelectedOrScenePlayer()
    {
        Run(savePrefab: false);
    }

    [MenuItem("Tools/Ultraloud/Player/Setup Selected Or Scene Player And Save Prefab")]
    private static void SetupSelectedOrScenePlayerAndSavePrefab()
    {
        Run(savePrefab: true);
    }

    [MenuItem("Tools/Ultraloud/Player/Setup Selected Or Scene Player", true)]
    [MenuItem("Tools/Ultraloud/Player/Setup Selected Or Scene Player And Save Prefab", true)]
    private static bool ValidateSetupMenu()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void Run(bool savePrefab)
    {
        EditablePlayerTarget target = ResolveTarget();
        try
        {
            ConfigurePlayer(target.Root);

            if (target.IsPrefabAssetEditing)
            {
                PrefabUtility.SaveAsPrefabAsset(target.Root, target.PrefabAssetPath);
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(target.PrefabAssetPath);
                Selection.activeObject = prefabAsset;
                EditorGUIUtility.PingObject(prefabAsset);
            }
            else if (savePrefab)
            {
                string prefabPath = ResolvePrefabOutputPath(target.Root);
                EnsureAssetFolderExists(Path.GetDirectoryName(prefabPath)?.Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAssetAndConnect(target.Root, prefabPath, InteractionMode.UserAction);
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Selection.activeObject = prefabAsset;
                EditorGUIUtility.PingObject(prefabAsset);
            }
            else
            {
                Selection.activeGameObject = target.Root;
            }

            Debug.Log($"Player rig setup completed on '{target.Root.name}'.", target.Root);
        }
        finally
        {
            if (target.IsPrefabAssetEditing)
            {
                PrefabUtility.UnloadPrefabContents(target.Root);
            }
            else if (target.Root.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(target.Root.scene);
            }
        }
    }

    private static EditablePlayerTarget ResolveTarget()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected != null && EditorUtility.IsPersistent(selected))
        {
            string prefabAssetPath = AssetDatabase.GetAssetPath(selected);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabAssetPath);
            return new EditablePlayerTarget(prefabRoot, prefabAssetPath, true);
        }

        GameObject root = ResolveSceneRoot(selected);
        if (root == null)
        {
            root = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(root, "Create Player");
        }

        return new EditablePlayerTarget(root, ResolvePrefabOutputPath(root), false);
    }

    private static GameObject ResolveSceneRoot(GameObject selected)
    {
        if (selected != null)
        {
            Transform current = selected.transform;
            while (current.parent != null)
            {
                current = current.parent;
            }

            return current.gameObject;
        }

        GameObject existing = GameObject.Find("Player");
        return existing;
    }

    private static void ConfigurePlayer(GameObject root)
    {
        Undo.RegisterFullObjectHierarchyUndo(root, "Setup Player Rig");

        root.name = "Player";
        root.transform.localScale = Vector3.one;
        Vector3 playerEuler = root.transform.eulerAngles;
        root.transform.rotation = Quaternion.Euler(0f, playerEuler.y, 0f);

        Camera viewCamera = EnsureViewCamera(root.transform);
        GameObject quadObject = EnsureViewModelQuad(root.transform, viewCamera.transform);
        GameObject capsuleVisual = EnsureVisualCapsule(root.transform);

        RemoveConflictingPhysics(capsuleVisual);
        RemoveConflictingPhysics(quadObject);

        UnityEngine.Object noFrictionMaterial = GetOrCreateNoFrictionMaterialAsset();

        CapsuleCollider collider = GetOrAddComponent<CapsuleCollider>(root);
        collider.direction = 1;
        collider.center = new Vector3(0f, 1f, 0f);
        collider.radius = 0.45f;
        collider.height = 2f;
        collider.isTrigger = false;
        AssignColliderMaterial(collider, noFrictionMaterial);

        Rigidbody body = GetOrAddComponent<Rigidbody>(root);
        body.mass = 1f;
        body.useGravity = true;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
#if UNITY_6000_0_OR_NEWER
        body.linearDamping = 0f;
        body.angularDamping = 0f;
#else
        body.drag = 0f;
        body.angularDrag = 0f;
#endif
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

        RetroFpsController controller = GetOrAddComponent<RetroFpsController>(root);
        ApplyControllerReferences(controller, viewCamera, quadObject.transform, capsuleVisual.GetComponent<Renderer>(), inputActions);

        MeshRenderer capsuleRenderer = capsuleVisual.GetComponent<MeshRenderer>();
        if (capsuleRenderer != null)
        {
            capsuleRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            capsuleRenderer.receiveShadows = false;
        }
    }

    private static Camera EnsureViewCamera(Transform root)
    {
        GameObject cameraObject = FindDirectChild(root, "Main Camera");
        if (cameraObject == null)
        {
            cameraObject = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create Main Camera");
            cameraObject.transform.SetParent(root, false);
        }

        cameraObject.name = "Main Camera";
        cameraObject.tag = "MainCamera";
        cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;
        cameraObject.transform.localScale = Vector3.one;

        Camera camera = GetOrAddComponent<Camera>(cameraObject);
        camera.nearClipPlane = 0.01f;
        camera.fieldOfView = 75f;

        GetOrAddComponent<AudioListener>(cameraObject);
        return camera;
    }

    private static GameObject EnsureViewModelQuad(Transform root, Transform cameraTransform)
    {
        GameObject quadObject = FindDirectChild(root, "Quad");
        if (quadObject == null)
        {
            quadObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Undo.RegisterCreatedObjectUndo(quadObject, "Create Viewmodel Quad");
            quadObject.name = "Quad";
            quadObject.transform.SetParent(root, false);
        }

        quadObject.name = "Quad";
        quadObject.transform.localPosition = new Vector3(0.26f, 1.32f, 0.75f);
        quadObject.transform.localRotation = Quaternion.identity;
        quadObject.transform.localScale = new Vector3(1.2f, 0.9f, 1f);

        MeshRenderer renderer = quadObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Material gunMaterial = AssetDatabase.LoadAssetAtPath<Material>(GunMaterialPath);
            if (gunMaterial != null && (renderer.sharedMaterial == null || renderer.sharedMaterial.name.StartsWith("Default")))
            {
                renderer.sharedMaterial = gunMaterial;
            }
        }

        if (quadObject.TryGetComponent(out Collider quadCollider))
        {
            Undo.DestroyObjectImmediate(quadCollider);
        }

        return quadObject;
    }

    private static GameObject EnsureVisualCapsule(Transform root)
    {
        GameObject capsuleObject = FindDirectChild(root, "Capsule");
        if (capsuleObject == null)
        {
            capsuleObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(capsuleObject, "Create Capsule Visual");
            capsuleObject.name = "Capsule";
            capsuleObject.transform.SetParent(root, false);
        }

        capsuleObject.name = "Capsule";
        capsuleObject.transform.localPosition = new Vector3(0f, 1f, 0f);
        capsuleObject.transform.localRotation = Quaternion.identity;
        capsuleObject.transform.localScale = new Vector3(0.9f, 1f, 0.9f);

        return capsuleObject;
    }

    private static void RemoveConflictingPhysics(GameObject child)
    {
        if (child == null)
        {
            return;
        }

        Collider[] colliders = child.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Undo.DestroyObjectImmediate(colliders[i]);
        }

        Rigidbody childBody = child.GetComponent<Rigidbody>();
        if (childBody != null)
        {
            Undo.DestroyObjectImmediate(childBody);
        }

        RetroFpsController childController = child.GetComponent<RetroFpsController>();
        if (childController != null)
        {
            Undo.DestroyObjectImmediate(childController);
        }
    }

    private static void ApplyControllerReferences(
        RetroFpsController controller,
        Camera viewCamera,
        Transform viewModelRoot,
        Renderer bodyRenderer,
        InputActionAsset inputActions)
    {
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("viewCamera").objectReferenceValue = viewCamera;
        serializedController.FindProperty("viewModelRoot").objectReferenceValue = viewModelRoot;
        serializedController.FindProperty("bodyRenderer").objectReferenceValue = bodyRenderer;
        serializedController.FindProperty("inputActions").objectReferenceValue = inputActions;
        serializedController.FindProperty("actionMapName").stringValue = "Player";
        serializedController.FindProperty("moveActionName").stringValue = "Move";
        serializedController.FindProperty("lookActionName").stringValue = "Look";
        serializedController.FindProperty("jumpActionName").stringValue = "Jump";
        serializedController.FindProperty("sprintActionName").stringValue = "Sprint";
        serializedController.FindProperty("crouchActionName").stringValue = "Crouch";
        serializedController.FindProperty("lockCursorOnEnable").boolValue = true;
        serializedController.FindProperty("hideBodyRenderer").boolValue = true;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
    }

    private static UnityEngine.Object GetOrCreateNoFrictionMaterialAsset()
    {
        UnityEngine.Object material = AssetDatabase.LoadMainAssetAtPath(NoFrictionMaterialPath);
        if (material == null)
        {
            EnsureAssetFolderExists(Path.GetDirectoryName(NoFrictionMaterialPath)?.Replace('\\', '/'));
            Type physicsMaterialType = ResolvePhysicsMaterialType();
            if (physicsMaterialType == null)
            {
                return null;
            }

            material = Activator.CreateInstance(physicsMaterialType, "PlayerNoFriction") as UnityEngine.Object;
            if (material == null)
            {
                return null;
            }

            AssetDatabase.CreateAsset(material, NoFrictionMaterialPath);
        }

        SetMemberValue(material, "dynamicFriction", 0f);
        SetMemberValue(material, "staticFriction", 0f);
        SetEnumMemberValue(material, "frictionCombine", "Minimum");
        SetMemberValue(material, "bounciness", 0f);
        SetEnumMemberValue(material, "bounceCombine", "Minimum");
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static void AssignColliderMaterial(Collider collider, UnityEngine.Object material)
    {
        SerializedObject serializedCollider = new SerializedObject(collider);
        SerializedProperty materialProperty = serializedCollider.FindProperty("m_Material");
        if (materialProperty == null)
        {
            return;
        }

        materialProperty.objectReferenceValue = material;
        serializedCollider.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Type ResolvePhysicsMaterialType()
    {
        Assembly physicsAssembly = typeof(Collider).Assembly;
        return physicsAssembly.GetType("UnityEngine.PhysicsMaterial")
            ?? physicsAssembly.GetType("UnityEngine.PhysicMaterial");
    }

    private static void SetMemberValue(UnityEngine.Object target, string memberName, object value)
    {
        Type targetType = target.GetType();
        PropertyInfo property = targetType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value);
            return;
        }

        FieldInfo field = targetType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }

    private static void SetEnumMemberValue(UnityEngine.Object target, string memberName, string enumValueName)
    {
        Type targetType = target.GetType();
        PropertyInfo property = targetType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (property != null && property.CanWrite && property.PropertyType.IsEnum)
        {
            property.SetValue(target, Enum.Parse(property.PropertyType, enumValueName));
            return;
        }

        FieldInfo field = targetType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType.IsEnum)
        {
            field.SetValue(target, Enum.Parse(field.FieldType, enumValueName));
        }
    }

    private static string ResolvePrefabOutputPath(GameObject root)
    {
        string existingPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
        if (!string.IsNullOrWhiteSpace(existingPrefabPath))
        {
            return existingPrefabPath.Replace('\\', '/');
        }

        return DefaultPrefabPath;
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

    private static GameObject FindDirectChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
        {
            component = Undo.AddComponent<T>(gameObject);
        }

        return component;
    }

    private readonly struct EditablePlayerTarget
    {
        public EditablePlayerTarget(GameObject root, string prefabAssetPath, bool isPrefabAssetEditing)
        {
            Root = root;
            PrefabAssetPath = prefabAssetPath;
            IsPrefabAssetEditing = isPrefabAssetEditing;
        }

        public GameObject Root { get; }
        public string PrefabAssetPath { get; }
        public bool IsPrefabAssetEditing { get; }
    }
}
