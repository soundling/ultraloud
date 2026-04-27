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
    private float damage;
    private float explosionRadius;
    private float explosionForce;
    private float impactDamage;
    private float detonateTime;
    private LayerMask collisionMask;
    private Color effectColor;
    private bool exploded;

    public void ConfigureVisual(string objectName, Color color, float scale)
    {
        EnsureCachedReferences();
        gameObject.name = string.IsNullOrWhiteSpace(objectName) ? "Grenade Projectile" : objectName;
        transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);

        if (cachedRenderer == null)
        {
            return;
        }

        if (runtimeMaterial == null)
        {
            runtimeMaterial = CreateRuntimeMaterial("Grenade Projectile", color, true);
            cachedRenderer.sharedMaterial = runtimeMaterial;
        }
        else
        {
            runtimeMaterial.name = "Grenade Projectile";
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
        Color effectColor)
    {
        EnsureCachedReferences();
        RestoreIgnoredCollisions();
        damagedTargets.Clear();
        exploded = false;
        this.damage = damage;
        this.explosionRadius = explosionRadius;
        this.explosionForce = explosionForce;
        this.impactDamage = impactDamage;
        this.collisionMask = collisionMask;
        this.effectColor = effectColor;
        detonateTime = Time.time + Mathf.Max(0f, fuseTime);
        cachedCollider.radius = 0.5f;
        cachedCollider.enabled = true;
        cachedBody.useGravity = true;
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
        exploded = true;
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
        if (!exploded && Time.time >= detonateTime)
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
                directDamageable.ApplyDamage(impactDamage, explosionPoint, explosionNormal);
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
                    damageable.ApplyDamage(damage * falloff, closestPoint, hitNormal);
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
        RetroGameContext.Events.Publish(new RetroExplosionEvent(gameObject, explosionPoint, explosionRadius, damage, effectColor));
        ReturnOrDestroy();
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
