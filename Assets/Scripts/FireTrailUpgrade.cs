using UnityEngine;

// Ember discipline: ignited enemies drip fire behind them as they move, laying
// zones along their own approach path. The enemy becomes the brush.
//
// This inverts the incentive of every other Order — Shadow wants enemies dead
// immediately, Serpent doesn't care when. Ember wants them to live a while
// burning, because a wolf that runs four seconds on fire paints an entire lane.
[CreateAssetMenu(fileName = "FireTrailUpgrade", menuName = "Upgrades/Fire Trail")]
public class FireTrailUpgrade : BaseUpgrade
{
    [SerializeField] private int trailLevel = 1; // 1 = 0.5u/4s, 2 = 0.75u/7s (before Panic and the capstone widen them)

    public override string ChainName => "Fire Trail";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Fire Trail";
        if (weight == 0f)
            weight = 55f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        EmberBoost boost = targetKnight.GetComponent<EmberBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<EmberBoost>();
        }

        // Rank I is worth NO zone dps — the only pick in the Order that isn't. It
        // buys the lane, which is the whole of what Fire Trail is for. Rank II adds
        // +0.25 dps and +0.25u on top.
        boost.SetFireTrailLevel(trailLevel);

        Debug.Log($"Applied Fire Trail {trailLevel} to {targetKnight.name}: " +
            $"zones now {boost.ZoneDps} dps, trails {boost.TrailZoneRadius}u");
    }
}
