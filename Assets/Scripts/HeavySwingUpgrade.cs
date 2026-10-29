using UnityEngine;

// Heavy Swing (Guardian): an enemy the sword kills is launched backward, away from
// the knight, spinning, and hits the first enemy it touches. Rank I hits for 15.
// Rank II, Guided Heavy Swing, bends the body onto a nearby enemy and hits for 25
// (owner, 2026-10-15). Either rank also makes the sword itself hit for at least 20
// (owner, 2026-10-24; see GuardianBoost.SwordDamageFloor).
//
// A sword upgrade that buys the LANDING rather than a bigger blow, which is the
// Order's pillar: the kill already happened, and Heavy Swing makes it reach the next
// body as well. The flight itself is LaunchedBody's; this only writes the rank.
[CreateAssetMenu(fileName = "HeavySwingUpgrade", menuName = "Upgrades/Heavy Swing")]
public class HeavySwingUpgrade : BaseUpgrade
{
    [Tooltip("Rank this tier grants: 1 = Heavy Swing, 2 = Guided Heavy Swing. Damage and homing range come off GuardianBoost, so the tuning is one file rather than one per asset.")]
    [SerializeField] private int rank = 1;

    public override string ChainName => "Heavy Swing";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Heavy Swing";
        if (weight == 0f)
            weight = 100f; // Common
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        GuardianBoost boost = targetKnight.GetComponent<GuardianBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<GuardianBoost>();
        }

        boost.SetHeavySwing(rank);

        Debug.Log($"Applied Heavy Swing to {targetKnight.name}: rank {boost.HeavySwingLevel}, " +
                  $"sword hits for at least {boost.SwordDamageFloor}, " +
                  $"{boost.HeavySwingDamage} damage, {boost.HeavySwingGuideRadius:F2}u homing");
    }
}
