using UnityEngine;

// Ember discipline: a sword swing has a low chance to hurl fire along the shield
// facing. THREE ranks, and none of them improves the odds — each one adds a
// fireball to the same rare moment (I one, II two, III three). The payoff gets
// bigger rather than more frequent, which keeps Firebrand something you can't
// fish for and keeps the sword a close-range panic button rather than a primary
// fire delivery system.
//
// Rank I opening at ONE fireball is the whole point of the tier split. It used to
// open at two, which meant the first pick of a Rare-weight chain already put a
// pair of fireballs on a swing, and a chain whose entry-level tier is that strong
// has nowhere left to grow — the later ranks were decoration on something already
// worth taking. One is a real upgrade that is not yet a build.
[CreateAssetMenu(fileName = "FirebrandUpgrade", menuName = "Upgrades/Firebrand")]
public class FirebrandUpgrade : BaseUpgrade
{
    [SerializeField] private float hurlChance = 20f; // Percent per swing — same at every rank
    [SerializeField] private int fireballCount = 1;
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
