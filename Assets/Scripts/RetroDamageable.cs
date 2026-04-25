using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public sealed class RetroDamageable : MonoBehaviour
{
    private const float BloodDecalProbeHeight = 1.2f;
    private const float BloodDecalProbeDistance = 5.5f;

    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private bool disableRenderersOnDeath = true;
    [SerializeField] private bool disableCollidersOnDeath = true;
    [SerializeField] private float destroyDelay = 0f;
    [Header("Hit Effects")]
    [SerializeField] private Sprite bloodSplatterSprite;
    [SerializeField] private Sprite bloodSpraySprite;
    [SerializeField] private Color bloodColor = new Color(0.58f, 0.02f, 0.015f, 0.95f);
    [SerializeField] private bool spawnBloodOnHit = true;
    [SerializeField] private bool spawnBloodOnDeath = true;
    [SerializeField] private Vector2 bloodSplatterScaleRange = new Vector2(0.55f, 0.95f);
    [SerializeField, Range(1, 8)] private int bloodDecalCount = 4;
    [SerializeField, Range(0, 24)] private int bloodSprayParticleCount = 12;
    [SerializeField] private Vector2 bloodSprayScaleRange = new Vector2(0.12f, 0.26f);
    [SerializeField] private Vector2 bloodSpraySpeedRange = new Vector2(1.4f, 4.2f);
    [SerializeField, Min(0f)] private float deathBloodScaleMultiplier = 2.2f;
    [SerializeField, Min(0f)] private float bloodSurfaceOffset = 0.018f;
    [SerializeField, Min(0f)] private float bloodDecalScatterRadius = 0.32f;
    [SerializeField, Min(0f)] private float bloodSplatterLifetime = 1.6f;
    [SerializeField, Min(0f)] private float bloodSprayLifetime = 0.55f;

    private float currentHealth;
    private bool initialized;
    private bool dead;
    private GameObject lastDamageSource;
    private static RetroComponentPool<BloodSpriteEffect> bloodEffectPool;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => dead;
    public GameObject LastDamageSource => lastDamageSource;

    public event Action<RetroDamageable, float, Vector3, Vector3> Damaged;
    public event Action<RetroDamageable> Died;

    private void Awake()
    {
        InitializeHealth();
    }

    private void OnEnable()
    {
        InitializeHealth();
        lastDamageSource = null;
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        bloodSplatterScaleRange.x = Mathf.Max(0.01f, bloodSplatterScaleRange.x);
        bloodSplatterScaleRange.y = Mathf.Max(bloodSplatterScaleRange.x, bloodSplatterScaleRange.y);
        bloodDecalCount = Mathf.Max(1, bloodDecalCount);
        bloodSprayParticleCount = Mathf.Max(0, bloodSprayParticleCount);
        bloodSprayScaleRange.x = Mathf.Max(0.01f, bloodSprayScaleRange.x);
        bloodSprayScaleRange.y = Mathf.Max(bloodSprayScaleRange.x, bloodSprayScaleRange.y);
        bloodSpraySpeedRange.x = Mathf.Max(0f, bloodSpraySpeedRange.x);
        bloodSpraySpeedRange.y = Mathf.Max(bloodSpraySpeedRange.x, bloodSpraySpeedRange.y);
        deathBloodScaleMultiplier = Mathf.Max(0f, deathBloodScaleMultiplier);
        bloodSurfaceOffset = Mathf.Max(0f, bloodSurfaceOffset);
        bloodDecalScatterRadius = Mathf.Max(0f, bloodDecalScatterRadius);
        bloodSplatterLifetime = Mathf.Max(0f, bloodSplatterLifetime);
        bloodSprayLifetime = Mathf.Max(0f, bloodSprayLifetime);
    }

    public void ApplyDamage(float damage)
    {
        Vector3 hitPoint = ResolveFallbackHitPoint();
        Vector3 hitNormal = (hitPoint - transform.position).sqrMagnitude > 0.0001f
            ? (hitPoint - transform.position).normalized
            : Vector3.up;
        ApplyDamage(damage, hitPoint, hitNormal, null);
    }

    public void ApplyDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        ApplyDamage(damage, hitPoint, hitNormal, null);
    }

    public void ApplyDamage(float damage, Vector3 hitPoint, Vector3 hitNormal, GameObject source)
    {
        if (dead || damage <= 0f)
        {
            return;
        }

        InitializeHealth();
        lastDamageSource = source;
        bool lethal = currentHealth - damage <= 0f;
        if ((lethal && spawnBloodOnDeath) || (!lethal && spawnBloodOnHit))
        {
            SpawnBloodSplatter(hitPoint, hitNormal, lethal ? deathBloodScaleMultiplier : 1f, lethal);
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        Damaged?.Invoke(this, damage, hitPoint, hitNormal);
        RetroGameContext.Events.Publish(new RetroDamageEvent(source, this, damage, hitPoint, hitNormal, lethal));
        if (currentHealth <= 0f)
        {
            HandleDeath(source);
        }
    }

    public float Heal(float amount, GameObject source = null)
    {
        if (dead || amount <= 0f)
        {
            return 0f;
        }

        InitializeHealth();
        float previousHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        float healed = currentHealth - previousHealth;
        if (healed > 0f)
        {
            RetroGameContext.Events.Publish(new RetroHealEvent(source, this, healed, currentHealth));
        }

        return healed;
    }

    public float RestoreHealth(GameObject source = null)
    {
        return Heal(maxHealth, source);
    }

    private void InitializeHealth()
    {
        if (initialized)
        {
            return;
        }

        currentHealth = maxHealth;
        initialized = true;
    }

    private Vector3 ResolveFallbackHitPoint()
    {
        Collider targetCollider = GetComponentInChildren<Collider>();
        if (targetCollider != null)
        {
            return targetCollider.bounds.center;
        }

        Renderer targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
        {
            return targetRenderer.bounds.center;
        }

        return transform.position;
    }

    private void SpawnBloodSplatter(Vector3 hitPoint, Vector3 hitNormal, float scaleMultiplier, bool deathBurst)
    {
        if ((bloodSplatterSprite == null && bloodSpraySprite == null) || scaleMultiplier <= 0f)
        {
            return;
        }

        Vector3 safeNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.up;
        BuildSurfaceBasis(safeNormal, out Vector3 tangent, out Vector3 bitangent);

        if (bloodSplatterSprite != null)
        {
            int decalCount = Mathf.Max(1, bloodDecalCount);
            for (int i = 0; i < decalCount; i++)
            {
                bool mainDecal = i == 0;
                Vector2 scatter = mainDecal
                    ? Vector2.zero
                    : Random.insideUnitCircle * bloodDecalScatterRadius * Random.Range(0.25f, 1f) * scaleMultiplier;

                if (!TryResolveBloodDecalSurface(hitPoint, safeNormal, tangent, bitangent, scatter, out Vector3 decalPosition, out Vector3 decalNormal))
                {
                    continue;
                }

                Quaternion decalRotation = ResolveBloodRotation(decalNormal)
                    * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                float decalScale = Random.Range(bloodSplatterScaleRange.x, bloodSplatterScaleRange.y)
                    * scaleMultiplier
                    * (mainDecal ? Random.Range(1.05f, 1.35f) : Random.Range(0.35f, 0.8f));
                Vector2 decalSize = new Vector2(
                    decalScale * Random.Range(0.75f, 1.45f),
                    decalScale * Random.Range(0.65f, 1.2f));
                Color decalColor = Color.Lerp(
                    bloodColor,
                    new Color(0.18f, 0f, 0.005f, bloodColor.a),
                    Random.Range(0f, mainDecal ? 0.18f : 0.35f));

                SpawnBloodQuad(
                    "BloodDecal",
                    bloodSplatterSprite.texture,
                    decalColor,
                    decalPosition,
                    decalRotation,
                    decalSize,
                    Mathf.Max(bloodSplatterLifetime, deathBurst ? 5.25f : 3.2f) * Random.Range(0.82f, 1.18f),
                    Vector3.zero,
                    0f,
                    0f,
                    billboard: false,
                    shrinkOverLife: false,
                    collideAndStick: false,
                    ignoredRoot: transform);
            }
        }

        Sprite spraySprite = bloodSpraySprite != null ? bloodSpraySprite : bloodSplatterSprite;
        if (spraySprite == null || bloodSprayParticleCount <= 0)
        {
            return;
        }

        int sprayCount = Mathf.RoundToInt(bloodSprayParticleCount * Mathf.Lerp(1f, 1.55f, Mathf.Clamp01(scaleMultiplier - 1f)));
        float velocityScale = Mathf.Pow(Mathf.Max(1f, scaleMultiplier), 0.25f);
        for (int i = 0; i < sprayCount; i++)
        {
            Vector3 lateral = tangent * Random.Range(-1f, 1f) + bitangent * Random.Range(-0.75f, 0.75f);
            Vector3 sprayDirection = (safeNormal * Random.Range(0.85f, 1.35f) + lateral * Random.Range(0.25f, 0.95f)).normalized;
            Vector3 velocity = sprayDirection * Random.Range(bloodSpraySpeedRange.x, bloodSpraySpeedRange.y) * velocityScale
                + Vector3.up * Random.Range(0.1f, 0.85f);
            Vector3 sprayPosition = hitPoint
                + safeNormal * (bloodSurfaceOffset + 0.03f)
                + lateral * Random.Range(0.01f, 0.07f);
            float sprayScale = Random.Range(bloodSprayScaleRange.x, bloodSprayScaleRange.y)
                * scaleMultiplier
                * Random.Range(0.75f, 1.25f);
            Vector2 spraySize = new Vector2(
                sprayScale * Random.Range(0.55f, 1.25f),
                sprayScale * Random.Range(0.45f, 1.15f));
            Color sprayColor = Color.Lerp(
                bloodColor,
                new Color(0.95f, 0.08f, 0.025f, bloodColor.a),
                Random.Range(0f, 0.28f));

            SpawnBloodQuad(
                "BloodSpray",
                spraySprite.texture,
                sprayColor,
                sprayPosition,
                ResolveBloodRotation(safeNormal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)),
                spraySize,
                bloodSprayLifetime * Random.Range(0.7f, 1.25f),
                velocity,
                gravity: 7.5f,
                angularSpeed: Random.Range(-480f, 480f),
                billboard: true,
                shrinkOverLife: true,
                collideAndStick: true,
                ignoredRoot: transform);
        }
    }

    private void SpawnBloodQuad(
        string objectName,
        Texture texture,
        Color tint,
        Vector3 position,
        Quaternion rotation,
        Vector2 size,
        float lifetime,
        Vector3 velocity,
        float gravity,
        float angularSpeed,
        bool billboard,
        bool shrinkOverLife,
        bool collideAndStick,
        Transform ignoredRoot)
    {
        if (texture == null)
        {
            return;
        }

        BloodSpriteEffect effect = BloodEffectPool?.Rent(position, rotation);
        if (effect == null)
        {
            return;
        }

        effect.gameObject.name = objectName;
        effect.transform.localScale = new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), 1f);
        effect.Initialize(objectName, texture, tint, lifetime, velocity, gravity, angularSpeed, billboard, shrinkOverLife, collideAndStick, ignoredRoot, bloodSurfaceOffset);
    }

    private static RetroComponentPool<BloodSpriteEffect> BloodEffectPool
    {
        get
        {
            if (bloodEffectPool == null || !bloodEffectPool.IsValid)
            {
                bloodEffectPool = RetroPoolService.Shared.GetOrCreateComponentPool(
                    "RetroDamageable.BloodSpriteEffect",
                    CreateBloodSpriteEffect,
                    new RetroPoolSettings(prewarmCount: 32, maxInactiveCount: 512));
            }

            return bloodEffectPool;
        }
    }

    private static BloodSpriteEffect CreateBloodSpriteEffect(Transform parent)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "BloodSpriteEffect";
        quad.transform.SetParent(parent, false);

        Collider quadCollider = quad.GetComponent<Collider>();
        if (quadCollider != null)
        {
            quadCollider.enabled = false;
            Destroy(quadCollider);
        }

        Renderer quadRenderer = quad.GetComponent<Renderer>();
        if (quadRenderer == null)
        {
            Destroy(quad);
            return null;
        }

        quadRenderer.shadowCastingMode = ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;

        BloodSpriteEffect effect = quad.AddComponent<BloodSpriteEffect>();
        effect.ConfigureRenderer(quadRenderer);
        return effect;
    }

    private static Quaternion ResolveBloodRotation(Vector3 hitNormal)
    {
        Vector3 forward = hitNormal;
        Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f
            ? Vector3.forward
            : Vector3.up;
        return Quaternion.LookRotation(forward, up);
    }

    private static void BuildSurfaceBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
    {
        Vector3 seed = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.92f ? Vector3.forward : Vector3.up;
        tangent = Vector3.Cross(seed, normal).normalized;
        bitangent = Vector3.Cross(normal, tangent).normalized;
    }

    private static void BuildHorizontalBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
    {
        Vector3 projected = Vector3.ProjectOnPlane(normal, Vector3.up);
        if (projected.sqrMagnitude < 0.0001f)
        {
            projected = Vector3.forward;
        }

        bitangent = projected.normalized;
        tangent = Vector3.Cross(Vector3.up, bitangent).normalized;
    }

    private bool TryResolveBloodDecalSurface(
        Vector3 hitPoint,
        Vector3 safeNormal,
        Vector3 tangent,
        Vector3 bitangent,
        Vector2 scatter,
        out Vector3 decalPosition,
        out Vector3 decalNormal)
    {
        BuildHorizontalBasis(safeNormal, out Vector3 groundTangent, out Vector3 groundBitangent);
        Vector3 scatterOffset = groundTangent * scatter.x + groundBitangent * scatter.y;
        Vector3 origin = hitPoint
            + scatterOffset
            + safeNormal * Mathf.Max(0.06f, bloodSurfaceOffset + 0.05f);
        Vector3 lateral = tangent * Random.Range(-0.35f, 0.35f) + bitangent * Random.Range(-0.25f, 0.25f);
        Vector3 sprayDirection = safeNormal * Random.Range(0.55f, 1.15f)
            + lateral
            + Vector3.down * Random.Range(0f, 0.28f);
        if (sprayDirection.sqrMagnitude < 0.0001f)
        {
            sprayDirection = Vector3.down;
        }

        if (TryRaycastBloodSurface(origin, sprayDirection, BloodDecalProbeDistance, transform, out RaycastHit hit)
            || TryRaycastBloodSurface(origin + Vector3.up * BloodDecalProbeHeight, Vector3.down, BloodDecalProbeDistance + BloodDecalProbeHeight, transform, out hit)
            || TryRaycastBloodSurface(hitPoint + scatterOffset + Vector3.up * BloodDecalProbeHeight, Vector3.down, BloodDecalProbeDistance + BloodDecalProbeHeight, transform, out hit))
        {
            decalPosition = hit.point + hit.normal * Mathf.Max(0.006f, bloodSurfaceOffset);
            decalNormal = hit.normal;
            return true;
        }

        decalPosition = default;
        decalNormal = Vector3.up;
        return false;
    }

    private static bool TryLinecastBloodSurface(Vector3 start, Vector3 end, Transform ignoredRoot, out RaycastHit hit)
    {
        Vector3 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
        {
            hit = default;
            return false;
        }

        return TryRaycastBloodSurface(start, delta / distance, distance, ignoredRoot, out hit);
    }

    private static bool TryRaycastBloodSurface(Vector3 origin, Vector3 direction, float distance, Transform ignoredRoot, out RaycastHit bestHit)
    {
        bestHit = default;
        if (direction.sqrMagnitude < 0.0001f || distance <= 0f)
        {
            return false;
        }

        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, distance, ~0, QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit candidate = hits[i];
            if (candidate.collider == null || IsIgnoredBloodCollider(candidate.collider, ignoredRoot) || candidate.distance >= bestDistance)
            {
                continue;
            }

            bestHit = candidate;
            bestDistance = candidate.distance;
            found = true;
        }

        return found;
    }

    private static bool IsIgnoredBloodCollider(Collider collider, Transform ignoredRoot)
    {
        if (collider == null)
        {
            return true;
        }

        Transform hitTransform = collider.transform;
        if (ignoredRoot != null && (hitTransform == ignoredRoot || hitTransform.IsChildOf(ignoredRoot)))
        {
            return true;
        }

        return collider.GetComponentInParent<RetroFpsController>() != null
            || collider.GetComponentInParent<RetroWeaponSystem>() != null
            || collider.GetComponentInParent<PlayerInput>() != null
            || collider.CompareTag("Player");
    }

    private static Material CreateTransparentTextureMaterial(string materialName, Texture texture, Color tint)
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
        material.EnableKeyword("_BLENDMODE_ALPHA");

        SetMaterialTextureIfPresent(material, texture, "_UnlitColorMap", "_BaseColorMap", "_MainTex", "_BaseMap");
        SetMaterialColorIfPresent(material, tint, "_UnlitColor", "_BaseColor", "_Color");
        SetMaterialFloatIfPresent(material, 0f, "_AlphaRemapMin");
        SetMaterialFloatIfPresent(material, 1f, "_AlphaRemapMax");
        SetMaterialFloatIfPresent(material, 1f, "_SurfaceType");
        SetMaterialFloatIfPresent(material, 0f, "_BlendMode");
        SetMaterialFloatIfPresent(material, 0f, "_AlphaCutoffEnable", "_AlphaClip");
        SetMaterialFloatIfPresent(material, 0f, "_ZWrite", "_TransparentZWrite");
        SetMaterialFloatIfPresent(material, (float)CullMode.Off, "_CullMode", "_CullModeForward");
        SetMaterialFloatIfPresent(material, (float)BlendMode.SrcAlpha, "_SrcBlend");
        SetMaterialFloatIfPresent(material, (float)BlendMode.OneMinusSrcAlpha, "_DstBlend");
        SetMaterialFloatIfPresent(material, (float)BlendMode.One, "_AlphaSrcBlend");
        SetMaterialFloatIfPresent(material, (float)BlendMode.OneMinusSrcAlpha, "_AlphaDstBlend");

        return material;
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

    private sealed class BloodSpriteEffect : MonoBehaviour, IRetroPoolLifecycle
    {
        private RetroPooledObject pooledObject;
        private Renderer effectRenderer;
        private Material effectMaterial;
        private Color initialTint;
        private Vector3 initialScale;
        private Vector3 velocity;
        private float lifetime;
        private float age;
        private float gravity;
        private float angularSpeed;
        private float surfaceOffset;
        private float rollDegrees;
        private bool billboard;
        private bool shrinkOverLife;
        private bool collideAndStick;
        private bool stuck;
        private Transform ignoredRoot;

        public void ConfigureRenderer(Renderer renderer)
        {
            effectRenderer = renderer;
        }

        public void Initialize(
            string materialName,
            Texture texture,
            Color tint,
            float lifeSeconds,
            Vector3 initialVelocity,
            float gravityStrength,
            float spinDegreesPerSecond,
            bool faceCamera,
            bool shrink,
            bool stickOnCollision,
            Transform collisionIgnoredRoot,
            float collisionSurfaceOffset)
        {
            if (effectRenderer == null)
            {
                effectRenderer = GetComponent<Renderer>();
            }

            EnsureMaterial(materialName, texture, tint);
            initialTint = tint;
            initialScale = transform.localScale;
            velocity = initialVelocity;
            lifetime = Mathf.Max(0f, lifeSeconds);
            gravity = Mathf.Max(0f, gravityStrength);
            angularSpeed = spinDegreesPerSecond;
            billboard = faceCamera;
            shrinkOverLife = shrink;
            collideAndStick = stickOnCollision;
            stuck = false;
            ignoredRoot = collisionIgnoredRoot;
            surfaceOffset = Mathf.Max(0.006f, collisionSurfaceOffset);
            rollDegrees = transform.eulerAngles.z;
            ApplyTint(initialTint);
        }

        public void OnPoolRent(RetroPooledObject pooledObject)
        {
            this.pooledObject = pooledObject;
            age = 0f;
            if (effectRenderer == null)
            {
                effectRenderer = GetComponent<Renderer>();
            }

            if (effectRenderer != null)
            {
                effectRenderer.enabled = false;
            }
        }

        public void OnPoolReturn(RetroPooledObject pooledObject)
        {
            velocity = Vector3.zero;
            lifetime = 0f;
            gravity = 0f;
            angularSpeed = 0f;
            billboard = false;
            shrinkOverLife = false;
            collideAndStick = false;
            stuck = false;
            ignoredRoot = null;
            surfaceOffset = 0.018f;
            age = 0f;
            transform.localScale = Vector3.one;
            Color hiddenTint = initialTint;
            hiddenTint.a = 0f;
            ApplyTint(hiddenTint);
            if (effectRenderer != null)
            {
                effectRenderer.enabled = false;
            }
        }

        public void OnPoolDestroy(RetroPooledObject pooledObject)
        {
            DestroyOwnedMaterial();
            this.pooledObject = null;
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;

            if (!stuck && velocity.sqrMagnitude > 0.000001f)
            {
                Vector3 previous = transform.position;
                velocity += Vector3.down * gravity * deltaTime;
                velocity *= Mathf.Exp(-1.45f * deltaTime);
                Vector3 next = previous + velocity * deltaTime;
                if (collideAndStick && TryLinecastBloodSurface(previous, next, ignoredRoot, out RaycastHit hit))
                {
                    transform.position = hit.point + hit.normal * surfaceOffset;
                    transform.rotation = ResolveBloodRotation(hit.normal) * Quaternion.Euler(0f, 0f, rollDegrees);
                    velocity = Vector3.zero;
                    gravity = 0f;
                    angularSpeed = 0f;
                    billboard = false;
                    shrinkOverLife = false;
                    stuck = true;
                    initialScale = transform.localScale * Random.Range(0.85f, 1.2f);
                    lifetime = Mathf.Max(lifetime - age, Random.Range(0.55f, 1.25f));
                    age = 0f;
                }
                else
                {
                    transform.position = next;
                }
            }

            if (billboard)
            {
                rollDegrees += angularSpeed * deltaTime;
                Camera camera = Camera.main;
                if (camera != null)
                {
                    Vector3 toCamera = transform.position - camera.transform.position;
                    if (toCamera.sqrMagnitude > 0.0001f)
                    {
                        transform.rotation = Quaternion.LookRotation(toCamera.normalized, camera.transform.up)
                            * Quaternion.Euler(0f, 0f, rollDegrees);
                    }
                }
                else if (Mathf.Abs(angularSpeed) > 0.001f)
                {
                    transform.Rotate(0f, 0f, angularSpeed * deltaTime, Space.Self);
                }
            }
            else if (Mathf.Abs(angularSpeed) > 0.001f)
            {
                transform.Rotate(0f, 0f, angularSpeed * deltaTime, Space.Self);
            }

            if (lifetime <= 0f)
            {
                return;
            }

            age += deltaTime;
            float normalizedAge = age / lifetime;
            if (normalizedAge >= 1f)
            {
                ReturnOrDestroy();
                return;
            }

            float fadeStart = shrinkOverLife ? 0.08f : 0.62f;
            float fadeAmount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStart, 1f, normalizedAge));
            Color tint = initialTint;
            tint.a *= 1f - fadeAmount;
            ApplyTint(tint);

            if (shrinkOverLife)
            {
                float scale = Mathf.Lerp(1f, 0.42f, Mathf.SmoothStep(0f, 1f, normalizedAge));
                transform.localScale = initialScale * scale;
            }
        }

        private void OnDestroy()
        {
            DestroyOwnedMaterial();
        }

        private void ReturnOrDestroy()
        {
            if (pooledObject != null && pooledObject.IsRented)
            {
                pooledObject.ReturnToPool();
                return;
            }

            Destroy(gameObject);
        }

        private void EnsureMaterial(string materialName, Texture texture, Color tint)
        {
            if (effectMaterial == null)
            {
                effectMaterial = RetroDamageable.CreateTransparentTextureMaterial(materialName, texture, tint);
                if (effectRenderer != null)
                {
                    effectRenderer.sharedMaterial = effectMaterial;
                }
            }
            else
            {
                effectMaterial.name = materialName;
                RetroDamageable.SetMaterialTextureIfPresent(effectMaterial, texture, "_UnlitColorMap", "_BaseColorMap", "_MainTex", "_BaseMap");
                RetroDamageable.SetMaterialColorIfPresent(effectMaterial, tint, "_UnlitColor", "_BaseColor", "_Color");
            }
        }

        private void DestroyOwnedMaterial()
        {
            if (effectMaterial == null)
            {
                return;
            }

            Destroy(effectMaterial);
            effectMaterial = null;
        }

        private void ApplyTint(Color tint)
        {
            if (effectRenderer != null)
            {
                effectRenderer.enabled = tint.a > 0.001f;
            }

            RetroDamageable.SetMaterialColorIfPresent(effectMaterial, tint, "_UnlitColor", "_BaseColor", "_Color");
        }
    }

    private void HandleDeath(GameObject source)
    {
        dead = true;
        Died?.Invoke(this);
        RetroGameContext.Events.Publish(new RetroDeathEvent(source, this, transform.position));

        if (disableRenderersOnDeath)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }
        }

        if (disableCollidersOnDeath)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }
}
