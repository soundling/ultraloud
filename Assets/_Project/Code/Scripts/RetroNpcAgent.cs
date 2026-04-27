using IDisposable = System.IDisposable;
using UnityEngine;
using UnityEngine.AI;

public enum RetroNpcBehaviorMode
{
    Passive = 0,
    Defensive = 1,
    Aggressive = 2,
    Skittish = 3
}

public enum RetroNpcIdleMode
{
    Stationary = 0,
    Wander = 1,
    Patrol = 2
}

public enum RetroNpcNavigationMode
{
    Auto = 0,
    Transform = 1,
    Rigidbody = 2,
    NavMeshAgent = 3
}

public enum RetroNpcBrainState
{
    Idle = 0,
    Patrol = 1,
    Chase = 2,
    Attack = 3,
    Flee = 4,
    Dead = 5
}

[DisallowMultipleComponent]
public sealed class RetroNpcAgent : MonoBehaviour
{
    [Header("Behavior")]
    [SerializeField] private RetroNpcBehaviorMode behaviorMode = RetroNpcBehaviorMode.Defensive;
    [SerializeField] private RetroNpcIdleMode idleMode = RetroNpcIdleMode.Wander;
    [SerializeField] private bool startProvoked;
    [SerializeField] private bool provokeOnDamage = true;
    [SerializeField] private bool calmAfterLosingTarget = true;

    [Header("Targeting")]
    [SerializeField] private Transform target;
    [SerializeField] private bool preferFpsControllerTarget = true;
    [SerializeField] private string targetTag = "Player";
    [SerializeField, Min(0f)] private float awarenessRadius = 14f;
    [SerializeField, Min(0f)] private float loseTargetRadius = 24f;
    [SerializeField, Range(1f, 360f)] private float fieldOfView = 170f;
    [SerializeField] private bool requireLineOfSight;
    [SerializeField] private LayerMask lineOfSightMask = ~0;
    [SerializeField, Min(0.02f)] private float targetSearchInterval = 0.35f;
    [SerializeField, Min(0f)] private float eyeHeight = 0.85f;
    [SerializeField, Min(0f)] private float targetAimHeight = 1f;

    [Header("Navigation")]
    [SerializeField] private RetroNpcNavigationMode navigationMode = RetroNpcNavigationMode.Auto;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private Rigidbody movementBody;
    [SerializeField, Min(0f)] private float moveSpeed = 2.4f;
    [SerializeField, Min(0f)] private float acceleration = 12f;
    [SerializeField, Min(0f)] private float turnSpeed = 540f;
    [SerializeField, Min(0f)] private float stoppingDistance = 1.35f;
    [SerializeField, Min(0.02f)] private float repathInterval = 0.18f;
    [SerializeField] private bool horizontalOnly = true;
    [SerializeField] private bool autoCreateNavMeshAgent = true;
    [SerializeField, Min(0.01f)] private float navAgentRadius = 0.42f;
    [SerializeField, Min(0.1f)] private float navAgentHeight = 1.8f;
    [SerializeField] private ObstacleAvoidanceType obstacleAvoidance = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    [SerializeField, Range(0, 99)] private int avoidancePriority = 50;
    [SerializeField] private bool staggerAvoidancePriority = true;
    [SerializeField, Range(0, 49)] private int avoidancePrioritySpread = 20;

    [Header("Combat")]
    [SerializeField, Min(0f)] private float attackRange = 1.65f;
    [SerializeField, Min(0f)] private float attackDamage = 8f;
    [SerializeField, Min(0.05f)] private float attackCooldown = 1.2f;
    [SerializeField, Min(0f)] private float fleeDistance = 6f;

    [Header("Idle Navigation")]
    [SerializeField] private Transform[] patrolPoints = new Transform[0];
    [SerializeField] private bool loopPatrol = true;
    [SerializeField, Min(0.05f)] private float patrolPointTolerance = 0.45f;
    [SerializeField, Min(0f)] private float wanderRadius = 5f;
    [SerializeField, Min(0.05f)] private float wanderPointTolerance = 0.45f;
    [SerializeField] private Vector2 idleWaitRange = new Vector2(1.25f, 3f);

    [Header("Doors")]
    [SerializeField] private bool useBuildingDoors = true;
    [SerializeField] private bool detectStartingInterior = true;
    [SerializeField, Min(0.25f)] private float doorSearchRadius = 9f;
    [SerializeField, Min(0.05f)] private float doorUseDistance = 0.8f;
    [SerializeField, Min(0.05f)] private float doorUseCooldown = 1.2f;
    [SerializeField, Range(0f, 1f)] private float idleDoorUseChance = 0.16f;
    [SerializeField, Min(0f)] private float interiorWanderMargin = 0.58f;

