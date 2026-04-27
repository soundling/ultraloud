using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct RetroInventoryStack
{
    [SerializeField] private RetroResourceDefinition resource;
    [SerializeField, Min(0)] private int amount;

    public RetroResourceDefinition Resource => resource;
    public int Amount => amount;

    public RetroInventoryStack(RetroResourceDefinition resource, int amount)
    {
        this.resource = resource;
        this.amount = Mathf.Max(0, amount);
    }

    public void SetAmount(int value)
    {
        amount = Mathf.Max(0, value);
    }
}

[DisallowMultipleComponent]
public sealed class RetroInventory : MonoBehaviour
{
    [SerializeField] private List<RetroInventoryStack> items = new();
    [SerializeField] private bool logChanges;

    public event Action<RetroInventory> Changed;

    public IReadOnlyList<RetroInventoryStack> Items => items;

    public int Add(RetroResourceDefinition resource, int amount)
    {
        if (resource == null || amount <= 0)
        {
            return 0;
        }

        int index = FindIndex(resource);
        int currentAmount = index >= 0 ? items[index].Amount : 0;
        int accepted = Mathf.Min(amount, Mathf.Max(0, resource.MaxAmount - currentAmount));
        if (accepted <= 0)
        {
            return 0;
        }

        if (index >= 0)
        {
            RetroInventoryStack stack = items[index];
            stack.SetAmount(currentAmount + accepted);
            items[index] = stack;
        }
        else
        {
            items.Add(new RetroInventoryStack(resource, accepted));
        }

        if (logChanges)
        {
            Debug.Log($"Picked up {accepted} {resource.DisplayName}. Total: {GetAmount(resource)}", this);
        }

        Changed?.Invoke(this);
        return accepted;
    }

    public bool Remove(RetroResourceDefinition resource, int amount)
    {
        if (resource == null || amount <= 0)
        {
            return false;
        }

        int index = FindIndex(resource);
        if (index < 0 || items[index].Amount < amount)
        {
            return false;
        }

        RetroInventoryStack stack = items[index];
        stack.SetAmount(stack.Amount - amount);
        if (stack.Amount <= 0)
        {
            items.RemoveAt(index);
        }
        else
        {
            items[index] = stack;
        }

        Changed?.Invoke(this);
        return true;
    }

    public int GetAmount(RetroResourceDefinition resource)
    {
        int index = FindIndex(resource);
        return index >= 0 ? items[index].Amount : 0;
    }

    public bool Has(RetroResourceDefinition resource, int amount)
    {
        return resource != null && amount >= 0 && GetAmount(resource) >= amount;
    }

    private int FindIndex(RetroResourceDefinition resource)
    {
        if (resource == null)
        {
            return -1;
        }

        string resourceId = resource.ResourceId;
        for (int i = 0; i < items.Count; i++)
        {
            RetroResourceDefinition itemResource = items[i].Resource;
            if (itemResource == resource || (itemResource != null && itemResource.ResourceId == resourceId))
            {
                return i;
            }
        }

        return -1;
    }

    private void OnValidate()
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            RetroInventoryStack stack = items[i];
            if (stack.Resource == null || stack.Amount <= 0)
            {
                items.RemoveAt(i);
                continue;
            }

            stack.SetAmount(Mathf.Min(stack.Amount, stack.Resource.MaxAmount));
            items[i] = stack;
        }
    }
}
