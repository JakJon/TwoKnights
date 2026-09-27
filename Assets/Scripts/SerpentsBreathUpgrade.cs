using UnityEngine;

// Serpent discipline: the SWORD's venom.
//
// Every sword swing exhales, without a roll. That is the change of 2026-09-17 and
// it is what the chain is now about: the knight's facing is the exhale's direction,
// so a swing is a placement, and a placement you only get two swings in three is a
// placement you cannot plan around.
//
// The ranks, and why they are shaped this way:
//   I   one venom bead down the facing, 4.5s
//   II  three beads in a narrow fan, 4.5s
//   III the same three beads at 6s, and an 8s venom cloud out with them
//
// Every rank throws the same object the Venom Tip trail is made of - a bead, spent
// on the first body it touches - so rank II is three separate catches rather than
// one thicker one. Rank III keeps that fan and adds what a bead is not: a cloud
// does not get spent, it keeps working on everything that walks through it, and it
// goes down the middle of the fan. That is the rank the discipline is built toward.
//
// The asset states its RANK and nothing else, the way Vial Throw's do. Everything
// the rank means lives in PoisonTipBoost.SetBreathTier, so three assets cannot
// quietly disagree about what Serpent's Breath II is.
[CreateAssetMenu(fileName = "SerpentsBreathUpgrade", menuName = "Upgrades/Serpent's Breath")]
public class SerpentsBreathUpgrade : BaseUpgrade
{
    [Tooltip("Which rank this asset is: 1, 2 or 3. The boost derives the whole exhale from it (see PoisonTipBoost.SetBreathTier).")]
    [SerializeField] private int tier = 1;

    public override string ChainName => "Serpent's Breath";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Serpent's Breath";
        if (weight == 0f)
            weight = 100f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        PoisonTipBoost boost = targetKnight.GetComponent<PoisonTipBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<PoisonTipBoost>();
        }

        boost.SetBreathTier(tier);

        Debug.Log($"Applied Serpent's Breath {tier} to {targetKnight.name}: " +
                  $"{boost.BreathBeadCount} bead(s) per swing" +
                  (boost.BreathSendsCloud ? " plus a cloud." : "."));
    }
}
