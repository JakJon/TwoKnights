using UnityEngine;

// Frigid discipline: a blow landed on a body held in ice breaks it, hard.
//
// The only damage in an Order that otherwise has none - and it proves the pillar
// rather than breaking it, because this is damage the player aimed and landed,
// not a timer paying out.
//
// The tension is the whole point and it is there on every frozen body on the
// field: break it for the damage, or leave it standing as a statue and keep the
// time. There is no right answer, which is what makes this a discipline instead
// of a number.
//
// Rank two throws splinters, which CHILL and never freeze (pillar 2). They are
// the Order's only way to reach more than one body at a time.
[CreateAssetMenu(fileName = "ShatterUpgrade", menuName = "Upgrades/Shatter")]
public class ShatterUpgrade : BaseUpgrade
{
    [Tooltip("1 = the blow deals double, 2 = triple and throws chilling splinters.")]
    [SerializeField] private int shatterLevel = 1;

    public override string ChainName => "Shatter";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Shatter";
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

        boost.SetShatter(shatterLevel);

        Debug.Log($"Applied Shatter {shatterLevel} to {targetKnight.name}: x{boost.ShatterMultiplier} on frozen bodies, splinters {boost.ShatterSplinters}");
    }
}
