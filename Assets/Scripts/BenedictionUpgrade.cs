using UnityEngine;

// Dawn discipline: the special door. Spending the special lifts BOTH knights —
// whatever the special happens to be. Hooked once in PlayerSpecial
// rather than inside each special, so Rapid Fire, Field Healing, and anything a
// later loadout adds all carry the blessing without knowing about Dawn.
//
// Rank II deliberately raises the number and adds a shared untouchable beat
// rather than making the special cheaper; Dawn buys resilience, not tempo.
[CreateAssetMenu(fileName = "BenedictionUpgrade", menuName = "Upgrades/Benediction")]
public class BenedictionUpgrade : BaseUpgrade
{
    [SerializeField] private int healAmount = 20;
    [SerializeField] private float invulnerableSeconds = 0f; // rank II

    public override string ChainName => "Benediction";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Benediction";
        if (weight == 0f)
            weight = 65f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        DawnBoost boost = targetKnight.GetComponent<DawnBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<DawnBoost>();
        }

        boost.SetBenediction(healAmount, invulnerableSeconds);

        Debug.Log($"Applied Benediction to {targetKnight.name}: special heals both knights {healAmount}, {invulnerableSeconds}s shared untouchable");
    }
}
