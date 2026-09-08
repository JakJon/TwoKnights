using UnityEngine;

// Frigid discipline: the arrow door. Every arrow this knight fires carries cold.
//
// The chain deepens the cold rather than buying access to it, because access is
// already free at rank one - and the depth carries to EVERYTHING the knight's
// cold touches, the ward and the blade included. Frost Tip is not "your arrows
// are colder", it is "you are colder".
//
// Rank one on its own is a real upgrade that is not yet a build: things arrive
// slower and you get more shots into them, and nothing freezes. Deep Freeze is
// the pick that turns the Order on.
[CreateAssetMenu(fileName = "FrostTipUpgrade", menuName = "Upgrades/Frost Tip")]
public class FrostTipUpgrade : BaseUpgrade
{
    [Tooltip("1 = half speed for 3s, 2 = a fifth for 6s, 3 = a tenth for 10s.")]
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

        Debug.Log($"Applied Frost Tip {tipLevel} to {targetKnight.name}: chilled bodies move at x{boost.ChillSpeedMultiplier} for {boost.ChillSeconds}s");
    }
}
