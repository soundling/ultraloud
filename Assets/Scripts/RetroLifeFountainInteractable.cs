using UnityEngine;

public enum RetroLifeFountainHealMode
{
    RestoreToFull = 0,
    FixedAmount = 1,
    FractionOfMax = 2
}

public sealed class RetroLifeFountainInteractable : RetroInteractableBehaviour
{
    [Header("Life Fountain")]
    [SerializeField] private RetroLifeFountainHealMode healMode = RetroLifeFountainHealMode.RestoreToFull;
    [SerializeField, Min(0f)] private float healAmount = 100f;
    [SerializeField, Range(0f, 1f)] private float healFraction = 1f;
    [SerializeField, Min(0f)] private float useCooldown = 0.75f;
    [SerializeField] private RetroAudioCue healedCue;
    [SerializeField] private RetroAudioCue fullCue;
    [SerializeField] private string healedMessage = "Life restored.";
    [SerializeField] private string fullHealthMessage = "You are already full of life.";
    [SerializeField] private string missingHealthMessage = "The fountain finds no living body to heal.";

    private float nextUseTime = -999f;

    protected override string DefaultInteractionVerb => "Drink from";

    public override bool CanInteract(in RetroInteractionContext context)
    {
        return base.CanInteract(context) && Time.time >= nextUseTime;
    }

    protected override void InteractInternal(in RetroInteractionContext context)
    {
        nextUseTime = Time.time + useCooldown;
        RetroDamageable target = ResolveTarget(context);
        if (target == null)
        {
            context.Interactor?.ShowStatusMessage(missingHealthMessage, 1.5f);
            return;
        }

        if (target.CurrentHealth >= target.MaxHealth - 0.001f)
        {
            context.Interactor?.ShowStatusMessage(fullHealthMessage, 1.35f);
            PlayCue(fullCue);
            return;
        }

        float healed = target.Heal(ResolveHealAmount(target), gameObject);
        if (healed <= 0f)
        {
            context.Interactor?.ShowStatusMessage(fullHealthMessage, 1.35f);
            PlayCue(fullCue);
            return;
        }

        context.Interactor?.ShowStatusMessage(healedMessage, 1.5f);
        PlayCue(healedCue);
    }

    private RetroDamageable ResolveTarget(in RetroInteractionContext context)
    {
        if (context.Actor != null && context.Actor.TryGetComponent(out RetroDamageable actorDamageable))
        {
            return actorDamageable;
        }

        if (context.ActorTransform != null)
        {
            RetroDamageable parentDamageable = context.ActorTransform.GetComponentInParent<RetroDamageable>();
            if (parentDamageable != null)
            {
                return parentDamageable;
            }
        }

        return context.Interactor != null ? context.Interactor.GetComponentInParent<RetroDamageable>() : null;
    }

    private float ResolveHealAmount(RetroDamageable target)
    {
        return healMode switch
        {
            RetroLifeFountainHealMode.FixedAmount => healAmount,
            RetroLifeFountainHealMode.FractionOfMax => target.MaxHealth * healFraction,
            _ => target.MaxHealth
        };
    }
}
