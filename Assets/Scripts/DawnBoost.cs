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
    private int manaOrbMend = 0;
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
    // the mend is coming, the same way Ember's Fireball cadence is countable.
    private int lifebloomInterval = 0;
    private int lifebloomMend = 0;
    private int killCounter = 0;

    // --- Second Wind: the panic door ---
    private float secondWindThreshold = 0f; // fraction of max HP
    private float secondWindSeconds = 0f;
    private int secondWindMend = 0;
    private bool secondWindSpent = false;   // cleared at every wave start

    // --- Benediction: the special door ---
    private int benedictionMend = 0;
    private float benedictionInvuln = 0f;

    // --- Last Light: capstone ---
    private bool lastLight = false;
    private bool lastLightSpent = false;

    public const int LastLightMend = 25;
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

    public void SetManaOrbMend(int amount)
    {
        manaOrbMend = Mathf.Max(manaOrbMend, amount);
    }

    public void EnableSlowOrbs()
    {
        slowsOrbs = true;
    }

    public int ManaOrbMend => manaOrbMend;
    public bool SlowsOrbs => slowsOrbs;

    // Rounded up, so the first Sunwell tier is always worth at least +1 even on
    // a stingy orb
    public int ScaleOrbHeal(int baseAmount)
    {
        if (orbHealMultiplier <= 1f) return baseAmount;
        return Mathf.CeilToInt(baseAmount * orbHealMultiplier);
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

    public float EchoFraction => echoFraction;
    public bool RoutesOverheal => routesOverheal;

    // ---- Lifebloom ----

    public void SetLifebloom(int interval, int mend)
    {
        // Lower interval = more often, so Min is the upgrade direction here
        lifebloomInterval = lifebloomInterval == 0
            ? Mathf.Max(1, interval)
            : Mathf.Min(lifebloomInterval, Mathf.Max(1, interval));
        lifebloomMend = Mathf.Max(lifebloomMend, mend);
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

        health.Heal(lifebloomMend);
        GetComponent<GlowManager>()?.StartGlow(DawnGlow, 0.25f);
        PlayerStats.Increment("dawn.lifebloom");
    }

    // ---- Second Wind ----

    public void SetSecondWind(float threshold, float seconds, int mend)
    {
        secondWindThreshold = Mathf.Clamp01(Mathf.Max(secondWindThreshold, threshold));
        secondWindSeconds = Mathf.Max(secondWindSeconds, seconds);
        secondWindMend = Mathf.Max(secondWindMend, mend);
    }

    public float SecondWindSeconds => secondWindSeconds;
    public int SecondWindMend => secondWindMend;

    /// <summary>
    /// True exactly once per wave, and only when the knight has actually been
    /// driven to the brink. Spends the charge as it answers, so the caller can
    /// treat a true return as "the light has already been committed".
    /// </summary>
    public bool TrySpendSecondWind(int currentHealth, int maxHealth)
    {
        if (secondWindSeconds <= 0f || secondWindSpent || maxHealth <= 0) return false;
        if (currentHealth > secondWindThreshold * maxHealth) return false;

        secondWindSpent = true;
        PlayerStats.Increment("dawn.second_wind");
        return true;
    }

    /// <summary>
    /// Rearms Second Wind on both knights. Called from Spawner.BeginWave, which
    /// is the single funnel every wave start passes through (including the dev
    /// wave picker), so no path can start a wave with a stale charge.
    /// </summary>
    public static void OnWaveStarted()
    {
        RearmSecondWind("PlayerLeft");
        RearmSecondWind("PlayerRight");
    }

    private static void RearmSecondWind(string knightTag)
    {
        GameObject knight = GameObject.FindWithTag(knightTag);
        if (knight == null) return;
        DawnBoost boost = knight.GetComponent<DawnBoost>();
        if (boost != null) boost.secondWindSpent = false;
    }

    // ---- Benediction ----

    public void SetBenediction(int mend, float invulnSeconds)
    {
        benedictionMend = Mathf.Max(benedictionMend, mend);
        benedictionInvuln = Mathf.Max(benedictionInvuln, invulnSeconds);
    }

    public bool HasBenediction => benedictionMend > 0;

    /// <summary>
    /// Spending a special lifts the OTHER knight, whatever the special was —
    /// Rapid Fire, Field Mending, anything a later loadout adds. Hooked once in
    /// PlayerSpecial rather than inside each special, so a new special cannot
    /// quietly escape the rule.
    /// </summary>
    public void PayBenediction(string ownTag)
    {
        if (benedictionMend <= 0) return;

        GameObject other = OtherKnight(ownTag);
        if (other == null) return;

        other.GetComponent<PlayerHealth>()?.Heal(benedictionMend);
        other.GetComponent<GlowManager>()?.StartGlow(DawnGlow, 1f);

        if (benedictionInvuln > 0f)
        {
            other.GetComponent<PlayerHealth>()?.SetInvulnerable(benedictionInvuln);
            GetComponent<PlayerHealth>()?.SetInvulnerable(benedictionInvuln);
        }

        PlayerStats.Increment("dawn.benediction");
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
