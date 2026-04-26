using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(160)]
[DisallowMultipleComponent]
[RequireComponent(typeof(RetroDamageable))]
public sealed class RetroSkeletonMotocrossRider : MonoBehaviour
{
    private enum ChaosMoveMode
    {
        Hunt = 0,
        Orbit = 1,
        Donut = 2,
        Wheelie = 3,
        Ram = 4,
        Wander = 5
    }

    private const int OverlapBufferSize = 40;
    private static readonly Collider[] OverlapBuffer = new Collider[OverlapBufferSize];

    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
    private static readonly int SpecularStrengthId = Shader.PropertyToID("_SpecularStrength");
    private static readonly int MacroNormalBendId = Shader.PropertyToID("_MacroNormalBend");
    private static readonly int WrapDiffuseId = Shader.PropertyToID("_WrapDiffuse");

    [Header("References")]
    [SerializeField] private RetroDamageable damageable;
    [SerializeField] private DirectionalSpriteAnimator animator;
    [SerializeField] private DirectionalSpriteLocomotion locomotion;
    [SerializeField] private Rigidbody movementBody;
    [SerializeField] private Renderer visualRenderer;
    [SerializeField] private Transform visualRoot;

    [Header("Clips")]
    [SerializeField] private string idleClipId = "Idle";
    [SerializeField] private string rideClipId = "Ride";
    [SerializeField] private string wheelieClipId = "Wheelie";
    [SerializeField] private string attackClipId = "Attack";

    [Header("Targeting")]
    [SerializeField, Min(0f)] private float awarenessRadius = 34f;
    [SerializeField, Min(0.05f)] private float retargetInterval = 0.9f;
    [SerializeField, Range(0f, 1f)] private float randomTargetChance = 0.55f;
    [SerializeField] private string preferredTargetTag = "Player";
    [SerializeField] private bool attackAnythingDamageable = true;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float cruiseSpeed = 7.5f;
    [SerializeField, Min(0f)] private float burstSpeed = 13.5f;
    [SerializeField, Min(0f)] private float reversePanicSpeed = 4.5f;
    [SerializeField, Min(0f)] private float acceleration = 36f;
    [SerializeField, Min(0f)] private float turnSpeed = 760f;
    [SerializeField, Min(0f)] private float orbitRadius = 7.5f;
    [SerializeField, Min(0f)] private float wanderRadius = 12f;
    [SerializeField] private Vector2 tacticDurationRange = new(0.32f, 0.95f);
    [SerializeField, Range(0f, 1f)] private float wheelieChance = 0.22f;
    [SerializeField, Range(0f, 1f)] private float donutChance = 0.18f;

    [Header("Ramming")]
    [SerializeField, Min(0f)] private float ramDamage = 27f;
    [SerializeField, Min(0f)] private float ramRadius = 2.2f;
    [SerializeField, Min(0f)] private float ramForwardOffset = 2.1f;
    [SerializeField, Min(0.05f)] private float ramCooldown = 0.42f;
    [SerializeField, Min(0f)] private float ramKnockback = 9f;
    [SerializeField] private LayerMask damageMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Visual Chaos")]
    [SerializeField, Min(0f)] private float bobAmplitude = 0.18f;
    [SerializeField, Min(0f)] private float bobFrequency = 13f;
    [SerializeField, Min(0f)] private float leanDegrees = 13f;
    [SerializeField] private Color speedRimColor = new(1f, 0.18f, 0.05f, 1f);
    [SerializeField] private Color wheelieRimColor = new(1f, 0.78f, 0.28f, 1f);
    [SerializeField, Min(0f)] private float exhaustTrailInterval = 0.055f;
    [SerializeField] private Color exhaustTrailColor = new(1f, 0.28f, 0.04f, 0.85f);
    [SerializeField] private Color boneDustTrailColor = new(0.76f, 0.7f, 0.55f, 0.58f);

    private readonly List<RetroDamageable> targetCandidates = new();
    private readonly HashSet<RetroDamageable> damagedThisPulse = new();
    private MaterialPropertyBlock propertyBlock;

