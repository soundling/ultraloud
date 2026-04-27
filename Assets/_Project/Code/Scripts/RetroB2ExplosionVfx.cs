using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class RetroB2ExplosionVfx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] animationFrames = new Sprite[0];
    [SerializeField, Min(0.1f)] private float framesPerSecond = 16f;
    [SerializeField, Min(0.05f)] private float lifetime = 0.78f;
    [SerializeField, Min(0.01f)] private float spriteScaleMultiplier = 0.42f;
    [SerializeField] private Color smokeFadeColor = new Color(0.55f, 0.16f, 0.12f, 0.82f);
    [SerializeField] private bool spawnShockwave = true;

    private LineRenderer shockwaveRing;
    private Material shockwaveMaterial;
    private Color initialColor = Color.white;
    private float radius = 1f;
    private float age;
    private bool playing;

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
    }

    private void OnValidate()
    {
        AutoAssignReferences();
        framesPerSecond = Mathf.Max(0.1f, framesPerSecond);
        lifetime = Mathf.Max(0.05f, lifetime);
        spriteScaleMultiplier = Mathf.Max(0.01f, spriteScaleMultiplier);
    }

    private void Update()
    {
        if (!playing)
        {
            return;
        }

        age += Time.deltaTime;
        float normalizedAge = Mathf.Clamp01(age / lifetime);
        ApplyFrame(normalizedAge);
        ApplyScaleAndTint(normalizedAge);
        ApplyShockwave(normalizedAge);
        if (normalizedAge >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void LateUpdate()
    {
        FaceCamera();
    }

    public void Play(float explosionRadius, Color color)
    {
        AutoAssignReferences();
        radius = Mathf.Max(0.1f, explosionRadius);
        initialColor = color;
        age = 0f;
        playing = true;
        if (spawnShockwave)
        {
            EnsureShockwave();
        }

        ApplyFrame(0f);
        ApplyScaleAndTint(0f);
        ApplyShockwave(0f);
    }

    private void AutoAssignReferences()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (spriteRenderer != null && animationFrames != null && animationFrames.Length > 0 && spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = animationFrames[0];
        }
    }

    private void ApplyFrame(float normalizedAge)
    {
        if (spriteRenderer == null || animationFrames == null || animationFrames.Length == 0)
        {
            return;
        }

        int frame = Mathf.Clamp(Mathf.FloorToInt(age * framesPerSecond), 0, animationFrames.Length - 1);
        spriteRenderer.sprite = animationFrames[frame];
    }

    private void ApplyScaleAndTint(float normalizedAge)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        float punch = Mathf.Sin(Mathf.Clamp01(normalizedAge) * Mathf.PI);
        float scale = Mathf.Lerp(radius * 0.22f, radius * spriteScaleMultiplier, Mathf.SmoothStep(0f, 1f, normalizedAge));
        scale += punch * radius * 0.045f;
        spriteRenderer.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);

        Color tint = Color.Lerp(initialColor, smokeFadeColor, Mathf.SmoothStep(0.2f, 1f, normalizedAge));
        tint.a = Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0.62f, 1f, normalizedAge));
        spriteRenderer.color = tint;
    }

    private void FaceCamera()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 toCamera = camera.transform.position - spriteRenderer.transform.position;
        if (toCamera.sqrMagnitude < 0.0001f)
        {
            return;
        }

        spriteRenderer.transform.rotation = Quaternion.LookRotation(-toCamera.normalized, camera.transform.up);
    }

    private void EnsureShockwave()
    {
        if (shockwaveRing != null)
        {
            return;
        }

        GameObject ringObject = new GameObject("ShockwaveRing");
        ringObject.transform.SetParent(transform, false);
        shockwaveMaterial = CreateTransparentMaterial("B2 Shockwave", additive: true);
        shockwaveRing = ringObject.AddComponent<LineRenderer>();
        shockwaveRing.useWorldSpace = true;
        shockwaveRing.positionCount = 97;
        shockwaveRing.alignment = LineAlignment.View;
        shockwaveRing.textureMode = LineTextureMode.Stretch;
        shockwaveRing.numCapVertices = 2;
        shockwaveRing.numCornerVertices = 2;
        shockwaveRing.shadowCastingMode = ShadowCastingMode.Off;
        shockwaveRing.receiveShadows = false;
        shockwaveRing.allowOcclusionWhenDynamic = false;
        shockwaveRing.sharedMaterial = shockwaveMaterial;
    }

    private void ApplyShockwave(float normalizedAge)
    {
        if (shockwaveRing == null)
        {
            return;
        }

        float ringRadius = Mathf.Lerp(radius * 0.22f, radius * 1.25f, Mathf.SmoothStep(0f, 1f, normalizedAge));
        float alpha = 1f - Mathf.SmoothStep(0.12f, 0.88f, normalizedAge);
        Color color = Color.Lerp(Color.white, initialColor, 0.45f);
        color.a = alpha * 0.58f;
        shockwaveRing.widthMultiplier = Mathf.Lerp(0.18f, 0.035f, normalizedAge);
        shockwaveRing.startColor = color;
        shockwaveRing.endColor = color;
        ApplyMaterialColor(shockwaveMaterial, color);

        Vector3 center = transform.position + Vector3.up * 0.09f;
        for (int i = 0; i < 97; i++)
        {
            float angle = i / 96f * Mathf.PI * 2f;
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * ringRadius, 0f, Mathf.Sin(angle) * ringRadius);
            shockwaveRing.SetPosition(i, point);
        }
    }

    private void OnDestroy()
    {
        if (shockwaveMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(shockwaveMaterial);
        }
        else
        {
            DestroyImmediate(shockwaveMaterial);
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
        SetMaterialColorIfPresent(material, color * 3.2f, "_EmissiveColor", "_EmissionColor");
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
