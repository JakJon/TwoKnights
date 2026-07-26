using UnityEngine;

// Ember discipline: ignited enemies move faster, and fire zones burn hotter.
//
// It reads as a downside — you are making the things running at you run faster —
// and that tension is the point. With Fire Trail it's a large gain: a panicking
// wolf covers more ground while burning, paints more field, and reaches its own
// trail sooner. Gated behind Fire Trail, because without trails it is purely a
// drawback.
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

        Debug.Log($"Applied Searing Panic {panicLevel} to {targetKnight.name}: x{boost.PanicSpeedMultiplier} speed, zones now {boost.ZoneDps} dps");
    }
}
