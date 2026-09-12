using UnityEngine;

// Ember capstone: the fire does not go out, it burns bigger, and it burns hotter.
// Three things (owner's call, 2026-09-08):
//
//   1. Your fire zones stop expiring, so every zone you place burns for the rest of
//      the wave. Ember stops being a hazard you re-apply and becomes a map you draw.
//   2. +0.25u to every Fire Trail lane, on top of whatever Fire Trail and Searing
//      Panic already bought. At the top of the Order lanes reach 1.5u.
//   3. +0.5 zone dps — joint-biggest single damage step in Ember, level with Searing
//      Panic II, and still only an eighth of a fully-built field's rate. The capstone
//      is bought for PERMANENCE; the number is a garnish.
//
// IT DOES NOT IGNITE. That was tried and taken back out the same day: burning ground
// lights nothing, whoever laid it and however long it burns. The ignition doors stay
// the arrow, the fireball and the Fire Sight beam — things the player aims. See the
// pillar at the top of EmberBoost.
//
// The blue embers mixed into these zones are the tell for (1) — FireField draws them
// only in ETERNAL zones, so a lane that is about to go out and one that never will
// are different-looking fires.
//
// FireField enforces the two things eternal zones demand: a hard cap with oldest-first
// retirement (immortal zones would otherwise accumulate without bound), and a wave-end
// clear driven from Spawner (so wave N+1 doesn't begin inside wave N's inferno).
// Availability is gated by requiresOrderCount on the asset.
[CreateAssetMenu(fileName = "ScorchedEarthUpgrade", menuName = "Upgrades/Scorched Earth")]
public class ScorchedEarthUpgrade : BaseUpgrade
{
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Scorched Earth";
        if (weight == 0f)
            weight = 10f; // Legendary
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        EmberBoost boost = targetKnight.GetComponent<EmberBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<EmberBoost>();
        }

        boost.EnableScorchedEarth();

        Debug.Log($"Applied Scorched Earth to {targetKnight.name}: zones are eternal, " +
            $"burn at {boost.ZoneDps} dps, trails {boost.TrailZoneRadius}u");
    }
}
