using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class RetroVfxService : MonoBehaviour
{
    private RetroComponentPool<PooledBulletTrail> bulletTrailPool;
    private RetroComponentPool<PooledFlash> flashPool;

    public void SpawnBulletTrail(string label, Vector3 start, Vector3 end, Color color, float width, float duration)
    {
        if ((end - start).sqrMagnitude <= 0.000001f || width <= 0f || duration <= 0f)
        {
            return;
        }

        EnsureBulletTrailPool();
        PooledBulletTrail trail = bulletTrailPool?.Rent();
        if (trail == null)
        {
            return;
        }

        trail.Play(label, start, end, color, width, duration);
    }

    public void SpawnImpactFlash(Vector3 position, Vector3 normal, Color color, float scale = 0.06f, float duration = 0.08f)
    {
        Vector3 safeNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        SpawnFlash("ImpactFlash", position + safeNormal * 0.03f, Quaternion.LookRotation(safeNormal, Vector3.up), color, scale, duration);
    }

    public void SpawnExplosionFlash(Vector3 position, Color color, float radius, float duration = 0.12f)
    {
        SpawnFlash("ExplosionFlash", position, Quaternion.identity, color, Mathf.Max(0.05f, radius * 0.55f), duration);
    }

    private void SpawnFlash(string label, Vector3 position, Quaternion rotation, Color color, float scale, float duration)
    {
        if (scale <= 0f || duration <= 0f)
        {
            return;
        }

        EnsureFlashPool();
        PooledFlash flash = flashPool?.Rent(position, rotation);
        if (flash == null)
        {
            return;
        }

        flash.Play(label, color, scale, duration);
    }

    private void EnsureBulletTrailPool()
    {
        if (bulletTrailPool != null && bulletTrailPool.IsValid)
        {
            return;
        }

        bulletTrailPool = RetroGameContext.Pools.GetOrCreateComponentPool(
            "RetroVfxService.BulletTrails",
            CreateBulletTrail,
            new RetroPoolSettings(prewarmCount: 32, maxInactiveCount: 256));
    }

    private void EnsureFlashPool()
    {
        if (flashPool != null && flashPool.IsValid)
        {
            return;
        }

        flashPool = RetroGameContext.Pools.GetOrCreateComponentPool(
            "RetroVfxService.Flashes",
            CreateFlash,
            new RetroPoolSettings(prewarmCount: 16, maxInactiveCount: 128));
    }

    private static PooledBulletTrail CreateBulletTrail(Transform parent)
    {
        GameObject trailObject = new GameObject("PooledBulletTrail");
        trailObject.transform.SetParent(parent, false);
        LineRenderer glow = trailObject.AddComponent<LineRenderer>();
        LineRenderer core = trailObject.AddComponent<LineRenderer>();
        PooledBulletTrail trail = trailObject.AddComponent<PooledBulletTrail>();
        trail.Configure(core, glow);
        return trail;
    }

    private static PooledFlash CreateFlash(Transform parent)
    {
        GameObject flashObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flashObject.name = "PooledFlash";
        flashObject.transform.SetParent(parent, false);

        Collider collider = flashObject.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }

        Renderer renderer = flashObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            Destroy(flashObject);
            return null;
        }

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        PooledFlash flash = flashObject.AddComponent<PooledFlash>();
        flash.Configure(renderer);
        return flash;
    }

    private sealed class PooledBulletTrail : MonoBehaviour, IRetroPoolLifecycle
    {
        private RetroPooledObject pooledObject;
        private LineRenderer coreLine;
        private LineRenderer glowLine;
        private Material coreMaterial;
        private Material glowMaterial;
        private Color color;
        private float duration;
        private float age;
        private float baseWidth;

        public void Configure(LineRenderer core, LineRenderer glow)
        {
            coreLine = core;
            glowLine = glow;
            coreMaterial = CreateTransparentMaterial("Bullet Trail Core", additive: true);
            glowMaterial = CreateTransparentMaterial("Bullet Trail Glow", additive: true);
            ConfigureLine(coreLine, coreMaterial);
            ConfigureLine(glowLine, glowMaterial);
        }

        public void Play(string label, Vector3 start, Vector3 end, Color tint, float width, float lifetime)
        {
            gameObject.name = string.IsNullOrWhiteSpace(label) ? "BulletTrail" : $"{label} BulletTrail";
            color = tint;
            duration = Mathf.Max(0.01f, lifetime);
            age = 0f;
            baseWidth = Mathf.Max(0.001f, width);
            ApplyEndpoints(coreLine, start, end);
            ApplyEndpoints(glowLine, start, end);
            Apply(0f);
        }

        public void OnPoolRent(RetroPooledObject pooledObject)
        {
            this.pooledObject = pooledObject;
            age = 0f;
            SetLinesEnabled(true);
        }

        public void OnPoolReturn(RetroPooledObject pooledObject)
        {
            age = 0f;
            SetLinesEnabled(false);
        }

        public void OnPoolDestroy(RetroPooledObject pooledObject)
        {
            DestroyRuntimeMaterial(coreMaterial);
            DestroyRuntimeMaterial(glowMaterial);
            coreMaterial = null;
            glowMaterial = null;
            this.pooledObject = null;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float normalizedAge = Mathf.Clamp01(age / duration);
            Apply(normalizedAge);
            if (normalizedAge >= 1f)
            {
                pooledObject?.ReturnToPool();
            }
        }

        private void Apply(float normalizedAge)
        {
            float fade = 1f - Mathf.SmoothStep(0f, 1f, normalizedAge);
            float shrink = Mathf.Lerp(1f, 0.16f, normalizedAge);
            ApplyLine(glowLine, glowMaterial, color, fade, shrink, baseWidth * 2.8f, 0.24f);
            ApplyLine(coreLine, coreMaterial, color, fade, shrink, baseWidth, 1f);
        }

        private void SetLinesEnabled(bool enabled)
        {
            if (coreLine != null) coreLine.enabled = enabled;
            if (glowLine != null) glowLine.enabled = enabled;
        }

        private static void ConfigureLine(LineRenderer line, Material material)
        {
            if (line == null)
            {
                return;
            }

            line.useWorldSpace = true;
            line.positionCount = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 2;
            line.numCornerVertices = 1;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.allowOcclusionWhenDynamic = false;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.22f),
                new Keyframe(0.18f, 1f),
                new Keyframe(1f, 0.05f));
            line.sharedMaterial = material;
            line.enabled = false;
        }

        private static void ApplyEndpoints(LineRenderer line, Vector3 start, Vector3 end)
        {
            if (line == null)
            {
                return;
            }

            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private static void ApplyLine(LineRenderer line, Material material, Color baseColor, float fade, float shrink, float width, float alphaMultiplier)
        {
            if (line == null)
            {
                return;
            }

            Color start = baseColor;
            start.a *= fade * alphaMultiplier;
            Color end = start;
            end.a *= 0.08f;
            line.startColor = start;
            line.endColor = end;
            line.widthMultiplier = Mathf.Max(0.001f, width * shrink);
            ApplyMaterialColor(material, start);
        }
    }

    private sealed class PooledFlash : MonoBehaviour, IRetroPoolLifecycle
    {
        private RetroPooledObject pooledObject;
        private Renderer effectRenderer;
        private Material material;
        private Color color;
        private float duration;
        private float age;
        private float baseScale;

        public void Configure(Renderer renderer)
        {
            effectRenderer = renderer;
            material = CreateTransparentMaterial("Pooled Flash", additive: true);
            if (effectRenderer != null)
            {
                effectRenderer.sharedMaterial = material;
                effectRenderer.enabled = false;
            }
        }

        public void Play(string label, Color tint, float scale, float lifetime)
        {
            gameObject.name = string.IsNullOrWhiteSpace(label) ? "PooledFlash" : label;
            color = tint;
            duration = Mathf.Max(0.01f, lifetime);
            age = 0f;
            baseScale = Mathf.Max(0.001f, scale);
            Apply(0f);
        }

        public void OnPoolRent(RetroPooledObject pooledObject)
        {
            this.pooledObject = pooledObject;
            age = 0f;
            if (effectRenderer != null)
            {
                effectRenderer.enabled = true;
            }
        }

        public void OnPoolReturn(RetroPooledObject pooledObject)
        {
            age = 0f;
            if (effectRenderer != null)
            {
                effectRenderer.enabled = false;
            }
        }

        public void OnPoolDestroy(RetroPooledObject pooledObject)
        {
            DestroyRuntimeMaterial(material);
            material = null;
            this.pooledObject = null;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float normalizedAge = Mathf.Clamp01(age / duration);
            Apply(normalizedAge);
            if (normalizedAge >= 1f)
            {
                pooledObject?.ReturnToPool();
            }
        }

        private void Apply(float normalizedAge)
        {
            float fade = 1f - Mathf.SmoothStep(0f, 1f, normalizedAge);
            float scale = Mathf.Lerp(baseScale, baseScale * 0.35f, normalizedAge);
            transform.localScale = Vector3.one * scale;

            Color tint = color;
            tint.a *= fade;
            if (effectRenderer != null)
            {
                effectRenderer.enabled = tint.a > 0.001f;
            }

            ApplyMaterialColor(material, tint);
        }
    }

    private static Material CreateTransparentMaterial(string materialName, bool additive)
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        shader ??= Shader.Find("Unlit/Color");
        shader ??= Shader.Find("Sprites/Default");
        shader ??= Shader.Find("Standard");
        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader)
        {
            name = materialName,
            renderQueue = (int)RenderQueue.Transparent
        };

        material.SetOverrideTag("RenderType", "Transparent");
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword(additive ? "_BLENDMODE_ADD" : "_BLENDMODE_ALPHA");
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

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        SetMaterialColorIfPresent(material, color, "_UnlitColor", "_BaseColor", "_Color");
        SetMaterialColorIfPresent(material, color * 3.5f, "_EmissiveColor", "_EmissionColor");
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

    private static void SetMaterialColorIfPresent(Material material, Color value, params string[] propertyNames)
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
}
