using UnityEngine;

// The Guardian Order's per-knight stat sheet, mirroring FrigidBoost / EmberBoost /
// DawnBoost / NinjaBoost. Every Guardian upgrade that isn't pure shield geometry
// writes into this and nothing else stores Guardian state; the combat systems read it.
//
// (Greatshield and Bowsight are the two exceptions and always will be: they buy the
// SHAPE of the guard and the line of the shot, so they live on the shield GameObject
// as ShieldShape and ShieldSight. This sheet is for everything that is a number.)
//
// THE PILLAR, in two parts (see Docs/Design/guardian-order.md):
//
//   1. Guardian never asks the knight to aim better. Every other Order buys a
//      bigger number on a shot the player already had to land; Guardian buys the
//      LANDING. The guard is wider, the arrow bends, the rock comes back. That is
//      why it is the Order you can draft without knowing you have drafted it.
//   2. Nothing here rewards being hit. A block is a mistake the guard covered for,
//      not an achievement - so the Order pays out for what the guard SENDS BACK,
//      never for the fact that it was struck. Stalwart (blocks charge the special)
//      was designed and cut for exactly this reason; don't put it back.
//
// The capstone, Bulwark, is pillar 2 taken to its end: a body that reaches the
// guard stops costing health at all and is simply shoved off it.
public class GuardianBoost : MonoBehaviour
{
    // ---- tuning knobs, all in one place so the balance pass is one file ----

    // Reflect speed used to be one const for both ranks. It is now the Reflector
    // chain's headline number, and it drives TWO things off the same figure: how
    // hard a rock leaves the guard, and how fast this knight's own arrows fly.
    // That is the whole point of putting projectile speed here — the card can say
    // "x2.5" once and the player can see it in both places.
    private const float ReflectSpeedRank1 = 1.5f;
    private const float ReflectSpeedRank2 = 2.5f;

    /// <summary>How much faster a reflected rock flies than it arrived, AND what
    /// this knight's arrows are multiplied by. Rank II keeps the 2.5 the rebound
    /// has always left at, so the Order's signature does not change feel — rank I
    /// is the new, gentler step in front of it.</summary>
    public float ReflectSpeedMultiplier
    {
        get
        {
            if (reflectorLevel >= 2) return ReflectSpeedRank2;
            if (reflectorLevel == 1) return ReflectSpeedRank1;
            return 1f;
        }
    }

    /// <summary>A reflected rock is destroyed this long after the block, whatever it
    /// hit or didn't.
    ///
    /// NOT optional and not a tidiness measure. A wave is not complete while any
    /// tracked projectile is alive (BaseWave.IsWaveComplete), and every rock in the
    /// game up to now has been guaranteed to die because it always ended on a knight
    /// or on a guard. A reflected one can fly into open sky forever, and without this
    /// the wave would never end.</summary>
    public const float ReflectedLifetimeSeconds = 4f;

    /// <summary>How hard a guided shot turns, in degrees per second. High on purpose:
    /// the whole promise of the chain is that once the arrow commits it connects, so
    /// a tier buys HOW EARLY it commits (the radius) and never whether it lands.</summary>
    public const float GuidedTurnDegreesPerSecond = 720f;

    /// <summary>Guided targets are re-acquired on a beat rather than every frame -
    /// one overlap query per arrow per tenth of a second, not sixty. Steering itself
    /// runs every frame; it is the SEARCH that is throttled.</summary>
    public const float GuidedSearchInterval = 0.1f;

    /// <summary>How far Bulwark throws a body that reaches the guard.</summary>
    public const float BulwarkShoveDistance = 2f;

    /// <summary>How long that shove takes. Short enough to read as a shield bash,
    /// long enough not to look like the enemy teleported.</summary>
    public const float BulwarkShoveSeconds = 0.18f;

    // ---- owned ranks ----
    private int reflectorLevel;
    private int guidedShotLevel;
    private int guidedReflectionsLevel;
    private bool bulwark;

    // Setters are monotonic, the house rule: a re-pick or an out-of-order draft can
    // never walk a knight's guard backwards.
    public void SetReflector(int level) { reflectorLevel = Mathf.Max(reflectorLevel, level); }
    public void SetGuidedShot(int level) { guidedShotLevel = Mathf.Max(guidedShotLevel, level); }
    public void SetGuidedReflections(int level) { guidedReflectionsLevel = Mathf.Max(guidedReflectionsLevel, level); }
    public void SetBulwark() { bulwark = true; }

    // Equipment multiplies the rank's number rather than inflating the rank, the
    // same separation FrigidBoost keeps for chill duration: rank also decides the
    // CHANCE, so an item that bought a rank to double the damage would quietly hand
    // over the reliability as well.
    private float reflectDamageBonus = 1f;
    public void MultiplyReflectDamage(float factor) { reflectDamageBonus *= Mathf.Max(1f, factor); }

