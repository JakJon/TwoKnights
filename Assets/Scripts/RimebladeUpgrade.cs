using UnityEngine;

// Frigid discipline: the sword door. Two separate things (owner, 2026-10-05):
// every swing throws off a burst of cold that chills, or freezes what was
// already chilled, and deals no damage; and the blade itself adds frost damage
// to whatever it touches. A swing that hits nothing still bursts.
//
// Every Order hangs a discipline off the sword and each one does something
// different with it - Serpent exhales a cloud, Shadow echoes the swing, Ember
// throws ordnance. Frigid's blade is cold iron, and it answers the thing that has
// already closed the distance.
//
// The burst opens one unit out from the knight's middle along the swing and does
// not travel, because the whole point of the sword in this game is that something
// is already too close. It freezes a chilled body with Rimeblade alone (owner,
// 2026-10-09) - no Frost Tip needed - so a swing into a body the ward, an arrow or
// the last swing already chilled stops it dead at arm's length.
[CreateAssetMenu(fileName = "RimebladeUpgrade", menuName = "Upgrades/Rimeblade")]
public class RimebladeUpgrade : BaseUpgrade
{
    [Tooltip("Every rank bursts on every swing (chill or freeze only), and adds frost damage to the blade's own hit. 1 = 1.6u, +5 frost. 2 = 2.3u, +10 frost. 3 = 3.0u, +15 frost.")]
    [SerializeField] private int bladeLevel = 1;

    public override string ChainName => "Rimeblade";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Rimeblade";
        if (weight == 0f)
            weight = 55f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        FrigidBoost boost = targetKnight.GetComponent<FrigidBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<FrigidBoost>();
        }

        boost.SetRimeblade(bladeLevel);

        Debug.Log($"Applied Rimeblade {bladeLevel} to {targetKnight.name}: burst every swing, {boost.RimebladeRadius}u, +{boost.RimebladeFrostDamage} frost on a sword hit");
    }
}
