using UnityEngine;

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class RetroGameContext : MonoBehaviour
{
    private static RetroGameContext shared;

    [SerializeField] private RetroGameplayEventBus eventBus;
    [SerializeField] private RetroPoolService poolService;
    [SerializeField] private RetroAudioService audioService;
    [SerializeField] private RetroVfxService vfxService;
    [SerializeField] private RetroWeaponFeedbackService weaponFeedbackService;

    public static RetroGameContext Shared
    {
        get
        {
            if (shared != null)
            {
                return shared;
            }

            shared = FindAnyObjectByType<RetroGameContext>();
            if (shared != null)
            {
                shared.EnsureServices();
                return shared;
            }

            GameObject contextObject = new GameObject("RetroGameContext");
            shared = contextObject.AddComponent<RetroGameContext>();
            shared.EnsureServices();
            return shared;
        }
    }

    public static RetroGameplayEventBus Events => Shared.EventBus;
    public static RetroPoolService Pools => Shared.PoolService;
    public static RetroAudioService Audio => Shared.AudioService;
    public static RetroVfxService Vfx => Shared.VfxService;
    public static RetroWeaponFeedbackService WeaponFeedback => Shared.WeaponFeedbackService;

    public RetroGameplayEventBus EventBus
    {
        get
        {
            EnsureServices();
            return eventBus;
        }
    }

    public RetroPoolService PoolService
    {
        get
        {
            EnsureServices();
            return poolService;
        }
    }

    public RetroAudioService AudioService
    {
        get
        {
            EnsureServices();
            return audioService;
        }
    }

    public RetroVfxService VfxService
    {
        get
        {
            EnsureServices();
            return vfxService;
        }
    }

    public RetroWeaponFeedbackService WeaponFeedbackService
    {
        get
        {
            EnsureServices();
            return weaponFeedbackService;
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
        EnsureServices();
    }

    private void EnsureServices()
    {
        eventBus = EnsureLocalService(eventBus);
        poolService = EnsurePoolService(poolService);
        audioService = EnsureLocalService(audioService);
        vfxService = EnsureLocalService(vfxService);
        weaponFeedbackService = EnsureLocalService(weaponFeedbackService);
    }

    private RetroPoolService EnsurePoolService(RetroPoolService service)
    {
        if (service != null)
        {
            return service;
        }

        service = GetComponent<RetroPoolService>();
        return service != null ? service : RetroPoolService.Shared;
    }

    private T EnsureLocalService<T>(T service) where T : Component
    {
        if (service != null)
        {
            return service;
        }

        service = GetComponent<T>();
        if (service == null)
        {
            service = gameObject.AddComponent<T>();
        }

        return service;
    }

    private void OnDestroy()
    {
        if (shared == this)
        {
            shared = null;
        }
    }
}
