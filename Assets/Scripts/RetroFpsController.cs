using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public sealed class RetroFpsController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform viewModelRoot;
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string sprintActionName = "Sprint";
    [SerializeField] private string crouchActionName = "Crouch";
    [SerializeField] private bool lockCursorOnEnable = true;
    [SerializeField] private bool hideBodyRenderer = true;

    [Header("Look")]
    [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.14f;
    [SerializeField, Min(1f)] private float gamepadLookSpeed = 180f;
    [SerializeField, Range(30f, 89f)] private float maxPitch = 88f;
    [SerializeField] private bool invertY;
    [SerializeField, Min(0f)] private float rollAmount = 4f;
    [SerializeField, Min(0f)] private float rollSmoothing = 12f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 11f;
    [SerializeField, Min(0f)] private float sprintSpeed = 15f;
    [SerializeField, Min(0f)] private float crouchSpeed = 7.5f;
    [SerializeField, Min(0f)] private float groundAcceleration = 90f;
    [SerializeField, Min(0f)] private float airAcceleration = 28f;
    [SerializeField, Min(0f)] private float airMaxSpeed = 13f;
    [SerializeField, Min(0f)] private float friction = 10f;
    [SerializeField, Min(0f)] private float stopSpeed = 4f;
    [SerializeField, Min(0f)] private float jumpVelocity = 7.2f;
    [SerializeField, Min(0f)] private float groundedForgetTime = 0.08f;
    [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
    [SerializeField, Min(0f)] private float jumpBuffer = 0.12f;
    [SerializeField, Min(0f)] private float gravityMultiplier = 2.2f;
    [SerializeField, Min(0f)] private float fallGravityMultiplier = 3.4f;
    [SerializeField, Range(0f, 89f)] private float maxGroundAngle = 55f;
    [SerializeField, Min(0f)] private float groundStickForce = 5f;

    [Header("Presentation")]
    [SerializeField, Min(0f)] private float crouchCameraOffset = 0.45f;
    [SerializeField, Min(0f)] private float crouchBlendSpeed = 12f;
    [SerializeField, Min(0f)] private float sprintFovBoost = 6f;
    [SerializeField, Min(0f)] private float fovBlendSpeed = 8f;
    [SerializeField, Min(0f)] private float headBobFrequency = 2.1f;
    [SerializeField, Min(0f)] private float headBobVerticalAmplitude = 0.05f;
    [SerializeField, Min(0f)] private float headBobSideAmplitude = 0.03f;
    [SerializeField, Min(0f)] private float landingDip = 0.08f;
    [SerializeField, Min(0f)] private float landingRecoverSpeed = 10f;
    [SerializeField, Min(0f)] private float viewModelBobMultiplier = 1.4f;
    [SerializeField, Min(0f)] private float viewModelSwayPosition = 0.025f;
    [SerializeField, Min(0f)] private float viewModelSwayRotation = 4.5f;

    private Rigidbody body;
    private CapsuleCollider capsule;
    private PlayerInput playerInput;
    private InputActionMap actionMap;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private bool ownsActionMap;
    private bool usingGamepadLook;
    private bool grounded;
    private bool wantsSprint;
    private bool wantsCrouch;
    private float yaw;
    private float pitch;
    private float lastJumpPressedTime = -999f;
    private float lastGroundContactTime = -999f;
    private float crouchBlend;
    private float bobTime;
    private float currentRoll;
    private float landingOffset;
    private float baseFieldOfView;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 groundNormal = Vector3.up;
    private Vector3 cameraBaseLocalPosition;
    private Quaternion cameraBaseLocalRotation = Quaternion.identity;
    private Vector3 viewModelBaseCameraLocalPosition;
    private Quaternion viewModelBaseCameraLocalRotation = Quaternion.identity;

    private void Reset()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        AutoWireReferences();
        ConfigureBody();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        AutoWireReferences();
        ConfigureBody();
        CachePresentationAnchors();
        ResolveActions();
        ApplyBodyVisibility();

        yaw = transform.eulerAngles.y;
        pitch = NormalizePitch(viewCamera != null ? viewCamera.transform.localEulerAngles.x : 0f);
    }

    private void OnEnable()
    {
        if (actionMap == null)
        {
            ResolveActions();
        }

        if (ownsActionMap && actionMap != null)
        {
            actionMap.Enable();
        }
    }

    private void OnDisable()
    {
        if (ownsActionMap && actionMap != null)
        {
            actionMap.Disable();
        }
    }

    private void Start()
    {
        if (viewCamera != null)
        {
            baseFieldOfView = viewCamera.fieldOfView;
        }

        if (lockCursorOnEnable)
        {
            LockCursor();
        }
    }

    private void Update()
    {
        HandleCursor();
        ReadInput();
        UpdateLook(Time.unscaledDeltaTime);
    }

    private void FixedUpdate()
    {
        if (body == null)
        {
            return;
        }

        float deltaTime = Time.fixedDeltaTime;
        bool wasGrounded = grounded;
        grounded = Time.time - lastGroundContactTime <= groundedForgetTime;

        if (!grounded)
        {
            groundNormal = Vector3.up;
        }

        Vector3 velocity = BodyVelocity;
        float impactVelocity = velocity.y;
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        Vector3 wishDirection = GetWishDirection();
        float targetSpeed = GetTargetSpeed();

        if (grounded)
        {
            horizontalVelocity = ApplyFriction(horizontalVelocity, deltaTime);
            horizontalVelocity = Accelerate(horizontalVelocity, wishDirection, targetSpeed, groundAcceleration, deltaTime);

            if (CanJump())
            {
                velocity.y = jumpVelocity;
                grounded = false;
                lastJumpPressedTime = -999f;
                lastGroundContactTime = -999f;
            }
            else
            {
                velocity.y = -groundStickForce;
            }
        }
        else
        {
            horizontalVelocity = Accelerate(horizontalVelocity, wishDirection, Mathf.Min(targetSpeed, airMaxSpeed), airAcceleration, deltaTime);

            float gravityScale = velocity.y > 0f && IsJumpHeld()
                ? gravityMultiplier
                : fallGravityMultiplier;
            velocity.y += Physics.gravity.y * gravityScale * deltaTime;
        }

        velocity.x = horizontalVelocity.x;
        velocity.z = horizontalVelocity.z;
        BodyVelocity = velocity;
        body.angularVelocity = Vector3.zero;

        if (!wasGrounded && grounded && impactVelocity < -2f)
        {
            float impactAmount = Mathf.InverseLerp(2f, 14f, -impactVelocity);
            landingOffset -= landingDip * impactAmount;
        }
    }

    private void LateUpdate()
    {
        UpdatePresentation(Time.deltaTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        EvaluateGroundContacts(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        EvaluateGroundContacts(collision);
    }

    private void AutoWireReferences()
    {
        if (viewCamera == null)
        {
            viewCamera = GetComponentInChildren<Camera>(true);
        }

        if (viewModelRoot == null)
        {
            Transform quad = FindNamedChildRecursive(transform, "Quad");
            if (quad != null)
            {
                viewModelRoot = quad;
            }
        }

        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponent<Renderer>();
        }

        if (bodyRenderer == null)
        {
            Transform capsuleVisual = FindNamedChildRecursive(transform, "Capsule");
            if (capsuleVisual != null)
            {
                bodyRenderer = capsuleVisual.GetComponent<Renderer>();
            }
        }

        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponentInChildren<MeshRenderer>(true);
        }
    }

    private void ConfigureBody()
    {
        if (body == null)
        {
            return;
        }

        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        SetBodyDamping(0f, 0f);

        body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.constraints &= ~RigidbodyConstraints.FreezeRotationY;
    }

    private void ApplyBodyVisibility()
    {
        if (!hideBodyRenderer || bodyRenderer == null)
        {
            return;
        }

        bodyRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        bodyRenderer.receiveShadows = false;
    }

    private void CachePresentationAnchors()
    {
        if (viewCamera != null)
        {
            cameraBaseLocalPosition = viewCamera.transform.localPosition;
            cameraBaseLocalRotation = viewCamera.transform.localRotation;
            baseFieldOfView = viewCamera.fieldOfView;
        }

        if (viewCamera != null && viewModelRoot != null)
        {
            viewModelBaseCameraLocalPosition = viewCamera.transform.InverseTransformPoint(viewModelRoot.position);
            viewModelBaseCameraLocalRotation = Quaternion.Inverse(viewCamera.transform.rotation) * viewModelRoot.rotation;
        }
    }

    private void ResolveActions()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            playerInput = GetComponentInParent<PlayerInput>();
        }

        InputActionAsset source = playerInput != null ? playerInput.actions : inputActions;
        ownsActionMap = playerInput == null;

        if (source == null)
        {
            Debug.LogWarning($"{nameof(RetroFpsController)} on {name} needs a PlayerInput component or an InputActionAsset assigned.", this);
            return;
        }

        actionMap = source.FindActionMap(actionMapName, false);
        if (actionMap == null)
        {
            Debug.LogWarning($"{nameof(RetroFpsController)} could not find action map '{actionMapName}' on {source.name}.", this);
            return;
        }

        moveAction = actionMap.FindAction(moveActionName, false);
        lookAction = actionMap.FindAction(lookActionName, false);
        jumpAction = actionMap.FindAction(jumpActionName, false);
        sprintAction = actionMap.FindAction(sprintActionName, false);
        crouchAction = actionMap.FindAction(crouchActionName, false);
    }

    private void HandleCursor()
    {
        if (!lockCursorOnEnable)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
        }
    }

    private void ReadInput()
    {
        moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        wantsSprint = sprintAction != null && sprintAction.IsPressed();
        wantsCrouch = crouchAction != null && crouchAction.IsPressed();

        if (jumpAction != null && jumpAction.WasPressedThisFrame())
        {
            lastJumpPressedTime = Time.time;
        }
    }

    private void UpdateLook(float deltaTime)
    {
        if (lookAction == null || viewCamera == null)
        {
            return;
        }

        lookInput = lookAction.ReadValue<Vector2>();
        if (lookInput.sqrMagnitude > 0.0001f)
        {
            InputDevice device = lookAction.activeControl != null ? lookAction.activeControl.device : null;
            if (device is Gamepad || device is Joystick)
            {
                usingGamepadLook = true;
            }
            else if (device is Pointer)
            {
                usingGamepadLook = false;
            }
        }

        if (!usingGamepadLook && Cursor.lockState != CursorLockMode.Locked)
        {
            lookInput = Vector2.zero;
            return;
        }

        float lookScale = usingGamepadLook ? gamepadLookSpeed * deltaTime : mouseSensitivity;
        yaw += lookInput.x * lookScale;
        pitch += lookInput.y * lookScale * (invertY ? 1f : -1f);
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void UpdatePresentation(float deltaTime)
    {
        if (viewCamera == null)
        {
            return;
        }

        float sprintAlpha = Mathf.Clamp01(Mathf.InverseLerp(moveSpeed, sprintSpeed, PlanarVelocity.magnitude));
        bool movingOnGround = grounded && moveInput.sqrMagnitude > 0.01f;

        if (movingOnGround)
        {
            float bobRate = headBobFrequency * Mathf.Lerp(0.75f, 1.5f, sprintAlpha + 0.25f);
            bobTime += deltaTime * bobRate;
        }

        crouchBlend = Mathf.MoveTowards(crouchBlend, wantsCrouch ? 1f : 0f, crouchBlendSpeed * deltaTime);
        landingOffset = Mathf.MoveTowards(landingOffset, 0f, landingRecoverSpeed * deltaTime);

        Vector3 bobOffset = Vector3.zero;
        if (movingOnGround)
        {
            float bobWave = bobTime * Mathf.PI * 2f;
            bobOffset = new Vector3(
                Mathf.Sin(bobWave) * headBobSideAmplitude,
                Mathf.Abs(Mathf.Cos(bobWave)) * headBobVerticalAmplitude,
                0f) * Mathf.Clamp01(PlanarVelocity.magnitude / sprintSpeed);
        }

        float targetRoll = -moveInput.x * rollAmount;
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, 1f - Mathf.Exp(-rollSmoothing * deltaTime));

        Transform cameraTransform = viewCamera.transform;
        cameraTransform.localPosition = cameraBaseLocalPosition
            + Vector3.down * (crouchCameraOffset * crouchBlend)
            + bobOffset
            + Vector3.up * landingOffset;
        cameraTransform.localRotation = cameraBaseLocalRotation * Quaternion.Euler(pitch, 0f, currentRoll + bobOffset.x * 28f);

        float targetFov = baseFieldOfView + sprintFovBoost * sprintAlpha;
        viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-fovBlendSpeed * deltaTime));

        if (viewModelRoot == null)
        {
            return;
        }

        float swayScale = usingGamepadLook ? 0.02f : 0.0025f;
        Vector2 swayInput = Vector2.ClampMagnitude(lookInput * swayScale, 1f);
        Vector3 viewModelLocalOffset = viewModelBaseCameraLocalPosition
            + bobOffset * viewModelBobMultiplier
            + Vector3.down * (crouchCameraOffset * crouchBlend * 0.35f)
            + Vector3.up * (landingOffset * 0.5f)
            + new Vector3(-swayInput.x, -swayInput.y, 0f) * viewModelSwayPosition;
        Quaternion viewModelLocalRotation = viewModelBaseCameraLocalRotation * Quaternion.Euler(
            swayInput.y * viewModelSwayRotation,
            -swayInput.x * viewModelSwayRotation,
            -moveInput.x * viewModelSwayRotation * 0.65f);

        viewModelRoot.SetPositionAndRotation(
            cameraTransform.TransformPoint(viewModelLocalOffset),
            cameraTransform.rotation * viewModelLocalRotation);
    }

    private void EvaluateGroundContacts(Collision collision)
    {
        float minGroundDot = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            float upDot = Vector3.Dot(contact.normal, Vector3.up);
            if (upDot < minGroundDot)
            {
                continue;
            }

            lastGroundContactTime = Time.time;
            groundNormal = contact.normal;
            return;
        }
    }

    private Vector3 GetWishDirection()
    {
        Vector3 wishDirection = transform.forward * moveInput.y + transform.right * moveInput.x;
        if (wishDirection.sqrMagnitude > 1f)
        {
            wishDirection.Normalize();
        }

        if (grounded && wishDirection.sqrMagnitude > 0.0001f)
        {
            wishDirection = Vector3.ProjectOnPlane(wishDirection, groundNormal).normalized;
        }

        return wishDirection;
    }

    private float GetTargetSpeed()
    {
        if (wantsCrouch)
        {
            return crouchSpeed;
        }

        if (wantsSprint && moveInput.y > 0.1f)
        {
            return sprintSpeed;
        }

        return moveSpeed;
    }

    private bool CanJump()
    {
        return Time.time - lastJumpPressedTime <= jumpBuffer
            && Time.time - lastGroundContactTime <= coyoteTime;
    }

    private bool IsJumpHeld()
    {
        return jumpAction != null && jumpAction.IsPressed();
    }

    private Vector3 ApplyFriction(Vector3 velocity, float deltaTime)
    {
        float speed = velocity.magnitude;
        if (speed < 0.001f)
        {
            return Vector3.zero;
        }

        float control = speed < stopSpeed ? stopSpeed : speed;
        float drop = control * friction * deltaTime;
        float nextSpeed = Mathf.Max(0f, speed - drop);
        return velocity * (nextSpeed / speed);
    }

    private static Vector3 Accelerate(Vector3 velocity, Vector3 wishDirection, float wishSpeed, float acceleration, float deltaTime)
    {
        if (wishDirection.sqrMagnitude < 0.0001f || wishSpeed <= 0f)
        {
            return velocity;
        }

        float currentSpeed = Vector3.Dot(velocity, wishDirection);
        float addSpeed = wishSpeed - currentSpeed;
        if (addSpeed <= 0f)
        {
            return velocity;
        }

        float accelSpeed = acceleration * deltaTime * wishSpeed;
        if (accelSpeed > addSpeed)
        {
            accelSpeed = addSpeed;
        }

        return velocity + wishDirection * accelSpeed;
    }

    private Vector3 PlanarVelocity => body == null
        ? Vector3.zero
        : Vector3.ProjectOnPlane(BodyVelocity, Vector3.up);

    private Vector3 BodyVelocity
    {
        get
        {
#if UNITY_6000_0_OR_NEWER
            return body.linearVelocity;
#else
            return body.velocity;
#endif
        }
        set
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = value;
#else
            body.velocity = value;
#endif
        }
    }

    private void SetBodyDamping(float linear, float angular)
    {
#if UNITY_6000_0_OR_NEWER
        body.linearDamping = linear;
        body.angularDamping = angular;
#else
        body.drag = linear;
        body.angularDrag = angular;
#endif
    }

    private static float NormalizePitch(float angle)
    {
        if (angle > 180f)
        {
            angle -= 360f;
        }

        return angle;
    }

    private static Transform FindNamedChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nestedMatch = FindNamedChildRecursive(child, childName);
            if (nestedMatch != null)
            {
                return nestedMatch;
            }
        }

        return null;
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
