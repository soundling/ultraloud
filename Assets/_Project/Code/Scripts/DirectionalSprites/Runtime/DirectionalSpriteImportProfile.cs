using System;
using System.Collections.Generic;
using UnityEngine;

public enum DirectionalSheetOrigin
{
    TopLeft = 0,
    BottomLeft = 1
}

[CreateAssetMenu(fileName = "DirectionalSpriteImportProfile", menuName = "Ultraloud/Directional Sprites/Import Profile")]
public sealed class DirectionalSpriteImportProfile : ScriptableObject
{
    [Header("Sheet Layout")]
    public Vector2Int cellSize = new(128, 128);
    public Vector2Int cellPadding = Vector2Int.zero;
    public Vector2Int sheetMargin = Vector2Int.zero;
    public DirectionalSheetOrigin sheetOrigin = DirectionalSheetOrigin.TopLeft;

    [Header("Generated Sprites")]
    [Min(1)] public int pixelsPerUnit = 100;
    public FilterMode filterMode = FilterMode.Point;
    public Vector2 pivot = new(0.5f, 0f);
    public string outputFolderSuffix = "_Directional";

    [Header("Clip Defaults")]
    [Min(0f)] public float defaultFramesPerSecond = 8f;
    public bool overwriteGeneratedFiles = true;
    public List<DirectionalSpriteImportClipTemplate> clips = new();
}

[Serializable]
public sealed class DirectionalSpriteImportClipTemplate
{
    public string clipId = "Idle";
    public bool loop = true;
    [Min(0f)] public float framesPerSecond = 8f;
    public List<DirectionalSpriteImportAngleTemplate> angles = new();
}

[Serializable]
public sealed class DirectionalSpriteImportAngleTemplate
{
    public string label = "Front";
    [Range(-180f, 180f)] public float yawDegrees = 0f;
    public DirectionalSpriteSymmetry symmetry = DirectionalSpriteSymmetry.Unique;
    public bool flipX;
    public Vector2Int startCell = Vector2Int.zero;
    public Vector2Int frameStep = Vector2Int.right;
    [Min(1)] public int frameCount = 1;
}
