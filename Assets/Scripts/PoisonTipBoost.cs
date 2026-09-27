using UnityEngine;

// The knight's poison stat sheet: every Serpent upgrade writes into this component,
// and PlayerShooter / EnemyBase read from it when arrows fire and poisoned enemies die.
public class PoisonTipBoost : MonoBehaviour
{
    private float poisonChance = 0f; // Percentage chance (0-100)

    // Venom Tip's trail: how long a bead shed by a poisoned arrow keeps working.
    // 0 means the knight has no Venom Tip yet and therefore no trail. Ranks set it
    // outright (1 / 3 / 5) rather than adding, so the asset states the real
    // number and a re-pick cannot compound it past the top of the ladder.
    private float trailBubbleSeconds = 0f;
    private int tickDamageBonus = 0; // Equipment: added to each poison tick
    private int miasmaLevel = 0; // Miasma: 0 = off, 1-2 = death-cloud size/duration
    private bool plaguebringer = false; // Serpent + Guardian combo: this knight's poison clouds hunt mobs (see PoisonCloud)

    // Acid Dagger (Serpent capstone): a jab follows every sword swing. The damage and
    // the sprite come off the upgrade asset; the timing and reach live in SwordSwing,
    // which is what throws it.
    private bool acidDagger = false;
    private int acidDaggerDamage = 0;
    private Sprite acidDaggerSprite;

    // Serpent's Breath: EVERY sword swing exhales venom along the shield facing.
    // Not a roll any more (owner, 2026-09-17) - the swing is the cadence, so what
    // a rank buys is what comes out of it rather than how often anything does.
    private int breathTier = 0;
    private int breathBeads = 0;         // 0 = the knight has no Serpent's Breath yet
    private bool breathCloud = false;    // rank III sends a cloud out with the beads
    private bool breathLargeCloud = false;
    private float breathSecondsMin = 0f;
    private float breathSecondsMax = 0f;
    private int breathStep = 0;          // walks the lifetime band, see NextBreathSeconds

    // Airborne Virus: every enemy this knight has poisoned breathes venom beads
    // out around itself on a clock. 0 bubbles = the knight has no rank yet.
    private int airborneTier = 0;
    private int airborneBubbles = 0;
    private float airborneInterval = 0f;
    private float airborneStartDegrees = 0f; // where the ring's first bead points

    public const int MaxMiasmaLevel = 2;

    public void IncreasePoisonChance(float amount)
    {
        poisonChance = Mathf.Clamp(poisonChance + amount, 0f, 100f);
    }

    public float GetPoisonChance()
    {
        return poisonChance;
    }

    /// <summary>
    /// Set by each Venom Tip rank. Max rather than assignment for the same reason
    /// every other tier here uses it: the draft can offer a rank the knight
    /// already has, and picking it again must never be a downgrade.
    /// </summary>
    public void SetTrailBubbleSeconds(float seconds)
    {
        trailBubbleSeconds = Mathf.Max(trailBubbleSeconds, seconds);
    }

    public float TrailBubbleSeconds => trailBubbleSeconds;

    // Check if this shot should be poisoned based on chance
    public bool ShouldApplyPoison()
    {
        return Random.Range(0f, 100f) < poisonChance;
    }

    public void AddTickDamage(int amount)
    {
        tickDamageBonus += amount;
    }

    public void IncreaseMiasmaLevel()
    {
        miasmaLevel = Mathf.Min(miasmaLevel + 1, MaxMiasmaLevel);
    }

    public void EnablePlaguebringer()
    {
        plaguebringer = true;
    }

    public void EnableAcidDagger(int damage, Sprite sprite)
    {
        acidDagger = true;
        acidDaggerDamage = Mathf.Max(acidDaggerDamage, damage);
        if (sprite != null) acidDaggerSprite = sprite;
    }

    // The rank III cloud's lifetime. Was 3s, the same as Miasma's first level;
    // raised to 8s on 2026-09-25 (owner).
    public const float BreathCloudSeconds = 8f;

