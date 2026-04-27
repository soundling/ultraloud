using UnityEngine;

[DisallowMultipleComponent]
public sealed class RetroB2BomberActor : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] animationFrames = new Sprite[0];
    [SerializeField, Min(0.1f)] private float animationFramesPerSecond = 7f;
    [SerializeField, Min(0.01f)] private float spriteScale = 11.5f;
    [SerializeField] private Vector3 visualOffset = Vector3.zero;

    [Header("Bombs")]
    [SerializeField] private RetroB2BombProjectile bombPrefab;
    [SerializeField] private Vector3 bombSpawnOffset = new Vector3(0f, -1.25f, 0f);

    [Header("Audio")]
    [SerializeField] private bool playEngineRumble = true;
    [SerializeField, Range(0f, 1f)] private float engineVolume = 0.38f;
    [SerializeField, Min(1f)] private float engineMaxDistance = 165f;

    private Vector3 startPosition;
    private Vector3 endPosition;
    private Vector3 flightDirection = Vector3.forward;
    private Vector3[] impactPoints = new Vector3[0];
    private float[] dropTimes = new float[0];
    private LayerMask groundMask = ~0;
    private GameObject source;
    private AudioSource engineSource;
    private float duration = 1f;
    private float age;
    private int nextDropIndex;
    private bool initialized;

    private static AudioClip sharedEngineClip;

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
    }

    private void OnValidate()
    {
        AutoAssignReferences();
        animationFramesPerSecond = Mathf.Max(0.1f, animationFramesPerSecond);
        spriteScale = Mathf.Max(0.01f, spriteScale);
        engineMaxDistance = Mathf.Max(1f, engineMaxDistance);
        ApplyVisualScale();
    }

    private void Update()
    {
        if (!initialized)
        {
            AnimateSprite(Time.time);
            return;
        }

        age += Time.deltaTime;
        float normalizedAge = Mathf.Clamp01(age / duration);
        transform.position = Vector3.Lerp(startPosition, endPosition, normalizedAge);
        AnimateSprite(age);

        while (nextDropIndex < dropTimes.Length && normalizedAge >= dropTimes[nextDropIndex])
        {
            DropBomb(nextDropIndex);
            nextDropIndex++;
        }

        if (normalizedAge >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void LateUpdate()
    {
        FaceCameraAndPointAlongPath();
    }

    public void Initialize(
        Vector3 start,
        Vector3 end,
        float passDuration,
        Vector3[] plannedImpactPoints,
        float[] plannedDropTimes,
        LayerMask raycastMask,
        GameObject damageSource)
    {
        AutoAssignReferences();
        startPosition = start;
        endPosition = end;
        duration = Mathf.Max(0.1f, passDuration);
        age = 0f;
        nextDropIndex = 0;
        impactPoints = plannedImpactPoints ?? new Vector3[0];
        dropTimes = plannedDropTimes ?? new float[0];
        groundMask = raycastMask;
        source = damageSource != null ? damageSource : gameObject;
        transform.position = startPosition;
        Vector3 delta = endPosition - startPosition;
        flightDirection = Vector3.ProjectOnPlane(delta, Vector3.up);
        if (flightDirection.sqrMagnitude < 0.0001f)
        {
            flightDirection = transform.forward;
        }

        flightDirection.Normalize();
        initialized = true;
        StartEngineRumble();
    }

    private void DropBomb(int index)
    {
        if (bombPrefab == null || index < 0 || index >= impactPoints.Length)
        {
            return;
        }

        Vector3 spawnPosition = transform.position + bombSpawnOffset;
        RetroB2BombProjectile bomb = Instantiate(bombPrefab, spawnPosition, Quaternion.identity);
        bomb.name = "B2 Heavy Bomb";
        bomb.Initialize(impactPoints[index], groundMask, source != null ? source : gameObject);
    }

    private void AutoAssignReferences()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        ApplyVisualScale();
    }

    private void ApplyVisualScale()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.transform.localPosition = visualOffset;
        spriteRenderer.transform.localScale = Vector3.one * spriteScale;
        if (animationFrames != null && animationFrames.Length > 0 && spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = animationFrames[0];
        }
    }

    private void AnimateSprite(float time)
    {
        if (spriteRenderer == null || animationFrames == null || animationFrames.Length == 0)
        {
            return;
        }

        int frame = Mathf.FloorToInt(time * animationFramesPerSecond) % animationFrames.Length;
        if (frame < 0)
        {
            frame = 0;
        }

        spriteRenderer.sprite = animationFrames[frame];
    }

    private void FaceCameraAndPointAlongPath()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Camera camera = Camera.main;
        Transform visual = spriteRenderer.transform;
        if (camera == null)
        {
            visual.rotation = Quaternion.LookRotation(-flightDirection, Vector3.up);
            return;
        }

        Vector3 toCamera = camera.transform.position - visual.position;
        if (toCamera.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion baseRotation = Quaternion.LookRotation(-toCamera.normalized, camera.transform.up);
        float roll = ResolveScreenRoll(camera);
        visual.rotation = baseRotation * Quaternion.Euler(0f, 0f, roll);
    }

    private float ResolveScreenRoll(Camera camera)
    {
        Vector3 screenPosition = camera.WorldToScreenPoint(transform.position);
        Vector3 screenAhead = camera.WorldToScreenPoint(transform.position + flightDirection * 8f);
        Vector2 delta = new Vector2(screenAhead.x - screenPosition.x, screenAhead.y - screenPosition.y);
        if (delta.sqrMagnitude < 1f)
        {
            return 0f;
        }

        return Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;
    }

    private void StartEngineRumble()
    {
        if (!playEngineRumble || engineVolume <= 0f)
        {
            return;
        }

        if (engineSource == null)
        {
            engineSource = gameObject.AddComponent<AudioSource>();
        }

        engineSource.clip = GetEngineClip();
        engineSource.loop = true;
        engineSource.playOnAwake = false;
        engineSource.spatialBlend = 1f;
        engineSource.volume = engineVolume;
        engineSource.pitch = Random.Range(0.86f, 1.04f);
        engineSource.minDistance = 12f;
        engineSource.maxDistance = engineMaxDistance;
        engineSource.rolloffMode = AudioRolloffMode.Logarithmic;
        engineSource.Play();
    }

    private static AudioClip GetEngineClip()
    {
        if (sharedEngineClip != null)
        {
            return sharedEngineClip;
        }

        const int sampleRate = 22050;
        const float lengthSeconds = 1.35f;
        int sampleCount = Mathf.CeilToInt(sampleRate * lengthSeconds);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float rumble = Mathf.Sin(t * Mathf.PI * 2f * 54f) * 0.34f;
            rumble += Mathf.Sin(t * Mathf.PI * 2f * 83f) * 0.21f;
            rumble += Mathf.Sin(t * Mathf.PI * 2f * 126f) * 0.11f;
            float tremolo = 0.82f + Mathf.Sin(t * Mathf.PI * 2f * 5.5f) * 0.18f;
            samples[i] = Mathf.Clamp(rumble * tremolo, -0.7f, 0.7f);
        }

        sharedEngineClip = AudioClip.Create("B2BomberEngineRumble", sampleCount, 1, sampleRate, false);
        sharedEngineClip.SetData(samples, 0);
        return sharedEngineClip;
    }
}
