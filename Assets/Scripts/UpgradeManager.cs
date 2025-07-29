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
        
        // Weighted random selection based on upgrade weights
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
        // Calculate total weight for all available upgrades
        float totalWeight = upgrades.Sum(u => u.Weight);
        
        // Random selection based on weights (similar to WaveManager)
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var upgrade in upgrades)
        {
            currentWeight += upgrade.Weight;
            if (randomValue <= currentWeight)
            {
                return upgrade;
            }
        }
        
        // Fallback to last upgrade if something went wrong with weight calculation
        return upgrades.LastOrDefault();
    }
    
    public void ApplyUpgrade(BaseUpgrade upgrade, KnightTarget targetKnight)
    {
        GameObject knight = targetKnight == KnightTarget.LeftKnight 
            ? GameObject.FindWithTag("PlayerLeft") 
            : GameObject.FindWithTag("PlayerRight");
            
        if (knight != null && upgrade != null)
        {
            upgrade.ApplyUpgrade(knight);
        }
    }
}