    [Header("References")]
    [SerializeField] private RetroDamageable damageable;

    private Vector3 homePosition;
    private Vector3 currentDestination;
    private Vector3 idleDestination;
    private Vector3 lastMoveDirection;
    private bool hasDestination;
    private bool hasIdleDestination;
    private bool provoked;
    private int patrolIndex;
    private float nextTargetSearchTime;
    private float nextRepathTime;
    private float nextAttackTime;
    private float idleUntilTime;
    private bool subscribedToDamageable;
    private IDisposable damageEventSubscription;
    private RetroNpcBrainState state = RetroNpcBrainState.Idle;
    private int resolvedAvoidancePriority;
    private bool avoidancePriorityResolved;
    private RetroHybridBuilding currentBuilding;
    private RetroBuildingDoorInteractable pendingDoor;
    private float nextDoorUseTime;
    private bool navMeshDisabledForInterior;

    public RetroNpcBehaviorMode BehaviorMode => behaviorMode;
    public RetroNpcBrainState CurrentState => state;
    public Transform Target => target;
    public bool IsProvoked => provoked;
    public Vector3 HomePosition => homePosition;

    private void Reset()
    {
        AutoAssignReferences(true);
        homePosition = transform.position;
    }

    private void Awake()
    {
        AutoAssignReferences(true);
        homePosition = transform.position;
        provoked = startProvoked;
        RefreshBuildingMembership(true);
        ConfigureNavMeshAgent();
    }

    private void OnEnable()
    {
        AutoAssignReferences(true);
        homePosition = transform.position;
        provoked = startProvoked;
        hasIdleDestination = false;
        idleUntilTime = Time.time + GetIdleWait();
        RefreshBuildingMembership(true);
        SubscribeToDamageable();
        SubscribeToGameplayEvents();
        ConfigureNavMeshAgent();
    }

    private void OnDisable()
    {
        UnsubscribeFromDamageable();
        UnsubscribeFromGameplayEvents();
        StopMovement();
    }

    private void OnValidate()
    {
        awarenessRadius = Mathf.Max(0f, awarenessRadius);
        loseTargetRadius = Mathf.Max(awarenessRadius, loseTargetRadius);
        targetSearchInterval = Mathf.Max(0.02f, targetSearchInterval);
        eyeHeight = Mathf.Max(0f, eyeHeight);
        targetAimHeight = Mathf.Max(0f, targetAimHeight);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        turnSpeed = Mathf.Max(0f, turnSpeed);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
        repathInterval = Mathf.Max(0.02f, repathInterval);
        navAgentRadius = Mathf.Max(0.01f, navAgentRadius);
        navAgentHeight = Mathf.Max(navAgentRadius * 2f, navAgentHeight);
        avoidancePriority = Mathf.Clamp(avoidancePriority, 0, 99);
        avoidancePrioritySpread = Mathf.Clamp(avoidancePrioritySpread, 0, 49);
        avoidancePriorityResolved = false;
        attackRange = Mathf.Max(0f, attackRange);
        attackDamage = Mathf.Max(0f, attackDamage);
        attackCooldown = Mathf.Max(0.05f, attackCooldown);
        fleeDistance = Mathf.Max(0f, fleeDistance);
        patrolPointTolerance = Mathf.Max(0.05f, patrolPointTolerance);
        wanderRadius = Mathf.Max(0f, wanderRadius);
        wanderPointTolerance = Mathf.Max(0.05f, wanderPointTolerance);
        idleWaitRange.x = Mathf.Max(0f, idleWaitRange.x);
        idleWaitRange.y = Mathf.Max(idleWaitRange.x, idleWaitRange.y);
        doorSearchRadius = Mathf.Max(0.25f, doorSearchRadius);
        doorUseDistance = Mathf.Max(0.05f, doorUseDistance);
        doorUseCooldown = Mathf.Max(0.05f, doorUseCooldown);
        interiorWanderMargin = Mathf.Max(0f, interiorWanderMargin);
        AutoAssignReferences(false);
        ConfigureNavMeshAgent();
    }

    private void Update()
    {
        TickBrain();
    }

    public void SetTarget(Transform newTarget, bool makeProvoked)
    {
        target = newTarget;
        if (makeProvoked)
        {
            Provoke(newTarget);
        }
    }

    public void Provoke(Transform source)
    {
        if (behaviorMode == RetroNpcBehaviorMode.Passive)
        {
            return;
        }

        provoked = true;
        if (source != null)
        {
            target = source;
        }
        else
        {
            AcquireTarget(true);
        }

        PublishAlert(target, 1f);
    }

    public void Calm()
    {
        provoked = startProvoked;
        if (!provoked)
        {
            target = null;
        }

        hasIdleDestination = false;
        SetState(RetroNpcBrainState.Idle);
        StopMovement();
    }

