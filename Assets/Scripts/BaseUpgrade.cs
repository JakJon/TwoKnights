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

public abstract class BaseUpgrade : ScriptableObject
{
    [SerializeField] protected string upgradeName;
    [SerializeField] protected string description;
    [SerializeField] protected float weight = 1f; // Higher weight = more likely to be selected
    [SerializeField] protected List<BaseUpgrade> unlockedBy = new List<BaseUpgrade>();
    [SerializeField] protected List<BaseUpgrade> lockedBy = new List<BaseUpgrade>();
    
    public string UpgradeName => upgradeName;
    public string Description => description;
    public float Weight => weight;
    public IReadOnlyList<BaseUpgrade> UnlockedBy => unlockedBy;
    public IReadOnlyList<BaseUpgrade> LockedBy => lockedBy;
    
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