using UnityEngine;

[CreateAssetMenu(menuName = "Ultraloud/Resources/Resource Definition", fileName = "ResourceDefinition")]
public sealed class RetroResourceDefinition : ScriptableObject
{
    [SerializeField] private string resourceId = "resource";
    [SerializeField] private string displayName = "Resource";
    [SerializeField] private Sprite icon;
    [SerializeField] private Sprite worldSprite;
    [SerializeField, Min(1)] private int maxAmount = 999;
    [SerializeField] private Color hudTint = Color.white;

    public string ResourceId => string.IsNullOrWhiteSpace(resourceId) ? name : resourceId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ResourceId : displayName;
    public Sprite Icon => icon;
    public Sprite WorldSprite => worldSprite != null ? worldSprite : icon;
    public int MaxAmount => Mathf.Max(1, maxAmount);
    public Color HudTint => hudTint;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            resourceId = name;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = resourceId;
        }

        maxAmount = Mathf.Max(1, maxAmount);
    }
}
