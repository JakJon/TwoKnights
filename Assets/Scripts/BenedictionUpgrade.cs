using UnityEngine;

// Dawn discipline: the special door. Spending the special also lifts the OTHER
// knight — whatever the special happens to be. Hooked once in PlayerSpecial
// rather than inside each special, so Rapid Fire, Field Mending, and anything a
// later loadout adds all carry the blessing without knowing about Dawn.
//
// Rank II deliberately raises the number and adds a shared untouchable beat
// rather than making the special cheaper; Dawn buys resilience, not tempo.
[CreateAssetMenu(fileName = "BenedictionUpgrade", menuName = "Upgrades/Benediction")]
public class BenedictionUpgrade : BaseUpgrade
{
    [SerializeField] private int mendAmount = 15;
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

        boost.SetBenediction(mendAmount, invulnerableSeconds);

        Debug.Log($"Applied Benediction to {targetKnight.name}: special mends partner {mendAmount}, {invulnerableSeconds}s shared untouchable");
    }
}
