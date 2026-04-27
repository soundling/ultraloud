using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RetroFlockAgent))]
public sealed class RetroFlyPestAgent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RetroFlockAgent flockAgent;
    [SerializeField] private DirectionalSpriteAnimator animator;

    [Header("Bite")]
    [SerializeField, Min(0.01f)] private float biteRadius = 0.42f;
    [SerializeField, Min(0f)] private float biteDamage = 1.2f;
    [SerializeField, Min(0.01f)] private float biteInterval = 0.34f;
    [SerializeField] private LayerMask biteMask = ~0;
    [SerializeField] private bool biteExplicitTargetOnly = true;

    [Header("Animation")]
    [SerializeField] private Vector2 animationSpeedRange = new(0.88f, 1.35f);

    private readonly Collider[] biteHits = new Collider[12];
    private RetroFlySwarm swarm;
    private RetroDamageable target;
    private float nextBiteTime;

    public RetroDamageable Target => target;

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
        RandomizeAnimation();
        nextBiteTime = Time.time + Random.Range(0f, Mathf.Max(0.01f, biteInterval));
    }

    private void OnValidate()
    {
        biteRadius = Mathf.Max(0.01f, biteRadius);
        biteDamage = Mathf.Max(0f, biteDamage);
        biteInterval = Mathf.Max(0.01f, biteInterval);
        if (animationSpeedRange.y < animationSpeedRange.x)
        {
            animationSpeedRange.y = animationSpeedRange.x;
        }

        AutoAssignReferences();
    }

    private void Update()
    {
        if (biteDamage <= 0f || Time.time < nextBiteTime)
        {
            return;
        }

        nextBiteTime = Time.time + biteInterval * Random.Range(0.78f, 1.28f);
        TryBite();
    }

    public void ConfigureSwarm(
        RetroFlySwarm owner,
        RetroDamageable initialTarget,
        Transform homeAnchor,
        string groupId,
        Vector3 initialVelocity)
    {
        swarm = owner;
        target = initialTarget;
        AutoAssignReferences();

        if (flockAgent != null)
        {
            flockAgent.GroupId = groupId;
            flockAgent.HomeAnchor = homeAnchor;
            flockAgent.SetVelocity(initialVelocity);
            flockAgent.RandomizePhase(Random.value * 10000f);
        }
    }

    public void ConfigureBite(float radius, float damage, float interval, LayerMask mask)
    {
        biteRadius = Mathf.Max(0.01f, radius);
        biteDamage = Mathf.Max(0f, damage);
        biteInterval = Mathf.Max(0.01f, interval);
        biteMask = mask;
    }

    public void SetTarget(RetroDamageable nextTarget)
    {
        target = nextTarget;
    }

    private void AutoAssignReferences()
    {
        if (flockAgent == null)
        {
            flockAgent = GetComponent<RetroFlockAgent>();
        }

        if (animator == null)
        {
            animator = GetComponent<DirectionalSpriteAnimator>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<DirectionalSpriteAnimator>(true);
        }
    }

    private void RandomizeAnimation()
    {
        if (animator == null)
        {
            return;
        }

        animator.AnimationSpeed = Random.Range(animationSpeedRange.x, animationSpeedRange.y);
        animator.SetNormalizedClipPhase(Random.value);
    }

    private void TryBite()
    {
        if (target != null && IsValidTarget(target) && TryBiteTarget(target))
        {
            return;
        }

        if (biteExplicitTargetOnly)
        {
            return;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, biteRadius, biteHits, biteMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = biteHits[i];
            if (hit == null)
            {
                continue;
            }

            RetroDamageable candidate = hit.GetComponentInParent<RetroDamageable>();
            if (candidate != null && IsValidTarget(candidate) && TryBiteTarget(candidate))
            {
                return;
            }
        }
    }

    private bool TryBiteTarget(RetroDamageable damageable)
    {
        Collider targetCollider = damageable.GetComponentInChildren<Collider>();
        Vector3 hitPoint = ResolveTargetPoint(damageable, targetCollider);

        Vector3 toTarget = hitPoint - transform.position;
        if (toTarget.sqrMagnitude > biteRadius * biteRadius)
        {
            return false;
        }

        Vector3 hitNormal = toTarget.sqrMagnitude > 0.0001f ? -toTarget.normalized : Vector3.up;
        damageable.ApplyDamage(biteDamage, hitPoint, hitNormal, gameObject);
        return true;
    }

    private Vector3 ResolveTargetPoint(RetroDamageable damageable, Collider targetCollider)
    {
        if (targetCollider == null)
        {
            return damageable.transform.position;
        }

        return targetCollider.bounds.ClosestPoint(transform.position);
    }

    private bool IsValidTarget(RetroDamageable candidate)
    {
        if (candidate == null || candidate.IsDead)
        {
            return false;
        }

        if (candidate.GetComponentInParent<RetroFlyPestAgent>() != null)
        {
            return false;
        }

        return swarm == null || swarm.IsValidTarget(candidate);
    }
}
