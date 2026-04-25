using UnityEngine;

[DisallowMultipleComponent]
public sealed class RetroGibOnDeath : MonoBehaviour
{
    private const int MaxDamageSamples = 32;

    [Header("References")]
    [SerializeField] private RetroDamageable damageable;
    [SerializeField] private RetroGoreProfile goreProfile;

    [Header("Trigger Override")]
    [SerializeField] private bool useProfileThresholds = true;
    [SerializeField, Min(0.02f)] private float damageWindow = 0.9f;
    [SerializeField, Min(0.02f)] private float clusterWindow = 0.18f;
    [SerializeField, Min(0f)] private float minimumRecentDamage = 72f;
    [SerializeField, Min(0f)] private float minimumClusterDamage = 48f;
    [SerializeField, Range(1, 32)] private int minimumClusterHits = 4;
    [SerializeField, Min(0f)] private float highSingleHitDamage = 80f;

    [Header("Spawn")]
    [SerializeField] private bool spawnAtRendererCenter = true;
    [SerializeField] private Vector3 localCenterOffset = new(0f, 0.35f, 0f);
    [SerializeField, Min(0f)] private float intensityMultiplier = 1f;
    [SerializeField] private bool debugDecision;

    private readonly DamageSample[] samples = new DamageSample[MaxDamageSamples];
    private int sampleCount;
    private int nextSampleIndex;
    private float lastGibTime = -999f;
    private Vector3 lastHitPoint;
    private Vector3 lastHitNormal = Vector3.up;
    private GameObject lastSource;
    private bool subscribed;

