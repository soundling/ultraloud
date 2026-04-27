using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

internal static class RetroSceneLightCache
{
    private const double EditorRefreshInterval = 0.1;

    private static readonly List<Light> activeLights = new List<Light>(32);
    private static int refreshedFrame = -1;
#if UNITY_EDITOR
    private static double nextEditorRefreshTime = double.NegativeInfinity;
#endif

    public static IReadOnlyList<Light> ActiveLights
    {
        get
        {
            RefreshIfNeeded();
            return activeLights;
        }
    }

    private static void RefreshIfNeeded()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextEditorRefreshTime)
            {
                return;
            }

            nextEditorRefreshTime = now + EditorRefreshInterval;
            Refresh();
            return;
        }
#endif

        if (refreshedFrame == Time.frameCount)
        {
            return;
        }

        refreshedFrame = Time.frameCount;
        Refresh();
    }

    private static void Refresh()
    {
        activeLights.Clear();
#if UNITY_2023_1_OR_NEWER
        Light[] sceneLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
#else
        Light[] sceneLights = Object.FindObjectsOfType<Light>();
#endif
        foreach (Light lightSource in sceneLights)
        {
            if (lightSource == null || !lightSource.isActiveAndEnabled || lightSource.intensity <= 0f)
            {
                continue;
            }

            activeLights.Add(lightSource);
        }
    }
}
