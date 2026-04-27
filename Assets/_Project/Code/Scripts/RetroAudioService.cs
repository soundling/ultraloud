using UnityEngine;

public struct RetroAudioPlayback
{
    public float Volume;
    public float Pitch;
    public float SpatialBlend;
    public float MinDistance;
    public float MaxDistance;
    public AudioRolloffMode RolloffMode;
    public int Priority;

    public static RetroAudioPlayback Default => new RetroAudioPlayback
    {
        Volume = 1f,
        Pitch = 1f,
        SpatialBlend = 1f,
        MinDistance = 1f,
        MaxDistance = 35f,
        RolloffMode = AudioRolloffMode.Logarithmic,
        Priority = 128
    };

    public static RetroAudioPlayback FromCue(RetroAudioCue cue)
    {
        if (cue == null)
        {
            return Default;
        }

        return new RetroAudioPlayback
        {
            Volume = cue.volume,
            Pitch = Random.Range(cue.pitchRange.x, cue.pitchRange.y),
            SpatialBlend = cue.spatialBlend,
            MinDistance = cue.minDistance,
            MaxDistance = cue.maxDistance,
            RolloffMode = cue.rolloffMode,
            Priority = cue.priority
        };
    }
}

[DisallowMultipleComponent]
public sealed class RetroAudioService : MonoBehaviour
{
    private RetroComponentPool<PooledAudioEmitter> emitterPool;

    public void PlayCue(RetroAudioCue cue, Vector3 position)
    {
        if (cue == null || !cue.TryPickClip(out AudioClip clip))
        {
            return;
        }

        PlayClip(clip, position, RetroAudioPlayback.FromCue(cue));
    }

    public void PlayClip(AudioClip clip, Vector3 position)
    {
        PlayClip(clip, position, RetroAudioPlayback.Default);
    }

    public void PlayClip(AudioClip clip, Vector3 position, RetroAudioPlayback playback)
    {
        if (clip == null)
        {
            return;
        }

        EnsurePool();
        PooledAudioEmitter emitter = emitterPool?.Rent(position, Quaternion.identity);
        if (emitter == null)
        {
            return;
        }

        emitter.Play(clip, playback);
    }

    private void EnsurePool()
    {
        if (emitterPool != null && emitterPool.IsValid)
        {
            return;
        }

        emitterPool = RetroGameContext.Pools.GetOrCreateComponentPool(
            "RetroAudioService.Emitters",
            CreateEmitter,
            new RetroPoolSettings(prewarmCount: 16, maxInactiveCount: 96));
    }

    private static PooledAudioEmitter CreateEmitter(Transform parent)
    {
        GameObject emitterObject = new GameObject("PooledAudioEmitter");
        emitterObject.transform.SetParent(parent, false);
        AudioSource source = emitterObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        PooledAudioEmitter emitter = emitterObject.AddComponent<PooledAudioEmitter>();
        emitter.Configure(source);
        return emitter;
    }

    private sealed class PooledAudioEmitter : MonoBehaviour, IRetroPoolLifecycle
    {
        private RetroPooledObject pooledObject;
        private AudioSource source;

        public void Configure(AudioSource audioSource)
        {
            source = audioSource;
        }

        public void Play(AudioClip clip, RetroAudioPlayback playback)
        {
            if (source == null)
            {
                source = GetComponent<AudioSource>();
            }

            if (source == null || clip == null)
            {
                pooledObject?.ReturnToPool();
                return;
            }

            source.clip = clip;
            source.volume = Mathf.Clamp01(playback.Volume);
            source.pitch = Mathf.Max(0.01f, playback.Pitch);
            source.spatialBlend = Mathf.Clamp01(playback.SpatialBlend);
            source.minDistance = Mathf.Max(0f, playback.MinDistance);
            source.maxDistance = Mathf.Max(0.01f, playback.MaxDistance);
            source.rolloffMode = playback.RolloffMode;
            source.priority = Mathf.Clamp(playback.Priority, 0, 256);
            source.Play();
        }

        public void OnPoolRent(RetroPooledObject pooledObject)
        {
            this.pooledObject = pooledObject;
            if (source == null)
            {
                source = GetComponent<AudioSource>();
            }
        }

        public void OnPoolReturn(RetroPooledObject pooledObject)
        {
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }
        }

        public void OnPoolDestroy(RetroPooledObject pooledObject)
        {
            this.pooledObject = null;
        }

        private void Update()
        {
            if (pooledObject != null && pooledObject.IsRented && source != null && !source.isPlaying)
            {
                pooledObject.ReturnToPool();
            }
        }
    }
}
