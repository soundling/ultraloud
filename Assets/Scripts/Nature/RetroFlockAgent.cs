using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(35)]
public sealed class RetroFlockAgent : MonoBehaviour
{
    public enum BoundsMode
    {
        None,
        Sphere,
        Box
    }

    private static readonly List<RetroFlockAgent> Agents = new();

    [Header("Group")]
    [SerializeField] private string groupId = "Birds";
    [SerializeField] private bool simulateInEditMode;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float minSpeed = 2.8f;
    [SerializeField, Min(0.01f)] private float maxSpeed = 7.5f;
    [SerializeField, Min(0.01f)] private float maxForce = 9.5f;
    [SerializeField, Min(0.01f)] private float turnResponsiveness = 8f;
    [SerializeField] private Vector3 initialVelocity = new(0f, 0f, 4.5f);

    [Header("Direct Flight")]
    [SerializeField] private bool useCourseTargets = true;
    [SerializeField, Min(0f)] private float courseWeight = 1.65f;
    [SerializeField] private Vector2 courseRetargetInterval = new(3.2f, 6.5f);
    [SerializeField, Min(0.1f)] private float courseArrivalRadius = 3.2f;
    [SerializeField, Range(0f, 1f)] private float courseVerticalInfluence = 0.38f;
    [SerializeField, Range(0f, 1f)] private float courseRandomness = 0.32f;

    [Header("Flocking")]
    [SerializeField, Min(0.01f)] private float neighborRadius = 7.5f;
    [SerializeField, Min(0.01f)] private float separationRadius = 2.1f;
    [SerializeField, Min(0f)] private float separationWeight = 2.8f;
    [SerializeField, Min(0f)] private float alignmentWeight = 0.85f;
    [SerializeField, Min(0f)] private float cohesionWeight = 0.72f;
    [SerializeField, Min(0f)] private float wanderWeight = 0.24f;

