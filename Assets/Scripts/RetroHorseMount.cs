using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(170)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class RetroHorseMount : RetroInteractableBehaviour
{
    private enum RiderMode
    {
        Empty = 0,
        Player = 1,
        Npc = 2
    }

    private const int ImpactBufferSize = 36;
    private static readonly Collider[] ImpactBuffer = new Collider[ImpactBufferSize];
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
    [SerializeField] private Transform seatAnchor;
    [SerializeField] private Transform dismountAnchor;

    [Header("Sprite Sets")]
    [SerializeField] private string mountDisplayName = "Horse";
    [SerializeField] private DirectionalSpriteDefinition riderlessDefinition;
    [SerializeField] private DirectionalSpriteDefinition defaultMountedNpcDefinition;
    [SerializeField] private Sprite[] firstPersonRidingFrames = new Sprite[0];
    [SerializeField] private Material firstPersonRidingMaterial;

    [Header("Clips")]
    [SerializeField] private string idleClipId = "Idle";
    [SerializeField] private string walkClipId = "Walk";
    [SerializeField] private string gallopClipId = "Gallop";

    [Header("Player Input")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string sprintActionName = "Sprint";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string interactActionName = "Interact";
    [SerializeField] private Key fallbackDismountKey = Key.F;
    [SerializeField] private Key fallbackJumpKey = Key.Space;
    [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.13f;
    [SerializeField, Min(1f)] private float gamepadLookSpeed = 170f;
    [SerializeField, Range(20f, 89f)] private float maxLookPitch = 68f;
    [SerializeField, Range(0f, 120f)] private float maxLookYawOffset = 82f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 5.4f;
    [SerializeField, Min(0f)] private float gallopSpeed = 11.2f;
    [SerializeField, Min(0f)] private float reverseSpeed = 2.2f;
    [SerializeField, Min(0f)] private float acceleration = 22f;
    [SerializeField, Min(0f)] private float braking = 30f;
    [SerializeField, Min(0f)] private float turnSpeed = 185f;
    [SerializeField, Min(0f)] private float emptyWanderSpeed = 1.7f;
    [SerializeField, Min(0f)] private float emptyWanderRadius = 7f;
    [SerializeField, Min(0.2f)] private float emptyWanderRetargetTime = 2.4f;
    [SerializeField] private bool wanderWhenEmpty = true;

    [Header("Player Presentation")]
    [SerializeField, Min(0f)] private float cameraBobAmplitude = 0.105f;
    [SerializeField, Min(0f)] private float cameraBobFrequency = 10.5f;
    [SerializeField, Min(0f)] private float cameraRollDegrees = 3.5f;
    [SerializeField, Min(0f)] private float gallopFovBoost = 8f;
    [SerializeField, Min(0.1f)] private float cameraBlendSpeed = 13f;
    [SerializeField] private Vector3 cameraMountedLocalOffset = new(0f, 0.18f, 0.04f);
    [SerializeField] private Vector3 firstPersonOverlayLocalPosition = new(0f, -0.31f, 0.86f);
    [SerializeField] private Vector2 firstPersonOverlaySize = new(0.92f, 0.92f);
    [SerializeField, Min(0.01f)] private float firstPersonFrameDuration = 0.075f;

    [Header("Impact")]
    [SerializeField, Min(0f)] private float impactDamage = 18f;
    [SerializeField, Min(0f)] private float impactRadius = 1.1f;
    [SerializeField, Min(0f)] private float impactForwardOffset = 1.25f;
    [SerializeField, Min(0.05f)] private float impactCooldown = 0.42f;
    [SerializeField, Min(0f)] private float impactKnockback = 6.5f;
    [SerializeField] private LayerMask impactMask = ~0;
    [SerializeField] private QueryTriggerInteraction impactTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Effects")]
    [SerializeField, Min(0f)] private float dustTrailInterval = 0.085f;
    [SerializeField] private Color dustTrailColor = new(0.72f, 0.62f, 0.43f, 0.52f);
    [SerializeField] private Color speedRimColor = new(0.9f, 0.84f, 0.68f, 1f);

    private readonly HashSet<RetroDamageable> damagedThisPulse = new();
    private readonly List<ObjectState> hiddenPlayerObjects = new();
    private MaterialPropertyBlock propertyBlock;
    private RiderMode riderMode;
    private PlayerRideState playerState;
    private NpcRideState npcState;
    private InputActionMap playerActionMap;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction jumpAction;
    private InputAction interactAction;
    private bool enabledActionMapForRide;
    private bool usingGamepadLook;
    private Vector3 currentVelocity;
    private Vector3 npcDesiredDirection;
    private float npcThrottle;
    private bool npcSprint;
    private Vector3 homePosition;
    private Vector3 wanderDestination;
    private float nextWanderTime;
    private float nextImpactTime;
    private float nextDustTime;
    private float mountedAtTime;
    private int mountedFrame;
    private float lookYawOffset;
    private float lookPitch;
    private float overlayFrameTimer;
    private int overlayFrameIndex;
    private bool wasVisualRendererEnabled = true;
    private Vector3 visualBaseLocalPosition;
    private float visualPhaseOffset = -1f;
    private string currentClip;

    protected override string DefaultInteractionVerb => riderMode == RiderMode.Empty ? "Ride" : "Dismount";
    protected override string DefaultInteractionName => string.IsNullOrWhiteSpace(mountDisplayName) ? gameObject.name : mountDisplayName;
    public bool IsOccupied => riderMode != RiderMode.Empty;
    public bool HasPlayerRider => riderMode == RiderMode.Player;
    public bool HasNpcRider => riderMode == RiderMode.Npc;
    public GameObject RiderObject => riderMode == RiderMode.Player ? playerState.Actor : npcState.Rider;
    public DirectionalSpriteDefinition DefaultMountedNpcDefinition => defaultMountedNpcDefinition;

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
        EnsureVisualPhaseOffset();
        homePosition = transform.position;
        wanderDestination = homePosition;
        ConfigurePhysics();
    }

    private void OnEnable()
    {
        AutoAssignReferences();
        EnsureVisualPhaseOffset();
        ConfigurePhysics();
        visualBaseLocalPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        if (riderMode == RiderMode.Empty)
        {
            ApplyDefinition(riderlessDefinition);
            PickWanderDestination(true);
        }
    }

    private void OnDisable()
    {
        if (riderMode == RiderMode.Player)
        {
            DismountPlayer(false);
        }
        else if (riderMode == RiderMode.Npc)
        {
            DismountNpc(false);
        }
    }

    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0f, walkSpeed);
        gallopSpeed = Mathf.Max(walkSpeed, gallopSpeed);
        reverseSpeed = Mathf.Max(0f, reverseSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        braking = Mathf.Max(0f, braking);
        turnSpeed = Mathf.Max(0f, turnSpeed);
        emptyWanderSpeed = Mathf.Max(0f, emptyWanderSpeed);
        emptyWanderRadius = Mathf.Max(0f, emptyWanderRadius);
        emptyWanderRetargetTime = Mathf.Max(0.2f, emptyWanderRetargetTime);
        cameraBobAmplitude = Mathf.Max(0f, cameraBobAmplitude);
        cameraBobFrequency = Mathf.Max(0f, cameraBobFrequency);
        cameraBlendSpeed = Mathf.Max(0.1f, cameraBlendSpeed);
        firstPersonOverlaySize.x = Mathf.Max(0.01f, firstPersonOverlaySize.x);
        firstPersonOverlaySize.y = Mathf.Max(0.01f, firstPersonOverlaySize.y);
        firstPersonFrameDuration = Mathf.Max(0.01f, firstPersonFrameDuration);
        impactDamage = Mathf.Max(0f, impactDamage);
        impactRadius = Mathf.Max(0f, impactRadius);
        impactForwardOffset = Mathf.Max(0f, impactForwardOffset);
        impactCooldown = Mathf.Max(0.05f, impactCooldown);
        impactKnockback = Mathf.Max(0f, impactKnockback);
        dustTrailInterval = Mathf.Max(0f, dustTrailInterval);
        AutoAssignReferences();
    }

    private void Update()
    {
        if (damageable != null && damageable.IsDead)
        {
            TickDead();
            return;
        }

        switch (riderMode)
        {
            case RiderMode.Player:
                TickPlayerRide();
                break;
            case RiderMode.Npc:
                TickNpcRide();
                break;
            default:
                TickEmptyHorse();
                break;
        }

        TickImpacts();
        TickDustTrails();
        TickClip();
    }

    private void LateUpdate()
    {
        ApplyVisualMotion();
        ApplyShaderPulse();
    }

    public override bool CanInteract(in RetroInteractionContext context)
    {
        if (!base.CanInteract(context) || damageable != null && damageable.IsDead)
        {
            return false;
        }

        if (riderMode == RiderMode.Empty)
        {
            return ResolvePlayerController(context.Actor) != null;
        }

        return riderMode == RiderMode.Player && ResolvePlayerController(context.Actor) == playerState.Controller;
    }

    public override string GetInteractionPrompt(in RetroInteractionContext context)
    {
        string displayName = ResolveMountDisplayName();
        return riderMode == RiderMode.Player ? $"Dismount {displayName}" : $"Ride {displayName}";
    }

    public bool CanAcceptNpcRider(GameObject rider)
    {
        return riderMode == RiderMode.Empty
            && rider != null
            && rider != gameObject
            && (damageable == null || !damageable.IsDead);
    }

    public bool IsNpcRider(GameObject rider)
    {
        return riderMode == RiderMode.Npc && npcState.Rider == rider;
    }

    public bool TryMountNpc(GameObject rider, DirectionalSpriteDefinition mountedDefinition = null, bool hideRiderObject = true)
    {
        if (!CanAcceptNpcRider(rider))
        {
            return false;
        }

        riderMode = RiderMode.Npc;
        npcState = new NpcRideState(rider, hideRiderObject);
        currentVelocity = Vector3.zero;
        npcDesiredDirection = transform.forward;
        npcThrottle = 0f;
        npcSprint = false;
        mountedAtTime = Time.time;
        mountedFrame = Time.frameCount;

        DisableNpcRiderObject(ref npcState);
        AttachNpcRider(ref npcState);
        ApplyDefinition(mountedDefinition != null ? mountedDefinition : defaultMountedNpcDefinition);
        PlayClip(idleClipId, true);
        return true;
    }

    public void DismountNpc(bool restoreRider = true)
    {
        if (riderMode != RiderMode.Npc)
        {
            return;
        }

        if (restoreRider)
        {
            RestoreNpcRiderObject(npcState);
        }

        npcState = default;
        riderMode = RiderMode.Empty;
        npcDesiredDirection = Vector3.zero;
        npcThrottle = 0f;
        npcSprint = false;
        ApplyDefinition(riderlessDefinition);
        PickWanderDestination(true);
    }

    public void SetNpcRideInput(Vector3 worldDirection, float throttle, bool sprint)
    {
        if (riderMode != RiderMode.Npc)
        {
            return;
        }

        worldDirection = ProjectHorizontal(worldDirection);
        if (worldDirection.sqrMagnitude < 0.0001f)
        {
            worldDirection = transform.forward;
        }

        npcDesiredDirection = worldDirection.normalized;
        npcThrottle = Mathf.Clamp01(throttle);
        npcSprint = sprint;
    }

    protected override void InteractInternal(in RetroInteractionContext context)
    {
        if (riderMode == RiderMode.Player)
        {
            DismountPlayer(true);
            return;
        }

        TryMountPlayer(context.Actor);
    }

    private bool TryMountPlayer(GameObject actor)
    {
        if (riderMode != RiderMode.Empty || actor == null)
        {
            return false;
        }

        RetroFpsController controller = ResolvePlayerController(actor);
        if (controller == null || controller.ViewCamera == null)
        {
            return false;
        }

        GameObject actorRoot = controller.gameObject;
        riderMode = RiderMode.Player;
        mountedAtTime = Time.unscaledTime;
        mountedFrame = Time.frameCount;
        playerState = CapturePlayerState(actorRoot, controller);
        currentVelocity = Vector3.zero;
        lookYawOffset = 0f;
        lookPitch = NormalizePitch(controller.ViewCamera.transform.localEulerAngles.x);

        HidePlayerObjectsForRide(playerState);
        DisablePlayerForRide(playerState);
        ResolvePlayerRideActions(actorRoot, controller);
        EnsureFirstPersonOverlay(playerState);

        if (visualRenderer != null)
        {
            wasVisualRendererEnabled = visualRenderer.enabled;
            visualRenderer.enabled = false;
        }

        PlayClip(idleClipId, true);
        return true;
    }

    private void DismountPlayer(bool placeAtDismount)
    {
        if (riderMode != RiderMode.Player)
        {
            return;
        }

        if (placeAtDismount && playerState.ActorTransform != null)
        {
            playerState.ActorTransform.SetPositionAndRotation(ResolveDismountPosition(), Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        }

        DestroyFirstPersonOverlay();
        RestoreHiddenPlayerObjects();
        ReleasePlayerRideActions();
        RestorePlayerAfterRide(playerState);

        if (visualRenderer != null)
        {
            visualRenderer.enabled = wasVisualRendererEnabled;
        }

        playerState = default;
        riderMode = RiderMode.Empty;
        ApplyDefinition(riderlessDefinition);
        PickWanderDestination(true);
    }

    private void TickPlayerRide()
    {
        if (playerState.Actor == null || playerState.ViewCamera == null)
        {
            DismountPlayer(false);
            return;
        }

        if (CanReadDismountThisFrame() && WasDismountPressed())
        {
            DismountPlayer(true);
            return;
        }

        Vector2 move = ReadMoveInput();
        bool sprint = ReadSprintInput();
        bool hop = WasJumpPressed();
        TickLookInput(Time.unscaledDeltaTime);
        TickMountedMovement(move, sprint, hop);
        UpdateMountedPlayerPose(move, sprint);
        AnimateFirstPersonOverlay();
    }

    private void TickNpcRide()
    {
        if (npcState.Rider == null)
        {
            DismountNpc(false);
            return;
        }

        Vector3 direction = npcDesiredDirection.sqrMagnitude > 0.0001f ? npcDesiredDirection : transform.forward;
        float targetSpeed = Mathf.Lerp(0f, npcSprint ? gallopSpeed : walkSpeed, npcThrottle);
        MoveHorse(direction, targetSpeed, 0f);
    }

    private void TickEmptyHorse()
    {
        if (!wanderWhenEmpty || emptyWanderSpeed <= 0f)
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, braking * Time.deltaTime);
            return;
        }

        if (Time.time >= nextWanderTime || HorizontalDistance(transform.position, wanderDestination) < 0.65f)
        {
            PickWanderDestination(false);
        }

        Vector3 toDestination = ProjectHorizontal(wanderDestination - transform.position);
        if (toDestination.sqrMagnitude < 0.0001f)
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, braking * Time.deltaTime);
            return;
        }

        MoveHorse(toDestination.normalized, emptyWanderSpeed, 0f);
    }

    private void TickMountedMovement(Vector2 moveInput, bool sprint, bool hop)
    {
        float forward = Mathf.Clamp(moveInput.y, -1f, 1f);
        float targetSpeed = forward >= 0f
            ? forward * (sprint ? gallopSpeed : walkSpeed)
            : forward * reverseSpeed;

        float turnInput = Mathf.Clamp(moveInput.x, -1f, 1f);
        float turnAmount = turnInput * turnSpeed * Time.deltaTime * Mathf.Lerp(0.38f, 1f, Mathf.InverseLerp(0f, gallopSpeed, Mathf.Abs(currentVelocity.magnitude)));
        if (Mathf.Abs(turnAmount) > 0.001f)
        {
            Quaternion nextRotation = Quaternion.Euler(0f, transform.eulerAngles.y + turnAmount, 0f);
            MoveRotation(nextRotation);
        }

        MoveHorse(transform.forward, targetSpeed, hop ? 0.16f : 0f);
    }

    private void MoveHorse(Vector3 desiredDirection, float targetSpeed, float hopLift)
    {
        desiredDirection = ProjectHorizontal(desiredDirection);
        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            desiredDirection = transform.forward;
        }

        desiredDirection.Normalize();
        float accel = Mathf.Abs(targetSpeed) > Mathf.Abs(Vector3.Dot(currentVelocity, desiredDirection)) ? acceleration : braking;
        Vector3 desiredVelocity = desiredDirection * targetSpeed;
        currentVelocity = Vector3.MoveTowards(currentVelocity, desiredVelocity, accel * Time.deltaTime);
        Vector3 nextPosition = transform.position + currentVelocity * Time.deltaTime + Vector3.up * hopLift;
        MovePosition(nextPosition);

        if (currentVelocity.sqrMagnitude > 0.08f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(ProjectHorizontal(currentVelocity).normalized, Vector3.up);
            MoveRotation(Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * 0.72f * Time.deltaTime));
        }
    }

    private void MovePosition(Vector3 position)
    {
        if (movementBody != null)
        {
            movementBody.MovePosition(position);
        }
        else
        {
            transform.position = position;
        }
    }

    private void MoveRotation(Quaternion rotation)
    {
        if (movementBody != null)
        {
            movementBody.MoveRotation(rotation);
        }
        else
        {
            transform.rotation = rotation;
        }
    }

    private void UpdateMountedPlayerPose(Vector2 moveInput, bool sprint)
    {
        Transform actor = playerState.ActorTransform;
        Camera viewCamera = playerState.ViewCamera;
        if (actor == null || viewCamera == null)
        {
            return;
        }

        Vector3 seatPosition = ResolveSeatPosition();
        actor.SetPositionAndRotation(seatPosition, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));

        float speed01 = Mathf.InverseLerp(0f, Mathf.Max(0.1f, gallopSpeed), ProjectHorizontal(currentVelocity).magnitude);
        float bobPhase = Time.time * cameraBobFrequency * Mathf.Lerp(0.55f, 1.45f, speed01);
        Vector3 bobOffset = new(
            Mathf.Sin(bobPhase * 0.5f) * cameraBobAmplitude * 0.42f,
            Mathf.Abs(Mathf.Cos(bobPhase)) * cameraBobAmplitude * Mathf.Lerp(0.25f, 1.45f, speed01),
            Mathf.Sin(bobPhase) * cameraBobAmplitude * 0.28f);
        float roll = -moveInput.x * cameraRollDegrees + Mathf.Sin(bobPhase * 0.5f) * cameraRollDegrees * 0.26f * speed01;

        Transform cameraTransform = viewCamera.transform;
        Vector3 targetLocalPosition = playerState.CameraBaseLocalPosition + cameraMountedLocalOffset + bobOffset;
        Quaternion targetLocalRotation = playerState.CameraBaseLocalRotation * Quaternion.Euler(lookPitch, lookYawOffset, roll);
        float blend = 1f - Mathf.Exp(-cameraBlendSpeed * Time.deltaTime);
        cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, targetLocalPosition, blend);
        cameraTransform.localRotation = Quaternion.Slerp(cameraTransform.localRotation, targetLocalRotation, blend);

        float targetFov = playerState.BaseFieldOfView + gallopFovBoost * speed01 * (sprint ? 1f : 0.45f);
        viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, targetFov, blend);
    }

    private void TickLookInput(float deltaTime)
    {
        Camera viewCamera = playerState.ViewCamera;
        if (viewCamera == null)
        {
            return;
        }

        Vector2 look = ReadLookInput(out bool gamepadLook);
        if (look.sqrMagnitude < 0.0001f)
        {
            return;
        }

        usingGamepadLook = gamepadLook;
        if (!usingGamepadLook && Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        float scale = usingGamepadLook ? gamepadLookSpeed * deltaTime : mouseSensitivity;
        lookYawOffset += look.x * scale;
        lookPitch -= look.y * scale;
        lookYawOffset = Mathf.Clamp(lookYawOffset, -maxLookYawOffset, maxLookYawOffset);
        lookPitch = Mathf.Clamp(lookPitch, -maxLookPitch, maxLookPitch);
    }

    private void TickImpacts()
    {
        if (impactDamage <= 0f || Time.time < nextImpactTime || ProjectHorizontal(currentVelocity).magnitude < walkSpeed * 0.7f)
        {
            return;
        }

        Vector3 forward = ProjectHorizontal(currentVelocity);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = transform.forward;
        }

        forward.Normalize();
        Vector3 center = transform.position + forward * impactForwardOffset + Vector3.up * 0.78f;
        int hitCount = Physics.OverlapSphereNonAlloc(center, impactRadius, ImpactBuffer, impactMask, impactTriggerInteraction);
        damagedThisPulse.Clear();
        bool hitAnything = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = ImpactBuffer[i];
            if (hit == null || hit.transform.IsChildOf(transform) || IsCurrentRiderCollider(hit))
            {
                continue;
            }

            RetroDamageable targetDamageable = hit.GetComponentInParent<RetroDamageable>();
            if (targetDamageable == null || targetDamageable == damageable || targetDamageable.IsDead || !damagedThisPulse.Add(targetDamageable))
            {
                continue;
            }

            float speed01 = Mathf.InverseLerp(0f, Mathf.Max(0.1f, gallopSpeed), ProjectHorizontal(currentVelocity).magnitude);
            float damage = impactDamage * Mathf.Lerp(0.65f, 1.5f, speed01);
            Vector3 hitPoint = hit.ClosestPoint(center);
            targetDamageable.ApplyDamage(damage, hitPoint, -forward, gameObject);
            if (hit.attachedRigidbody != null && !hit.attachedRigidbody.isKinematic)
            {
                hit.attachedRigidbody.AddForce(forward * impactKnockback + Vector3.up * impactKnockback * 0.18f, ForceMode.Impulse);
            }

            RetroGameContext.Vfx.SpawnImpactFlash(hitPoint, -forward, speedRimColor, 0.16f, 0.11f);
            hitAnything = true;
        }

        if (hitAnything)
        {
            nextImpactTime = Time.time + impactCooldown;
        }
    }

    private void TickDustTrails()
    {
        if (dustTrailInterval <= 0f || Time.time < nextDustTime || ProjectHorizontal(currentVelocity).sqrMagnitude < 1.4f)
        {
            return;
        }

        nextDustTime = Time.time + dustTrailInterval;
        Vector3 backward = -ProjectHorizontal(currentVelocity).normalized;
        Vector3 start = transform.position + backward * 0.85f + Vector3.up * 0.18f + transform.right * Random.Range(-0.38f, 0.38f);
        Vector3 end = start + backward * Random.Range(0.55f, 1.15f) + Vector3.up * Random.Range(0.04f, 0.25f);
        RetroGameContext.Vfx.SpawnBulletTrail("HorseDust", start, end, dustTrailColor, Random.Range(0.04f, 0.075f), Random.Range(0.08f, 0.17f));
    }

    private void TickClip()
    {
        float speed = ProjectHorizontal(currentVelocity).magnitude;
        if (speed > walkSpeed * 1.12f)
        {
            PlayClip(gallopClipId);
        }
        else if (speed > 0.25f)
        {
            PlayClip(walkClipId);
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
        if (visualRoot == null || riderMode == RiderMode.Player)
        {
            return;
        }

        EnsureVisualPhaseOffset();
        float speed01 = Mathf.InverseLerp(0f, Mathf.Max(0.1f, gallopSpeed), ProjectHorizontal(currentVelocity).magnitude);
        float bob = Mathf.Sin(Time.time * cameraBobFrequency + visualPhaseOffset) * cameraBobAmplitude * 0.65f * Mathf.Lerp(0.2f, 1.2f, speed01);
        visualRoot.localPosition = visualBaseLocalPosition + Vector3.up * bob;
    }

    private void ApplyShaderPulse()
    {
        if (visualRenderer == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        EnsureVisualPhaseOffset();
        float speed01 = Mathf.InverseLerp(0f, Mathf.Max(0.1f, gallopSpeed), ProjectHorizontal(currentVelocity).magnitude);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 13.5f + visualPhaseOffset * 1.37f);
        visualRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(RimColorId, speedRimColor);
        propertyBlock.SetFloat(RimStrengthId, Mathf.Lerp(0.02f, 0.16f + pulse * 0.04f, speed01));
        propertyBlock.SetFloat(SpecularStrengthId, Mathf.Lerp(0.02f, 0.08f, speed01));
        propertyBlock.SetFloat(MacroNormalBendId, Mathf.Lerp(0.5f, 0.72f, speed01));
        propertyBlock.SetFloat(WrapDiffuseId, Mathf.Lerp(0.3f, 0.38f, speed01));
        visualRenderer.SetPropertyBlock(propertyBlock);
    }

    private void TickDead()
    {
        currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, braking * Time.deltaTime);
        if (riderMode == RiderMode.Player)
        {
            DismountPlayer(true);
        }
        else if (riderMode == RiderMode.Npc)
        {
            DismountNpc(true);
        }
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

    private void ApplyDefinition(DirectionalSpriteDefinition definition)
    {
        if (definition == null || animator == null)
        {
            return;
        }

        animator.Definition = definition;
        currentClip = null;
        PlayClip(idleClipId, true);
    }

    private PlayerRideState CapturePlayerState(GameObject actor, RetroFpsController controller)
    {
        Rigidbody actorBody = actor.GetComponent<Rigidbody>();
        CapsuleCollider capsule = actor.GetComponent<CapsuleCollider>();
        RetroInteractor interactor = actor.GetComponent<RetroInteractor>();
        RetroWeaponSystem weaponSystem = actor.GetComponent<RetroWeaponSystem>();
        Camera viewCamera = controller.ViewCamera;

        return new PlayerRideState
        {
            Actor = actor,
            ActorTransform = actor.transform,
            Controller = controller,
            Body = actorBody,
            Capsule = capsule,
            Interactor = interactor,
            WeaponSystem = weaponSystem,
            ViewCamera = viewCamera,
            CameraTransform = viewCamera.transform,
            CameraBaseLocalPosition = viewCamera.transform.localPosition,
            CameraBaseLocalRotation = viewCamera.transform.localRotation,
            BaseFieldOfView = viewCamera.fieldOfView,
            ControllerWasEnabled = controller.enabled,
            InteractorWasEnabled = interactor != null && interactor.enabled,
            WeaponSystemWasEnabled = weaponSystem != null && weaponSystem.enabled,
            CapsuleWasEnabled = capsule != null && capsule.enabled,
            BodyWasKinematic = actorBody != null && actorBody.isKinematic,
            BodyUsedGravity = actorBody != null && actorBody.useGravity,
            BodyConstraints = actorBody != null ? actorBody.constraints : RigidbodyConstraints.None,
            BodyVelocity = actorBody != null ? GetBodyVelocity(actorBody) : Vector3.zero,
            BodyAngularVelocity = actorBody != null ? actorBody.angularVelocity : Vector3.zero,
            ControllerViewModelPresentationWasEnabled = controller.IsViewModelPresentationEnabled
        };
    }

    private void DisablePlayerForRide(PlayerRideState state)
    {
        if (state.Controller != null)
        {
            state.Controller.SetViewModelPresentationEnabled(false);
            state.Controller.enabled = false;
        }

        if (state.Interactor != null)
        {
            state.Interactor.enabled = false;
        }

        if (state.WeaponSystem != null)
        {
            state.WeaponSystem.enabled = false;
        }

        if (state.Capsule != null)
        {
            state.Capsule.enabled = false;
        }

        if (state.Body != null)
        {
            SetBodyVelocity(state.Body, Vector3.zero);
            state.Body.angularVelocity = Vector3.zero;
            state.Body.useGravity = false;
            state.Body.isKinematic = true;
            state.Body.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    private void RestorePlayerAfterRide(PlayerRideState state)
    {
        if (state.CameraTransform != null)
        {
            state.CameraTransform.localPosition = state.CameraBaseLocalPosition;
            state.CameraTransform.localRotation = state.CameraBaseLocalRotation;
        }

        if (state.ViewCamera != null)
        {
            state.ViewCamera.fieldOfView = state.BaseFieldOfView;
        }

        if (state.Body != null)
        {
            state.Body.isKinematic = state.BodyWasKinematic;
            state.Body.useGravity = state.BodyUsedGravity;
            state.Body.constraints = state.BodyConstraints;
            SetBodyVelocity(state.Body, Vector3.zero);
            state.Body.angularVelocity = state.BodyAngularVelocity;
        }

        if (state.Capsule != null)
        {
            state.Capsule.enabled = state.CapsuleWasEnabled;
        }

        if (state.WeaponSystem != null)
        {
            state.WeaponSystem.enabled = state.WeaponSystemWasEnabled;
        }

        if (state.Controller != null)
        {
            state.Controller.SetViewModelPresentationEnabled(state.ControllerViewModelPresentationWasEnabled);
            state.Controller.enabled = state.ControllerWasEnabled;
        }

        if (state.Interactor != null)
        {
            state.Interactor.enabled = state.InteractorWasEnabled;
        }
    }

    private void HidePlayerObjectsForRide(PlayerRideState state)
    {
        hiddenPlayerObjects.Clear();
        Transform cameraTransform = state.ViewCamera != null ? state.ViewCamera.transform : null;
        if (cameraTransform != null)
        {
            HideChildIfPresent(cameraTransform, "RuntimeWeaponRoot");
            HideChildIfPresent(cameraTransform, "RuntimeSpriteWeaponRoot");
        }

        Transform viewModelRoot = state.Controller != null ? state.Controller.ViewModelRoot : null;
        if (viewModelRoot != null)
        {
            StoreAndSetActive(viewModelRoot.gameObject, false);
        }
    }

    private void HideChildIfPresent(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            StoreAndSetActive(child.gameObject, false);
        }
    }

    private void StoreAndSetActive(GameObject target, bool active)
    {
        if (target == null)
        {
            return;
        }

        hiddenPlayerObjects.Add(new ObjectState(target, target.activeSelf));
        target.SetActive(active);
    }

    private void RestoreHiddenPlayerObjects()
    {
        for (int i = hiddenPlayerObjects.Count - 1; i >= 0; i--)
        {
            ObjectState state = hiddenPlayerObjects[i];
            if (state.Target != null)
            {
                state.Target.SetActive(state.WasActive);
            }
        }

        hiddenPlayerObjects.Clear();
    }

    private void ResolvePlayerRideActions(GameObject actor, RetroFpsController controller)
    {
        PlayerInput playerInput = actor.GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            playerInput = actor.GetComponentInParent<PlayerInput>();
        }

        InputActionAsset source = playerInput != null ? playerInput.actions : controller.InputActionsAsset;
        if (source == null)
        {
            return;
        }

        playerActionMap = source.FindActionMap(actionMapName, false);
        if (playerActionMap == null)
        {
            return;
        }

        enabledActionMapForRide = !playerActionMap.enabled;
        if (enabledActionMapForRide)
        {
            playerActionMap.Enable();
        }

        moveAction = playerActionMap.FindAction(moveActionName, false);
        lookAction = playerActionMap.FindAction(lookActionName, false);
        sprintAction = playerActionMap.FindAction(sprintActionName, false);
        jumpAction = playerActionMap.FindAction(jumpActionName, false);
        interactAction = playerActionMap.FindAction(interactActionName, false);
    }

    private void ReleasePlayerRideActions()
    {
        if (enabledActionMapForRide && playerActionMap != null)
        {
            playerActionMap.Disable();
        }

        playerActionMap = null;
        moveAction = null;
        lookAction = null;
        sprintAction = null;
        jumpAction = null;
        interactAction = null;
        enabledActionMapForRide = false;
    }

    private Vector2 ReadMoveInput()
    {
        if (moveAction != null && moveAction.enabled)
        {
            return Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        }

        Vector2 move = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
            {
                move.y += 1f;
            }
            if (Keyboard.current.sKey.isPressed)
            {
                move.y -= 1f;
            }
            if (Keyboard.current.dKey.isPressed)
            {
                move.x += 1f;
            }
            if (Keyboard.current.aKey.isPressed)
            {
                move.x -= 1f;
            }
        }

        return Vector2.ClampMagnitude(move, 1f);
    }

    private Vector2 ReadLookInput(out bool gamepadLook)
    {
        gamepadLook = false;
        if (lookAction != null && lookAction.enabled)
        {
            Vector2 value = lookAction.ReadValue<Vector2>();
            InputDevice device = lookAction.activeControl != null ? lookAction.activeControl.device : null;
            gamepadLook = device is Gamepad || device is Joystick;
            return value;
        }

        if (Mouse.current != null)
        {
            return Mouse.current.delta.ReadValue();
        }

        return Vector2.zero;
    }

    private bool ReadSprintInput()
    {
        if (sprintAction != null && sprintAction.enabled)
        {
            return sprintAction.IsPressed();
        }

        return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
    }

    private bool WasJumpPressed()
    {
        if (jumpAction != null && jumpAction.enabled && jumpAction.WasPressedThisFrame())
        {
            return true;
        }

        return Keyboard.current != null && fallbackJumpKey != Key.None && Keyboard.current[fallbackJumpKey].wasPressedThisFrame;
    }

    private bool WasDismountPressed()
    {
        if (interactAction != null && interactAction.enabled && interactAction.WasPressedThisFrame())
        {
            return true;
        }

        return Keyboard.current != null && fallbackDismountKey != Key.None && Keyboard.current[fallbackDismountKey].wasPressedThisFrame;
    }

    private bool CanReadDismountThisFrame()
    {
        return Time.frameCount > mountedFrame + 1 && Time.unscaledTime >= mountedAtTime + 0.18f;
    }

    private Vector3 ResolveSeatPosition()
    {
        if (seatAnchor != null)
        {
            return seatAnchor.position;
        }

        return transform.position + Vector3.up * 1.62f - transform.forward * 0.16f;
    }

    private Vector3 ResolveDismountPosition()
    {
        if (dismountAnchor != null)
        {
            return GroundPosition(dismountAnchor.position);
        }

        Vector3[] offsets =
        {
            transform.right * 1.65f,
            -transform.right * 1.65f,
            -transform.forward * 1.75f,
            transform.forward * 1.9f
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 candidate = GroundPosition(transform.position + offsets[i] + Vector3.up * 0.25f);
            if (!Physics.CheckSphere(candidate + Vector3.up * 0.8f, 0.38f, ~0, QueryTriggerInteraction.Ignore))
            {
                return candidate;
            }
        }

        return GroundPosition(transform.position + transform.right * 1.65f);
    }

    private static Vector3 GroundPosition(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * 2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * 0.06f;
        }

        return position;
    }

    private void EnsureFirstPersonOverlay(PlayerRideState state)
    {
        if (state.ViewCamera == null || firstPersonRidingFrames == null || firstPersonRidingFrames.Length == 0)
        {
            return;
        }

        GameObject overlay = new("HorseRidingOverlay");
        overlay.transform.SetParent(state.ViewCamera.transform, false);
        overlay.transform.localPosition = firstPersonOverlayLocalPosition;
        overlay.transform.localRotation = Quaternion.identity;
        overlay.transform.localScale = Vector3.one;

        SpriteRenderer spriteRenderer = overlay.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = short.MaxValue;
        if (firstPersonRidingMaterial != null)
        {
            spriteRenderer.sharedMaterial = firstPersonRidingMaterial;
        }

        playerState.OverlayObject = overlay;
        playerState.OverlayRenderer = spriteRenderer;
        ApplyFirstPersonOverlaySprite(spriteRenderer, firstPersonRidingFrames[0]);
        overlayFrameTimer = 0f;
        overlayFrameIndex = 0;
    }

    private void AnimateFirstPersonOverlay()
    {
        if (playerState.OverlayRenderer == null || firstPersonRidingFrames == null || firstPersonRidingFrames.Length == 0)
        {
            return;
        }

        float speed01 = Mathf.InverseLerp(0f, Mathf.Max(0.1f, gallopSpeed), ProjectHorizontal(currentVelocity).magnitude);
        overlayFrameTimer += Time.deltaTime * Mathf.Lerp(0.45f, 1.55f, speed01);
        if (overlayFrameTimer >= firstPersonFrameDuration)
        {
            overlayFrameTimer -= firstPersonFrameDuration;
            overlayFrameIndex = (overlayFrameIndex + 1) % firstPersonRidingFrames.Length;
            ApplyFirstPersonOverlaySprite(playerState.OverlayRenderer, firstPersonRidingFrames[overlayFrameIndex]);
        }

        Transform overlayTransform = playerState.OverlayRenderer.transform;
        float bob = Mathf.Sin(Time.time * cameraBobFrequency) * 0.018f * speed01;
        overlayTransform.localPosition = firstPersonOverlayLocalPosition + new Vector3(0f, bob, 0f);
    }

    private void ApplyFirstPersonOverlaySprite(SpriteRenderer renderer, Sprite sprite)
    {
        if (renderer == null || sprite == null)
        {
            return;
        }

        renderer.sprite = sprite;
        Vector3 spriteSize = sprite.bounds.size;
        float width = Mathf.Max(0.0001f, spriteSize.x);
        float height = Mathf.Max(0.0001f, spriteSize.y);
        renderer.transform.localScale = new Vector3(
            firstPersonOverlaySize.x / width,
            firstPersonOverlaySize.y / height,
            1f);
    }

    private void DestroyFirstPersonOverlay()
    {
        if (playerState.OverlayObject != null)
        {
            Destroy(playerState.OverlayObject);
        }
    }

    private void DisableNpcRiderObject(ref NpcRideState state)
    {
        if (state.Rider == null || !state.HideRiderObject)
        {
            return;
        }

        state.Renderers = state.Rider.GetComponentsInChildren<Renderer>(true);
        state.RendererStates = new bool[state.Renderers.Length];
        for (int i = 0; i < state.Renderers.Length; i++)
        {
            state.RendererStates[i] = state.Renderers[i] != null && state.Renderers[i].enabled;
            if (state.Renderers[i] != null)
            {
                state.Renderers[i].enabled = false;
            }
        }

        state.Colliders = state.Rider.GetComponentsInChildren<Collider>(true);
        state.ColliderStates = new bool[state.Colliders.Length];
        for (int i = 0; i < state.Colliders.Length; i++)
        {
            state.ColliderStates[i] = state.Colliders[i] != null && state.Colliders[i].enabled;
            if (state.Colliders[i] != null)
            {
                state.Colliders[i].enabled = false;
            }
        }

        state.DisabledBehaviours = state.Rider.GetComponentsInChildren<MonoBehaviour>(true);
        state.BehaviourStates = new bool[state.DisabledBehaviours.Length];
        for (int i = 0; i < state.DisabledBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = state.DisabledBehaviours[i];
            state.BehaviourStates[i] = behaviour != null && behaviour.enabled;
            if (behaviour == null || behaviour is RetroHorseNpcRider || behaviour is RetroDamageable)
            {
                continue;
            }

            if (behaviour is RetroNpcAgent
                || behaviour is RetroMerchantCombatant
                || behaviour is DirectionalSpriteLocomotion
                || behaviour is DirectionalSpriteAnimator
                || behaviour is DirectionalSpriteBillboardLitRenderer
                || behaviour is DirectionalSpriteHitMask)
            {
                behaviour.enabled = false;
            }
        }

        state.Rigidbody = state.Rider.GetComponent<Rigidbody>();
        if (state.Rigidbody != null)
        {
            state.RigidbodyWasKinematic = state.Rigidbody.isKinematic;
            state.RigidbodyUsedGravity = state.Rigidbody.useGravity;
            state.RigidbodyVelocity = GetBodyVelocity(state.Rigidbody);
            state.RigidbodyAngularVelocity = state.Rigidbody.angularVelocity;
            state.Rigidbody.isKinematic = true;
            state.Rigidbody.useGravity = false;
            SetBodyVelocity(state.Rigidbody, Vector3.zero);
            state.Rigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void AttachNpcRider(ref NpcRideState state)
    {
        if (state.Rider == null)
        {
            return;
        }

        Transform riderTransform = state.Rider.transform;
        state.PreviousParent = riderTransform.parent;
        state.PreviousPosition = riderTransform.position;
        state.PreviousRotation = riderTransform.rotation;
        Transform parent = seatAnchor != null ? seatAnchor : transform;
        riderTransform.SetParent(parent, false);
        riderTransform.localPosition = Vector3.zero;
        riderTransform.localRotation = Quaternion.identity;
    }

    private void RestoreNpcRiderObject(NpcRideState state)
    {
        if (state.Rider == null)
        {
            return;
        }

        Transform riderTransform = state.Rider.transform;
        riderTransform.SetParent(state.PreviousParent, true);
        riderTransform.SetPositionAndRotation(ResolveDismountPosition(), transform.rotation);

        if (state.Renderers != null)
        {
            for (int i = 0; i < state.Renderers.Length; i++)
            {
                if (state.Renderers[i] != null && state.RendererStates != null && i < state.RendererStates.Length)
                {
                    state.Renderers[i].enabled = state.RendererStates[i];
                }
            }
        }

        if (state.Colliders != null)
        {
            for (int i = 0; i < state.Colliders.Length; i++)
            {
                if (state.Colliders[i] != null && state.ColliderStates != null && i < state.ColliderStates.Length)
                {
                    state.Colliders[i].enabled = state.ColliderStates[i];
                }
            }
        }

        if (state.DisabledBehaviours != null)
        {
            for (int i = 0; i < state.DisabledBehaviours.Length; i++)
            {
                MonoBehaviour behaviour = state.DisabledBehaviours[i];
                if (behaviour != null && state.BehaviourStates != null && i < state.BehaviourStates.Length)
                {
                    behaviour.enabled = state.BehaviourStates[i];
                }
            }
        }

        if (state.Rigidbody != null)
        {
            state.Rigidbody.isKinematic = state.RigidbodyWasKinematic;
            state.Rigidbody.useGravity = state.RigidbodyUsedGravity;
            SetBodyVelocity(state.Rigidbody, state.RigidbodyVelocity);
            state.Rigidbody.angularVelocity = state.RigidbodyAngularVelocity;
        }
    }

    private bool IsCurrentRiderCollider(Collider hit)
    {
        if (hit == null)
        {
            return false;
        }

        GameObject rider = RiderObject;
        return rider != null && hit.transform.IsChildOf(rider.transform);
    }

    private void PickWanderDestination(bool immediate)
    {
        homePosition = immediate ? transform.position : homePosition;
        Vector2 offset = Random.insideUnitCircle * emptyWanderRadius;
        wanderDestination = homePosition + new Vector3(offset.x, 0f, offset.y);
        nextWanderTime = Time.time + emptyWanderRetargetTime * Random.Range(0.7f, 1.4f);
    }

    private void OnGUI()
    {
        if (riderMode != RiderMode.Player)
        {
            return;
        }

        float width = Mathf.Min(330f, Screen.width - 32f);
        Rect rect = new((Screen.width - width) * 0.5f, Screen.height - 74f, width, 34f);
        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.56f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.93f);
        GUI.Label(rect, $"F  Dismount {ResolveMountDisplayName()}", PromptStyle);
        GUI.color = previous;
    }

    private string ResolveMountDisplayName()
    {
        return string.IsNullOrWhiteSpace(mountDisplayName) ? "Mount" : mountDisplayName;
    }

    private void EnsureVisualPhaseOffset()
    {
        if (visualPhaseOffset < 0f)
        {
            visualPhaseOffset = Random.Range(0f, 1000f);
        }
    }

    private static GUIStyle promptStyle;
    private static GUIStyle PromptStyle => promptStyle ??= new GUIStyle(GUI.skin.label)
    {
        alignment = TextAnchor.MiddleCenter,
        fontSize = 18,
        fontStyle = FontStyle.Bold,
        clipping = TextClipping.Clip
    };

    private static Vector3 ProjectHorizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        return ProjectHorizontal(a - b).magnitude;
    }

    private static float NormalizePitch(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }

    private static RetroFpsController ResolvePlayerController(GameObject actor)
    {
        if (actor == null)
        {
            return null;
        }

        RetroFpsController controller = actor.GetComponent<RetroFpsController>();
        if (controller != null)
        {
            return controller;
        }

        controller = actor.GetComponentInParent<RetroFpsController>();
        if (controller != null)
        {
            return controller;
        }

        return actor.GetComponentInChildren<RetroFpsController>(true);
    }

    private static Vector3 GetBodyVelocity(Rigidbody body)
    {
#if UNITY_6000_0_OR_NEWER
        return body.linearVelocity;
#else
        return body.velocity;
#endif
    }

    private static void SetBodyVelocity(Rigidbody body, Vector3 velocity)
    {
#if UNITY_6000_0_OR_NEWER
        body.linearVelocity = velocity;
#else
        body.velocity = velocity;
#endif
    }

    private struct ObjectState
    {
        public readonly GameObject Target;
        public readonly bool WasActive;

        public ObjectState(GameObject target, bool wasActive)
        {
            Target = target;
            WasActive = wasActive;
        }
    }

    private struct PlayerRideState
    {
        public GameObject Actor;
        public Transform ActorTransform;
        public RetroFpsController Controller;
        public Rigidbody Body;
        public CapsuleCollider Capsule;
        public RetroInteractor Interactor;
        public RetroWeaponSystem WeaponSystem;
        public Camera ViewCamera;
        public Transform CameraTransform;
        public Vector3 CameraBaseLocalPosition;
        public Quaternion CameraBaseLocalRotation;
        public float BaseFieldOfView;
        public bool ControllerWasEnabled;
        public bool InteractorWasEnabled;
        public bool WeaponSystemWasEnabled;
        public bool CapsuleWasEnabled;
        public bool ControllerViewModelPresentationWasEnabled;
        public bool BodyWasKinematic;
        public bool BodyUsedGravity;
        public RigidbodyConstraints BodyConstraints;
        public Vector3 BodyVelocity;
        public Vector3 BodyAngularVelocity;
        public GameObject OverlayObject;
        public SpriteRenderer OverlayRenderer;
    }

    private struct NpcRideState
    {
        public GameObject Rider;
        public bool HideRiderObject;
        public Transform PreviousParent;
        public Vector3 PreviousPosition;
        public Quaternion PreviousRotation;
        public Renderer[] Renderers;
        public bool[] RendererStates;
        public Collider[] Colliders;
        public bool[] ColliderStates;
        public MonoBehaviour[] DisabledBehaviours;
        public bool[] BehaviourStates;
        public Rigidbody Rigidbody;
        public bool RigidbodyWasKinematic;
        public bool RigidbodyUsedGravity;
        public Vector3 RigidbodyVelocity;
        public Vector3 RigidbodyAngularVelocity;

        public NpcRideState(GameObject rider, bool hideRiderObject)
        {
            Rider = rider;
            HideRiderObject = hideRiderObject;
            PreviousParent = null;
            PreviousPosition = Vector3.zero;
            PreviousRotation = Quaternion.identity;
            Renderers = null;
            RendererStates = null;
            Colliders = null;
            ColliderStates = null;
            DisabledBehaviours = null;
            BehaviourStates = null;
            Rigidbody = null;
            RigidbodyWasKinematic = false;
            RigidbodyUsedGravity = false;
            RigidbodyVelocity = Vector3.zero;
            RigidbodyAngularVelocity = Vector3.zero;
        }
    }
}
