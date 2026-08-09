using UnityEngine;

// Fire Sight: the capstone where the Guardian's beam and the Ember Order finally
// meet. The sight stops merely cooking what stands in it and sets it alight — the
// beam becomes an ignition source in its own right, and everything the Ember tree
// has been buying (trails, panic, scorched earth, stacking burns) now fires off a
// weapon that costs no arrows at all.
//
// It is deliberately the most expensive door in the game to open, because it is
// two chains deep rather than one: Bowsight II for the beam, and five Ember picks
// for the fire. Neither half is any use here without the other, so it cannot be
// stumbled into — a knight arrives at this because they spent a whole run heading
// for it.
//
// The payoff is a change of KIND, not of number. Nothing about the beam's damage
// moves. What changes is that holding it on something is now worth doing for a
// reason other than the damage it deals, and a knight who could only ever answer
// one enemy at a time can suddenly start a fire that answers the rest.
[CreateAssetMenu(fileName = "FireSightUpgrade", menuName = "Upgrades/Fire Sight")]
public class FireSightUpgrade : BaseUpgrade
{
    public override string ChainName => "Fire Sight";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Fire Sight";
        if (weight == 0f)
            weight = 8f; // Legendary, and rarer than Bowsight itself
    }

    // BOTH halves of the gate live in the asset, because those are the only two
    // gates UpgradeManager actually reads (BaseUpgrade.CanApply is declared but
    // never consulted, so a guard written there would be decoration):
    //   * order = Ember with requiresOrderCount = 5 — the five fire picks;
    //   * unlockedBy = [Bowsight 2] — the beam. That list is ANY-of, so Bowsight II
    //     must be the ONLY entry, or owning Bowsight I alone would open the door.
    public override void ApplyUpgrade(GameObject targetKnight)
    {
        ShieldSight sight = targetKnight.GetComponentInChildren<ShieldSight>();
        if (sight == null)
        {
            // Reachable only if the gating is mis-authored — Bowsight is what
            // mounts the beam, and without one there is nothing to set alight.
            Debug.LogWarning($"FireSightUpgrade: {targetKnight.name} has no ShieldSight to light. " +
                             "Check that this upgrade is unlocked by Bowsight 2.");
            return;
        }

        sight.SetIgnites(true);

        Debug.Log($"Applied {upgradeName} to {targetKnight.name}: the sight now ignites what it touches");
    }
}
