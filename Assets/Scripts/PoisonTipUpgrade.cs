using UnityEngine;

[CreateAssetMenu(fileName = "PoisonTipUpgrade", menuName = "Upgrades/Poison Tip")]
public class PoisonTipUpgrade : BaseUpgrade
{
    [SerializeField] private float poisonChanceIncrease = 100f; // 100% chance for testing
    
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Poison Tip";
        if (string.IsNullOrEmpty(description))
            description = $"Arrows have {poisonChanceIncrease}% chance to poison enemies";
        if (weight == 0f)
            weight = 50f; // Rare rarity weight
    }
    
    public override void ApplyUpgrade(GameObject targetKnight)
    {
        // Add or get the PoisonTipBoost component
        PoisonTipBoost poisonTipBoost = targetKnight.GetComponent<PoisonTipBoost>();
        if (poisonTipBoost == null)
        {
            poisonTipBoost = targetKnight.AddComponent<PoisonTipBoost>();
        }
        poisonTipBoost.IncreasePoisonChance(poisonChanceIncrease);
        
        Debug.Log($"Applied Poison Tip upgrade to {targetKnight.name}. New poison chance: {poisonTipBoost.GetPoisonChance()}%");
    }
}
