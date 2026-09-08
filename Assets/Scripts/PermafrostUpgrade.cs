using UnityEngine;

// Frigid capstone (requires 4 Frigid picks): frozen things do not thaw.
//
// A freeze lasts until something breaks it. The board fills with statues and the
// knights take them apart in whatever order they like - the literal end state of
// "you take the enemy's time", the way Scorched Earth is the end state of "you
// burn the arena". It also makes Shatter the whole endgame, since breaking the ice
// becomes the only way a statue ever moves again.
//
// It buys safety, not speed. Frigid deals no damage of its own, so the arrows
// still have to do all the killing and the wave takes exactly as long; a
// Permafrost knight is not faster, just untouched.
//
// Two things it demands, both already paid: the hold is expressed as a very long
// deadline rather than a second code path (EnemyBase.PermafrostSeconds), and the
// ice dies with the WAVE rather than the run (FrigidBoost.ClearFieldFrost, called
// from Spawner.BeginWave beside FireField.ClearAll). Without that second half,
// wave twelve would begin inside wave eleven's statues.
[CreateAssetMenu(fileName = "PermafrostUpgrade", menuName = "Upgrades/Permafrost")]
public class PermafrostUpgrade : BaseUpgrade
{
    public override string ChainName => "Permafrost";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Permafrost";
        if (weight == 0f)
            weight = 10f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        FrigidBoost boost = targetKnight.GetComponent<FrigidBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<FrigidBoost>();
        }

        boost.SetPermafrost();

        Debug.Log($"Applied Permafrost to {targetKnight.name}: freezes no longer expire");
    }
}
