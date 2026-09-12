using UnityEngine;

// Ember discipline: ignited enemies move faster, fire zones burn hotter, and every
// Fire Trail lane is laid wider.
//
// It reads as a downside — you are making the things running at you run faster —
// and that tension is the point. With Fire Trail it's a large gain three times over:
// a panicking wolf covers more ground while burning, each drop it leaves is wider,
// and every zone on the field hits harder. Gated behind Fire Trail, because without
// trails the speed is purely a drawback.
//
// Rank I is +0.25 zone dps, rank II is +0.5, and each rank is +0.25u of trail radius
// (owner's call, 2026-09-08). Rank I has to pay something the frame it lands — that is
// the frame the enemies get faster — and rank II is where it gets paid.
//
// The chain is bought for the SPEED. Both the damage and the width are trims on top of
// what Fire Trail already laid, which is true of every Ember pick after the lane.
[CreateAssetMenu(fileName = "SearingPanicUpgrade", menuName = "Upgrades/Searing Panic")]
public class SearingPanicUpgrade : BaseUpgrade
{
    [SerializeField] private int panicLevel = 1; // 1 = +35% speed, 2 = +60%

    public override string ChainName => "Searing Panic";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Searing Panic";
        if (weight == 0f)
            weight = 35f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        EmberBoost boost = targetKnight.GetComponent<EmberBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<EmberBoost>();
        }

        boost.SetSearingPanicLevel(panicLevel);

        Debug.Log($"Applied Searing Panic {panicLevel} to {targetKnight.name}: " +
            $"x{boost.PanicSpeedMultiplier} speed, zones now {boost.ZoneDps} dps, " +
            $"trails {boost.TrailZoneRadius}u");
    }
}
