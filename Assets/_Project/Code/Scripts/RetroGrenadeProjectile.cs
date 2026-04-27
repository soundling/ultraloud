using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public sealed class RetroGrenadeProjectile : MonoBehaviour, IRetroPoolLifecycle
{
    private readonly HashSet<RetroDamageable> damagedTargets = new HashSet<RetroDamageable>();
    private readonly List<Collider> ignoredOwnerColliders = new List<Collider>(8);
    private readonly List<Collider> ignoredProjectileColliders = new List<Collider>(8);

    private RetroPooledObject pooledObject;
    private Rigidbody cachedBody;
    private SphereCollider cachedCollider;
    private Renderer cachedRenderer;
    private Material runtimeMaterial;
    private GameObject source;
    private float damage;
    private float explosionRadius;
    private float explosionForce;
    private float impactDamage;
    private float detonateTime;
    private float trailInterval;
    private float trailWidth;
    private float trailDuration;
    private float nextTrailTime;
    private LayerMask collisionMask;
    private Color effectColor;
    private Vector3 previousTrailPosition;
    private bool alignVisualToVelocity;
    private bool exploded;

    public void ConfigureVisual(string objectName, Color color, float scale)
    {
        EnsureCachedReferences();
        string visualName = string.IsNullOrWhiteSpace(objectName) ? "Grenade Projectile" : objectName;
        gameObject.name = visualName;
        transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);

        if (cachedRenderer == null)
        {
            return;
        }

        if (runtimeMaterial == null)
        {
            runtimeMaterial = CreateRuntimeMaterial($"{visualName} Material", color, true);
            cachedRenderer.sharedMaterial = runtimeMaterial;
        }
        else
        {
            runtimeMaterial.name = $"{visualName} Material";
            ApplyRuntimeMaterialColor(runtimeMaterial, color, true);
        }

        cachedRenderer.shadowCastingMode = ShadowCastingMode.On;
        cachedRenderer.receiveShadows = true;
        cachedRenderer.enabled = true;
    }

    public void Initialize(
        GameObject owner,
        Vector3 velocity,
        float damage,
        float explosionRadius,
        float explosionForce,
        float fuseTime,
        float impactDamage,
        LayerMask collisionMask,
        Color effectColor,
        bool useGravity = true,
        bool alignVisualToVelocity = false,
        float trailInterval = 0f,
        float trailWidth = 0f,
        float trailDuration = 0f)
    {
        EnsureCachedReferences();
        RestoreIgnoredCollisions();
        damagedTargets.Clear();
        exploded = false;
        source = owner != null ? owner : gameObject;
        this.damage = damage;
        this.explosionRadius = explosionRadius;
        this.explosionForce = explosionForce;
        this.impactDamage = impactDamage;
        this.collisionMask = collisionMask;
        this.effectColor = effectColor;
        this.alignVisualToVelocity = alignVisualToVelocity;
        this.trailInterval = Mathf.Max(0f, trailInterval);
        this.trailWidth = Mathf.Max(0f, trailWidth);
        this.trailDuration = Mathf.Max(0f, trailDuration);
        previousTrailPosition = transform.position;
        nextTrailTime = Time.time + this.trailInterval;
        detonateTime = Time.time + Mathf.Max(0f, fuseTime);
        cachedCollider.radius = 0.5f;
        cachedCollider.enabled = true;
        cachedBody.useGravity = useGravity;
        cachedBody.isKinematic = false;
        cachedBody.detectCollisions = true;
        cachedBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        cachedBody.interpolation = RigidbodyInterpolation.Interpolate;

#if UNITY_6000_0_OR_NEWER
        cachedBody.linearVelocity = velocity;
#else
        cachedBody.velocity = velocity;
#endif
        cachedBody.angularVelocity = Random.onUnitSphere * 10f;
        AlignToVelocityIfNeeded();

        TrackIgnoredOwnerCollisions(owner);
    }

    public void OnPoolRent(RetroPooledObject pooledObject)
    {
        this.pooledObject = pooledObject;
        EnsureCachedReferences();
        damagedTargets.Clear();
        exploded = false;

        if (cachedCollider != null)
        {
            cachedCollider.enabled = true;
        }

        if (cachedRenderer != null)
        {
            cachedRenderer.enabled = true;
        }
    }

    public void OnPoolReturn(RetroPooledObject pooledObject)
    {
        RestoreIgnoredCollisions();
        damagedTargets.Clear();
        source = null;
        exploded = true;
        alignVisualToVelocity = false;
        trailInterval = 0f;
        trailWidth = 0f;
        trailDuration = 0f;
        if (cachedBody != null)
        {
#if UNITY_6000_0_OR_NEWER
            cachedBody.linearVelocity = Vector3.zero;
#else
            cachedBody.velocity = Vector3.zero;
#endif
            cachedBody.angularVelocity = Vector3.zero;
            cachedBody.Sleep();
        }

        if (cachedCollider != null)
        {
            cachedCollider.enabled = false;
        }

        if (cachedRenderer != null)
        {
            cachedRenderer.enabled = false;
        }
    }

    public void OnPoolDestroy(RetroPooledObject pooledObject)
    {
        RestoreIgnoredCollisions();
        DestroyRuntimeMaterial(runtimeMaterial);
        runtimeMaterial = null;
        source = null;
        this.pooledObject = null;
    }

    private void TrackIgnoredOwnerCollisions(GameObject owner)
    {
        if (owner == null)
        {
            return;
        }

        Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>(true);
        Collider[] projectileColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ownerColliders.Length; i++)
        {
            for (int j = 0; j < projectileColliders.Length; j++)
            {
                Collider ownerCollider = ownerColliders[i];
                Collider projectileCollider = projectileColliders[j];
                if (ownerCollider == null || projectileCollider == null)
                {
                    continue;
                }

                Physics.IgnoreCollision(ownerCollider, projectileCollider, true);
                ignoredOwnerColliders.Add(ownerCollider);
                ignoredProjectileColliders.Add(projectileCollider);
            }
        }
    }

    private void RestoreIgnoredCollisions()
    {
        int count = Mathf.Min(ignoredOwnerColliders.Count, ignoredProjectileColliders.Count);
        for (int i = 0; i < count; i++)
        {
            Collider ownerCollider = ignoredOwnerColliders[i];
            Collider projectileCollider = ignoredProjectileColliders[i];
            if (ownerCollider != null && projectileCollider != null)
            {
                Physics.IgnoreCollision(ownerCollider, projectileCollider, false);
            }
        }

        ignoredOwnerColliders.Clear();
        ignoredProjectileColliders.Clear();
    }

    private void EnsureCachedReferences()
    {
        if (cachedBody == null)
        {
            cachedBody = GetComponent<Rigidbody>();
        }

        if (cachedCollider == null)
        {
            cachedCollider = GetComponent<SphereCollider>();
        }

        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
        }
    }

    private void Update()
    {
        if (exploded)
        {
            return;
        }

        AlignToVelocityIfNeeded();
        SpawnProjectileTrailIfNeeded();

        if (Time.time >= detonateTime)
        {
            Explode(transform.position);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (exploded)
        {
            return;
        }

        if (((1 << collision.collider.gameObject.layer) & collisionMask.value) == 0)
        {
            return;
        }

        Vector3 explosionPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;
        Vector3 explosionNormal = collision.contactCount > 0
            ? collision.GetContact(0).normal
            : -transform.forward;

        if (impactDamage > 0f)
        {
            RetroDamageable directDamageable = collision.collider.GetComponentInParent<RetroDamageable>();
            if (directDamageable != null)
            {
                directDamageable.ApplyDamage(impactDamage, explosionPoint, explosionNormal, source);
            }
            else
            {
                collision.collider.SendMessageUpwards("ApplyDamage", impactDamage, SendMessageOptions.DontRequireReceiver);
            }
        }

        Explode(explosionPoint);
    }

    private void Explode(Vector3 explosionPoint)
    {
        if (exploded)
        {
            return;
        }

        exploded = true;
        Collider[] hits = Physics.OverlapSphere(explosionPoint, explosionRadius, collisionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            float distance = Vector3.Distance(explosionPoint, hit.ClosestPoint(explosionPoint));
            float falloff = Mathf.Clamp01(1f - distance / Mathf.Max(0.01f, explosionRadius));
            if (falloff <= 0f)
            {
                continue;
            }

            RetroDamageable damageable = hit.GetComponentInParent<RetroDamageable>();
            if (damageable != null)
            {
                if (damagedTargets.Add(damageable))
                {
                    Vector3 closestPoint = hit.ClosestPoint(explosionPoint);
                    Vector3 hitNormal = (closestPoint - explosionPoint).sqrMagnitude > 0.0001f
                        ? (closestPoint - explosionPoint).normalized
                        : Vector3.up;
                    damageable.ApplyDamage(damage * falloff, closestPoint, hitNormal, source);
                }
            }
            else
            {
                hit.SendMessageUpwards("ApplyDamage", damage * falloff, SendMessageOptions.DontRequireReceiver);
            }

            if (hit.attachedRigidbody != null)
            {
                hit.attachedRigidbody.AddExplosionForce(explosionForce * falloff, explosionPoint, explosionRadius, 0.15f, ForceMode.Impulse);
            }
        }

        SpawnExplosionFlash(explosionPoint);
        RetroGameContext.Events.Publish(new RetroExplosionEvent(source != null ? source : gameObject, explosionPoint, explosionRadius, damage, effectColor));
        ReturnOrDestroy();
    }

    private void AlignToVelocityIfNeeded()
    {
        if (!alignVisualToVelocity || cachedBody == null)
        {
            return;
        }

        Vector3 velocity = CurrentVelocity;
        if (velocity.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.FromToRotation(Vector3.up, velocity.normalized);
    }

    private void SpawnProjectileTrailIfNeeded()
    {
        if (trailInterval <= 0f || trailWidth <= 0f || trailDuration <= 0f || Time.time < nextTrailTime)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        if ((currentPosition - previousTrailPosition).sqrMagnitude <= 0.0001f)
        {
            nextTrailTime = Time.time + trailInterval;
            return;
        }

        Color trailColor = effectColor;
        trailColor.a = Mathf.Min(trailColor.a, 0.42f);
        RetroGameContext.Vfx.SpawnBulletTrail(gameObject.name, previousTrailPosition, currentPosition, trailColor, trailWidth, trailDuration);
        previousTrailPosition = currentPosition;
        nextTrailTime = Time.time + trailInterval;
    }

    private Vector3 CurrentVelocity
    {
        get
        {
#if UNITY_6000_0_OR_NEWER
            return cachedBody != null ? cachedBody.linearVelocity : Vector3.zero;
#else
            return cachedBody != null ? cachedBody.velocity : Vector3.zero;
#endif
        }
    }

    private void SpawnExplosionFlash(Vector3 explosionPoint)
    {
        RetroGameContext.Vfx.SpawnExplosionFlash(explosionPoint, effectColor, explosionRadius, 0.12f);
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

    private static Material CreateRuntimeMaterial(string materialName, Color baseColor, bool emissive)
    {
        Shader shader = Shader.Find("HDRP/Lit");
        shader ??= Shader.Find("Standard");
        shader ??= Shader.Find("Diffuse");

        Material material = new Material(shader)
        {
            name = materialName
        };

        ApplyRuntimeMaterialColor(material, baseColor, emissive);
        return material;
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

        if (emissive)
        {
            Color emission = baseColor * 4f;
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

}
