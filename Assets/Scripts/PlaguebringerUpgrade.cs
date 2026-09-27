using UnityEngine;

// Serpent + Guardian combo: every poison cloud this knight makes (Miasma, Serpent's
// Breath, Vial Throw) hunts the nearest on-screen mob it has not poisoned yet —
// Guardian's Guided Shot, taught to the Serpent's clouds. The steering and the
// gold-flecked look live in PoisonCloud.
//
// Since 2026-09-26 (owner) the knight's poison BUBBLES hunt too — Venom Tip's
// trail, Serpent's Breath's beads, and the beads Airborne Virus makes enemies
// breathe out. A bead is spent on its first body, so it simply chases the nearest
// on-screen mob; see PoisonTrailBubble.
//
// Gated like Sleeping Dart, the other two-Order upgrade: two owned upgrades of EACH
// Order (requiresOrderCount + requiresSecondOrderCount on the asset), AND unlockedBy
// = Miasma I, Serpent's Breath I or Vial Throw I — a knight with no way to make a
// cloud would be buying nothing.
//
// Was the Serpent capstone until 2026-09-10, when Acid Dagger took that slot. The
// asset keeps its filename, so upgrades.taken.plaguebringer still counts this pick.
[CreateAssetMenu(fileName = "PlaguebringerUpgrade", menuName = "Upgrades/Plaguebringer")]
public class PlaguebringerUpgrade : BaseUpgrade
{
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Plaguebringer";
        if (weight == 0f)
            weight = 55f; // Rare, like Sleeping Dart
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        PoisonTipBoost boost = targetKnight.GetComponent<PoisonTipBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<PoisonTipBoost>();
        }

        boost.EnablePlaguebringer();

        Debug.Log($"Applied Plaguebringer to {targetKnight.name}.");
    }
}
