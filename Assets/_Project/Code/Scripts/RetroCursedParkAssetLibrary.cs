using System;
using System.Collections.Generic;
using UnityEngine;

public enum RetroCursedParkAssetCategory
{
    MajorAttraction = 0,
    Machine = 1,
    Automaton = 2,
    SignageClutter = 3,
    GroundDecal = 4
}

[CreateAssetMenu(fileName = "CursedParkAssetLibrary", menuName = "Ultraloud/Attraction Parks/Cursed Park Asset Library")]
public sealed class RetroCursedParkAssetLibrary : ScriptableObject
{
    [SerializeField] private List<RetroCursedParkSpriteAsset> assets = new();

    public IReadOnlyList<RetroCursedParkSpriteAsset> Assets => assets;

    public bool TryGetWeighted(RetroCursedParkAssetCategory category, System.Random random, out RetroCursedParkSpriteAsset asset)
    {
        asset = null;
        if (assets == null || assets.Count == 0)
        {
            return false;
        }

        float totalWeight = 0f;
        for (int i = 0; i < assets.Count; i++)
        {
            RetroCursedParkSpriteAsset candidate = assets[i];
            if (candidate == null || candidate.Category != category || candidate.BaseMap == null)
            {
                continue;
            }

            totalWeight += Mathf.Max(0.001f, candidate.Weight);
        }

        if (totalWeight <= 0f)
        {
            return false;
        }

        float roll = (float)(random.NextDouble() * totalWeight);
        for (int i = 0; i < assets.Count; i++)
        {
            RetroCursedParkSpriteAsset candidate = assets[i];
            if (candidate == null || candidate.Category != category || candidate.BaseMap == null)
            {
                continue;
            }

            roll -= Mathf.Max(0.001f, candidate.Weight);
            if (roll <= 0f)
            {
                asset = candidate;
                return true;
            }
        }

        return false;
    }

    public RetroCursedParkSpriteAsset FindById(string id)
    {
        if (assets == null || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        for (int i = 0; i < assets.Count; i++)
        {
            RetroCursedParkSpriteAsset asset = assets[i];
            if (asset != null && string.Equals(asset.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return asset;
            }
        }

        return null;
    }

#if UNITY_EDITOR
    public void EditorReplaceAssets(IEnumerable<RetroCursedParkSpriteAsset> newAssets)
    {
        assets.Clear();
        if (newAssets == null)
        {
            return;
        }

        foreach (RetroCursedParkSpriteAsset asset in newAssets)
        {
            if (asset != null)
            {
                assets.Add(asset);
            }
        }
    }
#endif
}

[Serializable]
public sealed class RetroCursedParkSpriteAsset
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private RetroCursedParkAssetCategory category;
    [SerializeField] private Texture2D baseMap;
    [SerializeField] private Texture2D normalMap;
    [SerializeField] private Texture2D emissionMap;
    [SerializeField] private Vector2 baseSize = new(2f, 2f);
    [SerializeField] private Vector2 scaleRange = new(0.9f, 1.15f);
    [SerializeField, Min(0.001f)] private float weight = 1f;
    [SerializeField] private bool billboard = true;
    [SerializeField] private bool castShadow = true;
    [SerializeField] private bool receiveShadow = true;
    [SerializeField] private bool interactableCandidate;
    [SerializeField] private bool groundDecal;
    [SerializeField, Min(0f)] private float glowStrength = 0.7f;
    [SerializeField, Min(0f)] private float flickerStrength = 0.25f;
    [SerializeField, Min(0f)] private float swayStrength = 0.35f;
    [SerializeField] private Color tint = Color.white;
    [SerializeField] private Color emissionColor = Color.white;
    [SerializeField] private Color rimColor = new(1f, 0.42f, 0.12f, 1f);

    public string Id
    {
        get => id;
        set => id = value;
    }

    public string DisplayName
    {
        get => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        set => displayName = value;
    }

    public RetroCursedParkAssetCategory Category
    {
        get => category;
        set => category = value;
    }

    public Texture2D BaseMap
    {
        get => baseMap;
        set => baseMap = value;
    }

    public Texture2D NormalMap
    {
        get => normalMap;
        set => normalMap = value;
    }

    public Texture2D EmissionMap
    {
        get => emissionMap;
        set => emissionMap = value;
    }

    public Vector2 BaseSize
    {
        get => baseSize;
        set => baseSize = value;
    }

    public Vector2 ScaleRange
    {
        get => scaleRange;
        set => scaleRange = value;
    }

    public float Weight
    {
        get => weight;
        set => weight = Mathf.Max(0.001f, value);
    }

    public bool Billboard
    {
        get => billboard;
        set => billboard = value;
    }

    public bool CastShadow
    {
        get => castShadow;
        set => castShadow = value;
    }

    public bool ReceiveShadow
    {
        get => receiveShadow;
        set => receiveShadow = value;
    }

    public bool InteractableCandidate
    {
        get => interactableCandidate;
        set => interactableCandidate = value;
    }

    public bool GroundDecal
    {
        get => groundDecal;
        set => groundDecal = value;
    }

    public float GlowStrength
    {
        get => glowStrength;
        set => glowStrength = Mathf.Max(0f, value);
    }

    public float FlickerStrength
    {
        get => flickerStrength;
        set => flickerStrength = Mathf.Max(0f, value);
    }

    public float SwayStrength
    {
        get => swayStrength;
        set => swayStrength = Mathf.Max(0f, value);
    }

    public Color Tint
    {
        get => tint;
        set => tint = value;
    }

    public Color EmissionColor
    {
        get => emissionColor;
        set => emissionColor = value;
    }

    public Color RimColor
    {
        get => rimColor;
        set => rimColor = value;
    }
}
