using UnityEngine;

// Dawn discipline: the kill door. Every Nth enemy this knight kills mends them.
// The cadence is COUNTED, never rolled — the player can watch it coming, the
// same way Ember's Fireball lands on a beat you can count to. A chance-based
// lifesteal would make the same average health feel like weather.
[CreateAssetMenu(fileName = "LifebloomUpgrade", menuName = "Upgrades/Lifebloom")]
public class LifebloomUpgrade : BaseUpgrade
{
    [SerializeField] private int killsPerMend = 12;
    [SerializeField] private int mendAmount = 3;

    public override string ChainName => "Lifebloom";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Lifebloom";
        if (weight == 0f)
            weight = 105f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        DawnBoost boost = targetKnight.GetComponent<DawnBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<DawnBoost>();
        }

        boost.SetLifebloom(killsPerMend, mendAmount);

        Debug.Log($"Applied Lifebloom to {targetKnight.name}: every {killsPerMend} kills mends {mendAmount}");
    }
}
