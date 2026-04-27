using UnityEngine;

[DisallowMultipleComponent]
public sealed class RetroInventoryHud : MonoBehaviour
{
    [SerializeField] private RetroInventory inventory;
    [SerializeField] private bool hideWhenEmpty = true;
    [SerializeField, Range(1, 12)] private int maxVisibleItems = 8;
    [SerializeField] private Vector2 screenOffset = new(18f, 18f);
    [SerializeField] private Vector2 iconSize = new(34f, 34f);
    [SerializeField] private float itemSpacing = 8f;
    [SerializeField] private Color panelColor = new(0f, 0f, 0f, 0.48f);
    [SerializeField] private Color textColor = new(1f, 1f, 1f, 0.95f);

    private GUIStyle amountStyle;

    private void Reset()
    {
        AutoWire();
    }

    private void Awake()
    {
        AutoWire();
    }

    private void OnValidate()
    {
        maxVisibleItems = Mathf.Clamp(maxVisibleItems, 1, 12);
        iconSize.x = Mathf.Max(18f, iconSize.x);
        iconSize.y = Mathf.Max(18f, iconSize.y);
        itemSpacing = Mathf.Max(0f, itemSpacing);
    }

    private void AutoWire()
    {
        if (inventory == null)
        {
            inventory = GetComponent<RetroInventory>();
        }
    }

    private void OnGUI()
    {
        if (inventory == null)
        {
            return;
        }

        EnsureStyles();
        int visibleCount = CountVisibleItems();
        if (visibleCount <= 0 && hideWhenEmpty)
        {
            return;
        }

        float slotWidth = iconSize.x + 46f;
        float width = Mathf.Min(Screen.width - screenOffset.x * 2f, visibleCount * slotWidth + Mathf.Max(0, visibleCount - 1) * itemSpacing + 16f);
        float height = iconSize.y + 16f;
        Rect panelRect = new(screenOffset.x, Screen.height - screenOffset.y - height, width, height);

        Color oldColor = GUI.color;
        GUI.color = panelColor;
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);

        float x = panelRect.x + 8f;
        float y = panelRect.y + 8f;
        int drawn = 0;
        foreach (RetroInventoryStack stack in inventory.Items)
        {
            if (stack.Resource == null || stack.Amount <= 0)
            {
                continue;
            }

            if (drawn >= maxVisibleItems)
            {
                break;
            }

            DrawStack(stack, x, y);
            x += slotWidth + itemSpacing;
            drawn++;
        }

        GUI.color = oldColor;
    }

    private void DrawStack(RetroInventoryStack stack, float x, float y)
    {
        Sprite icon = stack.Resource.Icon;
        Texture iconTexture = icon != null ? icon.texture : Texture2D.whiteTexture;
        Rect iconRect = new(x, y, iconSize.x, iconSize.y);
        Rect amountRect = new(iconRect.xMax + 6f, y, 40f, iconSize.y);

        Color oldColor = GUI.color;
        GUI.color = stack.Resource.HudTint;
        GUI.DrawTexture(iconRect, iconTexture, ScaleMode.ScaleToFit, true);
        GUI.color = textColor;
        GUI.Label(amountRect, stack.Amount.ToString(), amountStyle);
        GUI.color = oldColor;
    }

    private int CountVisibleItems()
    {
        int count = 0;
        foreach (RetroInventoryStack stack in inventory.Items)
        {
            if (stack.Resource != null && stack.Amount > 0)
            {
                count++;
            }
        }

        return Mathf.Min(count, maxVisibleItems);
    }

    private void EnsureStyles()
    {
        if (amountStyle != null)
        {
            return;
        }

        amountStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Clip
        };
        amountStyle.normal.textColor = textColor;
    }
}
