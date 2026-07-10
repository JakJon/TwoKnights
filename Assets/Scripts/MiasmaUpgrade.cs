using UnityEngine;

// Serpent discipline: enemies that die poisoned leave a miasma cloud that poisons
// nearby enemies. Each pick raises the knight's cloud level (size + linger).
[CreateAssetMenu(fileName = "MiasmaUpgrade", menuName = "Upgrades/Miasma")]
public class MiasmaUpgrade : BaseUpgrade
{
    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Miasma";
        if (weight == 0f)
            weight = 40f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        PoisonTipBoost boost = targetKnight.GetComponent<PoisonTipBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<PoisonTipBoost>();
        }

        boost.IncreaseMiasmaLevel();

        Debug.Log($"Applied Miasma to {targetKnight.name}. Miasma level: {boost.MiasmaLevel}");
    }
}
