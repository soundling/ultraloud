using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class RetroB2BombProjectile : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] animationFrames = new Sprite[0];
    [SerializeField, Min(0.1f)] private float animationFramesPerSecond = 9f;
    [SerializeField, Min(0.01f)] private float spriteScale = 1.1f;
    [SerializeField] private Color trailColor = new Color(1f, 0.24f, 0.04f, 0.78f);
    [SerializeField, Min(0f)] private float trailInterval = 0.065f;

    [Header("Fall")]
    [SerializeField, Min(0.2f)] private float fallDuration = 4.65f;
    [SerializeField, Min(0.1f)] private float fallSpeed = 13.5f;
    [SerializeField, Min(0.2f)] private float maximumFallDuration = 7.5f;
    [SerializeField, Min(0f)] private float impactHoverHeight = 0.25f;

    [Header("Explosion")]
    [SerializeField] private RetroB2ExplosionVfx explosionPrefab;
    [SerializeField] private LayerMask damageMask = ~0;
    [SerializeField, Min(0.1f)] private float explosionRadius = 7.5f;
    [SerializeField, Min(0f)] private float damage = 88f;
    [SerializeField, Min(0f)] private float explosionForce = 19f;
    [SerializeField] private Color explosionColor = new Color(1f, 0.36f, 0.04f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.09f, 0.02f, 0.92f);

    [Header("Audio")]
    [SerializeField] private bool playWhistle = true;
    [SerializeField] private bool playExplosionSound = true;
    [SerializeField, Range(0f, 1f)] private float whistleVolume = 0.58f;
    [SerializeField, Range(0f, 1f)] private float explosionVolume = 1f;

    private Vector3 startPosition;
    private Vector3 impactPoint;
    private Vector3 previousPosition;
    private GameObject source;
    private BombWarningMarker warningMarker;
    private AudioSource whistleSource;
    private float age;
    private float activeFallDuration = 1f;
    private float nextTrailTime;
    private bool active;
    private bool exploded;

    private static AudioClip sharedWhistleClip;
    private static AudioClip sharedExplosionClip;

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
        animationFramesPerSecond = Mathf.Max(0.1f, animationFramesPerSecond);
        spriteScale = Mathf.Max(0.01f, spriteScale);
        trailInterval = Mathf.Max(0f, trailInterval);
        fallDuration = Mathf.Max(0.2f, fallDuration);
        fallSpeed = Mathf.Max(0.1f, fallSpeed);
        maximumFallDuration = Mathf.Max(fallDuration, maximumFallDuration);
        impactHoverHeight = Mathf.Max(0f, impactHoverHeight);
        explosionRadius = Mathf.Max(0.1f, explosionRadius);
        damage = Mathf.Max(0f, damage);
        explosionForce = Mathf.Max(0f, explosionForce);
    }

    private void Update()
    {
        if (!active || exploded)
        {
            AnimateSprite(Time.time);
            return;
        }

        previousPosition = transform.position;
        age += Time.deltaTime;
        float normalizedAge = Mathf.Clamp01(age / activeFallDuration);
        float travel = Mathf.Pow(normalizedAge, 1.14f);
        transform.position = Vector3.Lerp(startPosition, impactPoint + Vector3.up * impactHoverHeight, travel);
        AnimateSprite(age);
        UpdateWhistle(normalizedAge);
        UpdateWarning(normalizedAge);
        TrySpawnTrail();

        if (normalizedAge >= 1f)
        {
            Explode();
        }
    }

    private void LateUpdate()
    {
        FaceCamera();
    }

    public void Initialize(Vector3 requestedImpactPoint, LayerMask groundMask, GameObject damageSource)
    {
        AutoAssignReferences();
        startPosition = transform.position;
        previousPosition = startPosition;
        impactPoint = ResolveGroundPoint(requestedImpactPoint, groundMask);
        source = damageSource != null ? damageSource : gameObject;
        age = 0f;
        activeFallDuration = ResolveFallDuration();
        nextTrailTime = 0f;
        active = true;
        exploded = false;
        CreateWarningMarker();
        StartWhistle();
    }

    private float ResolveFallDuration()
    {
        float verticalDrop = Mathf.Max(0f, startPosition.y - (impactPoint.y + impactHoverHeight));
        float heightBasedDuration = verticalDrop / Mathf.Max(0.1f, fallSpeed);
        return Mathf.Clamp(Mathf.Max(fallDuration, heightBasedDuration), fallDuration, maximumFallDuration);
    }

    private Vector3 ResolveGroundPoint(Vector3 requestedImpactPoint, LayerMask groundMask)
    {
        Vector3 rayOrigin = requestedImpactPoint + Vector3.up * 80f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 180f, groundMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return requestedImpactPoint;
    }

    private void TrySpawnTrail()
    {
        if (trailInterval <= 0f || Time.time < nextTrailTime || (transform.position - previousPosition).sqrMagnitude < 0.0001f)
        {
            return;
        }

        nextTrailTime = Time.time + trailInterval;
        RetroGameContext.Vfx.SpawnBulletTrail("B2Bomb", previousPosition, transform.position, trailColor, 0.075f, 0.26f);
    }

    private void Explode()
    {
        if (exploded)
        {
            return;
        }

        exploded = true;
        active = false;

        if (warningMarker != null)
        {
            Destroy(warningMarker.gameObject);
            warningMarker = null;
        }

        if (whistleSource != null)
        {
            whistleSource.Stop();
        }

        ApplyExplosionDamage();
        SpawnExplosionVfx();
        PlayExplosionSound();
        RetroGameContext.Events.Publish(new RetroExplosionEvent(source, impactPoint, explosionRadius, damage, explosionColor));
        Destroy(gameObject);
    }

    private void ApplyExplosionDamage()
    {
        HashSet<RetroDamageable> damagedTargets = new HashSet<RetroDamageable>();
        Collider[] hits = Physics.OverlapSphere(impactPoint, explosionRadius, damageMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            Vector3 closestPoint = hit.ClosestPoint(impactPoint);
            float distance = Vector3.Distance(impactPoint, closestPoint);
            float falloff = Mathf.Clamp01(1f - distance / Mathf.Max(0.01f, explosionRadius));
            if (falloff <= 0f)
            {
                continue;
            }

            float resolvedDamage = damage * Mathf.Lerp(0.2f, 1f, falloff);
            Vector3 hitNormal = (closestPoint - impactPoint).sqrMagnitude > 0.0001f
                ? (closestPoint - impactPoint).normalized
                : Vector3.up;

            RetroDamageable damageable = hit.GetComponentInParent<RetroDamageable>();
            if (damageable != null)
            {
                if (damagedTargets.Add(damageable))
                {
                    damageable.ApplyDamage(resolvedDamage, closestPoint, hitNormal, source);
                }
            }
            else
            {
                hit.SendMessageUpwards("ApplyDamage", resolvedDamage, SendMessageOptions.DontRequireReceiver);
            }

            if (hit.attachedRigidbody != null)
            {
                hit.attachedRigidbody.AddExplosionForce(
                    explosionForce * Mathf.Max(0.15f, falloff),
                    impactPoint,
                    explosionRadius,
                    0.35f,
                    ForceMode.Impulse);
            }
        }
    }

    private void SpawnExplosionVfx()
    {
        RetroGameContext.Vfx.SpawnExplosionFlash(impactPoint + Vector3.up * 0.45f, explosionColor, explosionRadius, 0.22f);
        if (explosionPrefab == null)
        {
            return;
        }

        RetroB2ExplosionVfx explosion = Instantiate(explosionPrefab, impactPoint + Vector3.up * 0.22f, Quaternion.identity);
        explosion.name = "B2 Bomb Explosion";
        explosion.Play(explosionRadius, explosionColor);
    }

    private void CreateWarningMarker()
    {
        warningMarker = BombWarningMarker.Create(impactPoint, explosionRadius, warningColor, activeFallDuration);
    }

    private void UpdateWarning(float normalizedAge)
    {
        if (warningMarker != null)
        {
            warningMarker.SetProgress(normalizedAge);
        }
    }

    private void StartWhistle()
    {
        if (!playWhistle || whistleVolume <= 0f)
        {
            return;
        }

        if (whistleSource == null)
        {
            whistleSource = gameObject.AddComponent<AudioSource>();
        }

        whistleSource.clip = GetWhistleClip();
        whistleSource.loop = true;
        whistleSource.playOnAwake = false;
        whistleSource.spatialBlend = 1f;
        whistleSource.volume = whistleVolume;
        whistleSource.pitch = 0.84f;
        whistleSource.minDistance = 3f;
        whistleSource.maxDistance = 70f;
        whistleSource.rolloffMode = AudioRolloffMode.Logarithmic;
        whistleSource.Play();
    }

    private void UpdateWhistle(float normalizedAge)
    {
        if (whistleSource == null)
        {
            return;
        }

        whistleSource.pitch = Mathf.Lerp(0.82f, 1.62f, normalizedAge);
        whistleSource.volume = whistleVolume * Mathf.Lerp(0.55f, 1f, normalizedAge);
    }

    private void PlayExplosionSound()
    {
        if (!playExplosionSound || explosionVolume <= 0f)
        {
            return;
        }

        RetroAudioPlayback playback = RetroAudioPlayback.Default;
        playback.Volume = explosionVolume;
        playback.Pitch = Random.Range(0.82f, 1.05f);
        playback.MinDistance = 7f;
        playback.MaxDistance = 95f;
        playback.Priority = 40;
        RetroGameContext.Audio.PlayClip(GetExplosionClip(), impactPoint, playback);
    }

    private void AutoAssignReferences()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.transform.localScale = Vector3.one * spriteScale;
            if (animationFrames != null && animationFrames.Length > 0 && spriteRenderer.sprite == null)
            {
                spriteRenderer.sprite = animationFrames[0];
            }
        }
    }

    private void AnimateSprite(float time)
    {
        if (spriteRenderer == null || animationFrames == null || animationFrames.Length == 0)
        {
            return;
        }

        int frame = Mathf.FloorToInt(time * animationFramesPerSecond) % animationFrames.Length;
        if (frame < 0)
        {
            frame = 0;
        }

        spriteRenderer.sprite = animationFrames[frame];
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

        Transform visual = spriteRenderer.transform;
        Vector3 toCamera = camera.transform.position - visual.position;
        if (toCamera.sqrMagnitude < 0.0001f)
        {
            return;
        }

        visual.rotation = Quaternion.LookRotation(-toCamera.normalized, camera.transform.up)
            * Quaternion.Euler(0f, 0f, age * 120f);
    }

    private static AudioClip GetWhistleClip()
    {
        if (sharedWhistleClip != null)
        {
            return sharedWhistleClip;
        }

        const int sampleRate = 22050;
        const float lengthSeconds = 0.75f;
        int sampleCount = Mathf.CeilToInt(sampleRate * lengthSeconds);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float wobble = Mathf.Sin(t * Mathf.PI * 2f * 6.2f) * 12f;
            float tone = Mathf.Sin(t * Mathf.PI * 2f * (620f + wobble)) * 0.42f;
            float overtone = Mathf.Sin(t * Mathf.PI * 2f * (1240f + wobble * 1.6f)) * 0.12f;
            samples[i] = (tone + overtone) * 0.75f;
        }

        sharedWhistleClip = AudioClip.Create("B2BombWhistle", sampleCount, 1, sampleRate, false);
        sharedWhistleClip.SetData(samples, 0);
        return sharedWhistleClip;
    }

    private static AudioClip GetExplosionClip()
    {
        if (sharedExplosionClip != null)
        {
            return sharedExplosionClip;
        }

        const int sampleRate = 22050;
        const float lengthSeconds = 1.25f;
        int sampleCount = Mathf.CeilToInt(sampleRate * lengthSeconds);
        float[] samples = new float[sampleCount];
        int seed = 78123;
        for (int i = 0; i < sampleCount; i++)
        {
            seed = seed * 1103515245 + 12345;
            float noise = ((seed >> 16) & 0x7fff) / 16384f - 1f;
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 3.4f);
            float sub = Mathf.Sin(t * Mathf.PI * 2f * Mathf.Lerp(72f, 34f, Mathf.Clamp01(t / lengthSeconds))) * 0.55f;
            float crack = noise * Mathf.Exp(-t * 9.5f);
            samples[i] = Mathf.Clamp((sub + crack) * envelope, -1f, 1f);
        }

        sharedExplosionClip = AudioClip.Create("B2BombExplosion", sampleCount, 1, sampleRate, false);
        sharedExplosionClip.SetData(samples, 0);
        return sharedExplosionClip;
    }

    private sealed class BombWarningMarker : MonoBehaviour
    {
        private const int SegmentCount = 96;

        private LineRenderer outerRing;
        private LineRenderer innerRing;
        private Material material;
        private Vector3 center;
        private Color baseColor;
        private float radius;
        private float duration;
        private float progress;

        public static BombWarningMarker Create(Vector3 position, float radius, Color color, float duration)
        {
            GameObject markerObject = new GameObject("B2 Bomb Warning");
            BombWarningMarker marker = markerObject.AddComponent<BombWarningMarker>();
            marker.Initialize(position, radius, color, duration);
            return marker;
        }

        public void SetProgress(float normalizedProgress)
        {
            progress = Mathf.Clamp01(normalizedProgress);
            Refresh();
        }

        private void Initialize(Vector3 position, float warningRadius, Color warningColor, float warningDuration)
        {
            center = position + Vector3.up * 0.075f;
            radius = Mathf.Max(0.1f, warningRadius);
            baseColor = warningColor;
            duration = Mathf.Max(0.1f, warningDuration);
            transform.position = center;

            material = CreateTransparentMaterial("B2 Bomb Warning", additive: true);
            outerRing = CreateRing("OuterRing", 0.055f);
            innerRing = CreateRing("InnerRing", 0.028f);
            Refresh();
        }

        private LineRenderer CreateRing(string objectName, float width)
        {
            GameObject ringObject = new GameObject(objectName);
            ringObject.transform.SetParent(transform, false);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.positionCount = SegmentCount + 1;
            ring.alignment = LineAlignment.View;
            ring.textureMode = LineTextureMode.Stretch;
            ring.numCapVertices = 2;
            ring.numCornerVertices = 2;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.allowOcclusionWhenDynamic = false;
            ring.widthMultiplier = width;
            ring.sharedMaterial = material;
            return ring;
        }

        private void Update()
        {
            progress = Mathf.Clamp01(progress + Time.deltaTime / duration);
            Refresh();
        }

        private void Refresh()
        {
            float blink = 0.55f + Mathf.Abs(Mathf.Sin(Time.time * Mathf.Lerp(7f, 19f, progress))) * 0.45f;
            float outerScale = Mathf.Lerp(1.12f, 0.82f, progress);
            float innerScale = Mathf.Lerp(0.28f, 0.08f, progress);
            Color color = baseColor;
            color.a *= Mathf.Lerp(0.48f, 0.94f, progress) * blink;
            ApplyRing(outerRing, radius * outerScale, color);

            Color innerColor = Color.Lerp(baseColor, Color.white, 0.25f);
            innerColor.a = color.a * 0.62f;
            ApplyRing(innerRing, radius * innerScale, innerColor);
            ApplyMaterialColor(material, color);
        }

        private void ApplyRing(LineRenderer ring, float resolvedRadius, Color color)
        {
            if (ring == null)
            {
                return;
            }

            for (int i = 0; i <= SegmentCount; i++)
            {
                float angle = i / (float)SegmentCount * Mathf.PI * 2f;
                Vector3 point = center + new Vector3(Mathf.Cos(angle) * resolvedRadius, 0f, Mathf.Sin(angle) * resolvedRadius);
                ring.SetPosition(i, point);
            }

            ring.startColor = color;
            ring.endColor = color;
        }

        private void OnDestroy()
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
        SetMaterialColorIfPresent(material, color * 3f, "_EmissiveColor", "_EmissionColor");
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
