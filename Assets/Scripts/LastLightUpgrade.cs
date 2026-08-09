using UnityEngine;

// Dawn capstone (requires 4 Dawn picks): the vigil. Once per map, when the
// OTHER knight would die, they don't — they hold at 1 HP, are mended, and both
// knights get a beat of untouchability to reset.
//
// It is deliberately the PARTNER it saves, never the owner. Dawn's whole shape
// is that its best numbers route through the other knight, and a capstone that
// saved its own buyer would be an insurance policy instead of a vigil. The
// rescue is read from the dying knight's side in PlayerHealth.TakeDamage, which
// looks up the partner's sheet.
[CreateAssetMenu(fileName = "LastLightUpgrade", menuName = "Upgrades/Last Light")]
public class LastLightUpgrade : BaseUpgrade
{
    public override string ChainName => "Last Light";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Last Light";
        if (weight == 0f)
            weight = 15f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        DawnBoost boost = targetKnight.GetComponent<DawnBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<DawnBoost>();
        }

        boost.EnableLastLight();

        Debug.Log($"Applied Last Light to {targetKnight.name}: vigil armed over the other knight");
    }
}
