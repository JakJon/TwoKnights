using UnityEngine;

// Bowsight: bolts an aiming line onto the shield. The knight can't move, so every shot
// is a question of angle — the sight turns that guess into a read.
[CreateAssetMenu(fileName = "SightsUpgrade", menuName = "Upgrades/Sights")]
public class SightsUpgrade : BaseUpgrade
{
    // How far downrange the line reaches, in world units. Absolute per tier.
    [SerializeField] private float sightRange = 2.5f;

    public override string ChainName => "Bowsight";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Bowsight";
        if (weight == 0f)
            weight = 34f; // Epic
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

        sight.SetRange(sightRange);

        Debug.Log($"Applied {upgradeName} to {targetKnight.name}: sight range {sightRange}");
    }
}
