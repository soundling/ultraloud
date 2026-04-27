using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshObstacle))]
public sealed class RetroNavMeshDynamicObstacle : MonoBehaviour
{
    [Header("Obstacle")]
    [SerializeField] private NavMeshObstacle navMeshObstacle;
    [SerializeField] private NavMeshObstacleShape shape = NavMeshObstacleShape.Box;
    [SerializeField] private Vector3 center = new(0f, 0.5f, 0f);
    [SerializeField] private Vector3 size = Vector3.one;
    [SerializeField, Min(0.01f)] private float radius = 0.5f;
    [SerializeField, Min(0.05f)] private float height = 2f;

    [Header("Carving")]
    [SerializeField] private bool carve = true;
    [SerializeField] private bool carveOnlyStationary = true;
    [SerializeField, Min(0f)] private float carvingMoveThreshold = 0.1f;
    [SerializeField, Min(0f)] private float carvingTimeToStationary = 0.2f;

    [Header("Rebuild Requests")]
    [SerializeField] private bool requestRebuildOnLifecycle = true;
    [SerializeField] private bool requestRebuildWhenMoved = true;
    [SerializeField, Min(0f)] private float rebuildMoveThreshold = 0.35f;
    [SerializeField, Min(0f)] private float rebuildRotationThreshold = 3f;

    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 lastScale;
    private static bool applicationQuitting;

    public void ConfigureBox(Vector3 obstacleSize, Vector3 obstacleCenter, bool requestRebuild = true)
    {
        shape = NavMeshObstacleShape.Box;
        size = SanitizeVector(obstacleSize, 0.01f);
        center = obstacleCenter;
        EnsureObstacle();
        ApplyToObstacle();
        CacheTransformState();

        if (requestRebuild)
        {
            NotifyNavigationChanged();
        }
    }

    public void ConfigureCapsule(float obstacleRadius, float obstacleHeight, Vector3 obstacleCenter, bool requestRebuild = true)
    {
        shape = NavMeshObstacleShape.Capsule;
        radius = Mathf.Max(0.01f, obstacleRadius);
        height = Mathf.Max(radius * 2f, obstacleHeight);
        center = obstacleCenter;
        EnsureObstacle();
        ApplyToObstacle();
        CacheTransformState();

        if (requestRebuild)
        {
            NotifyNavigationChanged();
        }
    }

    public void NotifyNavigationChanged()
    {
        if (!Application.isPlaying || applicationQuitting)
        {
            return;
        }

        RetroNavMeshRebuildService.RequestSceneRebuild(transform.position);
    }

    private void Reset()
    {
        EnsureObstacle();
        ApplyToObstacle();
        CacheTransformState();
    }

    private void Awake()
    {
        applicationQuitting = false;
        EnsureObstacle();
        ApplyToObstacle();
        CacheTransformState();
    }

    private void OnEnable()
    {
        EnsureObstacle();
        ApplyToObstacle();
        CacheTransformState();

        if (requestRebuildOnLifecycle)
        {
            NotifyNavigationChanged();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || !requestRebuildWhenMoved)
        {
            return;
        }

        if (!HasTransformChangedEnough())
        {
            return;
        }

        CacheTransformState();
        NotifyNavigationChanged();
    }

    private void OnDisable()
    {
        if (requestRebuildOnLifecycle)
        {
            NotifyNavigationChanged();
        }
    }

    private void OnDestroy()
    {
        if (requestRebuildOnLifecycle)
        {
            NotifyNavigationChanged();
        }
    }

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
    }

    private void OnValidate()
    {
        size = SanitizeVector(size, 0.01f);
        radius = Mathf.Max(0.01f, radius);
        height = Mathf.Max(radius * 2f, height);
        carvingMoveThreshold = Mathf.Max(0f, carvingMoveThreshold);
        carvingTimeToStationary = Mathf.Max(0f, carvingTimeToStationary);
        rebuildMoveThreshold = Mathf.Max(0f, rebuildMoveThreshold);
        rebuildRotationThreshold = Mathf.Max(0f, rebuildRotationThreshold);

        if (navMeshObstacle == null)
        {
            navMeshObstacle = GetComponent<NavMeshObstacle>();
        }

        ApplyToObstacle();
        CacheTransformState();
    }

    private void EnsureObstacle()
    {
        if (navMeshObstacle == null)
        {
            navMeshObstacle = GetComponent<NavMeshObstacle>();
        }

        if (navMeshObstacle == null)
        {
            navMeshObstacle = gameObject.AddComponent<NavMeshObstacle>();
        }
    }

    private void ApplyToObstacle()
    {
        if (navMeshObstacle == null)
        {
            return;
        }

        navMeshObstacle.shape = shape;
        navMeshObstacle.center = center;
        if (shape == NavMeshObstacleShape.Box)
        {
            navMeshObstacle.size = SanitizeVector(size, 0.01f);
        }
        else
        {
            navMeshObstacle.radius = Mathf.Max(0.01f, radius);
            navMeshObstacle.height = Mathf.Max(radius * 2f, height);
        }

        navMeshObstacle.carving = carve;
        navMeshObstacle.carveOnlyStationary = carveOnlyStationary;
        navMeshObstacle.carvingMoveThreshold = carvingMoveThreshold;
        navMeshObstacle.carvingTimeToStationary = carvingTimeToStationary;
    }

    private bool HasTransformChangedEnough()
    {
        float moveThresholdSqr = rebuildMoveThreshold * rebuildMoveThreshold;
        if ((transform.position - lastPosition).sqrMagnitude > moveThresholdSqr)
        {
            return true;
        }

        if (Quaternion.Angle(transform.rotation, lastRotation) > rebuildRotationThreshold)
        {
            return true;
        }

        return (transform.lossyScale - lastScale).sqrMagnitude > 0.0001f;
    }

    private void CacheTransformState()
    {
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastScale = transform.lossyScale;
    }

    private static Vector3 SanitizeVector(Vector3 value, float minimum)
    {
        value.x = Mathf.Max(minimum, value.x);
        value.y = Mathf.Max(minimum, value.y);
        value.z = Mathf.Max(minimum, value.z);
        return value;
    }
}