    /// <summary>
    /// Each rank states the whole exhale rather than adding to it, the same way
    /// Vial Throw's ranks do, so an asset says what the upgrade DOES and a rank
    /// the draft re-offers cannot stack a fourth bead nobody authored. Ranks only
    /// move forward: a lower tier arriving after a higher one is ignored.
    ///
    ///   I   one venom bead down the facing, 4.5s
    ///   II  three beads in a narrow fan, 4.5s
    ///   III the same three beads at 6s, and an 8s venom cloud out with them
    ///
    /// Bead lifetimes were bands until 2026-09-25 (last 3.5-5.5 / 5.5-6.5 /
    /// 7.5-9.5s); the owner set them flat that day, and on 2026-09-26 gave ranks
    /// I and II 1.5s more (3s to 4.5s) and rank III 6s (was 4s).
    ///
    /// Two dials move across the ranks and they do different jobs. I to II is a
    /// change of SHAPE - one bead becomes a fan, which is three separate catches
    /// on three separate bodies rather than a thicker one, because a bead is spent
    /// on the first thing it touches. II to III leaves the shape alone and adds a
    /// cloud down the middle of it: a cloud is not spent, it keeps working on
    /// everything that walks through it, so the last rank is what turns the fan
    /// from a net you throw into a net with a standing area inside it.
    /// </summary>
    public void SetBreathTier(int tier)
    {
        if (tier <= breathTier) return;
        breathTier = tier;

        if (tier >= 3)
        {
            breathBeads = 3;
            breathCloud = true;
            breathLargeCloud = true;
            breathSecondsMin = 6f;
            breathSecondsMax = 6f;
        }
        else if (tier == 2)
        {
            breathBeads = 3;
            breathCloud = false;
            breathSecondsMin = 4.5f;
            breathSecondsMax = 4.5f;
        }
        else
        {
            breathBeads = 1;
            breathCloud = false;
            breathSecondsMin = 4.5f;
            breathSecondsMax = 4.5f;
        }
    }

    /// <summary>
    /// How long the next BEAD should last. Three steps across the rank's band,
    /// taken in turn and never rolled - the same refusal PoisonTrailBubble makes
    /// when it alternates a bead's lean instead of randomising it.
    ///
    /// One mechanism covers both shapes the ranks come in: a rank that throws three
    /// at once gets short/middle/long inside a single volley, and rank I, which
    /// throws one, walks the band across consecutive swings instead. Every rank's
    /// band is currently a single value, so each bead gets exactly that.
    ///
    /// The cloud does not come through here - it runs for BreathCloudSeconds flat.
    /// </summary>
    public float NextBreathSeconds()
    {
        if (breathSecondsMax <= breathSecondsMin) return breathSecondsMin;

        float t = (breathStep % 3) / 2f;
        breathStep++;
        return Mathf.Lerp(breathSecondsMin, breathSecondsMax, t);
    }

    /// <summary>
    /// Each rank states the whole release, the same way SetBreathTier does, and
    /// ranks only move forward. The beads go out evenly spaced round the enemy,
    /// the first one pointing at airborneStartDegrees (0 = right, 90 = up):
    ///
    ///   I   2 beads every 6s - left and right
    ///   II  5 beads every 5s - a pentagon, point up
    ///   III 8 beads every 4s - a ring that keeps I's left and right
    ///   IV  10 beads every 2s - a ring that keeps II's pentagon
    /// </summary>
    public void SetAirborneTier(int tier)
    {
        if (tier <= airborneTier) return;
        airborneTier = tier;

        if (tier >= 4)
        {
            airborneBubbles = 10;
            airborneInterval = 2f;
            airborneStartDegrees = 90f;
        }
        else if (tier == 3)
        {
            airborneBubbles = 8;
            airborneInterval = 4f;
            airborneStartDegrees = 0f;
        }
        else if (tier == 2)
        {
            airborneBubbles = 5;
            airborneInterval = 5f;
            airborneStartDegrees = 90f;
        }
        else
        {
            airborneBubbles = 2;
            airborneInterval = 6f;
            airborneStartDegrees = 0f;
        }
    }

    public int TickDamageBonus => tickDamageBonus;
    public int MiasmaLevel => miasmaLevel;
    public bool Plaguebringer => plaguebringer;
    public bool AcidDagger => acidDagger;
    public int AcidDaggerDamage => acidDaggerDamage;
    public Sprite AcidDaggerSprite => acidDaggerSprite;
    public int BreathBeadCount => breathBeads;
    public bool BreathSendsCloud => breathCloud;
    public bool BreathLargeCloud => breathLargeCloud;
    public int AirborneTier => airborneTier;
    public int AirborneBubbleCount => airborneBubbles;
    public float AirborneIntervalSeconds => airborneInterval;
    public float AirborneStartDegrees => airborneStartDegrees;
}
