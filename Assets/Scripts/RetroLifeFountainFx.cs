using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DefaultExecutionOrder(85)]
[DisallowMultipleComponent]
public sealed class RetroLifeFountainFx : MonoBehaviour
{
    private const string GeneratedRootName = "__LifeFountainFx";
    private const int MistCount = 6;
    private const int SparkCount = 8;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");
    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
    private static readonly int UnlitColorMapId = Shader.PropertyToID("_UnlitColorMap");

    [Header("Generated Textures")]
    [SerializeField] private Texture2D groundShadowTexture;
    [SerializeField] private Texture2D waterRippleTexture;
    [SerializeField] private Texture2D mistTexture;
    [SerializeField] private Texture2D sparkTexture;

    [Header("Placement")]
    [SerializeField] private Vector3 waterLocalPosition = new(0f, 2.72f, 0f);
    [SerializeField, Min(0.05f)] private float waterRadius = 1.02f;
    [SerializeField] private Vector2 groundShadowSize = new(4.8f, 3.25f);
    [SerializeField, Range(0f, 1f)] private float groundShadowAlpha = 0.38f;
    [SerializeField, Min(0f)] private float mistRadius = 0.82f;
    [SerializeField, Min(0.01f)] private float mistSize = 0.16f;
    [SerializeField, Min(0.01f)] private float sparkSize = 0.065f;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float pulseSpeed = 1.05f;
    [SerializeField, Min(0f)] private float rippleSpinSpeed = 14f;
    [SerializeField, Min(0f)] private float sparkOrbitSpeed = 24f;
    [SerializeField, Min(0f)] private float mistDriftSpeed = 0.36f;

    [Header("Color")]
    [SerializeField] private Color waterColor = new(0.22f, 0.82f, 0.9f, 0.48f);
    [SerializeField] private Color glowColor = new(0.36f, 0.82f, 0.62f, 0.54f);
    [SerializeField] private Color mistColor = new(0.52f, 0.9f, 0.8f, 0.12f);
    [SerializeField] private Color sparkColor = new(0.72f, 0.95f, 0.45f, 0.28f);
    [SerializeField, Min(0f)] private float emissionMultiplier = 1.25f;

    [Header("Light")]
    [SerializeField] private bool createPointLight = true;
    [SerializeField, Min(0f)] private float lightIntensity = 0.65f;
    [SerializeField, Min(0.01f)] private float lightRange = 3.2f;

    private GameObject generatedRoot;
    private Transform groundShadow;
    private Transform waterDisk;
    private readonly Transform[] mistSprites = new Transform[MistCount];
    private readonly Transform[] sparkSprites = new Transform[SparkCount];
    private Light fountainLight;
    private Material groundShadowMaterial;
    private Material waterMaterial;
    private Material mistMaterial;
    private Material sparkMaterial;
    private bool rebuildRequested = true;

    private void Reset()
    {
        ClampSettings();
        RequestRebuild();
    }

    private void Awake()
    {
        ClampSettings();
        RebuildIfNeeded(true);
    }

    private void OnEnable()
    {
        ClampSettings();
        RebuildIfNeeded(true);
        UpdateFx(ResolveTime());
    }

    private void OnValidate()
    {
        ClampSettings();
        RequestRebuild();
    }

    private void LateUpdate()
    {
        RebuildIfNeeded(false);
        UpdateFx(ResolveTime());
    }

    private void OnDisable()
    {
        DestroyMaterials();
    }

    private void OnDestroy()
    {
        DestroyGeneratedRoot();
        DestroyMaterials();
    }

    [ContextMenu("Rebuild Fountain FX Now")]
    public void RebuildFountainFxNow()
    {
        rebuildRequested = true;
        RebuildIfNeeded(true);
    }

    private void RequestRebuild()
    {
        rebuildRequested = true;
    }

    private void RebuildIfNeeded(bool force)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && EditorUtility.IsPersistent(this))
        {
            return;
        }
