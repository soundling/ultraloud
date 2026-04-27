using System.Collections.Generic;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DirectionalSpriteHitMask : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DirectionalSpriteAnimator animator;
    [SerializeField] private Renderer visualRenderer;
    [SerializeField] private Transform hitPlane;

    [Header("Mask")]
    [SerializeField] private bool useAlphaMask = true;
    [SerializeField] private bool useSpritePhysicsShapeFallback = true;
    [SerializeField] private bool acceptWhenMaskUnavailable = true;
    [SerializeField] private bool rejectHitsOutsideQuad = true;
    [SerializeField, Range(0f, 1f)] private float alphaThreshold = 0.08f;
    [SerializeField, Range(0, 8)] private int edgePaddingPixels = 2;

    [Header("Debug")]
    [SerializeField] private bool drawDebugHits;
    [SerializeField] private float debugDrawDuration = 0.35f;

    private static readonly Dictionary<Texture2D, AlphaMask> AlphaMasksByTexture = new();
    private static readonly HashSet<Texture2D> UnavailableAlphaMaskTextures = new();

    private readonly List<Vector2> physicsShapePoints = new(64);

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
    }

    private void OnValidate()
    {
        alphaThreshold = Mathf.Clamp01(alphaThreshold);
        edgePaddingPixels = Mathf.Clamp(edgePaddingPixels, 0, 8);
        debugDrawDuration = Mathf.Max(0f, debugDrawDuration);
        AutoAssignReferences();
    }

    public bool TryConfirmHit(
        Ray shotRay,
        RaycastHit broadHit,
        float maxDistance,
        out Vector3 visualHitPoint,
        out Vector3 visualHitNormal,
        out float visualHitDistance)
    {
        visualHitPoint = broadHit.point;
        visualHitNormal = broadHit.normal.sqrMagnitude > 0.0001f ? broadHit.normal.normalized : -shotRay.direction;
        visualHitDistance = broadHit.distance;

        AutoAssignReferences();
        animator?.RefreshNow();

        Sprite sprite = animator != null ? animator.CurrentSprite : null;
        Transform planeTransform = ResolveHitPlane();
        if (sprite == null || planeTransform == null)
        {
            return AcceptOrRejectUnavailable(visualHitPoint, visualHitNormal);
        }

        if (!TryProjectRayToQuad(shotRay, planeTransform, maxDistance, out Vector2 spriteUv, out visualHitPoint, out visualHitNormal, out visualHitDistance))
        {
            return AcceptOrRejectUnavailable(visualHitPoint, visualHitNormal);
        }

        if (animator != null && animator.CurrentFlipX)
        {
            spriteUv.x = 1f - spriteUv.x;
        }

        bool insideQuad = spriteUv.x >= 0f && spriteUv.x <= 1f && spriteUv.y >= 0f && spriteUv.y <= 1f;
        if (!insideQuad)
        {
            if (rejectHitsOutsideQuad)
            {
                DrawDebug(visualHitPoint, visualHitNormal, false);
                return false;
            }

            spriteUv.x = Mathf.Clamp01(spriteUv.x);
            spriteUv.y = Mathf.Clamp01(spriteUv.y);
        }

        if (useAlphaMask && TryContainsAlpha(sprite, spriteUv, out bool alphaHit))
        {
            DrawDebug(visualHitPoint, visualHitNormal, alphaHit);
            return alphaHit;
        }

        if (useSpritePhysicsShapeFallback && TryContainsPhysicsShape(sprite, spriteUv, out bool shapeHit))
        {
            DrawDebug(visualHitPoint, visualHitNormal, shapeHit);
            return shapeHit;
        }

        return AcceptOrRejectUnavailable(visualHitPoint, visualHitNormal);
    }

    private void AutoAssignReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<DirectionalSpriteAnimator>();
        }

        if (animator == null)
        {
            animator = GetComponentInParent<DirectionalSpriteAnimator>();
        }

        if (visualRenderer == null)
        {
            visualRenderer = GetComponentInChildren<MeshRenderer>(true);
        }

        if (visualRenderer == null)
        {
            visualRenderer = GetComponentInChildren<Renderer>(true);
        }

        if (hitPlane == null && visualRenderer != null)
        {
            hitPlane = visualRenderer.transform;
        }
    }

    private Transform ResolveHitPlane()
    {
        if (hitPlane != null)
        {
            return hitPlane;
        }

        return visualRenderer != null ? visualRenderer.transform : transform;
    }

    private bool TryProjectRayToQuad(
        Ray shotRay,
        Transform planeTransform,
        float maxDistance,
        out Vector2 spriteUv,
        out Vector3 hitPoint,
        out Vector3 hitNormal,
        out float hitDistance)
    {
        spriteUv = new Vector2(0.5f, 0.5f);
        hitPoint = shotRay.origin;
        hitNormal = -shotRay.direction;
        hitDistance = 0f;

        Vector3 planeNormal = planeTransform.forward;
        if (planeNormal.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        Plane plane = new Plane(planeNormal.normalized, planeTransform.position);
        if (!plane.Raycast(shotRay, out hitDistance))
        {
            return false;
        }

        if (hitDistance < 0f || hitDistance > maxDistance)
        {
            return false;
        }

        hitPoint = shotRay.GetPoint(hitDistance);
        Vector3 localPoint = planeTransform.InverseTransformPoint(hitPoint);
        spriteUv = new Vector2(localPoint.x + 0.5f, localPoint.y + 0.5f);
        hitNormal = Vector3.Dot(planeTransform.forward, shotRay.direction) > 0f
            ? -planeTransform.forward
            : planeTransform.forward;

        if (hitNormal.sqrMagnitude < 0.0001f)
        {
            hitNormal = -shotRay.direction;
        }
        else
        {
            hitNormal.Normalize();
        }

        return true;
    }

    private bool TryContainsAlpha(Sprite sprite, Vector2 spriteUv, out bool contains)
    {
        contains = false;
        if (!TryGetAlphaMask(sprite, out AlphaMask alphaMask))
        {
            return false;
        }

        contains = alphaMask.Contains(sprite, spriteUv, alphaThreshold, edgePaddingPixels);
        return true;
    }

    private bool TryContainsPhysicsShape(Sprite sprite, Vector2 spriteUv, out bool contains)
    {
        contains = false;
        if (sprite == null)
        {
            return false;
        }

        int shapeCount = sprite.GetPhysicsShapeCount();
        if (shapeCount <= 0)
        {
            return false;
        }

        Vector2 localPoint = SpriteUvToLocalPoint(sprite, spriteUv);
        if (ContainsPhysicsPoint(sprite, localPoint))
        {
            contains = true;
            return true;
        }

        if (edgePaddingPixels <= 0)
        {
            return true;
        }

        float padding = edgePaddingPixels / Mathf.Max(1f, sprite.pixelsPerUnit);
        contains = ContainsPhysicsPoint(sprite, localPoint + new Vector2(padding, 0f))
            || ContainsPhysicsPoint(sprite, localPoint + new Vector2(-padding, 0f))
            || ContainsPhysicsPoint(sprite, localPoint + new Vector2(0f, padding))
            || ContainsPhysicsPoint(sprite, localPoint + new Vector2(0f, -padding))
            || ContainsPhysicsPoint(sprite, localPoint + new Vector2(padding, padding))
            || ContainsPhysicsPoint(sprite, localPoint + new Vector2(-padding, padding))
            || ContainsPhysicsPoint(sprite, localPoint + new Vector2(padding, -padding))
            || ContainsPhysicsPoint(sprite, localPoint + new Vector2(-padding, -padding));
        return true;
    }

    private bool ContainsPhysicsPoint(Sprite sprite, Vector2 localPoint)
    {
        int shapeCount = sprite.GetPhysicsShapeCount();
        for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
        {
            physicsShapePoints.Clear();
            sprite.GetPhysicsShape(shapeIndex, physicsShapePoints);
            if (physicsShapePoints.Count >= 3 && IsPointInsidePolygon(localPoint, physicsShapePoints))
            {
                return true;
            }
        }

        return false;
    }

    private bool AcceptOrRejectUnavailable(Vector3 point, Vector3 normal)
    {
        DrawDebug(point, normal, acceptWhenMaskUnavailable);
        return acceptWhenMaskUnavailable;
    }

    private void DrawDebug(Vector3 point, Vector3 normal, bool accepted)
    {
        if (!drawDebugHits)
        {
            return;
        }

        Color color = accepted ? Color.green : Color.red;
        Debug.DrawRay(point, normal.normalized * 0.45f, color, debugDrawDuration);
    }

    private static bool TryGetAlphaMask(Sprite sprite, out AlphaMask alphaMask)
    {
        alphaMask = null;
        Texture2D texture = sprite != null ? sprite.texture : null;
        if (texture == null)
        {
            return false;
        }

        if (AlphaMasksByTexture.TryGetValue(texture, out alphaMask))
        {
            return alphaMask != null;
        }

        if (UnavailableAlphaMaskTextures.Contains(texture))
        {
            return false;
        }

        if (texture.isReadable && TryBuildAlphaMask(texture, out alphaMask))
        {
            AlphaMasksByTexture[texture] = alphaMask;
            return true;
        }

#if UNITY_EDITOR
        if (TryBuildEditorSourceAlphaMask(sprite, out alphaMask))
        {
            AlphaMasksByTexture[texture] = alphaMask;
            return true;
        }
#endif

        UnavailableAlphaMaskTextures.Add(texture);
        return false;
    }

    private static bool TryBuildAlphaMask(Texture2D texture, out AlphaMask alphaMask)
    {
        alphaMask = null;
        if (texture == null)
        {
            return false;
        }

        Color32[] pixels;
        try
        {
            pixels = texture.GetPixels32();
        }
        catch
        {
            return false;
        }

        if (pixels == null || pixels.Length == 0)
        {
            return false;
        }

        byte[] alpha = new byte[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            alpha[i] = pixels[i].a;
        }

        alphaMask = new AlphaMask(texture.width, texture.height, alpha);
        return true;
    }

#if UNITY_EDITOR
    private static bool TryBuildEditorSourceAlphaMask(Sprite sprite, out AlphaMask alphaMask)
    {
        alphaMask = null;
        string assetPath = AssetDatabase.GetAssetPath(sprite);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(assetPath);
        if (!File.Exists(fullPath))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(fullPath);
        }
        catch
        {
            return false;
        }

        Texture2D readableTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        try
        {
            if (!readableTexture.LoadImage(bytes, false))
            {
                return false;
            }

            return TryBuildAlphaMask(readableTexture, out alphaMask);
        }
        finally
        {
            DestroyTemporaryTexture(readableTexture);
        }
    }

    private static void DestroyTemporaryTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(texture);
        }
        else
        {
            Object.DestroyImmediate(texture);
        }
    }
