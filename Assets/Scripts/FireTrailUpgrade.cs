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
    [SerializeField] private int trailLevel = 1; // 1 = 0.5u/2s, 2 = 0.75u/3.5s

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

        // Each rank also raises every fire zone's damage (EmberBoost.ZoneDps reads
        // the rank counts) — Bellows was cut, so the dps knob rides here instead
        boost.SetFireTrailLevel(trailLevel);

        Debug.Log($"Applied Fire Trail {trailLevel} to {targetKnight.name}: zones now {boost.ZoneDps} dps");
    }
}
