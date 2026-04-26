using UnityEngine;

[DefaultExecutionOrder(180)]
[DisallowMultipleComponent]
public sealed class RetroHorseNpcRider : MonoBehaviour
{
    [Header("Mounting")]
    [SerializeField] private DirectionalSpriteDefinition mountedHorseDefinition;
    [SerializeField, Min(0f)] private float searchRadius = 18f;
    [SerializeField, Min(0.1f)] private float searchInterval = 1.15f;
    [SerializeField] private bool autoMountOnEnable = true;

    [Header("Riding")]
    [SerializeField, Min(0f)] private float targetSearchRadius = 30f;
    [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.55f;
    [SerializeField, Min(0f)] private float chaseDistance = 5.5f;
    [SerializeField, Min(0f)] private float orbitDistance = 3.1f;
    [SerializeField, Range(0f, 1f)] private float wanderThrottle = 0.42f;
    [SerializeField, Range(0f, 1f)] private float chaseThrottle = 0.88f;
    [SerializeField, Range(0f, 1f)] private float chaos = 0.32f;
    [SerializeField] private string preferredTargetTag = "Player";

    private RetroNpcAgent agent;
    private RetroDamageable damageable;
    private RetroHorseMount currentHorse;
    private Transform target;
    private Vector3 homePosition;
    private Vector3 wanderDirection;
    private float nextSearchTime;
    private float nextTargetRefreshTime;
    private float nextWanderPickTime;
    private float orbitSign = 1f;

    public RetroHorseMount CurrentHorse => currentHorse;
    public bool IsMounted => currentHorse != null && currentHorse.IsNpcRider(gameObject);

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
        homePosition = transform.position;
    }

    private void OnEnable()
    {
        AutoAssignReferences();
        homePosition = transform.position;
        nextSearchTime = autoMountOnEnable ? 0f : Time.time + searchInterval;
        PickWanderDirection();
    }

    private void OnDisable()
    {
        if (currentHorse != null && currentHorse.IsNpcRider(gameObject))
        {
            currentHorse.DismountNpc(true);
        }

        currentHorse = null;
    }

    private void Update()
    {
        if (damageable != null && damageable.IsDead)
        {
            if (currentHorse != null && currentHorse.IsNpcRider(gameObject))
            {
                currentHorse.DismountNpc(false);
            }

            currentHorse = null;
            return;
        }

        if (IsMounted)
        {
            TickMounted();
            return;
        }

        currentHorse = null;
        if (!autoMountOnEnable || Time.time < nextSearchTime)
        {
            return;
        }

        nextSearchTime = Time.time + searchInterval * Random.Range(0.75f, 1.35f);
        TryClaimNearestHorse();
    }

    private void TickMounted()
    {
        if (currentHorse == null)
        {
            return;
        }

        RefreshTarget();
        Vector3 desiredDirection;
        float throttle;
        bool sprint;

        if (target != null)
        {
            Vector3 toTarget = ProjectHorizontal(target.position - currentHorse.transform.position);
            float distance = toTarget.magnitude;
            Vector3 targetDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : currentHorse.transform.forward;
            Vector3 sideDirection = Vector3.Cross(Vector3.up, targetDirection) * orbitSign;
            if (distance <= orbitDistance)
            {
                desiredDirection = (sideDirection + targetDirection * 0.22f).normalized;
                throttle = chaseThrottle * 0.66f;
                sprint = false;
            }
            else if (distance <= chaseDistance)
            {
                desiredDirection = (sideDirection * 0.62f + targetDirection * 0.78f).normalized;
                throttle = chaseThrottle;
                sprint = Random.value > 0.45f;
            }
            else
            {
                desiredDirection = AddChaos(targetDirection, chaos * 0.6f);
                throttle = chaseThrottle;
                sprint = true;
            }
        }
        else
        {
            if (Time.time >= nextWanderPickTime)
            {
                PickWanderDirection();
            }

            desiredDirection = AddChaos(wanderDirection, chaos);
            throttle = wanderThrottle;
            sprint = false;
        }

        currentHorse.SetNpcRideInput(desiredDirection, throttle, sprint);
    }

