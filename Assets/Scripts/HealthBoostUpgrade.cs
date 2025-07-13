using UnityEngine;

[CreateAssetMenu(fileName = "HealthBoostUpgrade", menuName = "Upgrades/Health Boost")]
public class HealthBoostUpgrade : BaseUpgrade
{
    [SerializeField] private int healthIncrease = 25;
    
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Health Boost";
        if (string.IsNullOrEmpty(description))
            description = $"+{healthIncrease} Max Health";
    }
    
    public override void ApplyUpgrade(GameObject targetKnight)
    {
        var playerHealth = targetKnight.GetComponent("PlayerHealth");
        if (playerHealth != null)
        {
            var increaseMethod = playerHealth.GetType().GetMethod("IncreaseMaxHealth");
            if (increaseMethod != null)
            {
                increaseMethod.Invoke(playerHealth, new object[] { healthIncrease });
            }
        }
    }
}