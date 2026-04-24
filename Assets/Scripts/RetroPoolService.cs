using System;
using System.Collections.Generic;
using UnityEngine;

public interface IRetroPoolLifecycle
{
    void OnPoolRent(RetroPooledObject pooledObject);
    void OnPoolReturn(RetroPooledObject pooledObject);
    void OnPoolDestroy(RetroPooledObject pooledObject);
}

[Serializable]
public struct RetroPoolSettings
{
    [Min(0)] public int prewarmCount;
    [Min(0)] public int maxInactiveCount;
    public bool collectionChecks;
    public bool reparentOnReturn;

    public RetroPoolSettings(int prewarmCount, int maxInactiveCount, bool collectionChecks = true, bool reparentOnReturn = true)
    {
        this.prewarmCount = Mathf.Max(0, prewarmCount);
        this.maxInactiveCount = Mathf.Max(0, maxInactiveCount);
        this.collectionChecks = collectionChecks;
        this.reparentOnReturn = reparentOnReturn;
    }

    public static RetroPoolSettings Default => new RetroPoolSettings(0, 128);
    public int ResolvedMaxInactiveCount => maxInactiveCount <= 0 ? int.MaxValue : maxInactiveCount;
}

public readonly struct RetroPoolSnapshot
{
    public readonly string Key;
    public readonly Type ItemType;
    public readonly int ActiveCount;
    public readonly int InactiveCount;
    public readonly int CreatedCount;
    public readonly int RentCount;
    public readonly int ReturnCount;

    public RetroPoolSnapshot(string key, Type itemType, int activeCount, int inactiveCount, int createdCount, int rentCount, int returnCount)
    {
        Key = key;
        ItemType = itemType;
        ActiveCount = activeCount;
        InactiveCount = inactiveCount;
        CreatedCount = createdCount;
        RentCount = rentCount;
        ReturnCount = returnCount;
    }
}

public sealed class RetroObjectPool<T> where T : class
{
    private readonly Stack<T> inactive;
    private readonly HashSet<T> active;
    private readonly Func<T> factory;
    private readonly Action<T> onRent;
    private readonly Action<T> onReturn;
    private readonly Action<T> onDestroy;
    private readonly RetroPoolSettings settings;
    private bool disposed;

    public string Key { get; }
    public int ActiveCount => active.Count;
    public int InactiveCount => inactive.Count;
    public int CreatedCount { get; private set; }
    public int RentCount { get; private set; }
    public int ReturnCount { get; private set; }
    public bool IsDisposed => disposed;

    public RetroObjectPool(string key, Func<T> factory, Action<T> onRent, Action<T> onReturn, Action<T> onDestroy, RetroPoolSettings settings)
    {
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        Key = string.IsNullOrWhiteSpace(key) ? typeof(T).Name : key;
        this.factory = factory;
        this.onRent = onRent;
        this.onReturn = onReturn;
        this.onDestroy = onDestroy;
        this.settings = settings;
        inactive = new Stack<T>(Mathf.Max(4, settings.prewarmCount));
        active = new HashSet<T>();
        Prewarm(settings.prewarmCount);
    }

    public void Prewarm(int count)
    {
        if (disposed || count <= 0)
        {
            return;
        }

        int target = Mathf.Min(count, settings.ResolvedMaxInactiveCount);
        while (inactive.Count < target)
        {
            T item = CreateItem();
            if (item == null)
            {
                return;
            }

            onReturn?.Invoke(item);
            inactive.Push(item);
        }
    }

    public T Rent()
    {
        if (disposed)
        {
            return null;
        }

        T item = null;
        while (inactive.Count > 0 && item == null)
        {
            item = inactive.Pop();
        }

        item ??= CreateItem();
        if (item == null)
        {
            return null;
        }

        active.Add(item);
        RentCount++;
        onRent?.Invoke(item);
        return item;
    }

    public bool Return(T item)
    {
        if (item == null)
        {
            return false;
        }

        if (disposed)
        {
            DestroyItem(item);
            return false;
        }

        if (settings.collectionChecks && !active.Remove(item))
        {
            return false;
        }

        if (!settings.collectionChecks)
        {
            active.Remove(item);
        }

        ReturnCount++;
        onReturn?.Invoke(item);
        if (inactive.Count < settings.ResolvedMaxInactiveCount)
        {
            inactive.Push(item);
        }
        else
        {
            DestroyItem(item);
        }

        return true;
    }

