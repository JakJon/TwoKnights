using UnityEngine;

// Guardian discipline: the shield bows back around the knight instead of standing flat.
// The ends sweep inboard, so the shield covers a wider slice of the orbit circle and
// catches shots arriving off-axis — the angles a straight bar lets slide past its edge.
[CreateAssetMenu(fileName = "ShieldCurveUpgrade", menuName = "Upgrades/Shield Curve")]
public class ShieldCurveUpgrade : BaseUpgrade
{
    // Radius of the bow in shield-local units — smaller is tighter. Curvature rather
    // than a fixed sweep angle, so a Tower Shield bends through a wider arc for free.
    [SerializeField] private float curveRadius = 2.6f;

    public override string ChainName => "Curved Aegis";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Curved Aegis";
        if (weight == 0f)
            weight = 16f; // Legendary
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        ShieldOrbit shield = targetKnight.GetComponentInChildren<ShieldOrbit>();
        if (shield == null)
        {
            Debug.LogWarning($"ShieldCurveUpgrade: {targetKnight.name} has no ShieldOrbit to bend.");
            return;
        }

        ShieldShape shape = shield.GetComponent<ShieldShape>();
        if (shape == null)
        {
            shape = shield.gameObject.AddComponent<ShieldShape>();
        }

        shape.SetCurveRadius(curveRadius);

        Debug.Log($"Applied {upgradeName} to {targetKnight.name}: curve radius {curveRadius}, arc {shape.ArcDegrees:0}°");
    }
}
