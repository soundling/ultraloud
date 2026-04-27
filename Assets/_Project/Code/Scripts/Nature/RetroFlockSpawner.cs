using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RetroFlockSpawner : MonoBehaviour
{
    private const string GeneratedRootName = "__FlockRuntime";

    [Header("Prefab")]
    [SerializeField] private GameObject agentPrefab;
    [SerializeField, Min(0)] private int count = 24;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool clearOnDisable = true;
    [SerializeField] private bool addFlockAgentIfMissing = true;

    [Header("Group")]
    [SerializeField] private string groupId = "Birds";
    [SerializeField] private Transform homeAnchor;

    [Header("Spawn Volume")]
    [SerializeField] private bool useSphereVolume = true;
    [SerializeField, Min(0.1f)] private float spawnRadius = 18f;
    [SerializeField] private Vector3 spawnExtents = new(22f, 7f, 22f);
    [SerializeField] private Vector2 speedRange = new(3.5f, 7f);
    [SerializeField] private Vector2 scaleRange = new(0.85f, 1.18f);
    [SerializeField] private int seed = 91731;

    private readonly List<GameObject> spawnedObjects = new();
    private Transform generatedRoot;

    public GameObject AgentPrefab
    {
        get => agentPrefab;
        set => agentPrefab = value;
    }

    private void OnValidate()
    {
        count = Mathf.Max(0, count);
        spawnRadius = Mathf.Max(0.1f, spawnRadius);
        spawnExtents.x = Mathf.Max(0.1f, spawnExtents.x);
        spawnExtents.y = Mathf.Max(0.1f, spawnExtents.y);
        spawnExtents.z = Mathf.Max(0.1f, spawnExtents.z);
        if (speedRange.y < speedRange.x)
        {
            speedRange.y = speedRange.x;
        }

        if (scaleRange.y < scaleRange.x)
        {
            scaleRange.y = scaleRange.x;
        }
    }

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnNow();
        }
    }

    private void OnDisable()
    {
        if (clearOnDisable)
        {
            ClearSpawned();
        }
    }

    [ContextMenu("Spawn Flock Now")]
    public void SpawnNow()
    {
        if (agentPrefab == null)
        {
            Debug.LogWarning("RetroFlockSpawner needs an agent prefab before it can spawn.", this);
            return;
        }

        ClearSpawned();
        EnsureGeneratedRoot();

        System.Random random = new(seed);
        Vector3 home = homeAnchor != null ? homeAnchor.position : transform.position;
        for (int i = 0; i < count; i++)
        {
            Vector3 position = home + SampleOffset(random);
            Quaternion rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            GameObject instance = Instantiate(agentPrefab, position, rotation, generatedRoot);
            instance.name = $"{agentPrefab.name}_{i:00}";
            float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)random.NextDouble());
            instance.transform.localScale = Vector3.one * scale;
            ConfigureAgent(instance, random, home);
            spawnedObjects.Add(instance);
        }
    }

    [ContextMenu("Clear Spawned Flock")]
    public void ClearSpawned()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            DestroyUnityObject(spawnedObjects[i]);
        }

        spawnedObjects.Clear();

        Transform existingRoot = transform.Find(GeneratedRootName);
        if (existingRoot != null)
        {
            DestroyUnityObject(existingRoot.gameObject);
        }

        generatedRoot = null;
    }

    private void EnsureGeneratedRoot()
    {
        if (generatedRoot != null)
        {
            return;
        }

        Transform existingRoot = transform.Find(GeneratedRootName);
        if (existingRoot != null)
        {
            generatedRoot = existingRoot;
            return;
        }

        GameObject root = new(GeneratedRootName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        generatedRoot = root.transform;
    }

    private Vector3 SampleOffset(System.Random random)
    {
        if (!useSphereVolume)
        {
            return new Vector3(
                Mathf.Lerp(-spawnExtents.x, spawnExtents.x, (float)random.NextDouble()),
                Mathf.Lerp(-spawnExtents.y, spawnExtents.y, (float)random.NextDouble()),
                Mathf.Lerp(-spawnExtents.z, spawnExtents.z, (float)random.NextDouble()));
        }

        Vector3 direction = RandomUnitVector(random);
        float radius = Mathf.Pow((float)random.NextDouble(), 1f / 3f) * spawnRadius;
        return new Vector3(direction.x * radius, direction.y * radius * 0.35f, direction.z * radius);
    }

    private void ConfigureAgent(GameObject instance, System.Random random, Vector3 home)
    {
        RetroFlockAgent agent = instance.GetComponent<RetroFlockAgent>();
        if (agent == null && addFlockAgentIfMissing)
        {
            agent = instance.AddComponent<RetroFlockAgent>();
        }

        if (agent == null)
        {
            return;
        }

        agent.GroupId = string.IsNullOrWhiteSpace(groupId) ? name : groupId;
        agent.SetHome(home);
        Vector3 direction = RandomUnitVector(random);
        direction.y *= 0.25f;
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = transform.forward;
        }

        float speed = Mathf.Lerp(speedRange.x, speedRange.y, (float)random.NextDouble());
        agent.SetVelocity(direction.normalized * Mathf.Max(0.01f, speed));
        agent.RandomizePhase(seed * 0.13f + iHash(instance.name) * 0.01f);
    }

    private static Vector3 RandomUnitVector(System.Random random)
    {
        double z = random.NextDouble() * 2.0 - 1.0;
        double angle = random.NextDouble() * Mathf.PI * 2.0;
        double radius = System.Math.Sqrt(System.Math.Max(0.0, 1.0 - z * z));
        return new Vector3((float)(radius * System.Math.Cos(angle)), (float)z, (float)(radius * System.Math.Sin(angle)));
    }

    private static float iHash(string text)
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < text.Length; i++)
            {
                hash = hash * 31 + text[i];
            }

            return Mathf.Abs(hash % 100000);
        }
    }

    private static void DestroyUnityObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
