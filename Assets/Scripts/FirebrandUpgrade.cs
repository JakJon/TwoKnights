using UnityEngine;

// Ember discipline: EVERY sword swing lobs fire along the shield facing (owner's
// call, 2026-09-25). It used to be a 20% roll for a fireball that flew four
// seconds; now it is certain and short. The fireball is lobbed, not thrown: it
// arcs a short way out, comes down and bursts.
//
//   I   one fireball, lands 0.5u out
//   II  the same fireball lobbed three times as far, 1.5u
//   III a second fireball beside the first
//
// The asset states its RANK and nothing else; what a rank means lives in
// EmberBoost, so three assets cannot quietly disagree about Firebrand II.
[CreateAssetMenu(fileName = "FirebrandUpgrade", menuName = "Upgrades/Firebrand")]
public class FirebrandUpgrade : BaseUpgrade
{
    [Tooltip("Which rank this asset is: 1, 2 or 3. See EmberBoost.SetFirebrand.")]
    [SerializeField] private int rank = 1;
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

        boost.SetFirebrand(rank);
        boost.SetFireballPrefab(fireballPrefab);

        Debug.Log($"Applied Firebrand {rank} to {targetKnight.name}: every swing, " +
                  $"{boost.FirebrandCount} fireball(s) lobbed {boost.FirebrandLobDistance}u");
    }
}
