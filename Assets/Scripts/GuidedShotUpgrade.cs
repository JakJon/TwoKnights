using UnityEngine;

// Guided Shot (Guardian discipline): the knight's own arrow bends onto whatever it
// passes close to — a mob or a health orb, since orbs in this game are collected by
// shooting them and bending onto one is the same favour as bending onto a rat.
//
// This is the Order's pillar in its plainest form: Guardian never asks the knight to
// aim better, it buys the LANDING. A tier raises the radius and nothing else, so what
// the player is buying is how EARLY the shot commits, never whether it connects.
//
// Only the main shot steers. Shadow arrows and shurikens fly straight, the same line
// PlayerProjectile.absorbsFieldEffects already draws — a steered shuriken fan would
// clear a screen without anyone aiming at anything.
[CreateAssetMenu(fileName = "GuidedShotUpgrade", menuName = "Upgrades/Guided Shot")]
public class GuidedShotUpgrade : BaseUpgrade
{
    [Tooltip("Rank this tier grants: 1 or 2 (rank 2 triples the radius). The radius itself lives on GuardianBoost so the chain tunes in one place.")]
    [SerializeField] private int rank = 1;

    public override string ChainName => "Guided Shot";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Guided Shot";
        if (weight == 0f)
            weight = 60f; // Rare
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        GuardianBoost boost = targetKnight.GetComponent<GuardianBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<GuardianBoost>();
        }

        boost.SetGuidedShot(rank);

        Debug.Log($"Applied Guided Shot to {targetKnight.name}: rank {boost.GuidedShotLevel}, " +
                  $"{boost.GuidedShotRadius:F2}u radius");
    }
}
