using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DirectionalSpriteLocomotion : MonoBehaviour
{
    [SerializeField] private DirectionalSpriteAnimator animator;
    [SerializeField] private Rigidbody movementBody;
    [SerializeField] private Transform movementReference;
    [SerializeField] private string idleClipId = "Idle";
    [SerializeField] private string walkClipId = "Walk";
    [SerializeField] private bool horizontalOnly = true;
    [SerializeField, Min(0f)] private float walkThreshold = 0.05f;
    [SerializeField, Min(0f)] private float speedSmoothing = 12f;

    private Vector3 lastPosition;
    private float smoothedSpeed;

    private void Reset()
    {
        AutoAssignReferences();
        lastPosition = GetReferencePosition();
    }

    private void Awake()
    {
        AutoAssignReferences();
        lastPosition = GetReferencePosition();
    }

    private void OnEnable()
    {
        lastPosition = GetReferencePosition();
    }

    private void OnValidate()
    {
        walkThreshold = Mathf.Max(0f, walkThreshold);
        speedSmoothing = Mathf.Max(0f, speedSmoothing);
        AutoAssignReferences();
    }

    private void Update()
    {
        if (animator == null)
        {
            return;
        }

        float targetSpeed = ResolveCurrentSpeed(Time.deltaTime);
        if (speedSmoothing > 0f)
        {
            smoothedSpeed = Mathf.MoveTowards(smoothedSpeed, targetSpeed, speedSmoothing * Time.deltaTime);
        }
        else
        {
            smoothedSpeed = targetSpeed;
        }

        string nextClipId = smoothedSpeed >= walkThreshold ? walkClipId : idleClipId;
        if (string.IsNullOrWhiteSpace(nextClipId))
        {
            return;
        }

        if (string.Equals(animator.CurrentClipId, nextClipId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        animator.Play(nextClipId, false);
    }

    private void AutoAssignReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<DirectionalSpriteAnimator>();
        }

        if (movementBody == null)
        {
            movementBody = GetComponent<Rigidbody>();
        }

        if (movementReference == null)
        {
            movementReference = transform;
        }
    }

    private float ResolveCurrentSpeed(float deltaTime)
    {
        if (movementBody != null)
        {
            Vector3 velocity = movementBody.linearVelocity;
            if (horizontalOnly)
            {
                velocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            }

            return velocity.magnitude;
        }

        Vector3 currentPosition = GetReferencePosition();
        Vector3 displacement = currentPosition - lastPosition;
        lastPosition = currentPosition;

        if (horizontalOnly)
        {
            displacement = Vector3.ProjectOnPlane(displacement, Vector3.up);
        }

        if (deltaTime <= 0f)
        {
            return 0f;
        }

        return displacement.magnitude / deltaTime;
    }

    private Vector3 GetReferencePosition()
    {
        return movementReference != null ? movementReference.position : transform.position;
    }
}