    private struct DamageSample
    {
        public float Time;
        public float Amount;
        public Vector3 Point;
        public Vector3 Normal;
        public GameObject Source;
    }

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
    }

    private void OnEnable()
    {
        AutoAssignReferences();
        Subscribe();
        sampleCount = 0;
        nextSampleIndex = 0;
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnValidate()
    {
        damageWindow = Mathf.Max(0.02f, damageWindow);
        clusterWindow = Mathf.Max(0.02f, clusterWindow);
        minimumRecentDamage = Mathf.Max(0f, minimumRecentDamage);
        minimumClusterDamage = Mathf.Max(0f, minimumClusterDamage);
        minimumClusterHits = Mathf.Max(1, minimumClusterHits);
        highSingleHitDamage = Mathf.Max(0f, highSingleHitDamage);
        intensityMultiplier = Mathf.Max(0f, intensityMultiplier);
        AutoAssignReferences();
    }

    public void SetProfile(RetroGoreProfile profile)
    {
        goreProfile = profile;
    }

    private void AutoAssignReferences()
    {
        if (damageable == null)
        {
            damageable = GetComponent<RetroDamageable>();
        }
    }

    private void Subscribe()
    {
        if (subscribed || damageable == null)
        {
            return;
        }

        damageable.Damaged += HandleDamaged;
        damageable.Died += HandleDied;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || damageable == null)
        {
            subscribed = false;
            return;
        }

        damageable.Damaged -= HandleDamaged;
        damageable.Died -= HandleDied;
        subscribed = false;
    }

    private void HandleDamaged(RetroDamageable source, float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (damage <= 0f)
        {
            return;
        }

        Vector3 safeNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.up;
        AddSample(new DamageSample
        {
            Time = Time.time,
            Amount = damage,
            Point = hitPoint,
            Normal = safeNormal,
            Source = lastSource
        });

        lastHitPoint = hitPoint;
        lastHitNormal = safeNormal;
    }

    private void HandleDied(RetroDamageable source)
    {
        lastSource = null;
        if (goreProfile == null)
        {
            return;
        }

        if (Time.time - lastGibTime < ResolveGibCooldown())
        {
            return;
        }

        if (!EvaluateGibDecision(out float intensity, out float recentDamage, out float bestClusterDamage, out int bestClusterHits, out float bestSingleHit))
        {
            if (debugDecision)
            {
                Debug.Log($"Gib skipped on {name}: recent={recentDamage:0.0}, bestCluster={bestClusterDamage:0.0}/{bestClusterHits}, bestSingle={bestSingleHit:0.0}.", this);
            }

            return;
        }

        Vector3 spawnCenter = ResolveSpawnCenter();
        Vector3 hitPoint = lastHitPoint == Vector3.zero ? spawnCenter : lastHitPoint;
        Vector3 hitNormal = lastHitNormal.sqrMagnitude > 0.0001f ? lastHitNormal : (spawnCenter - transform.position).normalized;
        if (hitNormal.sqrMagnitude < 0.0001f)
        {
            hitNormal = Vector3.up;
        }

        lastGibTime = Time.time;
        RetroGoreSystem.SpawnGoreBurst(goreProfile, spawnCenter, hitPoint, hitNormal, transform, intensity * intensityMultiplier, source != null ? source.gameObject : null);
    }

    private void AddSample(DamageSample sample)
    {
        samples[nextSampleIndex] = sample;
        nextSampleIndex = (nextSampleIndex + 1) % MaxDamageSamples;
        sampleCount = Mathf.Min(sampleCount + 1, MaxDamageSamples);
    }

    private bool EvaluateGibDecision(out float intensity, out float recentDamage, out float bestClusterDamage, out int bestClusterHits, out float bestSingleHit)
    {
        float now = Time.time;
        float resolvedDamageWindow = ResolveDamageWindow();
        float resolvedClusterWindow = ResolveClusterWindow();
        float minRecent = ResolveMinimumRecentDamage();
        float minClusterDamage = ResolveMinimumClusterDamage();
        int minClusterHits = ResolveMinimumClusterHits();
        float highSingle = ResolveHighSingleHitDamage();

        recentDamage = 0f;
        bestClusterDamage = 0f;
        bestClusterHits = 0;
        bestSingleHit = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            DamageSample sample = samples[i];
            if (now - sample.Time > resolvedDamageWindow)
            {
                continue;
            }

            recentDamage += sample.Amount;
            bestSingleHit = Mathf.Max(bestSingleHit, sample.Amount);
        }

        for (int i = 0; i < sampleCount; i++)
        {
            DamageSample anchor = samples[i];
            if (now - anchor.Time > resolvedDamageWindow)
            {
                continue;
            }

            float clusterDamage = 0f;
            int clusterHits = 0;
            for (int j = 0; j < sampleCount; j++)
            {
                DamageSample candidate = samples[j];
                if (now - candidate.Time > resolvedDamageWindow || Mathf.Abs(candidate.Time - anchor.Time) > resolvedClusterWindow)
                {
                    continue;
                }

                clusterDamage += candidate.Amount;
                clusterHits++;
            }

            if (clusterDamage > bestClusterDamage)
            {
                bestClusterDamage = clusterDamage;
                bestClusterHits = clusterHits;
            }
            else if (Mathf.Approximately(clusterDamage, bestClusterDamage))
            {
                bestClusterHits = Mathf.Max(bestClusterHits, clusterHits);
            }
        }

        bool highSingleHit = highSingle > 0f && bestSingleHit >= highSingle;
        bool burstDamage = recentDamage >= minRecent
            && (bestClusterDamage >= minClusterDamage || bestClusterHits >= minClusterHits);
        float thresholdBase = Mathf.Max(1f, Mathf.Min(minRecent, highSingle > 0f ? highSingle : minRecent));
        intensity = Mathf.Clamp(recentDamage / thresholdBase, 0.75f, 2.1f);
        return highSingleHit || burstDamage;
    }

    private Vector3 ResolveSpawnCenter()
    {
        Vector3 center = transform.TransformPoint(localCenterOffset);
        if (!spawnAtRendererCenter)
        {
            return center;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds ? bounds.center + transform.TransformVector(localCenterOffset) : center;
    }

    private float ResolveDamageWindow()
    {
        return useProfileThresholds && goreProfile != null ? goreProfile.DamageWindow : damageWindow;
    }

    private float ResolveClusterWindow()
    {
        return useProfileThresholds && goreProfile != null ? goreProfile.ClusterWindow : clusterWindow;
    }

    private float ResolveMinimumRecentDamage()
    {
        return useProfileThresholds && goreProfile != null ? goreProfile.MinimumRecentDamage : minimumRecentDamage;
    }

    private float ResolveMinimumClusterDamage()
    {
        return useProfileThresholds && goreProfile != null ? goreProfile.MinimumClusterDamage : minimumClusterDamage;
    }

    private int ResolveMinimumClusterHits()
    {
        return useProfileThresholds && goreProfile != null ? goreProfile.MinimumClusterHits : minimumClusterHits;
    }

    private float ResolveHighSingleHitDamage()
    {
        return useProfileThresholds && goreProfile != null ? goreProfile.HighSingleHitDamage : highSingleHitDamage;
    }

    private float ResolveGibCooldown()
    {
        return goreProfile != null ? goreProfile.GibCooldown : 0f;
    }
}
