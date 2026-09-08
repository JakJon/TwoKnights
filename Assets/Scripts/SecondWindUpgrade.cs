using UnityEngine;

// Dawn discipline: the toll door. Every so many points of damage this knight
// absorbs, they get an untouchable window to recover their footing. It answers
// the blow rather than preventing it — the hit that settles the toll still
// lands, which keeps the moment legible.
//
// Damage taken rather than a health fraction: a knight who is chipped down all
// wave earns it exactly as much as one who eats a single huge hit, and it can
// come around again in the same wave. Nothing about it is a dice roll.
//
// Reuses PlayerHealth's existing invulnerability deadline (the same one Iron
// Vigil sets), so there is exactly one untouchable system in the game.
[CreateAssetMenu(fileName = "SecondWindUpgrade", menuName = "Upgrades/Second Wind")]
public class SecondWindUpgrade : BaseUpgrade
{
    [SerializeField] private int damageToll = 60; // damage absorbed per window
    [SerializeField] private float invulnerableSeconds = 6f;
    [SerializeField] private int healAmount = 0; // rank II also heals

    public override string ChainName => "Second Wind";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Second Wind";
        if (weight == 0f)
            weight = 90f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        DawnBoost boost = targetKnight.GetComponent<DawnBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<DawnBoost>();
        }

        boost.SetSecondWind(damageToll, invulnerableSeconds, healAmount);

        Debug.Log($"Applied Second Wind to {targetKnight.name}: every {damageToll} damage taken -> {invulnerableSeconds}s untouchable, heal {healAmount}");
    }
}
