using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "FirstPersonSpriteVolumeMapSet", menuName = "Ultraloud/First Person Sprite/Volume Map Set")]
public sealed class FirstPersonSpriteVolumeMapSet : ScriptableObject
{
    [Header("Textures")]
    public Texture2D baseColor;
    [FormerlySerializedAs("detailNormal")] public Texture2D normal;
    public Texture2D frontDepth;
    public Texture2D emissive;

    [Header("Shape Tricks")]
    [Range(0f, 0.2f)] public float volumeThickness = 0.06f;
    [Range(0f, 0.08f)] public float parallaxScale = 0.012f;
    public bool invertFrontDepth;

    [Header("Material")]
    public Color baseColorTint = Color.white;
    public Color emissiveColor = Color.black;
    [Range(0f, 2f)] public float normalScale = 1f;
    [Range(0f, 1f)] public float alphaCutoff = 0.08f;
    public bool preserveBaseCoverage = true;
    [Range(0f, 0.2f)] public float coverageThreshold = 0.02f;
    [Range(0f, 1f)] public float selfShadowStrength = 0.25f;
    [Range(0f, 4f)] public float transmissionStrength = 0.2f;
    [Range(0f, 4f)] public float ambientOcclusionStrength = 1f;
    [Range(0f, 4f)] public float specularStrength = 1.2f;

    [Header("Material Fallbacks")]
    [Range(0f, 1f)] public float ambientOcclusion = 1f;
    [Range(0f, 1f)] public float roughness = 0.55f;
    [Range(0f, 1f)] public float metallic;
    [Range(0f, 1f)] public float materialThickness = 0.65f;

    private void OnValidate()
    {
        volumeThickness = Mathf.Clamp(volumeThickness, 0f, 0.2f);
        parallaxScale = Mathf.Clamp(parallaxScale, 0f, 0.08f);
        normalScale = Mathf.Clamp(normalScale, 0f, 2f);
        alphaCutoff = Mathf.Clamp01(alphaCutoff);
        coverageThreshold = Mathf.Clamp(coverageThreshold, 0f, 0.2f);
        selfShadowStrength = Mathf.Clamp01(selfShadowStrength);
        transmissionStrength = Mathf.Clamp(transmissionStrength, 0f, 4f);
        ambientOcclusionStrength = Mathf.Clamp(ambientOcclusionStrength, 0f, 4f);
        specularStrength = Mathf.Clamp(specularStrength, 0f, 4f);
        ambientOcclusion = Mathf.Clamp01(ambientOcclusion);
        roughness = Mathf.Clamp01(roughness);
        metallic = Mathf.Clamp01(metallic);
        materialThickness = Mathf.Clamp01(materialThickness);
    }
}
