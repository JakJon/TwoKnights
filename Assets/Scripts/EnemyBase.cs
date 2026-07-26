using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public abstract class EnemyBase : MonoBehaviour, IHasAttributes
{
    [Header("Health")]
    [SerializeField] protected float health = 10f;

    [Header("Special Rewards")]
    [SerializeField] protected int specialOnHit = 5; // Points for damage
    [SerializeField] protected int specialOnDeath = 10; // Points for death

    [Header("Gold")]
    [SerializeField] protected int goldOnDeath = 1;

    [Header("Collision Damage")]
    [SerializeField] protected int shieldDamage = 10; // Damage to shield
    protected int playerDamage = 20; // Damage to player

    [Header("Stagger")]
    [SerializeField] protected float staggerDuration = 0.2f; // Stun duration
    [SerializeField] protected AnimationClip staggerAnimation; // Stagger anim
    [SerializeField] protected AnimationClip defaultAnimation; // Default anim

    [Header("Damage Text")]
    [SerializeField] protected GameObject damageTextPrefab; // Floating text prefab
    [SerializeField] protected Vector3 damageTextOffset = new Vector3(0, 0.01f, 0); // Text offset
    [SerializeField] protected float damageTextStackSeparation = 0.25f; // Vertical spacing between stacked texts

    [Header("Audio")]
    [SerializeField] protected SoundEffect hurtSound;
    [SerializeField] protected SoundEffect deathSound;

    [Header("Enemy Attributes")]
    [SerializeField] protected EnemyType attributes;
    [Tooltip("Name shown on the death screen. Leave empty to derive from the class name (EnemyRatKing -> Rat King).")]
    [SerializeField] protected string displayName = "";

    [Header("Sprite Flipping")]
    [SerializeField] protected float directionThreshold = 0.01f; 
    
    protected SpriteRenderer spriteRenderer;
    protected GlowManager glowManager;
    protected Animator animator;
    // Death guard to avoid double-kill/race conditions
    protected bool isDead = false;
    // Poison system
    protected bool isPoisoned = false;
    protected float poisonTimer = 0f;
    protected int poisonDamage = 0;
    protected float poisonTickRate = 1f;
    protected float lastPoisonTick = 0f;
    protected Coroutine poisonCoroutine = null;
    protected PoisonBubbleEffect poisonBubbles = null; // Poison bubble effect
    
    // Track poison sources and their contributions
    protected List<PoisonSource> poisonSources = new List<PoisonSource>();
    
    [System.Serializable]
    public class PoisonSource
    {
        public string playerTag; // Store player tag instead of projectile reference
        public int damageContribution; //
        
        public PoisonSource(string playerTag, int damage)
        {
            this.playerTag = playerTag;
            damageContribution = damage;
        }
    }
    
    public bool IsPoisoned => isPoisoned;
    public bool IsDead => isDead;

    // ---- Ember Order: ignition ----
    // An ignited enemy is on fire: it takes the standardized fire dps directly (so
    // an Ignited-Tips arrow deals damage on its own, no ground fire required), AND
    // it becomes a walking torch that drips fire trails and panics. Only a fireball
    // or an arrow that carries ignite may call Ignite() — the ignition pillar. See
    // Docs/Design/ember-order.md.
    //
    // Burns STACK: every Ignite() adds an independent burn — its own dps (the igniting
    // knight's zone dps) on its own 8s timer. Total on-body dps is the sum of live
    // stacks, so re-igniting a burning enemy piles heat on; as each timer runs out the
    // dps steps back down, like a real fire dying in stages.
    private struct BurnStack
    {
        public float expiresAt;
        public float dps;
        public string ownerTag;
        public EmberBoost ownerBoost; // drives that stack's trail/panic if it's dominant
    }

    private readonly List<BurnStack> _burnStacks = new List<BurnStack>();
    private ParticleSystem _emberFlame; // on-body flame while burning

    private const float FireTickInterval = 0.25f; // how often the fields are sampled
    private const float FireFlushInterval = 1f;   // how often accrued damage lands as a number
    private const float MinTrailMoveSqr = 0.0004f; // ~0.02u — don't stack zones on a standing enemy

    private float _pendingFireDamage; // fractional dps accumulator
    private float _sinceFireTick;
    private float _sinceFireFlush;
    private string _lastFireOwnerTag; // owner credited at the next flush
    private float _sinceTrailDrop;
    private Vector3 _lastTrailPosition;
    private Vector3 _previousPosition;
    private bool _emberTrackingStarted;

    public bool IsIgnited => _burnStacks.Count > 0;

    // Fired with the credited knight's tag ("PlayerLeft"/"PlayerRight") whenever
    // that knight's damage kills an enemy — Thousand Cuts (Shadow Order) listens
    public static event System.Action<string> OnEnemyKilledBy;

    // Highest health this enemy has had; GetMaxHealth needs it because `health`
    // is mutated in place by damage (and some enemies set health after Awake)
    protected float peakHealth;

    protected static void RaiseEnemyKilledBy(string playerTag)
    {
        if (!string.IsNullOrEmpty(playerTag))
        {
            OnEnemyKilledBy?.Invoke(playerTag);
        }
    }

    protected static string PlayerTagFromProjectile(GameObject projectile)
    {
        if (projectile == null) return null;
        if (projectile.CompareTag("PlayerLeftProjectile")) return "PlayerLeft";
        if (projectile.CompareTag("PlayerRightProjectile")) return "PlayerRight";
        return null;
    }
    // Stagger system
    protected bool isStaggered = false;
    protected AnimationClip originalAnimationClip;
    public bool IsStaggered => isStaggered;

    protected virtual void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        glowManager = GetComponent<GlowManager>(); // Cache the component
        animator = GetComponent<Animator>(); // Cache the animator
        peakHealth = health;

        BaseWave.RegisterEnemy(gameObject); // Register for wave tracking
    }

    public virtual void TakeDamage(int damage, GameObject projectile)
    {
        // Ignore any damage once death has been triggered
        if (isDead) return;
        // Track the pre-damage peak so GetMaxHealth stays correct even for
        // enemies that raise their health after Awake (slime sizes, boss init)
        peakHealth = Mathf.Max(peakHealth, health);
        // Extension point for pre-damage effects
        OnBeforeDamageApplied(damage, projectile);
        
        glowManager?.StartGlow(Color.red, 0.3f);
        ShowDamageText(damage);
        
        health -= damage;
        
        // Extension point for post-damage effects
        OnAfterDamageApplied(damage, projectile);
        
        if (health > 0)
        {
            StartCoroutine(StaggerRoutine()); // Stagger if alive
        }
        
        if (health <= 0)
        {
            // Mark as dead immediately to prevent re-entrancy in the same frame
            isDead = true;
            // Serpent effects fire on any death while poisoned, not just poison-tick deaths
            TriggerPoisonDeathEffects();
            // Give death special to the player who fired the projectile
            GiveSpecialToPlayer(specialOnDeath, projectile);
            RaiseEnemyKilledBy(PlayerTagFromProjectile(projectile));

            // Play death sound
            if (deathSound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(deathSound);
            }

            // Call virtual death method for custom behavior
            OnDeath();
        }
        else
        {
        GiveSpecialToPlayer(specialOnHit, projectile);

        if (hurtSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(hurtSound);
        }
        }
    }

    protected virtual IEnumerator StaggerRoutine()
    {
        isStaggered = true;
        
        if (staggerAnimation != null && animator != null)
        {
            animator.Play(staggerAnimation.name);
        }
        
        yield return new WaitForSeconds(staggerDuration);
        
        if (defaultAnimation != null && animator != null)
        {
            animator.Play(defaultAnimation.name);
        }
        
        isStaggered = false;
    }

    public virtual void ApplyPoison(int damage, float duration, float tickRate, GameObject sourceProjectile = null)
    {
        // Extract player tag from projectile tag
        string playerTag = null;
        if (sourceProjectile != null)
        {
            playerTag = sourceProjectile.CompareTag("PlayerLeftProjectile") ? "PlayerLeft" : "PlayerRight";
        }
        ApplyPoisonFromTag(damage, duration, tickRate, playerTag);
    }

    // Tag-based entry point so non-projectile poison (Miasma clouds, Plaguebringer
    // bursts) still credits the right knight and can chain further spreads
    public void ApplyPoisonFromTag(int damage, float duration, float tickRate, string playerTag)
    {
        if (isDead) return;

        // Add or update poison source
        if (!string.IsNullOrEmpty(playerTag))
        {
            var existingSource = poisonSources.Find(ps => ps.playerTag == playerTag);
            if (existingSource != null)
            {
                existingSource.damageContribution += damage; // Stack damage from same source
            }
            else
            {
                poisonSources.Add(new PoisonSource(playerTag, damage)); // Add new source
            }
        }

        // Stack poison damage and reset timer
        poisonDamage += damage;
        poisonTimer = duration; // Reset timer to new duration
        poisonTickRate = tickRate; // Use latest tick rate
        lastPoisonTick = 0f;

        AudioManager.Instance?.PlaySFX(AudioManager.Instance.poisoned);
        
        // Create poison bubbles if not already created
        if (poisonBubbles == null)
        {
            GameObject bubbleObject;
            GameObject bubblePrefab = PoisonResourceManager.Instance?.GetPoisonBubblePrefab();
            
            if (bubblePrefab != null)
            {
                // Use prefab from resource manager
                bubbleObject = Instantiate(bubblePrefab, transform.position, Quaternion.identity);
                bubbleObject.transform.SetParent(transform);
                bubbleObject.transform.localPosition = Vector3.zero;
                poisonBubbles = bubbleObject.GetComponent<PoisonBubbleEffect>();
                
                // If prefab doesn't have the component, add it
                if (poisonBubbles == null)
                {
                    poisonBubbles = bubbleObject.AddComponent<PoisonBubbleEffect>();
                }
            }
            else
            {
                // Fallback: create dynamically and try to get sprite from resource manager
                bubbleObject = new GameObject("PoisonBubbles");
                bubbleObject.transform.SetParent(transform);
                bubbleObject.transform.localPosition = Vector3.zero;
                poisonBubbles = bubbleObject.AddComponent<PoisonBubbleEffect>();
                
                // Try to set sprite from resource manager
                Sprite bubbleSprite = PoisonResourceManager.Instance?.GetPoisonBubbleSprite();
                if (bubbleSprite != null)
                {
                    poisonBubbles.SetBubbleSprite(bubbleSprite);
                }
            }
        }
        
        // Start poison coroutine if not already running
        if (poisonCoroutine == null)
        {
            poisonCoroutine = StartCoroutine(PoisonRoutine());
        }
    }

    protected virtual IEnumerator PoisonRoutine()
    {
        isPoisoned = true;
        
        yield return new WaitForSeconds(0.4f); // Wait for red dmg glow to end
        
        float remainingDuration = poisonTimer - 0.4f;
        if (remainingDuration > 0f)
        {
            glowManager?.StartGlow(new Color(0.3f, 0.5f, 0.13f), remainingDuration, 5f, 0.75f); // Poison glow
        }
        
        // Start poison bubbles
        poisonBubbles?.StartBubbles();
        
        while (poisonTimer > 0f)
        {
            if (lastPoisonTick >= poisonTickRate)
            {
                health -= poisonDamage;
                ShowDamageText(poisonDamage, new Color(0.7f, 0.9f, 0.5f)); // Pale green text
                
                // Don't give special points for poison ticks - only for kills
                
                lastPoisonTick = 0f;
                if (health <= 0)
                {
                    if (isDead)
                    {
                        // Already handled by another damage source
                        yield break;
                    }
                    isDead = true;
                    TriggerPoisonDeathEffects();
                    Debug.Log($"Enemy died from poison! Poison sources count: {poisonSources.Count}");
                    
                    // Stop poison bubbles but let them finish their animation
                    poisonBubbles?.StopBubblesAndDetach();
                    
                    // Give death special to all contributors
                    foreach (var poisonSource in poisonSources)
                    {
                        if (!string.IsNullOrEmpty(poisonSource.playerTag))
                        {
                            Debug.Log($"Giving death special to poison contributor with player tag: {poisonSource.playerTag}");
                            GiveSpecialToPlayer(specialOnDeath, poisonSource.playerTag);
                            RaiseEnemyKilledBy(poisonSource.playerTag);
                        }
                        else
                        {
                            Debug.LogWarning("Poison source player tag is null or empty!");
                        }
                    }
                    
                    if (deathSound != null && AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlaySFX(deathSound);
                    }
                    OnDeath();
                    yield break;
                }
            }
            
            // Update timers
            lastPoisonTick += Time.deltaTime;
            poisonTimer -= Time.deltaTime;
            
            yield return null;
        }
        
        // Stop poison bubbles when effect ends but let them finish their animation
        poisonBubbles?.StopBubblesAndDetach();
        
        // Poison effect ended - clear all data
        isPoisoned = false;
        poisonCoroutine = null;
        poisonSources.Clear();
        poisonDamage = 0;
    }

    protected virtual void ShowDamageText(int damage, Color textColor = default)
    {        
        // Use red as default color if no color specified
        if (textColor == default)
        {
            textColor = Color.red;
        }
        if (damageTextPrefab != null)
        {
            // Calculate position based on sprite height
            float spriteHeight = spriteRenderer != null ? spriteRenderer.bounds.size.y : 1f;
            Vector3 adjustedOffset = damageTextOffset + new Vector3(0, spriteHeight, 0);
            Vector3 spawnPosition = transform.position + adjustedOffset;
            
            // Before spawning the new text, nudge existing texts on this enemy upward so they stack
            // Look for DamageText components under this enemy
        var existingTexts = GetComponentsInChildren<DamageText>(includeInactive: false);
            if (existingTexts != null && existingTexts.Length > 0)
            {
                foreach (var dt in existingTexts)
                {
                    // Push each existing damage text up by one slot
            dt.PushUp(damageTextStackSeparation);
                }
            }
            
            GameObject damageTextObj = Instantiate(damageTextPrefab, spawnPosition, Quaternion.identity);
            // Parent to this enemy so subsequent spawns can find and stack
            damageTextObj.transform.SetParent(transform);
            
            // Try to get the DamageText component and set the damage value and color
            var damageText = damageTextObj.GetComponent<DamageText>();
            if (damageText != null)
            {
                damageText.Initialize(damage, textColor);
            }
            else
            {
                
                // Fallback: Set the color directly on text components
                var textMesh = damageTextObj.GetComponent<TextMesh>();
                if (textMesh != null)
                {
                    textMesh.color = textColor;
                }
                else
                {
                    // Try TMPro Text component if TextMesh isn't found
                    var tmpText = damageTextObj.GetComponent<TMPro.TextMeshPro>();
                    if (tmpText != null)
                    {
                        tmpText.color = textColor;
                    }
                }
            }
        }
    }

    // Serpent Order death effects (Miasma clouds, Plaguebringer bursts). Called from
    // both death sites (arrow and poison tick) right after isDead flips, so subclass
    // OnDeath overrides can't skip it.
    protected void TriggerPoisonDeathEffects()
    {
        if (!isPoisoned || poisonSources.Count == 0) return;

        PlayerStats.Increment("kills.poisoned");

        // One cloud per death, using the strongest contributor's Miasma level;
        // every Plaguebringer contributor gets their burst credit
        int bestMiasmaLevel = 0;
        string miasmaOwnerTag = null;

        foreach (var source in poisonSources)
        {
            if (string.IsNullOrEmpty(source.playerTag)) continue;
            GameObject player = GameObject.FindWithTag(source.playerTag);
            PoisonTipBoost boost = player != null ? player.GetComponent<PoisonTipBoost>() : null;
            if (boost == null) continue;

            if (boost.MiasmaLevel > bestMiasmaLevel)
            {
                bestMiasmaLevel = boost.MiasmaLevel;
                miasmaOwnerTag = source.playerTag;
            }

            if (boost.Plaguebringer)
            {
                PlagueBurst(source.playerTag);
            }
        }

        if (bestMiasmaLevel > 0)
        {
            PoisonCloud.Spawn(transform.position, bestMiasmaLevel, miasmaOwnerTag);
        }
    }

    // Plaguebringer: the victim's full poison stacks jump to nearby enemies, and the
    // owning knight is paid in special. Spread poison carries the owner's tag, so
    // chain deaths keep bursting.
    private void PlagueBurst(string ownerTag)
    {
        const float burstRadius = 2.25f;
        const float burstPoisonDuration = 6f;
        const int burstSpecialBonus = 5;

        int burstDamage = Mathf.Max(poisonDamage, 3);

        foreach (var col in Physics2D.OverlapCircleAll(transform.position, burstRadius))
        {
            EnemyBase enemy = col.GetComponent<EnemyBase>();
            if (enemy != null && enemy != this && !enemy.IsDead)
            {
                enemy.ApplyPoisonFromTag(burstDamage, burstPoisonDuration, 1f, ownerTag);
            }
        }

        GiveSpecialToPlayer(burstSpecialBonus, ownerTag);
        PoisonCloud.SpawnBurstEffect(transform.position);
    }

    // ================= Ember Order =================

    // THE IGNITION PILLAR: the ONLY callers of this are PlayerProjectile (an arrow
    // that carries ignite) and FireballProjectile. Fire zones must never reach it —
    // if fire could start fire, the arena self-immolates and the player stops
    // mattering. See Docs/Design/ember-order.md.
    //
    // Each call adds an independent burn stack, so hitting a burning enemy with
    // another ignited arrow/fireball piles heat on rather than merely refreshing.
    public void Ignite(string playerTag)
    {
        if (isDead || string.IsNullOrEmpty(playerTag)) return;

        // Resolve the igniting knight's stat sheet; this stack's dps and (if it's
        // the dominant one) its trail/panic all read from it.
        GameObject owner = GameObject.FindWithTag(playerTag);
        EmberBoost ownerBoost = owner != null ? owner.GetComponent<EmberBoost>() : null;
        float stackDps = ownerBoost != null ? ownerBoost.ZoneDps : EmberBoost.BaseZoneDps;

        bool wasIgnited = IsIgnited;
        _burnStacks.Add(new BurnStack
        {
            expiresAt = Time.time + EmberBoost.IgniteDuration,
            dps = stackDps,
            ownerTag = playerTag,
            ownerBoost = ownerBoost
        });

        if (!wasIgnited)
        {
            _sinceTrailDrop = 0f;
            _lastTrailPosition = transform.position;
        }

        // Visible flame riding the enemy, so a burning enemy reads as on fire and
        // not merely tinted. Re-igniting just resumes the existing one.
        if (_emberFlame == null)
        {
            _emberFlame = FireFx.AttachEnemyFlame(gameObject);
        }
        else
        {
            var em = _emberFlame.emission;
            em.enabled = true;
            if (!_emberFlame.isPlaying) _emberFlame.Play();
        }

        // Ignition owns the glow while it burns — it's much shorter than poison,
        // whose glow resumes on its own next tick if it's still running
        glowManager?.StartGlow(new Color(1f, 0.45f, 0.12f), EmberBoost.IgniteDuration, 6f, 0.8f);
    }

    // Sum of live burn dps (on-body). Also reports the dominant stack's owner/boost —
    // the hottest burn — for kill credit and for driving trail/panic.
    private float BurnDps(out string dominantTag, out EmberBoost dominantBoost)
    {
        float total = 0f;
        float best = -1f;
        dominantTag = null;
        dominantBoost = null;
        for (int i = 0; i < _burnStacks.Count; i++)
        {
            BurnStack s = _burnStacks[i];
            total += s.dps;
            if (s.dps > best)
            {
                best = s.dps;
                dominantTag = s.ownerTag;
                dominantBoost = s.ownerBoost;
            }
        }
        return total;
    }

    // Drop burns whose timer ran out. Returns true while any remain.
    private bool PruneBurnStacks()
    {
        float now = Time.time;
        for (int i = _burnStacks.Count - 1; i >= 0; i--)
        {
            if (_burnStacks[i].expiresAt <= now)
            {
                _burnStacks.RemoveAt(i);
            }
        }
        return _burnStacks.Count > 0;
    }

    // Runs after every subclass Update (no enemy declares LateUpdate), so it sees
    // the movement they just performed. That's what lets Searing Panic and Fire
    // Trail work on every enemy — including the Rat King and anything added later —
    // without touching a single subclass.
    protected virtual void LateUpdate()
    {
        if (isDead) return;

        if (!_emberTrackingStarted)
        {
            _emberTrackingStarted = true;
            _previousPosition = transform.position;
            _lastTrailPosition = transform.position;
        }

        bool burning = PruneBurnStacks();
        if (!burning && _emberFlame != null && _emberFlame.emission.enabled)
        {
            var em = _emberFlame.emission;
            em.enabled = false;
            _emberFlame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (burning)
        {
            // The dominant (hottest) burn's knight owns trail + panic this frame
            string dominantTag;
            EmberBoost dominantBoost;
            BurnDps(out dominantTag, out dominantBoost);
            ApplySearingPanic(dominantBoost);
            DropFireTrail(dominantBoost);
        }

        _previousPosition = transform.position;

        TickFire();
    }

    // Multiplies whatever movement the subclass just did, along its own heading.
    // Works regardless of how that enemy moves (MoveTowards, Lerp, waypoints).
    private void ApplySearingPanic(EmberBoost boost)
    {
        if (boost == null || IsStaggered) return;

        float multiplier = boost.PanicSpeedMultiplier;
        if (multiplier <= 1f) return;

        Vector3 delta = transform.position - _previousPosition;
        if (delta.sqrMagnitude <= 1e-8f) return;

        transform.position += delta * (multiplier - 1f);
    }

    private void DropFireTrail(EmberBoost boost)
    {
        if (boost == null || !boost.HasFireTrail) return;

        _sinceTrailDrop += Time.deltaTime;
        if (_sinceTrailDrop < EmberBoost.TrailDropInterval) return;
        _sinceTrailDrop = 0f;

        // Only paint where the enemy actually travelled
        if ((transform.position - _lastTrailPosition).sqrMagnitude < MinTrailMoveSqr) return;

        boost.PlaceZone(transform.position,
            boost.TrailZoneRadius,
            boost.TrailZoneDuration);
        _lastTrailPosition = transform.position;
    }

    // Fire damage has two sources — being ignited (on-body) and standing in a fire
    // zone (ground) — and an enemy takes the HOTTER of the two, never their sum, so
    // an ignited enemy walking its own trail isn't billed twice for one fire.
    //
    // Enemies poll the field rather than the field damaging enemies; that one-way
    // direction is what keeps FireField structurally unable to reach Ignite().
    private void TickFire()
    {
        _sinceFireTick += Time.deltaTime;
        if (_sinceFireTick < FireTickInterval) return;

        float elapsed = _sinceFireTick;
        _sinceFireTick = 0f;

        float dps = 0f;
        string ownerTag = null;

        // On fire: the SUM of every live burn stack, each sized to its igniting
        // knight's Ember investment (base 2.0, up to 4.0 per stack). Stacking is what
        // makes a second ignited arrow pile heat on instead of just refreshing.
        if (IsIgnited)
        {
            string dominantTag;
            EmberBoost dominantBoost;
            dps = BurnDps(out dominantTag, out dominantBoost);
            ownerTag = dominantTag;
        }

        // Ground fire under this enemy — take it only if it burns hotter
        float zoneDps;
        string zoneOwner;
        if (FireField.Sample(transform.position, out zoneDps, out zoneOwner) && zoneDps > dps)
        {
            dps = zoneDps;
            ownerTag = zoneOwner;
        }

        if (dps > 0f)
        {
            _pendingFireDamage += dps * elapsed;
            _lastFireOwnerTag = ownerTag; // survives to the flush even if fire just expired
        }

        // Land accrued damage as ONE number per second, so the figure on screen IS
        // the dps: a base burn pops "2", a full build "4", stacks bigger. Flushing
        // on every accumulator crossing instead showed a stream of "1"s that read
        // as 1 dps no matter the actual rate.
        _sinceFireFlush += elapsed;
        if (_sinceFireFlush < FireFlushInterval) return;
        _sinceFireFlush = 0f;

        if (_pendingFireDamage < 1f) return;

        int whole = Mathf.FloorToInt(_pendingFireDamage);
        _pendingFireDamage -= whole;
        ApplyFireDamage(whole, _lastFireOwnerTag);
    }

    // Deliberately NOT routed through TakeDamage: that fires StaggerRoutine, and a
    // once-a-second stagger would stun-lock anything standing in fire (subclasses
    // skip movement while staggered), freezing the very movement Fire Trail needs.
    // Mirrors the poison tick's inline damage path instead.
    protected void ApplyFireDamage(int damage, string ownerTag)
    {
        if (isDead || damage <= 0) return;

        peakHealth = Mathf.Max(peakHealth, health);
        health -= damage;
        ShowDamageText(damage, new Color(1f, 0.55f, 0.2f)); // ember orange

        if (health > 0) return;

        isDead = true;
        // A poisoned enemy finished off by fire still owes Serpent its death effects
        TriggerPoisonDeathEffects();
        PlayerStats.Increment("kills.burned");

        if (!string.IsNullOrEmpty(ownerTag))
        {
            GiveSpecialToPlayer(specialOnDeath, ownerTag);
            RaiseEnemyKilledBy(ownerTag);
        }

        if (deathSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(deathSound);
        }

        OnDeath();
    }

    protected virtual void OnDeath()
    {
        if (goldOnDeath > 0)
        {
            GoldManager.Instance?.AddGold(goldOnDeath);
        }

        PlayerStats.Increment($"kills.{StatKey}");

        // Unregister this enemy from wave tracking before destroying
        BaseWave.UnregisterEnemy(gameObject);

        // Damage numbers are parented to us for stacking; the killing-blow text
        // spawns the same frame we die, so detach any still-animating texts
        // before Destroy takes them with it (otherwise the final hit never shows)
        DetachDamageTexts();

        // Default behavior: destroy the game object
        Destroy(gameObject);
    }

    // Reparent floating damage numbers to the scene root so they outlive this
    // enemy's destruction and finish their fade-and-rise on their own.
    protected void DetachDamageTexts()
    {
        var texts = GetComponentsInChildren<DamageText>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            texts[i].transform.SetParent(null, true);
        }
    }

    protected virtual string StatKey
    {
        get
        {
            var name = GetType().Name;
            if (name.StartsWith("Enemy")) name = name.Substring(5);
            return name.ToLowerInvariant();
        }
    }

    // Extension points for custom damage handling
    protected virtual void OnBeforeDamageApplied(int damage, GameObject projectile)
    {
        // Default: no custom behavior
    }

    protected virtual void OnAfterDamageApplied(int damage, GameObject projectile)
    {
        // Default: no custom behavior
    }

    protected void GiveSpecialToPlayer(int amount, GameObject projectile)
    {
        if (projectile == null)
        {
            Debug.LogWarning("GiveSpecialToPlayer: projectile is null!");
            return;
        }


        GameObject player = projectile.CompareTag("PlayerLeftProjectile")
            ? GameObject.FindWithTag("PlayerLeft")
            : GameObject.FindWithTag("PlayerRight");

        Debug.Log($"GiveSpecialToPlayer: found player = {(player != null ? player.name : "null")}");

        if (player != null)
        {
            PlayerSpecial playerSpecial = player.GetComponent<PlayerSpecial>();
            if (playerSpecial != null)
            {
                Debug.Log($"GiveSpecialToPlayer: Giving {amount} special to {player.name}");
                playerSpecial.updateSpecial(amount);
            }
            else
            {
                Debug.LogWarning($"GiveSpecialToPlayer: PlayerSpecial component not found on {player.name}");
            }
        }
        else
        {
            Debug.LogWarning($"GiveSpecialToPlayer: No player found for projectile tag {projectile.tag}");
        }
    }

    // Overloaded version that accepts a player tag directly
    protected void GiveSpecialToPlayer(int amount, string playerTag)
    {
        if (string.IsNullOrEmpty(playerTag))
        {
            Debug.LogWarning("GiveSpecialToPlayer: playerTag is null or empty!");
            return;
        }

        GameObject player = GameObject.FindWithTag(playerTag);
        Debug.Log($"GiveSpecialToPlayer: found player = {(player != null ? player.name : "null")} for tag {playerTag}");

        if (player != null)
        {
            PlayerSpecial playerSpecial = player.GetComponent<PlayerSpecial>();
            if (playerSpecial != null)
            {
                Debug.Log($"GiveSpecialToPlayer: Giving {amount} special to {player.name}");
                playerSpecial.updateSpecial(amount);
            }
            else
            {
                Debug.LogWarning($"GiveSpecialToPlayer: PlayerSpecial component not found on {player.name}");
            }
        }
        else
        {
            Debug.LogWarning($"GiveSpecialToPlayer: No player found for tag {playerTag}");
        }
    }

    public virtual bool HasAttribute(EnemyType attr)
    {
        return (attributes & attr) == attr;
    }

    // Virtual method for calculating collision damage (can be overridden by enemies like slime)
    protected virtual int GetShieldCollisionDamage()
    {
        return shieldDamage;
    }

    protected virtual int GetPlayerCollisionDamage()
    {
        return playerDamage;
    }

    // Virtual method for additional collision handling (can be overridden by specific enemies)
    protected virtual void OnAdditionalCollision(Collider2D other)
    {
        // Default: no additional collision behavior
    }

    // Virtual method for updating sprite direction based on movement
    protected virtual void UpdateSpriteDirection(Vector3 moveDirection)
    {
        if (spriteRenderer != null)
        {
            // Flip sprite based on horizontal movement direction with threshold
            if (moveDirection.x < -directionThreshold)
                spriteRenderer.flipX = true;  // Moving left
            else if (moveDirection.x > directionThreshold)
                spriteRenderer.flipX = false; // Moving right
        }
    }

    // Overload for updating sprite direction based on current position and target
    protected virtual void UpdateSpriteDirection(Vector3 currentPosition, Vector3 targetPosition)
    {
        Vector3 moveDirection = (targetPosition - currentPosition).normalized;
        UpdateSpriteDirection(moveDirection);
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
    if (isDead) return; // Ignore collisions after death triggered
        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            // Damage is handled in PlayerProjectile, just destroy the projectile
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("Shield"))
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.enemyShield);
            PlayerHealth playerHealth = other.transform.parent?.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(GetShieldCollisionDamage(), DisplayName);
            }
            Destroy(gameObject);
        }
        else if (other.CompareTag("PlayerLeft") || other.CompareTag("PlayerRight"))
        {
            // Damage feedback (the unified grunt) plays inside PlayerHealth.TakeDamage
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(GetPlayerCollisionDamage(), DisplayName);
            }
            Destroy(gameObject);
        }
        else
        {
            // Allow derived classes to handle additional collision types
            OnAdditionalCollision(other);
        }
    }

    // Name shown on the death screen when this enemy lands the killing blow
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(displayName))
            {
                return displayName;
            }

            string typeName = GetType().Name;
            if (typeName.StartsWith("Enemy"))
            {
                typeName = typeName.Substring("Enemy".Length);
            }

            // Split CamelCase: "RatKing" -> "Rat King"
            return System.Text.RegularExpressions.Regex.Replace(typeName, "(\\B[A-Z])", " $1");
        }
    }

    // Public getter for health (useful for UI or other systems)
    public float GetHealth() => health;
    
    // Public getter for max health (Killing Blow thresholds depend on this
    // being the real maximum, not the mutated current value)
    public virtual float GetMaxHealth() => Mathf.Max(peakHealth, health);
}