    private void TickBrain()
    {
        if (damageable != null && damageable.IsDead)
        {
            SetState(RetroNpcBrainState.Dead);
            StopMovement();
            return;
        }

        RefreshBuildingMembership(false);
        ResolveTargetForCurrentBehavior();

        switch (behaviorMode)
        {
            case RetroNpcBehaviorMode.Passive:
                TickIdleNavigation();
                break;
            case RetroNpcBehaviorMode.Skittish:
                if (provoked && HasUsableTarget())
                {
                    TickFlee();
                }
                else
                {
                    TickIdleNavigation();
                }
                break;
            case RetroNpcBehaviorMode.Aggressive:
                if (HasUsableTarget())
                {
                    TickCombat();
                }
                else
                {
                    TickIdleNavigation();
                }
                break;
            default:
                if (provoked && HasUsableTarget())
                {
                    TickCombat();
                }
                else
                {
                    TickIdleNavigation();
                }
                break;
        }
    }

    private void ResolveTargetForCurrentBehavior()
    {
        if (target != null && IsTargetLost(target))
        {
            ClearLostTarget();
        }

        if (behaviorMode == RetroNpcBehaviorMode.Passive)
        {
            return;
        }

        if (behaviorMode == RetroNpcBehaviorMode.Aggressive)
        {
            if (target == null || !IsTargetVisibleOrForced(target, false))
            {
                AcquireTarget(false);
            }

            if (target != null)
            {
                provoked = true;
            }

            return;
        }

        if (behaviorMode == RetroNpcBehaviorMode.Skittish)
        {
            if (!provoked)
            {
                AcquireTarget(false);
                if (target != null)
                {
                    provoked = true;
                }
            }
            else if (target == null)
            {
                AcquireTarget(true);
            }

            return;
        }

        if (provoked && target == null)
        {
            AcquireTarget(true);
        }
    }

    private void TickCombat()
    {
        Vector3 targetPoint = ResolveTargetPoint(target);
        float distance = HorizontalDistance(transform.position, targetPoint);
        if (distance <= attackRange)
        {
            SetState(RetroNpcBrainState.Attack);
            StopMovement();
            FacePosition(targetPoint);
            TryAttack(targetPoint);
            return;
        }

        SetState(RetroNpcBrainState.Chase);
        MoveTo(targetPoint, Mathf.Max(0.05f, stoppingDistance));
    }

    private void TickFlee()
    {
        Vector3 targetPoint = ResolveTargetPoint(target);
        Vector3 away = transform.position - targetPoint;
        if (horizontalOnly)
        {
            away = Vector3.ProjectOnPlane(away, Vector3.up);
        }

        if (away.sqrMagnitude < 0.0001f)
        {
            away = -transform.forward;
        }

        Vector3 destination = transform.position + away.normalized * Mathf.Max(0.1f, fleeDistance);
        SetState(RetroNpcBrainState.Flee);
        MoveTo(destination, 0.05f);

        if (calmAfterLosingTarget && target != null && HorizontalDistance(transform.position, targetPoint) >= loseTargetRadius * 0.85f)
        {
            ClearLostTarget();
        }
    }

    private void TickIdleNavigation()
    {
        switch (idleMode)
        {
            case RetroNpcIdleMode.Patrol:
                TickPatrol();
                break;
            case RetroNpcIdleMode.Wander:
                TickWander();
                break;
            default:
                SetState(RetroNpcBrainState.Idle);
                StopMovement();
                break;
        }
    }

