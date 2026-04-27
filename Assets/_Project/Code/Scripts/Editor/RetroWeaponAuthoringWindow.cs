using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class RetroWeaponAuthoringWindow : EditorWindow
{
    private const string DefaultPlayerPrefabPath = "Assets/_Project/Content/Actors/Player/Prefabs/Player.prefab";
    private const string DefaultDefinitionFolder = "Assets/_Project/Content/Gameplay/Weapons/Definitions";
    private const string FallbackGunVolumeMapSetPath = "Assets/_Project/Data/GunVolumeMapSet.asset";

    private static readonly string[] DefaultDefinitionPaths =
    {
        "Assets/_Project/Content/Gameplay/Weapons/Definitions/Pistol.asset",
        "Assets/_Project/Content/Gameplay/Weapons/Definitions/Rifle.asset",
        "Assets/_Project/Content/Gameplay/Weapons/Definitions/Shotgun.asset",
        "Assets/_Project/Content/Gameplay/Weapons/Definitions/GrenadeLauncher.asset"
    };

    private static readonly string[] MuzzleFlashSpritePaths =
    {
        "Assets/_Project/Art/Sprites/Weapons/MuzzleFlash_Pistol.png",
        "Assets/_Project/Art/Sprites/Weapons/MuzzleFlash_Rifle.png",
        "Assets/_Project/Art/Sprites/Weapons/MuzzleFlash_Shotgun.png",
        "Assets/_Project/Art/Sprites/Weapons/MuzzleFlash_GrenadeLauncher.png"
    };

    private static readonly string[] WeaponViewmodelMapSetPaths =
    {
        "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/PistolViewmodelMapSet.asset",
        "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/RifleViewmodelMapSet.asset",
        "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/ShotgunViewmodelMapSet.asset",
        "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/GrenadeLauncherViewmodelMapSet.asset"
    };

    private static readonly string[][] WeaponFireAnimationMapSetPaths =
    {
        new[]
        {
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/Pistol/PistolFireFrame00.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/Pistol/PistolFireFrame01.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/Pistol/PistolFireFrame02.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/Pistol/PistolFireFrame03.asset"
        },
        new[]
        {
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/Rifle/RifleFireFrame00.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/Rifle/RifleFireFrame01.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/Rifle/RifleFireFrame02.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/Rifle/RifleFireFrame03.asset"
        },
        new[]
        {
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/Shotgun/ShotgunFireFrame00.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/Shotgun/ShotgunFireFrame01.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/Shotgun/ShotgunFireFrame02.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/Shotgun/ShotgunFireFrame03.asset"
        },
        new[]
        {
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/GrenadeLauncher/GrenadeLauncherFireFrame00.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/GrenadeLauncher/GrenadeLauncherFireFrame01.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/GrenadeLauncher/GrenadeLauncherFireFrame02.asset",
            "Assets/_Project/Content/Gameplay/Weapons/ViewmodelMapSets/FireAnimation/GrenadeLauncher/GrenadeLauncherFireFrame03.asset"
        }
    };

    private static readonly string[] PistolMuzzleFlashFramePaths =
    {
        "Assets/_Project/Art/Sprites/Weapons/MuzzleFlash/Pistol/Pistol_MuzzleFlash_00.png",
        "Assets/_Project/Art/Sprites/Weapons/MuzzleFlash/Pistol/Pistol_MuzzleFlash_01.png",
        "Assets/_Project/Art/Sprites/Weapons/MuzzleFlash/Pistol/Pistol_MuzzleFlash_02.png",
        "Assets/_Project/Art/Sprites/Weapons/MuzzleFlash/Pistol/Pistol_MuzzleFlash_03.png"
    };

    private RetroWeaponSystem weaponSystem;
    private SerializedObject serializedWeaponSystem;
    private SerializedObject serializedDefinition;
    private RetroWeaponDefinition selectedDefinition;
    private Vector2 scroll;
    private int selectedSlot;
    private float nudgeStep = 0.005f;
    private float sizeStep = 0.02f;

    [MenuItem("Tools/Ultraloud/Weapons/Weapon Authoring")]
    public static void Open()
    {
        RetroWeaponAuthoringWindow window = GetWindow<RetroWeaponAuthoringWindow>("Weapon Authoring");
        window.minSize = new Vector2(620f, 620f);
        window.ResolveInitialWeaponSystem();
    }

    private void OnEnable()
    {
        ResolveInitialWeaponSystem();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeGameObject != null)
        {
            RetroWeaponSystem selectedSystem = Selection.activeGameObject.GetComponentInParent<RetroWeaponSystem>();
            if (selectedSystem != null)
            {
                SetWeaponSystem(selectedSystem);
                Repaint();
            }
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6f);
        DrawToolbar();
        EditorGUILayout.Space(8f);

        using (EditorGUILayout.ScrollViewScope scope = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = scope.scrollPosition;
            DrawWeaponSystemPanel();
            EditorGUILayout.Space(10f);
            DrawLoadoutPanel();
            EditorGUILayout.Space(10f);
            DrawSelectedDefinitionPanel();
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Find Player", EditorStyles.toolbarButton, GUILayout.Width(84f)))
            {
                SetWeaponSystem(FindSceneWeaponSystem() ?? LoadPrefabWeaponSystem());
            }

            if (GUILayout.Button("Create Missing Defaults", EditorStyles.toolbarButton, GUILayout.Width(150f)))
            {
                CreateMissingDefaultDefinitions();
            }

            if (GUILayout.Button("Assign Defaults", EditorStyles.toolbarButton, GUILayout.Width(108f)))
            {
                AssignDefaultDefinitionsToCurrentSystem();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Save Assets", EditorStyles.toolbarButton, GUILayout.Width(86f)))
            {
                SaveAssets();
            }
        }
    }

    private void DrawWeaponSystemPanel()
    {
        EditorGUILayout.LabelField("Weapon System", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        weaponSystem = (RetroWeaponSystem)EditorGUILayout.ObjectField("Target", weaponSystem, typeof(RetroWeaponSystem), true);
        if (EditorGUI.EndChangeCheck())
        {
            SetWeaponSystem(weaponSystem);
        }

        if (weaponSystem == null)
        {
            EditorGUILayout.HelpBox("Select a Player with RetroWeaponSystem, or use Find Player. The default prefab is Assets/_Project/Content/Actors/Player/Prefabs/Player.prefab.", MessageType.Info);
            return;
        }

        EnsureWeaponSystemSerialized();
        serializedWeaponSystem.Update();

        EditorGUILayout.PropertyField(serializedWeaponSystem.FindProperty("useSpriteVolumeViewModel"));
        EditorGUILayout.PropertyField(serializedWeaponSystem.FindProperty("spriteFireAnimationFrameDuration"));
        EditorGUILayout.PropertyField(serializedWeaponSystem.FindProperty("spriteMuzzleFlashDuration"));
        EditorGUILayout.PropertyField(serializedWeaponSystem.FindProperty("baseLocalPosition"));
        EditorGUILayout.PropertyField(serializedWeaponSystem.FindProperty("baseLocalEuler"));
        EditorGUILayout.PropertyField(serializedWeaponSystem.FindProperty("weaponSwitchDuration"));

        serializedWeaponSystem.ApplyModifiedProperties();
    }

    private void DrawLoadoutPanel()
    {
        EditorGUILayout.LabelField("Loadout", EditorStyles.boldLabel);
        if (weaponSystem == null)
        {
            return;
        }

        EnsureWeaponSystemSerialized();
        serializedWeaponSystem.Update();
        SerializedProperty definitions = serializedWeaponSystem.FindProperty("weaponDefinitions");
        if (definitions == null)
        {
            EditorGUILayout.HelpBox("RetroWeaponSystem is missing the weaponDefinitions field. Recompile scripts.", MessageType.Error);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            int newSize = Mathf.Max(0, EditorGUILayout.IntField("Slots", definitions.arraySize));
            if (newSize != definitions.arraySize)
            {
                definitions.arraySize = newSize;
                selectedSlot = Mathf.Clamp(selectedSlot, 0, Mathf.Max(0, definitions.arraySize - 1));
            }

            if (GUILayout.Button("+", GUILayout.Width(28f)))
            {
                definitions.arraySize++;
                selectedSlot = definitions.arraySize - 1;
            }

            if (GUILayout.Button("-", GUILayout.Width(28f)) && definitions.arraySize > 0)
            {
                definitions.DeleteArrayElementAtIndex(Mathf.Clamp(selectedSlot, 0, definitions.arraySize - 1));
                selectedSlot = Mathf.Clamp(selectedSlot, 0, Mathf.Max(0, definitions.arraySize - 1));
            }
        }

        for (int i = 0; i < definitions.arraySize; i++)
        {
            SerializedProperty element = definitions.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool selected = GUILayout.Toggle(selectedSlot == i, $"{i + 1}", "Button", GUILayout.Width(32f));
                if (selected && selectedSlot != i)
                {
                    selectedSlot = i;
                    SetSelectedDefinition(element.objectReferenceValue as RetroWeaponDefinition);
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(element, new GUIContent(ResolveSlotLabel(i)), true);
                if (EditorGUI.EndChangeCheck())
                {
                    SetSelectedDefinition(element.objectReferenceValue as RetroWeaponDefinition);
                }

                RetroWeaponDefinition definition = element.objectReferenceValue as RetroWeaponDefinition;
                using (new EditorGUI.DisabledScope(definition == null))
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(48f)))
                    {
                        EditorGUIUtility.PingObject(definition);
                    }
                }
            }
        }

        if (definitions.arraySize > 0)
        {
            selectedSlot = Mathf.Clamp(selectedSlot, 0, definitions.arraySize - 1);
            RetroWeaponDefinition slotDefinition = definitions.GetArrayElementAtIndex(selectedSlot).objectReferenceValue as RetroWeaponDefinition;
            if (slotDefinition != selectedDefinition)
            {
                SetSelectedDefinition(slotDefinition);
            }
        }

        serializedWeaponSystem.ApplyModifiedProperties();
    }

    private void DrawSelectedDefinitionPanel()
    {
        EditorGUILayout.LabelField("Selected Weapon", EditorStyles.boldLabel);
        selectedDefinition = (RetroWeaponDefinition)EditorGUILayout.ObjectField("Asset", selectedDefinition, typeof(RetroWeaponDefinition), false);

        if (selectedDefinition == null)
        {
            using (new EditorGUI.DisabledScope(weaponSystem == null))
            {
                if (GUILayout.Button("Create Asset For Selected Slot"))
                {
                    RetroWeaponDefinition definition = CreateDefinitionForSlot(selectedSlot, overwriteExisting: false);
                    AssignDefinitionToSlot(definition, selectedSlot);
                    SetSelectedDefinition(definition);
                }
            }

            return;
        }

        if (serializedDefinition == null || serializedDefinition.targetObject != selectedDefinition)
        {
            serializedDefinition = new SerializedObject(selectedDefinition);
        }

        serializedDefinition.Update();
        DrawDefinitionFields(serializedDefinition);
        EditorGUILayout.Space(8f);
        DrawMuzzleHelpers(serializedDefinition);

        if (serializedDefinition.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(selectedDefinition);
        }

        EditorGUILayout.Space(8f);
        DrawBalancePreview(selectedDefinition);
        EditorGUILayout.Space(8f);
        DrawDefinitionValidation(selectedDefinition);
    }

    private void DrawDefinitionFields(SerializedObject definition)
    {
        DrawSection(definition, "Identity", "displayName", "kind", "fireMode");
        DrawSection(definition, "Ammo", "magazineSize", "startingReserveAmmo", "maxReserveAmmo", "reloadDuration", "fireInterval");
        DrawSection(definition, "Weapon Feel", "autoReloadWhenEmpty", "dryFireCooldown", "dryFireKickPosition", "dryFireKickEuler", "spreadBloomPerShot", "maxSpreadAngle", "spreadRecoverySpeed", "movementSpreadPenalty");
        DrawSection(definition, "Combat", "damage", "range", "pellets", "spreadAngle", "impactForce");
        DrawSection(definition, "Projectile", "projectileSpeed", "explosionRadius", "explosionForce", "fuseTime");
        DrawSection(definition, "Presentation", "localPosition", "localEuler", "recoilPosition", "recoilEuler", "bodyColor", "accentColor", "primitiveMuzzleFlashScale");
        DrawSection(definition, "Bullet Trails", "bulletTrailEnabled", "bulletTrailColor", "bulletTrailWidth", "bulletTrailDuration", "bulletTrailStartOffset", "bulletTrailEndOffset", "bulletTrailMaxSegmentsPerShot");
        DrawSection(definition, "Sprite Viewmodel", "spriteMapSet", "fireAnimationMapSets", "fireAnimationFrameDuration", "spriteVisualLocalPosition", "spriteVisualLocalEuler", "spriteVisualSize", "spriteMuzzleLocalPosition", "spriteMuzzleLocalOffset", "muzzleFlashSprite", "muzzleFlashFrames", "muzzleFlashFrameDuration", "muzzleFlashSpriteSize", "baseTintMultiplier", "emissiveTintMultiplier", "weaponBodyTint", "weaponAccentTint");
    }

    private static void DrawSection(SerializedObject serializedObject, string label, params string[] propertyNames)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        for (int i = 0; i < propertyNames.Length; i++)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyNames[i]);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property);
            }
        }
    }

    private void DrawMuzzleHelpers(SerializedObject definition)
    {
        SerializedProperty muzzleOffset = definition.FindProperty("spriteMuzzleLocalOffset");
        SerializedProperty flashSize = definition.FindProperty("muzzleFlashSpriteSize");
        if (muzzleOffset == null || flashSize == null)
        {
            return;
        }

        EditorGUILayout.LabelField("Tuning Helpers", EditorStyles.boldLabel);
        nudgeStep = EditorGUILayout.FloatField("Offset Step", Mathf.Max(0.0001f, nudgeStep));
        sizeStep = EditorGUILayout.FloatField("Size Step", Mathf.Max(0.0001f, sizeStep));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("X -")) NudgeVector3(muzzleOffset, new Vector3(-nudgeStep, 0f, 0f));
            if (GUILayout.Button("X +")) NudgeVector3(muzzleOffset, new Vector3(nudgeStep, 0f, 0f));
            if (GUILayout.Button("Y -")) NudgeVector3(muzzleOffset, new Vector3(0f, -nudgeStep, 0f));
            if (GUILayout.Button("Y +")) NudgeVector3(muzzleOffset, new Vector3(0f, nudgeStep, 0f));
            if (GUILayout.Button("Z -")) NudgeVector3(muzzleOffset, new Vector3(0f, 0f, -nudgeStep));
            if (GUILayout.Button("Z +")) NudgeVector3(muzzleOffset, new Vector3(0f, 0f, nudgeStep));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Flash W -")) NudgeVector2(flashSize, new Vector2(-sizeStep, 0f));
            if (GUILayout.Button("Flash W +")) NudgeVector2(flashSize, new Vector2(sizeStep, 0f));
            if (GUILayout.Button("Flash H -")) NudgeVector2(flashSize, new Vector2(0f, -sizeStep));
            if (GUILayout.Button("Flash H +")) NudgeVector2(flashSize, new Vector2(0f, sizeStep));
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reset From Default Preset"))
            {
                Undo.RecordObject(selectedDefinition, "Reset Weapon Definition");
                ApplyDefaultPreset(selectedDefinition, selectedSlot);
                EditorUtility.SetDirty(selectedDefinition);
                definition.Update();
            }

            if (GUILayout.Button("Assign To Selected Slot"))
            {
                AssignDefinitionToSlot(selectedDefinition, selectedSlot);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply Feel Preset"))
            {
                Undo.RecordObject(selectedDefinition, "Apply Weapon Feel Preset");
                ApplyDefaultFeelPreset(selectedDefinition, selectedSlot);
                EditorUtility.SetDirty(selectedDefinition);
                definition.Update();
            }

            if (GUILayout.Button("Apply Trail Preset"))
            {
                Undo.RecordObject(selectedDefinition, "Apply Weapon Trail Preset");
                ApplyDefaultTrailPreset(selectedDefinition, selectedSlot);
                EditorUtility.SetDirty(selectedDefinition);
                definition.Update();
            }
        }
    }

    private static void DrawBalancePreview(RetroWeaponDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        float fireInterval = Mathf.Max(0.01f, definition.fireInterval);
        float reloadDuration = Mathf.Max(0.01f, definition.reloadDuration);
        float damagePerShot = Mathf.Max(0f, definition.damage) * Mathf.Max(1, definition.pellets);
        float cyclicDps = damagePerShot / fireInterval;
        float timeToEmpty = Mathf.Max(1, definition.magazineSize) * fireInterval;
        float sustainedDps = damagePerShot * Mathf.Max(1, definition.magazineSize) / Mathf.Max(0.01f, timeToEmpty + reloadDuration);
        string spread = $"{definition.spreadAngle:0.##} deg base / {Mathf.Max(definition.spreadAngle, definition.maxSpreadAngle):0.##} deg max";
        string message =
            $"Damage/trigger: {damagePerShot:0.#}   Cyclic DPS: {cyclicDps:0.#}   Sustained DPS: {sustainedDps:0.#}\n" +
            $"Time to empty: {timeToEmpty:0.##}s   Reload: {reloadDuration:0.##}s   Spread: {spread}\n" +
            $"Bloom: +{definition.spreadBloomPerShot:0.###}/shot, recovers {definition.spreadRecoverySpeed:0.##}/s, movement +{definition.movementSpreadPenalty:0.##}";

        EditorGUILayout.HelpBox(message, MessageType.None);
    }

    private static void DrawDefinitionValidation(RetroWeaponDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

        int warningCount = 0;
        if (definition.spriteMapSet == null)
        {
            warningCount++;
            EditorGUILayout.HelpBox("Sprite Viewmodel is missing a map set. The weapon will fall back to primitive geometry.", MessageType.Warning);
        }

        if (definition.muzzleFlashSprite == null && !HasAnySprite(definition.muzzleFlashFrames))
        {
            warningCount++;
            EditorGUILayout.HelpBox("Muzzle Flash Sprite/Frames are unassigned. Sprite weapons will fire without a flash sprite.", MessageType.Warning);
        }

        if (definition.spriteMapSet != null && !HasAnyMapSet(definition.fireAnimationMapSets))
        {
            warningCount++;
            EditorGUILayout.HelpBox("Fire Animation Map Sets are unassigned. The weapon will still recoil, but the viewmodel sprite will not animate through the shot.", MessageType.Info);
        }

        if (definition.maxSpreadAngle < definition.spreadAngle)
        {
            warningCount++;
            EditorGUILayout.HelpBox("Max Spread Angle is below base Spread Angle. OnValidate will clamp it upward.", MessageType.Warning);
        }

        if (definition.fireMode == RetroFireMode.Automatic && definition.spreadBloomPerShot <= 0f)
        {
            warningCount++;
            EditorGUILayout.HelpBox("Automatic fire has no spread bloom. This can feel flat unless the weapon is meant to stay laser-stable.", MessageType.Info);
        }

        if (definition.kind == RetroWeaponKind.Hitscan && definition.bulletTrailEnabled && definition.bulletTrailMaxSegmentsPerShot < definition.pellets)
        {
            warningCount++;
            EditorGUILayout.HelpBox("Bullet trail segments are lower than pellet count. Some pellets will not draw trails, which is fine for performance but worth authoring intentionally.", MessageType.Info);
        }

        if (definition.kind == RetroWeaponKind.GrenadeLauncher)
        {
            if (definition.projectileSpeed <= 0f)
            {
                warningCount++;
                EditorGUILayout.HelpBox("Projectile Speed is zero, so grenade launch velocity will collapse to player movement.", MessageType.Warning);
            }

            if (definition.explosionRadius <= 0f)
            {
                warningCount++;
                EditorGUILayout.HelpBox("Explosion Radius is zero. Grenades will not produce area damage.", MessageType.Warning);
            }
        }

        if (definition.bulletTrailEnabled && definition.bulletTrailDuration > 0.18f)
        {
            warningCount++;
            EditorGUILayout.HelpBox("Bullet Trail Duration is long for rapid weapons and can visually clutter the scene.", MessageType.Info);
        }

        if (warningCount == 0)
        {
            EditorGUILayout.HelpBox("No obvious authoring issues found.", MessageType.Info);
        }
    }

    private static void NudgeVector3(SerializedProperty property, Vector3 delta)
    {
        property.vector3Value += delta;
    }

    private static void NudgeVector2(SerializedProperty property, Vector2 delta)
    {
        Vector2 value = property.vector2Value + delta;
        property.vector2Value = new Vector2(Mathf.Max(0.01f, value.x), Mathf.Max(0.01f, value.y));
    }

    private void ResolveInitialWeaponSystem()
    {
        if (weaponSystem != null)
        {
            return;
        }

        RetroWeaponSystem selectedSystem = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<RetroWeaponSystem>()
            : null;
        SetWeaponSystem(selectedSystem ?? FindSceneWeaponSystem() ?? LoadPrefabWeaponSystem());
    }

    private void SetWeaponSystem(RetroWeaponSystem system)
    {
        weaponSystem = system;
        serializedWeaponSystem = weaponSystem != null ? new SerializedObject(weaponSystem) : null;
        selectedSlot = 0;
        selectedDefinition = null;
        serializedDefinition = null;
    }

    private void SetSelectedDefinition(RetroWeaponDefinition definition)
    {
        selectedDefinition = definition;
        serializedDefinition = selectedDefinition != null ? new SerializedObject(selectedDefinition) : null;
    }

    private void EnsureWeaponSystemSerialized()
    {
        if (weaponSystem != null && (serializedWeaponSystem == null || serializedWeaponSystem.targetObject != weaponSystem))
        {
            serializedWeaponSystem = new SerializedObject(weaponSystem);
        }
    }

    private static RetroWeaponSystem FindSceneWeaponSystem()
    {
        return FindAnyObjectByType<RetroWeaponSystem>();
    }

    private static RetroWeaponSystem LoadPrefabWeaponSystem()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlayerPrefabPath);
        return prefab != null ? prefab.GetComponent<RetroWeaponSystem>() : null;
    }

    private void CreateMissingDefaultDefinitions()
    {
        EnsureAssetFolderExists(DefaultDefinitionFolder);
        for (int i = 0; i < DefaultDefinitionPaths.Length; i++)
        {
            CreateDefinitionForSlot(i, overwriteExisting: false);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private RetroWeaponDefinition CreateDefinitionForSlot(int slot, bool overwriteExisting)
    {
        EnsureAssetFolderExists(DefaultDefinitionFolder);
        string path = ResolveDefinitionPath(slot);
        RetroWeaponDefinition existing = AssetDatabase.LoadAssetAtPath<RetroWeaponDefinition>(path);
        if (existing != null && !overwriteExisting)
        {
            return existing;
        }

        RetroWeaponDefinition definition = existing;
        if (definition == null)
        {
            definition = CreateInstance<RetroWeaponDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        ApplyDefaultPreset(definition, slot);
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private void AssignDefaultDefinitionsToCurrentSystem()
    {
        CreateMissingDefaultDefinitions();
        if (weaponSystem == null)
        {
            SetWeaponSystem(FindSceneWeaponSystem() ?? LoadPrefabWeaponSystem());
        }

        if (weaponSystem == null)
        {
            return;
        }

        EnsureWeaponSystemSerialized();
        serializedWeaponSystem.Update();
        SerializedProperty definitions = serializedWeaponSystem.FindProperty("weaponDefinitions");
        definitions.arraySize = DefaultDefinitionPaths.Length;
        for (int i = 0; i < DefaultDefinitionPaths.Length; i++)
        {
            definitions.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<RetroWeaponDefinition>(DefaultDefinitionPaths[i]);
        }

        serializedWeaponSystem.ApplyModifiedProperties();
        EditorUtility.SetDirty(weaponSystem);
        SaveAssets();
    }

    private void AssignDefinitionToSlot(RetroWeaponDefinition definition, int slot)
    {
        if (weaponSystem == null || definition == null)
        {
            return;
        }

        EnsureWeaponSystemSerialized();
        serializedWeaponSystem.Update();
        SerializedProperty definitions = serializedWeaponSystem.FindProperty("weaponDefinitions");
        if (definitions.arraySize <= slot)
        {
            definitions.arraySize = slot + 1;
        }

        definitions.GetArrayElementAtIndex(slot).objectReferenceValue = definition;
        serializedWeaponSystem.ApplyModifiedProperties();
        EditorUtility.SetDirty(weaponSystem);
    }

    private static void ApplyDefaultPreset(RetroWeaponDefinition definition, int slot)
    {
        FirstPersonSpriteVolumeMapSet defaultMapSet = LoadWeaponViewmodelMapSet(slot);
        Sprite muzzleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MuzzleFlashSpritePaths[Mathf.Clamp(slot, 0, MuzzleFlashSpritePaths.Length - 1)]);

        switch (slot)
        {
            case 0:
                ApplyPreset(definition, "Pistol", RetroWeaponKind.Hitscan, RetroFireMode.SemiAuto, 12, 72, 72, 1.2f, 0.28f, 28f, 110f, 1, 0.45f, 18f, 0f, 0f, 0f, 0f, new Vector3(0.02f, -0.01f, 0f), Vector3.zero, new Vector3(0f, 0.006f, -0.06f), new Vector3(4.5f, 1.2f, 1.6f), new Color(0.13f, 0.13f, 0.15f), new Color(0.72f, 0.68f, 0.58f), 0.18f, new Vector3(-0.02f, -0.02f, 0f), Vector3.zero, new Vector2(0.95f, 0.72f), new Vector3(0f, 0.02f, 0.34f), new Vector3(0.055f, 0f, 0f), muzzleSprite, new Vector2(0.38f, 0.28f), defaultMapSet);
                definition.muzzleFlashFrames = LoadSprites(PistolMuzzleFlashFramePaths);
                break;
            case 1:
                ApplyPreset(definition, "Rifle", RetroWeaponKind.Hitscan, RetroFireMode.Automatic, 30, 150, 150, 1.65f, 0.095f, 18f, 165f, 1, 0.8f, 20f, 0f, 0f, 0f, 0f, new Vector3(0.03f, -0.03f, 0.03f), new Vector3(1f, 0f, 0f), new Vector3(0f, 0.008f, -0.035f), new Vector3(2.5f, 0.7f, 1.2f), new Color(0.16f, 0.18f, 0.19f), new Color(0.42f, 0.34f, 0.22f), 0.24f, new Vector3(0.02f, -0.05f, 0.04f), new Vector3(0f, 0f, -1.5f), new Vector2(1.35f, 0.78f), new Vector3(0f, -0.02f, 0.78f), new Vector3(0.055f, 0f, 0f), muzzleSprite, new Vector2(0.62f, 0.24f), defaultMapSet);
                break;
            case 2:
                ApplyPreset(definition, "Shotgun", RetroWeaponKind.Hitscan, RetroFireMode.SemiAuto, 8, 40, 40, 1.95f, 0.82f, 11f, 60f, 8, 4.4f, 35f, 0f, 0f, 0f, 0f, new Vector3(0.02f, -0.04f, 0.06f), new Vector3(2f, 0f, 0f), new Vector3(0f, 0.01f, -0.095f), new Vector3(8f, 1.4f, 2f), new Color(0.18f, 0.14f, 0.11f), new Color(0.45f, 0.36f, 0.19f), 0.32f, new Vector3(0.01f, -0.06f, 0.06f), new Vector3(0f, 0f, -2f), new Vector2(1.45f, 0.82f), new Vector3(0f, -0.01f, 0.72f), new Vector3(0.055f, 0f, 0f), muzzleSprite, new Vector2(0.82f, 0.48f), defaultMapSet);
                break;
            default:
                ApplyPreset(definition, "Grenade Launcher", RetroWeaponKind.GrenadeLauncher, RetroFireMode.SemiAuto, 1, 10, 10, 2f, 0.9f, 95f, 120f, 1, 0.4f, 15f, 28f, 5.5f, 850f, 2.5f, new Vector3(0.05f, -0.05f, 0.08f), new Vector3(2f, 0f, 0f), new Vector3(0f, 0.015f, -0.12f), new Vector3(10f, 2f, 2.5f), new Color(0.14f, 0.16f, 0.13f), new Color(0.4f, 0.55f, 0.24f), 0.36f, new Vector3(0.02f, -0.055f, 0.06f), new Vector3(0f, 0f, -1f), new Vector2(1.25f, 0.86f), new Vector3(0f, 0f, 0.75f), new Vector3(0.055f, 0f, 0f), muzzleSprite, new Vector2(0.78f, 0.56f), defaultMapSet);
                break;
        }

        ApplyDefaultTrailPreset(definition, slot);
        ApplyDefaultFeelPreset(definition, slot);
        definition.fireAnimationMapSets = LoadWeaponFireAnimationMapSets(slot);
        definition.fireAnimationFrameDuration = ResolveDefaultFireAnimationFrameDuration(slot);
    }

    private static FirstPersonSpriteVolumeMapSet LoadWeaponViewmodelMapSet(int slot)
    {
        string path = WeaponViewmodelMapSetPaths[Mathf.Clamp(slot, 0, WeaponViewmodelMapSetPaths.Length - 1)];
        FirstPersonSpriteVolumeMapSet mapSet = AssetDatabase.LoadAssetAtPath<FirstPersonSpriteVolumeMapSet>(path);
        return mapSet != null
            ? mapSet
            : AssetDatabase.LoadAssetAtPath<FirstPersonSpriteVolumeMapSet>(FallbackGunVolumeMapSetPath);
    }

    private static FirstPersonSpriteVolumeMapSet[] LoadWeaponFireAnimationMapSets(int slot)
    {
        int safeSlot = Mathf.Clamp(slot, 0, WeaponFireAnimationMapSetPaths.Length - 1);
        string[] paths = WeaponFireAnimationMapSetPaths[safeSlot];
        FirstPersonSpriteVolumeMapSet[] mapSets = new FirstPersonSpriteVolumeMapSet[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            mapSets[i] = AssetDatabase.LoadAssetAtPath<FirstPersonSpriteVolumeMapSet>(paths[i]);
        }

        return HasAnyMapSet(mapSets) ? mapSets : new FirstPersonSpriteVolumeMapSet[0];
    }

    private static float ResolveDefaultFireAnimationFrameDuration(int slot)
    {
        return slot switch
        {
            1 => 0.018f,
            2 => 0.032f,
            3 => 0.034f,
            _ => 0.024f
        };
    }

    private static void ApplyDefaultTrailPreset(RetroWeaponDefinition definition, int slot)
    {
        definition.bulletTrailEnabled = true;
        switch (slot)
        {
            case 1:
                definition.bulletTrailColor = new Color(1f, 0.9f, 0.42f, 0.72f);
                definition.bulletTrailWidth = 0.014f;
                definition.bulletTrailDuration = 0.045f;
                definition.bulletTrailStartOffset = 0.1f;
                definition.bulletTrailEndOffset = 0.1f;
                definition.bulletTrailMaxSegmentsPerShot = 1;
                break;
            case 2:
                definition.bulletTrailColor = new Color(1f, 0.68f, 0.32f, 0.58f);
                definition.bulletTrailWidth = 0.012f;
                definition.bulletTrailDuration = 0.075f;
                definition.bulletTrailStartOffset = 0.08f;
                definition.bulletTrailEndOffset = 0.08f;
                definition.bulletTrailMaxSegmentsPerShot = 8;
                break;
            case 3:
                definition.bulletTrailColor = new Color(0.6f, 1f, 0.28f, 0.55f);
                definition.bulletTrailWidth = 0.035f;
                definition.bulletTrailDuration = 0.12f;
                definition.bulletTrailStartOffset = 0.06f;
                definition.bulletTrailEndOffset = 0f;
                definition.bulletTrailMaxSegmentsPerShot = 1;
                break;
            default:
                definition.bulletTrailColor = new Color(1f, 0.82f, 0.35f, 0.78f);
                definition.bulletTrailWidth = 0.018f;
                definition.bulletTrailDuration = 0.065f;
                definition.bulletTrailStartOffset = 0.08f;
                definition.bulletTrailEndOffset = 0.08f;
                definition.bulletTrailMaxSegmentsPerShot = 1;
                break;
        }
    }

    private static void ApplyDefaultFeelPreset(RetroWeaponDefinition definition, int slot)
    {
        definition.autoReloadWhenEmpty = true;
        definition.dryFireCooldown = 0.16f;
        definition.dryFireKickPosition = new Vector3(0f, 0.001f, -0.012f);
        definition.dryFireKickEuler = new Vector3(1.2f, 0.25f, 0.35f);

        switch (slot)
        {
            case 1:
                definition.spreadBloomPerShot = 0.11f;
                definition.maxSpreadAngle = 2.2f;
                definition.spreadRecoverySpeed = 7f;
                definition.movementSpreadPenalty = 0.45f;
                break;
            case 2:
                definition.spreadBloomPerShot = 0.35f;
                definition.maxSpreadAngle = 5.4f;
                definition.spreadRecoverySpeed = 3.5f;
                definition.movementSpreadPenalty = 0.8f;
                break;
            case 3:
                definition.dryFireCooldown = 0.24f;
                definition.dryFireKickPosition = new Vector3(0f, 0.001f, -0.018f);
                definition.dryFireKickEuler = new Vector3(1.8f, 0.2f, 0.25f);
                definition.spreadBloomPerShot = 0.05f;
                definition.maxSpreadAngle = 0.7f;
                definition.spreadRecoverySpeed = 4f;
                definition.movementSpreadPenalty = 0.15f;
                break;
            default:
                definition.spreadBloomPerShot = 0.18f;
                definition.maxSpreadAngle = 1.4f;
                definition.spreadRecoverySpeed = 6f;
                definition.movementSpreadPenalty = 0.25f;
                break;
        }

        definition.maxSpreadAngle = Mathf.Max(definition.spreadAngle, definition.maxSpreadAngle);
    }

    private static void ApplyPreset(
        RetroWeaponDefinition definition,
        string displayName,
        RetroWeaponKind kind,
        RetroFireMode fireMode,
        int magazineSize,
        int startingReserveAmmo,
        int maxReserveAmmo,
        float reloadDuration,
        float fireInterval,
        float damage,
        float range,
        int pellets,
        float spreadAngle,
        float impactForce,
        float projectileSpeed,
        float explosionRadius,
        float explosionForce,
        float fuseTime,
        Vector3 localPosition,
        Vector3 localEuler,
        Vector3 recoilPosition,
        Vector3 recoilEuler,
        Color bodyColor,
        Color accentColor,
        float primitiveFlashScale,
        Vector3 spriteVisualPosition,
        Vector3 spriteVisualEuler,
        Vector2 spriteVisualSize,
        Vector3 spriteMuzzlePosition,
        Vector3 spriteMuzzleOffset,
        Sprite muzzleFlashSprite,
        Vector2 muzzleFlashSize,
        FirstPersonSpriteVolumeMapSet mapSet)
    {
        definition.displayName = displayName;
        definition.kind = kind;
        definition.fireMode = fireMode;
        definition.magazineSize = magazineSize;
        definition.startingReserveAmmo = startingReserveAmmo;
        definition.maxReserveAmmo = maxReserveAmmo;
        definition.reloadDuration = reloadDuration;
        definition.fireInterval = fireInterval;
        definition.damage = damage;
        definition.range = range;
        definition.pellets = pellets;
        definition.spreadAngle = spreadAngle;
        definition.impactForce = impactForce;
        definition.projectileSpeed = projectileSpeed;
        definition.explosionRadius = explosionRadius;
        definition.explosionForce = explosionForce;
        definition.fuseTime = fuseTime;
        definition.localPosition = localPosition;
        definition.localEuler = localEuler;
        definition.recoilPosition = recoilPosition;
        definition.recoilEuler = recoilEuler;
        definition.bodyColor = bodyColor;
        definition.accentColor = accentColor;
        definition.primitiveMuzzleFlashScale = primitiveFlashScale;
        definition.spriteMapSet = mapSet;
        definition.spriteVisualLocalPosition = spriteVisualPosition;
        definition.spriteVisualLocalEuler = spriteVisualEuler;
        definition.spriteVisualSize = spriteVisualSize;
        definition.spriteMuzzleLocalPosition = spriteMuzzlePosition;
        definition.spriteMuzzleLocalOffset = spriteMuzzleOffset;
        definition.muzzleFlashSprite = muzzleFlashSprite;
        definition.muzzleFlashFrames = new Sprite[0];
        definition.muzzleFlashFrameDuration = 0.018f;
        definition.muzzleFlashSpriteSize = muzzleFlashSize;
        definition.baseTintMultiplier = Color.white;
        definition.emissiveTintMultiplier = Color.white;
        definition.weaponBodyTint = 0f;
        definition.weaponAccentTint = 0.35f;
    }

    private static string ResolveSlotLabel(int slot)
    {
        return slot switch
        {
            0 => "Pistol",
            1 => "Rifle",
            2 => "Shotgun",
            3 => "Grenade Launcher",
            _ => $"Weapon {slot + 1}"
        };
    }

    private static string ResolveDefinitionPath(int slot)
    {
        if (slot >= 0 && slot < DefaultDefinitionPaths.Length)
        {
            return DefaultDefinitionPaths[slot];
        }

        return $"{DefaultDefinitionFolder}/Weapon{slot + 1}.asset";
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

    private static Sprite[] LoadSprites(string[] assetPaths)
    {
        if (assetPaths == null || assetPaths.Length == 0)
        {
            return new Sprite[0];
        }

        Sprite[] sprites = new Sprite[assetPaths.Length];
        for (int i = 0; i < assetPaths.Length; i++)
        {
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(assetPaths[i]);
        }

        return sprites;
    }

    private static bool HasAnySprite(Sprite[] sprites)
    {
        if (sprites == null || sprites.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnyMapSet(FirstPersonSpriteVolumeMapSet[] mapSets)
    {
        if (mapSets == null || mapSets.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < mapSets.Length; i++)
        {
            if (mapSets[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static void SaveAssets()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
