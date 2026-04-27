using UnityEngine;

[CreateAssetMenu(fileName = "RetroGoreProfile", menuName = "Ultraloud/VFX/Gore Profile")]
public sealed class RetroGoreProfile : ScriptableObject
{
    [Header("Atlas Maps")]
    [SerializeField] private Texture2D baseAtlas;
    [SerializeField] private Texture2D normalAtlas;
    [SerializeField] private Texture2D packedMasksAtlas;
    [SerializeField] private Texture2D emissionAtlas;
    [SerializeField, Range(1, 16)] private int atlasColumns = 4;
    [SerializeField, Range(1, 16)] private int atlasRows = 4;
    [SerializeField, Range(1, 256)] private int frameCount = 16;

    [Header("Gib Trigger")]
    [SerializeField, Min(0.02f)] private float damageWindow = 0.9f;
    [SerializeField, Min(0.02f)] private float clusterWindow = 0.18f;
    [SerializeField, Min(0f)] private float minimumRecentDamage = 72f;
    [SerializeField, Min(0f)] private float minimumClusterDamage = 48f;
    [SerializeField, Range(1, 32)] private int minimumClusterHits = 4;
    [SerializeField, Min(0f)] private float highSingleHitDamage = 80f;
    [SerializeField, Min(0f)] private float gibCooldown = 0.05f;

    [Header("Burst Sprites")]
    [SerializeField, Range(0, 64)] private int bloodPuffCount = 18;
    [SerializeField, Range(0, 64)] private int streakCount = 14;
    [SerializeField, Range(0, 64)] private int spriteChunkCount = 14;
    [SerializeField, Range(0, 64)] private int boneSpriteCount = 8;
    [SerializeField, Range(0, 48)] private int decalCount = 10;
    [SerializeField] private Vector2 puffSizeRange = new(0.42f, 1.15f);
    [SerializeField] private Vector2 streakSizeRange = new(0.35f, 1.25f);
    [SerializeField] private Vector2 chunkSizeRange = new(0.28f, 0.72f);
    [SerializeField] private Vector2 decalSizeRange = new(0.85f, 2.8f);
    [SerializeField] private Vector2 spriteLifetimeRange = new(0.55f, 1.45f);
    [SerializeField] private Vector2 decalLifetimeRange = new(5.5f, 12f);

    [Header("Hybrid Mesh Chunks")]
    [SerializeField, Range(0, 64)] private int meshChunkCount = 18;
    [SerializeField] private Vector2 meshChunkSizeRange = new(0.08f, 0.28f);
    [SerializeField] private Vector2 meshChunkLifetimeRange = new(2.4f, 5.5f);
    [SerializeField] private Color meatChunkColor = new(0.45f, 0.015f, 0.018f, 1f);
    [SerializeField] private Color skinChunkColor = new(0.95f, 0.48f, 0.42f, 1f);
    [SerializeField] private Color boneChunkColor = new(0.92f, 0.78f, 0.55f, 1f);

    [Header("Motion")]
    [SerializeField] private Vector2 radialSpeedRange = new(3.5f, 12f);
    [SerializeField] private Vector2 upwardSpeedRange = new(1.1f, 6.5f);
    [SerializeField] private Vector2 gravityRange = new(5f, 13f);
    [SerializeField, Range(0f, 1f)] private float forwardBias = 0.62f;
    [SerializeField, Range(0f, 4f)] private float intensityScale = 1f;
    [SerializeField, Min(0f)] private float spawnRadius = 0.42f;
    [SerializeField, Min(0f)] private float screenFlashRadius = 1.4f;