#endif
        if (!force && !rebuildRequested && generatedRoot != null)
        {
            return;
        }

        RebuildInternal();
        rebuildRequested = false;
    }

    private void RebuildInternal()
    {
        DestroyGeneratedRoot();
        DestroyMaterials();

        generatedRoot = new GameObject(GeneratedRootName)
        {
            hideFlags = HideFlags.DontSave
        };
        generatedRoot.transform.SetParent(transform, false);
        generatedRoot.transform.localPosition = Vector3.zero;
        generatedRoot.transform.localRotation = Quaternion.identity;
        generatedRoot.transform.localScale = Vector3.one;

        groundShadowMaterial = CreateTransparentMaterial("Life Fountain Ground Shadow", false, groundShadowTexture);
        waterMaterial = CreateTransparentMaterial("Life Fountain Water", false, waterRippleTexture);
        mistMaterial = CreateTransparentMaterial("Life Fountain Mist", false, mistTexture);
        sparkMaterial = CreateTransparentMaterial("Life Fountain Sparks", true, sparkTexture);

        groundShadow = CreateQuad("Ground Contact Shadow", generatedRoot.transform, groundShadowMaterial).transform;
        groundShadow.localRotation = Quaternion.Euler(90f, 0f, 0f);

        waterDisk = CreateQuad("Water Disk", generatedRoot.transform, waterMaterial).transform;
        waterDisk.localRotation = Quaternion.Euler(90f, 0f, 0f);

        for (int i = 0; i < mistSprites.Length; i++)
        {
            mistSprites[i] = CreateQuad($"Mist {i:00}", generatedRoot.transform, mistMaterial).transform;
        }

        for (int i = 0; i < sparkSprites.Length; i++)
        {
            sparkSprites[i] = CreateQuad($"Spark {i:00}", generatedRoot.transform, sparkMaterial).transform;
        }

        if (createPointLight)
        {
            GameObject lightObject = new("Healing Point Light")
            {
                hideFlags = HideFlags.DontSave
            };
            lightObject.transform.SetParent(generatedRoot.transform, false);
            fountainLight = lightObject.AddComponent<Light>();
            fountainLight.type = LightType.Point;
            fountainLight.shadows = LightShadows.None;
        }
    }

    private static GameObject CreateQuad(string objectName, Transform parent, Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = objectName;
        quad.hideFlags = HideFlags.DontSave;
        quad.transform.SetParent(parent, false);
        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyUnityObject(collider);
        }

        Renderer renderer = quad.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;
        }

        return quad;
    }

    private void UpdateFx(float time)
    {
        if (generatedRoot == null)
        {
            return;
        }

        float pulse = 0.78f + Mathf.Sin(time * pulseSpeed * Mathf.PI * 2f) * 0.22f;
        UpdateGroundShadow();
        UpdateWater(time, pulse);
        UpdateBillboardParticles(time, pulse);
        UpdateLight(pulse);
    }

    private void UpdateGroundShadow()
    {
        if (groundShadow == null)
        {
            return;
        }

        groundShadow.localPosition = Vector3.up * 0.018f;
        groundShadow.localRotation = Quaternion.Euler(90f, 0f, 0f);
        groundShadow.localScale = new Vector3(Mathf.Max(0.01f, groundShadowSize.x), Mathf.Max(0.01f, groundShadowSize.y), 1f);
        ApplyMaterialColor(groundShadowMaterial, new Color(0f, 0f, 0f, groundShadowAlpha), 0f);
    }

    private void UpdateWater(float time, float pulse)
    {
        if (waterDisk == null)
        {
            return;
        }

        float radius = waterRadius * (1f + Mathf.Sin(time * 2.2f) * 0.025f);
        waterDisk.localPosition = waterLocalPosition;
        waterDisk.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        waterDisk.localRotation = Quaternion.Euler(90f, time * rippleSpinSpeed, 0f);

        Color color = waterColor;
        color.a *= Mathf.Lerp(0.72f, 1f, pulse);
        ApplyMaterialColor(waterMaterial, color, emissionMultiplier * 0.55f);
        SetMaterialTextureOffset(waterMaterial, new Vector2(time * 0.035f, -time * 0.022f));
    }

    private void UpdateBillboardParticles(float time, float pulse)
    {
        Camera camera = Camera.current != null ? Camera.current : Camera.main;
        for (int i = 0; i < mistSprites.Length; i++)
        {
            Transform mist = mistSprites[i];
            if (mist == null)
            {
                continue;
            }

            float phase = i * 1.0472f;
            float angle = phase + time * mistDriftSpeed;
            float bob = Mathf.Repeat(time * 0.17f + i * 0.18f, 1f);
            Vector3 offset = new(Mathf.Cos(angle) * mistRadius, 0.06f + bob * 0.58f, Mathf.Sin(angle) * mistRadius * 0.55f);
            mist.localPosition = waterLocalPosition + offset;
            float scale = mistSize * Mathf.Lerp(0.72f, 1.35f, bob) * Mathf.Lerp(0.85f, 1.08f, pulse);
            mist.localScale = Vector3.one * scale;
            FaceCamera(mist, camera);
            Color color = mistColor;
            color.a *= Mathf.Sin(bob * Mathf.PI) * 0.85f;
            ApplyMaterialColor(mistMaterial, color, emissionMultiplier * 0.28f);
        }

        for (int i = 0; i < sparkSprites.Length; i++)
        {
            Transform spark = sparkSprites[i];
            if (spark == null)
            {
                continue;
            }

            float phase = i * (Mathf.PI * 2f / SparkCount);
            float angle = phase + time * sparkOrbitSpeed * Mathf.Deg2Rad;
            float height = 0.24f + Mathf.Sin(time * 2.3f + phase * 2.1f) * 0.18f + (i % 3) * 0.38f;
            float radius = waterRadius * Mathf.Lerp(0.42f, 0.9f, (i % 4) / 3f);
            spark.localPosition = waterLocalPosition + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            spark.localScale = Vector3.one * sparkSize * Mathf.Lerp(0.72f, 1.45f, pulse);
            FaceCamera(spark, camera);
            Color color = sparkColor;
            color.a *= 0.58f + Mathf.Sin(time * 5.6f + phase) * 0.32f;
            ApplyMaterialColor(sparkMaterial, color, emissionMultiplier * 1.25f);
        }
    }

    private void UpdateLight(float pulse)
    {
        if (fountainLight == null)
        {
            return;
        }

        fountainLight.transform.localPosition = waterLocalPosition + Vector3.up * 0.7f;
        fountainLight.color = glowColor;
        fountainLight.intensity = lightIntensity * Mathf.Lerp(0.65f, 1.18f, pulse);
        fountainLight.range = lightRange * Mathf.Lerp(0.88f, 1.08f, pulse);
    }

    private void ClampSettings()
    {
        waterRadius = Mathf.Max(0.05f, waterRadius);
        groundShadowSize.x = Mathf.Max(0.01f, groundShadowSize.x);
        groundShadowSize.y = Mathf.Max(0.01f, groundShadowSize.y);
        groundShadowAlpha = Mathf.Clamp01(groundShadowAlpha);
        mistRadius = Mathf.Max(0f, mistRadius);
        mistSize = Mathf.Max(0.01f, mistSize);
        sparkSize = Mathf.Max(0.01f, sparkSize);
        pulseSpeed = Mathf.Max(0f, pulseSpeed);
        rippleSpinSpeed = Mathf.Max(0f, rippleSpinSpeed);
        sparkOrbitSpeed = Mathf.Max(0f, sparkOrbitSpeed);
        mistDriftSpeed = Mathf.Max(0f, mistDriftSpeed);
        emissionMultiplier = Mathf.Max(0f, emissionMultiplier);
        lightIntensity = Mathf.Max(0f, lightIntensity);
        lightRange = Mathf.Max(0.01f, lightRange);
    }

    private static void FaceCamera(Transform target, Camera camera)
    {
        if (target == null || camera == null)
        {
            return;
        }

        Vector3 toCamera = target.position - camera.transform.position;
        if (toCamera.sqrMagnitude < 0.0001f)
        {
            return;
        }

        target.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
    }

    private static Material CreateTransparentMaterial(string materialName, bool additive, Texture texture)
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
            hideFlags = HideFlags.HideAndDontSave,
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
        SetMaterialTextureIfPresent(material, texture, BaseMapId, MainTexId, BaseColorMapId, UnlitColorMapId);
        return material;
    }

    private static void ApplyMaterialColor(Material material, Color color, float emissionMultiplier)
    {
        if (material == null)
        {
            return;
        }

        SetMaterialColorIfPresent(material, color, BaseColorId, ColorId, UnlitColorId);
        Color emission = color * emissionMultiplier;
        emission.a = color.a;
        SetMaterialColorIfPresent(material, emission, EmissiveColorId, EmissionColorId);
    }

    private static void SetMaterialTextureOffset(Material material, Vector2 offset)
    {
        if (material == null)
        {
            return;
        }

        SetTextureOffsetIfPresent(material, BaseMapId, offset);
        SetTextureOffsetIfPresent(material, MainTexId, offset);
        SetTextureOffsetIfPresent(material, BaseColorMapId, offset);
        SetTextureOffsetIfPresent(material, UnlitColorMapId, offset);
    }

    private static void SetMaterialFloatIfPresent(Material material, float value, params string[] propertyNames)
    {
        for (int i = 0; i < propertyNames.Length; i++)
        {
            if (material.HasProperty(propertyNames[i]))
            {
                material.SetFloat(propertyNames[i], value);
            }
        }
    }

    private static void SetMaterialColorIfPresent(Material material, Color value, params int[] propertyIds)
    {
        for (int i = 0; i < propertyIds.Length; i++)
        {
            if (material.HasProperty(propertyIds[i]))
            {
                material.SetColor(propertyIds[i], value);
            }
        }
    }

    private static void SetMaterialTextureIfPresent(Material material, Texture texture, params int[] propertyIds)
    {
        if (texture == null)
        {
            return;
        }

        for (int i = 0; i < propertyIds.Length; i++)
        {
            if (material.HasProperty(propertyIds[i]))
            {
                material.SetTexture(propertyIds[i], texture);
            }
        }
    }

    private static void SetTextureOffsetIfPresent(Material material, int propertyId, Vector2 offset)
    {
        if (material.HasProperty(propertyId))
        {
            material.SetTextureOffset(propertyId, offset);
        }
    }

    private static float ResolveTime()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return (float)EditorApplication.timeSinceStartup;
        }
