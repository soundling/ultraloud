using System;
using System.Collections.Generic;
using UnityEngine;

public enum RetroWeaponReloadStage
{
    Started,
    Completed
}

public readonly struct RetroDamageEvent
{
    public readonly GameObject Source;
    public readonly RetroDamageable Target;
    public readonly float Amount;
    public readonly Vector3 Point;
    public readonly Vector3 Normal;
    public readonly bool Lethal;

    public RetroDamageEvent(GameObject source, RetroDamageable target, float amount, Vector3 point, Vector3 normal, bool lethal)
    {
        Source = source;
        Target = target;
        Amount = amount;
        Point = point;
        Normal = normal;
        Lethal = lethal;
    }
}

public readonly struct RetroDeathEvent
{
    public readonly GameObject Source;
    public readonly RetroDamageable Target;
    public readonly Vector3 Position;

    public RetroDeathEvent(GameObject source, RetroDamageable target, Vector3 position)
    {
        Source = source;
        Target = target;
        Position = position;
    }
}

public readonly struct RetroWeaponFiredEvent
{
    public readonly GameObject Owner;
    public readonly RetroWeaponDefinition Definition;
    public readonly string WeaponName;
    public readonly Vector3 Position;
    public readonly Vector3 Direction;

    public RetroWeaponFiredEvent(GameObject owner, RetroWeaponDefinition definition, string weaponName, Vector3 position, Vector3 direction)
    {
        Owner = owner;
        Definition = definition;
        WeaponName = weaponName;
        Position = position;
        Direction = direction;
    }
}

public readonly struct RetroWeaponDryFiredEvent
{
    public readonly GameObject Owner;
    public readonly RetroWeaponDefinition Definition;
    public readonly string WeaponName;

    public RetroWeaponDryFiredEvent(GameObject owner, RetroWeaponDefinition definition, string weaponName)
    {
        Owner = owner;
        Definition = definition;
        WeaponName = weaponName;
    }
}

public readonly struct RetroWeaponReloadEvent
{
    public readonly GameObject Owner;
    public readonly RetroWeaponDefinition Definition;
    public readonly string WeaponName;
    public readonly RetroWeaponReloadStage Stage;

    public RetroWeaponReloadEvent(GameObject owner, RetroWeaponDefinition definition, string weaponName, RetroWeaponReloadStage stage)
    {
        Owner = owner;
        Definition = definition;
        WeaponName = weaponName;
        Stage = stage;
    }
}

public readonly struct RetroWeaponSelectedEvent
{
    public readonly GameObject Owner;
    public readonly RetroWeaponDefinition Definition;
    public readonly string WeaponName;
    public readonly int SlotIndex;

    public RetroWeaponSelectedEvent(GameObject owner, RetroWeaponDefinition definition, string weaponName, int slotIndex)
    {
        Owner = owner;
        Definition = definition;
        WeaponName = weaponName;
        SlotIndex = slotIndex;
    }
}

public readonly struct RetroExplosionEvent
{
    public readonly GameObject Source;
    public readonly Vector3 Position;
    public readonly float Radius;
    public readonly float Damage;
    public readonly Color Color;

    public RetroExplosionEvent(GameObject source, Vector3 position, float radius, float damage, Color color)
    {
        Source = source;
        Position = position;
        Radius = radius;
        Damage = damage;
        Color = color;
    }
}

public readonly struct RetroNpcAlertEvent
{
    public readonly GameObject Npc;
    public readonly GameObject Target;
    public readonly Vector3 LastKnownPosition;
    public readonly float Urgency;

    public RetroNpcAlertEvent(GameObject npc, GameObject target, Vector3 lastKnownPosition, float urgency)
    {
        Npc = npc;
        Target = target;
        LastKnownPosition = lastKnownPosition;
        Urgency = urgency;
    }
}

[DisallowMultipleComponent]
public sealed class RetroGameplayEventBus : MonoBehaviour
{
    private readonly Dictionary<Type, Delegate> subscriptions = new Dictionary<Type, Delegate>(64);

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
        {
            return null;
        }

        Type eventType = typeof(TEvent);
        subscriptions.TryGetValue(eventType, out Delegate existing);
        subscriptions[eventType] = Delegate.Combine(existing, handler);
        return new Subscription<TEvent>(this, handler);
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler)
    {
        if (handler == null)
        {
            return;
        }

        Type eventType = typeof(TEvent);
        if (!subscriptions.TryGetValue(eventType, out Delegate existing))
        {
            return;
        }

        Delegate next = Delegate.Remove(existing, handler);
        if (next == null)
        {
            subscriptions.Remove(eventType);
        }
        else
        {
            subscriptions[eventType] = next;
        }
    }

    public void Publish<TEvent>(TEvent evt)
    {
        if (subscriptions.TryGetValue(typeof(TEvent), out Delegate existing) && existing is Action<TEvent> action)
        {
            action.Invoke(evt);
        }
    }

    public void Clear()
    {
        subscriptions.Clear();
    }

    private void OnDestroy()
    {
        Clear();
    }

    private sealed class Subscription<TEvent> : IDisposable
    {
        private RetroGameplayEventBus bus;
        private Action<TEvent> handler;

        public Subscription(RetroGameplayEventBus bus, Action<TEvent> handler)
        {
            this.bus = bus;
            this.handler = handler;
        }

        public void Dispose()
        {
            if (bus != null && handler != null)
            {
                bus.Unsubscribe(handler);
            }

            bus = null;
            handler = null;
        }
    }
}
