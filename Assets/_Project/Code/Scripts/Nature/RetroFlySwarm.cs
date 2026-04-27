using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RetroFlySwarm : MonoBehaviour
{
    private const string RuntimeRootName = "__FlySwarmRuntime";
    private const string HomeAnchorName = "__FlySwarmHome";
    private static int nextRuntimeId = 1;

    [Header("Prefab")]
    [SerializeField] private GameObject agentPrefab;
    [SerializeField, Min(1)] private int count = 50;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool clearOnDisable = true;

    [Header("Group")]
    [SerializeField] private string groupId = "PestFlies";

    [Header("Spawn")]
    [SerializeField, Min(0.1f)] private float spawnRadius = 3.2f;
    [SerializeField] private Vector2 speedRange = new(6.5f, 11.5f);
    [SerializeField] private Vector2 scaleRange = new(0.74f, 1.18f);
    [SerializeField] private int seed = 88431;

    [Header("Targeting")]
    [SerializeField] private bool preferTaggedTarget = true;
    [SerializeField] private string preferredTargetTag = "Player";
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField, Min(0.1f)] private float targetSearchRadius = 34f;
    [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.45f;

    [Header("Cloud Motion")]
    [SerializeField, Min(0f)] private float cloudMoveSpeed = 8.5f;
    [SerializeField, Min(0f)] private float cloudAcceleration = 18f;
    [SerializeField, Min(0f)] private float targetHoverHeight = 1.35f;
    [SerializeField, Min(0f)] private float attackOrbitRadius = 1.55f;
    [SerializeField, Min(0f)] private float orbitSpeed = 2.4f;
    [SerializeField, Min(0f)] private float idleDriftRadius = 2.5f;
    [SerializeField, Min(0f)] private float idleDriftSpeed = 0.55f;

    [Header("Bite")]
    [SerializeField, Min(0.01f)] private float biteRadius = 0.42f;
    [SerializeField, Min(0f)] private float biteDamage = 1.15f;
    [SerializeField, Min(0.01f)] private float biteInterval = 0.34f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private readonly List<RetroFlyPestAgent> agents = new();
    private Transform runtimeRoot;
    private Transform homeAnchor;
    private RetroDamageable currentTarget;
    private Vector3 spawnHome;
    private Vector3 cloudVelocity;
    private float nextTargetRefreshTime;
    private string runtimeGroupId;
    private int runtimeId;

    public RetroDamageable CurrentTarget => currentTarget;

    private void Awake()
    {
        runtimeId = nextRuntimeId++;
        spawnHome = transform.position;
        EnsureHomeAnchor();
    }

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnNow();
        }
    }

    private void OnDisable()
    {
        if (clearOnDisable)
        {
            ClearSpawned();
        }
    }

    private void OnValidate()
    {
        count = Mathf.Max(1, count);
        spawnRadius = Mathf.Max(0.1f, spawnRadius);
        if (speedRange.y < speedRange.x)
        {
            speedRange.y = speedRange.x;
        }

        if (scaleRange.y < scaleRange.x)
        {
            scaleRange.y = scaleRange.x;
        }

        targetSearchRadius = Mathf.Max(0.1f, targetSearchRadius);
        targetRefreshInterval = Mathf.Max(0.05f, targetRefreshInterval);
        cloudMoveSpeed = Mathf.Max(0f, cloudMoveSpeed);
        cloudAcceleration = Mathf.Max(0f, cloudAcceleration);
        biteRadius = Mathf.Max(0.01f, biteRadius);
        biteDamage = Mathf.Max(0f, biteDamage);
        biteInterval = Mathf.Max(0.01f, biteInterval);
    }

    private void Update()
    {
        EnsureHomeAnchor();
        RefreshTargetIfNeeded();
        MoveCloud(Time.deltaTime);
    }

    [ContextMenu("Spawn Fly Swarm Now")]
    public void SpawnNow()
    {
        if (agentPrefab == null)
        {
            Debug.LogWarning("RetroFlySwarm needs an agent prefab before it can spawn.", this);
            return;
        }

        ClearSpawned();
        EnsureHomeAnchor();
        EnsureRuntimeRoot();

        runtimeGroupId = ResolveRuntimeGroupId();
        System.Random random = new(seed);
        Vector3 home = homeAnchor.position;
        for (int i = 0; i < count; i++)
        {
            Vector3 position = home + SampleSphere(random, spawnRadius);
            Quaternion rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            GameObject instance = Instantiate(agentPrefab, position, rotation, runtimeRoot);
            instance.name = $"{agentPrefab.name}_{i:00}";
            instance.transform.localScale = Vector3.one * Mathf.Lerp(scaleRange.x, scaleRange.y, (float)random.NextDouble());

            RetroFlyPestAgent pestAgent = instance.GetComponent<RetroFlyPestAgent>();
            if (pestAgent == null)
            {
                pestAgent = instance.AddComponent<RetroFlyPestAgent>();
            }

            Vector3 velocity = SampleSphere(random, 1f);
            velocity.y *= 0.45f;
            if (velocity.sqrMagnitude < 0.001f)
            {
                velocity = transform.forward;
            }

            float speed = Mathf.Lerp(speedRange.x, speedRange.y, (float)random.NextDouble());
            pestAgent.ConfigureBite(biteRadius, biteDamage, biteInterval, targetMask);
            pestAgent.ConfigureSwarm(this, currentTarget, homeAnchor, runtimeGroupId, velocity.normalized * speed);
            agents.Add(pestAgent);
        }
    }

    [ContextMenu("Clear Fly Swarm")]
    public void ClearSpawned()
    {
        for (int i = agents.Count - 1; i >= 0; i--)
        {
            if (agents[i] != null)
            {
                DestroyUnityObject(agents[i].gameObject);
            }
        }

        agents.Clear();

        Transform existingRoot = transform.Find(RuntimeRootName);
        if (existingRoot != null)
        {
            DestroyUnityObject(existingRoot.gameObject);
        }

        runtimeRoot = null;
    }

    public bool IsValidTarget(RetroDamageable candidate)
    {
        if (candidate == null || candidate.IsDead)
        {
            return false;
        }

        Transform candidateTransform = candidate.transform;
        if (candidateTransform == transform || candidateTransform.IsChildOf(transform))
        {
            return false;
        }

        if (((1 << candidate.gameObject.layer) & targetMask.value) == 0)
        {
            return false;
        }

        if (candidate.GetComponentInParent<RetroFlyPestAgent>() != null)
        {
            return false;
        }

        return Vector3.Distance(candidate.transform.position, homeAnchor != null ? homeAnchor.position : transform.position) <= targetSearchRadius;
    }

    private void RefreshTargetIfNeeded()
    {
        if (Time.time < nextTargetRefreshTime && IsValidTarget(currentTarget))
        {
            return;
        }

        nextTargetRefreshTime = Time.time + targetRefreshInterval;
        currentTarget = ResolveTarget();
        for (int i = agents.Count - 1; i >= 0; i--)
        {
            RetroFlyPestAgent agent = agents[i];
            if (agent == null)
            {
                agents.RemoveAt(i);
                continue;
            }

            agent.SetTarget(currentTarget);
        }
    }

    private RetroDamageable ResolveTarget()
    {
        RetroDamageable taggedTarget = preferTaggedTarget ? ResolveTaggedTarget() : null;
        if (taggedTarget != null)
        {
            return taggedTarget;
        }

        RetroDamageable best = null;
        float bestDistanceSqr = float.MaxValue;
        Vector3 origin = homeAnchor != null ? homeAnchor.position : transform.position;
        RetroDamageable[] damageables = FindObjectsByType<RetroDamageable>(FindObjectsInactive.Exclude);
        for (int i = 0; i < damageables.Length; i++)
        {
            RetroDamageable candidate = damageables[i];
            if (!IsValidTarget(candidate))
            {
                continue;
            }

            float distanceSqr = (candidate.transform.position - origin).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            best = candidate;
            bestDistanceSqr = distanceSqr;
        }

        return best;
    }

    private RetroDamageable ResolveTaggedTarget()
    {
        if (string.IsNullOrWhiteSpace(preferredTargetTag))
        {
            return null;
        }

        GameObject[] taggedObjects;
        try
        {
            taggedObjects = GameObject.FindGameObjectsWithTag(preferredTargetTag);
        }
        catch (UnityException)
        {
            return null;
        }

        RetroDamageable best = null;
        float bestDistanceSqr = float.MaxValue;
        Vector3 origin = homeAnchor != null ? homeAnchor.position : transform.position;
        for (int i = 0; i < taggedObjects.Length; i++)
        {
            GameObject taggedObject = taggedObjects[i];
            if (taggedObject == null)
            {
                continue;
            }

            RetroDamageable candidate = taggedObject.GetComponentInParent<RetroDamageable>();
            if (candidate == null)
            {
                candidate = taggedObject.GetComponentInChildren<RetroDamageable>();
            }

            if (!IsValidTarget(candidate))
            {
                continue;
            }

            float distanceSqr = (candidate.transform.position - origin).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            best = candidate;
            bestDistanceSqr = distanceSqr;
        }

        return best;
    }

    private void MoveCloud(float deltaTime)
    {
        if (homeAnchor == null || deltaTime <= 0f)
        {
            return;
        }

        Vector3 desired = currentTarget != null && IsValidTarget(currentTarget)
            ? ResolveAttackAnchor(currentTarget)
            : ResolveIdleAnchor();

        Vector3 toDesired = desired - homeAnchor.position;
        Vector3 desiredVelocity = toDesired.sqrMagnitude > 0.0001f
            ? Vector3.ClampMagnitude(toDesired / Mathf.Max(deltaTime, 0.0001f), cloudMoveSpeed)
            : Vector3.zero;
        cloudVelocity = Vector3.MoveTowards(cloudVelocity, desiredVelocity, cloudAcceleration * deltaTime);
        homeAnchor.position += cloudVelocity * deltaTime;
    }

    private Vector3 ResolveAttackAnchor(RetroDamageable target)
    {
        Collider targetCollider = target.GetComponentInChildren<Collider>();
        Vector3 center = targetCollider != null ? targetCollider.bounds.center : target.transform.position;
        float orbitTime = Time.time * orbitSpeed + seed * 0.013f;
        Vector3 orbit = new(Mathf.Cos(orbitTime), 0.28f * Mathf.Sin(orbitTime * 1.37f), Mathf.Sin(orbitTime));
        return center + Vector3.up * targetHoverHeight + orbit * attackOrbitRadius;
    }

    private Vector3 ResolveIdleAnchor()
    {
        float t = Time.time * idleDriftSpeed + seed * 0.017f;
        Vector3 drift = new Vector3(Mathf.Cos(t * 0.87f), 0.26f * Mathf.Sin(t * 1.43f), Mathf.Sin(t)) * idleDriftRadius;
        return spawnHome + drift;
    }

    private void EnsureHomeAnchor()
    {
        if (homeAnchor != null)
        {
            return;
        }

        Transform existing = transform.Find(HomeAnchorName);
        if (existing != null)
        {
            homeAnchor = existing;
            return;
        }

        GameObject anchor = new(HomeAnchorName);
        anchor.transform.SetParent(transform, false);
        anchor.transform.position = transform.position;
        homeAnchor = anchor.transform;
    }

    private void EnsureRuntimeRoot()
    {
        if (runtimeRoot != null)
        {
            return;
        }

        Transform existing = transform.Find(RuntimeRootName);
        if (existing != null)
        {
            runtimeRoot = existing;
            return;
        }

        GameObject root = new(RuntimeRootName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        runtimeRoot = root.transform;
    }

    private string ResolveRuntimeGroupId()
    {
        if (runtimeId == 0)
        {
            runtimeId = nextRuntimeId++;
        }

        return string.IsNullOrWhiteSpace(groupId)
            ? $"FlySwarm_{runtimeId}"
            : $"{groupId}_{runtimeId}";
    }

    private static Vector3 SampleSphere(System.Random random, float radius)
    {
        double z = random.NextDouble() * 2.0 - 1.0;
        double angle = random.NextDouble() * Mathf.PI * 2.0;
        double ring = System.Math.Sqrt(System.Math.Max(0.0, 1.0 - z * z));
        float distance = Mathf.Pow((float)random.NextDouble(), 1f / 3f) * radius;
        return new Vector3(
            (float)(ring * System.Math.Cos(angle)) * distance,
            (float)z * distance * 0.42f,
            (float)(ring * System.Math.Sin(angle)) * distance);
    }

    private static void DestroyUnityObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Vector3 anchor = homeAnchor != null ? homeAnchor.position : transform.position;
        Gizmos.color = new Color(0.18f, 0.9f, 0.28f, 0.28f);
        Gizmos.DrawWireSphere(anchor, spawnRadius);
        Gizmos.color = new Color(0.95f, 0.2f, 0.08f, 0.35f);
        Gizmos.DrawWireSphere(anchor, targetSearchRadius);
        if (currentTarget != null)
        {
            Gizmos.color = new Color(1f, 0.1f, 0.05f, 0.8f);
            Gizmos.DrawLine(anchor, currentTarget.transform.position);
        }
    }
}
