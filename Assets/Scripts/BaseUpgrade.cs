using UnityEngine;
using System.Collections.Generic;

public enum UpgradeRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

public enum KnightTarget
{
    LeftKnight,
    RightKnight
}

[System.Serializable]
public struct UpgradeStat
{
    public string valueText;   // "+30%", "+10", "15%"
    public string labelText;   // "FIRE RATE", "MAX HEALTH", "" allowed
    public bool isPositive;    // authored judgment call, not sign-derived
}

public abstract class BaseUpgrade : ScriptableObject
{
    [SerializeField] protected string upgradeName;
    [SerializeField] protected string description;
    [SerializeField] protected float weight = 1f; // Higher weight = more likely to be selected
    [SerializeField] protected UpgradeOrder order = UpgradeOrder.Neutral;
    [SerializeField] protected int requiresOrderCount = 0; // Owned upgrades of this Order needed before this appears (capstone gating)
    [SerializeField] protected List<BaseUpgrade> unlockedBy = new List<BaseUpgrade>();
    [SerializeField] protected List<BaseUpgrade> lockedBy = new List<BaseUpgrade>();
    [SerializeField] protected List<UpgradeStat> stats = new List<UpgradeStat>();

    public string UpgradeName => upgradeName;
    public string Description => description;
    public float Weight => weight;
    public UpgradeOrder Order => order;
    public int RequiresOrderCount => requiresOrderCount;
    public IReadOnlyList<BaseUpgrade> UnlockedBy => unlockedBy;
    public IReadOnlyList<BaseUpgrade> LockedBy => lockedBy;
    public IReadOnlyList<UpgradeStat> Stats => stats;

    // Display name of the chain/family this upgrade belongs to (e.g. "Shadow Arrow").
    // Defaults to the class name split on capitals with the "Upgrade" suffix dropped.
    public virtual string ChainName
    {
        get
        {
            string n = GetType().Name;
            const string suffix = "Upgrade";
            if (n.EndsWith(suffix)) n = n.Substring(0, n.Length - suffix.Length);
            return System.Text.RegularExpressions.Regex.Replace(n, "(\\B[A-Z])", " $1");
        }
    }
    
    // Calculate rarity based on weight ranges
    public UpgradeRarity Rarity
    {
        get
        {
            return weight switch
            {
                >= 100f => UpgradeRarity.Common,
                >= 50f => UpgradeRarity.Rare,
                >= 20f => UpgradeRarity.Epic,
                _ => UpgradeRarity.Legendary
            };
        }
    }
    
    // Check if this upgrade can be applied (beyond just being unlocked)
    public virtual bool CanApply(GameObject targetKnight)
    {
        return true;
    }
    
    // Apply the upgrade to the specified knight
    public abstract void ApplyUpgrade(GameObject targetKnight);
    
    // Get a formatted display string for the upgrade
    public virtual string GetDisplayText()
    {
        return $"{upgradeName}\n{description}";
    }
}