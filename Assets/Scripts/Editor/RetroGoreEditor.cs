using UnityEditor;
using UnityEngine;

public sealed class RetroGoreEditorWindow : EditorWindow
{
    private const string DefaultPigGoreProfilePath = "Assets/Sprites/Effects/Gore/PigGoreProfile.asset";

    private Vector2 scroll;

    [MenuItem("Tools/Ultraloud/VFX/Gore")]
    public static void Open()
    {
        RetroGoreEditorWindow window = GetWindow<RetroGoreEditorWindow>("Gore");
        window.minSize = new Vector2(420f, 220f);
    }

    [MenuItem("GameObject/Ultraloud/VFX/Add Gore Gibbing To Selected", false, 30)]
    public static void AddGoreToSelected()
    {
        RetroGoreProfile profile = LoadDefaultProfile();
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (selected == null)
            {
                continue;
            }

            RetroGibOnDeath gib = selected.GetComponent<RetroGibOnDeath>();
            if (gib == null)
            {
                Undo.AddComponent<RetroGibOnDeath>(selected);
                gib = selected.GetComponent<RetroGibOnDeath>();
            }

            AssignProfile(gib, profile);
            EditorUtility.SetDirty(selected);
        }
    }

    private void OnGUI()
    {
        using (EditorGUILayout.ScrollViewScope scope = new(scroll))
        {
            scroll = scope.scrollPosition;
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Gore Gibbing", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Adds burst-damage gated gibbing. Pistol-style single hits stay as normal blood; dense pellet clusters or very high damage trigger the full hybrid sprite/mesh burst.", MessageType.Info);

            RetroGoreProfile profile = LoadDefaultProfile();
            EditorGUILayout.ObjectField("Default Profile", profile, typeof(RetroGoreProfile), false);
            if (GUILayout.Button("Add / Assign To Selected", GUILayout.Height(32f)))
            {
                AddGoreToSelected();
            }
        }
    }

    public static RetroGoreProfile LoadDefaultProfile()
    {
        return AssetDatabase.LoadAssetAtPath<RetroGoreProfile>(DefaultPigGoreProfilePath);
    }

    public static void AssignProfile(RetroGibOnDeath gib, RetroGoreProfile profile)
    {
        if (gib == null)
        {
            return;
        }

        SerializedObject serializedGib = new(gib);
        serializedGib.Update();
        SerializedProperty profileProperty = serializedGib.FindProperty("goreProfile");
        if (profileProperty != null)
        {
            profileProperty.objectReferenceValue = profile;
        }

        SerializedProperty useProfileProperty = serializedGib.FindProperty("useProfileThresholds");
        if (useProfileProperty != null)
        {
            useProfileProperty.boolValue = true;
        }

        serializedGib.ApplyModifiedProperties();
        EditorUtility.SetDirty(gib);
    }
}

[CustomEditor(typeof(RetroGibOnDeath))]
public sealed class RetroGibOnDeathEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Assign Default Pig Gore Profile"))
        {
            RetroGoreProfile profile = RetroGoreEditorWindow.LoadDefaultProfile();
            foreach (Object targetObject in targets)
            {
                RetroGoreEditorWindow.AssignProfile((RetroGibOnDeath)targetObject, profile);
            }
        }
    }
}