    private Transform currentTarget;
    private Vector3 homePosition;
    private Vector3 currentVelocity;
    private Vector3 wanderDestination;
    private ChaosMoveMode moveMode = ChaosMoveMode.Wander;
    private string currentClip;
    private float nextRetargetTime;
    private float nextTacticTime;
    private float nextRamTime;
    private float nextTrailTime;
    private float modeSign = 1f;
    private bool deathFxSpawned;
    private Vector3 visualBaseLocalPosition;

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
        homePosition = transform.position;
        ConfigurePhysics();
    }

    private void OnEnable()
    {
        AutoAssignReferences();
        ConfigurePhysics();
        visualBaseLocalPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        homePosition = transform.position;
        currentVelocity = Vector3.zero;
        deathFxSpawned = false;
        PickNewTactic(true);
        PlayClip(idleClipId);
    }

    private void OnValidate()
    {
        awarenessRadius = Mathf.Max(0f, awarenessRadius);
        retargetInterval = Mathf.Max(0.05f, retargetInterval);
        cruiseSpeed = Mathf.Max(0f, cruiseSpeed);
        burstSpeed = Mathf.Max(cruiseSpeed, burstSpeed);
        reversePanicSpeed = Mathf.Max(0f, reversePanicSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        turnSpeed = Mathf.Max(0f, turnSpeed);
        orbitRadius = Mathf.Max(0f, orbitRadius);
        wanderRadius = Mathf.Max(0f, wanderRadius);
        tacticDurationRange.x = Mathf.Max(0.05f, tacticDurationRange.x);
        tacticDurationRange.y = Mathf.Max(tacticDurationRange.x, tacticDurationRange.y);
        ramDamage = Mathf.Max(0f, ramDamage);
        ramRadius = Mathf.Max(0f, ramRadius);
        ramForwardOffset = Mathf.Max(0f, ramForwardOffset);
        ramCooldown = Mathf.Max(0.05f, ramCooldown);
        ramKnockback = Mathf.Max(0f, ramKnockback);
        bobAmplitude = Mathf.Max(0f, bobAmplitude);
        bobFrequency = Mathf.Max(0f, bobFrequency);
        leanDegrees = Mathf.Max(0f, leanDegrees);
        exhaustTrailInterval = Mathf.Max(0f, exhaustTrailInterval);
        AutoAssignReferences();
    }

    private void Update()
    {
        if (damageable != null && damageable.IsDead)
        {
            TickDead();
            return;
        }

        ResolveTarget();
        if (Time.time >= nextTacticTime)
        {
            PickNewTactic(false);
        }

        Vector3 desiredDirection = ResolveDesiredDirection();
        float desiredSpeed = ResolveDesiredSpeed();
        TickMovement(desiredDirection, desiredSpeed);
        TickRamming();
        TickExhaustTrails();
        TickClip();
    }

    private void LateUpdate()
    {
        ApplyVisualMotion();
        ApplyShaderPulse();
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

        if (locomotion == null)
        {
            locomotion = GetComponent<DirectionalSpriteLocomotion>();
        }

        if (movementBody == null)
        {
            movementBody = GetComponent<Rigidbody>();
        }

        if (visualRenderer == null)
        {
            visualRenderer = GetComponentInChildren<MeshRenderer>(true);
        }

        if (visualRoot == null && visualRenderer != null)
        {
            visualRoot = visualRenderer.transform;
        }
    }

    private void ConfigurePhysics()
    {
        if (locomotion != null)
        {
            locomotion.enabled = false;
        }

        if (movementBody == null)
        {
            return;
        }

        movementBody.useGravity = false;
        movementBody.isKinematic = true;
        movementBody.interpolation = RigidbodyInterpolation.Interpolate;
        movementBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void ResolveTarget()
    {
        if (Time.time < nextRetargetTime && IsUsableTarget(currentTarget))
        {
            return;
        }

        nextRetargetTime = Time.time + retargetInterval * Random.Range(0.72f, 1.25f);
        currentTarget = PickTarget();
    }

    private Transform PickTarget()
    {
        targetCandidates.Clear();

#if UNITY_2023_1_OR_NEWER
        RetroDamageable[] damageables = FindObjectsByType<RetroDamageable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        RetroDamageable[] damageables = FindObjectsOfType<RetroDamageable>();
#endif
        for (int i = 0; i < damageables.Length; i++)
        {
            RetroDamageable candidate = damageables[i];
            if (candidate == null || candidate == damageable || candidate.IsDead || candidate.transform.IsChildOf(transform))
            {
                continue;
            }

            float distance = HorizontalDistance(transform.position, candidate.transform.position);
            if (distance > awarenessRadius)
            {
                continue;
            }

            if (!attackAnythingDamageable && !candidate.CompareTag(preferredTargetTag))
            {
                continue;
            }

            targetCandidates.Add(candidate);
        }

        if (targetCandidates.Count == 0)
        {
            return null;
        }

        if (Random.value < randomTargetChance)
        {
            return targetCandidates[Random.Range(0, targetCandidates.Count)].transform;
        }

        RetroDamageable best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < targetCandidates.Count; i++)
        {
            RetroDamageable candidate = targetCandidates[i];
            float distance = HorizontalDistance(transform.position, candidate.transform.position);
            float score = -distance;
            if (candidate.CompareTag(preferredTargetTag))
            {
                score += 18f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best != null ? best.transform : null;
    }

    private bool IsUsableTarget(Transform candidate)
    {
        if (candidate == null || candidate == transform || candidate.IsChildOf(transform))
        {
            return false;
        }

        if (HorizontalDistance(transform.position, candidate.position) > awarenessRadius * 1.25f)
        {
            return false;
        }

        RetroDamageable targetDamageable = candidate.GetComponentInParent<RetroDamageable>();
        return targetDamageable == null || !targetDamageable.IsDead;
    }

    private void PickNewTactic(bool forceWander)
    {
        modeSign = Random.value < 0.5f ? -1f : 1f;
        nextTacticTime = Time.time + Random.Range(tacticDurationRange.x, tacticDurationRange.y);

        if (forceWander || currentTarget == null)
        {
            moveMode = ChaosMoveMode.Wander;
            wanderDestination = homePosition + Random.insideUnitSphere.ProjectHorizontal() * wanderRadius;
            return;
        }

        float roll = Random.value;
        if (roll < donutChance)
        {
            moveMode = ChaosMoveMode.Donut;
        }
        else if (roll < donutChance + wheelieChance)
        {
            moveMode = ChaosMoveMode.Wheelie;
        }
        else if (roll < 0.62f)
        {
            moveMode = ChaosMoveMode.Ram;
        }
        else if (roll < 0.82f)
        {
            moveMode = ChaosMoveMode.Orbit;
        }
        else
        {
            moveMode = ChaosMoveMode.Hunt;
        }
    }

    private Vector3 ResolveDesiredDirection()
    {
        if (currentTarget == null)
        {
            Vector3 toWander = wanderDestination - transform.position;
            if (toWander.ProjectHorizontal().sqrMagnitude < 1.2f)
            {
                PickNewTactic(true);
            }

            Vector3 wanderDirection = toWander.ProjectHorizontal();
            if (wanderDirection.sqrMagnitude < 0.0001f)
            {
                wanderDirection = transform.forward;
            }

            return AddChaos(wanderDirection.normalized, 0.35f);
        }

        Vector3 toTarget = (currentTarget.position - transform.position).ProjectHorizontal();
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            toTarget = transform.forward;
        }

        Vector3 targetDirection = toTarget.normalized;
        Vector3 sideDirection = Vector3.Cross(Vector3.up, targetDirection) * modeSign;
        float distance = toTarget.magnitude;

        return moveMode switch
        {
            ChaosMoveMode.Orbit => AddChaos((targetDirection * Mathf.Clamp01((distance - orbitRadius) / Mathf.Max(orbitRadius, 0.1f)) + sideDirection * 0.95f).normalized, 0.2f),
            ChaosMoveMode.Donut => AddChaos((sideDirection + transform.forward * 0.35f).normalized, 0.55f),
            ChaosMoveMode.Wheelie => AddChaos((targetDirection * 0.82f + sideDirection * 0.28f).normalized, 0.18f),
            ChaosMoveMode.Ram => AddChaos(targetDirection, 0.13f),
            _ => AddChaos((targetDirection + sideDirection * 0.18f).normalized, 0.25f)
        };
    }

    private float ResolveDesiredSpeed()
    {
        return moveMode switch
        {
            ChaosMoveMode.Donut => Mathf.Lerp(cruiseSpeed, burstSpeed, 0.45f),
            ChaosMoveMode.Wheelie => Mathf.Lerp(cruiseSpeed, burstSpeed, 0.68f),
            ChaosMoveMode.Ram => burstSpeed,
            ChaosMoveMode.Wander => cruiseSpeed * 0.78f,
            _ => cruiseSpeed
        };
    }

    private Vector3 AddChaos(Vector3 direction, float strength)
    {
        float t = Time.time * 3.7f + GetInstanceID() * 0.013f;
        Vector3 jitter = new(Mathf.Sin(t * 1.7f), 0f, Mathf.Cos(t * 1.19f));
        Vector3 mixed = direction + jitter * strength;
        mixed = mixed.ProjectHorizontal();
        return mixed.sqrMagnitude > 0.0001f ? mixed.normalized : direction;
    }

    private void TickMovement(Vector3 desiredDirection, float desiredSpeed)
    {
        desiredDirection = desiredDirection.ProjectHorizontal();
        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            desiredDirection = transform.forward.ProjectHorizontal();
        }

        desiredDirection.Normalize();
        Vector3 desiredVelocity = desiredDirection * desiredSpeed;
        currentVelocity = Vector3.MoveTowards(currentVelocity, desiredVelocity, acceleration * Time.deltaTime);
        Vector3 nextPosition = transform.position + currentVelocity * Time.deltaTime;

        if (movementBody != null)
        {
            movementBody.MovePosition(nextPosition);
        }
        else
        {
            transform.position = nextPosition;
        }

        if (currentVelocity.ProjectHorizontal().sqrMagnitude > 0.05f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(currentVelocity.ProjectHorizontal().normalized, Vector3.up);
            Quaternion nextRotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            if (movementBody != null)
            {
                movementBody.MoveRotation(nextRotation);
            }
            else
            {
                transform.rotation = nextRotation;
            }
        }
    }

    private void TickRamming()
    {
        if (ramDamage <= 0f || Time.time < nextRamTime)
        {
            return;
        }

        Vector3 center = transform.position + transform.forward.ProjectHorizontal().normalized * ramForwardOffset + Vector3.up * 0.75f;
        int hitCount = Physics.OverlapSphereNonAlloc(center, ramRadius, OverlapBuffer, damageMask, triggerInteraction);
        damagedThisPulse.Clear();
        bool hitAnything = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = OverlapBuffer[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            RetroDamageable targetDamageable = hit.GetComponentInParent<RetroDamageable>();
            if (targetDamageable == null || targetDamageable == damageable || targetDamageable.IsDead || !damagedThisPulse.Add(targetDamageable))
            {
                continue;
            }

            Vector3 hitPoint = hit.ClosestPoint(center);
            Vector3 normal = (targetDamageable.transform.position - transform.position).ProjectHorizontal();
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = transform.forward;
            }

            float modeMultiplier = moveMode == ChaosMoveMode.Wheelie ? 1.28f : moveMode == ChaosMoveMode.Ram ? 1.45f : 1f;
            targetDamageable.ApplyDamage(ramDamage * modeMultiplier, hitPoint, -normal.normalized, gameObject);
            if (hit.attachedRigidbody != null && !hit.attachedRigidbody.isKinematic)
            {
                hit.attachedRigidbody.AddForce(normal.normalized * ramKnockback + Vector3.up * (ramKnockback * 0.25f), ForceMode.Impulse);
            }

            RetroGameContext.Vfx.SpawnImpactFlash(hitPoint, -normal.normalized, speedRimColor, 0.16f, 0.13f);
            hitAnything = true;
        }

        if (hitAnything)
        {
            nextRamTime = Time.time + ramCooldown;
            moveMode = ChaosMoveMode.Ram;
            nextTacticTime = Mathf.Max(nextTacticTime, Time.time + 0.32f);
            PlayClip(attackClipId, true);
        }
    }

    private void TickExhaustTrails()
    {
        if (exhaustTrailInterval <= 0f || Time.time < nextTrailTime || currentVelocity.sqrMagnitude < 1f)
        {
            return;
        }

        nextTrailTime = Time.time + exhaustTrailInterval;
        Vector3 backward = currentVelocity.ProjectHorizontal();
        if (backward.sqrMagnitude < 0.0001f)
        {
            backward = transform.forward;
        }

        backward = -backward.normalized;
        Vector3 start = transform.position + backward * 1.15f + Vector3.up * 0.72f + transform.right * Random.Range(-0.22f, 0.22f);
        Vector3 end = start + backward * Random.Range(0.8f, 1.55f) + Vector3.up * Random.Range(0.05f, 0.4f);
        Color color = Random.value < 0.65f ? exhaustTrailColor : boneDustTrailColor;
        RetroGameContext.Vfx.SpawnBulletTrail("SkeletonMotoExhaust", start, end, color, Random.Range(0.035f, 0.075f), Random.Range(0.08f, 0.17f));
    }

    private void TickClip()
    {
        if (moveMode == ChaosMoveMode.Wheelie)
        {
            PlayClip(wheelieClipId);
        }
        else if (moveMode == ChaosMoveMode.Ram)
        {
            PlayClip(attackClipId);
        }
        else if (currentVelocity.ProjectHorizontal().magnitude > cruiseSpeed * 0.25f)
        {
            PlayClip(rideClipId);
        }
        else
        {
            PlayClip(idleClipId);
        }
    }

    private void PlayClip(string clipId, bool restart = false)
    {
        if (animator == null || string.IsNullOrWhiteSpace(clipId))
        {
            return;
        }

        if (!restart && string.Equals(currentClip, clipId, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (animator.Play(clipId, restart))
        {
            currentClip = clipId;
        }
    }

    private void ApplyVisualMotion()
    {
        if (visualRoot == null)
        {
            return;
        }

        float speed01 = Mathf.InverseLerp(0f, Mathf.Max(0.1f, burstSpeed), currentVelocity.ProjectHorizontal().magnitude);
        float bob = Mathf.Sin(Time.time * bobFrequency + GetInstanceID() * 0.01f) * bobAmplitude * Mathf.Lerp(0.35f, 1.25f, speed01);
        float wheelieLift = moveMode == ChaosMoveMode.Wheelie ? Mathf.Sin(Time.time * 18f) * 0.08f + 0.16f : 0f;
        visualRoot.localPosition = visualBaseLocalPosition + new Vector3(0f, bob + wheelieLift, 0f);

        float sideSpeed = Vector3.Dot(currentVelocity.ProjectHorizontal(), transform.right);
        float lean = Mathf.Clamp(-sideSpeed * 1.35f, -leanDegrees, leanDegrees);
        if (moveMode == ChaosMoveMode.Donut)
        {
            lean += leanDegrees * 0.45f * modeSign;
        }

        visualRoot.rotation = visualRoot.rotation * Quaternion.Euler(0f, 0f, lean);
    }

    private void ApplyShaderPulse()
    {
        if (visualRenderer == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        float speed01 = Mathf.InverseLerp(0f, Mathf.Max(0.1f, burstSpeed), currentVelocity.ProjectHorizontal().magnitude);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 22f);
        bool stunt = moveMode == ChaosMoveMode.Wheelie || moveMode == ChaosMoveMode.Ram || moveMode == ChaosMoveMode.Donut;
        Color rim = Color.Lerp(speedRimColor, wheelieRimColor, stunt ? pulse : 0f);

        visualRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(RimColorId, rim);
        propertyBlock.SetFloat(RimStrengthId, Mathf.Lerp(0.15f, stunt ? 1.35f : 0.72f, speed01));
        propertyBlock.SetFloat(SpecularStrengthId, Mathf.Lerp(0.2f, 1.15f, speed01));
        propertyBlock.SetFloat(MacroNormalBendId, Mathf.Lerp(0.72f, stunt ? 1.58f : 1.18f, pulse * speed01));
        propertyBlock.SetFloat(WrapDiffuseId, Mathf.Lerp(0.18f, 0.38f, speed01));
        visualRenderer.SetPropertyBlock(propertyBlock);
    }

    private void TickDead()
    {
        currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, acceleration * Time.deltaTime);
        if (!deathFxSpawned)
        {
            deathFxSpawned = true;
            RetroGameContext.Vfx.SpawnExplosionFlash(transform.position + Vector3.up * 1.15f, speedRimColor, 1.45f, 0.18f);
        }
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        return (a - b).ProjectHorizontal().magnitude;
    }
}

internal static class RetroSkeletonMotoVectorExtensions
{
    public static Vector3 ProjectHorizontal(this Vector3 value)
    {
        value.y = 0f;
        return value;
    }
}
