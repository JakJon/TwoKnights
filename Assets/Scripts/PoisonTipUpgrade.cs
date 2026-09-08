using UnityEngine;

// Serpent discipline: the arrow door, and the Order's one PLACEMENT upgrade.
//
// The chance to poison on hit is the half everybody notices. The half that
// actually decides how the Order is played is the trail — a poisoned arrow sheds
// a bead of venom every three units it travels, and anything that touches one is
// poisoned by it. That turns a shot's whole flight into the contribution instead
// of only its last inch, and it is why the ranks below raise the bead's LIFETIME
// rather than raising the chance twice: 5 seconds, then 10, then 15. A longer
// bead is a lane you can lay further ahead of the thing it is for, which is a
// different skill from aiming, and the one this Order wants to reward.
[CreateAssetMenu(fileName = "PoisonTipUpgrade", menuName = "Upgrades/Poison Tip")]
public class PoisonTipUpgrade : BaseUpgrade
{
    [SerializeField] private float poisonChanceIncrease = 30f; // Added to the knight's poison chance

    [Tooltip("How long a bead shed by this knight's poisoned arrows keeps working. Set OUTRIGHT by each rank, not added: 5 / 10 / 15 across the three. The boost keeps the larger of what it has and what it is given, so the draft re-offering a rank can never take the trail back down.")]
    [SerializeField] private float trailBubbleSeconds = 5f;

    // Chain steps are named "Venom Tip I/II/III" on the assets
    public override string ChainName => "Venom Tip";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Poison Tip";
        if (string.IsNullOrEmpty(description))
            description = $"Arrows have {poisonChanceIncrease}% chance to poison enemies, and a poisoned arrow trails venom that lingers {trailBubbleSeconds:0}s";
        if (weight == 0f)
            weight = 50f; // Rare rarity weight
    }
    
    public override void ApplyUpgrade(GameObject targetKnight)
    {
        // Add or get the PoisonTipBoost component
        PoisonTipBoost poisonTipBoost = targetKnight.GetComponent<PoisonTipBoost>();
        if (poisonTipBoost == null)
        {
            poisonTipBoost = targetKnight.AddComponent<PoisonTipBoost>();
        }
        poisonTipBoost.IncreasePoisonChance(poisonChanceIncrease);
        poisonTipBoost.SetTrailBubbleSeconds(trailBubbleSeconds);

        Debug.Log($"Applied Poison Tip upgrade to {targetKnight.name}. New poison chance: " +
                  $"{poisonTipBoost.GetPoisonChance()}%, trail beads last {poisonTipBoost.TrailBubbleSeconds}s");
    }
}
