using UnityEngine;

public readonly struct RetroInteractionContext
{
    public readonly RetroInteractor Interactor;
    public readonly GameObject Actor;
    public readonly Transform ActorTransform;
    public readonly Camera ViewCamera;
    public readonly IRetroInteractable Target;
    public readonly Collider Collider;
    public readonly Vector3 Origin;
    public readonly Vector3 Direction;
    public readonly Vector3 Point;
    public readonly Vector3 Normal;
    public readonly float Distance;

    public RetroInteractionContext(
        RetroInteractor interactor,
        GameObject actor,
        Transform actorTransform,
        Camera viewCamera,
        IRetroInteractable target,
        Collider collider,
        Vector3 origin,
        Vector3 direction,
        Vector3 point,
        Vector3 normal,
        float distance)
    {
        Interactor = interactor;
        Actor = actor;
        ActorTransform = actorTransform;
        ViewCamera = viewCamera;
        Target = target;
        Collider = collider;
        Origin = origin;
        Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        Point = point;
        Normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : -Direction;
        Distance = Mathf.Max(0f, distance);
    }
}

public interface IRetroInteractable
{
    GameObject InteractionGameObject { get; }
    Transform InteractionTransform { get; }
    int InteractionPriority { get; }
    float InteractionMaxDistance { get; }

    bool CanInteract(in RetroInteractionContext context);
    string GetInteractionPrompt(in RetroInteractionContext context);
    void SetInteractionFocused(bool focused, in RetroInteractionContext context);
    void Interact(in RetroInteractionContext context);
}

public readonly struct RetroInteractionFocusEvent
{
    public readonly GameObject Actor;
    public readonly GameObject Target;
    public readonly Vector3 Point;
    public readonly bool Focused;

    public RetroInteractionFocusEvent(GameObject actor, GameObject target, Vector3 point, bool focused)
    {
        Actor = actor;
        Target = target;
        Point = point;
        Focused = focused;
    }
}

public readonly struct RetroInteractionEvent
{
    public readonly GameObject Actor;
    public readonly GameObject Target;
    public readonly string Prompt;
    public readonly Vector3 Point;

    public RetroInteractionEvent(GameObject actor, GameObject target, string prompt, Vector3 point)
    {
        Actor = actor;
        Target = target;
        Prompt = prompt;
        Point = point;
    }
}
