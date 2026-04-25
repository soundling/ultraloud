using UnityEngine;
using UnityEngine.Events;

public abstract class RetroInteractableBehaviour : MonoBehaviour, IRetroInteractable
{
    [Header("Interaction")]
    [SerializeField] private bool interactionEnabled = true;
    [SerializeField] private string interactionName;
    [SerializeField] private string interactionVerb;
    [SerializeField, Min(0.1f)] private float interactionMaxDistance = 3.2f;
    [SerializeField] private int interactionPriority;

    [Header("Feedback")]
    [SerializeField] private RetroAudioCue interactionCue;
    [SerializeField] private string interactionMessage;
    [SerializeField, Min(0.1f)] private float interactionMessageDuration = 1.6f;

    [Header("Events")]
    [SerializeField] private UnityEvent onFocused;
    [SerializeField] private UnityEvent onUnfocused;
    [SerializeField] private UnityEvent onInteracted;

    public GameObject InteractionGameObject => gameObject;
    public Transform InteractionTransform => transform;
    public int InteractionPriority => interactionPriority;
    public float InteractionMaxDistance => interactionMaxDistance;

    protected virtual string DefaultInteractionVerb => "Use";
    protected virtual string DefaultInteractionName => gameObject.name;

    public virtual bool CanInteract(in RetroInteractionContext context)
    {
        return isActiveAndEnabled
            && interactionEnabled
            && context.Distance <= interactionMaxDistance + 0.001f;
    }

    public virtual string GetInteractionPrompt(in RetroInteractionContext context)
    {
        string resolvedVerb = string.IsNullOrWhiteSpace(interactionVerb) ? DefaultInteractionVerb : interactionVerb;
        string resolvedName = string.IsNullOrWhiteSpace(interactionName) ? DefaultInteractionName : interactionName;
        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            return resolvedVerb;
        }

        if (string.IsNullOrWhiteSpace(resolvedVerb))
        {
            return resolvedName;
        }

        return $"{resolvedVerb} {resolvedName}";
    }

    public void SetInteractionFocused(bool focused, in RetroInteractionContext context)
    {
        if (focused)
        {
            onFocused?.Invoke();
        }
        else
        {
            onUnfocused?.Invoke();
        }

        OnInteractionFocusChanged(focused, context);
    }

    public void Interact(in RetroInteractionContext context)
    {
        if (!CanInteract(context))
        {
            return;
        }

        onInteracted?.Invoke();
        PlayCue(interactionCue);
        if (!string.IsNullOrWhiteSpace(interactionMessage))
        {
            context.Interactor?.ShowStatusMessage(interactionMessage, interactionMessageDuration);
        }

        InteractInternal(context);
    }

    protected virtual void OnInteractionFocusChanged(bool focused, in RetroInteractionContext context)
    {
    }

    protected abstract void InteractInternal(in RetroInteractionContext context);

    protected void PlayCue(RetroAudioCue cue)
    {
        if (cue != null)
        {
            RetroGameContext.Audio.PlayCue(cue, transform.position);
        }
    }
}
