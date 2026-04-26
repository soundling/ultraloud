using UnityEngine;

[DisallowMultipleComponent]
public sealed class RetroBillboardSprite : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool yawOnly = true;

    private void LateUpdate()
    {
        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
        {
            return;
        }

        Vector3 toCamera = cameraToUse.transform.position - transform.position;
        if (yawOnly)
        {
            toCamera.y = 0f;
        }

        if (toCamera.sqrMagnitude < 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
    }
}
