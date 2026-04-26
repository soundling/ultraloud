using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-80)]
[DisallowMultipleComponent]
public sealed class RetroNavMeshRebuildService : MonoBehaviour
{
    private static readonly List<NavMeshBuildSource> BuildSources = new();
    private static readonly List<NavMeshBuildMarkup> BuildMarkups = new();
    private static RetroNavMeshRebuildService shared;
    private static bool applicationQuitting;

    [Header("Build")]
    [SerializeField] private bool rebuildOnStart = true;
    [SerializeField] private int agentTypeId;
    [SerializeField] private LayerMask includedLayers = ~0;
    [SerializeField] private NavMeshCollectGeometry collectGeometry = NavMeshCollectGeometry.PhysicsColliders;
    [SerializeField] private Vector3 buildCenter = Vector3.zero;
    [SerializeField] private Vector3 buildSize = new(220f, 70f, 220f);

    [Header("Runtime Changes")]
    [SerializeField, Min(0f)] private float rebuildDelay = 0.15f;
    [SerializeField] private bool drawBuildBounds;

    private NavMeshData navMeshData;
    private NavMeshDataInstance navMeshDataInstance;
    private AsyncOperation rebuildOperation;
    private bool rebuildQueued;
    private bool rebuildAfterCurrent;
    private float queuedRebuildTime;
    private int activeAgentTypeId = int.MinValue;

    public static RetroNavMeshRebuildService Shared
    {
        get
        {
            if (shared != null)
            {
                return shared;
            }

            shared = UnityEngine.Object.FindAnyObjectByType<RetroNavMeshRebuildService>();
            return shared;
        }
    }

    public static void RequestSceneRebuild(Vector3 reasonPosition)
    {
        if (!Application.isPlaying || applicationQuitting)
        {
            return;
        }

        RetroNavMeshRebuildService service = GetOrCreateService();
        if (service != null)
        {
            service.RequestRebuild(reasonPosition);
        }
    }

    public static void RebuildSceneNow(Vector3 reasonPosition)
    {
        if (!Application.isPlaying || applicationQuitting)
        {
            return;
        }

        RetroNavMeshRebuildService service = GetOrCreateService();
        if (service != null)
        {
            service.RebuildNow(reasonPosition);
        }
    }

    public void SetBuildBounds(Bounds bounds)
    {
        transform.position = bounds.center;
        buildCenter = Vector3.zero;
        buildSize = bounds.size;
    }

    public void RequestRebuild(Vector3 reasonPosition)
    {
        queuedRebuildTime = Time.time + rebuildDelay;
        rebuildQueued = true;
    }

    public void RebuildNow(Vector3 reasonPosition)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (rebuildOperation != null && !rebuildOperation.isDone)
        {
            rebuildAfterCurrent = true;
            return;
        }

        Bounds bounds = BuildWorldBounds();
        BuildSources.Clear();
        BuildMarkups.Clear();
        NavMeshBuilder.CollectSources(bounds, includedLayers.value, collectGeometry, 0, BuildMarkups, BuildSources);

        EnsureNavMeshData();
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(agentTypeId);
        rebuildOperation = NavMeshBuilder.UpdateNavMeshDataAsync(navMeshData, settings, BuildSources, bounds);
        rebuildQueued = false;
    }

    private static RetroNavMeshRebuildService GetOrCreateService()
    {
        RetroNavMeshRebuildService service = Shared;
        if (service != null)
        {
            return service;
        }

        if (!Application.isPlaying || applicationQuitting)
        {
            return null;
        }

        GameObject serviceObject = new("Retro NavMesh Rebuild Service");
        shared = serviceObject.AddComponent<RetroNavMeshRebuildService>();
        return shared;
    }

    private void Awake()
    {
        applicationQuitting = false;

        if (shared != null && shared != this)
        {
            enabled = false;
            return;
        }

        shared = this;
    }

    private void Start()
    {
        if (rebuildOnStart)
        {
            RequestRebuild(transform.position);
        }
    }

    private void Update()
    {
        if (rebuildOperation != null && rebuildOperation.isDone)
        {
            rebuildOperation = null;
            if (rebuildAfterCurrent)
            {
                rebuildAfterCurrent = false;
                RequestRebuild(transform.position);
            }
        }

        if (!rebuildQueued || rebuildOperation != null || Time.time < queuedRebuildTime)
        {
            return;
        }

        RebuildNow(transform.position);
    }

    private void OnDisable()
    {
        if (shared == this)
        {
            shared = null;
        }

        if (navMeshDataInstance.valid)
        {
            navMeshDataInstance.Remove();
        }

        navMeshData = null;
        activeAgentTypeId = int.MinValue;
    }

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
    }

    private void OnValidate()
    {
        buildSize.x = Mathf.Max(1f, buildSize.x);
        buildSize.y = Mathf.Max(1f, buildSize.y);
        buildSize.z = Mathf.Max(1f, buildSize.z);
        rebuildDelay = Mathf.Max(0f, rebuildDelay);
    }

    private void EnsureNavMeshData()
    {
        if (navMeshData != null && activeAgentTypeId == agentTypeId && navMeshDataInstance.valid)
        {
            return;
        }

        if (navMeshDataInstance.valid)
        {
            navMeshDataInstance.Remove();
        }

        navMeshData = new NavMeshData(agentTypeId)
        {
            name = "Runtime Retro NavMesh"
        };
        navMeshDataInstance = NavMesh.AddNavMeshData(navMeshData, Vector3.zero, Quaternion.identity);
        activeAgentTypeId = agentTypeId;
    }

    private Bounds BuildWorldBounds()
    {
        Vector3 size = new(Mathf.Max(1f, buildSize.x), Mathf.Max(1f, buildSize.y), Mathf.Max(1f, buildSize.z));
        return new Bounds(transform.position + buildCenter, size);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawBuildBounds)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.28f);
        Bounds bounds = BuildWorldBounds();
        Gizmos.DrawCube(bounds.center, bounds.size);
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.95f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
