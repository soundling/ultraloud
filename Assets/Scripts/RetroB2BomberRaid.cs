using System;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public sealed class RetroB2BomberRaid : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private RetroB2BomberActor bomberPrefab;

    [Header("Targeting")]
    [SerializeField] private Transform target;
    [SerializeField] private bool usePlayerTarget = true;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Schedule")]
    [SerializeField] private bool automaticRaids = true;
    [SerializeField] private Vector2 firstPassDelayRange = new Vector2(8f, 18f);
    [SerializeField] private Vector2 passIntervalRange = new Vector2(28f, 55f);

    [Header("Pass")]
    [SerializeField, Min(20f)] private float passHeight = 68f;
    [SerializeField, Min(12f)] private float passLength = 170f;
    [SerializeField, Min(1f)] private float passSpeed = 18f;
    [SerializeField, Min(0f)] private float lateralJitter = 22f;

    [Header("Bombing")]
    [SerializeField] private Vector2Int bombsPerPassRange = new Vector2Int(3, 5);
    [SerializeField, Min(0f)] private float bombLineLength = 44f;
    [SerializeField, Min(0f)] private float targetScatterRadius = 10f;
    [SerializeField, Min(0f)] private float minimumPlayerMissDistance = 6f;
    [SerializeField] private bool avoidDirectPlayerCenter = true;

    private float nextRaidTime;

    private void OnEnable()
    {
        ScheduleNextRaid(firstPassDelayRange);
    }

    private void OnValidate()
    {
        passHeight = Mathf.Max(20f, passHeight);
        passLength = Mathf.Max(12f, passLength);
        passSpeed = Mathf.Max(1f, passSpeed);
        lateralJitter = Mathf.Max(0f, lateralJitter);
        bombLineLength = Mathf.Max(0f, bombLineLength);
        targetScatterRadius = Mathf.Max(0f, targetScatterRadius);
        minimumPlayerMissDistance = Mathf.Max(0f, minimumPlayerMissDistance);
        firstPassDelayRange = SanitizeRange(firstPassDelayRange, 0f, 240f);
        passIntervalRange = SanitizeRange(passIntervalRange, 1f, 300f);
        bombsPerPassRange.x = Mathf.Max(1, bombsPerPassRange.x);
        bombsPerPassRange.y = Mathf.Max(bombsPerPassRange.x, bombsPerPassRange.y);
    }

    private void Update()
    {
        if (!automaticRaids || bomberPrefab == null || Time.time < nextRaidTime)
        {
            return;
        }

        TriggerRaidNow();
        ScheduleNextRaid(passIntervalRange);
    }

    public void TriggerRaidNow()
    {
        if (bomberPrefab == null)
        {
            Debug.LogWarning("B2 bomber raid is missing a bomber prefab.", this);
            return;
        }

        Transform resolvedTarget = ResolveTarget();
        Vector3 targetPosition = resolvedTarget != null ? resolvedTarget.position : transform.position;
        SpawnPass(targetPosition);
    }

    private void SpawnPass(Vector3 targetPosition)
    {
        Vector3 passDirection = RandomHorizontalDirection();
        Vector3 passRight = new Vector3(-passDirection.z, 0f, passDirection.x);
        Vector3 passCenter = targetPosition + passRight * Random.Range(-lateralJitter, lateralJitter);
        Vector3 start = passCenter - passDirection * (passLength * 0.5f) + Vector3.up * passHeight;
        Vector3 end = passCenter + passDirection * (passLength * 0.5f) + Vector3.up * passHeight;
        float duration = Mathf.Max(0.1f, Vector3.Distance(start, end) / passSpeed);

        int bombCount = Random.Range(bombsPerPassRange.x, bombsPerPassRange.y + 1);
        Vector3[] impactPoints = new Vector3[bombCount];
        float[] dropTimes = new float[bombCount];
        for (int i = 0; i < bombCount; i++)
        {
            float normalized = bombCount == 1 ? 0.5f : i / (float)(bombCount - 1);
            float along = Mathf.Lerp(-bombLineLength * 0.5f, bombLineLength * 0.5f, normalized);
            Vector2 scatter = Random.insideUnitCircle * targetScatterRadius;
            Vector3 offset = passDirection * (along + scatter.y) + passRight * scatter.x;

            if (avoidDirectPlayerCenter)
            {
                offset = PushAwayFromTargetCenter(offset, passDirection, passRight);
            }

            Vector3 impactPoint = targetPosition + offset;
            impactPoints[i] = impactPoint;
            dropTimes[i] = Mathf.Clamp01(
                Mathf.InverseLerp(-passLength * 0.5f, passLength * 0.5f, Vector3.Dot(impactPoint - passCenter, passDirection)));
            dropTimes[i] = Mathf.Clamp(dropTimes[i], 0.12f, 0.88f);
        }

        Array.Sort(dropTimes, impactPoints);

        RetroB2BomberActor bomber = Instantiate(bomberPrefab, start, Quaternion.identity);
        bomber.name = "B2 Bomber Pass";
        bomber.Initialize(start, end, duration, impactPoints, dropTimes, groundMask, gameObject);
    }

    private Vector3 PushAwayFromTargetCenter(Vector3 offset, Vector3 passDirection, Vector3 passRight)
    {
        Vector3 flatOffset = Vector3.ProjectOnPlane(offset, Vector3.up);
        if (flatOffset.magnitude >= minimumPlayerMissDistance)
        {
            return offset;
        }

        Vector2 randomDirection = Random.insideUnitCircle;
        if (randomDirection.sqrMagnitude < 0.001f)
        {
            randomDirection = Vector2.right;
        }

        randomDirection.Normalize();
        Vector3 pushed = passRight * randomDirection.x + passDirection * randomDirection.y;
        return pushed.normalized * minimumPlayerMissDistance;
    }

    private Transform ResolveTarget()
    {
        if (target != null)
        {
            return target;
        }

        if (usePlayerTarget)
        {
            RetroFpsController player = FindAnyObjectByType<RetroFpsController>();
            if (player != null)
            {
                target = player.transform;
                return target;
            }
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            return mainCamera.transform;
        }

        return null;
    }

    private void ScheduleNextRaid(Vector2 range)
    {
        range = SanitizeRange(range, 0f, 300f);
        nextRaidTime = Time.time + Random.Range(range.x, range.y);
    }

    private static Vector3 RandomHorizontalDirection()
    {
        float radians = Random.Range(0f, Mathf.PI * 2f);
        return new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)).normalized;
    }

    private static Vector2 SanitizeRange(Vector2 range, float minimum, float fallbackMax)
    {
        range.x = Mathf.Max(minimum, range.x);
        range.y = Mathf.Max(range.x, range.y);
        if (range.y <= minimum)
        {
            range.y = Mathf.Max(range.x, fallbackMax);
        }

        return range;
    }
}