    public int ReflectorLevel { get { return reflectorLevel; } }
    public int GuidedShotLevel { get { return guidedShotLevel; } }
    public int GuidedReflectionsLevel { get { return guidedReflectionsLevel; } }

    /// <summary>Bulwark: a body that reaches this knight's guard is shoved off it
    /// instead of trading the knight health for its removal.</summary>
    public bool HasBulwark { get { return bulwark; } }

    /// <summary>The chance a block turns the incoming ammunition around, as a
    /// percentage.
    ///
    /// The last tier is certainty rather than a better coin, so a finished chain is a
    /// promise the player can build around.
    ///
    /// TWO TIERS, 50 then 100 (owner's call, 2026-09-09). It shipped as a three-tier
    /// 30/60/100 and both halves of that were wrong. Thirty percent made rank one a
    /// thing that occasionally happened rather than a thing to play around, and the
    /// chain's whole selling point is that the guard becomes an ANSWER — you cannot
    /// aim a return line you only get one block in three. A coin flip is the floor at
    /// which a player starts angling the shield on purpose, which is when the Order
    /// turns on. And the middle tier was then a step from "usually" to "usually", so
    /// it was deleted outright rather than retuned: the chain now reads maybe, then
    /// always, and the second pick is a Legendary because certainty is what it sells.
    ///
    /// Ranks above 2 are answered as 2. Nothing grants one any more — the third asset
    /// became rank II — but the setters are monotonic by house rule and a save from
    /// before the change must not read as no Reflector at all.</summary>
    public float ReflectChance
    {
        get
        {
            if (reflectorLevel >= 2) return 100f;
            if (reflectorLevel == 1) return 50f;
            return 0f;
        }
    }

    /// <summary>What a reflected rock's own damage is multiplied by. The rock keeps
    /// its authored payload - a plain stone is worth an arrow, a powder rock is worth
    /// two and a half - and the chain multiplies whatever that was, so the mine's own
    /// escalation is what makes the Order scale rather than a number written here.</summary>
    public float ReflectDamageMultiplier
    {
        get
        {
            // Zero stays zero: an item that doubles what a rebound hits for is worth
            // nothing to a knight whose guard cannot send one back, and it must not
            // quietly become a reflect of its own.
            if (reflectorLevel >= 2) return 2.5f * reflectDamageBonus;
            if (reflectorLevel == 1) return 2f * reflectDamageBonus;
            return 0f;
        }
    }

    public bool CanReflect { get { return reflectorLevel > 0; } }

    /// <summary>Rolls one block. Certainty at rank III is answered without touching
    /// the dice, so a full-rank knight's guard is genuinely deterministic.</summary>
    public bool ShouldReflect()
    {
        float chance = ReflectChance;
        if (chance <= 0f) return false;
        if (chance >= 100f) return true;
        return Random.Range(0f, 100f) < chance;
    }

    /// <summary>How close a mob or an orb has to be before this knight's arrow bends
    /// onto it. Zero while the knight has no Guided Shot, which is what keeps the
    /// steering component off every arrow in a run with no Guardian picks.
    ///
    /// Widened on 2026-09-25 (owner's call): rank I by 15%, ranks II and III by
    /// 10%, from 0.5 / 1.0 / 1.6. Guided Reflections keeps the old radii.</summary>
    public float GuidedShotRadius
    {
        get
        {
            if (guidedShotLevel >= 3) return 1.76f;
            if (guidedShotLevel == 2) return 1.1f;
            if (guidedShotLevel == 1) return 0.575f;
            return 0f;
        }
    }

    /// <summary>The radii for the rock coming back. Deliberately
    /// a separate chain rather than a rider on Guided Shot: the arrow and the rebound
    /// are two different shots and the player pays for each one they want steered.</summary>
    public float GuidedReflectionRadius
    {
        get
        {
            if (guidedReflectionsLevel >= 3) return 1.6f;
            if (guidedReflectionsLevel == 2) return 1f;
            if (guidedReflectionsLevel == 1) return 0.5f;
            return 0f;
        }
    }

    /// <summary>The sheet on the knight wearing the given tag, or null. Mirrors
    /// FrigidBoost.FindKnightBoost so the reflect path can resolve an owner from a
    /// tag without holding a reference.</summary>
    public static GuardianBoost FindKnightBoost(string knightTag)
    {
        if (string.IsNullOrEmpty(knightTag)) return null;
        GameObject knight = GameObject.FindWithTag(knightTag);
        return knight != null ? knight.GetComponent<GuardianBoost>() : null;
    }
}
