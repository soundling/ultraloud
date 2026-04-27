using UnityEngine;

[DisallowMultipleComponent]
public sealed class RetroCursedParkInteractableAnchor : MonoBehaviour, IRetroInteractable
{
    [SerializeField] private bool interactionEnabled = true;
    [SerializeField] private string interactionName = "cursed machine";
    [SerializeField] private string interactionVerb = "Inspect";
    [SerializeField, Min(0.1f)] private float interactionMaxDistance = 3.4f;
    [SerializeField] private int interactionPriority = 20;
    [SerializeField] private string responseMessage = "It rattles like it noticed you.";
    [SerializeField, Min(0.1f)] private float responseDuration = 1.35f;

    public GameObject InteractionGameObject => gameObject;
    public Transform InteractionTransform => transform;
    public int InteractionPriority => interactionPriority;
    public float InteractionMaxDistance => interactionMaxDistance;

    public void Configure(string displayName, string verb, string response, float maxDistance, int priority)
    {
        interactionName = string.IsNullOrWhiteSpace(displayName) ? interactionName : displayName;
        interactionVerb = string.IsNullOrWhiteSpace(verb) ? interactionVerb : verb;
        responseMessage = string.IsNullOrWhiteSpace(response) ? responseMessage : response;
        interactionMaxDistance = Mathf.Max(0.1f, maxDistance);
        interactionPriority = priority;
    }

    public bool CanInteract(in RetroInteractionContext context)
    {
        return isActiveAndEnabled && interactionEnabled && context.Distance <= interactionMaxDistance + 0.001f;
    }

    public string GetInteractionPrompt(in RetroInteractionContext context)
    {
        if (string.IsNullOrWhiteSpace(interactionName))
        {
            return interactionVerb;
        }

        return string.IsNullOrWhiteSpace(interactionVerb)
            ? interactionName
            : $"{interactionVerb} {interactionName}";
    }

    public void SetInteractionFocused(bool focused, in RetroInteractionContext context)
    {
    }

    public void Interact(in RetroInteractionContext context)
    {
        if (!CanInteract(context))
        {
            return;
        }

        context.Interactor?.ShowStatusMessage(responseMessage, responseDuration);
    }
}
