using UnityEngine;

public enum DirectionalBillboardMode
{
    None = 0,
    YAxis = 1,
    Full = 2
}

[DisallowMultipleComponent]
public sealed class DirectionalSpriteAnimator : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private DirectionalSpriteDefinition definition;
    [SerializeField] private string initialClipId = "Idle";
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool useUnscaledTime;
    [SerializeField, Min(0f)] private float animationSpeed = 1f;

    [Header("Scene References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform billboardRoot;
    [SerializeField] private Transform facingReference;
    [SerializeField] private Camera targetCamera;

    [Header("Facing")]
    [SerializeField] private DirectionalBillboardMode billboardMode = DirectionalBillboardMode.YAxis;
    [SerializeField] private Vector3 billboardEulerOffset;
    [SerializeField] private float logicalFacingOffset;

    private DirectionalSpriteClip currentClip;
    private float clipTime;
    private bool isPlaying;

    public DirectionalSpriteDefinition Definition
    {
        get => definition;
        set
        {
            definition = value;
            Play(initialClipId, true);
        }
    }

    public string CurrentClipId => currentClip != null ? currentClip.clipId : string.Empty;

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play(initialClipId, false);
        }

        RefreshVisual();
    }

    private void OnValidate()
    {
        AutoAssignReferences();
    }

    private void Update()
    {
        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        AdvanceAnimation(deltaTime);
    }

    private void LateUpdate()
    {
        RefreshVisual();
    }

    public bool Play(string clipId, bool restart = true)
    {
        if (definition == null)
        {
            currentClip = null;
            return false;
        }

        if (!definition.TryGetClip(string.IsNullOrWhiteSpace(clipId) ? initialClipId : clipId, out DirectionalSpriteClip nextClip))
        {
            currentClip = null;
            return false;
        }

        bool clipChanged = currentClip != nextClip;
        currentClip = nextClip;

        if (clipChanged || restart)
        {
            clipTime = 0f;
        }

        isPlaying = true;
        RefreshVisual();
        return true;
    }

    public void Pause()
    {
        isPlaying = false;
    }

    public void Resume()
    {
        if (currentClip != null)
        {
            isPlaying = true;
        }
    }

    private void AutoAssignReferences()
    {
        if (facingReference == null)
        {
            facingReference = transform;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (billboardRoot == null && spriteRenderer != null)
        {
            billboardRoot = spriteRenderer.transform;
        }

        if (targetCamera == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                targetCamera = mainCamera;
            }
        }
    }

    private void AdvanceAnimation(float deltaTime)
    {
        if (!isPlaying || currentClip == null || animationSpeed <= 0f)
        {
            return;
        }

        float framesPerSecond = currentClip.framesPerSecond > 0f
            ? currentClip.framesPerSecond
            : 1f;

        clipTime += deltaTime * animationSpeed;

        if (!currentClip.loop)
        {
            int maxFrameCount = currentClip.GetMaxFrameCount();
            if (maxFrameCount <= 1)
            {
                clipTime = 0f;
                isPlaying = false;
                return;
            }

            float duration = maxFrameCount / framesPerSecond;
            if (clipTime >= duration)
            {
                clipTime = duration;
                isPlaying = false;
            }
        }
    }

    private void RefreshVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Camera cameraToUse = ResolveCamera();
        if (cameraToUse == null)
        {
            return;
        }

        UpdateBillboard(cameraToUse.transform);

        if (currentClip == null && definition != null)
        {
            currentClip = definition.GetDefaultClip();
        }

        if (currentClip == null)
        {
            spriteRenderer.sprite = null;
            return;
        }

        float relativeYaw = GetRelativeYaw(cameraToUse.transform);
        AngleSelection selection = FindBestAngle(currentClip, relativeYaw);
        if (selection.angle == null)
        {
            spriteRenderer.sprite = null;
            return;
        }

        spriteRenderer.sprite = GetCurrentFrame(selection.angle);
        spriteRenderer.flipX = selection.flipX;
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null && targetCamera.isActiveAndEnabled)
        {
            return targetCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            targetCamera = mainCamera;
        }

        return targetCamera;
    }

    private void UpdateBillboard(Transform cameraTransform)
    {
        if (billboardMode == DirectionalBillboardMode.None || billboardRoot == null)
        {
            return;
        }

        Vector3 directionToCamera = cameraTransform.position - billboardRoot.position;
        if (billboardMode == DirectionalBillboardMode.YAxis)
        {
            directionToCamera = Vector3.ProjectOnPlane(directionToCamera, Vector3.up);
        }

        if (directionToCamera.sqrMagnitude < 0.0001f)
        {
            return;
        }

        billboardRoot.rotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up) * Quaternion.Euler(billboardEulerOffset);
    }

    private float GetRelativeYaw(Transform cameraTransform)
    {
        Transform reference = facingReference != null ? facingReference : transform;
        Vector3 directionToCamera = cameraTransform.position - reference.position;
        directionToCamera = Vector3.ProjectOnPlane(directionToCamera, Vector3.up);

        if (directionToCamera.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        float rawYaw = Vector3.SignedAngle(reference.forward, directionToCamera.normalized, Vector3.up);
        return Mathf.DeltaAngle(0f, rawYaw - logicalFacingOffset);
    }

    private Sprite GetCurrentFrame(DirectionalSpriteAngleSet angle)
    {
        int frameCount = angle.FrameCount;
        if (frameCount <= 0)
        {
            return null;
        }

        if (frameCount == 1)
        {
            return angle.GetFrame(0);
        }

        float framesPerSecond = currentClip != null && currentClip.framesPerSecond > 0f
            ? currentClip.framesPerSecond
            : 1f;

        int frameIndex = Mathf.FloorToInt(clipTime * framesPerSecond);
        if (currentClip != null && currentClip.loop)
        {
            frameIndex %= frameCount;
        }

        return angle.GetFrame(frameIndex);
    }

    private static AngleSelection FindBestAngle(DirectionalSpriteClip clip, float relativeYaw)
    {
        AngleSelection best = default;
        best.delta = float.MaxValue;

        if (clip == null || clip.angles == null)
        {
            return best;
        }

        for (int i = 0; i < clip.angles.Count; i++)
        {
            DirectionalSpriteAngleSet angle = clip.angles[i];
            if (angle == null || angle.FrameCount <= 0)
            {
                continue;
            }

            ConsiderCandidate(angle, angle.yawDegrees, angle.flipX, relativeYaw, ref best);

            if (angle.symmetry == DirectionalSpriteSymmetry.MirrorToOppositeSide
                && Mathf.Abs(angle.yawDegrees) > 0.001f
                && Mathf.Abs(Mathf.Abs(angle.yawDegrees) - 180f) > 0.001f)
            {
                ConsiderCandidate(angle, -angle.yawDegrees, !angle.flipX, relativeYaw, ref best);
            }
        }

        return best;
    }

    private static void ConsiderCandidate(
        DirectionalSpriteAngleSet angle,
        float candidateYaw,
        bool flipX,
        float relativeYaw,
        ref AngleSelection best)
    {
        float delta = Mathf.Abs(Mathf.DeltaAngle(relativeYaw, candidateYaw));
        if (delta >= best.delta)
        {
            return;
        }

        best.angle = angle;
        best.flipX = flipX;
        best.delta = delta;
    }

    private struct AngleSelection
    {
        public DirectionalSpriteAngleSet angle;
        public bool flipX;
        public float delta;
    }
}