    public void Clear(bool includeActive)
    {
        while (inactive.Count > 0)
        {
            DestroyItem(inactive.Pop());
        }

        if (!includeActive)
        {
            return;
        }

        foreach (T item in active)
        {
            DestroyItem(item);
        }

        active.Clear();
    }

    public RetroPoolSnapshot Snapshot()
    {
        return new RetroPoolSnapshot(Key, typeof(T), ActiveCount, InactiveCount, CreatedCount, RentCount, ReturnCount);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Clear(includeActive: true);
    }

    private T CreateItem()
    {
        T item = factory();
        if (item != null)
        {
            CreatedCount++;
        }

        return item;
    }

    private void DestroyItem(T item)
    {
        if (item != null)
        {
            onDestroy?.Invoke(item);
        }
    }
}

internal interface IRetroPoolReturnSink
{
    string Key { get; }
    bool Return(RetroPooledObject pooledObject);
}

internal interface IRetroPoolControl
{
    RetroPoolSnapshot Snapshot();
    void Clear(bool includeActive);
    void Dispose();
}

[DisallowMultipleComponent]
public sealed class RetroPooledObject : MonoBehaviour
{
    private readonly List<IRetroPoolLifecycle> lifecycleCallbacks = new List<IRetroPoolLifecycle>(4);
    private IRetroPoolReturnSink owner;
    private Component primaryComponent;
    private float returnTime = -1f;

    public string PoolKey { get; private set; }
    public bool IsRented { get; private set; }
    public Component PrimaryComponent => primaryComponent;

    public void ReturnToPool()
    {
        if (owner != null)
        {
            owner.Return(this);
            return;
        }

        gameObject.SetActive(false);
    }

    public void ReturnToPoolAfter(float seconds)
    {
        returnTime = Time.time + Mathf.Max(0f, seconds);
    }

    public void CancelScheduledReturn()
    {
        returnTime = -1f;
    }

    internal void Bind(IRetroPoolReturnSink poolOwner, string poolKey, Component primary)
    {
        owner = poolOwner;
        PoolKey = poolKey;
        primaryComponent = primary;
        RefreshLifecycleCallbacks();
    }

    internal void NotifyRented()
    {
        IsRented = true;
        returnTime = -1f;
        RefreshLifecycleCallbacks();
        for (int i = 0; i < lifecycleCallbacks.Count; i++)
        {
            lifecycleCallbacks[i].OnPoolRent(this);
        }
    }

    internal void NotifyReturned()
    {
        IsRented = false;
        returnTime = -1f;
        for (int i = 0; i < lifecycleCallbacks.Count; i++)
        {
            lifecycleCallbacks[i].OnPoolReturn(this);
        }
    }

    internal void NotifyDestroyed()
    {
        for (int i = 0; i < lifecycleCallbacks.Count; i++)
        {
            lifecycleCallbacks[i].OnPoolDestroy(this);
        }

        lifecycleCallbacks.Clear();
        owner = null;
        primaryComponent = null;
    }

    private void Update()
    {
        if (IsRented && returnTime >= 0f && Time.time >= returnTime)
        {
            ReturnToPool();
        }
    }

    private void RefreshLifecycleCallbacks()
    {
        lifecycleCallbacks.Clear();
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IRetroPoolLifecycle callback)
            {
                lifecycleCallbacks.Add(callback);
            }
        }
    }
}

public sealed class RetroComponentPool<T> : IRetroPoolReturnSink, IRetroPoolControl where T : Component
{
    private readonly Stack<T> inactive;
    private readonly HashSet<T> active;
    private readonly Func<Transform, T> factory;
    private readonly RetroPoolSettings settings;
    private readonly Transform root;
    private bool disposed;

    public string Key { get; }
    public int ActiveCount => active.Count;
    public int InactiveCount => inactive.Count;
    public int CreatedCount { get; private set; }
    public int RentCount { get; private set; }
    public int ReturnCount { get; private set; }
    public bool IsValid => !disposed && root != null;

    internal RetroComponentPool(string key, Transform parent, Func<Transform, T> factory, RetroPoolSettings settings)
    {
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        Key = string.IsNullOrWhiteSpace(key) ? typeof(T).Name : key;
        this.factory = factory;
        this.settings = settings;
        inactive = new Stack<T>(Mathf.Max(4, settings.prewarmCount));
        active = new HashSet<T>();

        GameObject rootObject = new GameObject($"{Key} Pool");
        rootObject.transform.SetParent(parent, false);
        root = rootObject.transform;
        Prewarm(settings.prewarmCount);
    }

