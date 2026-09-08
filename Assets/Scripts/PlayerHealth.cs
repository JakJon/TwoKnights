using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;
    [SerializeField] private HealthBar healthBar;

    // The same red pulse every enemy flashes when it is hit. A knight taking a
    // hit was previously the one damage event in the game with no colour on it —
    // you heard the grunt and watched the bar move, which is easy to miss with
    // your eyes on the far side of the board.
    private GlowManager glowManager;

    // The same floating number every enemy prints, for the same reason the red
    // pulse above is shared: a hit on a knight was the one damage event in the
    // game that never said HOW MUCH. With the number now sized by the damage
    // (see DamageText), a graze and a powder rock are told apart at a glance
    // from the far side of the board, which is the whole point of putting it on
    // the knights as well.
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private Vector3 damageTextOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private float damageTextStackSeparation = 0.25f;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        glowManager = GetComponent<GlowManager>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        healthBar.Initialize(maxHealth);
        healthBar.SetValue(currentHealth);
    }

    // Read-only accessors for UI/status
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    // Mirrors EnemyBase.ShowDamageText: stacked above the sprite, parented so
    // later hits can find the earlier ones and nudge them up.
    private void ShowDamageText(int damage, Color textColor)
    {
        if (damageTextPrefab == null || damage <= 0) return;

        float spriteHeight = spriteRenderer != null ? spriteRenderer.bounds.size.y : 1f;
        Vector3 spawnPosition = transform.position + damageTextOffset + new Vector3(0f, spriteHeight, 0f);

        foreach (var existing in GetComponentsInChildren<DamageText>(false))
        {
            existing.PushUp(damageTextStackSeparation);
        }

        GameObject go = Instantiate(damageTextPrefab, spawnPosition, Quaternion.identity);
        go.transform.SetParent(transform);
        go.GetComponent<DamageText>()?.Initialize(damage, textColor);
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, null);
    }

    public void TakeDamage(int damage, string sourceName)
    {
        TakeDamage(damage, sourceName, Color.red, 0.3f);
    }

    /// <summary>
    /// Standard flash, but says what KIND of harm this was so equipment can
    /// answer it. Blasts use this.
    /// </summary>
    public void TakeDamage(int damage, string sourceName, DamageKind kind)
    {
        TakeDamage(damage, sourceName, Color.red, 0.3f, null, kind);
    }

    // Iron Vigil (and anything else granting an untouchable window) sets this
    // deadline. A deadline rather than a flag so the window always expires, even
    // if the coroutine that opened it is killed early — the same reasoning as
    // PlayerSpecial._gainFrozenUntil.
    private float _invulnerableUntil = -1f;
    public bool IsInvulnerable => Time.time < _invulnerableUntil;

    /// <summary>
    /// A standing, untimed block on all damage. Deliberately separate from the
    /// timed <see cref="IsInvulnerable"/> window: that one is bought by upgrades
    /// and announces itself with a halo, where this is scaffolding — the tutorial
    /// holds it up so a knight learning to block cannot be killed by the lesson.
    /// </summary>
    public bool Untouchable { get; set; }

    /// <summary>
    /// Raised whenever damage is aimed at a knight, BEFORE any invulnerability is
    /// consulted — so it fires even for a hit that lands on nothing. That is the
    /// point: the tutorial needs to know the rock got through while the knight it
    /// hit is untouchable, and "did it get through" is a different question from
    /// "did it hurt". Carries the knight and the name of what hit it.
    /// </summary>
    public static event System.Action<PlayerHealth, string> OnDamageAttempt;

    /// <summary>
    /// Blocks all incoming damage for <paramref name="seconds"/>. Overlapping
    /// calls extend the window rather than cutting it short.
    /// </summary>
    public void SetInvulnerable(float seconds)
    {
        _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + seconds);

        // Being untouchable is one thing to the player however it was bought —
        // Second Wind, Benediction, Last Light, Iron Vigil — so it gets one
        // look. Sized from the REMAINING window, not the requested seconds, so
        // an overlapping call extends the halo instead of cutting it short.
        DawnFx.ShowInvulnerableAura(gameObject, _invulnerableUntil - Time.time);
    }

    /// <summary>
    /// Damage with the flash and the voice spelled out. The streak reset and the
    /// death path stay identical whatever is passed, because a knight being hurt
    /// has to be ONE event however it happened — this is still the single funnel,
    /// it just lets a source that repeats (KnightPoison, 25 ticks) sound like
    /// itself instead of like twenty-five arrows. A null <paramref name="sound"/>
    /// gets the standard hurt cry, which is what every other caller wants.
    /// </summary>
    public void TakeDamage(int damage, string sourceName, Color flashColor, float flashSeconds,
                           SoundEffect sound = null, DamageKind kind = DamageKind.Generic)
    {
        if (damage > 0) OnDamageAttempt?.Invoke(this, sourceName);

        // An earned invulnerability window: no damage, no flash, no cry — and
        // critically no streak reset, because nothing actually got through
        if (damage > 0 && IsInvulnerable) return;

        // Untouchable is scaffolding, not a shield the player earned, so it stops
        // the damage but NOT the cry. In the tutorial that grunt is the only thing
        // saying the rock got through and the lesson is about to come round again;
        // silence would read as a clean block.
        if (damage > 0 && Untouchable)
        {
            PlayHurtVoice(sound);
            return;
        }

        // Equipment that answers one kind of harm scales it here rather than at
        // each source, so a new keg or bomb can't quietly escape the rule.
        // Rounded up so a protected hit still stings for at least 1.
        if (damage > 0 && kind == DamageKind.Blast)
        {
            var boost = GetComponent<EquipmentBoost>();
            if (boost != null && boost.BlastDamageMultiplier < 1f)
            {
                damage = Mathf.Max(1, Mathf.CeilToInt(damage * boost.BlastDamageMultiplier));
            }
        }

        // The one unified knight-damage sound: every damage source funnels
        // through here, so nothing else should play its own hit feedback
        if (damage > 0)
        {
            PlayHurtVoice(sound);
            // Matches EnemyBase.TakeDamage exactly, so "red pulse" means the same
            // thing whoever it happened to
            glowManager?.StartGlow(flashColor, flashSeconds);
            // Same again for the number: the knight's hit is written the way an
            // enemy's is, in the colour the hit already flashed.
            ShowDamageText(damage, flashColor);
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);

        // Reported as it happens, not read at the wave's end: a knight can be
        // driven to a sliver and healed back inside one second, and that is
        // exactly the moment the Dawn feat is about.
        DawnVigil.NoteHealth(currentHealth, maxHealth);
        healthBar.SetValue(currentHealth);

        // reset special
        PlayerSpecial playerSpecial = GetComponent<PlayerSpecial>();
        if (playerSpecial != null)
        {
            playerSpecial.ResetSpecialStreak();
        }

        // Second Wind (Dawn) answers the blow that settled the toll — after the
        // hit lands, never instead of it, so the moment stays legible
        if (damage > 0 && currentHealth > 0)
        {
            TrySecondWind(damage);
        }

        if (currentHealth <= 0)
        {
            // Last Light (Dawn capstone): the other knight's vigil catches this
            // one before the run can end
            if (TryLastLightRescue()) return;

            // Player has died - trigger scene transition to camp
            if (GameSceneManager.Instance != null)
            {
                GameSceneManager.Instance.OnPlayerDeath(KnightDisplayNameRich, sourceName);
            }
            else
            {
                // Fallback behavior if no scene manager exists
                Debug.LogWarning("GameSceneManager not found! Stopping game instead.");
                Time.timeScale = 0;
            }
        }
    }

    private string KnightDisplayName
    {
        get
        {
            if (CompareTag("PlayerLeft")) return KnightNames.LeftPlain;
            if (CompareTag("PlayerRight")) return KnightNames.RightPlain;
            return "A Knight";
        }
    }

    // Same name wearing its knight's colour, for anywhere it lands in a label
    // rather than a log line.
    private string KnightDisplayNameRich
    {
        get
        {
            if (CompareTag("PlayerLeft")) return KnightNames.Left;
            if (CompareTag("PlayerRight")) return KnightNames.Right;
            return "A Knight";
        }
    }

    // A null sound gets the standard hurt cry, which is what every caller but a
    // repeating source (KnightPoison) wants.
    private void PlayHurtVoice(SoundEffect sound)
    {
        if (AudioManager.Instance == null) return;
        SoundEffect voice = sound != null && sound.clip != null
            ? sound
            : AudioManager.Instance.playerHurt;
        AudioManager.Instance.PlaySFX(voice);
    }

    public void Heal(int amount)
    {
        Heal(amount, true, HealSource.Generic);
    }

    public void Heal(int amount, bool allowEcho)
    {
        Heal(amount, allowEcho, HealSource.Generic);
    }

    /// <summary>
    /// The single healing funnel — orbs, specials, Lifebloom, equipment, all of
    /// it. <paramref name="allowEcho"/> is false only for Shared Light's own
    /// echo, so two Dawn knights lift each other once rather than bouncing one
    /// heal between them forever.
    /// </summary>
    public void Heal(int amount, bool allowEcho, HealSource source)
    {
        if (amount <= 0) return;

        int missing = maxHealth - currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        healthBar.SetValue(currentHealth);
        CurePoison();

        // The holy light and its chime, fired from the one funnel every heal
        // passes through rather than from each Dawn effect — a knight who has
        // bought into the Order glows whenever they are healed, and no effect
        // added later can forget to ask. Non-Dawn knights heal silently and
        // unlit, exactly as before.
        //
        // An ORB is the one exception, and only for the sound's TIMING: it
        // already announces itself with orbCollect, and firing the chime on the
        // same frame fused the two into one noise you could not pick apart — so
        // the Sunwell payoff went unheard. Held back a beat instead, the player
        // hears the orb, then hears the light answer it.
        if (GetComponent<DawnBoost>() != null)
        {
            DawnFx.Blessing(gameObject);
            if (source == HealSource.Orb) StartCoroutine(BlessingChimeAfter(OrbChimeDelay));
            else PlayBlessingChime();
        }

        if (allowEcho) EchoSharedLight(amount, amount - Mathf.Min(amount, missing), source);
    }

    /// <summary>
    /// One shared soft chime for every Dawn effect, deliberately not one sound
    /// per discipline: the player should learn a single "the light answered"
    /// cue, not five. Guarded so a burst of heals in the same frame (a rescue
    /// that also echoes) can't stack into a chord — AudioManager runs one
    /// AudioSource and overlapping copies turn to mud.
    /// </summary>
    private static float _lastBlessingChime = -1f;
    private const float BlessingChimeCooldown = 0.12f;

    private static void PlayBlessingChime()
    {
        if (Time.unscaledTime - _lastBlessingChime < BlessingChimeCooldown) return;
        _lastBlessingChime = Time.unscaledTime;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(AudioManager.Instance.dawnBlessing);
    }

    /// <summary>
    /// Silence left between the end of orbCollect and the chime, so the two read
    /// as two events rather than one thick one.
    /// </summary>
    private const float BlessingChimeGapAfterOrb = 0.12f;

    /// <summary>
    /// Measured off the orb pickup clip rather than hardcoded, so replacing that
    /// sound with a longer one pushes the chime out ahead of it instead of
    /// sliding it back underneath.
    /// </summary>
    private static float OrbChimeDelay
    {
        get
        {
            AudioManager audio = AudioManager.Instance;
            float pickup = audio != null && audio.orbCollect != null && audio.orbCollect.clip != null
                ? audio.orbCollect.clip.length
                : 0f;
            return pickup + BlessingChimeGapAfterOrb;
        }
    }

    // Unscaled, so the chime still lands on the intended beat if something has
    // slowed time — the cue is feedback about a pickup, not part of the sim
    private IEnumerator BlessingChimeAfter(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        PlayBlessingChime();
    }

    /// <summary>
    /// Shared Light (Dawn Order): a share of every heal this knight receives is
    /// passed to the other one. At rank II whatever would have been WASTED over
    /// full health goes across whole instead of evaporating — the half of the
    /// Dawn pillar that says the Order never throws healing away.
    /// </summary>
    private void EchoSharedLight(int amount, int wasted, HealSource source)
    {
        DawnBoost dawn = GetComponent<DawnBoost>();
        if (dawn == null) return;

        int echo = Mathf.RoundToInt(amount * dawn.EchoFraction);
        // Routed overflow REPLACES the fractional echo rather than stacking with
        // it: the promise is "the whole amount goes across", not "the whole
        // amount plus a share of it on top"
        if (dawn.RoutesOverheal && wasted > echo) echo = wasted;
        if (echo <= 0) return;

        GameObject other = DawnBoost.OtherKnight(tag);
        if (other == null) return;

        PlayerHealth otherHealth = other.GetComponent<PlayerHealth>();
        if (otherHealth == null) return;

        // The echo carries the SOURCE across too: an orb that heals both knights
        // has to delay both chimes, or the partner's fires on the pickup frame
        // and puts the noise straight back
        otherHealth.Heal(echo, false, source);
        other.GetComponent<GlowManager>()?.StartGlow(DawnBoost.DawnGlow, 0.4f);
        PlayerStats.Increment("dawn.shared_light");
    }

    /// <summary>
    /// Second Wind (Dawn): every so many points of damage this knight has
    /// absorbed, the light holds them up for a beat. The counter is the
    /// DawnBoost's and runs for the whole map, so the toll is paid in harm
    /// taken rather than in how close to death it left them.
    /// </summary>
    private void TrySecondWind(int damage)
    {
        DawnBoost dawn = GetComponent<DawnBoost>();
        if (dawn == null || !dawn.TrySpendSecondWind(damage)) return;

        SetInvulnerable(dawn.SecondWindSeconds);
        glowManager?.StartGlow(DawnBoost.DawnGlow, dawn.SecondWindSeconds);
        if (dawn.SecondWindHeal > 0) Heal(dawn.SecondWindHeal);
    }

    /// <summary>
    /// Last Light (Dawn capstone): read from the DYING knight's side, but owned
    /// by the other one — the vigil is something a knight keeps over their
    /// partner, never over themselves. Returns true if the death was averted.
    /// </summary>
    private bool TryLastLightRescue()
    {
        GameObject partner = DawnBoost.OtherKnight(tag);
        if (partner == null) return false;

        DawnBoost vigil = partner.GetComponent<DawnBoost>();
        if (vigil == null || !vigil.TrySpendLastLight()) return false;

        currentHealth = 1;
        healthBar.SetValue(currentHealth);

        // Heal AFTER the floor is set so the heal (and its Shared Light echo)
        // reads off a living knight
        Heal(DawnBoost.LastLightHeal);

        SetInvulnerable(DawnBoost.LastLightInvulnSeconds);
        partner.GetComponent<PlayerHealth>()?.SetInvulnerable(DawnBoost.LastLightInvulnSeconds);

        glowManager?.StartGlow(DawnBoost.DawnGlow, DawnBoost.LastLightInvulnSeconds);
        partner.GetComponent<GlowManager>()?.StartGlow(DawnBoost.DawnGlow, DawnBoost.LastLightInvulnSeconds);

        // No sound of its own: the Heal above already rang the shared blessing
        // chime, and a second cue on the same beat would just crowd it

        Debug.Log($"Last Light: {KnightDisplayName} was caught by the other knight's vigil");
        return true;
    }

    public void IncreaseMaxHealth(int amount)
    {
        maxHealth += amount;
        currentHealth += amount; // Also heal the player by the same amount
        healthBar.Initialize(maxHealth);
        healthBar.SetValue(currentHealth);
        CurePoison();
    }

    // Healing "any way at all" clears poison — an orb, the heal special, a max
    // health upgrade. Funnelled through the two methods above rather than left to
    // each caller, so a heal added later cannot quietly forget to do it. See
    // KnightPoison for why the cure is total rather than partial.
    //
    // It clears it on BOTH knights, not just the healed one: the healing is a
    // clean slate for the pair. Reaching across via OtherKnight rather than a
    // scene-wide sweep, same as every other paired effect here, and no recursion
    // risk — Cure only stops a coroutine, it never heals back.
    private void CurePoison()
    {
        GetComponent<KnightPoison>()?.Cure();
        DawnBoost.OtherKnight(tag)?.GetComponent<KnightPoison>()?.Cure();
    }

    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    public int GetMaxHealth()
    {
        return maxHealth;
    }
}