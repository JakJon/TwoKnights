using UnityEngine;

// Ember discipline: a sword swing has a low chance to hurl a spread of fireballs
// along the shield facing. Rank II deliberately does NOT improve the odds — it
// adds a third fireball. The same rare moment hits harder rather than happening
// more often, which keeps Firebrand a payoff you can't fish for and keeps the
// sword a close-range panic button rather than a primary fire delivery system.
[CreateAssetMenu(fileName = "FirebrandUpgrade", menuName = "Upgrades/Firebrand")]
public class FirebrandUpgrade : BaseUpgrade
{
    [SerializeField] private float hurlChance = 20f; // Percent per swing — same at both ranks
    [SerializeField] private int fireballCount = 2;
    [Tooltip("Fireball prefab, so Firebrand works even if the Fireball chain wired nothing yet")]
    [SerializeField] private GameObject fireballPrefab;

    public override string ChainName => "Firebrand";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Firebrand";
        if (weight == 0f)
            weight = 55f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        EmberBoost boost = targetKnight.GetComponent<EmberBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<EmberBoost>();
        }

        boost.SetFirebrand(hurlChance, fireballCount);
        boost.SetFireballPrefab(fireballPrefab);

        Debug.Log($"Applied Firebrand to {targetKnight.name}: {hurlChance}% chance, {fireballCount} fireballs");
    }
}
