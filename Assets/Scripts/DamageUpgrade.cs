using UnityEngine;

[CreateAssetMenu(fileName = "DamageUpgrade", menuName = "Upgrades/Damage")]
public class DamageUpgrade : BaseUpgrade
{
    [SerializeField] private int damageIncrease = 5;
    
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Damage Boost";
        if (string.IsNullOrEmpty(description))
            description = $"+{damageIncrease} Damage";
    }
    
    public override void ApplyUpgrade(GameObject targetKnight)
    {
        // Since damage is handled in projectile collision detection, we'll store damage multiplier
        // on the player and check for it in projectile collision
        DamageBoost damageBoost = targetKnight.GetComponent<DamageBoost>();
        if (damageBoost == null)
        {
            damageBoost = targetKnight.AddComponent<DamageBoost>();
        }
        damageBoost.IncreaseDamage(damageIncrease);
    }
}

// Component to track damage bonuses
public class DamageBoost : MonoBehaviour
{
    private int totalDamageBonus = 0;
    
    public void IncreaseDamage(int amount)
    {
        totalDamageBonus += amount;
    }
    
    public int GetDamageBonus()
    {
        return totalDamageBonus;
    }
}