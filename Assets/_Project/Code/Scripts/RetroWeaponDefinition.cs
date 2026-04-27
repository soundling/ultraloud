using UnityEngine;

public enum RetroWeaponKind
{
    Hitscan,
    GrenadeLauncher,
    RocketLauncher
}

public enum RetroFireMode
{
    SemiAuto,
    Automatic
}

[CreateAssetMenu(menuName = "Ultraloud/Weapons/Weapon Definition", fileName = "RetroWeaponDefinition")]
public sealed class RetroWeaponDefinition : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Weapon";
    public RetroWeaponKind kind = RetroWeaponKind.Hitscan;
    public RetroFireMode fireMode = RetroFireMode.SemiAuto;

    [Header("Ammo")]
    [Min(1)] public int magazineSize = 12;
    [Min(0)] public int startingReserveAmmo = 72;
    [Min(0)] public int maxReserveAmmo = 72;
    [Min(0.01f)] public float reloadDuration = 1.2f;
    [Min(0.01f)] public float fireInterval = 0.28f;

    [Header("Weapon Feel")]
    public bool autoReloadWhenEmpty = true;
    [Min(0.01f)] public float dryFireCooldown = 0.16f;
    public Vector3 dryFireKickPosition = new Vector3(0f, 0.001f, -0.012f);
    public Vector3 dryFireKickEuler = new Vector3(1.2f, 0.25f, 0.35f);
    [Min(0f)] public float spreadBloomPerShot = 0.18f;
    [Min(0f)] public float maxSpreadAngle = 1.4f;
    [Min(0f)] public float spreadRecoverySpeed = 6f;
    [Min(0f)] public float movementSpreadPenalty = 0.25f;

    [Header("Combat")]
    [Min(0f)] public float damage = 28f;
    [Min(0.01f)] public float range = 110f;
    [Min(1)] public int pellets = 1;
    [Min(0f)] public float spreadAngle = 0.45f;
    [Min(0f)] public float impactForce = 18f;

    [Header("Projectile")]
    [Min(0f)] public float projectileSpeed = 28f;
    [Min(0f)] public float explosionRadius = 0f;
    [Min(0f)] public float explosionForce = 0f;
    [Min(0f)] public float fuseTime = 0f;

    [Header("Presentation")]
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localEuler = Vector3.zero;
    public Vector3 recoilPosition = new Vector3(0f, 0.006f, -0.06f);
    public Vector3 recoilEuler = new Vector3(4.5f, 1.2f, 1.6f);
    public Color bodyColor = new Color(0.13f, 0.13f, 0.15f);
    public Color accentColor = new Color(0.72f, 0.68f, 0.58f);
    [Min(0f)] public float primitiveMuzzleFlashScale = 0.18f;

    [Header("Audio")]
    public RetroAudioCue fireCue;
    public RetroAudioCue dryFireCue;
    public RetroAudioCue reloadStartCue;
    public RetroAudioCue reloadCompleteCue;
    public RetroAudioCue selectCue;

    [Header("Bullet Trails")]
    public bool bulletTrailEnabled = true;
    public Color bulletTrailColor = new Color(1f, 0.82f, 0.35f, 0.78f);
    [Min(0.001f)] public float bulletTrailWidth = 0.018f;
    [Min(0.01f)] public float bulletTrailDuration = 0.065f;
    [Min(0f)] public float bulletTrailStartOffset = 0.08f;
    [Min(0f)] public float bulletTrailEndOffset = 0.08f;
    [Range(1, 16)] public int bulletTrailMaxSegmentsPerShot = 1;

    [Header("Sprite Viewmodel")]
    public FirstPersonSpriteVolumeMapSet spriteMapSet;
    public FirstPersonSpriteVolumeMapSet[] fireAnimationMapSets = new FirstPersonSpriteVolumeMapSet[0];
    [Min(0.005f)] public float fireAnimationFrameDuration = 0.024f;
    public Vector3 spriteVisualLocalPosition = new Vector3(-0.02f, -0.02f, 0f);
    public Vector3 spriteVisualLocalEuler = Vector3.zero;
    public Vector2 spriteVisualSize = new Vector2(0.95f, 0.72f);
    public Vector3 spriteMuzzleLocalPosition = new Vector3(0f, 0.02f, 0.34f);
    public Vector3 spriteMuzzleLocalOffset = new Vector3(0.055f, 0f, 0f);
    public Sprite muzzleFlashSprite;
    public Sprite[] muzzleFlashFrames = new Sprite[0];
    [Min(0.005f)] public float muzzleFlashFrameDuration = 0.018f;
    public Vector2 muzzleFlashSpriteSize = new Vector2(0.38f, 0.28f);
    public Color baseTintMultiplier = Color.white;
    public Color emissiveTintMultiplier = Color.white;
    [Range(0f, 1f)] public float weaponBodyTint = 0.18f;
    [Range(0f, 1f)] public float weaponAccentTint = 0.7f;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = name;
        }

        magazineSize = Mathf.Max(1, magazineSize);
        startingReserveAmmo = Mathf.Max(0, startingReserveAmmo);
        maxReserveAmmo = Mathf.Max(startingReserveAmmo, maxReserveAmmo);
        reloadDuration = Mathf.Max(0.01f, reloadDuration);
        fireInterval = Mathf.Max(0.01f, fireInterval);
        dryFireCooldown = Mathf.Max(0.01f, dryFireCooldown);
        spreadBloomPerShot = Mathf.Max(0f, spreadBloomPerShot);
        spreadRecoverySpeed = Mathf.Max(0f, spreadRecoverySpeed);
        movementSpreadPenalty = Mathf.Max(0f, movementSpreadPenalty);
        damage = Mathf.Max(0f, damage);
        range = Mathf.Max(0.01f, range);
        pellets = Mathf.Max(1, pellets);
        spreadAngle = Mathf.Max(0f, spreadAngle);
        maxSpreadAngle = Mathf.Max(spreadAngle, maxSpreadAngle);
        impactForce = Mathf.Max(0f, impactForce);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        explosionRadius = Mathf.Max(0f, explosionRadius);
        explosionForce = Mathf.Max(0f, explosionForce);
        fuseTime = Mathf.Max(0f, fuseTime);
        primitiveMuzzleFlashScale = Mathf.Max(0f, primitiveMuzzleFlashScale);
        bulletTrailWidth = Mathf.Max(0.001f, bulletTrailWidth);
        bulletTrailDuration = Mathf.Max(0.01f, bulletTrailDuration);
        bulletTrailStartOffset = Mathf.Max(0f, bulletTrailStartOffset);
        bulletTrailEndOffset = Mathf.Max(0f, bulletTrailEndOffset);
        bulletTrailMaxSegmentsPerShot = Mathf.Clamp(bulletTrailMaxSegmentsPerShot, 1, 16);
        spriteVisualSize.x = Mathf.Max(0.01f, spriteVisualSize.x);
        spriteVisualSize.y = Mathf.Max(0.01f, spriteVisualSize.y);
        fireAnimationFrameDuration = Mathf.Max(0.005f, fireAnimationFrameDuration);
        muzzleFlashFrameDuration = Mathf.Max(0.005f, muzzleFlashFrameDuration);
        muzzleFlashSpriteSize.x = Mathf.Max(0.01f, muzzleFlashSpriteSize.x);
        muzzleFlashSpriteSize.y = Mathf.Max(0.01f, muzzleFlashSpriteSize.y);
    }
}
