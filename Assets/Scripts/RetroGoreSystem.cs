using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public static class RetroGoreSystem
{
    private const string SpriteShaderName = "Ultraloud/Effects/Gore Sprite HDRP";
    private const int MaxBounces = 2;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
    private static readonly int PackedMasksId = Shader.PropertyToID("_PackedMasks");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int FrameUvRectId = Shader.PropertyToID("_FrameUvRect");
    private static readonly int UseNormalMapId = Shader.PropertyToID("_UseNormalMap");
    private static readonly int UsePackedMasksId = Shader.PropertyToID("_UsePackedMasks");
    private static readonly int UseEmissionMapId = Shader.PropertyToID("_UseEmissionMap");

    private static RetroComponentPool<GoreSpriteParticle> spritePool;
    private static RetroComponentPool<GoreMeshChunk> meshPool;
    private static Mesh chunkMesh;

    public static void SpawnGoreBurst(
        RetroGoreProfile profile,
        Vector3 center,
        Vector3 hitPoint,
        Vector3 hitNormal,
        Transform victim,
        float intensity,
        GameObject source)
    {
        if (profile == null || intensity <= 0f)
        {
            return;
        }

        intensity = Mathf.Clamp(intensity * Mathf.Max(0.01f, profile.IntensityScale), 0.35f, 3f);
        Vector3 safeNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.up;
        BuildSurfaceBasis(safeNormal, out Vector3 tangent, out Vector3 bitangent);

        RetroGameContext.Vfx.SpawnExplosionFlash(
            center,
            new Color(0.88f, 0.02f, 0.015f, 0.85f),
            profile.ScreenFlashRadius * Mathf.Sqrt(intensity),
            0.14f);

        SpawnPuffs(profile, center, safeNormal, tangent, bitangent, intensity);
        SpawnStreaks(profile, center, safeNormal, tangent, bitangent, intensity);
        SpawnSpriteChunks(profile, center, safeNormal, tangent, bitangent, intensity);
        SpawnMeshChunks(profile, center, safeNormal, tangent, bitangent, victim, intensity);
        SpawnDecals(profile, center, hitPoint, safeNormal, tangent, bitangent, intensity);
    }

    private static void SpawnPuffs(RetroGoreProfile profile, Vector3 center, Vector3 normal, Vector3 tangent, Vector3 bitangent, float intensity)
    {
        int count = Mathf.RoundToInt(profile.BloodPuffCount * intensity);
        for (int i = 0; i < count; i++)
        {
            Vector3 direction = RandomBurstDirection(normal, tangent, bitangent, profile.ForwardBias);
            float size = RandomRange(profile.PuffSizeRange) * Random.Range(0.8f, 1.35f) * Mathf.Lerp(0.85f, 1.25f, intensity - 0.75f);
            Vector3 position = center + Random.insideUnitSphere * profile.SpawnRadius * Random.Range(0.2f, 1f);
            Vector3 velocity = direction * RandomRange(profile.RadialSpeedRange) * Random.Range(0.55f, 1.05f)
                + Vector3.up * RandomRange(profile.UpwardSpeedRange) * 0.45f;
            Color tint = Color.Lerp(new Color(0.78f, 0.02f, 0.015f, 0.96f), new Color(1f, 0.18f, 0.08f, 0.9f), Random.value * 0.35f);
            PlaySprite(
                profile,
                profile.RandomBloodFrame(),
                "GoreBloodPuff",
                position,
                Quaternion.LookRotation(direction, Vector3.up),
                new Vector2(size, size * Random.Range(0.75f, 1.25f)),
                tint,
                velocity,
                RandomRange(profile.GravityRange) * 0.4f,
                Random.Range(-420f, 420f),
                RandomRange(profile.SpriteLifetimeRange) * Random.Range(0.55f, 0.95f),
                billboard: true,
                shrink: true,
                collideAndStick: false);
        }
    }

    private static void SpawnStreaks(RetroGoreProfile profile, Vector3 center, Vector3 normal, Vector3 tangent, Vector3 bitangent, float intensity)
    {
        int count = Mathf.RoundToInt(profile.StreakCount * intensity);
        for (int i = 0; i < count; i++)
        {
            Vector3 direction = RandomBurstDirection(normal, tangent, bitangent, profile.ForwardBias + 0.15f);
            float size = RandomRange(profile.StreakSizeRange) * Mathf.Lerp(0.75f, 1.15f, intensity - 0.75f);
            Vector3 position = center + Random.insideUnitSphere * profile.SpawnRadius * 0.7f;
            Vector3 velocity = direction * RandomRange(profile.RadialSpeedRange) * Random.Range(0.95f, 1.55f)
                + Vector3.up * RandomRange(profile.UpwardSpeedRange) * 0.35f;
            Color tint = Color.Lerp(new Color(0.62f, 0.01f, 0.012f, 0.94f), new Color(1f, 0.07f, 0.02f, 0.88f), Random.value * 0.42f);
            PlaySprite(
                profile,
                profile.RandomStreakFrame(),
                "GoreBloodStreak",
                position,
                Quaternion.LookRotation(direction, Vector3.up),
                new Vector2(size * Random.Range(0.45f, 0.8f), size * Random.Range(1.25f, 2.1f)),
                tint,
                velocity,
                RandomRange(profile.GravityRange) * 0.55f,
                Random.Range(-720f, 720f),
                RandomRange(profile.SpriteLifetimeRange) * Random.Range(0.65f, 1.1f),
                billboard: true,
                shrink: true,
                collideAndStick: false);
        }
    }

    private static void SpawnSpriteChunks(RetroGoreProfile profile, Vector3 center, Vector3 normal, Vector3 tangent, Vector3 bitangent, float intensity)
    {
        int meatCount = Mathf.RoundToInt(profile.SpriteChunkCount * intensity);
        for (int i = 0; i < meatCount; i++)
        {
            SpawnChunkSprite(profile, center, normal, tangent, bitangent, profile.RandomMeatFrame(), Random.value < 0.18f);
        }

        int boneCount = Mathf.RoundToInt(profile.BoneSpriteCount * intensity);
        for (int i = 0; i < boneCount; i++)
        {
            SpawnChunkSprite(profile, center, normal, tangent, bitangent, profile.RandomBoneFrame(), true);
        }
    }

    private static void SpawnChunkSprite(RetroGoreProfile profile, Vector3 center, Vector3 normal, Vector3 tangent, Vector3 bitangent, int frame, bool brighter)
    {
        Vector3 direction = RandomBurstDirection(normal, tangent, bitangent, profile.ForwardBias);
        float size = RandomRange(profile.ChunkSizeRange) * Random.Range(0.78f, 1.35f);
        Vector3 position = center + Random.insideUnitSphere * profile.SpawnRadius * Random.Range(0.1f, 0.9f);
        Vector3 velocity = direction * RandomRange(profile.RadialSpeedRange) * Random.Range(0.7f, 1.35f)
            + Vector3.up * RandomRange(profile.UpwardSpeedRange);
        Color tint = brighter
            ? Color.Lerp(Color.white, new Color(1f, 0.82f, 0.62f, 1f), Random.value * 0.45f)
            : Color.Lerp(new Color(0.95f, 0.42f, 0.36f, 1f), Color.white, Random.value * 0.18f);

        PlaySprite(
            profile,
            frame,
            "GoreSpriteChunk",
            position,
            Random.rotation,
            new Vector2(size * Random.Range(0.8f, 1.35f), size * Random.Range(0.75f, 1.25f)),
            tint,
            velocity,
            RandomRange(profile.GravityRange),
            Random.Range(-900f, 900f),
            RandomRange(profile.SpriteLifetimeRange) * Random.Range(1f, 1.55f),
            billboard: true,
            shrink: false,
            collideAndStick: true);
    }

    private static void SpawnMeshChunks(RetroGoreProfile profile, Vector3 center, Vector3 normal, Vector3 tangent, Vector3 bitangent, Transform victim, float intensity)
    {
        int count = Mathf.RoundToInt(profile.MeshChunkCount * intensity);
        for (int i = 0; i < count; i++)
        {
            GoreMeshChunk chunk = MeshPool?.Rent(center + Random.insideUnitSphere * profile.SpawnRadius, Random.rotation);
            if (chunk == null)
            {
                continue;
            }

            Vector3 direction = RandomBurstDirection(normal, tangent, bitangent, profile.ForwardBias);
            Vector3 velocity = direction * RandomRange(profile.RadialSpeedRange) * Random.Range(0.7f, 1.45f)
                + Vector3.up * RandomRange(profile.UpwardSpeedRange);
            float size = RandomRange(profile.MeshChunkSizeRange) * Random.Range(0.8f, 1.5f) * Mathf.Lerp(0.8f, 1.2f, intensity - 0.75f);
            Color color = ResolveChunkColor(profile);
            chunk.Play(profile, color, size, velocity, RandomRange(profile.GravityRange), Random.rotationUniform.eulerAngles * Random.Range(2.5f, 7.5f), RandomRange(profile.MeshChunkLifetimeRange));
        }
    }

    private static void SpawnDecals(RetroGoreProfile profile, Vector3 center, Vector3 hitPoint, Vector3 normal, Vector3 tangent, Vector3 bitangent, float intensity)
    {
        int count = Mathf.RoundToInt(profile.DecalCount * Mathf.Lerp(0.8f, 1.35f, intensity - 0.75f));
        for (int i = 0; i < count; i++)
        {
            Vector3 origin = center + Random.insideUnitSphere * profile.SpawnRadius * 0.45f;
            Vector3 castDirection = (Random.value < 0.62f ? Vector3.down : RandomBurstDirection(normal, tangent, bitangent, 0.25f)).normalized;
            Vector3 decalPosition = hitPoint + normal * 0.025f;
            Vector3 decalNormal = normal;
            if (Physics.Raycast(origin, castDirection, out RaycastHit hit, 5.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                decalPosition = hit.point + hit.normal * 0.018f;
                decalNormal = hit.normal;
            }
            else
            {
                Vector2 scatter = Random.insideUnitCircle * Random.Range(0.15f, 1.75f);
                decalPosition = hitPoint + tangent * scatter.x + bitangent * scatter.y + normal * (0.02f + i * 0.002f);
            }

            float size = RandomRange(profile.DecalSizeRange) * Random.Range(0.55f, 1.25f) * Mathf.Lerp(0.8f, 1.2f, intensity - 0.75f);
            PlaySprite(
                profile,
                Random.value < 0.72f ? profile.RandomBloodFrame() : profile.RandomClusterFrame(),
                "GoreDecal",
                decalPosition,
                ResolveSurfaceRotation(decalNormal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)),
                new Vector2(size * Random.Range(0.7f, 1.5f), size * Random.Range(0.55f, 1.25f)),
                new Color(0.55f, 0.01f, 0.008f, Random.Range(0.78f, 0.95f)),
                Vector3.zero,
                0f,
                0f,
                RandomRange(profile.DecalLifetimeRange),
                billboard: false,
                shrink: false,
                collideAndStick: false);
        }
    }

    internal static void SpawnImpactDecal(RetroGoreProfile profile, Vector3 position, Vector3 normal, float sizeMultiplier)
    {
        if (profile == null || profile.BaseAtlas == null)
        {
            return;
        }

        float size = RandomRange(profile.DecalSizeRange) * Random.Range(0.25f, 0.55f) * Mathf.Max(0.2f, sizeMultiplier);
        PlaySprite(
            profile,
            profile.RandomBloodFrame(),
            "GoreImpactDecal",
            position + normal.normalized * 0.018f,
            ResolveSurfaceRotation(normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)),
            new Vector2(size * Random.Range(0.7f, 1.45f), size * Random.Range(0.55f, 1.2f)),
            new Color(0.45f, 0.008f, 0.006f, Random.Range(0.72f, 0.88f)),
            Vector3.zero,
            0f,
            0f,
            RandomRange(profile.DecalLifetimeRange) * Random.Range(0.55f, 0.95f),
            billboard: false,
            shrink: false,
            collideAndStick: false);
    }

    private static void PlaySprite(
        RetroGoreProfile profile,
        int frame,
        string label,
        Vector3 position,
        Quaternion rotation,
        Vector2 size,
        Color tint,
        Vector3 velocity,
        float gravity,
        float angularSpeed,
        float lifetime,
        bool billboard,
        bool shrink,
        bool collideAndStick)
    {
        if (profile.BaseAtlas == null)
        {
            return;
        }

        GoreSpriteParticle particle = SpritePool?.Rent(position, rotation);
        if (particle == null)
        {
            return;
        }

        particle.Play(profile, frame, label, tint, size, velocity, gravity, angularSpeed, lifetime, billboard, shrink, collideAndStick);
    }

    private static RetroComponentPool<GoreSpriteParticle> SpritePool
    {
        get
        {
            if (spritePool == null || !spritePool.IsValid)
            {
                spritePool = RetroPoolService.Shared.GetOrCreateComponentPool(
                    "RetroGoreSystem.SpriteParticles",
                    CreateSpriteParticle,
                    new RetroPoolSettings(prewarmCount: 64, maxInactiveCount: 768));
            }

            return spritePool;
        }
    }

    private static RetroComponentPool<GoreMeshChunk> MeshPool
    {
        get
        {
            if (meshPool == null || !meshPool.IsValid)
            {
                meshPool = RetroPoolService.Shared.GetOrCreateComponentPool(
                    "RetroGoreSystem.MeshChunks",
                    CreateMeshChunk,
                    new RetroPoolSettings(prewarmCount: 48, maxInactiveCount: 512));
            }

            return meshPool;
        }
    }

    private static GoreSpriteParticle CreateSpriteParticle(Transform parent)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "GoreSpriteParticle";
        quad.transform.SetParent(parent, false);

        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }

        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        GoreSpriteParticle particle = quad.AddComponent<GoreSpriteParticle>();
        particle.Configure(renderer);
        return particle;
    }

    private static GoreMeshChunk CreateMeshChunk(Transform parent)
    {
        GameObject chunkObject = new("GoreMeshChunk");
        chunkObject.transform.SetParent(parent, false);
        MeshFilter filter = chunkObject.AddComponent<MeshFilter>();
        filter.sharedMesh = ChunkMesh;
        MeshRenderer renderer = chunkObject.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        GoreMeshChunk chunk = chunkObject.AddComponent<GoreMeshChunk>();
        chunk.Configure(renderer);
        return chunk;
    }

    private static Mesh ChunkMesh
    {
        get
        {
            if (chunkMesh != null)
            {
                return chunkMesh;
            }

            chunkMesh = new Mesh
            {
                name = "Retro Gore Lowpoly Chunk"
            };
            Vector3[] vertices =
            {
                new(-0.48f, -0.22f, -0.3f),
                new(0.52f, -0.18f, -0.24f),
                new(0.16f, 0.5f, -0.18f),
                new(-0.22f, 0.18f, 0.55f),
                new(0.3f, -0.42f, 0.28f),
                new(-0.56f, -0.36f, 0.12f)
            };
            int[] triangles =
            {
                0, 2, 1, 0, 3, 2, 0, 5, 3, 0, 1, 5,
                1, 2, 4, 1, 4, 5, 2, 3, 4, 3, 5, 4,
                2, 0, 3, 4, 3, 2
            };
            chunkMesh.vertices = vertices;
            chunkMesh.triangles = triangles;
            chunkMesh.RecalculateNormals();
            chunkMesh.RecalculateBounds();
            return chunkMesh;
        }
    }

    private static Color ResolveChunkColor(RetroGoreProfile profile)
    {
        float roll = Random.value;
        if (roll < 0.18f)
        {
            return Color.Lerp(profile.BoneChunkColor, Color.white, Random.value * 0.22f);
        }

        if (roll < 0.48f)
        {
            return Color.Lerp(profile.SkinChunkColor, profile.MeatChunkColor, Random.Range(0.15f, 0.45f));
        }

        return Color.Lerp(profile.MeatChunkColor, new Color(0.85f, 0.04f, 0.025f, 1f), Random.value * 0.28f);
    }

    private static Vector3 RandomBurstDirection(Vector3 normal, Vector3 tangent, Vector3 bitangent, float forwardBias)
    {
        Vector2 disc = Random.insideUnitCircle;
        Vector3 lateral = tangent * disc.x + bitangent * disc.y;
        Vector3 direction = normal * Mathf.Max(0f, forwardBias) + lateral * Random.Range(0.45f, 1.35f) + Vector3.up * Random.Range(-0.1f, 0.6f);
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Random.onUnitSphere;
        }

        return direction.normalized;
    }

    private static void BuildSurfaceBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
    {
        Vector3 seed = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.92f ? Vector3.forward : Vector3.up;
        tangent = Vector3.Cross(seed, normal).normalized;
        bitangent = Vector3.Cross(normal, tangent).normalized;
    }

    private static Quaternion ResolveSurfaceRotation(Vector3 normal)
    {
        Vector3 safeNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        Vector3 up = Mathf.Abs(Vector3.Dot(safeNormal, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
        return Quaternion.LookRotation(safeNormal, up);
    }

    private static float RandomRange(Vector2 range)
    {
        return Random.Range(range.x, range.y);
    }

    private static Material CreateSpriteMaterial()
    {
        Shader shader = Shader.Find(SpriteShaderName);
        shader ??= Shader.Find("HDRP/Unlit");
        shader ??= Shader.Find("Unlit/Transparent");
        shader ??= Shader.Find("Sprites/Default");

        Material material = new(shader)
        {
            name = "Generated Gore Sprite Material",
            renderQueue = (int)RenderQueue.Transparent
        };
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_BLENDMODE_ALPHA");
        SetFloatIfPresent(material, 1f, "_SurfaceType");
        SetFloatIfPresent(material, 0f, "_BlendMode");
        SetFloatIfPresent(material, (float)CullMode.Off, "_CullMode", "_CullModeForward");
        SetFloatIfPresent(material, (float)BlendMode.SrcAlpha, "_SrcBlend");
        SetFloatIfPresent(material, (float)BlendMode.OneMinusSrcAlpha, "_DstBlend");
        SetFloatIfPresent(material, (float)BlendMode.One, "_AlphaSrcBlend");
        SetFloatIfPresent(material, (float)BlendMode.OneMinusSrcAlpha, "_AlphaDstBlend");
        return material;
    }

    private static Material CreateChunkMaterial()
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        shader ??= Shader.Find("Universal Render Pipeline/Unlit");
        shader ??= Shader.Find("Standard");
        return new Material(shader)
        {
            name = "Generated Gore Mesh Chunk Material"
        };
    }

    private static void SetTextureIfPresent(Material material, Texture texture, params string[] propertyNames)
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

    private static void SetColorIfPresent(Material material, Color value, params string[] propertyNames)
    {
        if (material == null)
        {
            return;
        }

        for (int i = 0; i < propertyNames.Length; i++)
        {
            if (material.HasProperty(propertyNames[i]))
            {
                material.SetColor(propertyNames[i], value);
            }
        }
    }

    private static void SetFloatIfPresent(Material material, float value, params string[] propertyNames)
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

    private static void DestroyMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(material);
        }
        else
        {
            Object.DestroyImmediate(material);
        }
    }

    private sealed class GoreSpriteParticle : MonoBehaviour, IRetroPoolLifecycle
    {
        private RetroPooledObject pooledObject;
        private Renderer particleRenderer;
        private Material material;
        private RetroGoreProfile profile;
        private Color initialTint;
        private Vector3 initialScale;
        private Vector3 velocity;
        private float gravity;
        private float angularSpeed;
        private float lifetime;
        private float age;
        private float roll;
        private bool billboard;
        private bool shrink;
        private bool collideAndStick;
        private bool stuck;

        public void Configure(Renderer renderer)
        {
            particleRenderer = renderer;
            material = CreateSpriteMaterial();
            if (particleRenderer != null)
            {
                particleRenderer.sharedMaterial = material;
            }
        }

        public void Play(RetroGoreProfile goreProfile, int frame, string label, Color tint, Vector2 size, Vector3 initialVelocity, float gravityStrength, float spinDegrees, float lifeSeconds, bool faceCamera, bool shrinkOverLife, bool stickOnCollision)
        {
            profile = goreProfile;
            gameObject.name = label;
            EnsureMaterial();
            ApplyProfile(frame, tint);

            transform.localScale = new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), 1f);
            initialScale = transform.localScale;
            velocity = initialVelocity;
            gravity = Mathf.Max(0f, gravityStrength);
            angularSpeed = spinDegrees;
            lifetime = Mathf.Max(0.01f, lifeSeconds);
            age = 0f;
            roll = transform.eulerAngles.z;
            billboard = faceCamera;
            shrink = shrinkOverLife;
            collideAndStick = stickOnCollision;
            stuck = false;
            initialTint = tint;
            SetVisible(true);
        }

        public void OnPoolRent(RetroPooledObject pooledObject)
        {
            this.pooledObject = pooledObject;
            age = 0f;
            stuck = false;
            SetVisible(true);
        }

        public void OnPoolReturn(RetroPooledObject pooledObject)
        {
            profile = null;
            velocity = Vector3.zero;
            SetVisible(false);
        }

        public void OnPoolDestroy(RetroPooledObject pooledObject)
        {
            DestroyMaterial(material);
            material = null;
            this.pooledObject = null;
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            age += deltaTime;
            if (age >= lifetime)
            {
                pooledObject?.ReturnToPool();
                return;
            }

            if (!stuck)
            {
                Vector3 previous = transform.position;
                velocity += Vector3.down * gravity * deltaTime;
                velocity *= Mathf.Exp(-1.15f * deltaTime);
                Vector3 next = previous + velocity * deltaTime;

                if (collideAndStick && velocity.sqrMagnitude > 0.01f && Physics.Linecast(previous, next, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                {
                    transform.position = hit.point + hit.normal * 0.018f;
                    transform.rotation = ResolveSurfaceRotation(hit.normal) * Quaternion.Euler(0f, 0f, roll);
                    velocity = Vector3.zero;
                    gravity = 0f;
                    angularSpeed = 0f;
                    billboard = false;
                    shrink = false;
                    stuck = true;
                    lifetime = Mathf.Max(lifetime - age, Random.Range(3.5f, 7.5f));
                    age = 0f;
                    RetroGoreSystem.SpawnImpactDecal(profile, hit.point, hit.normal, Mathf.Max(initialScale.x, initialScale.y));
                }
                else
                {
                    transform.position = next;
                }
            }

            roll += angularSpeed * deltaTime;
            if (billboard)
            {
                Camera camera = Camera.main;
                if (camera != null)
                {
                    Vector3 toCamera = transform.position - camera.transform.position;
                    if (toCamera.sqrMagnitude > 0.0001f)
                    {
                        transform.rotation = Quaternion.LookRotation(toCamera.normalized, camera.transform.up)
                            * Quaternion.Euler(0f, 0f, roll);
                    }
                }
                else
                {
                    transform.Rotate(0f, 0f, angularSpeed * deltaTime, Space.Self);
                }
            }
            else if (!stuck && Mathf.Abs(angularSpeed) > 0.001f)
            {
                transform.Rotate(0f, 0f, angularSpeed * deltaTime, Space.Self);
            }

            float normalizedAge = Mathf.Clamp01(age / lifetime);
            float fadeStart = stuck ? 0.78f : (shrink ? 0.08f : 0.56f);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStart, 1f, normalizedAge));
            Color tint = initialTint;
            tint.a *= fade;
            SetColorIfPresent(material, tint, "_BaseColor", "_UnlitColor", "_Color");

            if (shrink)
            {
                float scale = Mathf.Lerp(1f, 0.28f, Mathf.SmoothStep(0f, 1f, normalizedAge));
                transform.localScale = initialScale * scale;
            }
        }

        private void EnsureMaterial()
        {
            if (material == null)
            {
                material = CreateSpriteMaterial();
                if (particleRenderer != null)
                {
                    particleRenderer.sharedMaterial = material;
                }
            }
        }

        private void ApplyProfile(int frame, Color tint)
        {
            if (material == null || profile == null)
            {
                return;
            }

            SetTextureIfPresent(material, profile.BaseAtlas, "_BaseMap", "_BaseColorMap", "_UnlitColorMap", "_MainTex");
            SetTextureIfPresent(material, profile.NormalAtlas, "_NormalMap");
            SetTextureIfPresent(material, profile.PackedMasksAtlas, "_PackedMasks");
            SetTextureIfPresent(material, profile.EmissionAtlas, "_EmissionMap");
            SetColorIfPresent(material, tint, "_BaseColor", "_UnlitColor", "_Color");
            if (material.HasProperty(FrameUvRectId))
            {
                material.SetVector(FrameUvRectId, ResolveFrameRect(profile, frame));
            }

            if (material.HasProperty(UseNormalMapId))
            {
                material.SetFloat(UseNormalMapId, profile.NormalAtlas != null ? 1f : 0f);
            }

            if (material.HasProperty(UsePackedMasksId))
            {
                material.SetFloat(UsePackedMasksId, profile.PackedMasksAtlas != null ? 1f : 0f);
            }

            if (material.HasProperty(UseEmissionMapId))
            {
                material.SetFloat(UseEmissionMapId, profile.EmissionAtlas != null ? 1f : 0f);
            }
        }

        private static Vector4 ResolveFrameRect(RetroGoreProfile profile, int frame)
        {
            int columns = profile.AtlasColumns;
            int rows = profile.AtlasRows;
            frame = Mathf.Clamp(frame, 0, profile.FrameCount - 1);
            int column = frame % columns;
            int row = frame / columns;
            float invColumns = 1f / columns;
            float invRows = 1f / rows;
            float uMin = column * invColumns;
            float vMax = 1f - row * invRows;
            float vMin = vMax - invRows;
            return new Vector4(uMin, vMin, invColumns, invRows);
        }

        private void SetVisible(bool visible)
        {
            if (particleRenderer != null)
            {
                particleRenderer.enabled = visible;
            }
        }
    }

    private sealed class GoreMeshChunk : MonoBehaviour, IRetroPoolLifecycle
    {
        private RetroPooledObject pooledObject;
        private Renderer chunkRenderer;
        private Material material;
        private RetroGoreProfile profile;
        private Vector3 velocity;
        private Vector3 angularVelocity;
        private Vector3 baseScale;
        private float gravity;
        private float lifetime;
        private float age;
        private int bounces;
        private bool spawnedImpactDecal;

        public void Configure(Renderer renderer)
        {
            chunkRenderer = renderer;
            material = CreateChunkMaterial();
            if (chunkRenderer != null)
            {
                chunkRenderer.sharedMaterial = material;
            }
        }

        public void Play(RetroGoreProfile goreProfile, Color color, float size, Vector3 initialVelocity, float gravityStrength, Vector3 spinDegrees, float lifeSeconds)
        {
            profile = goreProfile;
            gameObject.name = "GoreMeshChunk";
            EnsureMaterial();
            SetColorIfPresent(material, color, "_BaseColor", "_UnlitColor", "_Color");
            transform.localScale = new Vector3(
                size * Random.Range(0.65f, 1.45f),
                size * Random.Range(0.55f, 1.25f),
                size * Random.Range(0.65f, 1.55f));
            baseScale = transform.localScale;
            velocity = initialVelocity;
            angularVelocity = spinDegrees;
            gravity = Mathf.Max(0f, gravityStrength);
            lifetime = Mathf.Max(0.01f, lifeSeconds);
            age = 0f;
            bounces = 0;
            spawnedImpactDecal = false;
            SetVisible(true);
        }

        public void OnPoolRent(RetroPooledObject pooledObject)
        {
            this.pooledObject = pooledObject;
            age = 0f;
            SetVisible(true);
        }

        public void OnPoolReturn(RetroPooledObject pooledObject)
        {
            velocity = Vector3.zero;
            angularVelocity = Vector3.zero;
            profile = null;
            SetVisible(false);
        }

        public void OnPoolDestroy(RetroPooledObject pooledObject)
        {
            DestroyMaterial(material);
            material = null;
            this.pooledObject = null;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            age += deltaTime;
            if (age >= lifetime)
            {
                pooledObject?.ReturnToPool();
                return;
            }

            Vector3 previous = transform.position;
            velocity += Vector3.down * gravity * deltaTime;
            velocity *= Mathf.Exp(-0.42f * deltaTime);
            Vector3 next = previous + velocity * deltaTime;

            if (velocity.sqrMagnitude > 0.01f && Physics.Linecast(previous, next, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + hit.normal * 0.025f;
                if (!spawnedImpactDecal)
                {
                    RetroGoreSystem.SpawnImpactDecal(profile, hit.point, hit.normal, Mathf.Max(baseScale.x, baseScale.z) * 5f);
                    spawnedImpactDecal = true;
                }

                bounces++;
                if (bounces >= MaxBounces)
                {
                    velocity = Vector3.zero;
                    angularVelocity = Vector3.zero;
                    gravity = 0f;
                }
                else
                {
                    velocity = Vector3.Reflect(velocity, hit.normal) * Random.Range(0.18f, 0.36f);
                    angularVelocity *= 0.45f;
                }
            }
            else
            {
                transform.position = next;
            }

            transform.Rotate(angularVelocity * deltaTime, Space.Self);
            float normalizedAge = Mathf.Clamp01(age / lifetime);
            float scale = Mathf.Lerp(1f, 0.55f, Mathf.SmoothStep(0.72f, 1f, normalizedAge));
            transform.localScale = baseScale * scale;
        }

        private void EnsureMaterial()
        {
            if (material == null)
            {
                material = CreateChunkMaterial();
                if (chunkRenderer != null)
                {
                    chunkRenderer.sharedMaterial = material;
                }
            }
        }

        private void SetVisible(bool visible)
        {
            if (chunkRenderer != null)
            {
                chunkRenderer.enabled = visible;
            }
        }
    }
}
