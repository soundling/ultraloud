using UnityEngine;

[DisallowMultipleComponent]
public sealed class RetroResourcePickup : MonoBehaviour
{
    [SerializeField] private RetroResourceDefinition resource;
    [SerializeField, Min(1)] private int amount = 1;
    [SerializeField] private bool autoPickup = true;
    [SerializeField] private bool destroyWhenCollected = true;
    [SerializeField] private string pickupMessage;
    [SerializeField, Min(0.1f)] private float messageDuration = 1.2f;

    public RetroResourceDefinition Resource => resource;
    public int Amount => amount;

    private void Reset()
    {
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        amount = Mathf.Max(1, amount);
        messageDuration = Mathf.Max(0.1f, messageDuration);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!autoPickup)
        {
            return;
        }

        TryCollect(other.GetComponentInParent<RetroInventory>(), other.GetComponentInParent<RetroInteractor>());
    }

    public bool TryCollect(RetroInventory inventory, RetroInteractor interactor = null)
    {
        if (resource == null || inventory == null)
        {
            return false;
        }

        int accepted = inventory.Add(resource, amount);
        if (accepted <= 0)
        {
            interactor?.ShowStatusMessage($"{resource.DisplayName} is full", messageDuration);
            return false;
        }

        ShowCollectedMessage(interactor, accepted);
        if (destroyWhenCollected)
        {
            Destroy(gameObject);
        }

        return true;
    }

    private void ShowCollectedMessage(RetroInteractor interactor, int accepted)
    {
        if (interactor == null)
        {
            return;
        }

        string message = string.IsNullOrWhiteSpace(pickupMessage)
            ? $"+{accepted} {resource.DisplayName}"
            : pickupMessage;
        interactor.ShowStatusMessage(message, messageDuration);
    }
}