    private void TryClaimNearestHorse()
    {
#if UNITY_2023_1_OR_NEWER
        RetroHorseMount[] horses = FindObjectsByType<RetroHorseMount>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        RetroHorseMount[] horses = FindObjectsOfType<RetroHorseMount>();
#endif
        RetroHorseMount best = null;
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < horses.Length; i++)
        {
            RetroHorseMount horse = horses[i];
            if (horse == null || !horse.CanAcceptNpcRider(gameObject))
            {
                continue;
            }

            float distance = HorizontalDistance(transform.position, horse.transform.position);
            if (distance > searchRadius || distance >= bestScore)
            {
                continue;
            }

            best = horse;
            bestScore = distance;
        }

        if (best == null)
        {
            return;
        }

        DirectionalSpriteDefinition definition = mountedHorseDefinition != null ? mountedHorseDefinition : best.DefaultMountedNpcDefinition;
        if (best.TryMountNpc(gameObject, definition, true))
        {
            currentHorse = best;
            orbitSign = Random.value < 0.5f ? -1f : 1f;
            RefreshTarget(true);
        }
    }

    private void RefreshTarget(bool force = false)
    {
        if (!force && Time.time < nextTargetRefreshTime && IsTargetUsable(target))
        {
            return;
        }

        nextTargetRefreshTime = Time.time + targetRefreshInterval * Random.Range(0.75f, 1.35f);
        if (agent != null && agent.Target != null && IsTargetUsable(agent.Target))
        {
            target = agent.Target;
            return;
        }

        GameObject tagged = !string.IsNullOrWhiteSpace(preferredTargetTag) ? GameObject.FindGameObjectWithTag(preferredTargetTag) : null;
        if (tagged != null && HorizontalDistance(currentHorse != null ? currentHorse.transform.position : transform.position, tagged.transform.position) <= targetSearchRadius)
        {
            target = tagged.transform;
            return;
        }

        target = null;
    }

    private bool IsTargetUsable(Transform candidate)
    {
        if (candidate == null || candidate == transform)
        {
            return false;
        }

        RetroDamageable targetDamageable = candidate.GetComponentInParent<RetroDamageable>();
        return targetDamageable == null || !targetDamageable.IsDead;
    }

    private void PickWanderDirection()
    {
        Vector3 homeBias = ProjectHorizontal(homePosition - (currentHorse != null ? currentHorse.transform.position : transform.position));
        Vector2 random = Random.insideUnitCircle.normalized;
        Vector3 randomDirection = new(random.x, 0f, random.y);
        wanderDirection = homeBias.sqrMagnitude > 8f
            ? (homeBias.normalized + randomDirection * 0.45f).normalized
            : randomDirection;
        if (wanderDirection.sqrMagnitude < 0.0001f)
        {
            wanderDirection = transform.forward;
        }

        nextWanderPickTime = Time.time + Random.Range(1.2f, 2.8f);
        orbitSign = Random.value < 0.5f ? -1f : 1f;
    }

    private Vector3 AddChaos(Vector3 direction, float strength)
    {
        float t = Time.time * 2.1f + GetInstanceID() * 0.017f;
        Vector3 wobble = new(Mathf.Sin(t * 1.7f), 0f, Mathf.Cos(t * 1.23f));
        Vector3 mixed = ProjectHorizontal(direction) + wobble * Mathf.Clamp01(strength);
        return mixed.sqrMagnitude > 0.0001f ? mixed.normalized : direction;
    }

    private void AutoAssignReferences()
    {
        if (agent == null)
        {
            agent = GetComponent<RetroNpcAgent>();
        }

        if (damageable == null)
        {
            damageable = GetComponent<RetroDamageable>();
        }
    }

    private static Vector3 ProjectHorizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        return ProjectHorizontal(a - b).magnitude;
    }
}
