using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class RetroCursedParkSpriteProp : MonoBehaviour
{
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
    private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
    private static readonly int AlphaCutoffId = Shader.PropertyToID("_AlphaCutoff");
    private static readonly int SpecularStrengthId = Shader.PropertyToID("_SpecularStrength");

    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Texture2D baseMap;
    [SerializeField] private Texture2D emissionMap;
    [SerializeField] private bool billboardYawOnly = true;
    [SerializeField] private bool groundDecal;
    [SerializeField] private Color tint = Color.white;
    [SerializeField] private Color emissionColor = Color.white;
    [SerializeField] private Color rimColor = new(1f, 0.42f, 0.12f, 1f);
    [SerializeField, Min(0f)] private float emissionStrength = 0.8f;
    [SerializeField, Min(0f)] private float flickerStrength = 0.25f;
    [SerializeField, Min(0f)] private float rimStrength = 0.2f;
    [SerializeField, Min(0f)] private float swayStrength = 0.2f;
    [SerializeField, Min(0f)] private float alphaCutoff = 0.04f;

    private MaterialPropertyBlock propertyBlock;
    private Quaternion authoredLocalRotation;
    private float phaseOffset = -1f;

    private void Reset()
    {
        AutoAssign();
    }

    private void OnEnable()
    {
        AutoAssign();
        authoredLocalRotation = transform.localRotation;
        EnsurePhaseOffset();
        ApplyProperties();
    }

    private void OnValidate()
    {
        emissionStrength = Mathf.Max(0f, emissionStrength);
        flickerStrength = Mathf.Max(0f, flickerStrength);
        rimStrength = Mathf.Max(0f, rimStrength);
        swayStrength = Mathf.Max(0f, swayStrength);
        alphaCutoff = Mathf.Max(0f, alphaCutoff);
        AutoAssign();
        ApplyProperties();
    }

    private void LateUpdate()
    {
        Quaternion? billboardRotation = null;
        if (!groundDecal && billboardYawOnly)
        {
            Camera activeCamera = Camera.main;
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
            {
                activeCamera = UnityEditor.SceneView.lastActiveSceneView.camera;
            }
#endif
            if (activeCamera != null)
            {
                Vector3 toCamera = activeCamera.transform.position - transform.position;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    billboardRotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
                }
            }
        }

        float sway = 0f;
        if (!groundDecal && swayStrength > 0f)
        {
            EnsurePhaseOffset();
            float t = Time.realtimeSinceStartup * 1.7f + phaseOffset;
            sway = Mathf.Sin(t) * swayStrength;
        }

        if (billboardRotation.HasValue)
        {
            transform.rotation = billboardRotation.Value * Quaternion.Euler(0f, 0f, sway);
        }
        else if (swayStrength > 0f)
        {
            transform.localRotation = authoredLocalRotation * Quaternion.Euler(0f, 0f, sway);
        }

        ApplyProperties();
    }

    public void Configure(RetroCursedParkSpriteAsset asset, Renderer rendererOverride = null)
    {
        if (asset == null)
        {
            return;
        }

        targetRenderer = rendererOverride != null ? rendererOverride : targetRenderer;
        baseMap = asset.BaseMap;
        emissionMap = asset.EmissionMap;
        billboardYawOnly = asset.Billboard && !asset.GroundDecal;
        groundDecal = asset.GroundDecal;
        tint = asset.Tint;
        emissionColor = asset.EmissionColor;
        rimColor = asset.RimColor;
        emissionStrength = asset.GlowStrength;
        flickerStrength = asset.FlickerStrength;
        rimStrength = asset.GlowStrength * 0.18f;
        swayStrength = asset.SwayStrength;
        ApplyProperties();
    }

    private void AutoAssign()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }
    }

    private void ApplyProperties()
    {
        if (targetRenderer == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        if (baseMap != null)
        {
            propertyBlock.SetTexture(MainTexId, baseMap);
        }

        if (emissionMap != null)
        {
            propertyBlock.SetTexture(EmissionMapId, emissionMap);
        }

        float flicker = 1f;
        if (flickerStrength > 0f)
        {
            EnsurePhaseOffset();
            float t = Time.realtimeSinceStartup * 8.7f + phaseOffset * 1.61f;
            flicker += Mathf.Sin(t) * flickerStrength + Mathf.Sin(t * 2.31f) * flickerStrength * 0.35f;
        }

        propertyBlock.SetColor(BaseColorId, tint);
        propertyBlock.SetColor(EmissionColorId, emissionColor);
        propertyBlock.SetColor(RimColorId, rimColor);
        propertyBlock.SetFloat(EmissionStrengthId, Mathf.Max(0f, emissionStrength * flicker));
        propertyBlock.SetFloat(RimStrengthId, rimStrength * Mathf.Max(0.25f, flicker));
        propertyBlock.SetFloat(AlphaCutoffId, alphaCutoff);
        propertyBlock.SetFloat(SpecularStrengthId, groundDecal ? 0.05f : 0.22f);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void EnsurePhaseOffset()
    {
        if (phaseOffset < 0f)
        {
            phaseOffset = Random.Range(0f, 1000f);
        }
    }
}
