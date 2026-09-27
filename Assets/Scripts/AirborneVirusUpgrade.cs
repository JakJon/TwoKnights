using UnityEngine;

// Serpent discipline: the venom spreads on its own.
//
// Replaced Virulence (owner, 2026-09-25), which only made the numbers bigger.
// This one gives a poisoned enemy something to DO: every few seconds it breathes
// venom beads out around itself, and whatever those touch is poisoned too.
//
//   I   two beads, left and right, every 6s
//   II  five beads in a pentagon, every 5s
//   III eight beads in a ring, every 4s
//   IV  ten beads in a ring, every 2s
//
// The asset states its RANK and nothing else, the way Serpent's Breath's do.
// Everything the rank means lives in PoisonTipBoost.SetAirborneTier, and the
// release itself is EnemyBase.ReleaseAirborneVirus.
[CreateAssetMenu(fileName = "AirborneVirusUpgrade", menuName = "Upgrades/Airborne Virus")]
public class AirborneVirusUpgrade : BaseUpgrade
{
    [Tooltip("Which rank this asset is: 1 to 4. The boost derives the whole release from it (see PoisonTipBoost.SetAirborneTier).")]
    [SerializeField] private int tier = 1;

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Airborne Virus";
        if (weight == 0f)
            weight = 50f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        PoisonTipBoost boost = targetKnight.GetComponent<PoisonTipBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<PoisonTipBoost>();
        }

        boost.SetAirborneTier(tier);

        Debug.Log($"Applied Airborne Virus {tier} to {targetKnight.name}: " +
                  $"{boost.AirborneBubbleCount} bubbles every {boost.AirborneIntervalSeconds}s.");
    }
}
