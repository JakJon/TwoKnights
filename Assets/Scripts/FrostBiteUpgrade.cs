using UnityEngine;

// Frost Bite — the Frigid capstone (requires 4 Frigid picks).
//
// Chilled bodies lose 2 a second, frozen bodies 3. It is the one upgrade in the
// Order that deals damage on a timer, and it is the whole reason the Order can
// close a wave on its own instead of handing every kill to the other knight.
//
// NOT gated on Deep Freeze, deliberately (owner's call, 2026-09-08). A knight who
// only ever chills — the Glacial Ward build, which needs no aim and is the Order's
// no-skill door — still gets the chilled rate. Gating it on the freeze chain would
// have made that whole half of the roster a dead end.
//
// It is also the only place Frigid's damage lives. Deep Freeze III holds a body for
// up to nineteen seconds and does nothing to it; the chill slows and does nothing to it.
// Everything the Order does is time until this is drafted, and then all of that held
// time starts converting at once — which is why the capstone slot is the right home
// for it and why nothing below it should ever grow a tick of its own.
//
// The combination to watch: Frost Tip + Deep Freeze III + Frost Bite is a 22s
// hold (13s chill left + 9s) at 3 a second = 66, and Frost Bite ticks never wear the
// ice down, so none of it interrupts itself. A bat, a rat, a grey wolf (45) or a
// black wolf (60) dies inside one hold; an ogre (80) walks out alive. With Hoarfrost
// Band doubling the chill it is 35s and 105 - the ogre dies. (Figures from
// 2026-10-05, when Deep Freeze took over the chill time and added to it.)
// Bosses are held half as long, so ~28 into 1500-2500: nothing.
//
// The reason a second freeze is always available is that the thaw reprieve was
// removed on the same day - a body out of the ice can be chilled and stopped again
// immediately. What stops that being free is pillar 2: only a blow can freeze.
//
// That is the intended ceiling, not a defect waiting on a nerf - the fun is in a
// player finding the combination. If it is ever genuinely wanted lower, the one
// number is FrostBiteFrozenDps.
[CreateAssetMenu(fileName = "FrostBiteUpgrade", menuName = "Upgrades/Frost Bite")]
public class FrostBiteUpgrade : BaseUpgrade
{
    public override string ChainName => "Frost Bite";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Frost Bite";
        if (weight == 0f)
            weight = 12f; // Legendary
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        FrigidBoost boost = targetKnight.GetComponent<FrigidBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<FrigidBoost>();
        }

        boost.SetFrostBite();

        Debug.Log($"Applied Frost Bite to {targetKnight.name}: chilled bodies lose " +
                  $"{FrigidBoost.FrostBiteChilledDps}/s, frozen {FrigidBoost.FrostBiteFrozenDps}/s");
    }
}
