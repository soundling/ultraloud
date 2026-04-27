using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class RetroInteractor : MonoBehaviour
{
    private const int HitBufferSize = 64;

    [Header("References")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string interactActionName = "Interact";

    [Header("Probe")]
    [SerializeField, Min(0.1f)] private float maxDistance = 3.2f;
    [SerializeField, Min(0f)] private float probeRadius = 0.26f;
    [SerializeField] private LayerMask interactionMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Input")]
    [SerializeField] private Key fallbackKeyboardKey = Key.F;
    [SerializeField] private string promptKeyLabel = "F";

    [Header("HUD")]
    [SerializeField] private bool drawPrompt = true;
    [SerializeField] private Color promptBackgroundColor = new Color(0f, 0f, 0f, 0.58f);
    [SerializeField] private Color promptTextColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] private Color keyBackgroundColor = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] private Color keyTextColor = new Color(0f, 0f, 0f, 0.92f);

    private readonly RaycastHit[] hitBuffer = new RaycastHit[HitBufferSize];
    private InputActionMap actionMap;
    private InputAction interactAction;
    private IRetroInteractable focusedInteractable;
    private RetroInteractionContext focusedContext;
    private IRetroInteractable currentInteractable;
    private RetroInteractionContext currentContext;
    private GUIStyle promptStyle;
    private GUIStyle keyStyle;
    private GUIStyle messageStyle;
    private bool ownsActionMap;
    private string statusMessage;
    private float statusMessageUntilTime = -999f;

    public IRetroInteractable CurrentInteractable => currentInteractable;
    public bool HasInteractable => !IsMissing(currentInteractable);

    private void Reset()
    {
        AutoWireReferences();
    }

    private void Awake()
    {
        AutoWireReferences();
        ResolveActions();
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
        SetFocusedInteractable(null, default);
        if (ownsActionMap && actionMap != null)
        {
            actionMap.Disable();
        }
    }

    private void OnValidate()
    {
        maxDistance = Mathf.Max(0.1f, maxDistance);
        probeRadius = Mathf.Max(0f, probeRadius);
    }

    private void Update()
    {
        if (viewCamera == null)
        {
            AutoWireReferences();
        }

        FindCurrentInteractable();
        if (!IsMissing(currentInteractable) && WasInteractPressed())
        {
            InteractWithCurrent();
        }
    }

    public void ShowStatusMessage(string message, float duration)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        statusMessage = message;
        statusMessageUntilTime = Time.unscaledTime + Mathf.Max(0.1f, duration);
    }

    private void AutoWireReferences()
    {
        RetroFpsController controller = GetComponent<RetroFpsController>();
        if (controller != null)
        {
            if (viewCamera == null)
            {
                viewCamera = controller.ViewCamera;
            }

            if (inputActions == null)
            {
                inputActions = controller.InputActionsAsset;
            }
        }

        if (viewCamera == null)
        {
            viewCamera = GetComponentInChildren<Camera>(true);
        }
    }

    private void ResolveActions()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            playerInput = GetComponentInParent<PlayerInput>();
        }

        InputActionAsset source = playerInput != null ? playerInput.actions : inputActions;
        ownsActionMap = playerInput == null;
        if (source == null)
        {
            return;
        }

        actionMap = source.FindActionMap(actionMapName, false);
        if (actionMap == null)
        {
            Debug.LogWarning($"{nameof(RetroInteractor)} could not find action map '{actionMapName}' on {source.name}.", this);
            return;
        }

        interactAction = actionMap.FindAction(interactActionName, false);
        if (interactAction == null)
        {
            Debug.LogWarning($"{nameof(RetroInteractor)} could not find action '{interactActionName}' on action map '{actionMapName}'.", this);
        }
    }

    private void FindCurrentInteractable()
    {
        currentInteractable = null;
        currentContext = default;
        if (viewCamera == null)
        {
            SetFocusedInteractable(null, default);
            return;
        }

        Transform cameraTransform = viewCamera.transform;
        Vector3 origin = cameraTransform.position;
        Vector3 direction = cameraTransform.forward;
        int hitCount = probeRadius > 0.001f
            ? Physics.SphereCastNonAlloc(origin, probeRadius, direction, hitBuffer, maxDistance, interactionMask, triggerInteraction)
            : Physics.RaycastNonAlloc(origin, direction, hitBuffer, maxDistance, interactionMask, triggerInteraction);

        float bestScore = float.NegativeInfinity;
        float nearestBlockDistance = float.PositiveInfinity;
        IRetroInteractable bestInteractable = null;
        RetroInteractionContext bestContext = default;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null || IsSelfCollider(hitCollider))
            {
                continue;
            }

            IRetroInteractable interactable = FindInteractable(hitCollider);
            if (interactable == null)
            {
                if (!hitCollider.isTrigger && hit.distance < nearestBlockDistance)
                {
                    nearestBlockDistance = hit.distance;
                }

                continue;
            }

            float resolvedDistance = ResolveHitDistance(origin, hit);
            if (resolvedDistance > maxDistance || resolvedDistance > interactable.InteractionMaxDistance + 0.001f)
            {
                continue;
            }

            RetroInteractionContext context = BuildContext(interactable, hitCollider, origin, direction, hit, resolvedDistance);
            if (!interactable.CanInteract(context))
            {
                continue;
            }

            Vector3 targetPoint = context.Point;
            Vector3 toTarget = targetPoint - origin;
            float aim = toTarget.sqrMagnitude > 0.0001f ? Vector3.Dot(direction, toTarget.normalized) : 1f;
            float distanceScore = Mathf.InverseLerp(maxDistance, 0f, resolvedDistance) * 100f;
            float score = interactable.InteractionPriority * 1000f + distanceScore + aim * 20f;
            if (score > bestScore)
            {
                bestScore = score;
                bestInteractable = interactable;
                bestContext = context;
            }
        }

        if (!IsMissing(bestInteractable) && bestContext.Distance <= nearestBlockDistance + Mathf.Max(0.12f, probeRadius))
        {
            currentInteractable = bestInteractable;
            currentContext = bestContext;
        }

        SetFocusedInteractable(currentInteractable, currentContext);
    }

    private void SetFocusedInteractable(IRetroInteractable interactable, RetroInteractionContext context)
    {
        if (SameInteractable(focusedInteractable, interactable))
        {
            focusedContext = context;
            return;
        }

        if (!IsMissing(focusedInteractable))
        {
            focusedInteractable.SetInteractionFocused(false, focusedContext);
            RetroGameContext.Events.Publish(new RetroInteractionFocusEvent(gameObject, focusedInteractable.InteractionGameObject, focusedContext.Point, false));
        }

        focusedInteractable = interactable;
        focusedContext = context;

        if (!IsMissing(focusedInteractable))
        {
            focusedInteractable.SetInteractionFocused(true, focusedContext);
            RetroGameContext.Events.Publish(new RetroInteractionFocusEvent(gameObject, focusedInteractable.InteractionGameObject, focusedContext.Point, true));
        }
    }

    private void InteractWithCurrent()
    {
        string prompt = currentInteractable.GetInteractionPrompt(currentContext);
        GameObject targetObject = currentInteractable.InteractionGameObject;
        currentInteractable.Interact(currentContext);
        RetroGameContext.Events.Publish(new RetroInteractionEvent(gameObject, targetObject, prompt, currentContext.Point));
    }

    private bool WasInteractPressed()
    {
        if (interactAction != null && interactAction.WasPressedThisFrame())
        {
            return true;
        }

        if (fallbackKeyboardKey == Key.None || Keyboard.current == null)
        {
            return false;
        }

        return Keyboard.current[fallbackKeyboardKey].wasPressedThisFrame;
    }

    private RetroInteractionContext BuildContext(
        IRetroInteractable interactable,
        Collider hitCollider,
        Vector3 origin,
        Vector3 direction,
        RaycastHit hit,
        float distance)
    {
        Vector3 point = ResolveHitPoint(origin, hit, distance);
        Vector3 normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal : -direction;
        return new RetroInteractionContext(
            this,
            gameObject,
            transform,
            viewCamera,
            interactable,
            hitCollider,
            origin,
            direction,
            point,
            normal,
            distance);
    }

    private Vector3 ResolveHitPoint(Vector3 origin, RaycastHit hit, float distance)
    {
        if (hit.point.sqrMagnitude > 0.000001f || hit.distance > 0.001f)
        {
            return hit.point;
        }

        if (hit.collider != null)
        {
            return hit.collider.ClosestPoint(origin);
        }

        return origin + viewCamera.transform.forward * distance;
    }

    private static float ResolveHitDistance(Vector3 origin, RaycastHit hit)
    {
        if (hit.distance > 0.001f)
        {
            return hit.distance;
        }

        if (hit.collider == null)
        {
            return 0f;
        }

        return Vector3.Distance(origin, hit.collider.ClosestPoint(origin));
    }

    private bool IsSelfCollider(Collider hitCollider)
    {
        Transform hitTransform = hitCollider.transform;
        return hitTransform == transform || hitTransform.IsChildOf(transform);
    }

    private static IRetroInteractable FindInteractable(Collider hitCollider)
    {
        Transform current = hitCollider.transform;
        while (current != null)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && behaviours[i].isActiveAndEnabled && behaviours[i] is IRetroInteractable interactable)
                {
                    return interactable;
                }
            }

            current = current.parent;
        }

        return null;
    }

    private static bool SameInteractable(IRetroInteractable left, IRetroInteractable right)
    {
        if (IsMissing(left) || IsMissing(right))
        {
            return IsMissing(left) && IsMissing(right);
        }

        Object leftObject = left as Object;
        Object rightObject = right as Object;
        if (leftObject != null || rightObject != null)
        {
            return leftObject == rightObject;
        }

        return ReferenceEquals(left, right);
    }

    private static bool IsMissing(IRetroInteractable interactable)
    {
        if (interactable == null)
        {
            return true;
        }

        return interactable is Object unityObject && unityObject == null;
    }

    private void OnGUI()
    {
        EnsureGuiStyles();
        DrawStatusMessage();
        if (!drawPrompt || IsMissing(currentInteractable))
        {
            return;
        }

        string prompt = currentInteractable.GetInteractionPrompt(currentContext);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        string keyLabel = string.IsNullOrWhiteSpace(promptKeyLabel) ? fallbackKeyboardKey.ToString() : promptKeyLabel;
        float panelWidth = Mathf.Min(430f, Screen.width - 32f);
        float panelHeight = 42f;
        float maxPanelY = Mathf.Max(48f, Screen.height - panelHeight - 18f);
        float panelY = Mathf.Clamp(Screen.height - 104f, 48f, maxPanelY);
        Rect panelRect = new Rect((Screen.width - panelWidth) * 0.5f, panelY, panelWidth, panelHeight);
        Rect keyRect = new Rect(panelRect.x + 10f, panelRect.y + 7f, 34f, 28f);
        Rect textRect = new Rect(keyRect.xMax + 10f, panelRect.y + 8f, panelRect.width - 58f, 26f);

        Color previousColor = GUI.color;
        GUI.color = promptBackgroundColor;
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = keyBackgroundColor;
        GUI.DrawTexture(keyRect, Texture2D.whiteTexture);
        GUI.color = keyTextColor;
        GUI.Label(keyRect, keyLabel, keyStyle);
        GUI.color = promptTextColor;
        GUI.Label(textRect, prompt, promptStyle);
        GUI.color = previousColor;
    }

    private void DrawStatusMessage()
    {
        if (string.IsNullOrWhiteSpace(statusMessage) || Time.unscaledTime >= statusMessageUntilTime)
        {
            return;
        }

        float fade = Mathf.Clamp01((statusMessageUntilTime - Time.unscaledTime) / 0.25f);
        float width = Mathf.Min(520f, Screen.width - 32f);
        float maxY = Mathf.Max(48f, Screen.height - 58f);
        float y = Mathf.Clamp(Screen.height - 154f, 48f, maxY);
        Rect rect = new Rect((Screen.width - width) * 0.5f, y, width, 34f);
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f * fade);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.95f * fade);
        GUI.Label(rect, statusMessage, messageStyle);
        GUI.color = previousColor;
    }

    private void EnsureGuiStyles()
    {
        if (promptStyle != null)
        {
            return;
        }

        promptStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Clip
        };

        keyStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Clip
        };

        messageStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            clipping = TextClipping.Clip
        };
    }
}
