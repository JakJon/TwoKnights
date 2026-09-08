using UnityEngine;

// Dawn discipline: the kill door. Every Nth enemy this knight kills heals them.
// The cadence is COUNTED, never rolled — the player can watch it coming, the
// same way Ember's Fireball lands on a beat you can count to. A chance-based
// lifesteal would make the same average health feel like weather.
[CreateAssetMenu(fileName = "LifebloomUpgrade", menuName = "Upgrades/Lifebloom")]
public class LifebloomUpgrade : BaseUpgrade
{
    [SerializeField] private int killsPerHeal = 12;
    [SerializeField] private int healAmount = 3;

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

        boost.SetLifebloom(killsPerHeal, healAmount);

        Debug.Log($"Applied Lifebloom to {targetKnight.name}: every {killsPerHeal} kills heals {healAmount}");
    }
}
