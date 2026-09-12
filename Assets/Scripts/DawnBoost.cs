using UnityEngine;

// The knight's Dawn stat sheet: every Dawn Order upgrade writes into this
// component. PlayerHealth reads it inside Heal (Shared Light) and TakeDamage
// (Second Wind, Last Light), CollectibleOrb reads the COLLECTING knight's sheet,
// PlayerSpecial reads it when the special is spent (Benediction), and it listens
// for kill credit itself to run Lifebloom's counter.
//
// THE DAWN PILLAR: no heal without a named source, and nothing is wasted.
// Every point of health this Order pays out is bought by something the player
// did — an orb they shot, a kill they counted, a special they spent. Dawn has
// NO passive regeneration, and it never lets healing evaporate: at full health
// Shared Light hands the overflow to the other knight instead of dropping it.
// Nothing here may relax either half of that. See Docs/Design/dawn-order.md.
//
// Setters are monotonic (Mathf.Max, or Min where lower is the upgrade) so a
// late re-pick of a lower tier can never downgrade a knight — same rule as
// EmberBoost and NinjaBoost.
public class DawnBoost : MonoBehaviour
{
    // --- Sunwell: the orb door ---
    private float orbHealMultiplier = 1f;
    private int manaOrbHeal = 0;
    private bool slowsOrbs = false;

    // Sunwell III slows orbs for BOTH knights — the orb is shared ground, so
    // this is the one Dawn effect that lands on the field rather than on a
    // knight. CollectibleOrb asks the static below at spawn time.
    public const float SlowedOrbSpeedMultiplier = 0.65f;

    // --- Shared Light: the partnership door ---
    private float echoFraction = 0f;
    private bool routesOverheal = false;

    // --- Lifebloom: the kill door ---
    // Deterministic, not a dice roll: the player can count to twelve and know
    // the heal is coming, the same way Ember's Fireball cadence is countable.
    private int lifebloomInterval = 0;
    private int lifebloomHeal = 0;
    private int killCounter = 0;

    // --- Second Wind: the toll door ---
    // Paid for in damage rather than read off a health fraction: every N points
    // of harm this knight absorbs buys one untouchable window, however low or
    // high their health happens to be. Deterministic and countable, the same way
    // Lifebloom's kill counter is, and it can pay out more than once in a wave
    // if the wave keeps hitting you. The counter runs for the whole map.
    private int secondWindToll = 0;         // damage per untouchable window
    private float secondWindSeconds = 0f;
    private int secondWindHeal = 0;
    private int damageTowardSecondWind = 0;

    // --- Benediction: the special door ---
    private int benedictionHeal = 0;
    private float benedictionInvuln = 0f;

    // --- Last Light: capstone ---
    private bool lastLight = false;
    private bool lastLightSpent = false;

    public const int LastLightHeal = 25;
    public const float LastLightInvulnSeconds = 2f;

    // The colour every Dawn effect flashes, matching the Order's UI accent
    // rgb(240, 200, 170) in UpgradeMenu.uss — a Dawn proc should be the same
    // warm gold wherever it happens.
    public static readonly Color DawnGlow = new Color(240f / 255f, 200f / 255f, 170f / 255f);

    private void OnEnable()
    {
        EnemyBase.OnEnemyKilledBy += HandleKillCredit;
    }

    private void OnDisable()
    {
        EnemyBase.OnEnemyKilledBy -= HandleKillCredit;
    }

    // ---- Sunwell ----

    public void SetOrbHealMultiplier(float multiplier)
    {
        orbHealMultiplier = Mathf.Max(orbHealMultiplier, multiplier);
    }

    // Equipment (Oathbound Locket) multiplies on top of whatever Sunwell tier the
    // knight holds, rather than going through the monotonic setter above, which
    // would swallow it the moment Sunwell matched it. Items always stack.
    private float equipmentOrbHealMultiplier = 1f;

    public void MultiplyOrbHealFromEquipment(float multiplier)
    {
        equipmentOrbHealMultiplier *= Mathf.Max(1f, multiplier);
    }

    private float TotalOrbHealMultiplier => orbHealMultiplier * equipmentOrbHealMultiplier;

    public void SetManaOrbHeal(int amount)
    {
        manaOrbHeal = Mathf.Max(manaOrbHeal, amount);
    }

    public void EnableSlowOrbs()
    {
        slowsOrbs = true;
    }

    public int ManaOrbHeal => manaOrbHeal;
    public bool SlowsOrbs => slowsOrbs;

    // Rounded up, so the first Sunwell tier is always worth at least +1 even on
    // a stingy orb
    public int ScaleOrbHeal(int baseAmount)
    {
        float multiplier = TotalOrbHealMultiplier;
        if (multiplier <= 1f) return baseAmount;
        return Mathf.CeilToInt(baseAmount * multiplier);
    }

    /// <summary>
    /// Whether EITHER knight has bought Sunwell III. Orbs are shared ground —
    /// one knight slowing them helps both, which is the Order behaving like
    /// itself. Called once per orb at spawn, never per frame.
    /// </summary>
    public static bool AnyKnightSlowsOrbs()
    {
        return KnightSlowsOrbs("PlayerLeft") || KnightSlowsOrbs("PlayerRight");
    }

    private static bool KnightSlowsOrbs(string knightTag)
    {
        GameObject knight = GameObject.FindWithTag(knightTag);
        if (knight == null) return false;
        DawnBoost boost = knight.GetComponent<DawnBoost>();
        return boost != null && boost.slowsOrbs;
    }

    // ---- Shared Light ----

    public void SetEchoFraction(float fraction)
    {
        echoFraction = Mathf.Clamp01(Mathf.Max(echoFraction, fraction));
    }

