using UnityEngine;

// Dawn discipline: the panic door. The first time in a wave this knight is
// driven below the threshold, they get a brief untouchable window to recover
// their footing. It answers the blow rather than preventing it — the hit that
// takes you low still lands, which keeps the moment legible.
//
// Reuses PlayerHealth's existing invulnerability deadline (the same one Iron
// Vigil sets), so there is exactly one untouchable system in the game.
[CreateAssetMenu(fileName = "SecondWindUpgrade", menuName = "Upgrades/Second Wind")]
public class SecondWindUpgrade : BaseUpgrade
{
    [SerializeField] private float healthThreshold = 0.3f; // fraction of max HP
    [SerializeField] private float invulnerableSeconds = 1.2f;
    [SerializeField] private int mendAmount = 0; // rank II also mends

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

        boost.SetSecondWind(healthThreshold, invulnerableSeconds, mendAmount);

        Debug.Log($"Applied Second Wind to {targetKnight.name}: below {healthThreshold:P0} HP -> {invulnerableSeconds}s untouchable, mend {mendAmount}");
    }
}
