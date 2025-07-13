using UnityEngine;

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
    [SerializeField] protected UpgradeRarity rarity = UpgradeRarity.Common;
    [SerializeField] protected bool isUnlocked = true;
    
    public string UpgradeName => upgradeName;
    public string Description => description;
    public UpgradeRarity Rarity => rarity;
    public bool IsUnlocked => isUnlocked;
    
    // Check if this upgrade can be applied (beyond just being unlocked)
    public virtual bool CanApply(GameObject targetKnight)
    {
        return isUnlocked;
    }
    
    // Apply the upgrade to the specified knight
    public abstract void ApplyUpgrade(GameObject targetKnight);
    
    // Get a formatted display string for the upgrade
    public virtual string GetDisplayText()
    {
        return $"{upgradeName}\n{description}";
    }
}