using System;
using System.Collections.Generic;
using UnityEngine;

public enum DirectionalSpriteSymmetry
{
    Unique = 0,
    MirrorToOppositeSide = 1
}

[CreateAssetMenu(fileName = "DirectionalSpriteDefinition", menuName = "Ultraloud/Directional Sprites/Definition")]
public sealed class DirectionalSpriteDefinition : ScriptableObject
{
    public string defaultClipId = "Idle";
    public List<DirectionalSpriteClip> clips = new();

    public DirectionalSpriteClip GetDefaultClip()
    {
        if (clips == null || clips.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(defaultClipId))
        {
            for (int i = 0; i < clips.Count; i++)
            {
                DirectionalSpriteClip clip = clips[i];
                if (clip == null)
                {
                    continue;
                }

                if (string.Equals(clip.clipId, defaultClipId, StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }
            }
        }

        return clips[0];
    }

    public bool TryGetClip(string clipId, out DirectionalSpriteClip clip)
    {
        clip = FindClip(clipId);
        if (clip != null)
        {
            return true;
        }

        clip = GetDefaultClip();
        return clip != null;
    }

    public DirectionalSpriteClip FindClip(string clipId)
    {
        if (clips == null || clips.Count == 0 || string.IsNullOrWhiteSpace(clipId))
        {
            return null;
        }

        for (int i = 0; i < clips.Count; i++)
        {
            DirectionalSpriteClip clip = clips[i];
            if (clip == null)
            {
                continue;
            }

            if (string.Equals(clip.clipId, clipId, StringComparison.OrdinalIgnoreCase))
            {
                return clip;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(defaultClipId) && clips != null && clips.Count > 0 && clips[0] != null)
        {
            defaultClipId = clips[0].clipId;
        }
    }
}

[Serializable]
public sealed class DirectionalSpriteClip
{
    public string clipId = "Idle";
    public bool loop = true;
    [Min(0f)] public float framesPerSecond = 8f;
    public List<DirectionalSpriteAngleSet> angles = new();

    public int GetMaxFrameCount()
    {
        if (angles == null || angles.Count == 0)
        {
            return 0;
        }

        int maxFrameCount = 0;
        for (int i = 0; i < angles.Count; i++)
        {
            DirectionalSpriteAngleSet angle = angles[i];
            if (angle == null)
            {
                continue;
            }

            int frameCount = angle.FrameCount;
            if (frameCount > maxFrameCount)
            {
                maxFrameCount = frameCount;
            }
        }

        return maxFrameCount;
    }
}

[Serializable]
public sealed class DirectionalSpriteAngleSet
{
    public string label = "Front";
    [Range(-180f, 180f)] public float yawDegrees = 0f;
    public DirectionalSpriteSymmetry symmetry = DirectionalSpriteSymmetry.Unique;
    public bool flipX;
    public List<Sprite> frames = new();

    public int FrameCount => frames != null ? frames.Count : 0;

    public Sprite GetFrame(int index)
    {
        if (frames == null || frames.Count == 0)
        {
            return null;
        }

        if (index < 0)
        {
            index = 0;
        }
        else if (index >= frames.Count)
        {
            index = frames.Count - 1;
        }

        return frames[index];
    }
}
