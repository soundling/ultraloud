using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public enum RetroShootableSurfaceKind
{
    Wood,
    Stone,
    Bird
}

[DisallowMultipleComponent]
[RequireComponent(typeof(RetroDamageable))]
public sealed class RetroShootableFeedback : MonoBehaviour
{
    [SerializeField] private RetroDamageable damageable;
    [SerializeField] private RetroShootableSurfaceKind surfaceKind = RetroShootableSurfaceKind.Wood;
    [SerializeField] private bool spawnImpactEffects = true;
    [SerializeField] private bool spawnDeathEffects = true;
    [SerializeField] private bool useImpactFlash = true;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private bool kickVisualRoot = true;
    [SerializeField, Min(0f)] private float visualKickDistance = 0.045f;
    [SerializeField, Min(0f)] private float visualKickAngle = 4.5f;
    [SerializeField, Min(0f)] private float visualKickReturnSpeed = 16f;
    [SerializeField, Min(0f)] private float effectScale = 1f;
    [SerializeField, Min(0f)] private float deathEffectMultiplier = 2.4f;
    [SerializeField] private bool disableFlockAgentOnDeath;

    private static RetroComponentPool<ShootableParticle> particlePool;
    private static RetroComponentPool<ShootableDecal> decalPool;
    private static Mesh quadMesh;
    private static Mesh diamondMesh;
    private static Mesh shardMesh;
    private static Mesh decalMesh;

    private Transform resolvedVisualRoot;
    private Vector3 visualBaseLocalPosition;
    private Quaternion visualBaseLocalRotation = Quaternion.identity;
    private Vector3 visualKickOffset;
    private Vector3 visualKickEuler;
    private Vector3 lastHitPoint;
    private Vector3 lastHitNormal = Vector3.up;
    private bool hasLastHit;

    private void Reset()
    {
        damageable = GetComponent<RetroDamageable>();
        ApplySurfaceDefaults();
    }

    private void Awake()
    {
        ResolveDamageable();
        CacheVisualRoot();
    }

