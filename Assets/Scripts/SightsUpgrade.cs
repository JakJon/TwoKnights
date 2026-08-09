using UnityEngine;

// Bowsight: bolts a laser sight onto the shield. The knight can't move, so every shot is a
// question of angle — the beam turns that guess into a read, and it chews on anything left
// standing in it.
[CreateAssetMenu(fileName = "SightsUpgrade", menuName = "Upgrades/Sights")]
public class SightsUpgrade : BaseUpgrade
{
    // How far downrange the beam reaches, in world units. The arena is 20x11.25 and the
    // knights sit at x = +/-2, so the full tier runs off the edge of the screen at any
    // angle — a laser sight shouldn't stop in mid-air.
    [SerializeField] private float sightRange = 6f;
    // Damage per second of unbroken contact. ShieldSight quantises this into 1-point
    // ticks, so 2 dps means "one damage per half second held on target".
    [SerializeField] private float damagePerSecond = 2f;

    public override string ChainName => "Bowsight";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Bowsight";
        if (weight == 0f)
            weight = 16f; // Legendary
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        ShieldOrbit shield = targetKnight.GetComponentInChildren<ShieldOrbit>();
        if (shield == null)
        {
            Debug.LogWarning($"SightsUpgrade: {targetKnight.name} has no ShieldOrbit to mount a sight on.");
            return;
        }

        ShieldSight sight = shield.GetComponent<ShieldSight>();
        if (sight == null)
        {
            sight = shield.gameObject.AddComponent<ShieldSight>();
        }

        sight.Configure(sightRange, damagePerSecond);

        Debug.Log($"Applied {upgradeName} to {targetKnight.name}: beam {sightRange} units, {damagePerSecond} dps");
    }
}