    [Header("Home / Bounds")]
    [SerializeField] private Transform homeAnchor;
    [SerializeField] private BoundsMode boundsMode = BoundsMode.Sphere;
    [SerializeField, Min(0.1f)] private float homeRadius = 28f;
    [SerializeField] private Vector3 boundsHalfExtents = new(26f, 8f, 26f);
    [SerializeField, Min(0f)] private float boundsWeight = 2.4f;
    [SerializeField] private bool keepWithinHeightBand = true;
    [SerializeField] private Vector2 heightBand = new(3f, 16f);
    [SerializeField, Min(0f)] private float heightWeight = 1.4f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private bool avoidObstacles = true;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField, Min(0.1f)] private float obstacleProbeDistance = 5f;
    [SerializeField, Min(0f)] private float obstacleWeight = 4f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private Vector3 velocity;
    private Vector3 homePosition;
    private Vector3 courseTarget;
    private float phase;
    private float nextCourseRetargetTime;
    private double lastUpdateTime;
    private bool hasUpdateTime;
    private bool hasCourseTarget;

    private static readonly RaycastHit[] ObstacleHits = new RaycastHit[12];

    public string GroupId
    {
        get => groupId;
        set => groupId = string.IsNullOrWhiteSpace(value) ? "default" : value;
    }

    public Vector3 Velocity => velocity;

    public Vector3 HomePosition => homeAnchor != null ? homeAnchor.position : homePosition;

    private void Reset()
    {
        homePosition = transform.position;
        phase = UnityEngine.Random.value * 1000f;
        velocity = initialVelocity.sqrMagnitude > 0.0001f ? transform.TransformDirection(initialVelocity) : transform.forward * minSpeed;
        hasCourseTarget = false;
    }

    private void OnEnable()
    {
        if (!Agents.Contains(this))
        {
            Agents.Add(this);
        }

        if (homePosition == Vector3.zero)
        {
            homePosition = transform.position;
        }

        if (velocity.sqrMagnitude < 0.0001f)
        {
            velocity = initialVelocity.sqrMagnitude > 0.0001f ? transform.TransformDirection(initialVelocity) : transform.forward * minSpeed;
        }

        phase = phase == 0f ? UnityEngine.Random.value * 1000f : phase;
        hasUpdateTime = false;
        hasCourseTarget = false;
    }

    private void OnDisable()
    {
        Agents.Remove(this);
    }

    private void OnValidate()
    {
        minSpeed = Mathf.Max(0f, minSpeed);
        maxSpeed = Mathf.Max(0.01f, maxSpeed);
        if (maxSpeed < minSpeed)
        {
            maxSpeed = minSpeed;
        }

        maxForce = Mathf.Max(0.01f, maxForce);
        turnResponsiveness = Mathf.Max(0.01f, turnResponsiveness);
        courseWeight = Mathf.Max(0f, courseWeight);
        courseRetargetInterval.x = Mathf.Max(0.1f, courseRetargetInterval.x);
        courseRetargetInterval.y = Mathf.Max(courseRetargetInterval.x, courseRetargetInterval.y);
        courseArrivalRadius = Mathf.Max(0.1f, courseArrivalRadius);
        neighborRadius = Mathf.Max(0.01f, neighborRadius);
        separationRadius = Mathf.Clamp(separationRadius, 0.01f, neighborRadius);
        homeRadius = Mathf.Max(0.1f, homeRadius);
        boundsHalfExtents.x = Mathf.Max(0.1f, boundsHalfExtents.x);
        boundsHalfExtents.y = Mathf.Max(0.1f, boundsHalfExtents.y);
        boundsHalfExtents.z = Mathf.Max(0.1f, boundsHalfExtents.z);
        if (heightBand.y < heightBand.x)
        {
            heightBand.y = heightBand.x;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying && !simulateInEditMode)
        {
            hasUpdateTime = false;
            return;
        }

        Step(ResolveDeltaTime());
    }

    public void SetVelocity(Vector3 worldVelocity)
    {
        velocity = ClampVelocity(worldVelocity);
        initialVelocity = transform.InverseTransformDirection(velocity);
        hasCourseTarget = false;
    }

    public void SetHome(Vector3 worldPosition)
    {
        homePosition = worldPosition;
        hasCourseTarget = false;
    }

    public void RandomizePhase(float seed)
    {
        phase = seed;
    }

    public void Step(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        EnsureCourseTarget();

        Vector3 steering = ComputeCourseSteering();
        steering += ComputeFlockingSteering();
        steering += ComputeBoundsSteering();
        steering += ComputeHeightSteering();
        steering += ComputeObstacleSteering();
        steering += ComputeWanderSteering();
        steering = Vector3.ClampMagnitude(steering, maxForce);

        velocity = ClampVelocity(velocity + steering * deltaTime);
        transform.position += velocity * deltaTime;

        if (velocity.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            float rotationBlend = 1f - Mathf.Exp(-turnResponsiveness * deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationBlend);
        }
    }

    private float ResolveDeltaTime()
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if (!hasUpdateTime)
        {
            lastUpdateTime = now;
            hasUpdateTime = true;
            return Application.isPlaying ? Mathf.Max(Time.deltaTime, 0.0001f) : 0.016f;
        }

        float deltaTime = Mathf.Clamp((float)(now - lastUpdateTime), 0.0001f, 0.05f);
        lastUpdateTime = now;
        return deltaTime;
    }

    private Vector3 ComputeFlockingSteering()
    {
        Vector3 position = transform.position;
        float neighborRadiusSqr = neighborRadius * neighborRadius;
        float separationRadiusSqr = separationRadius * separationRadius;
        Vector3 separation = Vector3.zero;
        Vector3 alignment = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        int neighborCount = 0;
        int separationCount = 0;
        string resolvedGroupId = ResolveGroupId(groupId);

        for (int i = 0; i < Agents.Count; i++)
        {
            RetroFlockAgent other = Agents[i];
            if (other == null || other == this || !other.isActiveAndEnabled || ResolveGroupId(other.groupId) != resolvedGroupId)
            {
                continue;
            }

            Vector3 toOther = other.transform.position - position;
            float distanceSqr = toOther.sqrMagnitude;
            if (distanceSqr > neighborRadiusSqr || distanceSqr < 0.0001f)
            {
                continue;
            }

            neighborCount++;
            alignment += other.velocity;
            cohesion += other.transform.position;

            if (distanceSqr <= separationRadiusSqr)
            {
                float distance = Mathf.Sqrt(distanceSqr);
                separation -= toOther / Mathf.Max(distance * distance, 0.001f);
                separationCount++;
            }
        }

        Vector3 steering = Vector3.zero;
        if (separationCount > 0 && separationWeight > 0f)
        {
            steering += SteerTowards(separation / separationCount) * separationWeight;
        }

        if (neighborCount > 0)
        {
            if (alignmentWeight > 0f)
            {
                steering += SteerTowards(alignment / neighborCount) * alignmentWeight;
            }

            if (cohesionWeight > 0f)
            {
                Vector3 center = cohesion / neighborCount;
                steering += SteerTowards(center - position) * cohesionWeight;
            }
        }

        return steering;
    }

    private Vector3 ComputeCourseSteering()
    {
        if (!useCourseTargets || courseWeight <= 0f)
        {
            return Vector3.zero;
        }

        Vector3 toTarget = courseTarget - transform.position;
        if (toTarget.magnitude <= courseArrivalRadius || Time.realtimeSinceStartup >= nextCourseRetargetTime)
        {
            PickCourseTarget(false);
            toTarget = courseTarget - transform.position;
        }

        toTarget.y *= courseVerticalInfluence;
        return SteerTowards(toTarget) * courseWeight;
    }

    private Vector3 ComputeBoundsSteering()
    {
        if (boundsMode == BoundsMode.None || boundsWeight <= 0f)
        {
            return Vector3.zero;
        }

        Vector3 home = HomePosition;
        Vector3 offset = transform.position - home;
        if (boundsMode == BoundsMode.Sphere)
        {
            float distance = offset.magnitude;
            if (distance <= homeRadius * 0.72f)
            {
                return Vector3.zero;
            }

            float strength = Mathf.InverseLerp(homeRadius * 0.72f, homeRadius, distance);
            return SteerTowards(-offset) * boundsWeight * strength;
        }

        Vector3 normalized = new(
            boundsHalfExtents.x > 0f ? offset.x / boundsHalfExtents.x : 0f,
            boundsHalfExtents.y > 0f ? offset.y / boundsHalfExtents.y : 0f,
            boundsHalfExtents.z > 0f ? offset.z / boundsHalfExtents.z : 0f);
        float outside = Mathf.Max(Mathf.Abs(normalized.x), Mathf.Abs(normalized.y), Mathf.Abs(normalized.z));
        if (outside <= 0.72f)
        {
            return Vector3.zero;
        }

        Vector3 target = new(
            -Mathf.Sign(normalized.x) * Mathf.Max(0f, Mathf.Abs(normalized.x) - 0.72f),
            -Mathf.Sign(normalized.y) * Mathf.Max(0f, Mathf.Abs(normalized.y) - 0.72f),
            -Mathf.Sign(normalized.z) * Mathf.Max(0f, Mathf.Abs(normalized.z) - 0.72f));
        return SteerTowards(target) * boundsWeight * Mathf.InverseLerp(0.72f, 1f, outside);
    }

    private Vector3 ComputeHeightSteering()
    {
        if (!keepWithinHeightBand || heightWeight <= 0f)
        {
            return Vector3.zero;
        }

        float y = transform.position.y;
        if (y < heightBand.x)
        {
            return SteerTowards(Vector3.up) * heightWeight * Mathf.InverseLerp(heightBand.x, heightBand.x - 4f, y);
        }

        if (y > heightBand.y)
        {
            return SteerTowards(Vector3.down) * heightWeight * Mathf.InverseLerp(heightBand.y, heightBand.y + 4f, y);
        }

        return Vector3.zero;
    }

    private Vector3 ComputeObstacleSteering()
    {
        if (!avoidObstacles || obstacleWeight <= 0f || obstacleProbeDistance <= 0f || velocity.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 origin = transform.position;
        Vector3 forward = velocity.normalized;
        if (!TryFindObstacle(origin, forward, out RaycastHit hit))
        {
            return Vector3.zero;
        }

        Vector3 away = Vector3.ProjectOnPlane(forward, hit.normal).normalized + hit.normal;
        float strength = 1f - Mathf.Clamp01(hit.distance / obstacleProbeDistance);
        return SteerTowards(away) * obstacleWeight * strength;
    }

    private Vector3 ComputeWanderSteering()
    {
        if (wanderWeight <= 0f)
        {
            return Vector3.zero;
        }

        float t = Time.realtimeSinceStartup * 0.31f + phase;
        Vector3 noise = new(
            Mathf.PerlinNoise(t, phase * 0.13f) - 0.5f,
            (Mathf.PerlinNoise(phase * 0.71f, t * 0.83f) - 0.5f) * 0.42f,
            Mathf.PerlinNoise(t * 0.57f, phase * 0.37f) - 0.5f);
        Vector3 horizontalNoise = Vector3.ProjectOnPlane(noise, Vector3.up);
        if (horizontalNoise.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 forward = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : transform.forward;
        Vector3 desired = forward + horizontalNoise.normalized * 0.32f + Vector3.up * Mathf.Clamp(noise.y, -0.18f, 0.18f);
        return SteerTowards(desired) * wanderWeight;
    }

    private void EnsureCourseTarget()
    {
        if (!useCourseTargets || hasCourseTarget)
        {
            return;
        }

        PickCourseTarget(true);
    }

    private void PickCourseTarget(bool preferCurrentHeading)
    {
        Vector3 home = HomePosition;
        Vector3 forward = Vector3.ProjectOnPlane(velocity.sqrMagnitude > 0.0001f ? velocity : transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle;
        Vector3 randomDirection = new(randomCircle.x, 0f, randomCircle.y);
        if (randomDirection.sqrMagnitude < 0.0001f)
        {
            randomDirection = Quaternion.Euler(0f, UnityEngine.Random.Range(-45f, 45f), 0f) * forward;
        }

        randomDirection.Normalize();
        float randomness = preferCurrentHeading ? Mathf.Min(courseRandomness, 0.18f) : courseRandomness;
        Vector3 direction = Vector3.Slerp(forward, randomDirection, Mathf.Clamp01(randomness));
        float distance = ResolveCourseDistance(preferCurrentHeading);

        courseTarget = home + direction * distance;
        courseTarget.y = ResolveCourseTargetHeight(home, preferCurrentHeading);
        nextCourseRetargetTime = Time.realtimeSinceStartup + UnityEngine.Random.Range(courseRetargetInterval.x, courseRetargetInterval.y);
        hasCourseTarget = true;
    }

    private float ResolveCourseDistance(bool preferCurrentHeading)
    {
        if (boundsMode == BoundsMode.Box)
        {
            float horizontalExtent = Mathf.Min(boundsHalfExtents.x, boundsHalfExtents.z);
            return Mathf.Max(courseArrivalRadius * 1.5f, horizontalExtent * (preferCurrentHeading ? 0.78f : UnityEngine.Random.Range(0.45f, 0.82f)));
        }

        float radius = Mathf.Max(homeRadius, courseArrivalRadius * 2f);
        return radius * (preferCurrentHeading ? 0.78f : UnityEngine.Random.Range(0.42f, 0.82f));
    }

    private float ResolveCourseTargetHeight(Vector3 home, bool preferCurrentHeading)
    {
        if (!keepWithinHeightBand)
        {
            return preferCurrentHeading
                ? transform.position.y
                : home.y + UnityEngine.Random.Range(-boundsHalfExtents.y * 0.45f, boundsHalfExtents.y * 0.45f);
        }

        float min = heightBand.x + 0.75f;
        float max = Mathf.Max(min, heightBand.y - 0.75f);
        if (preferCurrentHeading)
        {
            return Mathf.Clamp(transform.position.y, min, max);
        }

        return UnityEngine.Random.Range(min, max);
    }

    private bool TryFindObstacle(Vector3 origin, Vector3 forward, out RaycastHit bestHit)
    {
        bestHit = default;
        int hitCount = Physics.RaycastNonAlloc(origin, forward, ObstacleHits, obstacleProbeDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;
        bool found = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = ObstacleHits[i];
            Collider candidateCollider = candidate.collider;
            if (candidateCollider == null || ShouldIgnoreObstacle(candidateCollider) || candidate.distance >= bestDistance)
            {
                continue;
            }

            bestHit = candidate;
            bestDistance = candidate.distance;
            found = true;
        }

        return found;
    }

    private bool ShouldIgnoreObstacle(Collider candidate)
    {
        if (candidate == null)
        {
            return true;
        }

        Transform candidateTransform = candidate.transform;
        if (candidateTransform == transform || candidateTransform.IsChildOf(transform))
        {
            return true;
        }

        RetroFlockAgent otherAgent = candidate.GetComponentInParent<RetroFlockAgent>();
        return otherAgent != null;
    }

    private Vector3 SteerTowards(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 desired = direction.normalized * maxSpeed;
        return Vector3.ClampMagnitude(desired - velocity, maxForce);
    }

    private Vector3 ClampVelocity(Vector3 candidate)
    {
        if (candidate.sqrMagnitude < 0.0001f)
        {
            candidate = transform.forward * Mathf.Max(minSpeed, 0.01f);
        }

        float speed = candidate.magnitude;
        float targetSpeed = Mathf.Clamp(speed, minSpeed, maxSpeed);
        return candidate.normalized * targetSpeed;
    }

    private static string ResolveGroupId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "default" : value;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.65f, 1f, 0.35f);
        Vector3 home = HomePosition;
        if (boundsMode == BoundsMode.Sphere)
        {
            Gizmos.DrawWireSphere(home, homeRadius);
        }
        else if (boundsMode == BoundsMode.Box)
        {
            Gizmos.DrawWireCube(home, boundsHalfExtents * 2f);
        }

        Gizmos.color = new Color(1f, 0.85f, 0.25f, 0.75f);
        Gizmos.DrawLine(transform.position, transform.position + velocity);
    }
}
