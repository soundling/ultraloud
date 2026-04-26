using UnityEngine;

[DisallowMultipleComponent]
public sealed class RetroResourceGatherable : RetroInteractableBehaviour
{
    [Header("Resource")]
    [SerializeField] private RetroResourceDefinition resource;
    [SerializeField, Min(1)] private int amountPerGather = 1;
    [SerializeField, Min(1)] private int gatherUses = 1;
    [SerializeField] private bool destroyWhenDepleted = true;
    [SerializeField] private string gatheredMessage;
    [SerializeField] private string depletedMessage = "Nothing left to gather";
    [SerializeField, Min(0.1f)] private float messageDuration = 1.2f;

    private int remainingUses;

    protected override string DefaultInteractionVerb => "Gather";
    protected override string DefaultInteractionName => resource != null ? resource.DisplayName : gameObject.name;

    private void Awake()
    {
        remainingUses = Mathf.Max(1, gatherUses);
    }

    private void OnEnable()
    {
        if (remainingUses <= 0)
        {
            remainingUses = Mathf.Max(1, gatherUses);
        }
    }

    private void OnValidate()
    {
        amountPerGather = Mathf.Max(1, amountPerGather);
        gatherUses = Mathf.Max(1, gatherUses);
        messageDuration = Mathf.Max(0.1f, messageDuration);
    }

    public override bool CanInteract(in RetroInteractionContext context)
    {
        return base.CanInteract(context) && resource != null && remainingUses > 0;
    }

    protected override void InteractInternal(in RetroInteractionContext context)
    {
        RetroInventory inventory = context.Actor != null ? context.Actor.GetComponentInParent<RetroInventory>() : null;
        if (inventory == null)
        {
            context.Interactor?.ShowStatusMessage("No inventory", messageDuration);
            return;
        }

        int accepted = inventory.Add(resource, amountPerGather);
        if (accepted <= 0)
        {
            context.Interactor?.ShowStatusMessage($"{resource.DisplayName} is full", messageDuration);
            return;
        }

        remainingUses--;
        bool depleted = remainingUses <= 0;
        string message = string.IsNullOrWhiteSpace(gatheredMessage)
            ? $"+{accepted} {resource.DisplayName}"
            : gatheredMessage;
        if (depleted && !string.IsNullOrWhiteSpace(depletedMessage))
        {
            message = $"{message} - {depletedMessage}";
        }

        context.Interactor?.ShowStatusMessage(message, messageDuration);

        if (!depleted)
        {
            return;
        }

        if (destroyWhenDepleted)
        {
            Destroy(gameObject);
        }
    }
}
