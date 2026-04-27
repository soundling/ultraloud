using UnityEngine;
using Random = UnityEngine.Random;

public enum RetroMerchantCombatTactic
{
    HoldGround = 0,
    StrafeLeft = 1,
    StrafeRight = 2,
    Backpedal = 3,
    Pressure = 4
}

[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(RetroNpcAgent))]
[RequireComponent(typeof(RetroDamageable))]
public sealed class RetroMerchantCombatant : MonoBehaviour
{
    private const int HitBufferSize = 48;
    private static readonly RaycastHit[] HitBuffer = new RaycastHit[HitBufferSize];

    [Header("References")]
    [SerializeField] private RetroNpcAgent npcAgent;
    [SerializeField] private RetroDamageable damageable;
    [SerializeField] private DirectionalSpriteAnimator animator;
    [SerializeField] private Renderer visualRenderer;

    [Header("Clips")]
    [SerializeField] private string shootClipId = "Shoot";
    [SerializeField] private string meleeClipId = "Melee";

    [Header("Targeting")]
    [SerializeField, Min(0.1f)] private float engagementRange = 24f;
    [SerializeField, Min(0f)] private float targetAimHeight = 1.15f;
    [SerializeField, Min(0f)] private float muzzleHeight = 7.15f;
    [SerializeField, Min(0f)] private float muzzleForwardOffset = 0.55f;
    [SerializeField, Min(0f)] private float muzzleSideOffset = 0.34f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Ranged")]
    [SerializeField, Min(0.1f)] private float rangedAttackRange = 18f;
    [SerializeField] private Vector2Int shotsPerBurstRange = new Vector2Int(2, 4);
    [SerializeField, Min(0.01f)] private float shotInterval = 0.16f;
    [SerializeField, Min(0.05f)] private float burstCooldown = 1.25f;
    [SerializeField, Min(0f)] private float shotDamage = 7f;
    [SerializeField, Min(0f)] private float shotSpreadAngle = 1.8f;
    [SerializeField, Min(0f)] private float movingSpreadPenalty = 1.2f;
    [SerializeField, Min(0f)] private float shotImpactForce = 7f;
    [SerializeField] private Color bulletTrailColor = new Color(1f, 0.78f, 0.28f, 0.9f);
    [SerializeField, Min(0.001f)] private float bulletTrailWidth = 0.024f;
    [SerializeField, Min(0.01f)] private float bulletTrailDuration = 0.11f;
    [SerializeField, Min(0f)] private float bulletTrailStartOffset = 0.18f;
    [SerializeField, Min(0f)] private float bulletTrailEndOffset = 0.08f;

    [Header("Melee")]
    [SerializeField, Min(0.1f)] private float meleeRange = 3.1f;
    [SerializeField, Min(0f)] private float meleeDamage = 24f;
    [SerializeField, Min(0.05f)] private float meleeCooldown = 1.15f;
    [SerializeField, Min(0f)] private float meleeWindup = 0.14f;
    [SerializeField, Range(1f, 180f)] private float meleeArcDegrees = 78f;
    [SerializeField] private Color meleeFlashColor = new Color(0.25f, 0.95f, 1f, 0.8f);

    [Header("Tactics")]
    [SerializeField] private Vector2 tacticDurationRange = new Vector2(0.6f, 1.25f);
    [SerializeField, Min(0f)] private float idealMinRange = 7.5f;
    [SerializeField, Min(0f)] private float idealMaxRange = 13.5f;
    [SerializeField, Min(0f)] private float strafeSpeed = 1.65f;
    [SerializeField, Min(0f)] private float backpedalSpeed = 2.35f;
    [SerializeField, Min(0f)] private float pressureSpeed = 1.85f;

    private readonly Collider[] selfColliders = new Collider[16];
    private int selfColliderCount;
    private RetroMerchantCombatTactic currentTactic = RetroMerchantCombatTactic.HoldGround;
    private Transform currentTarget;
    private float nextTacticTime;
    private float nextBurstTime;
    private float nextShotTime;
    private float nextMeleeTime;
    private float pendingMeleeDamageTime = -999f;
    private int burstShotsRemaining;
    private bool meleeDamageApplied;

