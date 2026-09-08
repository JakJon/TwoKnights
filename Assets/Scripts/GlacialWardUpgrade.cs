using UnityEngine;

// Frigid discipline: a standing ring of cold around the knight. The signature,
// and the Order's second Common door.
//
// The most Two Knights upgrade in the game: the knights cannot move, so an aura
// is a real, readable, stationary piece of the board rather than something the
// player has to carry around, and it needs no aim at all.
//
// Rank two is the clearest statement of what the Order is for. The core loop here
// is rotating a shield to intercept things; halving the speed of an incoming rock
// is not damage and not defence, it is time to get the shield there.
//
// The ward CHILLS and never freezes - pillar 2. See GlacialWard and FrigidBoost.
[CreateAssetMenu(fileName = "GlacialWardUpgrade", menuName = "Upgrades/Glacial Ward")]
public class GlacialWardUpgrade : BaseUpgrade
{
    [Tooltip("1 = 2.5u ring, 2 = 3.5u and enemy ammunition crossing it is halved.")]
    [SerializeField] private int wardLevel = 1;

    public override string ChainName => "Glacial Ward";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Glacial Ward";
        if (weight == 0f)
            weight = 100f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        FrigidBoost boost = targetKnight.GetComponent<FrigidBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<FrigidBoost>();
        }

        boost.SetGlacialWard(wardLevel);

        // The ring is its own component rather than something the sheet ticks, so
        // a knight with no ward pays nothing per frame for the Order existing.
        if (targetKnight.GetComponent<GlacialWard>() == null)
        {
            targetKnight.AddComponent<GlacialWard>();
        }

        Debug.Log($"Applied Glacial Ward {wardLevel} to {targetKnight.name}: {boost.WardRadius}u ring, slows ammo {boost.WardSlowsProjectiles}");
    }
}
