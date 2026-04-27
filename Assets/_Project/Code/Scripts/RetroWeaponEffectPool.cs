using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class RetroWeaponEffectPool : MonoBehaviour
{
    private readonly List<TrailInstance> activeTrails = new List<TrailInstance>(32);
    private readonly List<FlashInstance> activeFlashes = new List<FlashInstance>(16);
    private RetroObjectPool<TrailInstance> trailPool;
    private RetroObjectPool<FlashInstance> flashPool;

    public static RetroWeaponEffectPool Create(Transform parent)
    {
        GameObject root = new GameObject("RetroWeaponEffectPool");
        if (parent != null)
        {
            root.transform.SetParent(parent, false);
        }

        return root.AddComponent<RetroWeaponEffectPool>();
    }

    public void SpawnBulletTrail(string effectName, Vector3 start, Vector3 end, Color color, float width, float duration)
    {
        float length = Vector3.Distance(start, end);
        if (length <= 0.001f || width <= 0f || duration <= 0f)
        {
            return;
        }

        EnsurePools();
        TrailInstance trail = trailPool != null ? trailPool.Rent() : null;
        if (trail == null)
        {
            return;
        }

        trail.Activate(effectName, start, end, color, width, duration);
        activeTrails.Add(trail);
    }

    public void SpawnImpactFlash(Vector3 position, Vector3 normal, Color color, float scale, float duration)
    {
        if (scale <= 0f || duration <= 0f)
        {
            return;
        }

        EnsurePools();
        FlashInstance flash = flashPool != null ? flashPool.Rent() : null;
        if (flash == null)
        {
            return;
        }

        flash.Activate(position + normal.normalized * 0.03f, color, scale, duration);
        activeFlashes.Add(flash);
    }

    public void Clear()
    {
        for (int i = activeTrails.Count - 1; i >= 0; i--)
        {
            trailPool?.Return(activeTrails[i]);
        }

        activeTrails.Clear();

        for (int i = activeFlashes.Count - 1; i >= 0; i--)
        {
            flashPool?.Return(activeFlashes[i]);
        }

        activeFlashes.Clear();
    }

    private void Awake()
    {
        EnsurePools();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        for (int i = activeTrails.Count - 1; i >= 0; i--)
        {
            TrailInstance trail = activeTrails[i];
            if (!trail.Tick(deltaTime))
            {
                activeTrails.RemoveAt(i);
                trailPool?.Return(trail);
            }
        }

        for (int i = activeFlashes.Count - 1; i >= 0; i--)
        {
            FlashInstance flash = activeFlashes[i];
            if (!flash.Tick(deltaTime))
            {
                activeFlashes.RemoveAt(i);
                flashPool?.Return(flash);
            }
        }
    }

    private void OnDestroy()
    {
        trailPool?.Dispose();
        flashPool?.Dispose();
        activeTrails.Clear();
        activeFlashes.Clear();
    }

    private void EnsurePools()
    {
        if (trailPool == null || trailPool.IsDisposed)
        {
            trailPool = new RetroObjectPool<TrailInstance>(
                "Weapon Bullet Trails",
                CreateTrailInstance,
                onRent: null,
                onReturn: trail => trail?.Release(),
                onDestroy: trail => trail?.Destroy(),
                settings: new RetroPoolSettings(prewarmCount: 16, maxInactiveCount: 128));
        }

        if (flashPool == null || flashPool.IsDisposed)
        {
            flashPool = new RetroObjectPool<FlashInstance>(
                "Weapon Impact Flashes",
                CreateFlashInstance,
                onRent: null,
                onReturn: flash => flash?.Release(),
                onDestroy: flash => flash?.Destroy(),
                settings: new RetroPoolSettings(prewarmCount: 8, maxInactiveCount: 64));
        }
    }

    private TrailInstance CreateTrailInstance()
    {
        GameObject root = new GameObject("BulletTrail");
        root.transform.SetParent(transform, false);
        TrailInstance trail = new TrailInstance(root);
        if (trail.IsUsable)
        {
            return trail;
        }

        trail.Destroy();
        return null;
    }

    private FlashInstance CreateFlashInstance()
    {
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "ImpactFlash";
        flash.transform.SetParent(transform, false);

        Collider collider = flash.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }

        Renderer renderer = flash.GetComponent<Renderer>();
        if (renderer == null)
        {
            Destroy(flash);
            return null;
        }

        FlashInstance instance = new FlashInstance(flash, renderer);
        if (instance.IsUsable)
        {
            return instance;
        }

        instance.Destroy();
        return null;
    }

    private sealed class TrailInstance
    {
        private readonly GameObject root;
        private readonly LineRenderer glowLine;
        private readonly LineRenderer coreLine;
        private readonly Renderer fallbackRenderer;
        private readonly Material glowMaterial;
        private readonly Material coreMaterial;
        private readonly Material fallbackMaterial;
        private Vector3 fallbackBaseScale;
        private Color color;
        private float duration;
        private float age;
        private float baseWidth;
        private bool usesLineRenderer;

        public bool IsUsable => root != null && (usesLineRenderer || fallbackRenderer != null);

        public TrailInstance(GameObject root)
        {
            this.root = root;
            glowLine = root.AddComponent<LineRenderer>();
            coreLine = root.AddComponent<LineRenderer>();
            if (glowLine != null && coreLine != null)
            {
                usesLineRenderer = true;
                glowMaterial = CreateTransparentMaterial("Bullet Trail Glow");
                coreMaterial = CreateTransparentMaterial("Bullet Trail Core");
                ConfigureLine(glowLine, glowMaterial);
                ConfigureLine(coreLine, coreMaterial);
                return;
            }

            if (glowLine != null)
            {
                Object.Destroy(glowLine);
            }

            if (coreLine != null)
            {
                Object.Destroy(coreLine);
            }

            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "BulletTrailFallback";
            fallback.transform.SetParent(root.transform, false);
            Collider collider = fallback.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Object.Destroy(collider);
            }

            fallbackRenderer = fallback.GetComponent<Renderer>();
            fallbackMaterial = CreateTransparentMaterial("Bullet Trail Fallback");
            if (fallbackRenderer != null && fallbackMaterial != null)
            {
                fallbackRenderer.sharedMaterial = fallbackMaterial;
                fallbackRenderer.shadowCastingMode = ShadowCastingMode.Off;
                fallbackRenderer.receiveShadows = false;
            }
        }

        public void Activate(string effectName, Vector3 start, Vector3 end, Color tint, float width, float lifetime)
        {
            root.name = string.IsNullOrWhiteSpace(effectName) ? "BulletTrail" : $"{effectName} BulletTrail";
            root.SetActive(true);
            color = tint;
            duration = Mathf.Max(0.01f, lifetime);
            age = 0f;
            baseWidth = Mathf.Max(0.001f, width);

            if (usesLineRenderer)
            {
                ApplyLineEndpoints(glowLine, start, end);
                ApplyLineEndpoints(coreLine, start, end);
                Apply(0f);
                return;
            }

            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.001f)
            {
                Release();
                return;
            }

            root.transform.position = (start + end) * 0.5f;
            root.transform.rotation = Quaternion.LookRotation(delta / length, Vector3.up);
            fallbackBaseScale = new Vector3(baseWidth, baseWidth, length);
            root.transform.localScale = fallbackBaseScale;
            Apply(0f);
        }

        public bool Tick(float deltaTime)
        {
            age += deltaTime;
            float normalizedAge = Mathf.Clamp01(age / duration);
            Apply(normalizedAge);
            return normalizedAge < 1f;
        }

        public void Release()
        {
            root.SetActive(false);
        }

        public void Destroy()
        {
            DestroyMaterial(glowMaterial);
            DestroyMaterial(coreMaterial);
            DestroyMaterial(fallbackMaterial);
            if (root != null)
            {
                Object.Destroy(root);
            }
        }

        private void Apply(float normalizedAge)
        {
            float fade = 1f - Mathf.SmoothStep(0f, 1f, normalizedAge);
            float shrink = Mathf.Lerp(1f, 0.16f, normalizedAge);

            if (usesLineRenderer)
            {
                ApplyLine(glowLine, glowMaterial, color, fade, shrink, baseWidth * 2.8f, 0.24f);
                ApplyLine(coreLine, coreMaterial, color, fade, shrink, baseWidth, 1f);
                return;
            }

            if (fallbackRenderer != null)
            {
                Color faded = color;
                faded.a *= fade;
                fallbackRenderer.enabled = faded.a > 0.001f;
                root.transform.localScale = new Vector3(
                    fallbackBaseScale.x * shrink,
                    fallbackBaseScale.y * shrink,
                    fallbackBaseScale.z);
                ApplyMaterialColor(fallbackMaterial, faded);
            }
        }

        private static void ConfigureLine(LineRenderer line, Material material)
        {
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

            if (material != null)
            {
                line.sharedMaterial = material;
            }
        }

        private static void ApplyLineEndpoints(LineRenderer line, Vector3 start, Vector3 end)
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

    private sealed class FlashInstance
    {
        private readonly GameObject root;
        private readonly Renderer renderer;
        private readonly Material material;
        private Color color;
        private float duration;
        private float age;
        private float baseScale;

        public bool IsUsable => root != null && renderer != null && material != null;

        public FlashInstance(GameObject root, Renderer renderer)
        {
            this.root = root;
            this.renderer = renderer;
            material = CreateTransparentMaterial("Impact Flash");
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        public void Activate(Vector3 position, Color tint, float scale, float lifetime)
        {
            root.SetActive(true);
            root.transform.position = position;
            color = tint;
            duration = Mathf.Max(0.01f, lifetime);
            age = 0f;
            baseScale = Mathf.Max(0.001f, scale);
            Apply(0f);
        }

        public bool Tick(float deltaTime)
        {
            age += deltaTime;
            float normalizedAge = Mathf.Clamp01(age / duration);
            Apply(normalizedAge);
            return normalizedAge < 1f;
        }

        public void Release()
        {
            root.SetActive(false);
        }

        public void Destroy()
        {
            DestroyMaterial(material);
            if (root != null)
            {
                Object.Destroy(root);
            }
        }

        private void Apply(float normalizedAge)
        {
            float fade = 1f - Mathf.SmoothStep(0f, 1f, normalizedAge);
            float scale = Mathf.Lerp(baseScale, baseScale * 0.35f, normalizedAge);
            root.transform.localScale = Vector3.one * scale;

            Color tint = color;
            tint.a *= fade;
            renderer.enabled = tint.a > 0.001f;
            ApplyMaterialColor(material, tint);
        }
    }

    private static Material CreateTransparentMaterial(string materialName)
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
        material.EnableKeyword("_BLENDMODE_ADD");
        SetMaterialFloatIfPresent(material, 1f, "_SurfaceType");
        SetMaterialFloatIfPresent(material, 1f, "_BlendMode");
        SetMaterialFloatIfPresent(material, 0f, "_AlphaCutoffEnable", "_AlphaClip");
        SetMaterialFloatIfPresent(material, 0f, "_ZWrite", "_TransparentZWrite");
        SetMaterialFloatIfPresent(material, (float)CullMode.Off, "_CullMode", "_CullModeForward");
        SetMaterialFloatIfPresent(material, (float)BlendMode.SrcAlpha, "_SrcBlend");
        SetMaterialFloatIfPresent(material, (float)BlendMode.One, "_DstBlend");
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
