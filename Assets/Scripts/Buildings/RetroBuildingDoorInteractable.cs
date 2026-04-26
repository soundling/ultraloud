using UnityEngine;

[DisallowMultipleComponent]
public sealed class RetroBuildingDoorInteractable : RetroInteractableBehaviour
{
    [Header("Door Teleport")]
    [SerializeField] private Transform teleportTarget;
    [SerializeField] private bool alignActorYaw = true;
    [SerializeField] private bool clearRigidbodyVelocity = true;
    [SerializeField] private string enteredMessage = "Entered.";
    [SerializeField, Min(0.1f)] private float enteredMessageDuration = 1.1f;

    protected override string DefaultInteractionVerb => "Enter";
    protected override string DefaultInteractionName => "building";

    public void SetTeleportTarget(Transform target)
    {
        teleportTarget = target;
    }

    protected override void InteractInternal(in RetroInteractionContext context)
    {
        Transform actorTransform = ResolveActorTransform(context);
        if (actorTransform == null || teleportTarget == null)
        {
            return;
        }

        Rigidbody actorBody = actorTransform.GetComponent<Rigidbody>();
        if (actorBody == null && context.Actor != null)
        {
            actorBody = context.Actor.GetComponentInParent<Rigidbody>();
        }

        Vector3 targetPosition = teleportTarget.position;
        Quaternion targetRotation = alignActorYaw
            ? Quaternion.Euler(0f, teleportTarget.eulerAngles.y, 0f)
            : actorTransform.rotation;

        if (actorBody != null)
        {
            actorBody.position = targetPosition;
            actorBody.rotation = targetRotation;
            if (clearRigidbodyVelocity)
            {
#if UNITY_6000_0_OR_NEWER
                actorBody.linearVelocity = Vector3.zero;
#else
                actorBody.velocity = Vector3.zero;
#endif
                actorBody.angularVelocity = Vector3.zero;
            }
        }
        else
        {
            actorTransform.SetPositionAndRotation(targetPosition, targetRotation);
        }

        Physics.SyncTransforms();

        if (!string.IsNullOrWhiteSpace(enteredMessage))
        {
            context.Interactor?.ShowStatusMessage(enteredMessage, enteredMessageDuration);
        }
    }

    private static Transform ResolveActorTransform(in RetroInteractionContext context)
    {
        if (context.ActorTransform != null)
        {
            return context.ActorTransform;
        }

        if (context.Actor != null)
        {
            return context.Actor.transform;
        }

        return context.Interactor != null ? context.Interactor.transform : null;
    }
}
