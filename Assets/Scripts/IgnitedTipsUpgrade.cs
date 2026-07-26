using UnityEngine;

// Ember discipline: arrows have a chance to set their target alight. This is an
// ACCESS chain, not a damage chain — it buys the right to start fires, and the
// fire on the ground is what actually kills. Rolls independently per projectile,
// so shadow arrows and shurikens each get their own chance.
[CreateAssetMenu(fileName = "IgnitedTipsUpgrade", menuName = "Upgrades/Ignited Tips")]
public class IgnitedTipsUpgrade : BaseUpgrade
{
    [SerializeField] private float igniteChance = 30f; // Percent chance per projectile

    public override string ChainName => "Ignited Tips";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Ignited Tips";
        if (weight == 0f)
            weight = 110f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        EmberBoost boost = targetKnight.GetComponent<EmberBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<EmberBoost>();
        }

        boost.SetIgniteChance(igniteChance);

        Debug.Log($"Applied Ignited Tips to {targetKnight.name}: {igniteChance}% ignite chance");
    }
}
