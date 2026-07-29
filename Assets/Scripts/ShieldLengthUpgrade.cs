using UnityEngine;

// Guardian discipline: the shield grows along its face. A taller bar simply intercepts
// more of what comes straight in — the plainest possible answer to "I keep getting hit",
// and the chain Curved Aegis bends once you own it.
[CreateAssetMenu(fileName = "ShieldLengthUpgrade", menuName = "Upgrades/Shield Length")]
public class ShieldLengthUpgrade : BaseUpgrade
{
    // Absolute multiplier over the authored shield, not a per-tier increment, so tiers
    // can't compound if one is ever applied twice.
    [SerializeField] private float lengthMultiplier = 1.25f;

    public override string ChainName => "Tower Shield";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Tower Shield";
        if (weight == 0f)
            weight = 16f; // Legendary
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        ShieldOrbit shield = targetKnight.GetComponentInChildren<ShieldOrbit>();
        if (shield == null)
        {
            Debug.LogWarning($"ShieldLengthUpgrade: {targetKnight.name} has no ShieldOrbit to grow.");
            return;
        }

        ShieldShape shape = shield.GetComponent<ShieldShape>();
        if (shape == null)
        {
            shape = shield.gameObject.AddComponent<ShieldShape>();
        }

        shape.SetLengthMultiplier(lengthMultiplier);

        Debug.Log($"Applied {upgradeName} to {targetKnight.name}: shield length x{lengthMultiplier}");
    }
}
