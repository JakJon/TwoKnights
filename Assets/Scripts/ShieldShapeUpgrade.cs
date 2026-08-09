using UnityEngine;

// Guardian discipline: the shield gets bigger in both directions at once. Each tier
// widens the span from tip to tip AND bows the bar back around the knight, so it stops
// more of what comes straight in and starts catching the off-axis shots that used to
// slip past a flat edge. The bow never costs span — ShieldShape grows the arc to
// compensate — so a tier is pure gain.
[CreateAssetMenu(fileName = "ShieldShapeUpgrade", menuName = "Upgrades/Shield Shape")]
public class ShieldShapeUpgrade : BaseUpgrade
{
    // Both figures are absolute rather than per-tier increments, so a tier can't
    // compound if it is ever applied twice.
    [SerializeField] private float lengthMultiplier = 1.5f;  // target span vs the authored shield
    [SerializeField] private float curveRadius = 2.6f;       // shield-local units; smaller is a tighter bow

    // Display only — the assets stay named Greatshield 1-3 on disk, because the
    // filename is what upgrades.taken.greatshield_N is slugged from.
    public override string ChainName => "Dawn Shield";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Dawn Shield";
        if (weight == 0f)
            weight = 16f; // Legendary
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        ShieldOrbit shield = targetKnight.GetComponentInChildren<ShieldOrbit>();
        if (shield == null)
        {
            Debug.LogWarning($"ShieldShapeUpgrade: {targetKnight.name} has no ShieldOrbit to reshape.");
            return;
        }

        ShieldShape shape = shield.GetComponent<ShieldShape>();
        if (shape == null)
        {
            shape = shield.gameObject.AddComponent<ShieldShape>();
        }

        shape.SetShape(lengthMultiplier, curveRadius);

        Debug.Log($"Applied {upgradeName} to {targetKnight.name}: span x{lengthMultiplier}, " +
                  $"curve radius {curveRadius}, arc {shape.ArcDegrees:0}°");
    }
}
