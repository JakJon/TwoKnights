using UnityEngine;

// Reflector (Guardian discipline): the guard stops being purely defensive. Anything
// that lands on it comes back off, keeping whatever it was carrying — a plain stone
// hits like an arrow, a green one rots what it lands on, a powder one actually goes
// off. The mine's own escalation is therefore what makes this chain scale; deeper
// waves throw better ammunition, and a Guardian knight is the one who gets to use it.
//
// EVERYTHING THROWN AT A KNIGHT IS FAIR GAME (owner's call, 2026-09-09) — rocks,
// gnomes' pickaxes, the giant slimes' fireballs, a cart's bomb. It used to be rocks
// alone, which was never a rule anyone wrote down; it was simply where the code
// lived. The shared door is GuardianReflect, and each kind of ammunition only says
// how fast it was going and what it hits for. The one exemption is a slime's WARD,
// which is the boss's guard rather than a shot at anybody.
//
// The chain is a ROLL, not a count, and it is TWO PICKS LONG — 50 then 100 (owner,
// 2026-09-09). Rank II is deliberately certainty rather than a better coin, so the
// end of the chain is a promise the player can build around, and it is priced as a
// Legendary because that promise is the whole thing being bought. The middle tier
// that used to sit between them was cut rather than retuned: a step from "usually"
// to "usually" is not a pick anyone has to think about.
[CreateAssetMenu(fileName = "ReflectorUpgrade", menuName = "Upgrades/Reflector")]
public class ReflectorUpgrade : BaseUpgrade
{
    [Tooltip("Rank this tier grants: 1 or 2. Chance and damage both come off GuardianBoost, so the tuning is one file rather than one per asset.")]
    [SerializeField] private int rank = 1;

    public override string ChainName => "Reflector";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Reflector";
        if (weight == 0f)
            weight = 60f; // Rare
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        GuardianBoost boost = targetKnight.GetComponent<GuardianBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<GuardianBoost>();
        }

        boost.SetReflector(rank);

        // The same figure that sends a rock back sends this knight's arrows out.
        // Projectile speed used to be a classless stat chain anybody could buy; it
        // lives here now because "things leave your guard faster" is one idea, and
        // the card can print one number for both halves of it.
        PlayerShooter shooter = targetKnight.GetComponent<PlayerShooter>();
        if (shooter != null) shooter.SetProjectileSpeedMultiplier(boost.ReflectSpeedMultiplier);

        Debug.Log($"Applied Reflector to {targetKnight.name}: rank {boost.ReflectorLevel}, " +
                  $"{boost.ReflectChance:0}% of blocks, damage x{boost.ReflectDamageMultiplier:F2}, " +
                  $"speed x{boost.ReflectSpeedMultiplier:F2}");
    }
}
