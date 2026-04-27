using UnityEngine;

[CreateAssetMenu(menuName = "Ultraloud/Audio/Audio Cue", fileName = "RetroAudioCue")]
public sealed class RetroAudioCue : ScriptableObject
{
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    public Vector2 pitchRange = Vector2.one;
    [Range(0f, 1f)] public float spatialBlend = 1f;
    [Min(0f)] public float minDistance = 1f;
    [Min(0.01f)] public float maxDistance = 35f;
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    public int priority = 128;

    public bool TryPickClip(out AudioClip clip)
    {
        clip = null;
        if (clips == null || clips.Length == 0)
        {
            return false;
        }

        for (int attempt = 0; attempt < clips.Length; attempt++)
        {
            AudioClip candidate = clips[Random.Range(0, clips.Length)];
            if (candidate != null)
            {
                clip = candidate;
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        pitchRange.x = Mathf.Max(0.01f, pitchRange.x);
        pitchRange.y = Mathf.Max(pitchRange.x, pitchRange.y);
        minDistance = Mathf.Max(0f, minDistance);
        maxDistance = Mathf.Max(0.01f, maxDistance);
    }
}