    private void TickPatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            TickWander();
            return;
        }

        Transform waypoint = FindNextValidPatrolPoint();
        if (waypoint == null)
        {
            SetState(RetroNpcBrainState.Idle);
            StopMovement();
            return;
        }

        float distance = HorizontalDistance(transform.position, waypoint.position);
        if (distance <= patrolPointTolerance)
        {
            StopMovement();
            SetState(RetroNpcBrainState.Idle);

            if (Time.time >= idleUntilTime)
            {
                AdvancePatrolIndex();
                idleUntilTime = Time.time + GetIdleWait();
            }

            return;
        }

        SetState(RetroNpcBrainState.Patrol);
        MoveTo(waypoint.position, patrolPointTolerance);
    }

    private void TickWander()
    {
        if (wanderRadius <= 0f)
        {
            SetState(RetroNpcBrainState.Idle);
            StopMovement();
            return;
        }

        if (!hasIdleDestination)
        {
            if (Time.time < idleUntilTime)
            {
                SetState(RetroNpcBrainState.Idle);
                StopMovement();
                return;
            }

            PickWanderDestination();
        }

        float distance = HorizontalDistance(transform.position, idleDestination);
        if (distance <= wanderPointTolerance)
        {
            hasIdleDestination = false;
            idleUntilTime = Time.time + GetIdleWait();
            SetState(RetroNpcBrainState.Idle);
            StopMovement();
            return;
        }

        SetState(RetroNpcBrainState.Patrol);
        MoveTo(idleDestination, wanderPointTolerance);
    }

    private void TryAttack(Vector3 targetPoint)
    {
        if (attackDamage <= 0f || Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackCooldown;

        RetroDamageable targetDamageable = ResolveTargetDamageable();
        if (targetDamageable != null && targetDamageable != damageable && !targetDamageable.IsDead)
        {
            Vector3 normal = transform.position - targetPoint;
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = -transform.forward;
            }

            targetDamageable.ApplyDamage(attackDamage, targetPoint, normal.normalized, gameObject);
            return;
        }

        if (target != null)
        {
            target.SendMessageUpwards("ApplyDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
        }
    }

    private RetroDamageable ResolveTargetDamageable()
    {
        if (target == null)
        {
            return null;
        }

        RetroDamageable targetDamageable = target.GetComponentInParent<RetroDamageable>();
        if (targetDamageable == null)
        {
            targetDamageable = target.GetComponentInChildren<RetroDamageable>();
        }

        return targetDamageable;
    }

    private void MoveTo(Vector3 destination, float stopDistance)
    {
        if (TryMoveThroughDoor(destination, stopDistance))
        {
            return;
        }

        MoveToDirect(destination, stopDistance);
    }

    private void MoveToDirect(Vector3 destination, float stopDistance)
    {
        currentDestination = destination;
        hasDestination = true;

        Vector3 toDestination = destination - transform.position;
        if (horizontalOnly)
        {
            toDestination = Vector3.ProjectOnPlane(toDestination, Vector3.up);
        }

        if (toDestination.sqrMagnitude <= stopDistance * stopDistance)
        {
            StopMovement();
            return;
        }

        RetroNpcNavigationMode resolvedMode = ResolveNavigationMode();
        switch (resolvedMode)
        {
            case RetroNpcNavigationMode.NavMeshAgent:
                MoveWithNavMeshAgent(destination, stopDistance);
                break;
            case RetroNpcNavigationMode.Rigidbody:
                MoveWithRigidbody(toDestination.normalized);
                break;
            default:
                MoveWithTransform(toDestination.normalized);
                break;
        }
    }

    private void MoveWithTransform(Vector3 direction)
    {
        Vector3 velocity = direction * moveSpeed;
        transform.position += velocity * Time.deltaTime;
        FaceDirection(direction);
        lastMoveDirection = direction;
    }

    private void MoveWithRigidbody(Vector3 direction)
    {
        if (movementBody == null)
        {
            MoveWithTransform(direction);
            return;
        }

        Vector3 velocity = direction * moveSpeed;
        if (movementBody.isKinematic)
        {
            movementBody.MovePosition(movementBody.position + velocity * Time.deltaTime);
        }
        else
        {
            Vector3 nextVelocity = velocity;
            if (horizontalOnly)
            {
                nextVelocity.y = movementBody.linearVelocity.y;
            }

            movementBody.linearVelocity = nextVelocity;
        }

        FaceDirection(direction);
        lastMoveDirection = direction;
    }

    private void MoveWithNavMeshAgent(Vector3 destination, float stopDistance)
    {
        if (!CanUseNavMeshAgent())
        {
            MoveWithTransform((destination - transform.position).normalized);
            return;
        }

        navMeshAgent.speed = moveSpeed;
        navMeshAgent.acceleration = Mathf.Max(0.01f, acceleration);
        navMeshAgent.angularSpeed = turnSpeed;
        navMeshAgent.stoppingDistance = stopDistance;
        navMeshAgent.updateRotation = false;
        navMeshAgent.radius = navAgentRadius;
        navMeshAgent.height = navAgentHeight;
        navMeshAgent.obstacleAvoidanceType = obstacleAvoidance;
        navMeshAgent.avoidancePriority = ResolveAvoidancePriority();
        navMeshAgent.isStopped = false;

        if (Time.time >= nextRepathTime || (navMeshAgent.destination - destination).sqrMagnitude > 0.25f)
        {
            navMeshAgent.SetDestination(destination);
            nextRepathTime = Time.time + repathInterval;
        }

        Vector3 desired = navMeshAgent.desiredVelocity;
        if (horizontalOnly)
        {
            desired = Vector3.ProjectOnPlane(desired, Vector3.up);
        }

        if (desired.sqrMagnitude > 0.0001f)
        {
            FaceDirection(desired.normalized);
            lastMoveDirection = desired.normalized;
        }
    }

    private void StopMovement()
    {
        hasDestination = false;

        if (CanUseNavMeshAgent())
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath();
        }

        if (movementBody != null && !movementBody.isKinematic)
        {
            Vector3 velocity = movementBody.linearVelocity;
            if (horizontalOnly)
            {
                velocity.x = 0f;
                velocity.z = 0f;
            }
            else
            {
                velocity = Vector3.zero;
            }

            movementBody.linearVelocity = velocity;
        }
    }

    public void NotifyTeleportedByDoor(RetroBuildingDoorInteractable door)
    {
        pendingDoor = null;
        hasIdleDestination = false;
        idleUntilTime = Time.time + GetIdleWait() * 0.35f;
        nextDoorUseTime = Time.time + doorUseCooldown;
        SetCurrentBuilding(door != null ? door.ResolveDestinationBuilding() : RetroHybridBuilding.FindInteriorContaining(transform.position, 0.08f));
        homePosition = transform.position;
        StopMovement();
    }

    private bool TryMoveThroughDoor(Vector3 destination, float stopDistance)
    {
        if (!useBuildingDoors)
        {
            return false;
        }

        if (pendingDoor != null)
        {
            return MoveTowardDoor(pendingDoor);
        }

        RetroHybridBuilding sourceBuilding = ResolveInteriorBuildingAt(transform.position);
        RetroHybridBuilding destinationBuilding = ResolveInteriorBuildingAt(destination);
        if (sourceBuilding == destinationBuilding)
        {
            return false;
        }

        RetroBuildingDoorInteractable routeDoor = null;
        if (sourceBuilding != null)
        {
            routeDoor = FindBestDoor(sourceBuilding, RetroBuildingDoorSide.Interior, destination, true);
        }
        else if (destinationBuilding != null)
        {
            routeDoor = FindBestDoor(destinationBuilding, RetroBuildingDoorSide.Exterior, destination, true);
        }

        if (routeDoor == null || !routeDoor.CanNpcUse(gameObject))
        {
            return false;
        }

        pendingDoor = routeDoor;
        return MoveTowardDoor(routeDoor);
    }

    private bool MoveTowardDoor(RetroBuildingDoorInteractable door)
    {
        if (door == null || !door.CanNpcUse(gameObject))
        {
            pendingDoor = null;
            return false;
        }

        Vector3 approachPosition = door.NpcApproachPosition;
        if (HorizontalDistance(transform.position, approachPosition) <= doorUseDistance)
        {
            StopMovement();
            FacePosition(door.transform.position);
            if (Time.time < nextDoorUseTime)
            {
                return true;
            }

            if (door.TryUseByNpc(gameObject))
            {
                return true;
            }

            pendingDoor = null;
            nextDoorUseTime = Time.time + doorUseCooldown;
            return false;
        }

        MoveToDirect(approachPosition, doorUseDistance);
        return true;
    }

    private bool TryPickIdleDoorDestination()
    {
        if (!useBuildingDoors
            || Time.time < nextDoorUseTime
            || idleDoorUseChance <= 0f
            || Random.value > idleDoorUseChance)
        {
            return false;
        }

        RetroBuildingDoorInteractable door = IsInsideBuildingInterior()
            ? FindBestDoor(currentBuilding, RetroBuildingDoorSide.Interior, transform.position, true)
            : FindBestDoor(null, RetroBuildingDoorSide.Exterior, transform.position, false);
        if (door == null || !door.CanNpcUse(gameObject))
        {
            return false;
        }

        pendingDoor = door;
        idleDestination = door.NpcApproachPosition;
        hasIdleDestination = true;
        return true;
    }

    private RetroBuildingDoorInteractable FindBestDoor(
        RetroHybridBuilding ownerBuilding,
        RetroBuildingDoorSide side,
        Vector3 scorePosition,
        bool allowBeyondSearchRadius)
    {
        RetroBuildingDoorInteractable[] doors = Object.FindObjectsByType<RetroBuildingDoorInteractable>(FindObjectsInactive.Exclude);
        RetroBuildingDoorInteractable bestDoor = null;
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < doors.Length; i++)
        {
            RetroBuildingDoorInteractable door = doors[i];
            if (door == null
                || door.DoorSide != side
                || !door.CanNpcUse(gameObject))
            {
                continue;
            }

            if (ownerBuilding != null && door.Building != ownerBuilding)
            {
                continue;
            }

            float approachDistance = HorizontalDistance(transform.position, door.NpcApproachPosition);
            if (!allowBeyondSearchRadius && approachDistance > doorSearchRadius)
            {
                continue;
            }

            float destinationScore = HorizontalDistance(door.DestinationPosition, scorePosition) * 0.18f;
            float score = approachDistance + destinationScore;
            if (score < bestScore)
            {
                bestScore = score;
                bestDoor = door;
            }
        }

        return bestDoor;
    }

    private void RefreshBuildingMembership(bool force)
    {
        if (!detectStartingInterior && currentBuilding == null && !force)
        {
            return;
        }

        RetroHybridBuilding containing = ResolveInteriorBuildingAt(transform.position);
        SetCurrentBuilding(containing);
        if (force && containing != null)
        {
            homePosition = transform.position;
        }
    }

    private RetroHybridBuilding ResolveInteriorBuildingAt(Vector3 position)
    {
        if (currentBuilding != null && currentBuilding.ContainsInteriorWorldPosition(position, 0.12f))
        {
            return currentBuilding;
        }

        return RetroHybridBuilding.FindInteriorContaining(position, 0.12f);
    }

    private bool IsInsideBuildingInterior()
    {
        return currentBuilding != null && currentBuilding.ContainsInteriorWorldPosition(transform.position, 0.16f);
    }

    private void SetCurrentBuilding(RetroHybridBuilding building)
    {
        if (currentBuilding == building)
        {
            ApplyInteriorNavMeshState();
            return;
        }

        currentBuilding = building;
        ApplyInteriorNavMeshState();
    }

    private void ApplyInteriorNavMeshState()
    {
        bool inside = currentBuilding != null;
        if (inside)
        {
            if (navMeshAgent != null && navMeshAgent.enabled)
            {
                if (navMeshAgent.isOnNavMesh)
                {
                    navMeshAgent.ResetPath();
                }

                navMeshAgent.enabled = false;
                navMeshDisabledForInterior = true;
            }

            return;
        }

        if (navMeshDisabledForInterior && navMeshAgent != null)
        {
            navMeshAgent.enabled = true;
            navMeshDisabledForInterior = false;
            ConfigureNavMeshAgent();
            nextRepathTime = 0f;
        }
    }

    private RetroNpcNavigationMode ResolveNavigationMode()
    {
        if (IsInsideBuildingInterior())
        {
            return movementBody != null && navigationMode != RetroNpcNavigationMode.Transform
                ? RetroNpcNavigationMode.Rigidbody
                : RetroNpcNavigationMode.Transform;
        }

        if (navigationMode == RetroNpcNavigationMode.NavMeshAgent && CanUseNavMeshAgent())
        {
            return RetroNpcNavigationMode.NavMeshAgent;
        }

        if (navigationMode == RetroNpcNavigationMode.Rigidbody && movementBody != null)
        {
            return RetroNpcNavigationMode.Rigidbody;
        }

        if (navigationMode == RetroNpcNavigationMode.Transform)
        {
            return RetroNpcNavigationMode.Transform;
        }

        if (navigationMode == RetroNpcNavigationMode.Auto)
        {
            if (CanUseNavMeshAgent())
            {
                return RetroNpcNavigationMode.NavMeshAgent;
            }

            if (movementBody != null)
            {
                return RetroNpcNavigationMode.Rigidbody;
            }
        }

        return RetroNpcNavigationMode.Transform;
    }

    private bool CanUseNavMeshAgent()
    {
        if (navMeshDisabledForInterior || IsInsideBuildingInterior())
        {
            return false;
        }

        if (navMeshAgent == null || !navMeshAgent.isActiveAndEnabled)
        {
            return false;
        }

        if (navMeshAgent.isOnNavMesh)
        {
            return true;
        }

        float sampleRadius = Mathf.Max(1f, navAgentRadius * 4f);
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, sampleRadius, navMeshAgent.areaMask))
        {
            navMeshAgent.Warp(hit.position);
        }

        return navMeshAgent.isOnNavMesh;
    }

    private void ConfigureNavMeshAgent()
    {
        if (navMeshAgent == null)
        {
            return;
        }

        navMeshAgent.speed = moveSpeed;
        navMeshAgent.acceleration = Mathf.Max(0.01f, acceleration);
        navMeshAgent.angularSpeed = turnSpeed;
        navMeshAgent.stoppingDistance = stoppingDistance;
        navMeshAgent.updateRotation = false;
        navMeshAgent.radius = navAgentRadius;
        navMeshAgent.height = navAgentHeight;
        navMeshAgent.obstacleAvoidanceType = obstacleAvoidance;
        navMeshAgent.avoidancePriority = ResolveAvoidancePriority();
    }

    private int ResolveAvoidancePriority()
    {
        if (!staggerAvoidancePriority || avoidancePrioritySpread <= 0)
        {
            return avoidancePriority;
        }

        if (avoidancePriorityResolved)
        {
            return resolvedAvoidancePriority;
        }

        int range = avoidancePrioritySpread * 2 + 1;
        int instanceHash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this) & int.MaxValue;
        int offset = Mathf.Abs(instanceHash) % range - avoidancePrioritySpread;
        resolvedAvoidancePriority = Mathf.Clamp(avoidancePriority + offset, 0, 99);
        avoidancePriorityResolved = true;
        return resolvedAvoidancePriority;
    }

    private bool AcquireTarget(bool force)
    {
        if (!force && Time.time < nextTargetSearchTime)
        {
            return target != null;
        }

        nextTargetSearchTime = Time.time + targetSearchInterval;

        if (preferFpsControllerTarget)
        {
            RetroFpsController controller = UnityEngine.Object.FindAnyObjectByType<RetroFpsController>();
            if (controller != null && TryAssignTarget(controller.transform, force))
            {
                return true;
            }
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            RetroFpsController controller = mainCamera.GetComponentInParent<RetroFpsController>();
            Transform cameraTarget = controller != null ? controller.transform : mainCamera.transform;
            if (TryAssignTarget(cameraTarget, force))
            {
                return true;
            }
        }

        GameObject taggedTarget = FindTaggedTarget();
        if (taggedTarget != null && TryAssignTarget(taggedTarget.transform, force))
        {
            return true;
        }

        return false;
    }

    private bool TryAssignTarget(Transform candidate, bool force)
    {
        if (candidate == null || candidate == transform || candidate.IsChildOf(transform))
        {
            return false;
        }

        if (!force && !IsTargetVisibleOrForced(candidate, false))
        {
            return false;
        }

        target = candidate;
        return true;
    }

    private bool HasUsableTarget()
    {
        return target != null && !IsTargetLost(target);
    }

    private bool IsTargetLost(Transform candidate)
    {
        if (candidate == null)
        {
            return true;
        }

        if (loseTargetRadius <= 0f)
        {
            return false;
        }

        return HorizontalDistance(transform.position, ResolveTargetPoint(candidate)) > loseTargetRadius;
    }

    private bool IsTargetVisibleOrForced(Transform candidate, bool force)
    {
        if (force)
        {
            return true;
        }

        Vector3 targetPoint = ResolveTargetPoint(candidate);
        float distance = HorizontalDistance(transform.position, targetPoint);
        if (awarenessRadius > 0f && distance > awarenessRadius)
        {
            return false;
        }

        Vector3 toTarget = targetPoint - GetEyePosition();
        if (horizontalOnly)
        {
            toTarget = Vector3.ProjectOnPlane(toTarget, Vector3.up);
        }

        if (toTarget.sqrMagnitude > 0.0001f && fieldOfView < 359f)
        {
            float angle = Vector3.Angle(transform.forward, toTarget.normalized);
            if (angle > fieldOfView * 0.5f)
            {
                return false;
            }
        }

        return !requireLineOfSight || HasLineOfSight(candidate, targetPoint);
    }

    private bool HasLineOfSight(Transform candidate, Vector3 targetPoint)
    {
        Vector3 origin = GetEyePosition();
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance <= 0.0001f)
        {
            return true;
        }

        if (!Physics.Raycast(origin, toTarget / distance, out RaycastHit hit, distance, lineOfSightMask, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        Transform hitTransform = hit.collider.transform;
        return hitTransform == candidate || hitTransform.IsChildOf(candidate);
    }

    private void ClearLostTarget()
    {
        target = null;
        if (calmAfterLosingTarget)
        {
            provoked = startProvoked;
        }

        hasIdleDestination = false;
        idleUntilTime = Time.time + GetIdleWait();
    }

    private Vector3 ResolveTargetPoint(Transform candidate)
    {
        if (candidate == null)
        {
            return transform.position;
        }

        Collider candidateCollider = candidate.GetComponentInChildren<Collider>();
        if (candidateCollider != null)
        {
            return candidateCollider.bounds.center;
        }

        return candidate.position + Vector3.up * targetAimHeight;
    }

    private Vector3 GetEyePosition()
    {
        return transform.position + Vector3.up * eyeHeight;
    }

    private GameObject FindTaggedTarget()
    {
        if (string.IsNullOrWhiteSpace(targetTag))
        {
            return null;
        }

        try
        {
            return GameObject.FindGameObjectWithTag(targetTag);
        }
        catch (UnityException)
        {
            return null;
        }
    }

    private Transform FindNextValidPatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            return null;
        }

        patrolIndex = Mathf.Clamp(patrolIndex, 0, patrolPoints.Length - 1);
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            int index = (patrolIndex + i) % patrolPoints.Length;
            Transform point = patrolPoints[index];
            if (point != null)
            {
                patrolIndex = index;
                return point;
            }
        }

        return null;
    }

    private void AdvancePatrolIndex()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            patrolIndex = 0;
            return;
        }

        if (loopPatrol)
        {
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
            return;
        }

        patrolIndex = Mathf.Min(patrolIndex + 1, patrolPoints.Length - 1);
    }

    private void PickWanderDestination()
    {
        if (TryPickIdleDoorDestination())
        {
            return;
        }

        if (IsInsideBuildingInterior())
        {
            idleDestination = currentBuilding.GetRandomInteriorWorldPosition(interiorWanderMargin);
            hasIdleDestination = true;
            return;
        }

        Vector2 offset = Random.insideUnitCircle * wanderRadius;
        idleDestination = homePosition + new Vector3(offset.x, 0f, offset.y);
        if (!horizontalOnly)
        {
            idleDestination.y = homePosition.y;
        }

        hasIdleDestination = true;
    }

    private float GetIdleWait()
    {
        return Random.Range(idleWaitRange.x, idleWaitRange.y);
    }

    private void FacePosition(Vector3 position)
    {
        Vector3 direction = position - transform.position;
        if (horizontalOnly)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        }

        FaceDirection(direction);
    }

    private void FaceDirection(Vector3 direction)
    {
        if (turnSpeed <= 0f || direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private void SetState(RetroNpcBrainState nextState)
    {
        state = nextState;
    }

    private void AutoAssignReferences(bool allowCreateNavMeshAgent)
    {
        if (damageable == null)
        {
            damageable = GetComponent<RetroDamageable>();
        }

        if (movementBody == null)
        {
            movementBody = GetComponent<Rigidbody>();
        }

        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (navMeshAgent == null
            && allowCreateNavMeshAgent
            && autoCreateNavMeshAgent
            && navigationMode != RetroNpcNavigationMode.Transform
            && navigationMode != RetroNpcNavigationMode.Rigidbody)
        {
            navMeshAgent = gameObject.AddComponent<NavMeshAgent>();
        }
    }

    private void SubscribeToDamageable()
    {
        if (subscribedToDamageable || damageable == null)
        {
            return;
        }

        damageable.Damaged += HandleDamaged;
        damageable.Died += HandleDied;
        subscribedToDamageable = true;
    }

    private void UnsubscribeFromDamageable()
    {
        if (!subscribedToDamageable || damageable == null)
        {
            subscribedToDamageable = false;
            return;
        }

        damageable.Damaged -= HandleDamaged;
        damageable.Died -= HandleDied;
        subscribedToDamageable = false;
    }

    private void SubscribeToGameplayEvents()
    {
        damageEventSubscription?.Dispose();
        damageEventSubscription = RetroGameContext.Events.Subscribe<RetroDamageEvent>(HandleDamageEvent);
    }

    private void UnsubscribeFromGameplayEvents()
    {
        damageEventSubscription?.Dispose();
        damageEventSubscription = null;
    }

    private void HandleDamaged(RetroDamageable source, float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!provokeOnDamage || behaviorMode == RetroNpcBehaviorMode.Passive)
        {
            return;
        }

        provoked = true;
        AcquireTarget(true);
        PublishAlert(target, 0.85f);
    }

    private void HandleDamageEvent(RetroDamageEvent evt)
    {
        if (damageable == null
            || evt.Target != damageable
            || !provokeOnDamage
            || behaviorMode == RetroNpcBehaviorMode.Passive)
        {
            return;
        }

        Transform sourceTransform = evt.Source != null ? evt.Source.transform : null;
        if (sourceTransform != null && sourceTransform != transform && !sourceTransform.IsChildOf(transform))
        {
            target = sourceTransform;
        }
        else if (target == null)
        {
            AcquireTarget(true);
        }

        provoked = true;
        PublishAlert(target, 1f);
    }

    private void HandleDied(RetroDamageable source)
    {
        SetState(RetroNpcBrainState.Dead);
        StopMovement();
        enabled = false;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void PublishAlert(Transform alertTarget, float urgency)
    {
        Vector3 lastKnownPosition = alertTarget != null ? alertTarget.position : transform.position;
        GameObject targetObject = alertTarget != null ? alertTarget.gameObject : null;
        RetroGameContext.Events.Publish(new RetroNpcAlertEvent(gameObject, targetObject, lastKnownPosition, Mathf.Clamp01(urgency)));
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? homePosition : transform.position;

        Gizmos.color = new Color(0.3f, 0.65f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, awarenessRadius);

        Gizmos.color = new Color(1f, 0.22f, 0.16f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = new Color(1f, 0.82f, 0.18f, 0.35f);
        Gizmos.DrawWireSphere(origin, wanderRadius);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, GetEyePosition());

        if (target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(GetEyePosition(), ResolveTargetPoint(target));
        }

        if (hasDestination)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentDestination);
            Gizmos.DrawWireSphere(currentDestination, 0.18f);
        }

        if (hasIdleDestination)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(idleDestination, wanderPointTolerance);
        }

        if (lastMoveDirection.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, lastMoveDirection.normalized);
        }
    }
}