#endif

    private static Vector2 SpriteUvToLocalPoint(Sprite sprite, Vector2 spriteUv)
    {
        Rect rect = sprite.rect;
        Vector2 pixel = new Vector2(spriteUv.x * rect.width, spriteUv.y * rect.height);
        Vector2 pivot = sprite.pivot;
        float pixelsPerUnit = Mathf.Max(1f, sprite.pixelsPerUnit);
        return (pixel - pivot) / pixelsPerUnit;
    }

    private static bool IsPointInsidePolygon(Vector2 point, List<Vector2> polygon)
    {
        bool inside = false;
        int previousIndex = polygon.Count - 1;
        for (int currentIndex = 0; currentIndex < polygon.Count; currentIndex++)
        {
            Vector2 current = polygon[currentIndex];
            Vector2 previous = polygon[previousIndex];
            bool crosses = current.y > point.y != previous.y > point.y;
            if (crosses)
            {
                float xAtY = (previous.x - current.x) * (point.y - current.y) / (previous.y - current.y + Mathf.Epsilon) + current.x;
                if (point.x < xAtY)
                {
                    inside = !inside;
                }
            }

            previousIndex = currentIndex;
        }

        return inside;
    }

    private sealed class AlphaMask
    {
        private readonly int width;
        private readonly int height;
        private readonly byte[] alpha;

        public AlphaMask(int width, int height, byte[] alpha)
        {
            this.width = width;
            this.height = height;
            this.alpha = alpha;
        }

        public bool Contains(Sprite sprite, Vector2 spriteUv, float threshold, int paddingPixels)
        {
            if (sprite == null || alpha == null || alpha.Length == 0 || width <= 0 || height <= 0)
            {
                return false;
            }

            Rect textureRect = ResolveTextureRect(sprite, width, height);
            float pixelX = textureRect.x + Mathf.Clamp01(spriteUv.x) * Mathf.Max(1f, textureRect.width - 1f);
            float pixelY = textureRect.y + Mathf.Clamp01(spriteUv.y) * Mathf.Max(1f, textureRect.height - 1f);
            int centerX = Mathf.Clamp(Mathf.RoundToInt(pixelX), 0, width - 1);
            int centerY = Mathf.Clamp(Mathf.RoundToInt(pixelY), 0, height - 1);
            int radius = Mathf.Max(0, paddingPixels);
            byte thresholdByte = (byte)Mathf.Clamp(Mathf.RoundToInt(threshold * 255f), 0, 255);

            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                if (y < 0 || y >= height)
                {
                    continue;
                }

                for (int x = centerX - radius; x <= centerX + radius; x++)
                {
                    if (x < 0 || x >= width)
                    {
                        continue;
                    }

                    if (alpha[y * width + x] >= thresholdByte)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Rect ResolveTextureRect(Sprite sprite, int fallbackWidth, int fallbackHeight)
        {
            try
            {
                return sprite.textureRect;
            }
            catch
            {
                return new Rect(0f, 0f, fallbackWidth, fallbackHeight);
            }
        }
    }
}
