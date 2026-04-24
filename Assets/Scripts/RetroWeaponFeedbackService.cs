using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RetroWeaponFeedbackService : MonoBehaviour
{
    [Header("Fallback Cues")]
    [SerializeField] private RetroAudioCue weaponFireCue;
    [SerializeField] private RetroAudioCue weaponDryFireCue;
    [SerializeField] private RetroAudioCue reloadStartCue;
    [SerializeField] private RetroAudioCue reloadCompleteCue;
    [SerializeField] private RetroAudioCue weaponSelectCue;
    [SerializeField] private RetroAudioCue explosionCue;
    [SerializeField] private RetroAudioCue damageCue;
    [SerializeField] private RetroAudioCue deathCue;

    [Header("Tuning")]
    [SerializeField] private bool playFallbackCues = true;
    [SerializeField, Min(0f)] private float damageCueMinInterval = 0.035f;
    [SerializeField, Min(0f)] private float weaponSelectCueMinInterval = 0.08f;

    private IDisposable weaponFiredSubscription;
    private IDisposable weaponDryFiredSubscription;
    private IDisposable weaponReloadSubscription;
    private IDisposable weaponSelectedSubscription;
    private IDisposable explosionSubscription;
    private IDisposable damageSubscription;
    private IDisposable deathSubscription;
    private float nextDamageCueTime;
    private float nextSelectCueTime;

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        Unsubscribe();
        RetroGameplayEventBus events = RetroGameContext.Events;
        weaponFiredSubscription = events.Subscribe<RetroWeaponFiredEvent>(HandleWeaponFired);
        weaponDryFiredSubscription = events.Subscribe<RetroWeaponDryFiredEvent>(HandleWeaponDryFired);
        weaponReloadSubscription = events.Subscribe<RetroWeaponReloadEvent>(HandleWeaponReload);
        weaponSelectedSubscription = events.Subscribe<RetroWeaponSelectedEvent>(HandleWeaponSelected);
        explosionSubscription = events.Subscribe<RetroExplosionEvent>(HandleExplosion);
        damageSubscription = events.Subscribe<RetroDamageEvent>(HandleDamage);
        deathSubscription = events.Subscribe<RetroDeathEvent>(HandleDeath);
    }

    private void Unsubscribe()
    {
        weaponFiredSubscription?.Dispose();
        weaponDryFiredSubscription?.Dispose();
        weaponReloadSubscription?.Dispose();
        weaponSelectedSubscription?.Dispose();
        explosionSubscription?.Dispose();
        damageSubscription?.Dispose();
        deathSubscription?.Dispose();

        weaponFiredSubscription = null;
        weaponDryFiredSubscription = null;
        weaponReloadSubscription = null;
        weaponSelectedSubscription = null;
        explosionSubscription = null;
        damageSubscription = null;
        deathSubscription = null;
    }

    private void HandleWeaponFired(RetroWeaponFiredEvent evt)
    {
        PlayCue(evt.Definition != null ? evt.Definition.fireCue : null, weaponFireCue, evt.Position);
    }

    private void HandleWeaponDryFired(RetroWeaponDryFiredEvent evt)
    {
        Vector3 position = evt.Owner != null ? evt.Owner.transform.position : transform.position;
        PlayCue(evt.Definition != null ? evt.Definition.dryFireCue : null, weaponDryFireCue, position);
    }

    private void HandleWeaponReload(RetroWeaponReloadEvent evt)
    {
        Vector3 position = evt.Owner != null ? evt.Owner.transform.position : transform.position;
        RetroAudioCue authoredCue = null;
        RetroAudioCue fallbackCue = null;

        if (evt.Stage == RetroWeaponReloadStage.Started)
        {
            authoredCue = evt.Definition != null ? evt.Definition.reloadStartCue : null;
            fallbackCue = reloadStartCue;
        }
        else if (evt.Stage == RetroWeaponReloadStage.Completed)
        {
            authoredCue = evt.Definition != null ? evt.Definition.reloadCompleteCue : null;
            fallbackCue = reloadCompleteCue;
        }

        PlayCue(authoredCue, fallbackCue, position);
    }

    private void HandleWeaponSelected(RetroWeaponSelectedEvent evt)
    {
        if (Time.time < nextSelectCueTime)
        {
            return;
        }

        nextSelectCueTime = Time.time + weaponSelectCueMinInterval;
        Vector3 position = evt.Owner != null ? evt.Owner.transform.position : transform.position;
        PlayCue(evt.Definition != null ? evt.Definition.selectCue : null, weaponSelectCue, position);
    }

    private void HandleExplosion(RetroExplosionEvent evt)
    {
        PlayCue(null, explosionCue, evt.Position);
    }

    private void HandleDamage(RetroDamageEvent evt)
    {
        if (evt.Lethal || Time.time < nextDamageCueTime)
        {
            return;
        }

        nextDamageCueTime = Time.time + damageCueMinInterval;
        PlayCue(null, damageCue, evt.Point);
    }

    private void HandleDeath(RetroDeathEvent evt)
    {
        PlayCue(null, deathCue, evt.Position);
    }

    private void PlayCue(RetroAudioCue authoredCue, RetroAudioCue fallbackCue, Vector3 position)
    {
        RetroAudioCue cue = authoredCue != null ? authoredCue : playFallbackCues ? fallbackCue : null;
        if (cue == null)
        {
            return;
        }

        RetroGameContext.Audio.PlayCue(cue, position);
    }
}