    public Texture2D BaseAtlas => baseAtlas;
    public Texture2D NormalAtlas => normalAtlas;
    public Texture2D PackedMasksAtlas => packedMasksAtlas;
    public Texture2D EmissionAtlas => emissionAtlas;
    public int AtlasColumns => Mathf.Max(1, atlasColumns);
    public int AtlasRows => Mathf.Max(1, atlasRows);
    public int FrameCount => Mathf.Clamp(frameCount, 1, AtlasColumns * AtlasRows);
    public float DamageWindow => Mathf.Max(0.02f, damageWindow);
    public float ClusterWindow => Mathf.Max(0.02f, clusterWindow);
    public float MinimumRecentDamage => Mathf.Max(0f, minimumRecentDamage);
    public float MinimumClusterDamage => Mathf.Max(0f, minimumClusterDamage);
    public int MinimumClusterHits => Mathf.Max(1, minimumClusterHits);
    public float HighSingleHitDamage => Mathf.Max(0f, highSingleHitDamage);
    public float GibCooldown => Mathf.Max(0f, gibCooldown);
    public int BloodPuffCount => Mathf.Max(0, bloodPuffCount);
    public int StreakCount => Mathf.Max(0, streakCount);
    public int SpriteChunkCount => Mathf.Max(0, spriteChunkCount);
    public int BoneSpriteCount => Mathf.Max(0, boneSpriteCount);
    public int DecalCount => Mathf.Max(0, decalCount);
    public int MeshChunkCount => Mathf.Max(0, meshChunkCount);
    public Vector2 PuffSizeRange => ClampRange(puffSizeRange, 0.01f);
    public Vector2 StreakSizeRange => ClampRange(streakSizeRange, 0.01f);
    public Vector2 ChunkSizeRange => ClampRange(chunkSizeRange, 0.01f);
    public Vector2 DecalSizeRange => ClampRange(decalSizeRange, 0.01f);
    public Vector2 SpriteLifetimeRange => ClampRange(spriteLifetimeRange, 0.01f);
    public Vector2 DecalLifetimeRange => ClampRange(decalLifetimeRange, 0.01f);
    public Vector2 MeshChunkSizeRange => ClampRange(meshChunkSizeRange, 0.01f);
    public Vector2 MeshChunkLifetimeRange => ClampRange(meshChunkLifetimeRange, 0.01f);
    public Color MeatChunkColor => meatChunkColor;
    public Color SkinChunkColor => skinChunkColor;
    public Color BoneChunkColor => boneChunkColor;
    public Vector2 RadialSpeedRange => ClampRange(radialSpeedRange, 0f);
    public Vector2 UpwardSpeedRange => ClampRange(upwardSpeedRange, 0f);
    public Vector2 GravityRange => ClampRange(gravityRange, 0f);
    public float ForwardBias => Mathf.Clamp01(forwardBias);
    public float IntensityScale => Mathf.Max(0f, intensityScale);
    public float SpawnRadius => Mathf.Max(0f, spawnRadius);
    public float ScreenFlashRadius => Mathf.Max(0f, screenFlashRadius);

    private void OnValidate()
    {
        atlasColumns = Mathf.Clamp(atlasColumns, 1, 16);
        atlasRows = Mathf.Clamp(atlasRows, 1, 16);
        frameCount = Mathf.Clamp(frameCount, 1, atlasColumns * atlasRows);
        damageWindow = Mathf.Max(0.02f, damageWindow);
        clusterWindow = Mathf.Max(0.02f, clusterWindow);
        minimumClusterHits = Mathf.Max(1, minimumClusterHits);
        gibCooldown = Mathf.Max(0f, gibCooldown);
        puffSizeRange = ClampRange(puffSizeRange, 0.01f);
        streakSizeRange = ClampRange(streakSizeRange, 0.01f);
        chunkSizeRange = ClampRange(chunkSizeRange, 0.01f);
        decalSizeRange = ClampRange(decalSizeRange, 0.01f);
        spriteLifetimeRange = ClampRange(spriteLifetimeRange, 0.01f);
        decalLifetimeRange = ClampRange(decalLifetimeRange, 0.01f);
        meshChunkSizeRange = ClampRange(meshChunkSizeRange, 0.01f);
        meshChunkLifetimeRange = ClampRange(meshChunkLifetimeRange, 0.01f);
        radialSpeedRange = ClampRange(radialSpeedRange, 0f);
        upwardSpeedRange = ClampRange(upwardSpeedRange, 0f);
        gravityRange = ClampRange(gravityRange, 0f);
        forwardBias = Mathf.Clamp01(forwardBias);
        intensityScale = Mathf.Max(0f, intensityScale);
        spawnRadius = Mathf.Max(0f, spawnRadius);
        screenFlashRadius = Mathf.Max(0f, screenFlashRadius);
    }

    public int RandomBloodFrame()
    {
        return Random.Range(0, Mathf.Min(FrameCount, 4));
    }

    public int RandomMeatFrame()
    {
        return Random.Range(Mathf.Min(4, FrameCount - 1), Mathf.Min(FrameCount, 8));
    }

    public int RandomBoneFrame()
    {
        int min = Mathf.Min(8, FrameCount - 1);
        int max = Mathf.Min(FrameCount, 11);
        return Random.Range(min, Mathf.Max(min + 1, max));
    }

    public int RandomStreakFrame()
    {
        int min = Mathf.Min(11, FrameCount - 1);
        int max = Mathf.Min(FrameCount, 14);
        return Random.Range(min, Mathf.Max(min + 1, max));
    }

    public int RandomClusterFrame()
    {
        int min = Mathf.Min(14, FrameCount - 1);
        return Random.Range(min, FrameCount);
    }

    private static Vector2 ClampRange(Vector2 range, float minValue)
    {
        range.x = Mathf.Max(minValue, range.x);
        range.y = Mathf.Max(range.x, range.y);
        return range;
    }
}
