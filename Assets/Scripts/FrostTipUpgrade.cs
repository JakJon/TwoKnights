using UnityEngine;

// Frigid discipline: the arrow door. Every arrow this knight fires carries cold.
//
// The chain deepens the cold rather than buying access to it, because access is
// already free at rank one - and the depth carries to EVERYTHING the knight's
// cold touches, the ward and the blade included. Frost Tip is not "your arrows
// are colder", it is "you are colder".
//
// Rank one already freezes (owner's call, 2026-09-11): the first arrow chills,
// and a second arrow into a chilled body freezes it for however long the chill
// had left. That makes the chain's longer chills longer freezes too. Deep Freeze
// adds time and toughness to the ice. Shatter unlocks off rank one for the same
// reason - there is something to break now.
[CreateAssetMenu(fileName = "FrostTipUpgrade", menuName = "Upgrades/Frost Tip")]
public class FrostTipUpgrade : BaseUpgrade
{
    [Tooltip("Always 1. Frost Tip is a single card since 2026-10-05: arrows chill (-30% for 3s) and a second hit freezes. The deeper slow and the longer chill its ranks II and III used to give are on Deep Freeze II and III now.")]
    [SerializeField] private int tipLevel = 1;

    public override string ChainName => "Frost Tip";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Frost Tip";
        if (weight == 0f)
            weight = 110f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        FrigidBoost boost = targetKnight.GetComponent<FrigidBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<FrigidBoost>();
        }

        boost.SetFrostTip(tipLevel);

        Debug.Log($"Applied Frost Tip {tipLevel} to {targetKnight.name}: chilled bodies move at x{boost.ChillSpeedMultiplier} for {boost.ChillSeconds}s, and can now freeze");
    }
}
