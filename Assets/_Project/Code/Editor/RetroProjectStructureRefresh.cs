using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RetroProjectStructureRefresh
{
    private const string SessionRefreshKey = "Ultraloud.ProjectStructureRefresh.HasRun";

    private static readonly string[] ProjectRoots =
    {
        "Assets/_Project",
        "Assets/_Project/Art",
        "Assets/_Project/Audio",
        "Assets/_Project/Code",
        "Assets/_Project/Content",
        "Assets/_Project/Content/Actors",
        "Assets/_Project/Content/Gameplay",
        "Assets/_Project/Content/Gameplay/Resources",
        "Assets/_Project/Content/World",
        "Assets/_Project/Data",
        "Assets/_Project/Documentation",
        "Assets/_Project/Scenes",
        "Assets/_Project/Settings"
    };

    static RetroProjectStructureRefresh()
    {
        if (SessionState.GetBool(SessionRefreshKey, false))
        {
            return;
        }

        SessionState.SetBool(SessionRefreshKey, true);
        EditorApplication.delayCall += ForceRefresh;
    }

    [MenuItem("Tools/Ultraloud/Project/Force Structure Refresh")]
    public static void ForceRefresh()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        foreach (string root in ProjectRoots)
        {
            if (AssetDatabase.IsValidFolder(root))
            {
                AssetDatabase.ImportAsset(root, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[Ultraloud] Project structure refresh complete.");
    }
}
