using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RetroArtifactPickupFx : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
    private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
    private static readonly int ArtifactGlowStrengthId = Shader.PropertyToID("_ArtifactGlowStrength");

    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] rotationFrames = Array.Empty<Sprite>();
    [SerializeField, Min(1f)] private float frameRate = 36f;
    [SerializeField] private bool billboardToCamera = true;
    [SerializeField] private bool yawOnly = true;
    [SerializeField, Min(0f)] private float bobAmplitude = 0.13f;
    [SerializeField, Min(0f)] private float bobFrequency = 1.15f;
    [SerializeField, Min(0f)] private float secondaryBobAmplitude = 0.035f;
    [SerializeField, Min(0f)] private float secondaryBobFrequency = 2.55f;
    [SerializeField, Min(0f)] private float rollAmplitude = 3.5f;
    [SerializeField, Min(0f)] private float rollFrequency = 0.9f;
    [SerializeField] private Color baseTint = Color.white;
    [SerializeField] private Color pulseTint = new(0.82f, 0.94f, 1.06f, 1f);
    [SerializeField, Range(0f, 1f)] private float tintPulseBlend = 0.24f;
    [SerializeField, Min(0f)] private float emissionMin = 0.35f;
    [SerializeField, Min(0f)] private float emissionMax = 1.25f;
    [SerializeField, Min(0f)] private float artifactGlowMin = 0.18f;
    [SerializeField, Min(0f)] private float artifactGlowMax = 0.72f;
    [SerializeField, Min(0f)] private float rimMin = 0.08f;
    [SerializeField, Min(0f)] private float rimMax = 0.36f;

    private Vector3 visualBaseLocalPosition;
    private Quaternion visualBaseLocalRotation;
    private MaterialPropertyBlock propertyBlock;
    private float phaseOffset;
    private int lastFrame = -1;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        if (visualRoot != null)
        {
            visualBaseLocalPosition = visualRoot.localPosition;
            visualBaseLocalRotation = visualRoot.localRotation;
        }

        phaseOffset = UnityEngine.Random.value * 100f;
        propertyBlock = new MaterialPropertyBlock();
        ApplyFrame(0);
    }

    private void OnValidate()
    {
        frameRate = Mathf.Max(1f, frameRate);
        bobAmplitude = Mathf.Max(0f, bobAmplitude);
        bobFrequency = Mathf.Max(0f, bobFrequency);
        secondaryBobAmplitude = Mathf.Max(0f, secondaryBobAmplitude);
        secondaryBobFrequency = Mathf.Max(0f, secondaryBobFrequency);
        rollAmplitude = Mathf.Max(0f, rollAmplitude);
        rollFrequency = Mathf.Max(0f, rollFrequency);
        tintPulseBlend = Mathf.Clamp01(tintPulseBlend);
        emissionMin = Mathf.Max(0f, emissionMin);
        emissionMax = Mathf.Max(emissionMin, emissionMax);
        artifactGlowMin = Mathf.Max(0f, artifactGlowMin);
        artifactGlowMax = Mathf.Max(artifactGlowMin, artifactGlowMax);
        rimMin = Mathf.Max(0f, rimMin);
        rimMax = Mathf.Max(rimMin, rimMax);
        ResolveReferences();
    }

    private void LateUpdate()
    {
        float time = Time.time + phaseOffset;
        AnimateFrame(time);
        AnimatePose(time);
        AnimateMaterial(time);
    }

    private void ResolveReferences()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (visualRoot == null)
        {
            visualRoot = spriteRenderer != null ? spriteRenderer.transform : transform;
        }
    }

    private void AnimateFrame(float time)
    {
        if (rotationFrames == null || rotationFrames.Length == 0)
        {
            return;
        }

        int frame = Mathf.FloorToInt(time * frameRate) % rotationFrames.Length;
        ApplyFrame(frame);
    }

    private void ApplyFrame(int frame)
    {
        if (spriteRenderer == null || rotationFrames == null || rotationFrames.Length == 0)
        {
            return;
        }

        frame = Mathf.Clamp(frame, 0, rotationFrames.Length - 1);
        if (frame == lastFrame || rotationFrames[frame] == null)
        {
            return;
        }

        spriteRenderer.sprite = rotationFrames[frame];
        lastFrame = frame;
    }

    private void AnimatePose(float time)
    {
        if (visualRoot == null)
        {
            return;
        }

        float bob = Mathf.Sin(time * Mathf.PI * 2f * bobFrequency) * bobAmplitude
            + Mathf.Sin(time * Mathf.PI * 2f * secondaryBobFrequency + 1.7f) * secondaryBobAmplitude;
        visualRoot.localPosition = visualBaseLocalPosition + Vector3.up * bob;

        float roll = Mathf.Sin(time * Mathf.PI * 2f * rollFrequency) * rollAmplitude;
        if (billboardToCamera)
        {
            Camera targetCamera = Camera.main;
            if (targetCamera != null)
            {
                Vector3 toCamera = targetCamera.transform.position - visualRoot.position;
                if (yawOnly)
                {
                    toCamera.y = 0f;
                }

                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    visualRoot.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
                    return;
                }
            }
        }

        visualRoot.localRotation = visualBaseLocalRotation * Quaternion.Euler(0f, 0f, roll);
    }

    private void AnimateMaterial(float time)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        float pulse = Mathf.InverseLerp(-1f, 1f, Mathf.Sin(time * Mathf.PI * 2f * 1.45f));
        Color tint = Color.Lerp(baseTint, pulseTint, pulse * tintPulseBlend);
        float emission = Mathf.Lerp(emissionMin, emissionMax, pulse);
        float artifactGlow = Mathf.Lerp(artifactGlowMin, artifactGlowMax, pulse);
        float rim = Mathf.Lerp(rimMin, rimMax, pulse);

        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, tint);
        propertyBlock.SetFloat(EmissionStrengthId, emission);
        propertyBlock.SetFloat(RimStrengthId, rim);
        propertyBlock.SetFloat(ArtifactGlowStrengthId, artifactGlow);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }
}