#endif
        return Time.time;
    }

    private void DestroyGeneratedRoot()
    {
        if (generatedRoot == null)
        {
            Transform existing = transform.Find(GeneratedRootName);
            if (existing != null)
            {
                generatedRoot = existing.gameObject;
            }
        }

        if (generatedRoot != null)
        {
            DestroyUnityObject(generatedRoot);
        }

        generatedRoot = null;
        groundShadow = null;
        waterDisk = null;
        fountainLight = null;
        for (int i = 0; i < mistSprites.Length; i++)
        {
            mistSprites[i] = null;
        }

        for (int i = 0; i < sparkSprites.Length; i++)
        {
            sparkSprites[i] = null;
        }
    }

    private void DestroyMaterials()
    {
        DestroyMaterial(groundShadowMaterial);
        DestroyMaterial(waterMaterial);
        DestroyMaterial(mistMaterial);
        DestroyMaterial(sparkMaterial);
        groundShadowMaterial = null;
        waterMaterial = null;
        mistMaterial = null;
        sparkMaterial = null;
    }

    private static void DestroyMaterial(Material material)
    {
        if (material != null)
        {
            DestroyUnityObject(material);
        }
    }

    private static void DestroyUnityObject(Object value)
    {
        if (value == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(value);
        }
        else
        {
            DestroyImmediate(value);
        }
    }
}
