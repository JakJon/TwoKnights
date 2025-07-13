using UnityEngine;

[CreateAssetMenu(fileName = "FiringSpeedUpgrade", menuName = "Upgrades/Firing Speed")]
public class FiringSpeedUpgrade : BaseUpgrade
{
    [SerializeField] private float speedMultiplier = 1.5f;
    
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Firing Speed";
        if (string.IsNullOrEmpty(description))
            description = $"+{(speedMultiplier - 1f) * 100:F0}% Projectile Speed";
    }
    
    public override void ApplyUpgrade(GameObject targetKnight)
    {
        var playerShooter = targetKnight.GetComponent("PlayerShooter");
        if (playerShooter != null)
        {
            var modifyMethod = playerShooter.GetType().GetMethod("ModifyProjectileSpeed");
            if (modifyMethod != null)
            {
                modifyMethod.Invoke(playerShooter, new object[] { speedMultiplier });
            }
        }
    }
}