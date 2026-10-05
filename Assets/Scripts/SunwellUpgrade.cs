using UnityEngine;

// Dawn discipline: the orb door. Health orbs are already collected by SHOOTING
// them (CollectibleOrb), so this chain rewards aim rather than paying out for
// standing still — it makes a shot the player already wanted to take worth more.
// Rank II crosses the orbs over (owner, 2026-10-05): a health orb also gives
// special and a mana orb also heals. Rank III slows orbs for both knights, the
// one Dawn effect that lands on the field instead of on a knight.
[CreateAssetMenu(fileName = "SunwellUpgrade", menuName = "Upgrades/Sunwell")]
public class SunwellUpgrade : BaseUpgrade
{
    [SerializeField] private float orbHealMultiplier = 1.5f;
    [SerializeField] private float manaOrbMultiplier = 1.5f; // rank I+: mana orbs fill more of the bar
    [SerializeField] private int manaOrbHeal = 0;   // rank II+: mana orbs heal too
    [SerializeField] private int healthOrbSpecial = 0; // rank II+: health orbs fill the special bar too
    [SerializeField] private bool slowOrbs = false; // rank III: orbs linger for both knights

    public override string ChainName => "Sunwell";

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(upgradeName))
            upgradeName = "Sunwell";
        if (weight == 0f)
            weight = 110f;
    }

    public override void ApplyUpgrade(GameObject targetKnight)
    {
        DawnBoost boost = targetKnight.GetComponent<DawnBoost>();
        if (boost == null)
        {
            boost = targetKnight.AddComponent<DawnBoost>();
        }

        boost.SetOrbHealMultiplier(orbHealMultiplier);
        boost.SetManaOrbMultiplier(manaOrbMultiplier);
        if (manaOrbHeal > 0) boost.SetManaOrbHeal(manaOrbHeal);
        if (healthOrbSpecial > 0) boost.SetHealthOrbSpecial(healthOrbSpecial);
        if (slowOrbs) boost.EnableSlowOrbs();

        Debug.Log($"Applied Sunwell to {targetKnight.name}: orbs x{orbHealMultiplier}, mana x{manaOrbMultiplier}, mana heal {manaOrbHeal}, health orb special {healthOrbSpecial}, slow {slowOrbs}");
    }
}
