using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "UpgradeManager", menuName = "Upgrades/Upgrade Manager")]
public class UpgradeManager : ScriptableObject
{
    [SerializeField] private List<BaseUpgrade> allUpgrades = new List<BaseUpgrade>();
    [SerializeField] private int upgradesPerSelection = 3;
    
    // Get a random selection of available upgrades
    public List<BaseUpgrade> GetRandomUpgrades()
    {
        var availableUpgrades = allUpgrades.Where(upgrade => upgrade.IsUnlocked).ToList();
        
        if (availableUpgrades.Count <= upgradesPerSelection)
        {
            return availableUpgrades;
        }
        
        // Weighted random selection based on rarity
        List<BaseUpgrade> selectedUpgrades = new List<BaseUpgrade>();
        List<BaseUpgrade> tempList = new List<BaseUpgrade>(availableUpgrades);
        
        for (int i = 0; i < upgradesPerSelection && tempList.Count > 0; i++)
        {
            BaseUpgrade selected = GetWeightedRandomUpgrade(tempList);
            selectedUpgrades.Add(selected);
            tempList.Remove(selected);
        }
        
        return selectedUpgrades;
    }
    
    private BaseUpgrade GetWeightedRandomUpgrade(List<BaseUpgrade> upgrades)
    {
        // Simple weighted selection - rarer items have lower chance
        var weightedUpgrades = new List<(BaseUpgrade upgrade, float weight)>();
        
        foreach (var upgrade in upgrades)
        {
            float weight = upgrade.Rarity switch
            {
                UpgradeRarity.Common => 1.0f,
                UpgradeRarity.Rare => 0.5f,
                UpgradeRarity.Epic => 0.25f,
                UpgradeRarity.Legendary => 0.1f,
                _ => 1.0f
            };
            weightedUpgrades.Add((upgrade, weight));
        }
        
        float totalWeight = weightedUpgrades.Sum(x => x.weight);
        float randomValue = Random.Range(0f, totalWeight);
        
        float currentWeight = 0f;
        foreach (var (upgrade, weight) in weightedUpgrades)
        {
            currentWeight += weight;
            if (randomValue <= currentWeight)
            {
                return upgrade;
            }
        }
        
        return weightedUpgrades.LastOrDefault().upgrade;
    }
    
    public void ApplyUpgrade(BaseUpgrade upgrade, KnightTarget targetKnight)
    {
        GameObject knight = targetKnight == KnightTarget.LeftKnight 
            ? GameObject.FindWithTag("PlayerLeft") 
            : GameObject.FindWithTag("PlayerRight");
            
        if (knight != null && upgrade != null)
        {
            upgrade.ApplyUpgrade(knight);
            Debug.Log($"{upgrade.UpgradeName} was selected for {targetKnight}");
        }
    }
}