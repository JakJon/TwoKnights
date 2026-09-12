using UnityEngine;

// Frigid discipline: the sword door. A swing throws off a burst of cold around
// the knight, dealing the sword's own damage to everything it catches.
//
// Every Order hangs a discipline off the sword and each one does something
// different with it - Serpent exhales a cloud, Shadow echoes the swing, Ember
// throws ordnance. Frigid's blade is cold iron, and it answers the thing that has
// already closed the distance.
//
// The burst is centred on the KNIGHT rather than thrown along the facing, because
// the whole point of the sword in this game is that something is already too
// close. With Frost Tip owned, a swing into a body an arrow already chilled
// stops it dead at arm's length.
[CreateAssetMenu(fileName = "RimebladeUpgrade", menuName = "Upgrades/Rimeblade")]
public class RimebladeUpgrade : BaseUpgrade
{
    [Tooltip("1 = 33% of swings at 1.6u. 2 = 60% at 2.3u. 3 = every swing.")]
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

        Debug.Log($"Applied Rimeblade {bladeLevel} to {targetKnight.name}: {boost.RimebladeChance}% of swings, {boost.RimebladeRadius}u");
    }
}
