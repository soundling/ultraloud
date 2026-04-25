using UnityEngine;

public sealed class RetroPetInteractable : RetroInteractableBehaviour
{
    [Header("Pet")]
    [SerializeField] private RetroAudioCue petCue;
    [SerializeField] private bool playGeneratedSnort = true;
    [SerializeField] private bool calmNpcOnPet = true;
    [SerializeField, Min(0f)] private float cooldown = 0.45f;
    [SerializeField] private string petMessage = "The pig snorts happily.";

    private static AudioClip generatedSnortClip;
    private float nextPetTime = -999f;

    protected override string DefaultInteractionVerb => "Pet";

    public override bool CanInteract(in RetroInteractionContext context)
    {
        return base.CanInteract(context) && Time.time >= nextPetTime;
    }

    protected override void InteractInternal(in RetroInteractionContext context)
    {
        nextPetTime = Time.time + cooldown;
        if (!string.IsNullOrWhiteSpace(petMessage))
        {
            context.Interactor?.ShowStatusMessage(petMessage, 1.35f);
        }

        if (petCue != null)
        {
            RetroGameContext.Audio.PlayCue(petCue, transform.position);
        }
        else if (playGeneratedSnort)
        {
            RetroGameContext.Audio.PlayClip(GetGeneratedSnortClip(), transform.position, BuildSnortPlayback());
        }

        if (calmNpcOnPet && TryGetComponent(out RetroNpcAgent npcAgent))
        {
            npcAgent.Calm();
        }
    }

    private static RetroAudioPlayback BuildSnortPlayback()
    {
        RetroAudioPlayback playback = RetroAudioPlayback.Default;
        playback.Volume = 0.72f;
        playback.Pitch = Random.Range(0.9f, 1.12f);
        playback.SpatialBlend = 1f;
        playback.MinDistance = 1.2f;
        playback.MaxDistance = 18f;
        playback.Priority = 96;
        return playback;
    }

    private static AudioClip GetGeneratedSnortClip()
    {
        if (generatedSnortClip != null)
        {
            return generatedSnortClip;
        }

        const int sampleRate = 22050;
        const float duration = 0.42f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        uint noise = 0x6d2b79f5u;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float normalized = t / duration;
            float envelope = Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI);
            float wobble = Mathf.Sin(t * 38f) * 0.18f;
            float frequency = Mathf.Lerp(190f, 82f, normalized) + wobble * 65f;
            noise ^= noise << 13;
            noise ^= noise >> 17;
            noise ^= noise << 5;
            float grit = ((noise & 0xffff) / 32768f - 1f) * 0.28f;
            samples[i] = (Mathf.Sin(t * frequency * Mathf.PI * 2f) * 0.62f + grit) * envelope * 0.85f;
        }

        generatedSnortClip = AudioClip.Create("Generated Pig Snort", sampleCount, 1, sampleRate, false);
        generatedSnortClip.SetData(samples, 0);
        return generatedSnortClip;
    }
}