    public void Prewarm(int count)
    {
        if (!IsValid || count <= 0)
        {
            return;
        }

        int target = Mathf.Min(count, settings.ResolvedMaxInactiveCount);
        while (inactive.Count < target)
        {
            T item = CreateInstance();
            if (item == null)
            {
                return;
            }

            ReturnToInactive(item);
            inactive.Push(item);
        }
    }

    public T Rent()
    {
        return Rent(Vector3.zero, Quaternion.identity, null);
    }

    public T Rent(Vector3 position, Quaternion rotation)
    {
        return Rent(position, rotation, null);
    }

    public T Rent(Vector3 position, Quaternion rotation, Transform parent)
    {
        if (!IsValid)
        {
            return null;
        }

        T item = null;
        while (inactive.Count > 0 && item == null)
        {
            item = inactive.Pop();
        }

        item ??= CreateInstance();
        if (item == null)
        {
            return null;
        }

        Transform itemTransform = item.transform;
        if (parent != null)
        {
            itemTransform.SetParent(parent, false);
            itemTransform.localPosition = position;
            itemTransform.localRotation = rotation;
        }
        else
        {
            itemTransform.SetParent(root, false);
            itemTransform.SetPositionAndRotation(position, rotation);
        }

        active.Add(item);
        RentCount++;
        GameObject itemObject = item.gameObject;
        itemObject.SetActive(true);
        RetroPooledObject pooledObject = itemObject.GetComponent<RetroPooledObject>();
        pooledObject?.NotifyRented();
        return item;
    }

    public bool Return(T item)
    {
        if (item == null)
        {
            return false;
        }

        if (disposed)
        {
            DestroyInstance(item);
            return false;
        }

        if (settings.collectionChecks && !active.Remove(item))
        {
            return false;
        }

        if (!settings.collectionChecks)
        {
            active.Remove(item);
        }

        ReturnCount++;
        RetroPooledObject pooledObject = item.GetComponent<RetroPooledObject>();
        pooledObject?.NotifyReturned();
        ReturnToInactive(item);

        if (inactive.Count < settings.ResolvedMaxInactiveCount)
        {
            inactive.Push(item);
        }
        else
        {
            DestroyInstance(item);
        }

        return true;
    }

    bool IRetroPoolReturnSink.Return(RetroPooledObject pooledObject)
    {
        if (pooledObject == null)
        {
            return false;
        }

        T item = pooledObject.PrimaryComponent as T;
        if (item == null)
        {
            item = pooledObject.GetComponent<T>();
        }

        return Return(item);
    }

    public void Clear(bool includeActive)
    {
        while (inactive.Count > 0)
        {
            DestroyInstance(inactive.Pop());
        }

        if (!includeActive)
        {
            return;
        }

        foreach (T item in active)
        {
            DestroyInstance(item);
        }

        active.Clear();
    }

    public RetroPoolSnapshot Snapshot()
    {
        return new RetroPoolSnapshot(Key, typeof(T), ActiveCount, InactiveCount, CreatedCount, RentCount, ReturnCount);
    }

    internal void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Clear(includeActive: true);
        if (root != null)
        {
            DestroyUnityObject(root.gameObject);
        }
    }

    void IRetroPoolControl.Dispose()
    {
        Dispose();
    }

    private T CreateInstance()
    {
        T item = factory(root);
        if (item == null)
        {
            return null;
        }

        GameObject itemObject = item.gameObject;
        RetroPooledObject pooledObject = itemObject.GetComponent<RetroPooledObject>();
        if (pooledObject == null)
        {
            pooledObject = itemObject.AddComponent<RetroPooledObject>();
        }

        pooledObject.Bind(this, Key, item);
        CreatedCount++;
        return item;
    }

    private void ReturnToInactive(T item)
    {
        if (item == null)
        {
            return;
        }

        GameObject itemObject = item.gameObject;
        itemObject.SetActive(false);
        if (settings.reparentOnReturn && root != null)
        {
            item.transform.SetParent(root, false);
        }
    }

    private void DestroyInstance(T item)
    {
        if (item == null)
        {
            return;
        }

        RetroPooledObject pooledObject = item.GetComponent<RetroPooledObject>();
        pooledObject?.NotifyDestroyed();
        DestroyUnityObject(item.gameObject);
    }

    private static void DestroyUnityObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}

