using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A persistent item a knight carries into every run, earned from a quest or
/// bought with crystals. The between-run counterpart to an upgrade: upgrades are
/// drafted inside one run and lost with it, equipment is chosen in camp and kept.
///
/// Same contract as BaseUpgrade on purpose — Apply writes numbers into a boost
/// component and contains no gameplay logic of its own. See EquipmentBoost.
/// </summary>
public abstract class EquipmentDefinition : ScriptableObject
{
    [Tooltip("Stable save key. Never change it once a build has shipped — saves store this string.")]
    [SerializeField] protected string id;
    [SerializeField] protected string displayName;
    // There is no flavor "description" any more (owner, 2026-10-05: not needed).
    // The effect line is the only text an item has.
    [Tooltip("Exactly what it does, with the numbers. This is the line a player buys on, and the only text the item has.")]
    [SerializeField] protected string effect;
    [SerializeField] protected Sprite icon;
    [Tooltip("Crystals to buy in the camp shop. 0 means quest reward only, never sold.")]
    [SerializeField] protected int crystalCost = 0;
    [Tooltip("Badges on the item card, same shape as an upgrade's.")]
    [SerializeField] protected List<UpgradeStat> stats = new List<UpgradeStat>();

    public string Id => id;
    public string DisplayName => displayName;
    /// <summary>The mechanical line, with numbers. Never leave this empty on a shop item.</summary>
    public string Effect => effect;
    public Sprite Icon => icon;
    public int CrystalCost => crystalCost;
    public bool SoldInShop => crystalCost > 0;
    public IReadOnlyList<UpgradeStat> Stats => stats;

    /// <summary>
    /// Called once per run, per knight carrying this item, before the first wave.
    /// </summary>
    public abstract void Apply(GameObject knight);

    protected static EquipmentBoost BoostOn(GameObject knight)
    {
        var boost = knight.GetComponent<EquipmentBoost>();
        return boost != null ? boost : knight.AddComponent<EquipmentBoost>();
    }
}
