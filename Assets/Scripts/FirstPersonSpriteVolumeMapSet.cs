using UnityEngine;

[CreateAssetMenu(fileName = "FirstPersonSpriteVolumeMapSet", menuName = "Ultraloud/First Person Sprite/Volume Map Set")]
public sealed class FirstPersonSpriteVolumeMapSet : ScriptableObject
{
    [Header("Textures")]
    public Texture2D baseColor;
    public Texture2D detailNormal;
    public Texture2D macroNormal;
    public Texture2D frontDepth;
    public Texture2D backDepth;
    public Texture2D thickness;
    public Texture2D packedMasks;
    public Texture2D shellOccupancyAtlas;
    public Texture2D sdf;
    public Texture2D emissive;

    [Header("Volume")]
    [Min(0f)] public float volumeThickness = 0.12f;
    [Min(0f)] public float parallaxScale = 0.08f;
    [Min(1)] public int raymarchSteps = 24;
    [Min(1)] public int shadowSteps = 12;
    [Min(1)] public int shellSliceCount = 16;
    public Vector2Int shellAtlasGrid = new(4, 4);

    [Header("Depth Decoding")]
    public bool invertFrontDepth;
    public bool invertBackDepth;
    public bool autoCorrectDualDepth = true;
    [Range(0f, 0.05f)] public float minimumDepthSeparation = 0.01f;

    [Header("Material")]
    public Color baseColorTint = Color.white;
    public Color emissiveColor = Color.black;
    [Range(0f, 2f)] public float detailNormalScale = 1f;
    [Range(0f, 2f)] public float macroNormalScale = 1f;
    [Range(0f, 1f)] public float alphaCutoff = 0.2f;
    [Range(0f, 1f)] public float selfShadowStrength = 0.6f;
    [Range(0f, 4f)] public float transmissionStrength = 0.35f;
    [Range(0f, 4f)] public float ambientOcclusionStrength = 1f;
    [Range(0f, 4f)] public float specularStrength = 1f;
    [Range(0f, 1f)] public float sdfSoftness = 0.08f;
}
