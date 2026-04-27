using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RetroDamageable))]
public sealed class RetroKillerRabbit : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RetroDamageable damageable;
    [SerializeField] private DirectionalSpriteAnimator animator;
    [SerializeField] private Rigidbody movementBody;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private RetroGoreProfile goreProfile;

    [Header("Targeting")]
    [SerializeField] private bool preferTaggedTarget = true;
    [SerializeField] private string preferredTargetTag = "Player";
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField, Min(0.1f)] private float targetSearchRadius = 38f;
    [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.24f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float chaseSpeed = 9.4f;
    [SerializeField, Min(0f)] private float acceleration = 42f;
    [SerializeField, Min(0f)] private float turnSpeed = 980f;
    [SerializeField, Min(0f)] private float stopDistance = 2.65f;
    [SerializeField, Min(0f)] private float idleWanderRadius = 4.5f;
    [SerializeField, Min(0f)] private float idleWanderSpeed = 2.2f;
    [SerializeField, Min(0f)] private float hopAmplitude = 0.16f;
    [SerializeField, Min(0f)] private float hopFrequency = 14f;

    [Header("Frenzy Bite")]
    [SerializeField, Min(0f)] private float attackDamage = 28f;
    [SerializeField, Min(0.01f)] private float attackCooldown = 0.2f;
    [SerializeField, Min(0.05f)] private float lungeDuration = 0.28f;
    [SerializeField, Min(0f)] private float lungeSpeed = 15.5f;
    [SerializeField, Range(0f, 1f)] private float biteWindowStart = 0.22f;
    [SerializeField, Range(0f, 1f)] private float biteWindowEnd = 0.82f;
    [SerializeField, Min(0.01f)] private float biteRadius = 0.64f;
    [SerializeField, Min(0f)] private float biteForwardOffset = 0.52f;
    [SerializeField, Min(0f)] private float biteHeight = 0.42f;
    [SerializeField, Min(0f)] private float biteKnockback = 7.5f;
    [SerializeField, Range(0f, 1f)] private float chainAttackChance = 0.9f;
    [SerializeField, Min(0f)] private float biteGoreIntensity = 1.85f;
    [SerializeField, Min(0f)] private float biteDecalSize = 0.75f;

    [Header("Animation")]
    [SerializeField] private string sprintClipId = "Sprint";
    [SerializeField] private string attackClipId = "Attack";
    [SerializeField] private Vector2 animationSpeedRange = new(0.96f, 1.22f);

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private readonly Collider[] biteHits = new Collider[24];
    private readonly HashSet<RetroDamageable> hitThisLunge = new();
    private RetroDamageable currentTarget;
    private Vector3 homePosition;
    private Vector3 horizontalVelocity;
    private Vector3 wanderDestination;
    private Vector3 attackDirection = Vector3.forward;
    private Vector3 visualBaseLocalPosition;
    private float nextTargetRefreshTime;
    private float nextAttackTime;
    private float lungeStartTime;
    private float lungeEndTime;
    private float nextWanderTime;
    private bool hasWanderDestination;

    public RetroDamageable CurrentTarget => currentTarget;

    private bool IsLunging => Time.time < lungeEndTime;

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
        homePosition = transform.position;
        CacheVisualBase();
    }

    private void OnEnable()
    {
        AutoAssignReferences();
        CacheVisualBase();
        homePosition = transform.position;
        nextTargetRefreshTime = 0f;
        nextAttackTime = Time.time + Random.Range(0f, attackCooldown);
        horizontalVelocity = Vector3.zero;
        if (animator != null)
        {
            animator.AnimationSpeed = Random.Range(animationSpeedRange.x, animationSpeedRange.y);
            animator.SetNormalizedClipPhase(Random.value);
            animator.Play(sprintClipId, true);
        }
    }

    private void OnValidate()
    {
        targetSearchRadius = Mathf.Max(0.1f, targetSearchRadius);
        targetRefreshInterval = Mathf.Max(0.05f, targetRefreshInterval);
        chaseSpeed = Mathf.Max(0f, chaseSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        turnSpeed = Mathf.Max(0f, turnSpeed);
        stopDistance = Mathf.Max(0f, stopDistance);
        attackDamage = Mathf.Max(0f, attackDamage);
        attackCooldown = Mathf.Max(0.01f, attackCooldown);
        lungeDuration = Mathf.Max(0.05f, lungeDuration);
        lungeSpeed = Mathf.Max(0f, lungeSpeed);
        biteWindowEnd = Mathf.Max(biteWindowStart, biteWindowEnd);
        biteRadius = Mathf.Max(0.01f, biteRadius);
        biteForwardOffset = Mathf.Max(0f, biteForwardOffset);
        biteHeight = Mathf.Max(0f, biteHeight);
        biteKnockback = Mathf.Max(0f, biteKnockback);
        biteGoreIntensity = Mathf.Max(0f, biteGoreIntensity);
        biteDecalSize = Mathf.Max(0f, biteDecalSize);
        if (animationSpeedRange.y < animationSpeedRange.x)
        {
            animationSpeedRange.y = animationSpeedRange.x;
        }

        AutoAssignReferences();
    }

    private void FixedUpdate()
    {
        if (damageable != null && damageable.IsDead)
        {
            horizontalVelocity = Vector3.zero;
            return;
        }

        RefreshTargetIfNeeded();

        float deltaTime = Time.fixedDeltaTime;
        if (IsLunging)
        {
            UpdateLunge(deltaTime);
            return;
        }

        if (currentTarget != null && IsValidTarget(currentTarget))
        {
            UpdateChase(deltaTime);
        }
        else
        {
            UpdateIdle(deltaTime);
        }
    }

    private void LateUpdate()
    {
        UpdateVisualHop();
    }

    private void AutoAssignReferences()
    {
        if (damageable == null)
        {
            damageable = GetComponent<RetroDamageable>();
        }

        if (animator == null)
        {
            animator = GetComponent<DirectionalSpriteAnimator>();
        }

        if (movementBody == null)
        {
            movementBody = GetComponent<Rigidbody>();
        }

        if (visualRoot == null)
        {
            Transform quad = transform.Find("Quad");
            visualRoot = quad != null ? quad : GetComponentInChildren<Renderer>(true)?.transform;
        }
    }

    private void CacheVisualBase()
    {
        if (visualRoot != null)
        {
            visualBaseLocalPosition = visualRoot.localPosition;
        }
    }

    private void RefreshTargetIfNeeded()
    {
        if (Time.time < nextTargetRefreshTime && IsValidTarget(currentTarget))
        {
            return;
        }

        nextTargetRefreshTime = Time.time + targetRefreshInterval;
        currentTarget = ResolveTarget();
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
        RetroDamageable[] damageables = FindObjectsByType<RetroDamageable>(FindObjectsInactive.Exclude);
        for (int i = 0; i < damageables.Length; i++)
        {
            RetroDamageable candidate = damageables[i];
            if (!IsValidTarget(candidate))
            {
                continue;
            }

            float distanceSqr = (ResolveTargetCenter(candidate) - transform.position).sqrMagnitude;
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

            float distanceSqr = (ResolveTargetCenter(candidate) - transform.position).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            best = candidate;
            bestDistanceSqr = distanceSqr;
        }

        return best;
    }

    private bool IsValidTarget(RetroDamageable candidate)
    {
        if (candidate == null || candidate.IsDead || candidate == damageable)
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

        if (candidate.GetComponentInParent<RetroKillerRabbit>() != null)
        {
            return false;
        }

        return (ResolveTargetCenter(candidate) - transform.position).sqrMagnitude <= targetSearchRadius * targetSearchRadius;
    }

    private void UpdateChase(float deltaTime)
    {
        Vector3 targetCenter = ResolveTargetCenter(currentTarget);
        Vector3 toTarget = targetCenter - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        Vector3 direction = distance > 0.001f ? toTarget / distance : transform.forward;

        if (distance <= Mathf.Max(stopDistance, biteForwardOffset + biteRadius * 0.35f) && Time.time >= nextAttackTime)
        {
            BeginLunge(direction);
            return;
        }

        Vector3 desiredVelocity = direction * chaseSpeed;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, acceleration * deltaTime);
        Move(horizontalVelocity * deltaTime);
        RotateToward(direction, deltaTime);
        PlayClip(sprintClipId, false);
    }

    private void UpdateIdle(float deltaTime)
    {
        if (!hasWanderDestination || Time.time >= nextWanderTime || Vector3.Distance(transform.position, wanderDestination) <= 0.35f)
        {
            Vector2 scatter = Random.insideUnitCircle * idleWanderRadius;
            wanderDestination = homePosition + new Vector3(scatter.x, 0f, scatter.y);
            nextWanderTime = Time.time + Random.Range(1.1f, 2.8f);
            hasWanderDestination = true;
        }

        Vector3 toDestination = wanderDestination - transform.position;
        toDestination.y = 0f;
        Vector3 direction = toDestination.sqrMagnitude > 0.001f ? toDestination.normalized : transform.forward;
        Vector3 desiredVelocity = direction * idleWanderSpeed;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, acceleration * 0.42f * deltaTime);
        Move(horizontalVelocity * deltaTime);
        RotateToward(direction, deltaTime);
        PlayClip(sprintClipId, false);
    }

    private void BeginLunge(Vector3 direction)
    {
        attackDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        lungeStartTime = Time.time;
        lungeEndTime = lungeStartTime + lungeDuration;
        horizontalVelocity = attackDirection * lungeSpeed;
        nextAttackTime = lungeEndTime + attackCooldown * Random.Range(0.58f, 1.18f);
        hitThisLunge.Clear();
        PlayClip(attackClipId, true);
        RotateToward(attackDirection, Time.fixedDeltaTime);
    }

    private void UpdateLunge(float deltaTime)
    {
        float t = Mathf.InverseLerp(lungeStartTime, lungeEndTime, Time.time);
        float speedScale = Mathf.Lerp(1.12f, 0.28f, t);
        horizontalVelocity = attackDirection * lungeSpeed * speedScale;
        Move(horizontalVelocity * deltaTime);
        RotateToward(attackDirection, deltaTime);

        if (t >= biteWindowStart && t <= biteWindowEnd)
        {
            TryBite();
        }

        if (Time.time >= lungeEndTime)
        {
            PlayClip(sprintClipId, false);
            if (currentTarget != null && IsValidTarget(currentTarget) && Random.value < chainAttackChance)
            {
                nextAttackTime = Mathf.Min(nextAttackTime, Time.time + attackCooldown * 0.28f);
            }
        }
    }

    private void TryBite()
    {
        Vector3 mouth = ResolveMouthPosition();
        if (currentTarget != null && IsValidTarget(currentTarget) && TryDamageTarget(currentTarget, mouth))
        {
            return;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(mouth, biteRadius, biteHits, targetMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = biteHits[i];
            if (hit == null)
            {
                continue;
            }

            RetroDamageable candidate = hit.GetComponentInParent<RetroDamageable>();
            if (IsValidTarget(candidate) && TryDamageTarget(candidate, mouth))
            {
                return;
            }
        }
    }

    private bool TryDamageTarget(RetroDamageable target, Vector3 mouth)
    {
        if (target == null || hitThisLunge.Contains(target))
        {
            return false;
        }

        Collider targetCollider = target.GetComponentInChildren<Collider>();
        Vector3 hitPoint = targetCollider != null
            ? targetCollider.bounds.ClosestPoint(mouth)
            : ResolveTargetCenter(target);

        if ((hitPoint - mouth).sqrMagnitude > biteRadius * biteRadius)
        {
            return false;
        }

        Vector3 normal = attackDirection.sqrMagnitude > 0.001f ? attackDirection.normalized : transform.forward;
        hitThisLunge.Add(target);
        target.ApplyDamage(attackDamage, hitPoint, normal, gameObject);
        ApplyTargetKnockback(target, normal);
        SpawnBiteGore(target, hitPoint, normal);
        return true;
    }

    private void ApplyTargetKnockback(RetroDamageable target, Vector3 normal)
    {
        if (biteKnockback <= 0f || target == null)
        {
            return;
        }

        Rigidbody targetBody = target.GetComponentInParent<Rigidbody>();
        if (targetBody == null || targetBody.isKinematic)
        {
            return;
        }

        targetBody.AddForce((normal + Vector3.up * 0.22f).normalized * biteKnockback, ForceMode.VelocityChange);
    }

    private void SpawnBiteGore(RetroDamageable victim, Vector3 hitPoint, Vector3 normal)
    {
        if (goreProfile == null || biteGoreIntensity <= 0f)
        {
            return;
        }

        Vector3 safeNormal = normal.sqrMagnitude > 0.001f ? normal.normalized : transform.forward;
        Vector3 center = hitPoint + safeNormal * 0.08f + Vector3.up * 0.06f;
        RetroGoreSystem.SpawnGoreBurst(goreProfile, center, hitPoint, safeNormal, victim != null ? victim.transform : null, biteGoreIntensity, gameObject);
        if (biteDecalSize > 0f)
        {
            RetroGoreSystem.SpawnImpactDecal(goreProfile, hitPoint, safeNormal, biteDecalSize);
        }
    }

    private Vector3 ResolveTargetCenter(RetroDamageable target)
    {
        if (target == null)
        {
            return transform.position;
        }

        Collider targetCollider = target.GetComponentInChildren<Collider>();
        if (targetCollider != null)
        {
            return targetCollider.bounds.center;
        }

        Renderer targetRenderer = target.GetComponentInChildren<Renderer>();
        return targetRenderer != null ? targetRenderer.bounds.center : target.transform.position;
    }

    private Vector3 ResolveMouthPosition()
    {
        Vector3 forward = attackDirection.sqrMagnitude > 0.001f ? attackDirection.normalized : transform.forward;
        return transform.position + Vector3.up * biteHeight + forward * biteForwardOffset;
    }

    private void Move(Vector3 displacement)
    {
        Vector3 nextPosition = transform.position + displacement;
        if (movementBody != null && movementBody.isKinematic)
        {
            movementBody.MovePosition(nextPosition);
        }
        else
        {
            transform.position = nextPosition;
        }
    }

    private void RotateToward(Vector3 direction, float deltaTime)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * deltaTime);
        if (movementBody != null && movementBody.isKinematic)
        {
            movementBody.MoveRotation(nextRotation);
        }
        else
        {
            transform.rotation = nextRotation;
        }
    }

    private void PlayClip(string clipId, bool restart)
    {
        if (animator == null || string.IsNullOrWhiteSpace(clipId))
        {
            return;
        }

        if (!restart && string.Equals(animator.CurrentClipId, clipId, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        animator.Play(clipId, restart);
    }

    private void UpdateVisualHop()
    {
        if (visualRoot == null)
        {
            return;
        }

        float speedRatio = Mathf.Clamp01(horizontalVelocity.magnitude / Mathf.Max(0.01f, chaseSpeed));
        float attackBoost = IsLunging ? 1.85f : 1f;
        float wave = Mathf.Abs(Mathf.Sin(Time.time * hopFrequency * attackBoost));
        float hop = wave * hopAmplitude * Mathf.Lerp(0.25f, 1f, speedRatio) * attackBoost;
        visualRoot.localPosition = visualBaseLocalPosition + Vector3.up * hop;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.05f, 0.05f, 0.34f);
        Gizmos.DrawWireSphere(transform.position, targetSearchRadius);
        Gizmos.color = new Color(1f, 0f, 0f, 0.75f);
        Gizmos.DrawWireSphere(ResolveMouthPosition(), biteRadius);
        if (currentTarget != null)
        {
            Gizmos.DrawLine(transform.position + Vector3.up * 0.2f, ResolveTargetCenter(currentTarget));
        }
    }
}
