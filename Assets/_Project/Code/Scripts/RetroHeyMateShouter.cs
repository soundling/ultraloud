using UnityEngine;

[DisallowMultipleComponent]
public sealed class RetroHeyMateShouter : MonoBehaviour
{
    [SerializeField] private RetroAudioCue shoutCue;
    [SerializeField] private RetroDamageable damageable;
    [SerializeField] private bool shoutOnEnable = true;
    [SerializeField] private bool muteWhenDead = true;
    [SerializeField] private bool requirePlayerNearby;
    [SerializeField, Min(0f)] private float playerDetectionRadius = 44f;
    [SerializeField] private Vector2 firstShoutDelayRange = new(0.25f, 0.95f);
    [SerializeField] private Vector2 shoutIntervalRange = new(1.8f, 3.4f);
    [SerializeField, Range(0f, 1f)] private float shoutChance = 1f;
    [SerializeField, Min(0f)] private float sourceHeight = 7.05f;

    private Transform cachedPlayer;
    private float nextPlayerSearchTime;
    private float nextShoutTime;

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
    }

    private void OnEnable()
    {
        AutoAssignReferences();
        nextPlayerSearchTime = 0f;
        ScheduleNextShout(shoutOnEnable ? firstShoutDelayRange : shoutIntervalRange);
    }

    private void OnValidate()
    {
        playerDetectionRadius = Mathf.Max(0f, playerDetectionRadius);
        sourceHeight = Mathf.Max(0f, sourceHeight);
        firstShoutDelayRange.x = Mathf.Max(0f, firstShoutDelayRange.x);
        firstShoutDelayRange.y = Mathf.Max(firstShoutDelayRange.x, firstShoutDelayRange.y);
        shoutIntervalRange.x = Mathf.Max(0.05f, shoutIntervalRange.x);
        shoutIntervalRange.y = Mathf.Max(shoutIntervalRange.x, shoutIntervalRange.y);
        AutoAssignReferences();
    }

    private void Update()
    {
        if (Time.time < nextShoutTime)
        {
            return;
        }

        if (Random.value <= shoutChance && CanShout())
        {
            ShoutNow();
        }

        ScheduleNextShout(shoutIntervalRange);
    }

    public void ShoutNow()
    {
        if (shoutCue == null)
        {
            return;
        }

        RetroGameContext.Audio.PlayCue(shoutCue, transform.position + Vector3.up * sourceHeight);
    }

    private void AutoAssignReferences()
    {
        if (damageable == null)
        {
            damageable = GetComponent<RetroDamageable>();
        }
    }

    private bool CanShout()
    {
        if (muteWhenDead && damageable != null && damageable.IsDead)
        {
            return false;
        }

        if (!requirePlayerNearby)
        {
            return true;
        }

        Transform player = ResolvePlayer();
        if (player == null)
        {
            return false;
        }

        return Vector3.SqrMagnitude(player.position - transform.position) <= playerDetectionRadius * playerDetectionRadius;
    }

    private Transform ResolvePlayer()
    {
        if (cachedPlayer != null)
        {
            return cachedPlayer;
        }

        if (Time.time < nextPlayerSearchTime)
        {
            return null;
        }

        nextPlayerSearchTime = Time.time + 0.7f;

        RetroFpsController fpsController = FindAnyObjectByType<RetroFpsController>();
        if (fpsController != null)
        {
            cachedPlayer = fpsController.transform;
            return cachedPlayer;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            cachedPlayer = taggedPlayer.transform;
        }

        return cachedPlayer;
    }

    private void ScheduleNextShout(Vector2 range)
    {
        nextShoutTime = Time.time + Random.Range(range.x, range.y);
    }
}
