using UnityEngine;

// Frigid discipline: cold landing on an already chilled body now FREEZES it.
//
// This is the pick that turns the Order on. Everything before it is a slow;
// everything after it is a stop. Gating it behind a Rare means jamming the mine's
// carts is a deliberate build choice rather than something that happens to a
// player who took one Common.
//
// A frozen cart is the showcase: a held cart anchors MineCart's spacing sweep, so
// the run behind it stacks up against it, and one arrow turns the mine's signature
// hazard into a wall of the player's own making.
[CreateAssetMenu(fileName = "DeepFreezeUpgrade", menuName = "Upgrades/Deep Freeze")]
public class DeepFreezeUpgrade : BaseUpgrade
{
    [Tooltip("1 = held for 3s, 2 = held for 6s.")]
    [SerializeField] private int freezeLevel = 1;

    public override string ChainName => "Deep Freeze";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Deep Freeze";
        if (weight == 0f)
            weight = 55f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        FrigidBoost boost = targetKnight.GetComponent<FrigidBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<FrigidBoost>();
        }

        boost.SetDeepFreeze(freezeLevel);

        Debug.Log($"Applied Deep Freeze {freezeLevel} to {targetKnight.name}: a second touch of cold holds for {boost.FreezeSeconds}s");
    }
}
