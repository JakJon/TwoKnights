using UnityEngine;

// Frigid discipline: the ice holds longer and is harder to break.
//
// Frost Tip is what freezes: a second blow on a chilled body turns the chill it
// had left into ice. Deep Freeze used to be the pick that unlocked freezing at
// all; since 2026-09-11 (owner's call) each rank adds 3 seconds to the freeze and
// 10 to the damage the ice takes before it breaks (5 -> 15 -> 25 -> 35).
//
// A frozen cart is the showcase: a held cart anchors MineCart's spacing sweep, so
// the run behind it stacks up against it, and one arrow turns the mine's signature
// hazard into a wall of the player's own making. Deep Freeze is what makes that
// wall last.
//
// Since 2026-10-05 (owner's call) the chain also carries what Frost Tip II and
// III used to: each rank adds to the chill time (+3s, +6s, +10s) and ranks II and
// III deepen the slow to 50% and 70%. Rank I's card says -30%, which is the depth
// Frost Tip already gives. See FrigidBoost.ColdRank and ChillSeconds.
//
// Rank three began life as "Permafrost", a separate capstone whose ice never
// melted at all; it was folded into this chain on 2026-09-08 (owner's call),
// because a parallel capstone was silently voiding rank two's only selling point.
//
// Nothing governs the cycle at the far end any more: the thaw reprieve was removed
// on 2026-09-08, so a body that comes out of the ice can be chilled
// on the next touch and stopped on the one after. Pillar 2 is the only brake, and
// it is enough - freezing takes a BLOW, so a knight holding one body forever is
// spending an arrow a time the rest of the wave does not get.
//
// The asset keeps the filename Permafrost.asset even though the card now reads
// "Deep Freeze III", because the stat slug is minted from the FILENAME
// (UpgradeManager.StatSlug) and saves already hold upgrades.taken.permafrost.
// Same trick Greatshield plays to read "Holy Shield". The Frigid quest line used
// to reveal this card; since 2026-10-05 it reveals Frost Bite instead, so rank
// III is an ordinary draft pick once rank II is owned.
[CreateAssetMenu(fileName = "DeepFreezeUpgrade", menuName = "Upgrades/Deep Freeze")]
public class DeepFreezeUpgrade : BaseUpgrade
{
    [Tooltip("Each rank adds 3s to a freeze and 10 to the damage it takes to break (1 = +3s/15, 2 = +6s/25, 3 = +9s/35). It also sets the chill: slow 30% / 50% / 70% and +3s / +6s / +10s of chill time. Bosses are held half as long - see FrigidBoost.BossFreezeMultiplier.")]
    [SerializeField] private int freezeLevel = 1;

    public override string ChainName => "Deep Freeze";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Deep Freeze";
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

        boost.SetDeepFreeze(freezeLevel);

        Debug.Log($"Applied Deep Freeze {freezeLevel} to {targetKnight.name}: freezes hold +{boost.FreezeBonusSeconds}s and take {boost.IceBreakDamage} damage to break; chill x{boost.ChillSpeedMultiplier} for {boost.ChillSeconds}s");
    }
}
