using UnityEngine;

// Ember capstone: the fire does not go out. Your fire zones stop expiring, so
// every zone you place burns for the rest of the wave — Ember stops being a hazard
// you re-apply and becomes a map you are drawing.
//
// FireField enforces the two things this demands: a hard zone cap with oldest-first
// retirement (immortal zones would otherwise accumulate without bound), and a
// wave-end clear driven from Spawner (so wave N+1 doesn't begin inside wave N's
// inferno). Availability is gated by requiresOrderCount on the asset.
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

        Debug.Log($"Applied Scorched Earth to {targetKnight.name}: fire zones no longer expire.");
    }
}