    public void EnableOverhealRouting()
    {
        routesOverheal = true;
    }

    // Equipment (Warm Lantern, Dawnbreak Crown) adds its share on top of Shared
    // Light's, for the same reason the orb multiplier above is kept apart.
    private float equipmentEchoFraction = 0f;

    public void AddEchoFractionFromEquipment(float fraction)
    {
        equipmentEchoFraction += Mathf.Max(0f, fraction);
    }

    public float EchoFraction => Mathf.Clamp01(echoFraction + equipmentEchoFraction);
    public bool RoutesOverheal => routesOverheal;

    // ---- Lifebloom ----

    public void SetLifebloom(int interval, int heal)
    {
        // Lower interval = more often, so Min is the upgrade direction here
        lifebloomInterval = lifebloomInterval == 0
            ? Mathf.Max(1, interval)
            : Mathf.Min(lifebloomInterval, Mathf.Max(1, interval));
        lifebloomHeal = Mathf.Max(lifebloomHeal, heal);
    }

    public int LifebloomInterval => lifebloomInterval;

    private void HandleKillCredit(string playerTag)
    {
        if (lifebloomInterval <= 0) return;
        if (!gameObject.CompareTag(playerTag)) return;

        killCounter++;
        if (killCounter < lifebloomInterval) return;

        killCounter = 0;
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health == null) return;

        health.Heal(lifebloomHeal);
        GetComponent<GlowManager>()?.StartGlow(DawnGlow, 0.25f);
        PlayerStats.Increment("dawn.lifebloom");
    }

    // ---- Second Wind ----

    public void SetSecondWind(int damageToll, float seconds, int heal)
    {
        // A smaller toll is the upgrade, so Min is the direction here — same
        // rule as Lifebloom's interval
        secondWindToll = secondWindToll == 0
            ? Mathf.Max(1, damageToll)
            : Mathf.Min(secondWindToll, Mathf.Max(1, damageToll));
        secondWindSeconds = Mathf.Max(secondWindSeconds, seconds);
        secondWindHeal = Mathf.Max(secondWindHeal, heal);
    }

    public float SecondWindSeconds => secondWindSeconds;
    public int SecondWindHeal => secondWindHeal;

    /// <summary>
    /// Books the damage this knight just took against the toll, and returns true
    /// on the blow that settles it. Spends the counter as it answers, so the
    /// caller can treat a true return as "the light has already been committed".
    /// </summary>
    public bool TrySpendSecondWind(int damage)
    {
        if (secondWindToll <= 0 || secondWindSeconds <= 0f || damage <= 0) return false;

        damageTowardSecondWind += damage;
        if (damageTowardSecondWind < secondWindToll) return false;

        // Carry the surplus instead of clearing it: a blow far bigger than the
        // toll must not throw away the part of itself that overpaid
        damageTowardSecondWind -= secondWindToll;
        PlayerStats.Increment("dawn.second_wind");
        return true;
    }

    // ---- Benediction ----

    public void SetBenediction(int heal, float invulnSeconds)
    {
        benedictionHeal = Mathf.Max(benedictionHeal, heal);
        benedictionInvuln = Mathf.Max(benedictionInvuln, invulnSeconds);
    }

    public bool HasBenediction => benedictionHeal > 0;

    /// <summary>
    /// Spending a special lifts BOTH knights, whatever the special was — Rapid
    /// Fire, Field Healing, anything a later loadout adds. Hooked once in
    /// PlayerSpecial rather than inside each special, so a new special cannot
    /// quietly escape the rule.
    ///
    /// It used to pay the partner only, and paying both is the owner's call
    /// (2026-09-06). The old shape made Benediction a gift you could not use on
    /// yourself, which meant the knight who did the work of filling the bar was
    /// the one knight it never reached — and since the special is spent at the
    /// worst moment of a wave, that is usually the knight who needed it. Paying
    /// both keeps the "your special is a thing you do FOR the pair" reading and
    /// stops it being a reason to hold the bar until your partner is hurt.
    /// </summary>
    public void PayBenediction(string ownTag)
    {
        if (benedictionHeal <= 0) return;

        Bless(gameObject);
        Bless(OtherKnight(ownTag));

        PlayerStats.Increment("dawn.benediction");
    }

    // One knight's share of the blessing: the heal, the glow that says where it
    // landed, and rank II's untouchable beat. Pulled out so the two knights
    // cannot drift apart — the whole point of the upgrade is that they get the
    // same thing.
    private void Bless(GameObject knight)
    {
        if (knight == null) return;

        PlayerHealth health = knight.GetComponent<PlayerHealth>();
        health?.Heal(benedictionHeal);
        knight.GetComponent<GlowManager>()?.StartGlow(DawnGlow, 1f);

        if (benedictionInvuln > 0f) health?.SetInvulnerable(benedictionInvuln);
    }

    // ---- Last Light (capstone) ----

    public void EnableLastLight()
    {
        lastLight = true;
    }

    /// <summary>
    /// The vigil this knight keeps over the OTHER one. Spends on success, so it
    /// fires at most once — the charge lasts as long as this component does,
    /// which is one map.
    /// </summary>
    public bool TrySpendLastLight()
    {
        if (!lastLight || lastLightSpent) return false;
        lastLightSpent = true;
        PlayerStats.Increment("dawn.last_light");
        return true;
    }

    public bool LastLightReady => lastLight && !lastLightSpent;

    // ---- shared ----

    public static GameObject OtherKnight(string ownTag)
    {
        string otherTag = ownTag == "PlayerLeft" ? "PlayerRight" : "PlayerLeft";
        return GameObject.FindWithTag(otherTag);
    }
}