    private void OnEnable()
    {
        ResolveDamageable();
        if (damageable != null)
        {
            damageable.Damaged += HandleDamaged;
            damageable.Died += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (damageable != null)
        {
            damageable.Damaged -= HandleDamaged;
            damageable.Died -= HandleDied;
        }
    }

    private void OnValidate()
    {
        ResolveDamageable();
        visualKickDistance = Mathf.Max(0f, visualKickDistance);
        visualKickAngle = Mathf.Max(0f, visualKickAngle);
        visualKickReturnSpeed = Mathf.Max(0f, visualKickReturnSpeed);
        effectScale = Mathf.Max(0f, effectScale);
        deathEffectMultiplier = Mathf.Max(0f, deathEffectMultiplier);
    }

    private void LateUpdate()
    {
        UpdateVisualKick(Time.deltaTime);
    }

    private void ResolveDamageable()
    {
        if (damageable == null)
        {
            damageable = GetComponent<RetroDamageable>();
        }
    }

    private void ApplySurfaceDefaults()
    {
        switch (surfaceKind)
        {
            case RetroShootableSurfaceKind.Stone:
                visualKickDistance = 0.018f;
                visualKickAngle = 1.5f;
                effectScale = 1.1f;
                break;
            case RetroShootableSurfaceKind.Bird:
                visualKickDistance = 0.075f;
                visualKickAngle = 9f;
                effectScale = 0.75f;
                deathEffectMultiplier = 3f;
                disableFlockAgentOnDeath = true;
                break;
            default:
                visualKickDistance = 0.05f;
                visualKickAngle = 4.5f;
                effectScale = 1f;
                break;
        }
    }

    private void HandleDamaged(RetroDamageable target, float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        Vector3 safeNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.up;
        lastHitPoint = hitPoint;
        lastHitNormal = safeNormal;
        hasLastHit = true;

        if (spawnImpactEffects)
        {
            SpawnSurfaceBurst(hitPoint, safeNormal, Mathf.Max(1f, damage), false);
        }

        ApplyVisualKick(safeNormal, damage);
    }

    private void HandleDied(RetroDamageable target)
    {
        if (disableFlockAgentOnDeath)
        {
            RetroFlockAgent flockAgent = GetComponent<RetroFlockAgent>();
            if (flockAgent != null)
            {
                flockAgent.enabled = false;
            }
        }

        if (!spawnDeathEffects)
        {
            return;
        }

        Vector3 point = hasLastHit ? lastHitPoint : ResolveFallbackEffectPoint();
        Vector3 normal = hasLastHit ? lastHitNormal : Vector3.up;
        SpawnSurfaceBurst(point, normal, damageable != null ? damageable.MaxHealth : 50f, true);
    }

    private Vector3 ResolveFallbackEffectPoint()
    {
        Collider targetCollider = GetComponentInChildren<Collider>();
        if (targetCollider != null)
        {
            return targetCollider.bounds.center;
        }

        Renderer targetRenderer = GetComponentInChildren<Renderer>();
        return targetRenderer != null ? targetRenderer.bounds.center : transform.position;
    }

    private void ApplyVisualKick(Vector3 hitNormal, float damage)
    {
        if (!kickVisualRoot || visualKickReturnSpeed <= 0f)
        {
            return;
        }

        CacheVisualRoot();
        if (resolvedVisualRoot == null)
        {
            return;
        }

        float intensity = Mathf.Clamp01(damage / 30f);
        Vector3 localNormal = transform.InverseTransformDirection(hitNormal);
        visualKickOffset += localNormal * visualKickDistance * Mathf.Lerp(0.6f, 1.65f, intensity);
        visualKickEuler += Random.insideUnitSphere * visualKickAngle * Mathf.Lerp(0.5f, 1.45f, intensity);
        visualKickOffset = Vector3.ClampMagnitude(visualKickOffset, visualKickDistance * 3.2f);
        visualKickEuler = Vector3.ClampMagnitude(visualKickEuler, visualKickAngle * 3.2f);
    }

    private void UpdateVisualKick(float deltaTime)
    {
        if (!kickVisualRoot || resolvedVisualRoot == null)
        {
            CacheVisualRoot();
            if (resolvedVisualRoot == null)
            {
                return;
            }
        }

        float damp = 1f - Mathf.Exp(-visualKickReturnSpeed * Mathf.Max(0f, deltaTime));
        visualKickOffset = Vector3.Lerp(visualKickOffset, Vector3.zero, damp);
        visualKickEuler = Vector3.Lerp(visualKickEuler, Vector3.zero, damp);
        resolvedVisualRoot.localPosition = visualBaseLocalPosition + visualKickOffset;
        resolvedVisualRoot.localRotation = visualBaseLocalRotation * Quaternion.Euler(visualKickEuler);
    }

    private void CacheVisualRoot()
    {
        Transform next = visualRoot != null ? visualRoot : FindGeneratedVisualRoot();
        if (next == null || next == resolvedVisualRoot)
        {
            return;
        }

        resolvedVisualRoot = next;
        visualBaseLocalPosition = resolvedVisualRoot.localPosition;
        visualBaseLocalRotation = resolvedVisualRoot.localRotation;
        visualKickOffset = Vector3.zero;
        visualKickEuler = Vector3.zero;
    }

    private Transform FindGeneratedVisualRoot()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name.StartsWith("__"))
            {
                return child;
            }
        }

        return transform.childCount > 0 ? transform.GetChild(0) : transform;
    }

    private void SpawnSurfaceBurst(Vector3 point, Vector3 normal, float damage, bool death)
    {
        float intensity = Mathf.Clamp(damage / 20f, 0.65f, death ? 5.5f : 2.75f);
        intensity *= Mathf.Max(0f, effectScale) * (death ? deathEffectMultiplier : 1f);
        BuildSurfaceBasis(normal, out Vector3 tangent, out Vector3 bitangent);

        if (useImpactFlash)
        {
            Color flashColor = ResolveFlashColor();
            RetroGameContext.Vfx.SpawnImpactFlash(point, normal, flashColor, Mathf.Lerp(0.05f, 0.16f, Mathf.Clamp01(intensity / 4f)), death ? 0.11f : 0.065f);
        }

        switch (surfaceKind)
        {
            case RetroShootableSurfaceKind.Stone:
                SpawnStoneBurst(point, normal, tangent, bitangent, intensity, death);
                break;
            case RetroShootableSurfaceKind.Bird:
                SpawnBirdBurst(point, normal, tangent, bitangent, intensity, death);
                break;
            default:
                SpawnWoodBurst(point, normal, tangent, bitangent, intensity, death);
                break;
        }
    }

    private void SpawnWoodBurst(Vector3 point, Vector3 normal, Vector3 tangent, Vector3 bitangent, float intensity, bool death)
    {
        SpawnDecal(point, normal, RandomColor(new Color(0.22f, 0.11f, 0.045f, 0.85f), new Color(0.48f, 0.25f, 0.09f, 0.72f)), Random.Range(0.16f, 0.3f) * intensity, death ? 5.5f : 3.5f);
        SpawnParticles(ShardMesh, point, normal, tangent, bitangent, Mathf.RoundToInt(7 * intensity), new Color(0.37f, 0.18f, 0.07f, 1f), new Color(0.74f, 0.46f, 0.22f, 1f), new Vector2(0.035f, 0.095f), new Vector2(1.4f, 4.4f), 5.5f, 0.85f, false, false);
        SpawnParticles(DiamondMesh, point, normal, tangent, bitangent, Mathf.RoundToInt((death ? 18 : 8) * intensity), new Color(0.13f, 0.35f, 0.08f, 0.9f), new Color(0.78f, 0.65f, 0.22f, 0.82f), new Vector2(0.055f, 0.16f), new Vector2(1.1f, 3.6f), 2.8f, 1.4f, true, false);
        SpawnParticles(DecalMesh, point, normal, tangent, bitangent, Mathf.RoundToInt(4 * intensity), new Color(0.48f, 0.37f, 0.22f, 0.38f), new Color(0.75f, 0.58f, 0.36f, 0.28f), new Vector2(0.12f, 0.34f), new Vector2(0.35f, 1.25f), 1.25f, 2.4f, true, true);
    }

    private void SpawnStoneBurst(Vector3 point, Vector3 normal, Vector3 tangent, Vector3 bitangent, float intensity, bool death)
    {
        SpawnDecal(point, normal, RandomColor(new Color(0.08f, 0.075f, 0.065f, 0.8f), new Color(0.28f, 0.27f, 0.24f, 0.62f)), Random.Range(0.13f, 0.24f) * intensity, death ? 7f : 5f);
        SpawnParticles(ShardMesh, point, normal, tangent, bitangent, Mathf.RoundToInt(10 * intensity), new Color(0.28f, 0.27f, 0.24f, 1f), new Color(0.78f, 0.76f, 0.68f, 1f), new Vector2(0.025f, 0.08f), new Vector2(1.8f, 5.8f), 7.2f, 0.72f, false, false);
        SpawnParticles(DecalMesh, point, normal, tangent, bitangent, Mathf.RoundToInt(8 * intensity), new Color(0.38f, 0.36f, 0.31f, 0.42f), new Color(0.72f, 0.68f, 0.58f, 0.25f), new Vector2(0.13f, 0.42f), new Vector2(0.45f, 1.85f), 1.1f, 2.15f, true, true);
        SpawnParticles(QuadMesh, point, normal, tangent, bitangent, Mathf.RoundToInt(3 * intensity), new Color(1f, 0.58f, 0.16f, 0.82f), new Color(1f, 0.92f, 0.5f, 0.65f), new Vector2(0.018f, 0.04f), new Vector2(2.5f, 7.5f), 6.4f, 0.55f, true, false);
    }

    private void SpawnBirdBurst(Vector3 point, Vector3 normal, Vector3 tangent, Vector3 bitangent, float intensity, bool death)
    {
        SpawnParticles(DiamondMesh, point, normal, tangent, bitangent, Mathf.RoundToInt((death ? 18 : 7) * intensity), new Color(0.72f, 0.64f, 0.48f, 0.95f), new Color(1f, 0.94f, 0.78f, 0.9f), new Vector2(0.055f, 0.16f), new Vector2(1.3f, 4.2f), 2.4f, 1.15f, true, false);
        SpawnParticles(DecalMesh, point, normal, tangent, bitangent, Mathf.RoundToInt((death ? 10 : 4) * intensity), new Color(0.48f, 0.015f, 0.01f, 0.85f), new Color(0.95f, 0.06f, 0.025f, 0.8f), new Vector2(0.035f, 0.12f), new Vector2(1.7f, 4.6f), 5.2f, 0.9f, true, false);
        if (death)
        {
            SpawnDecal(point, normal, RandomColor(new Color(0.38f, 0.005f, 0.005f, 0.9f), new Color(0.85f, 0.035f, 0.02f, 0.72f)), Random.Range(0.16f, 0.32f) * intensity, 3f);
        }
    }

    private void SpawnParticles(
        Mesh mesh,
        Vector3 point,
        Vector3 normal,
        Vector3 tangent,
        Vector3 bitangent,
        int count,
        Color colorA,
        Color colorB,
        Vector2 scaleRange,
        Vector2 speedRange,
        float gravity,
        float drag,
        bool billboard,
        bool growOverLife)
    {
        count = Mathf.Clamp(count, 0, 96);
        for (int i = 0; i < count; i++)
        {
            Vector3 lateral = tangent * Random.Range(-1f, 1f) + bitangent * Random.Range(-1f, 1f);
            Vector3 direction = (normal * Random.Range(0.65f, 1.45f) + lateral * Random.Range(0.25f, 1.1f) + Vector3.up * Random.Range(0f, 0.55f)).normalized;
            Vector3 velocity = direction * Random.Range(speedRange.x, speedRange.y);
            Vector3 spawnPoint = point + normal * Random.Range(0.025f, 0.08f) + lateral * Random.Range(0f, 0.05f);
            Quaternion rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.0001f ? direction : normal, Vector3.up)
                * Quaternion.Euler(Random.Range(-35f, 35f), Random.Range(-180f, 180f), Random.Range(-180f, 180f));
            float scale = Random.Range(scaleRange.x, scaleRange.y);
            Vector3 localScale = billboard
                ? new Vector3(scale * Random.Range(0.65f, 1.4f), scale * Random.Range(1f, 2.2f), scale)
                : Vector3.one * scale;
            Color color = RandomColor(colorA, colorB);
            float lifetime = Random.Range(0.34f, 0.95f) * (growOverLife ? 1.35f : 1f);

            ShootableParticle particle = ParticlePool?.Rent(spawnPoint, rotation);
            if (particle != null)
            {
                particle.Play(mesh, color, localScale, velocity, Random.insideUnitSphere * Random.Range(120f, 640f), gravity, drag, lifetime, billboard, growOverLife);
            }
        }
    }

    private void SpawnDecal(Vector3 point, Vector3 normal, Color color, float scale, float lifetime)
    {
        if (scale <= 0f || lifetime <= 0f)
        {
            return;
        }

        Quaternion rotation = ResolveSurfaceRotation(normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        ShootableDecal decal = DecalPool?.Rent(point + normal * 0.026f, rotation);
        if (decal != null)
        {
            decal.Play(DecalMesh, color, scale, lifetime);
        }
    }

    private Color ResolveFlashColor()
    {
        return surfaceKind switch
        {
            RetroShootableSurfaceKind.Stone => new Color(1f, 0.68f, 0.28f, 0.62f),
            RetroShootableSurfaceKind.Bird => new Color(0.95f, 0.08f, 0.035f, 0.72f),
            _ => new Color(0.92f, 0.55f, 0.22f, 0.58f)
        };
    }

    private static RetroComponentPool<ShootableParticle> ParticlePool
    {
        get
        {
            if (particlePool == null || !particlePool.IsValid)
            {
                particlePool = RetroPoolService.Shared.GetOrCreateComponentPool(
                    "RetroShootableFeedback.Particles",
                    CreateParticle,
                    new RetroPoolSettings(prewarmCount: 96, maxInactiveCount: 1024));
            }

            return particlePool;
        }
    }

    private static RetroComponentPool<ShootableDecal> DecalPool
    {
        get
        {
            if (decalPool == null || !decalPool.IsValid)
            {
                decalPool = RetroPoolService.Shared.GetOrCreateComponentPool(
                    "RetroShootableFeedback.Decals",
                    CreateDecal,
                    new RetroPoolSettings(prewarmCount: 32, maxInactiveCount: 384));
            }

            return decalPool;
        }
    }

    private static ShootableParticle CreateParticle(Transform parent)
    {
        GameObject particleObject = new("ShootableParticle");
        particleObject.transform.SetParent(parent, false);
        MeshFilter filter = particleObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = particleObject.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ShootableParticle particle = particleObject.AddComponent<ShootableParticle>();
        particle.Configure(filter, renderer);
        return particle;
    }

    private static ShootableDecal CreateDecal(Transform parent)
    {
        GameObject decalObject = new("ShootableDecal");
        decalObject.transform.SetParent(parent, false);
        MeshFilter filter = decalObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = decalObject.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ShootableDecal decal = decalObject.AddComponent<ShootableDecal>();
        decal.Configure(filter, renderer);
        return decal;
    }

    private static Mesh QuadMesh => quadMesh != null ? quadMesh : quadMesh = BuildQuadMesh();
    private static Mesh DiamondMesh => diamondMesh != null ? diamondMesh : diamondMesh = BuildDiamondMesh();
    private static Mesh ShardMesh => shardMesh != null ? shardMesh : shardMesh = BuildShardMesh();
    private static Mesh DecalMesh => decalMesh != null ? decalMesh : decalMesh = BuildStarMesh(9, 0.35f, 1f);

    private static Mesh BuildQuadMesh()
    {
        Mesh mesh = new() { name = "Shootable Quad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildDiamondMesh()
    {
        Mesh mesh = new() { name = "Shootable Diamond" };
        mesh.vertices = new[]
        {
            new Vector3(0f, -0.65f, 0f),
            new Vector3(0.28f, 0f, 0f),
            new Vector3(0f, 0.65f, 0f),
            new Vector3(-0.28f, 0f, 0f)
        };
        mesh.uv = new[] { new Vector2(0.5f, 0f), new Vector2(1f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 0.5f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildShardMesh()
    {
        Mesh mesh = new() { name = "Shootable Shard" };
        mesh.vertices = new[]
        {
            new Vector3(0f, 0.55f, 0f),
            new Vector3(-0.45f, -0.35f, -0.28f),
            new Vector3(0.5f, -0.28f, -0.22f),
            new Vector3(0.05f, -0.42f, 0.52f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 1, 1, 3, 2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildStarMesh(int points, float innerRadius, float outerRadius)
    {
        points = Mathf.Max(3, points);
        Vector3[] vertices = new Vector3[points * 2 + 1];
        int[] triangles = new int[points * 2 * 3];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < points * 2; i++)
        {
            float angle = i / (float)(points * 2) * Mathf.PI * 2f;
            float radius = (i & 1) == 0 ? outerRadius : innerRadius;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        for (int i = 0; i < points * 2; i++)
        {
            int triangle = i * 3;
            triangles[triangle] = 0;
            triangles[triangle + 1] = i + 1;
            triangles[triangle + 2] = i == points * 2 - 1 ? 1 : i + 2;
        }

        Mesh mesh = new() { name = "Shootable Star Decal" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Quaternion ResolveSurfaceRotation(Vector3 hitNormal)
    {
        Vector3 forward = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.up;
        Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
        return Quaternion.LookRotation(forward, up);
    }

    private static void BuildSurfaceBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
    {
        Vector3 seed = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.92f ? Vector3.forward : Vector3.up;
        tangent = Vector3.Cross(seed, normal).normalized;
        bitangent = Vector3.Cross(normal, tangent).normalized;
    }

    private static Color RandomColor(Color a, Color b)
    {
        return Color.Lerp(a, b, Random.value);
    }

    private static Material CreateVfxMaterial(string materialName)
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        shader ??= Shader.Find("Unlit/Color");
        shader ??= Shader.Find("Sprites/Default");
        shader ??= Shader.Find("Standard");
        if (shader == null)
        {
            return null;
        }

        Material material = new(shader)
        {
            name = materialName,
            renderQueue = (int)RenderQueue.Transparent
        };
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_BLENDMODE_ALPHA");
        SetMaterialFloatIfPresent(material, 1f, "_SurfaceType");
        SetMaterialFloatIfPresent(material, 0f, "_BlendMode");
        SetMaterialFloatIfPresent(material, 0f, "_ZWrite", "_TransparentZWrite");
        SetMaterialFloatIfPresent(material, (float)CullMode.Off, "_CullMode", "_CullModeForward");
        SetMaterialFloatIfPresent(material, (float)BlendMode.SrcAlpha, "_SrcBlend");
        SetMaterialFloatIfPresent(material, (float)BlendMode.OneMinusSrcAlpha, "_DstBlend");
        SetMaterialFloatIfPresent(material, (float)BlendMode.One, "_AlphaSrcBlend");
        SetMaterialFloatIfPresent(material, (float)BlendMode.OneMinusSrcAlpha, "_AlphaDstBlend");
        return material;
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", color);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
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

    private abstract class PooledShootableVfx : MonoBehaviour, IRetroPoolLifecycle
    {
        protected RetroPooledObject pooledObject;
        protected MeshFilter meshFilter;
        protected MeshRenderer meshRenderer;
        protected Material material;
        protected Color baseColor;
        protected float lifetime;
        protected float age;

        public void Configure(MeshFilter filter, MeshRenderer renderer)
        {
            meshFilter = filter;
            meshRenderer = renderer;
            material = CreateVfxMaterial(GetType().Name);
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = material;
                meshRenderer.enabled = false;
            }
        }

        public virtual void OnPoolRent(RetroPooledObject pooledObject)
        {
            this.pooledObject = pooledObject;
            age = 0f;
            SetVisible(true);
        }

        public virtual void OnPoolReturn(RetroPooledObject pooledObject)
        {
            age = 0f;
            SetVisible(false);
        }

        public virtual void OnPoolDestroy(RetroPooledObject pooledObject)
        {
            if (material != null)
            {
                Destroy(material);
                material = null;
            }

            this.pooledObject = null;
        }

        protected void SetVisible(bool visible)
        {
            if (meshRenderer != null)
            {
                meshRenderer.enabled = visible;
            }
        }

        protected void ReturnToPool()
        {
            if (pooledObject != null && pooledObject.IsRented)
            {
                pooledObject.ReturnToPool();
                return;
            }

            Destroy(gameObject);
        }

        protected void ApplyFade(float normalizedAge)
        {
            Color color = baseColor;
            color.a *= 1f - Mathf.SmoothStep(0f, 1f, normalizedAge);
            SetVisible(color.a > 0.001f);
            ApplyMaterialColor(material, color);
        }
    }

    private sealed class ShootableParticle : PooledShootableVfx
    {
        private Vector3 velocity;
        private Vector3 angularVelocity;
        private Vector3 baseScale;
        private float gravity;
        private float drag;
        private bool billboard;
        private bool growOverLife;

        public void Play(Mesh mesh, Color color, Vector3 scale, Vector3 initialVelocity, Vector3 spin, float gravityStrength, float dragStrength, float lifeSeconds, bool faceCamera, bool grow)
        {
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = mesh;
            }

            baseColor = color;
            baseScale = scale;
            velocity = initialVelocity;
            angularVelocity = spin;
            gravity = Mathf.Max(0f, gravityStrength);
            drag = Mathf.Max(0f, dragStrength);
            lifetime = Mathf.Max(0.05f, lifeSeconds);
            billboard = faceCamera;
            growOverLife = grow;
            transform.localScale = baseScale;
            ApplyMaterialColor(material, baseColor);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            age += deltaTime;
            float normalizedAge = Mathf.Clamp01(age / lifetime);
            transform.position += velocity * deltaTime;
            velocity += Vector3.down * gravity * deltaTime;
            velocity *= Mathf.Exp(-drag * deltaTime);

            if (billboard && Camera.main != null)
            {
                Vector3 toCamera = transform.position - Camera.main.transform.position;
                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(toCamera.normalized, Camera.main.transform.up);
                }
            }
            else
            {
                transform.Rotate(angularVelocity * deltaTime, Space.Self);
            }

            float scale = growOverLife
                ? Mathf.Lerp(0.55f, 1.85f, Mathf.SmoothStep(0f, 1f, normalizedAge))
                : Mathf.Lerp(1f, 0.72f, normalizedAge);
            transform.localScale = baseScale * scale;
            ApplyFade(normalizedAge);

            if (normalizedAge >= 1f)
            {
                ReturnToPool();
            }
        }
    }

    private sealed class ShootableDecal : PooledShootableVfx
    {
        private float baseScale;

        public void Play(Mesh mesh, Color color, float scale, float lifeSeconds)
        {
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = mesh;
            }

            baseColor = color;
            baseScale = Mathf.Max(0.001f, scale);
            lifetime = Mathf.Max(0.05f, lifeSeconds);
            transform.localScale = Vector3.one * baseScale;
            ApplyMaterialColor(material, baseColor);
        }

        private void Update()
        {
            age += Time.deltaTime;
            float normalizedAge = Mathf.Clamp01(age / lifetime);
            float scale = Mathf.Lerp(baseScale, baseScale * 0.92f, normalizedAge);
            transform.localScale = Vector3.one * scale;
            ApplyFade(normalizedAge);

            if (normalizedAge >= 1f)
            {
                ReturnToPool();
            }
        }
    }
}