[DisallowMultipleComponent]
public sealed class RetroPoolService : MonoBehaviour
{
    private static RetroPoolService shared;
    private readonly Dictionary<string, object> componentPools = new Dictionary<string, object>();

    public static RetroPoolService Shared
    {
        get
        {
            if (shared != null)
            {
                return shared;
            }

            shared = FindAnyObjectByType<RetroPoolService>();
            if (shared != null)
            {
                return shared;
            }

            GameObject serviceObject = new GameObject("RetroPoolService");
            shared = serviceObject.AddComponent<RetroPoolService>();
            return shared;
        }
    }

    public static bool TryReturn(Component component)
    {
        if (component == null)
        {
            return false;
        }

        RetroPooledObject pooledObject = component.GetComponentInParent<RetroPooledObject>();
        if (pooledObject == null)
        {
            return false;
        }

        pooledObject.ReturnToPool();
        return true;
    }

    public static bool TryReturn(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        RetroPooledObject pooledObject = gameObject.GetComponent<RetroPooledObject>();
        if (pooledObject == null)
        {
            return false;
        }

        pooledObject.ReturnToPool();
        return true;
    }

    public RetroComponentPool<T> GetOrCreateComponentPool<T>(string key, Func<Transform, T> factory, RetroPoolSettings settings) where T : Component
    {
        string resolvedKey = string.IsNullOrWhiteSpace(key) ? typeof(T).FullName : key;
        if (componentPools.TryGetValue(resolvedKey, out object existing))
        {
            if (existing is RetroComponentPool<T> typedPool)
            {
                return typedPool;
            }

            Debug.LogError($"Pool key '{resolvedKey}' is already registered for a different type.", this);
            return null;
        }

        RetroComponentPool<T> pool = new RetroComponentPool<T>(resolvedKey, transform, factory, settings);
        componentPools.Add(resolvedKey, pool);
        return pool;
    }

    public RetroComponentPool<T> GetOrCreatePrefabPool<T>(string key, T prefab, RetroPoolSettings settings) where T : Component
    {
        if (prefab == null)
        {
            Debug.LogError("Cannot create a component pool from a null prefab.", this);
            return null;
        }

        string resolvedKey = string.IsNullOrWhiteSpace(key)
            ? $"{typeof(T).FullName}:{prefab.gameObject.name}"
            : key;

        return GetOrCreateComponentPool(
            resolvedKey,
            parent =>
            {
                T instance = Instantiate(prefab, parent);
                instance.gameObject.name = prefab.gameObject.name;
                return instance;
            },
            settings);
    }

    public RetroComponentPool<Transform> GetOrCreatePrefabPool(string key, GameObject prefab, RetroPoolSettings settings)
    {
        if (prefab == null)
        {
            Debug.LogError("Cannot create a GameObject pool from a null prefab.", this);
            return null;
        }

        string resolvedKey = string.IsNullOrWhiteSpace(key)
            ? $"GameObject:{prefab.name}"
            : key;

        return GetOrCreateComponentPool(
            resolvedKey,
            parent =>
            {
                GameObject instance = Instantiate(prefab, parent);
                instance.name = prefab.name;
                return instance.transform;
            },
            settings);
    }

    public void ClearAll(bool includeActive)
    {
        foreach (object pool in componentPools.Values)
        {
            if (pool is IRetroPoolControl control)
            {
                control.Clear(includeActive);
            }
        }
    }

    public void CollectSnapshots(List<RetroPoolSnapshot> results)
    {
        if (results == null)
        {
            return;
        }

        foreach (object pool in componentPools.Values)
        {
            if (pool is IRetroPoolControl control)
            {
                results.Add(control.Snapshot());
            }
        }
    }

    private void Awake()
    {
        if (shared != null && shared != this)
        {
            Destroy(gameObject);
            return;
        }

        shared = this;
    }

    private void OnDestroy()
    {
        foreach (object pool in componentPools.Values)
        {
            if (pool is IRetroPoolControl control)
            {
                control.Dispose();
            }
        }

        componentPools.Clear();
        if (shared == this)
        {
            shared = null;
        }
    }
}
