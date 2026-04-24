using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class RetroWeaponSystem : MonoBehaviour
{
    [Serializable]
    private sealed class WeaponSpriteView
    {
        public FirstPersonSpriteVolumeMapSet mapSet;
        public Vector3 visualLocalPosition = Vector3.zero;
        public Vector3 visualLocalEuler = Vector3.zero;
        public Vector2 visualSize = new Vector2(1.2f, 0.9f);
        public Vector3 muzzleLocalPosition = new Vector3(0f, 0f, 0.55f);
        public Color baseTintMultiplier = Color.white;
        public Color emissiveTintMultiplier = Color.white;
        [Range(0f, 1f)] public float weaponBodyTint = 0.25f;
        [Range(0f, 1f)] public float weaponAccentTint = 0.65f;
    }

    private sealed class WeaponRuntime
    {
        public RetroWeaponDefinition Definition;
        public string Name;
        public RetroWeaponKind Kind;
        public RetroFireMode FireMode;
        public int MagazineSize;
        public int AmmoInMagazine;
        public int ReserveAmmo;
        public int MaxReserveAmmo;
        public float ReloadDuration;
        public float FireInterval;
        public bool AutoReloadWhenEmpty;
        public float DryFireCooldown;
        public Vector3 DryFireKickPosition;
        public Vector3 DryFireKickEuler;
        public float Damage;
        public float Range;
        public int Pellets;
        public float SpreadAngle;
        public float SpreadBloomPerShot;
        public float MaxSpreadAngle;
        public float SpreadRecoverySpeed;
        public float MovementSpreadPenalty;
        public float CurrentSpreadBloom;
        public float ProjectileSpeed;
        public float ExplosionRadius;
        public float ExplosionForce;
        public float FuseTime;
        public float ImpactForce;
        public Vector3 LocalPosition;
        public Vector3 LocalEuler;
        public Vector3 RecoilPosition;
        public Vector3 RecoilEuler;
        public Color BodyColor;
        public Color AccentColor;
        public float MuzzleFlashScale;
        public bool BulletTrailEnabled;
        public Color BulletTrailColor;
        public float BulletTrailWidth;
        public float BulletTrailDuration;
        public float BulletTrailStartOffset;
        public float BulletTrailEndOffset;
        public int BulletTrailMaxSegmentsPerShot;
        public FirstPersonSpriteVolumeMapSet SpriteMapSet;
        public Vector3 SpriteVisualLocalPosition;
        public Vector3 SpriteVisualLocalEuler;
        public Vector2 SpriteVisualSize;
        public Vector3 SpriteMuzzleLocalPosition;
        public Vector3 SpriteMuzzleLocalOffset;
        public Color SpriteBaseTintMultiplier;
        public Color SpriteEmissiveTintMultiplier;
        public float SpriteWeaponBodyTint;
        public float SpriteWeaponAccentTint;
        public Sprite MuzzleFlashSprite;
        public Vector2 MuzzleFlashSpriteSize;
        public GameObject Visual;
        public Transform Muzzle;
        public GameObject MuzzleFlash;
    }

    [Header("References")]
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Renderer legacyViewModelRenderer;
    [SerializeField] private FirstPersonSpriteVolumeRenderer legacySpriteVolumeRenderer;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string attackActionName = "Attack";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string previousWeaponActionName = "Previous";
    [SerializeField] private string nextWeaponActionName = "Next";
    [SerializeField] private bool disableLegacyViewModel = true;
    [SerializeField] private bool showDebugHud = true;

    [Header("HUD")]
    [SerializeField] private bool showCrosshair = true;
    [SerializeField] private Color crosshairColor = new Color(1f, 1f, 1f, 0.82f);
    [SerializeField, Min(0f)] private float crosshairGap = 5f;
    [SerializeField, Min(1f)] private float crosshairLength = 9f;
    [SerializeField, Min(1f)] private float crosshairThickness = 2f;

    [Header("Combat")]
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField, Min(0.01f)] private float defaultImpactForce = 20f;
    [SerializeField, Min(0.01f)] private float weaponSwitchDuration = 0.2f;

    [Header("Loadout")]
    [SerializeField] private RetroWeaponDefinition[] weaponDefinitions = Array.Empty<RetroWeaponDefinition>();

    [Header("Viewmodel")]
    [SerializeField] private bool useSpriteVolumeViewModel = true;
    [SerializeField] private WeaponSpriteView[] weaponSpriteViews = Array.Empty<WeaponSpriteView>();
    [SerializeField] private Sprite[] muzzleFlashSprites = Array.Empty<Sprite>();
    [SerializeField] private Vector2[] muzzleFlashSpriteSizes = Array.Empty<Vector2>();
    [SerializeField, Min(0.01f)] private float spriteMuzzleFlashDuration = 0.055f;
    [SerializeField] private Vector3[] spriteMuzzleLocalOffsets =
    {
        new Vector3(0.055f, 0f, 0f),
        new Vector3(0.055f, 0f, 0f),
        new Vector3(0.055f, 0f, 0f),
        new Vector3(0.055f, 0f, 0f)
    };
    [SerializeField] private Vector3 baseLocalPosition = new Vector3(0.32f, -0.22f, 0.58f);
    [SerializeField] private Vector3 baseLocalEuler = new Vector3(2f, -6f, 0f);
    [SerializeField, Min(0f)] private float bobFrequency = 8.5f;
    [SerializeField, Min(0f)] private float bobHorizontalAmplitude = 0.018f;
    [SerializeField, Min(0f)] private float bobVerticalAmplitude = 0.012f;
    [SerializeField, Min(0f)] private float mouseSwayScale = 0.0025f;
    [SerializeField, Min(0f)] private float gamepadSwayScale = 0.03f;
    [SerializeField, Min(0f)] private float swayPositionAmount = 0.03f;
    [SerializeField, Min(0f)] private float swayRotationAmount = 6f;
    [SerializeField, Min(0f)] private float recoilReturnSpeed = 15f;
    [SerializeField, Min(0f)] private float reloadDrop = 0.14f;
    [SerializeField, Min(0f)] private float reloadBack = 0.16f;
    [SerializeField, Min(0f)] private float reloadTilt = 24f;
    [SerializeField, Min(0f)] private float switchDrop = 0.09f;
    [SerializeField, Min(0f)] private float switchYaw = 14f;

    private WeaponRuntime[] weapons = Array.Empty<WeaponRuntime>();
    private InputActionMap actionMap;
    private InputAction attackAction;
    private InputAction lookAction;
    private InputAction previousWeaponAction;
    private InputAction nextWeaponAction;
    private Rigidbody body;
    private Transform runtimeRoot;
    private Transform spriteViewModelTransform;
    private Transform spriteMuzzle;
    private GameObject spriteMuzzleFlash;
    private Material spriteMuzzleFlashMaterial;
    private Texture spriteMuzzleFlashTexture;
    private RetroComponentPool<RetroGrenadeProjectile> grenadeProjectilePool;
    private FirstPersonSpriteVolumeMapSet defaultSpriteMapSet;
    private bool usingSpriteVolumeViewModel;
    private bool ownsActionMap;
    private bool usingGamepadLook;
    private bool isReloading;
    private int currentWeaponIndex;
    private float nextFireTime;
    private float reloadEndTime;
    private float switchEndTime = -999f;
    private float bobTime;
    private float muzzleFlashDisableTime = -999f;
    private Vector2 lookSample;
    private Vector3 recoilPositionOffset;
    private Vector3 recoilEulerOffset;

    public string CurrentWeaponName => CurrentWeapon != null ? CurrentWeapon.Name : string.Empty;
    public int CurrentMagazineAmmo => CurrentWeapon != null ? CurrentWeapon.AmmoInMagazine : 0;
    public int CurrentReserveAmmo => CurrentWeapon != null ? CurrentWeapon.ReserveAmmo : 0;
    public RetroWeaponDefinition CurrentWeaponDefinition => CurrentWeapon != null ? CurrentWeapon.Definition : null;
    public float CurrentSpreadAngle => CurrentWeapon != null ? ResolveCurrentSpreadAngle(CurrentWeapon) : 0f;
    public bool Reloading => isReloading;

    public event Action<RetroWeaponDefinition> WeaponSelected;
    public event Action<RetroWeaponDefinition> WeaponFired;
    public event Action<RetroWeaponDefinition> WeaponDryFired;
    public event Action<RetroWeaponDefinition> ReloadStarted;
    public event Action<RetroWeaponDefinition> ReloadCompleted;

    private WeaponRuntime CurrentWeapon => currentWeaponIndex >= 0 && currentWeaponIndex < weapons.Length
        ? weapons[currentWeaponIndex]
        : null;

    private void Reset()
    {
        AutoWireReferences();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        AutoWireReferences();
        ResolveActions();
        EnsureWeaponsInitialized();
        EnsureRuntimePresentation();
        ApplyPresentationMode();
        SetWeaponActive(currentWeaponIndex, true);
        ApplyCurrentSpriteView(forceLightRefresh: true);
    }

    private void OnEnable()
    {
        if (actionMap == null)
        {
            ResolveActions();
        }

        if (ownsActionMap && actionMap != null)
        {
            actionMap.Enable();
        }
    }

    private void OnDisable()
    {
        if (ownsActionMap && actionMap != null)
        {
            actionMap.Disable();
        }
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterial(spriteMuzzleFlashMaterial);
        spriteMuzzleFlashMaterial = null;
        spriteMuzzleFlashTexture = null;
    }

    private void Update()
    {
        if (viewCamera == null)
        {
            return;
        }

        UpdateLookSample();
        CompleteReloadIfNeeded();
        RecoverWeaponSpread(Time.deltaTime);
        HandleWeaponSelectionInput();
        HandleReloadInput();
        HandleFireInput();
        UpdateMuzzleFlashState();
    }

    private void LateUpdate()
    {
        UpdateViewmodelPresentation(Time.deltaTime);
    }

    private void AutoWireReferences()
    {
        if (viewCamera == null)
        {
            RetroFpsController controller = GetComponent<RetroFpsController>();
            if (controller != null)
            {
                viewCamera = controller.ViewCamera;
                inputActions = inputActions != null ? inputActions : controller.InputActionsAsset;
            }
        }

        if (viewCamera == null)
        {
            viewCamera = GetComponentInChildren<Camera>(true);
        }

        if (legacyViewModelRenderer == null)
        {
            Transform quad = FindNamedChildRecursive(transform, "Quad");
            if (quad != null)
            {
                legacyViewModelRenderer = quad.GetComponent<Renderer>();
                legacySpriteVolumeRenderer = quad.GetComponent<FirstPersonSpriteVolumeRenderer>();
            }
        }

        if (legacySpriteVolumeRenderer == null && legacyViewModelRenderer != null)
        {
            legacySpriteVolumeRenderer = legacyViewModelRenderer.GetComponent<FirstPersonSpriteVolumeRenderer>();
        }

        if (legacyViewModelRenderer == null && legacySpriteVolumeRenderer != null)
        {
            legacyViewModelRenderer = legacySpriteVolumeRenderer.GetComponent<Renderer>();
        }
    }

    private void ResolveActions()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();
        InputActionAsset source = playerInput != null ? playerInput.actions : inputActions;
        ownsActionMap = playerInput == null;

        if (source == null)
        {
            Debug.LogWarning($"{nameof(RetroWeaponSystem)} on {name} needs a PlayerInput component or an InputActionAsset assigned.", this);
            return;
        }

        actionMap = source.FindActionMap(actionMapName, false);
        if (actionMap == null)
        {
            Debug.LogWarning($"{nameof(RetroWeaponSystem)} could not find action map '{actionMapName}' on {source.name}.", this);
            return;
        }

        attackAction = actionMap.FindAction(attackActionName, false);
        lookAction = actionMap.FindAction(lookActionName, false);
        previousWeaponAction = actionMap.FindAction(previousWeaponActionName, false);
        nextWeaponAction = actionMap.FindAction(nextWeaponActionName, false);
    }

    private void EnsureWeaponsInitialized()
    {
        if (weapons != null && weapons.Length > 0)
        {
            return;
        }

        if (weaponDefinitions != null && weaponDefinitions.Length > 0)
        {
            weapons = new WeaponRuntime[weaponDefinitions.Length];
            for (int i = 0; i < weaponDefinitions.Length; i++)
            {
                weapons[i] = BuildAuthoredWeaponRuntime(weaponDefinitions[i], i);
            }
        }
        else
        {
            weapons = new WeaponRuntime[4];
            for (int i = 0; i < weapons.Length; i++)
            {
                weapons[i] = BuildDefaultWeaponRuntime(i);
            }
        }

        currentWeaponIndex = Mathf.Clamp(currentWeaponIndex, 0, Mathf.Max(0, weapons.Length - 1));
    }

    private WeaponRuntime BuildAuthoredWeaponRuntime(RetroWeaponDefinition definition, int index)
    {
        WeaponRuntime weapon = BuildDefaultWeaponRuntime(index);
        if (definition == null)
        {
            return weapon;
        }

        int maxReserveAmmo = Mathf.Max(0, definition.startingReserveAmmo, definition.maxReserveAmmo);
        int startingReserveAmmo = Mathf.Clamp(definition.startingReserveAmmo, 0, maxReserveAmmo);

        weapon.Definition = definition;
        weapon.Name = string.IsNullOrWhiteSpace(definition.displayName) ? definition.name : definition.displayName;
        weapon.Kind = definition.kind;
        weapon.FireMode = definition.fireMode;
        weapon.MagazineSize = Mathf.Max(1, definition.magazineSize);
        weapon.AmmoInMagazine = weapon.MagazineSize;
        weapon.ReserveAmmo = startingReserveAmmo;
        weapon.MaxReserveAmmo = maxReserveAmmo;
        weapon.ReloadDuration = Mathf.Max(0.01f, definition.reloadDuration);
        weapon.FireInterval = Mathf.Max(0.01f, definition.fireInterval);
        weapon.AutoReloadWhenEmpty = definition.autoReloadWhenEmpty;
        weapon.DryFireCooldown = Mathf.Max(0.01f, definition.dryFireCooldown);
        weapon.DryFireKickPosition = definition.dryFireKickPosition;
        weapon.DryFireKickEuler = definition.dryFireKickEuler;
        weapon.Damage = Mathf.Max(0f, definition.damage);
        weapon.Range = Mathf.Max(0.01f, definition.range);
        weapon.Pellets = Mathf.Max(1, definition.pellets);
        weapon.SpreadAngle = Mathf.Max(0f, definition.spreadAngle);
        weapon.SpreadBloomPerShot = Mathf.Max(0f, definition.spreadBloomPerShot);
        weapon.MaxSpreadAngle = Mathf.Max(weapon.SpreadAngle, definition.maxSpreadAngle);
        weapon.SpreadRecoverySpeed = Mathf.Max(0f, definition.spreadRecoverySpeed);
        weapon.MovementSpreadPenalty = Mathf.Max(0f, definition.movementSpreadPenalty);
        weapon.CurrentSpreadBloom = 0f;
        weapon.ProjectileSpeed = Mathf.Max(0f, definition.projectileSpeed);
        weapon.ExplosionRadius = Mathf.Max(0f, definition.explosionRadius);
        weapon.ExplosionForce = Mathf.Max(0f, definition.explosionForce);
        weapon.FuseTime = Mathf.Max(0f, definition.fuseTime);
        weapon.ImpactForce = Mathf.Max(0f, definition.impactForce);
        weapon.LocalPosition = definition.localPosition;
        weapon.LocalEuler = definition.localEuler;
        weapon.RecoilPosition = definition.recoilPosition;
        weapon.RecoilEuler = definition.recoilEuler;
        weapon.BodyColor = definition.bodyColor;
        weapon.AccentColor = definition.accentColor;
        weapon.MuzzleFlashScale = Mathf.Max(0f, definition.primitiveMuzzleFlashScale);
        weapon.BulletTrailEnabled = definition.bulletTrailEnabled;
        weapon.BulletTrailColor = definition.bulletTrailColor;
        weapon.BulletTrailWidth = Mathf.Max(0.001f, definition.bulletTrailWidth);
        weapon.BulletTrailDuration = Mathf.Max(0.01f, definition.bulletTrailDuration);
        weapon.BulletTrailStartOffset = Mathf.Max(0f, definition.bulletTrailStartOffset);
        weapon.BulletTrailEndOffset = Mathf.Max(0f, definition.bulletTrailEndOffset);
        weapon.BulletTrailMaxSegmentsPerShot = Mathf.Clamp(definition.bulletTrailMaxSegmentsPerShot, 1, 16);
        weapon.SpriteMapSet = definition.spriteMapSet != null ? definition.spriteMapSet : weapon.SpriteMapSet;
        weapon.SpriteVisualLocalPosition = definition.spriteVisualLocalPosition;
        weapon.SpriteVisualLocalEuler = definition.spriteVisualLocalEuler;
        weapon.SpriteVisualSize = new Vector2(
            Mathf.Max(0.01f, definition.spriteVisualSize.x),
            Mathf.Max(0.01f, definition.spriteVisualSize.y));
        weapon.SpriteMuzzleLocalPosition = definition.spriteMuzzleLocalPosition;
        weapon.SpriteMuzzleLocalOffset = definition.spriteMuzzleLocalOffset;
        weapon.SpriteBaseTintMultiplier = definition.baseTintMultiplier;
        weapon.SpriteEmissiveTintMultiplier = definition.emissiveTintMultiplier;
        weapon.SpriteWeaponBodyTint = Mathf.Clamp01(definition.weaponBodyTint);
        weapon.SpriteWeaponAccentTint = Mathf.Clamp01(definition.weaponAccentTint);
        weapon.MuzzleFlashSprite = definition.muzzleFlashSprite != null ? definition.muzzleFlashSprite : weapon.MuzzleFlashSprite;
        weapon.MuzzleFlashSpriteSize = new Vector2(
            Mathf.Max(0.01f, definition.muzzleFlashSpriteSize.x),
            Mathf.Max(0.01f, definition.muzzleFlashSpriteSize.y));

        return weapon;
    }

    private WeaponRuntime BuildDefaultWeaponRuntime(int index)
    {
        WeaponRuntime weapon = index switch
        {
            0 => new WeaponRuntime
            {
                Name = "Pistol",
                Kind = RetroWeaponKind.Hitscan,
                FireMode = RetroFireMode.SemiAuto,
                MagazineSize = 12,
                AmmoInMagazine = 12,
                ReserveAmmo = 72,
                MaxReserveAmmo = 72,
                ReloadDuration = 1.2f,
                FireInterval = 0.28f,
                Damage = 28f,
                Range = 110f,
                Pellets = 1,
                SpreadAngle = 0.45f,
                ImpactForce = defaultImpactForce * 0.9f,
                LocalPosition = new Vector3(0.02f, -0.01f, 0f),
                LocalEuler = Vector3.zero,
                RecoilPosition = new Vector3(0f, 0.006f, -0.06f),
                RecoilEuler = new Vector3(4.5f, 1.2f, 1.6f),
                BodyColor = new Color(0.13f, 0.13f, 0.15f),
                AccentColor = new Color(0.72f, 0.68f, 0.58f),
                MuzzleFlashScale = 0.18f,
                BulletTrailEnabled = true,
                BulletTrailColor = new Color(1f, 0.82f, 0.35f, 0.78f),
                BulletTrailWidth = 0.018f,
                BulletTrailDuration = 0.065f,
                BulletTrailStartOffset = 0.08f,
                BulletTrailEndOffset = 0.08f,
                BulletTrailMaxSegmentsPerShot = 1
            },
            1 => new WeaponRuntime
            {
                Name = "Rifle",
                Kind = RetroWeaponKind.Hitscan,
                FireMode = RetroFireMode.Automatic,
                MagazineSize = 30,
                AmmoInMagazine = 30,
                ReserveAmmo = 150,
                MaxReserveAmmo = 150,
                ReloadDuration = 1.65f,
                FireInterval = 0.095f,
                Damage = 18f,
                Range = 165f,
                Pellets = 1,
                SpreadAngle = 0.8f,
                ImpactForce = defaultImpactForce,
                LocalPosition = new Vector3(0.03f, -0.03f, 0.03f),
                LocalEuler = new Vector3(1f, 0f, 0f),
                RecoilPosition = new Vector3(0f, 0.008f, -0.035f),
                RecoilEuler = new Vector3(2.5f, 0.7f, 1.2f),
                BodyColor = new Color(0.16f, 0.18f, 0.19f),
                AccentColor = new Color(0.42f, 0.34f, 0.22f),
                MuzzleFlashScale = 0.24f,
                BulletTrailEnabled = true,
                BulletTrailColor = new Color(1f, 0.9f, 0.42f, 0.72f),
                BulletTrailWidth = 0.014f,
                BulletTrailDuration = 0.045f,
                BulletTrailStartOffset = 0.1f,
                BulletTrailEndOffset = 0.1f,
                BulletTrailMaxSegmentsPerShot = 1
            },
            2 => new WeaponRuntime
            {
                Name = "Shotgun",
                Kind = RetroWeaponKind.Hitscan,
                FireMode = RetroFireMode.SemiAuto,
                MagazineSize = 8,
                AmmoInMagazine = 8,
                ReserveAmmo = 40,
                MaxReserveAmmo = 40,
                ReloadDuration = 1.95f,
                FireInterval = 0.82f,
                Damage = 11f,
                Range = 60f,
                Pellets = 8,
                SpreadAngle = 4.4f,
                ImpactForce = defaultImpactForce * 1.75f,
                LocalPosition = new Vector3(0.02f, -0.04f, 0.06f),
                LocalEuler = new Vector3(2f, 0f, 0f),
                RecoilPosition = new Vector3(0f, 0.01f, -0.095f),
                RecoilEuler = new Vector3(8f, 1.4f, 2f),
                BodyColor = new Color(0.18f, 0.14f, 0.11f),
                AccentColor = new Color(0.45f, 0.36f, 0.19f),
                MuzzleFlashScale = 0.32f,
                BulletTrailEnabled = true,
                BulletTrailColor = new Color(1f, 0.68f, 0.32f, 0.58f),
                BulletTrailWidth = 0.012f,
                BulletTrailDuration = 0.075f,
                BulletTrailStartOffset = 0.08f,
                BulletTrailEndOffset = 0.08f,
                BulletTrailMaxSegmentsPerShot = 8
            },
            3 => new WeaponRuntime
            {
                Name = "Grenade Launcher",
                Kind = RetroWeaponKind.GrenadeLauncher,
                FireMode = RetroFireMode.SemiAuto,
                MagazineSize = 1,
                AmmoInMagazine = 1,
                ReserveAmmo = 10,
                MaxReserveAmmo = 10,
                ReloadDuration = 2f,
                FireInterval = 0.9f,
                Damage = 95f,
                Range = 120f,
                Pellets = 1,
                SpreadAngle = 0.4f,
                ProjectileSpeed = 28f,
                ExplosionRadius = 5.5f,
                ExplosionForce = 850f,
                FuseTime = 2.5f,
                ImpactForce = defaultImpactForce * 0.75f,
                LocalPosition = new Vector3(0.05f, -0.05f, 0.08f),
                LocalEuler = new Vector3(2f, 0f, 0f),
                RecoilPosition = new Vector3(0f, 0.015f, -0.12f),
                RecoilEuler = new Vector3(10f, 2f, 2.5f),
                BodyColor = new Color(0.14f, 0.16f, 0.13f),
                AccentColor = new Color(0.4f, 0.55f, 0.24f),
                MuzzleFlashScale = 0.36f,
                BulletTrailEnabled = true,
                BulletTrailColor = new Color(0.6f, 1f, 0.28f, 0.55f),
                BulletTrailWidth = 0.035f,
                BulletTrailDuration = 0.12f,
                BulletTrailStartOffset = 0.06f,
                BulletTrailEndOffset = 0f,
                BulletTrailMaxSegmentsPerShot = 1
            },
            _ => new WeaponRuntime
            {
                Name = $"Weapon {index + 1}",
                Kind = RetroWeaponKind.Hitscan,
                FireMode = RetroFireMode.SemiAuto,
                MagazineSize = 12,
                AmmoInMagazine = 12,
                ReserveAmmo = 72,
                MaxReserveAmmo = 72,
                ReloadDuration = 1.2f,
                FireInterval = 0.28f,
                Damage = 28f,
                Range = 110f,
                Pellets = 1,
                SpreadAngle = 0.45f,
                ImpactForce = defaultImpactForce,
                LocalPosition = Vector3.zero,
                LocalEuler = Vector3.zero,
                RecoilPosition = new Vector3(0f, 0.006f, -0.06f),
                RecoilEuler = new Vector3(4.5f, 1.2f, 1.6f),
                BodyColor = new Color(0.13f, 0.13f, 0.15f),
                AccentColor = new Color(0.72f, 0.68f, 0.58f),
                MuzzleFlashScale = 0.18f,
                BulletTrailEnabled = true,
                BulletTrailColor = new Color(1f, 0.82f, 0.35f, 0.78f),
                BulletTrailWidth = 0.018f,
                BulletTrailDuration = 0.065f,
                BulletTrailStartOffset = 0.08f,
                BulletTrailEndOffset = 0.08f,
                BulletTrailMaxSegmentsPerShot = 1
            }
        };

        ApplyDefaultFeelAuthoring(weapon, index);
        ApplyDefaultSpriteAuthoring(weapon, index);
        return weapon;
    }

    private static void ApplyDefaultFeelAuthoring(WeaponRuntime weapon, int index)
    {
        if (weapon == null)
        {
            return;
        }

        weapon.AutoReloadWhenEmpty = true;
        weapon.DryFireCooldown = 0.16f;
        weapon.DryFireKickPosition = new Vector3(0f, 0.001f, -0.012f);
        weapon.DryFireKickEuler = new Vector3(1.2f, 0.25f, 0.35f);

        switch (index)
        {
            case 1:
                weapon.SpreadBloomPerShot = 0.11f;
                weapon.MaxSpreadAngle = 2.2f;
                weapon.SpreadRecoverySpeed = 7f;
                weapon.MovementSpreadPenalty = 0.45f;
                break;
            case 2:
                weapon.SpreadBloomPerShot = 0.35f;
                weapon.MaxSpreadAngle = 5.4f;
                weapon.SpreadRecoverySpeed = 3.5f;
                weapon.MovementSpreadPenalty = 0.8f;
                break;
            case 3:
                weapon.SpreadBloomPerShot = 0.05f;
                weapon.MaxSpreadAngle = 0.7f;
                weapon.SpreadRecoverySpeed = 4f;
                weapon.MovementSpreadPenalty = 0.15f;
                weapon.DryFireCooldown = 0.24f;
                weapon.DryFireKickPosition = new Vector3(0f, 0.001f, -0.018f);
                weapon.DryFireKickEuler = new Vector3(1.8f, 0.2f, 0.25f);
                break;
            default:
                weapon.SpreadBloomPerShot = 0.18f;
                weapon.MaxSpreadAngle = 1.4f;
                weapon.SpreadRecoverySpeed = 6f;
                weapon.MovementSpreadPenalty = 0.25f;
                break;
        }

        weapon.MaxSpreadAngle = Mathf.Max(weapon.SpreadAngle, weapon.MaxSpreadAngle);
        weapon.CurrentSpreadBloom = 0f;
    }

    private void ApplyDefaultSpriteAuthoring(WeaponRuntime weapon, int index)
    {
        WeaponSpriteView spriteView = ResolveWeaponSpriteView(index);
        weapon.SpriteMapSet = spriteView != null ? spriteView.mapSet : null;
        weapon.SpriteVisualLocalPosition = ResolveSpriteVisualLocalPosition(index, spriteView);
        weapon.SpriteVisualLocalEuler = ResolveSpriteVisualLocalEuler(index, spriteView);
        weapon.SpriteVisualSize = ResolveSpriteVisualSize(index, spriteView);
        weapon.SpriteMuzzleLocalPosition = ResolveSpriteMuzzleLocalPosition(index, spriteView);
        weapon.SpriteMuzzleLocalOffset = ResolveSerializedSpriteMuzzleLocalOffset(index);
        weapon.SpriteBaseTintMultiplier = spriteView != null ? spriteView.baseTintMultiplier : Color.white;
        weapon.SpriteEmissiveTintMultiplier = spriteView != null ? spriteView.emissiveTintMultiplier : Color.white;
        weapon.SpriteWeaponBodyTint = spriteView != null ? spriteView.weaponBodyTint : 0.18f;
        weapon.SpriteWeaponAccentTint = spriteView != null ? spriteView.weaponAccentTint : 0.7f;
        weapon.MuzzleFlashSprite = ResolveSerializedMuzzleFlashSprite(index);
        weapon.MuzzleFlashSpriteSize = ResolveSerializedMuzzleFlashSpriteSize(index);
    }

    private void EnsureRuntimePresentation()
    {
        if (viewCamera == null || runtimeRoot != null)
        {
            return;
        }

        if (CanUseSpriteVolumeViewModel())
        {
            EnsureSpriteVolumePresentation();
            return;
        }

        usingSpriteVolumeViewModel = false;
        runtimeRoot = new GameObject("RuntimeWeaponRoot").transform;
        runtimeRoot.SetParent(viewCamera.transform, false);

        for (int i = 0; i < weapons.Length; i++)
        {
            BuildWeaponVisual(i, weapons[i]);
            SetWeaponActive(i, i == currentWeaponIndex);
        }
    }

    private bool CanUseSpriteVolumeViewModel()
    {
        return useSpriteVolumeViewModel
            && legacySpriteVolumeRenderer != null
            && legacyViewModelRenderer != null;
    }

    private void EnsureSpriteVolumePresentation()
    {
        usingSpriteVolumeViewModel = true;
        defaultSpriteMapSet ??= legacySpriteVolumeRenderer.MapSet;

        RetroFpsController controller = GetComponent<RetroFpsController>();
        if (controller != null)
        {
            controller.SetViewModelPresentationEnabled(false);
        }

        runtimeRoot = new GameObject("RuntimeSpriteWeaponRoot").transform;
        runtimeRoot.SetParent(viewCamera.transform, false);

        spriteViewModelTransform = legacySpriteVolumeRenderer.transform;
        spriteViewModelTransform.SetParent(runtimeRoot, false);
        spriteViewModelTransform.gameObject.SetActive(true);

        EnsureSpriteMuzzleObjects();
    }

    private void ApplyPresentationMode()
    {
        if (usingSpriteVolumeViewModel)
        {
            if (legacyViewModelRenderer != null)
            {
                legacyViewModelRenderer.enabled = true;
            }

            if (legacySpriteVolumeRenderer != null)
            {
                legacySpriteVolumeRenderer.enabled = true;
            }

            return;
        }

        if (!disableLegacyViewModel)
        {
            return;
        }

        if (legacyViewModelRenderer != null)
        {
            legacyViewModelRenderer.enabled = false;
        }

        if (legacySpriteVolumeRenderer != null)
        {
            legacySpriteVolumeRenderer.enabled = false;
        }
    }

    private void ApplyCurrentSpriteView(bool forceLightRefresh)
    {
        WeaponRuntime weapon = CurrentWeapon;
        if (!usingSpriteVolumeViewModel || weapon == null || legacySpriteVolumeRenderer == null || spriteViewModelTransform == null)
        {
            return;
        }

        legacySpriteVolumeRenderer.MapSet = weapon.SpriteMapSet != null
            ? weapon.SpriteMapSet
            : defaultSpriteMapSet;
        legacySpriteVolumeRenderer.SetTintMultipliers(
            ResolveSpriteBaseTint(weapon),
            ResolveSpriteEmissiveTint(weapon));

        Vector2 visualSize = weapon.SpriteVisualSize;
        spriteViewModelTransform.localPosition = weapon.SpriteVisualLocalPosition;
        spriteViewModelTransform.localRotation = Quaternion.Euler(weapon.SpriteVisualLocalEuler);
        spriteViewModelTransform.localScale = new Vector3(
            Mathf.Max(0.01f, visualSize.x),
            Mathf.Max(0.01f, visualSize.y),
            1f);

        EnsureSpriteMuzzleObjects();
        if (spriteMuzzle != null)
        {
            spriteMuzzle.localPosition = weapon.SpriteMuzzleLocalPosition + weapon.SpriteMuzzleLocalOffset;
            spriteMuzzle.localRotation = Quaternion.identity;
            weapon.Muzzle = spriteMuzzle;
        }

        if (spriteMuzzleFlash != null)
        {
            spriteMuzzleFlash.SetActive(false);
            weapon.MuzzleFlash = spriteMuzzleFlash;
            ApplySpriteMuzzleFlashMaterial();
        }

        legacySpriteVolumeRenderer.ApplyNow(forceLightRefresh);
    }

    private void EnsureSpriteMuzzleObjects()
    {
        if (runtimeRoot == null)
        {
            return;
        }

        if (spriteMuzzle == null)
        {
            spriteMuzzle = new GameObject("SpriteMuzzle").transform;
            spriteMuzzle.SetParent(runtimeRoot, false);
        }

        if (spriteMuzzleFlash != null)
        {
            return;
        }

        spriteMuzzleFlash = GameObject.CreatePrimitive(PrimitiveType.Quad);
        spriteMuzzleFlash.name = "SpriteMuzzleFlash";
        spriteMuzzleFlash.transform.SetParent(spriteMuzzle, false);
        spriteMuzzleFlash.transform.localPosition = Vector3.zero;
        spriteMuzzleFlash.transform.localRotation = Quaternion.identity;
        spriteMuzzleFlash.transform.localScale = Vector3.one * 0.1f;

        Collider flashCollider = spriteMuzzleFlash.GetComponent<Collider>();
        if (flashCollider != null)
        {
            flashCollider.enabled = false;
            Destroy(flashCollider);
        }

        Renderer flashRenderer = spriteMuzzleFlash.GetComponent<Renderer>();
        if (flashRenderer != null)
        {
            flashRenderer.shadowCastingMode = ShadowCastingMode.Off;
            flashRenderer.receiveShadows = false;
        }

        spriteMuzzleFlash.SetActive(false);
    }

    private void ApplySpriteMuzzleFlashMaterial()
    {
        if (spriteMuzzleFlash == null)
        {
            return;
        }

        Renderer flashRenderer = spriteMuzzleFlash.GetComponent<Renderer>();
        Sprite flashSprite = ResolveMuzzleFlashSprite(currentWeaponIndex);
        if (flashRenderer == null || flashSprite == null || flashSprite.texture == null)
        {
            if (flashRenderer != null)
            {
                flashRenderer.enabled = false;
            }

            return;
        }

        if (spriteMuzzleFlashMaterial == null || spriteMuzzleFlashTexture != flashSprite.texture)
        {
            DestroyRuntimeMaterial(spriteMuzzleFlashMaterial);
            spriteMuzzleFlashMaterial = CreateTransparentTextureMaterial("Sprite Muzzle Flash", flashSprite.texture, Color.white, additive: true);
            spriteMuzzleFlashTexture = flashSprite.texture;
        }

        flashRenderer.enabled = true;
        flashRenderer.sharedMaterial = spriteMuzzleFlashMaterial;
    }

    private WeaponSpriteView ResolveWeaponSpriteView(int weaponIndex)
    {
        if (weaponSpriteViews == null || weaponIndex < 0 || weaponIndex >= weaponSpriteViews.Length)
        {
            return null;
        }

        return weaponSpriteViews[weaponIndex];
    }

    private static Vector3 ResolveSpriteVisualLocalPosition(int weaponIndex, WeaponSpriteView spriteView)
    {
        if (spriteView != null)
        {
            return spriteView.visualLocalPosition;
        }

        return weaponIndex switch
        {
            0 => new Vector3(-0.02f, -0.02f, 0f),
            1 => new Vector3(0.02f, -0.05f, 0.04f),
            2 => new Vector3(0.01f, -0.06f, 0.06f),
            3 => new Vector3(0.02f, -0.055f, 0.06f),
            _ => Vector3.zero
        };
    }

    private static Vector3 ResolveSpriteVisualLocalEuler(int weaponIndex, WeaponSpriteView spriteView)
    {
        if (spriteView != null)
        {
            return spriteView.visualLocalEuler;
        }

        return weaponIndex switch
        {
            0 => Vector3.zero,
            1 => new Vector3(0f, 0f, -1.5f),
            2 => new Vector3(0f, 0f, -2f),
            3 => new Vector3(0f, 0f, -1f),
            _ => Vector3.zero
        };
    }

    private static Vector2 ResolveSpriteVisualSize(int weaponIndex, WeaponSpriteView spriteView)
    {
        if (spriteView != null)
        {
            return spriteView.visualSize;
        }

        return weaponIndex switch
        {
            0 => new Vector2(0.95f, 0.72f),
            1 => new Vector2(1.35f, 0.78f),
            2 => new Vector2(1.45f, 0.82f),
            3 => new Vector2(1.25f, 0.86f),
            _ => new Vector2(1.2f, 0.9f)
        };
    }

    private static Vector3 ResolveSpriteMuzzleLocalPosition(int weaponIndex, WeaponSpriteView spriteView)
    {
        if (spriteView != null)
        {
            return spriteView.muzzleLocalPosition;
        }

        return weaponIndex switch
        {
            0 => new Vector3(0f, 0.02f, 0.34f),
            1 => new Vector3(0f, -0.02f, 0.78f),
            2 => new Vector3(0f, -0.01f, 0.72f),
            3 => new Vector3(0f, 0f, 0.75f),
            _ => new Vector3(0f, 0f, 0.55f)
        };
    }

    private Vector3 ResolveSerializedSpriteMuzzleLocalOffset(int weaponIndex)
    {
        if (spriteMuzzleLocalOffsets != null
            && weaponIndex >= 0
            && weaponIndex < spriteMuzzleLocalOffsets.Length)
        {
            return spriteMuzzleLocalOffsets[weaponIndex];
        }

        return weaponIndex >= 0 ? new Vector3(0.055f, 0f, 0f) : Vector3.zero;
    }

    private Sprite ResolveMuzzleFlashSprite(int weaponIndex)
    {
        if (weapons != null
            && weaponIndex >= 0
            && weaponIndex < weapons.Length
            && weapons[weaponIndex]?.MuzzleFlashSprite != null)
        {
            return weapons[weaponIndex].MuzzleFlashSprite;
        }

        return ResolveSerializedMuzzleFlashSprite(weaponIndex);
    }

    private Sprite ResolveSerializedMuzzleFlashSprite(int weaponIndex)
    {
        if (muzzleFlashSprites == null || weaponIndex < 0 || weaponIndex >= muzzleFlashSprites.Length)
        {
            return null;
        }

        return muzzleFlashSprites[weaponIndex];
    }

    private Vector2 ResolveMuzzleFlashSpriteSize(int weaponIndex)
    {
        if (weapons != null
            && weaponIndex >= 0
            && weaponIndex < weapons.Length
            && weapons[weaponIndex] != null
            && weapons[weaponIndex].MuzzleFlashSpriteSize.x > 0f
            && weapons[weaponIndex].MuzzleFlashSpriteSize.y > 0f)
        {
            return weapons[weaponIndex].MuzzleFlashSpriteSize;
        }

        return ResolveSerializedMuzzleFlashSpriteSize(weaponIndex);
    }

    private Vector2 ResolveSerializedMuzzleFlashSpriteSize(int weaponIndex)
    {
        if (muzzleFlashSpriteSizes != null
            && weaponIndex >= 0
            && weaponIndex < muzzleFlashSpriteSizes.Length
            && muzzleFlashSpriteSizes[weaponIndex].x > 0f
            && muzzleFlashSpriteSizes[weaponIndex].y > 0f)
        {
            return muzzleFlashSpriteSizes[weaponIndex];
        }

        return weaponIndex switch
        {
            0 => new Vector2(0.38f, 0.28f),
            1 => new Vector2(0.62f, 0.24f),
            2 => new Vector2(0.82f, 0.48f),
            3 => new Vector2(0.78f, 0.56f),
            _ => new Vector2(0.46f, 0.3f)
        };
    }

    private static Color ResolveSpriteBaseTint(WeaponRuntime weapon)
    {
        if (weapon == null)
        {
            return Color.white;
        }

        return weapon.SpriteWeaponBodyTint > 0f
            ? weapon.SpriteBaseTintMultiplier * Color.Lerp(Color.white, weapon.BodyColor, weapon.SpriteWeaponBodyTint)
            : weapon.SpriteBaseTintMultiplier;
    }

    private static Color ResolveSpriteEmissiveTint(WeaponRuntime weapon)
    {
        if (weapon == null)
        {
            return Color.white;
        }

        return weapon.SpriteWeaponAccentTint > 0f
            ? weapon.SpriteEmissiveTintMultiplier * Color.Lerp(Color.white, weapon.AccentColor, weapon.SpriteWeaponAccentTint)
            : weapon.SpriteEmissiveTintMultiplier;
    }

    private static Color ResolveSpriteBaseTint(WeaponRuntime weapon, WeaponSpriteView spriteView)
    {
        Color tint = spriteView != null ? spriteView.baseTintMultiplier : Color.white;
        float weaponTint = spriteView != null ? spriteView.weaponBodyTint : 0.18f;
        return weapon != null && weaponTint > 0f
            ? tint * Color.Lerp(Color.white, weapon.BodyColor, weaponTint)
            : tint;
    }

    private static Color ResolveSpriteEmissiveTint(WeaponRuntime weapon, WeaponSpriteView spriteView)
    {
        Color tint = spriteView != null ? spriteView.emissiveTintMultiplier : Color.white;
        float weaponTint = spriteView != null ? spriteView.weaponAccentTint : 0.7f;
        return weapon != null && weaponTint > 0f
            ? tint * Color.Lerp(Color.white, weapon.AccentColor, weaponTint)
            : tint;
    }

    private void UpdateLookSample()
    {
        if (lookAction == null)
        {
            lookSample = Vector2.zero;
            return;
        }

        lookSample = lookAction.ReadValue<Vector2>();
        if (lookSample.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        InputDevice device = lookAction.activeControl != null ? lookAction.activeControl.device : null;
        if (device is Gamepad || device is Joystick)
        {
            usingGamepadLook = true;
        }
        else if (device is Pointer)
        {
            usingGamepadLook = false;
        }
    }

    private void HandleWeaponSelectionInput()
    {
        if (HandleDirectSelectionInput())
        {
            return;
        }

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (scroll > 0.2f)
            {
                CycleWeapon(-1);
                return;
            }

            if (scroll < -0.2f)
            {
                CycleWeapon(1);
                return;
            }
        }

        if (previousWeaponAction != null && previousWeaponAction.WasPressedThisFrame())
        {
            CycleWeapon(-1);
        }
        else if (nextWeaponAction != null && nextWeaponAction.WasPressedThisFrame())
        {
            CycleWeapon(1);
        }
    }

    private bool HandleDirectSelectionInput()
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        return TrySelectWeaponByKey(0, Keyboard.current.digit1Key.wasPressedThisFrame)
            || TrySelectWeaponByKey(1, Keyboard.current.digit2Key.wasPressedThisFrame)
            || TrySelectWeaponByKey(2, Keyboard.current.digit3Key.wasPressedThisFrame)
            || TrySelectWeaponByKey(3, Keyboard.current.digit4Key.wasPressedThisFrame)
            || TrySelectWeaponByKey(4, Keyboard.current.digit5Key.wasPressedThisFrame)
            || TrySelectWeaponByKey(5, Keyboard.current.digit6Key.wasPressedThisFrame)
            || TrySelectWeaponByKey(6, Keyboard.current.digit7Key.wasPressedThisFrame)
            || TrySelectWeaponByKey(7, Keyboard.current.digit8Key.wasPressedThisFrame)
            || TrySelectWeaponByKey(8, Keyboard.current.digit9Key.wasPressedThisFrame);
    }

    private bool TrySelectWeaponByKey(int weaponIndex, bool pressedThisFrame)
    {
        if (!pressedThisFrame || weapons == null || weaponIndex >= weapons.Length)
        {
            return false;
        }

        SelectWeapon(weaponIndex);
        return true;
    }

    private void CycleWeapon(int direction)
    {
        if (weapons == null || weapons.Length == 0)
        {
            return;
        }

        int nextIndex = (currentWeaponIndex + direction + weapons.Length) % weapons.Length;
        SelectWeapon(nextIndex);
    }

    private void SelectWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= weapons.Length || weaponIndex == currentWeaponIndex)
        {
            return;
        }

        isReloading = false;
        currentWeaponIndex = weaponIndex;
        switchEndTime = Time.time + weaponSwitchDuration;
        nextFireTime = Mathf.Max(nextFireTime, switchEndTime);

        for (int i = 0; i < weapons.Length; i++)
        {
            SetWeaponActive(i, i == currentWeaponIndex);
        }

        ApplyCurrentSpriteView(forceLightRefresh: true);
        WeaponSelected?.Invoke(CurrentWeapon != null ? CurrentWeapon.Definition : null);
        PublishWeaponSelected(CurrentWeapon);
    }

    private void HandleReloadInput()
    {
        if (CurrentWeapon == null || isReloading)
        {
            return;
        }

        bool reloadPressed = false;
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            reloadPressed = true;
        }

        if (!reloadPressed && Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame)
        {
            reloadPressed = true;
        }

        if (reloadPressed)
        {
            TryStartReload();
        }
    }

    private bool TryStartReload()
    {
        WeaponRuntime weapon = CurrentWeapon;
        if (weapon == null || isReloading)
        {
            return false;
        }

        if (weapon.AmmoInMagazine >= weapon.MagazineSize || weapon.ReserveAmmo <= 0)
        {
            return false;
        }

        isReloading = true;
        reloadEndTime = Time.time + weapon.ReloadDuration;
        nextFireTime = Mathf.Max(nextFireTime, reloadEndTime);
        ReloadStarted?.Invoke(weapon.Definition);
        PublishReloadEvent(weapon, RetroWeaponReloadStage.Started);
        return true;
    }

    private void CompleteReloadIfNeeded()
    {
        if (!isReloading || Time.time < reloadEndTime)
        {
            return;
        }

        WeaponRuntime weapon = CurrentWeapon;
        if (weapon == null)
        {
            isReloading = false;
            return;
        }

        int neededAmmo = weapon.MagazineSize - weapon.AmmoInMagazine;
        int ammoToLoad = Mathf.Min(neededAmmo, weapon.ReserveAmmo);
        weapon.AmmoInMagazine += ammoToLoad;
        weapon.ReserveAmmo -= ammoToLoad;
        isReloading = false;
        ReloadCompleted?.Invoke(weapon.Definition);
        PublishReloadEvent(weapon, RetroWeaponReloadStage.Completed);
    }

    private void HandleFireInput()
    {
        WeaponRuntime weapon = CurrentWeapon;
        if (weapon == null || Time.time < nextFireTime || isReloading)
        {
            return;
        }

        bool firePressedThisFrame = WasFirePressedThisFrame();
        bool fireHeld = IsFireHeld();
        bool wantsFire = weapon.FireMode == RetroFireMode.Automatic ? fireHeld : firePressedThisFrame;
        if (!wantsFire)
        {
            return;
        }

        if (weapon.AmmoInMagazine <= 0)
        {
            HandleEmptyWeaponFire(weapon);
            return;
        }

        FireCurrentWeapon();
    }

    private void HandleEmptyWeaponFire(WeaponRuntime weapon)
    {
        if (weapon == null)
        {
            return;
        }

        if (weapon.AutoReloadWhenEmpty && TryStartReload())
        {
            return;
        }

        nextFireTime = Time.time + Mathf.Max(0.01f, weapon.DryFireCooldown);
        ApplyWeaponKick(weapon.DryFireKickPosition, weapon.DryFireKickEuler);
        WeaponDryFired?.Invoke(weapon.Definition);
        PublishWeaponDryFired(weapon);
    }

    private void FireCurrentWeapon()
    {
        WeaponRuntime weapon = CurrentWeapon;
        if (weapon == null || viewCamera == null)
        {
            return;
        }

        if (!TryConsumeAmmo(weapon))
        {
            HandleEmptyWeaponFire(weapon);
            return;
        }

        nextFireTime = Time.time + weapon.FireInterval;

        switch (weapon.Kind)
        {
            case RetroWeaponKind.Hitscan:
                FireHitscanWeapon(weapon);
                break;
            case RetroWeaponKind.GrenadeLauncher:
                FireGrenadeLauncher(weapon);
                break;
        }

        AddSpreadBloom(weapon);
        ApplyWeaponKick(weapon.RecoilPosition, weapon.RecoilEuler);
        ShowMuzzleFlash(weapon);
        WeaponFired?.Invoke(weapon.Definition);
        PublishWeaponFired(weapon);
    }

    private void PublishWeaponSelected(WeaponRuntime weapon)
    {
        if (weapon == null)
        {
            return;
        }

        RetroGameContext.Events.Publish(new RetroWeaponSelectedEvent(gameObject, weapon.Definition, weapon.Name, currentWeaponIndex));
    }

    private void PublishWeaponFired(WeaponRuntime weapon)
    {
        if (weapon == null)
        {
            return;
        }

        Vector3 position = weapon.Muzzle != null
            ? weapon.Muzzle.position
            : viewCamera != null ? viewCamera.transform.position : transform.position;
        Vector3 direction = viewCamera != null ? viewCamera.transform.forward : transform.forward;
        RetroGameContext.Events.Publish(new RetroWeaponFiredEvent(gameObject, weapon.Definition, weapon.Name, position, direction));
    }

    private void PublishWeaponDryFired(WeaponRuntime weapon)
    {
        if (weapon == null)
        {
            return;
        }

        RetroGameContext.Events.Publish(new RetroWeaponDryFiredEvent(gameObject, weapon.Definition, weapon.Name));
    }

    private void PublishReloadEvent(WeaponRuntime weapon, RetroWeaponReloadStage stage)
    {
        if (weapon == null)
        {
            return;
        }

        RetroGameContext.Events.Publish(new RetroWeaponReloadEvent(gameObject, weapon.Definition, weapon.Name, stage));
    }

    private static bool TryConsumeAmmo(WeaponRuntime weapon)
    {
        if (weapon == null || weapon.AmmoInMagazine <= 0)
        {
            return false;
        }

        weapon.AmmoInMagazine = Mathf.Max(0, weapon.AmmoInMagazine - 1);
        return true;
    }

    private void ApplyWeaponKick(Vector3 positionKick, Vector3 eulerKick)
    {
        recoilPositionOffset += positionKick;
        recoilEulerOffset += new Vector3(
            eulerKick.x,
            UnityEngine.Random.Range(-eulerKick.y, eulerKick.y),
            UnityEngine.Random.Range(-eulerKick.z, eulerKick.z));
    }

    private void FireHitscanWeapon(WeaponRuntime weapon)
    {
        Vector3 origin = viewCamera.transform.position;
        int maxTrailSegments = Mathf.Min(weapon.Pellets, Mathf.Max(0, weapon.BulletTrailMaxSegmentsPerShot));
        float spreadAngle = ResolveCurrentSpreadAngle(weapon);
        for (int pelletIndex = 0; pelletIndex < weapon.Pellets; pelletIndex++)
        {
            Vector3 shotDirection = GetSpreadDirection(spreadAngle);
            Vector3 trailEnd = origin + shotDirection * weapon.Range;
            bool hitSomething = Physics.Raycast(origin, shotDirection, out RaycastHit hit, weapon.Range, hitMask, QueryTriggerInteraction.Ignore);
            if (hitSomething)
            {
                trailEnd = hit.point;
                ApplyDamage(hit.collider, weapon.Damage, hit.point, hit.normal);
                if (hit.rigidbody != null)
                {
                    hit.rigidbody.AddForceAtPosition(shotDirection * weapon.ImpactForce, hit.point, ForceMode.Impulse);
                }

                SpawnImpactFlash(hit.point, hit.normal, weapon.AccentColor);
            }

            if (pelletIndex < maxTrailSegments)
            {
                SpawnBulletTrail(weapon, ResolveBulletTrailStart(weapon, shotDirection), trailEnd, shotDirection);
            }
        }
    }

    private void FireGrenadeLauncher(WeaponRuntime weapon)
    {
        Vector3 origin = weapon.Muzzle != null ? weapon.Muzzle.position : viewCamera.transform.position + viewCamera.transform.forward * 0.6f;
        Vector3 launchVelocity = GetSpreadDirection(ResolveCurrentSpreadAngle(weapon)) * weapon.ProjectileSpeed + CurrentPlanarVelocity * 0.35f;
        EnsureGrenadeProjectilePool();
        RetroGrenadeProjectile projectile = grenadeProjectilePool?.Rent(origin, Quaternion.identity);
        if (projectile == null)
        {
            return;
        }

        projectile.ConfigureVisual($"{weapon.Name} Projectile", weapon.AccentColor, 0.12f);

        Vector3 launchDirection = launchVelocity.sqrMagnitude > 0.0001f ? launchVelocity.normalized : viewCamera.transform.forward;
        SpawnBulletTrail(
            weapon,
            ResolveBulletTrailStart(weapon, launchDirection),
            origin + launchDirection * Mathf.Max(1.5f, weapon.ProjectileSpeed * 0.12f),
            launchDirection);

        projectile.Initialize(
            owner: gameObject,
            velocity: launchVelocity,
            damage: weapon.Damage,
            explosionRadius: weapon.ExplosionRadius,
            explosionForce: weapon.ExplosionForce,
            fuseTime: weapon.FuseTime,
            impactDamage: weapon.Damage * 0.25f,
            collisionMask: hitMask,
            effectColor: weapon.AccentColor);
    }

    private void EnsureGrenadeProjectilePool()
    {
        if (grenadeProjectilePool != null && grenadeProjectilePool.IsValid)
        {
            return;
        }

        grenadeProjectilePool = RetroPoolService.Shared.GetOrCreateComponentPool(
            "RetroWeaponSystem.GrenadeProjectile",
            CreateGrenadeProjectile,
            new RetroPoolSettings(prewarmCount: 4, maxInactiveCount: 24));
    }

    private RetroGrenadeProjectile CreateGrenadeProjectile(Transform parent)
    {
        GameObject grenade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        grenade.name = "Grenade Projectile";
        grenade.transform.SetParent(parent, false);
        grenade.layer = gameObject.layer;

        Rigidbody grenadeBody = grenade.GetComponent<Rigidbody>();
        if (grenadeBody == null)
        {
            grenadeBody = grenade.AddComponent<Rigidbody>();
        }

        grenadeBody.useGravity = true;
        grenadeBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        grenadeBody.interpolation = RigidbodyInterpolation.Interpolate;

        Renderer renderer = grenade.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        RetroGrenadeProjectile projectile = grenade.GetComponent<RetroGrenadeProjectile>();
        if (projectile == null)
        {
            projectile = grenade.AddComponent<RetroGrenadeProjectile>();
        }

        grenade.SetActive(false);
        return projectile;
    }

    private void UpdateViewmodelPresentation(float deltaTime)
    {
        if (runtimeRoot == null || CurrentWeapon == null)
        {
            return;
        }

        float planarSpeed = CurrentPlanarVelocity.magnitude;
        if (planarSpeed > 0.15f)
        {
            bobTime += deltaTime * bobFrequency * Mathf.Lerp(0.65f, 1.4f, Mathf.Clamp01(planarSpeed / 9f));
        }

        float bobAlpha = Mathf.Clamp01(planarSpeed / 8f);
        Vector3 bobOffset = new Vector3(
            Mathf.Sin(bobTime) * bobHorizontalAmplitude,
            Mathf.Abs(Mathf.Cos(bobTime * 0.5f)) * bobVerticalAmplitude,
            0f) * bobAlpha;

        float swayScale = usingGamepadLook ? gamepadSwayScale * deltaTime : mouseSwayScale;
        Vector2 swayInput = Vector2.ClampMagnitude(lookSample * swayScale, 1f);
        Vector3 swayOffset = new Vector3(-swayInput.x, -swayInput.y, 0f) * swayPositionAmount;
        Vector3 swayEuler = new Vector3(-swayInput.y, swayInput.x, -swayInput.x * 0.7f) * swayRotationAmount;

        recoilPositionOffset = Vector3.Lerp(recoilPositionOffset, Vector3.zero, 1f - Mathf.Exp(-recoilReturnSpeed * deltaTime));
        recoilEulerOffset = Vector3.Lerp(recoilEulerOffset, Vector3.zero, 1f - Mathf.Exp(-recoilReturnSpeed * deltaTime));

        Vector3 reloadOffset = Vector3.zero;
        Vector3 reloadEuler = Vector3.zero;
        if (isReloading && CurrentWeapon.ReloadDuration > 0.001f)
        {
            float reloadT = 1f - Mathf.Clamp01((reloadEndTime - Time.time) / CurrentWeapon.ReloadDuration);
            float reloadPose = Mathf.Sin(reloadT * Mathf.PI);
            reloadOffset = new Vector3(0f, -reloadDrop * reloadPose, -reloadBack * reloadPose);
            reloadEuler = new Vector3(reloadTilt * reloadPose, 0f, reloadTilt * 0.45f * reloadPose);
        }

        Vector3 switchOffset = Vector3.zero;
        Vector3 switchEuler = Vector3.zero;
        if (Time.time < switchEndTime && weaponSwitchDuration > 0.001f)
        {
            float switchT = 1f - Mathf.Clamp01((switchEndTime - Time.time) / weaponSwitchDuration);
            float switchPose = Mathf.Sin(switchT * Mathf.PI);
            switchOffset = new Vector3(0f, -switchDrop * switchPose, 0f);
            switchEuler = new Vector3(0f, switchYaw * (1f - switchPose), switchYaw * 0.35f * switchPose);
        }

        runtimeRoot.localPosition = baseLocalPosition
            + CurrentWeapon.LocalPosition
            + bobOffset
            + swayOffset
            + recoilPositionOffset
            + reloadOffset
            + switchOffset;
        runtimeRoot.localRotation = Quaternion.Euler(
            baseLocalEuler
            + CurrentWeapon.LocalEuler
            + swayEuler
            + recoilEulerOffset
            + reloadEuler
            + switchEuler);
    }

    private void UpdateMuzzleFlashState()
    {
        if (Time.time < muzzleFlashDisableTime)
        {
            return;
        }

        WeaponRuntime weapon = CurrentWeapon;
        if (weapon != null && weapon.MuzzleFlash != null)
        {
            weapon.MuzzleFlash.SetActive(false);
        }
    }

    private void ShowMuzzleFlash(WeaponRuntime weapon)
    {
        if (weapon == null || weapon.MuzzleFlash == null)
        {
            return;
        }

        float flashScale = weapon.MuzzleFlashScale * UnityEngine.Random.Range(0.9f, 1.15f);
        if (usingSpriteVolumeViewModel)
        {
            if (ResolveMuzzleFlashSprite(currentWeaponIndex) == null)
            {
                return;
            }

            ApplySpriteMuzzleFlashMaterial();
            Vector2 flashSize = ResolveMuzzleFlashSpriteSize(currentWeaponIndex) * UnityEngine.Random.Range(0.9f, 1.15f);
            weapon.MuzzleFlash.transform.localScale = new Vector3(flashSize.x, flashSize.y, 1f);
            weapon.MuzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            muzzleFlashDisableTime = Time.time + spriteMuzzleFlashDuration;
        }
        else
        {
            weapon.MuzzleFlash.transform.localScale = Vector3.one * flashScale;
            muzzleFlashDisableTime = Time.time + 0.045f;
        }

        weapon.MuzzleFlash.SetActive(true);
    }

    private Vector3 GetSpreadDirection(float spreadAngle)
    {
        if (viewCamera == null || spreadAngle <= 0.001f)
        {
            return viewCamera != null ? viewCamera.transform.forward : transform.forward;
        }

        Vector2 cone = UnityEngine.Random.insideUnitCircle * Mathf.Tan(spreadAngle * Mathf.Deg2Rad);
        Vector3 direction = viewCamera.transform.forward
            + viewCamera.transform.right * cone.x
            + viewCamera.transform.up * cone.y;
        return direction.normalized;
    }

    private void RecoverWeaponSpread(float deltaTime)
    {
        if (weapons == null || deltaTime <= 0f)
        {
            return;
        }

        for (int i = 0; i < weapons.Length; i++)
        {
            WeaponRuntime weapon = weapons[i];
            if (weapon == null || weapon.CurrentSpreadBloom <= 0f)
            {
                continue;
            }

            float recovery = Mathf.Max(0f, weapon.SpreadRecoverySpeed) * deltaTime;
            weapon.CurrentSpreadBloom = Mathf.Max(0f, weapon.CurrentSpreadBloom - recovery);
        }
    }

    private void AddSpreadBloom(WeaponRuntime weapon)
    {
        if (weapon == null || weapon.SpreadBloomPerShot <= 0f)
        {
            return;
        }

        float maxBloom = Mathf.Max(0f, weapon.MaxSpreadAngle - weapon.SpreadAngle);
        weapon.CurrentSpreadBloom = Mathf.Min(maxBloom, weapon.CurrentSpreadBloom + weapon.SpreadBloomPerShot);
    }

    private float ResolveCurrentSpreadAngle(WeaponRuntime weapon)
    {
        if (weapon == null)
        {
            return 0f;
        }

        float movementSpread = weapon.MovementSpreadPenalty * Mathf.Clamp01(CurrentPlanarVelocity.magnitude / 9f);
        float maxSpread = Mathf.Max(weapon.SpreadAngle, weapon.MaxSpreadAngle);
        return Mathf.Clamp(weapon.SpreadAngle + weapon.CurrentSpreadBloom + movementSpread, 0f, maxSpread);
    }

    private Vector3 ResolveBulletTrailStart(WeaponRuntime weapon, Vector3 shotDirection)
    {
        Vector3 start = weapon != null && weapon.Muzzle != null
            ? weapon.Muzzle.position
            : viewCamera.transform.position + viewCamera.transform.forward * 0.35f;

        Vector3 safeDirection = shotDirection.sqrMagnitude > 0.0001f ? shotDirection.normalized : viewCamera.transform.forward;
        return start + safeDirection * Mathf.Max(0f, weapon != null ? weapon.BulletTrailStartOffset : 0f);
    }

    private void SpawnBulletTrail(WeaponRuntime weapon, Vector3 start, Vector3 end, Vector3 shotDirection)
    {
        if (weapon == null || !weapon.BulletTrailEnabled || weapon.BulletTrailDuration <= 0f || weapon.BulletTrailWidth <= 0f)
        {
            return;
        }

        Vector3 safeDirection = shotDirection.sqrMagnitude > 0.0001f ? shotDirection.normalized : (end - start).normalized;
        if (safeDirection.sqrMagnitude < 0.0001f)
        {
            safeDirection = viewCamera != null ? viewCamera.transform.forward : transform.forward;
        }

        Vector3 adjustedEnd = end - safeDirection * weapon.BulletTrailEndOffset;
        if ((adjustedEnd - start).sqrMagnitude < 0.04f)
        {
            return;
        }

        RetroGameContext.Vfx.SpawnBulletTrail(
            weapon.Name,
            start,
            adjustedEnd,
            weapon.BulletTrailColor,
            weapon.BulletTrailWidth,
            weapon.BulletTrailDuration);
    }

    private void ApplyDamage(Collider targetCollider, float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (targetCollider == null || damage <= 0f)
        {
            return;
        }

        RetroDamageable damageable = targetCollider.GetComponentInParent<RetroDamageable>();
        if (damageable != null)
        {
            damageable.ApplyDamage(damage, hitPoint, hitNormal, gameObject);
            return;
        }

        targetCollider.SendMessageUpwards("ApplyDamage", damage, SendMessageOptions.DontRequireReceiver);
    }

    private void SpawnImpactFlash(Vector3 position, Vector3 normal, Color color)
    {
        RetroGameContext.Vfx.SpawnImpactFlash(position, normal, color, 0.06f, 0.08f);
    }

    private void BuildWeaponVisual(int index, WeaponRuntime weapon)
    {
        if (runtimeRoot == null || weapon == null)
        {
            return;
        }

        Material bodyMaterial = CreateRuntimeMaterial($"{weapon.Name} Body", weapon.BodyColor, 0.2f, 0.52f, false);
        Material accentMaterial = CreateRuntimeMaterial($"{weapon.Name} Accent", weapon.AccentColor, 0.1f, 0.7f, false);
        Material flashMaterial = CreateRuntimeMaterial($"{weapon.Name} Flash", weapon.AccentColor * 1.35f, 0f, 0.1f, true);

        GameObject root = new GameObject(weapon.Name);
        root.transform.SetParent(runtimeRoot, false);
        weapon.Visual = root;

        switch (index)
        {
            case 0:
                CreateWeaponPart(root.transform, PrimitiveType.Cube, new Vector3(0f, -0.01f, 0.13f), new Vector3(0.12f, 0.08f, 0.26f), Vector3.zero, bodyMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.14f), new Vector3(0.1f, 0.05f, 0.18f), Vector3.zero, accentMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cube, new Vector3(-0.02f, -0.11f, 0.04f), new Vector3(0.07f, 0.16f, 0.08f), new Vector3(18f, 0f, -8f), bodyMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0.26f), new Vector3(0.025f, 0.05f, 0.025f), new Vector3(90f, 0f, 0f), accentMaterial);
                break;
            case 1:
                CreateWeaponPart(root.transform, PrimitiveType.Cube, new Vector3(0f, -0.03f, 0.26f), new Vector3(0.12f, 0.1f, 0.52f), Vector3.zero, bodyMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cube, new Vector3(-0.02f, -0.02f, -0.02f), new Vector3(0.08f, 0.12f, 0.24f), new Vector3(0f, 0f, -18f), bodyMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cube, new Vector3(0.01f, -0.14f, 0.18f), new Vector3(0.07f, 0.17f, 0.08f), new Vector3(16f, 0f, 0f), accentMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cube, new Vector3(0.01f, 0.03f, 0.28f), new Vector3(0.06f, 0.06f, 0.16f), Vector3.zero, accentMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, -0.02f, 0.58f), new Vector3(0.03f, 0.18f, 0.03f), new Vector3(90f, 0f, 0f), bodyMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cylinder, new Vector3(0.03f, 0.08f, 0.4f), new Vector3(0.035f, 0.12f, 0.035f), new Vector3(90f, 0f, 0f), accentMaterial);
                break;
            case 2:
                CreateWeaponPart(root.transform, PrimitiveType.Cube, new Vector3(0f, -0.03f, 0.2f), new Vector3(0.13f, 0.11f, 0.34f), Vector3.zero, bodyMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cube, new Vector3(-0.02f, -0.03f, -0.05f), new Vector3(0.08f, 0.13f, 0.24f), new Vector3(0f, 0f, -16f), bodyMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cube, new Vector3(0f, -0.11f, 0.18f), new Vector3(0.08f, 0.08f, 0.18f), Vector3.zero, accentMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cylinder, new Vector3(-0.015f, 0f, 0.48f), new Vector3(0.03f, 0.22f, 0.03f), new Vector3(90f, 0f, 0f), bodyMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cylinder, new Vector3(0.02f, -0.02f, 0.45f), new Vector3(0.025f, 0.18f, 0.025f), new Vector3(90f, 0f, 0f), accentMaterial);
                break;
            case 3:
                CreateWeaponPart(root.transform, PrimitiveType.Cube, new Vector3(0f, -0.02f, 0.16f), new Vector3(0.14f, 0.12f, 0.28f), Vector3.zero, bodyMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cube, new Vector3(-0.03f, -0.04f, -0.05f), new Vector3(0.08f, 0.13f, 0.22f), new Vector3(0f, 0f, -16f), bodyMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cube, new Vector3(0.03f, 0.06f, 0.14f), new Vector3(0.05f, 0.05f, 0.12f), Vector3.zero, accentMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.46f), new Vector3(0.055f, 0.24f, 0.055f), new Vector3(90f, 0f, 0f), accentMaterial);
                CreateWeaponPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, -0.1f, 0.2f), new Vector3(0.07f, 0.05f, 0.07f), Vector3.zero, bodyMaterial);
                break;
        }

        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.localPosition = index switch
        {
            0 => new Vector3(0f, 0.01f, 0.34f),
            1 => new Vector3(0f, -0.02f, 0.8f),
            2 => new Vector3(0f, -0.01f, 0.72f),
            _ => new Vector3(0f, 0f, 0.75f)
        };
        weapon.Muzzle = muzzle.transform;

        GameObject muzzleFlash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        muzzleFlash.name = "MuzzleFlash";
        muzzleFlash.transform.SetParent(muzzle.transform, false);
        muzzleFlash.transform.localPosition = Vector3.zero;
        muzzleFlash.transform.localScale = Vector3.one * weapon.MuzzleFlashScale;

        Collider flashCollider = muzzleFlash.GetComponent<Collider>();
        if (flashCollider != null)
        {
            flashCollider.enabled = false;
            Destroy(flashCollider);
        }

        Renderer flashRenderer = muzzleFlash.GetComponent<Renderer>();
        if (flashRenderer != null)
        {
            flashRenderer.sharedMaterial = flashMaterial;
            flashRenderer.shadowCastingMode = ShadowCastingMode.Off;
            flashRenderer.receiveShadows = false;
        }

        muzzleFlash.SetActive(false);
        weapon.MuzzleFlash = muzzleFlash;
    }

    private void CreateWeaponPart(
        Transform parent,
        PrimitiveType primitiveType,
        Vector3 localPosition,
        Vector3 localScale,
        Vector3 localEuler,
        Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        part.transform.localRotation = Quaternion.Euler(localEuler);
        part.layer = gameObject.layer;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void SetWeaponActive(int index, bool isActive)
    {
        if (index < 0 || index >= weapons.Length || weapons[index]?.Visual == null)
        {
            return;
        }

        weapons[index].Visual.SetActive(isActive);
    }

    private bool WasFirePressedThisFrame()
    {
        if (attackAction != null)
        {
            return attackAction.WasPressedThisFrame();
        }

        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    private bool IsFireHeld()
    {
        if (attackAction != null)
        {
            return attackAction.IsPressed();
        }

        return Mouse.current != null && Mouse.current.leftButton.isPressed;
    }

    private Vector3 CurrentPlanarVelocity
    {
        get
        {
            if (body == null)
            {
                return Vector3.zero;
            }

#if UNITY_6000_0_OR_NEWER
            return Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
#else
            return Vector3.ProjectOnPlane(body.velocity, Vector3.up);
#endif
        }
    }

    private static Transform FindNamedChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nestedMatch = FindNamedChildRecursive(child, childName);
            if (nestedMatch != null)
            {
                return nestedMatch;
            }
        }

        return null;
    }

    private static Material CreateRuntimeMaterial(string materialName, Color baseColor, float metallic, float smoothness, bool emissive)
    {
        Shader shader = Shader.Find("HDRP/Lit");
        shader ??= Shader.Find("Standard");
        shader ??= Shader.Find("Diffuse");

        Material material = new Material(shader)
        {
            name = materialName
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", baseColor);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", baseColor);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", metallic);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness);
        }

        if (emissive)
        {
            Color emission = baseColor * 3.5f;
            if (material.HasProperty("_EmissiveColor"))
            {
                material.SetColor("_EmissiveColor", emission);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
        }

        return material;
    }

    private static Material CreateTransparentTextureMaterial(string materialName, Texture texture, Color tint, bool additive)
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        shader ??= Shader.Find("Unlit/Transparent");
        shader ??= Shader.Find("Sprites/Default");

        Material material = new Material(shader)
        {
            name = materialName,
            renderQueue = (int)RenderQueue.Transparent
        };

        material.SetOverrideTag("RenderType", "Transparent");
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword(additive ? "_BLENDMODE_ADD" : "_BLENDMODE_ALPHA");

        SetMaterialTextureIfPresent(material, texture, "_UnlitColorMap", "_BaseColorMap", "_MainTex", "_BaseMap");
        SetMaterialColorIfPresent(material, tint, "_UnlitColor", "_BaseColor", "_Color");
        SetMaterialFloatIfPresent(material, 0f, "_AlphaRemapMin");
        SetMaterialFloatIfPresent(material, 1f, "_AlphaRemapMax");
        SetMaterialFloatIfPresent(material, 1f, "_SurfaceType");
        SetMaterialFloatIfPresent(material, additive ? 1f : 0f, "_BlendMode");
        SetMaterialFloatIfPresent(material, 0f, "_AlphaCutoffEnable", "_AlphaClip");
        SetMaterialFloatIfPresent(material, 0f, "_ZWrite", "_TransparentZWrite");
        SetMaterialFloatIfPresent(material, (float)CullMode.Off, "_CullMode", "_CullModeForward");
        SetMaterialFloatIfPresent(material, (float)BlendMode.SrcAlpha, "_SrcBlend");
        SetMaterialFloatIfPresent(material, additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha, "_DstBlend");
        SetMaterialFloatIfPresent(material, (float)BlendMode.One, "_AlphaSrcBlend");
        SetMaterialFloatIfPresent(material, (float)BlendMode.OneMinusSrcAlpha, "_AlphaDstBlend");

        return material;
    }

    private static void DestroyRuntimeMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(material);
        }
        else
        {
            DestroyImmediate(material);
        }
    }

    private static void SetMaterialTextureIfPresent(Material material, Texture texture, params string[] propertyNames)
    {
        if (material == null || texture == null)
        {
            return;
        }

        for (int i = 0; i < propertyNames.Length; i++)
        {
            if (material.HasProperty(propertyNames[i]))
            {
                material.SetTexture(propertyNames[i], texture);
            }
        }

    }

    private static void SetMaterialColorIfPresent(Material material, Color color, params string[] propertyNames)
    {
        if (material == null)
        {
            return;
        }

        for (int i = 0; i < propertyNames.Length; i++)
        {
            if (material.HasProperty(propertyNames[i]))
            {
                material.SetColor(propertyNames[i], color);
            }
        }
    }

    private static void SetMaterialFloatIfPresent(Material material, float value, params string[] propertyNames)
    {
        if (material == null)
        {
            return;
        }

        for (int i = 0; i < propertyNames.Length; i++)
        {
            if (material.HasProperty(propertyNames[i]))
            {
                material.SetFloat(propertyNames[i], value);
            }
        }
    }

    private static void ApplyRuntimeMaterialColor(Material material, Color baseColor, bool emissive)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", baseColor);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", baseColor);
        }

        if (!emissive)
        {
            return;
        }

        Color emission = baseColor * 3.5f;
        if (material.HasProperty("_EmissiveColor"))
        {
            material.SetColor("_EmissiveColor", emission);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }
    }

    private void OnGUI()
    {
        if (showCrosshair)
        {
            DrawCrosshair();
        }

        if (!showDebugHud || CurrentWeapon == null)
        {
            return;
        }

        GUI.color = Color.white;
        Rect panel = new Rect(16f, Screen.height - 108f, 420f, 92f);
        GUI.Box(panel, string.Empty);

        GUI.Label(new Rect(28f, Screen.height - 98f, 320f, 24f), $"Weapon: {CurrentWeapon.Name}");
        GUI.Label(new Rect(28f, Screen.height - 74f, 380f, 24f), $"Ammo: {CurrentWeapon.AmmoInMagazine} / {CurrentWeapon.ReserveAmmo}   Spread: {CurrentSpreadAngle:0.0}");
        GUI.Label(new Rect(28f, Screen.height - 50f, 390f, 24f), isReloading ? "Reloading..." : "LMB Fire  |  R Reload  |  Mouse Wheel / 1-4 Switch");
    }

    private void DrawCrosshair()
    {
        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        float gap = Mathf.Max(0f, crosshairGap);
        float length = Mathf.Max(1f, crosshairLength);
        float thickness = Mathf.Max(1f, crosshairThickness);
        float halfThickness = thickness * 0.5f;

        Color previousColor = GUI.color;
        GUI.color = crosshairColor;
        GUI.DrawTexture(new Rect(centerX - gap - length, centerY - halfThickness, length, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX + gap, centerY - halfThickness, length, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX - halfThickness, centerY - gap - length, thickness, length), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(centerX - halfThickness, centerY + gap, thickness, length), Texture2D.whiteTexture);
        GUI.color = previousColor;
    }
}