    public RetroMerchantCombatTactic CurrentTactic => currentTactic;
    public bool IsInCombat => HasCombatTarget();

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
        CacheSelfColliders();
    }

    private void OnEnable()
    {
        AutoAssignReferences();
        CacheSelfColliders();
        nextTacticTime = Time.time + Random.Range(0.15f, 0.35f);
        nextBurstTime = Time.time + Random.Range(0.25f, 0.65f);
        burstShotsRemaining = 0;
        pendingMeleeDamageTime = -999f;
        meleeDamageApplied = false;
    }

    private void OnValidate()
    {
        engagementRange = Mathf.Max(0.1f, engagementRange);
        rangedAttackRange = Mathf.Max(0.1f, rangedAttackRange);
        shotsPerBurstRange.x = Mathf.Max(1, shotsPerBurstRange.x);
        shotsPerBurstRange.y = Mathf.Max(shotsPerBurstRange.x, shotsPerBurstRange.y);
        shotInterval = Mathf.Max(0.01f, shotInterval);
        burstCooldown = Mathf.Max(0.05f, burstCooldown);
        shotDamage = Mathf.Max(0f, shotDamage);
        shotSpreadAngle = Mathf.Max(0f, shotSpreadAngle);
        movingSpreadPenalty = Mathf.Max(0f, movingSpreadPenalty);
        shotImpactForce = Mathf.Max(0f, shotImpactForce);
        bulletTrailWidth = Mathf.Max(0.001f, bulletTrailWidth);
        bulletTrailDuration = Mathf.Max(0.01f, bulletTrailDuration);
        bulletTrailStartOffset = Mathf.Max(0f, bulletTrailStartOffset);
        bulletTrailEndOffset = Mathf.Max(0f, bulletTrailEndOffset);
        meleeRange = Mathf.Max(0.1f, meleeRange);
        meleeDamage = Mathf.Max(0f, meleeDamage);
        meleeCooldown = Mathf.Max(0.05f, meleeCooldown);
        meleeWindup = Mathf.Max(0f, meleeWindup);
        idealMinRange = Mathf.Max(0f, idealMinRange);
        idealMaxRange = Mathf.Max(idealMinRange + 0.1f, idealMaxRange);
        tacticDurationRange.x = Mathf.Max(0.05f, tacticDurationRange.x);
        tacticDurationRange.y = Mathf.Max(tacticDurationRange.x, tacticDurationRange.y);
        strafeSpeed = Mathf.Max(0f, strafeSpeed);
        backpedalSpeed = Mathf.Max(0f, backpedalSpeed);
        pressureSpeed = Mathf.Max(0f, pressureSpeed);
        AutoAssignReferences();
    }

    private void Update()
    {
        if (damageable != null && damageable.IsDead)
        {
            enabled = false;
            return;
        }

        if (!HasCombatTarget())
        {
            burstShotsRemaining = 0;
            pendingMeleeDamageTime = -999f;
            meleeDamageApplied = false;
            return;
        }

        Vector3 targetPoint = ResolveTargetPoint(currentTarget);
        float distance = HorizontalDistance(transform.position, targetPoint);
        if (distance > engagementRange)
        {
            burstShotsRemaining = 0;
            return;
        }

        FaceTarget(targetPoint);
        TickTactics(targetPoint, distance);
        TickMelee(targetPoint, distance);
        TickRanged(targetPoint, distance);
    }

    private bool HasCombatTarget()
    {
        currentTarget = npcAgent != null ? npcAgent.Target : currentTarget;
        if (currentTarget == null || currentTarget == transform || currentTarget.IsChildOf(transform))
        {
            return false;
        }

        if (npcAgent != null && !npcAgent.IsProvoked && npcAgent.BehaviorMode != RetroNpcBehaviorMode.Aggressive)
        {
            return false;
        }

        RetroDamageable targetDamageable = ResolveTargetDamageable(currentTarget);
        return targetDamageable == null || !targetDamageable.IsDead;
    }

    private void TickTactics(Vector3 targetPoint, float distance)
    {
        if (Time.time >= nextTacticTime)
        {
            currentTactic = ChooseTactic(distance);
            nextTacticTime = Time.time + Random.Range(tacticDurationRange.x, tacticDurationRange.y);
        }

        if (burstShotsRemaining > 0 || pendingMeleeDamageTime > 0f)
        {
            return;
        }

        Vector3 toTarget = ProjectHorizontal(targetPoint - transform.position);
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 direction = currentTactic switch
        {
            RetroMerchantCombatTactic.StrafeLeft => Vector3.Cross(Vector3.up, toTarget.normalized),
            RetroMerchantCombatTactic.StrafeRight => Vector3.Cross(toTarget.normalized, Vector3.up),
            RetroMerchantCombatTactic.Backpedal => -toTarget.normalized,
            RetroMerchantCombatTactic.Pressure => toTarget.normalized,
            _ => Vector3.zero
        };

        float speed = currentTactic switch
        {
            RetroMerchantCombatTactic.Backpedal => backpedalSpeed,
            RetroMerchantCombatTactic.Pressure => pressureSpeed,
            RetroMerchantCombatTactic.StrafeLeft or RetroMerchantCombatTactic.StrafeRight => strafeSpeed,
            _ => 0f
        };

        if (currentTactic == RetroMerchantCombatTactic.Pressure && distance <= idealMinRange)
        {
            return;
        }

        if (currentTactic == RetroMerchantCombatTactic.Backpedal && distance >= idealMaxRange)
        {
            return;
        }

        MoveDirect(direction, speed);
    }

    private RetroMerchantCombatTactic ChooseTactic(float distance)
    {
        if (distance <= meleeRange * 1.45f)
        {
            return Random.value < 0.72f ? RetroMerchantCombatTactic.Backpedal : RandomStrafe();
        }

        if (distance < idealMinRange)
        {
            return Random.value < 0.65f ? RetroMerchantCombatTactic.Backpedal : RandomStrafe();
        }

        if (distance > idealMaxRange)
        {
            return Random.value < 0.7f ? RetroMerchantCombatTactic.Pressure : RandomStrafe();
        }

        float roll = Random.value;
        if (roll < 0.18f) return RetroMerchantCombatTactic.HoldGround;
        if (roll < 0.58f) return RandomStrafe();
        if (roll < 0.78f) return RetroMerchantCombatTactic.Backpedal;
        return RetroMerchantCombatTactic.Pressure;
    }

    private static RetroMerchantCombatTactic RandomStrafe()
    {
        return Random.value < 0.5f ? RetroMerchantCombatTactic.StrafeLeft : RetroMerchantCombatTactic.StrafeRight;
    }

    private void TickRanged(Vector3 targetPoint, float distance)
    {
        if (shotDamage <= 0f || distance > rangedAttackRange)
        {
            burstShotsRemaining = 0;
            return;
        }

        if (pendingMeleeDamageTime > 0f)
        {
            return;
        }

        if (burstShotsRemaining <= 0)
        {
            if (Time.time < nextBurstTime || !HasLineOfFire(targetPoint))
            {
                return;
            }

            burstShotsRemaining = Random.Range(shotsPerBurstRange.x, shotsPerBurstRange.y + 1);
            nextShotTime = Time.time;
            nextBurstTime = Time.time + burstCooldown + Random.Range(0f, 0.35f);
        }

        if (Time.time < nextShotTime)
        {
            return;
        }

        FireShot(targetPoint);
        burstShotsRemaining--;
        nextShotTime = Time.time + shotInterval;
    }

    private void TickMelee(Vector3 targetPoint, float distance)
    {
        if (pendingMeleeDamageTime > 0f)
        {
            if (!meleeDamageApplied && Time.time >= pendingMeleeDamageTime)
            {
                ApplyMeleeDamage(targetPoint);
                meleeDamageApplied = true;
            }

            if (Time.time >= pendingMeleeDamageTime + 0.18f)
            {
                pendingMeleeDamageTime = -999f;
            }

            return;
        }

        if (meleeDamage <= 0f || distance > meleeRange || Time.time < nextMeleeTime)
        {
            return;
        }

        PlayClip(meleeClipId);
        pendingMeleeDamageTime = Time.time + meleeWindup;
        meleeDamageApplied = false;
        nextMeleeTime = Time.time + meleeCooldown;
        burstShotsRemaining = 0;
    }

    private void FireShot(Vector3 targetPoint)
    {
        Vector3 origin = ResolveMuzzlePosition(targetPoint);
        Vector3 aimDirection = targetPoint - origin;
        if (aimDirection.sqrMagnitude < 0.0001f)
        {
            aimDirection = transform.forward;
        }

        float spread = shotSpreadAngle;
        if (currentTactic == RetroMerchantCombatTactic.StrafeLeft
            || currentTactic == RetroMerchantCombatTactic.StrafeRight
            || currentTactic == RetroMerchantCombatTactic.Backpedal)
        {
            spread += movingSpreadPenalty;
        }

        Vector3 shotDirection = ApplySpread(aimDirection.normalized, spread);
        float range = Mathf.Max(rangedAttackRange, Vector3.Distance(origin, targetPoint) + 1f);
        Vector3 trailEnd = origin + shotDirection * range;
        if (TryResolveShotHit(origin, shotDirection, range, out RaycastHit hit))
        {
            trailEnd = hit.point;
            ApplyDamageToHit(hit, shotDirection);
            if (hit.rigidbody != null && shotImpactForce > 0f)
            {
                hit.rigidbody.AddForceAtPosition(shotDirection * shotImpactForce, hit.point, ForceMode.Impulse);
            }

            RetroGameContext.Vfx.SpawnImpactFlash(hit.point, hit.normal, bulletTrailColor, 0.055f, 0.08f);
        }

        Vector3 trailStart = origin + shotDirection * bulletTrailStartOffset;
        Vector3 adjustedEnd = trailEnd - shotDirection * bulletTrailEndOffset;
        if ((adjustedEnd - trailStart).sqrMagnitude > 0.04f)
        {
            RetroGameContext.Vfx.SpawnBulletTrail("Merchant", trailStart, adjustedEnd, bulletTrailColor, bulletTrailWidth, bulletTrailDuration);
        }

        RetroGameContext.Vfx.SpawnImpactFlash(origin + shotDirection * 0.24f, -shotDirection, bulletTrailColor, 0.045f, 0.055f);
        RetroGameContext.Events.Publish(new RetroWeaponFiredEvent(gameObject, null, "Merchant", origin, shotDirection));
        PlayClip(shootClipId);
    }

    private bool TryResolveShotHit(Vector3 origin, Vector3 direction, float range, out RaycastHit bestHit)
    {
        bestHit = default;
        float bestDistance = float.PositiveInfinity;
        int hitCount = Physics.RaycastNonAlloc(origin, direction, HitBuffer, range, hitMask, triggerInteraction);
        if (hitCount >= HitBuffer.Length)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, range, hitMask, triggerInteraction);
            for (int i = 0; i < hits.Length; i++)
            {
                ConsiderShotHit(hits[i], ref bestHit, ref bestDistance);
            }
        }
        else
        {
            for (int i = 0; i < hitCount; i++)
            {
                ConsiderShotHit(HitBuffer[i], ref bestHit, ref bestDistance);
            }
        }

        return bestDistance < float.PositiveInfinity;
    }

    private void ConsiderShotHit(RaycastHit hit, ref RaycastHit bestHit, ref float bestDistance)
    {
        Collider hitCollider = hit.collider;
        if (hitCollider == null || IsSelfCollider(hitCollider) || hit.distance <= 0.001f)
        {
            return;
        }

        if (hit.distance < bestDistance)
        {
            bestDistance = hit.distance;
            bestHit = hit;
        }
    }

    private void ApplyDamageToHit(RaycastHit hit, Vector3 shotDirection)
    {
        if (hit.collider == null || shotDamage <= 0f)
        {
            return;
        }

        RetroDamageable targetDamageable = hit.collider.GetComponentInParent<RetroDamageable>();
        if (targetDamageable != null && targetDamageable != damageable)
        {
            targetDamageable.ApplyDamage(shotDamage, hit.point, -shotDirection, gameObject);
            return;
        }

        hit.collider.SendMessageUpwards("ApplyDamage", shotDamage, SendMessageOptions.DontRequireReceiver);
    }

    private void ApplyMeleeDamage(Vector3 targetPoint)
    {
        if (currentTarget == null)
        {
            return;
        }

        Vector3 toTarget = ProjectHorizontal(targetPoint - transform.position);
        float distance = toTarget.magnitude;
        if (distance > meleeRange + 0.35f)
        {
            return;
        }

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            float angle = Vector3.Angle(ProjectHorizontal(transform.forward).normalized, toTarget.normalized);
            if (angle > meleeArcDegrees * 0.5f)
            {
                return;
            }
        }

        RetroDamageable targetDamageable = ResolveTargetDamageable(currentTarget);
        if (targetDamageable != null && targetDamageable != damageable)
        {
            Vector3 normal = (transform.position - targetPoint).normalized;
            targetDamageable.ApplyDamage(meleeDamage, targetPoint, normal, gameObject);
            RetroGameContext.Vfx.SpawnImpactFlash(targetPoint, normal, meleeFlashColor, 0.11f, 0.12f);
            return;
        }

        currentTarget.SendMessageUpwards("ApplyDamage", meleeDamage, SendMessageOptions.DontRequireReceiver);
        RetroGameContext.Vfx.SpawnImpactFlash(targetPoint, -transform.forward, meleeFlashColor, 0.11f, 0.12f);
    }

    private bool HasLineOfFire(Vector3 targetPoint)
    {
        Vector3 origin = ResolveMuzzlePosition(targetPoint);
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        if (!TryResolveShotHit(origin, toTarget / distance, distance + 0.2f, out RaycastHit hit))
        {
            return true;
        }

        if (currentTarget == null || hit.collider == null)
        {
            return false;
        }

        Transform hitTransform = hit.collider.transform;
        return hitTransform == currentTarget || hitTransform.IsChildOf(currentTarget);
    }

    private Vector3 ResolveMuzzlePosition(Vector3 targetPoint)
    {
        float resolvedHeight = muzzleHeight;
        if (visualRenderer != null)
        {
            Bounds bounds = visualRenderer.bounds;
            if (bounds.size.y > 0.01f)
            {
                resolvedHeight = Mathf.Clamp(bounds.min.y + bounds.size.y * 0.55f - transform.position.y, 0.35f, bounds.size.y);
            }
        }

        Vector3 toTarget = ProjectHorizontal(targetPoint - transform.position);
        Vector3 forward = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
        return transform.position
            + Vector3.up * resolvedHeight
            + forward * muzzleForwardOffset
            + side * muzzleSideOffset;
    }

    private Vector3 ResolveTargetPoint(Transform targetTransform)
    {
        if (targetTransform == null)
        {
            return transform.position;
        }

        Collider targetCollider = targetTransform.GetComponentInChildren<Collider>();
        if (targetCollider != null)
        {
            return targetCollider.bounds.center;
        }

        return targetTransform.position + Vector3.up * targetAimHeight;
    }

    private static RetroDamageable ResolveTargetDamageable(Transform targetTransform)
    {
        if (targetTransform == null)
        {
            return null;
        }

        RetroDamageable targetDamageable = targetTransform.GetComponentInParent<RetroDamageable>();
        return targetDamageable != null ? targetDamageable : targetTransform.GetComponentInChildren<RetroDamageable>();
    }

    private void FaceTarget(Vector3 targetPoint)
    {
        Vector3 direction = ProjectHorizontal(targetPoint - transform.position);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(direction.normalized, Vector3.up),
            720f * Time.deltaTime);
    }

    private void MoveDirect(Vector3 direction, float speed)
    {
        if (speed <= 0f || direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.position += ProjectHorizontal(direction).normalized * speed * Time.deltaTime;
    }

    private void PlayClip(string clipId)
    {
        if (animator == null || string.IsNullOrWhiteSpace(clipId))
        {
            return;
        }

        animator.Play(clipId, true);
    }

    private void AutoAssignReferences()
    {
        if (npcAgent == null)
        {
            npcAgent = GetComponent<RetroNpcAgent>();
        }

        if (damageable == null)
        {
            damageable = GetComponent<RetroDamageable>();
        }

        if (animator == null)
        {
            animator = GetComponent<DirectionalSpriteAnimator>();
        }

        if (visualRenderer == null)
        {
            visualRenderer = GetComponentInChildren<Renderer>(true);
        }
    }

    private void CacheSelfColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(false);
        selfColliderCount = Mathf.Min(colliders.Length, selfColliders.Length);
        for (int i = 0; i < selfColliderCount; i++)
        {
            selfColliders[i] = colliders[i];
        }

        for (int i = selfColliderCount; i < selfColliders.Length; i++)
        {
            selfColliders[i] = null;
        }
    }

    private bool IsSelfCollider(Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        for (int i = 0; i < selfColliderCount; i++)
        {
            if (candidate == selfColliders[i])
            {
                return true;
            }
        }

        return candidate.transform == transform || candidate.transform.IsChildOf(transform);
    }

    private static Vector3 ApplySpread(Vector3 direction, float spreadAngle)
    {
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        if (spreadAngle <= 0.001f)
        {
            return direction;
        }

        Vector3 right = Vector3.Cross(Vector3.up, direction);
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }

        right.Normalize();
        Vector3 up = Vector3.Cross(direction, right).normalized;
        float yaw = Mathf.Tan(Random.Range(-spreadAngle, spreadAngle) * Mathf.Deg2Rad);
        float pitch = Mathf.Tan(Random.Range(-spreadAngle, spreadAngle) * Mathf.Deg2Rad);
        return (direction + right * yaw + up * pitch).normalized;
    }

    private static Vector3 ProjectHorizontal(Vector3 value)
    {
        return Vector3.ProjectOnPlane(value, Vector3.up);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, rangedAttackRange);
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, meleeRange);

        if (currentTarget != null)
        {
            Vector3 targetPoint = ResolveTargetPoint(currentTarget);
            Gizmos.color = bulletTrailColor;
            Gizmos.DrawLine(ResolveMuzzlePosition(targetPoint), targetPoint);
        }
    }
}
