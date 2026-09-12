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

    // Name-based alternative to the two clips above, for anything animated from a
    // .aseprite. The importer REGENERATES its clips on every reimport, so a clip
    // reference is a handle onto something that keeps being rebuilt; the Animator
    // state name — which is just the Aseprite tag — survives it. Either form works
    // and the clip wins when both are set, so nothing already authored changes.
    [Tooltip("Animator state played while staggered, e.g. the Aseprite tag \"Damage\". Ignored when Stagger Animation is set.")]
    [SerializeField] protected string staggerStateName = "";

    [Tooltip("Animator state returned to when the stagger ends, e.g. the Aseprite tag \"Rolling\". Ignored when Default Animation is set.")]
    [SerializeField] protected string defaultStateName = "";

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
    //
    // ONLY AIMED IGNITIONS STACK, and only the two you spend a shot on — an arrow that
    // rolled ignite, and a fireball. Ground fire sets a flag instead and never adds a
    // stack, because a Fire Trail lane is dozens of overlapping drops and stacking them
    // multiplied a body's damage by however many happened to meet under its feet (see
    // the fire-AoE flag below). Fire Sight is the third door and never stacks either: the
    // beam runs every frame, so it only ever lights something that is not already on
    // fire — see ShieldSight.
    private struct BurnStack
    {
        public float expiresAt;
        public float dps;
        public string ownerTag;
        public EmberBoost ownerBoost; // drives that stack's trail/panic if it's dominant
    }

    private readonly List<BurnStack> _burnStacks = new List<BurnStack>();
    private ParticleSystem _emberFlame; // on-body flame while burning

    private float _sinceSpreadCheck;

    // ---- The fire-AoE flag ----
    //
    // STANDING IN FIRE IS A FLAG, NOT AN IGNITION (owner's call, 2026-09-08). This is
    // the fix for the worst bug the Order has had: catching from the ground used to
    // add one burn STACK per distinct fire underfoot, and burn stacks SUM. A Fire
    // Trail lane is dozens of overlapping drops from many different burns, so one step
    // into a painted patch handed a body five or six stacks at once and it took 40-70
    // damage on the next flush — a full build's zone dps multiplied by however many
    // fires happened to overlap where it put its foot down.
    //
    // Now the ground sets ONE flag. While it holds, the body burns at the rate of the
    // ground under it — the specified zone dps, once, never a sum and never once per
    // zone — and counts as on fire for the flame, the trail and the panic. Overlap
    // cannot multiply a flag.
    //
    // THE LINGER DOES NOT BILL (owner's report, 2026-09-08). The flag outlives the
    // fire by EmberBoost.FireAoeLinger so the STATE does not chatter on and off as a
    // body clips the edge of a lane — but damage is charged only for the beats a body
    // is actually standing in fire.
    //
    // Charging the linger was a bug and a bad one: a bat that grazed a lane for a fifth
    // of a second was billed for 2.2 seconds, 44% of its health, most of it while
    // flying nowhere near the fire. Anything fast enough to cross a lane paid almost
    // the same as something that stopped in it, which is the opposite of what a
    // burning floor should do.
    private float _fireAoeUntil;

    // Whether the last sample actually found ground fire underfoot. This, not the
    // lingering flag, is what the field damage channel bills against.
    private bool _standingInFire;
    private float _fireAoeDps;
    private string _fireAoeOwnerTag;
    private EmberBoost _fireAoeBoost;
    // Ground DAMAGE runs off the flag unconditionally; this second bit is whether that
    // ground also sets the body alight, and it is false in every real run — only
    // EmberBoost.GroundFireIgnites can raise it, and no upgrade touches that.
    private bool _fireAoeIgnites;

    /// <summary>Burning because of the ground it is standing on (or just left).</summary>
    public bool IsBurningFromGround { get { return _fireAoeIgnites && Time.time < _fireAoeUntil; } }

    /// <summary>On fire by any route: an arrow/fireball burn, or the ground.</summary>
    public bool IsOnFire { get { return IsIgnited || IsBurningFromGround; } }

    private const float FireTickInterval = 0.25f; // how often the fields are sampled
    private const float FireFlushInterval = 1f;   // how often accrued damage lands as a number
    private const float MinTrailMoveSqr = 0.0004f; // ~0.02u — don't stack zones on a standing enemy

    // ---- Fire's TWO damage channels (owner's call, 2026-09-08) ----
    //
    // Ember bills a body twice, from two independent sources, each accruing and
    // flushing on its own clock:
    //
    //   1. IGNITION — the burn stacks an arrow or a fireball put on the body. Aimed
    //      and spent, so several of them stack and sum. (Fire Sight lights a body that
    //      is not alight and never adds a second stack — it is a beam, not a shot.)
    //   2. THE FIELD — the ground it is standing on, or a burning body pressed
    //      against it. One zone's worth, hottest-wins, never a sum.
    //
    // These used to be hottest-wins against EACH OTHER, so an ignited enemy standing
    // in fire paid for one fire. They are now additive: standing in your own Order's
    // burning ground while carrying your arrow's burn is worth both, which roughly
    // doubles what a full Ember build does to a body that is both lit and standing in
    // a lane. That is the intended shape — Ember's whole pitch is that the two halves
    // compose — and it is why the numbers land as two separate figures.
    //
    // The two flushes run half a second out of phase so the figures alternate on
    // screen instead of landing on top of each other, and they are different colours:
    // ember orange for the burn riding the body, gold for the ground under it.
    private static readonly Color IgniteDamageColor = new Color(1f, 0.55f, 0.2f);
    private static readonly Color FieldDamageColor = new Color(1f, 0.8f, 0.3f);

    private float _sinceFireTick;

    private float _pendingIgniteDamage; // fractional dps accumulator, ignition channel
    private float _sinceIgniteFlush;
    private string _lastIgniteOwnerTag; // owner credited at the next flush

    private float _pendingFieldDamage; // fractional dps accumulator, field channel
    private float _sinceFieldFlush = FireFlushInterval * 0.5f; // half a beat out of phase
    private string _lastFieldOwnerTag;

    // Heat handed over by a burning body in contact — a rate, not an ignition.
    // See ReceiveContactFire / BurnNeighboursByContact.
    private float _contactFireDps;
    private string _contactFireOwnerTag;
    private float _contactFireUntil;
    private float _sinceTrailDrop;
    private Vector3 _lastTrailPosition;
    private Vector3 _previousPosition;
    private bool _emberTrackingStarted;

    // The way this body last moved under its own power — what a Miasma cloud left
    // by its death drifts along. Zero until it has moved at all.
    private Vector2 _lastMoveHeading;

    // How fast this body is really travelling, in world units per second: the whole
    // frame's movement (its own walking plus chill, panic and shove), smoothed so a
    // hopper reads as its average pace rather than flickering between 0 and a leap.
    // A Plaguebringer cloud chases at this plus a little (see PoisonCloud). Measured
    // rather than read from a field because every mob type keeps its speed its own way.
    private float _measuredSpeed;
    private const float MeasuredSpeedSmoothing = 0.25f; // seconds to cover ~63% of a change

    // A single-frame jump longer than this is a teleport or a snap, not walking,
    // and is left out rather than read as a burst of speed
    private const float MeasuredSpeedMaxStep = 1f;

    public float MeasuredSpeed => _measuredSpeed;

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

    // Sleep (Sleeping Dart) rides the stagger flag rather than adding a second
    // "hold still" rule every enemy would have to learn: everything that already
    // stands still when staggered stands still when asleep, for free, and
    // anything that ignores stagger (the carts own their own position) keeps
    // ignoring it. The two are still separate underneath so a sleeping enemy
    // can be told apart from a flinching one.
    public bool IsStaggered => isStaggered || IsHeld;

    /// <summary>
    /// Held motionless, for ANY reason — a sleeping dart or the Frigid Order's
    /// ice. One question rather than two, because everything downstream of a hold
    /// (the freeze component, the pin in LateUpdate, the vehicles that own their
    /// own position) wants the same answer whichever bought it, and a second copy
    /// of "or frozen" in each of those places is a second copy that can be missed.
    /// The two states stay separate underneath so their tells can differ and a
    /// cleanse can take one off without the other.
    /// </summary>
    public bool IsHeld => IsAsleep || IsFrozen;

    // ---- sleep ----
    private float _sleepUntil = -1f;

    /// <summary>Held still by a sleeping dart. Ends on its own; damage does not wake it.</summary>
    public bool IsAsleep => !isDead && Time.time < _sleepUntil;

    /// <summary>
    /// Seconds of sleep still to run, or zero when awake. Anything that has to
    /// hold a SECOND thing still for the length of the nap - a mine cart, whose
    /// position its rider does not own - has to ask for what is left rather than
    /// reuse the seconds it was handed, because overlapping darts extend the
    /// deadline and the shorter of two holds would end the jam early.
    /// </summary>
    public float RemainingSleepSeconds => IsAsleep ? _sleepUntil - Time.time : 0f;

    /// <summary>
    /// Everything the knights can shoot sleeps: every mob, every boss, and
    /// anything added later. The hook is kept because refusing a status is a
    /// thing a fight might one day need to say, but nothing in the game says it
    /// - a boss that ignored the dart would send the player looking for the bug
    /// that is not there.
    /// </summary>
    public virtual bool ImmuneToSleep => false;

    // Where the body was put down, so the freeze has somewhere to hold it. See
    // SleepFreeze for how a sleeping enemy is actually made to stand still, and
    // why it is done TO the enemy rather than asked of it.
    private Vector3 _sleepAnchor;

    /// <summary>
    /// Put this enemy under for <paramref name="seconds"/>. Overlapping darts
    /// extend rather than shorten, matching how every other timed status here
    /// takes the later deadline.
    ///
    /// Deliberately not virtual. Sleep means one thing for everything on the
    /// board, and a subclass that could redefine it is a subclass that can get
    /// it wrong or forget it entirely - which is exactly how the dart came to do
    /// nothing to a boss in the first place.
    /// </summary>
    public void Sleep(float seconds)
    {
        if (isDead || ImmuneToSleep || seconds <= 0f) return;

        bool wasAwake = !IsAsleep;
        _sleepUntil = Mathf.Max(_sleepUntil, Time.time + seconds);

        if (wasAwake)
        {
            _sleepAnchor = transform.position;
            SleepFx.ShowZs(gameObject);
        }

        // The freeze itself: this enemy's own behaviour is switched off for the
        // length of the nap, so nothing it does in Update - walking, closing on a
        // knight, counting down to its next throw, arriving somewhere that sets
        // off an attack - happens at all. Done here, once, for everything on the
        // board rather than as a flag each enemy has to remember to read.
        SleepFreeze.Apply(this);

        // Anything on this object that owns its own position rather than letting
        // the enemy move itself - a mine cart, which keeps rolling however still
        // its rider is holding - is told to stop for as long as the sleep has
        // LEFT to run. The remaining time, not the seconds asked for: a second
        // dart extends the nap to the later deadline, and a hold sized from that
        // dart alone would end before the sleep it belongs to.
        ISleepHold[] holds = GetComponents<ISleepHold>();
        for (int i = 0; i < holds.Length; i++)
        {
            holds[i].HoldFor(RemainingSleepSeconds);
        }
    }

    /// <summary>Ends sleep immediately and takes the Zs off.</summary>
    public void WakeUp()
    {
        _sleepUntil = -1f;
        SleepFx.HideZs(gameObject);
        SleepFreeze.Release(this);
    }

    // ---- frost (the Frigid Order) ----
    //
    // Two states, not a stack counter. The first touch of cold CHILLS — the body
    // keeps coming, slower. Cold landing on something already chilled FREEZES it,
    // held where it stands. A player can read the board at a glance and always
    // answer "what does one more arrow do here?" without arithmetic, which five
    // stacks of anything on a screen with twenty rats on it does not allow.
    //
    // The freeze rides the same machinery as the sleeping dart rather than adding
    // a second way to hold a body still — see SleepFreeze, and IsHeld above.
    private float _chillUntil = -1f;
    private float _chillMultiplier = 1f;
    private float _frozenUntil = -1f;
    // How much damage the current ice takes before it breaks, and how much it has
    // taken so far. Set by the knight who froze it (5, plus 10 per Deep Freeze rank).
    private int _iceBreakDamage = FrigidBoost.FreezeBreakDamage;
    private int _iceDamageTaken;
    private Vector3 _frozenAnchor;
    private ParticleSystem _frostMotes;
    private bool _frostTinted;
    private Color _frostUntintedColor = Color.white;
    private bool _countedChillApplied;

    /// <summary>Slowed by cold, still walking.</summary>
    public bool IsChilled => !isDead && Time.time < _chillUntil;

    /// <summary>Stopped by cold. Not the same state as asleep: the two do not
    /// cancel each other and they do not look alike.</summary>
    public bool IsFrozen => !isDead && Time.time < _frozenUntil;

    /// <summary>What this body's movement is being multiplied by, or 1 when it is
    /// not chilled.</summary>
    public float ChillSpeedMultiplier => IsChilled ? _chillMultiplier : 1f;

    public float RemainingFreezeSeconds => IsFrozen ? _frozenUntil - Time.time : 0f;

    /// <summary>
    /// Whether this body can be slowed by having the movement it just made scaled
    /// back. False for anything whose position is owned by something else — a cart
    /// on rails does not stroll, and pulling it back here only puts it a frame's
    /// worth off the track before its own Update snaps it home. Those slow through
    /// their own speed instead; see MineCart.
    ///
    /// The mirror of AcceptsSearingPanic, and false in the same places for the
    /// same reason.
    /// </summary>
    protected virtual bool AcceptsChill => true;

    /// <summary>
    /// Set while something is writing this body's position ABSOLUTELY instead of
    /// stepping it — the screen entrances all do this with a Lerp. Scaling the
    /// delta against a lerp does not delay anything: the lerp rewrites the
    /// position next frame regardless, so all a chill buys there is a stutter and
    /// the enemy still arrives exactly on schedule. Better to let the entrance
    /// finish and start charging it cold once it is walking under its own power.
    /// </summary>
    protected bool positionIsScripted;

    // ---- Frost Bite (Frigid capstone) ----

    // Whether a knight who owns the capstone is the one whose cold is on this body,
    // and which knight that is. Both are cleared by PurgeFrost with everything else,
    // so cold that ends stops billing.
    private bool _frostBiteActive;
    private string _frostBiteOwnerTag;
    private float _pendingFrostDamage;
    private float _sinceFrostFlush;

    // How many bodies are in ice right now, across the whole field. Kept by hand
    // on both sides of the hold; OnDeath and the wave clear both route through
    // Thaw/PurgeFrost, so there is no path that leaves this counting a body that
    // is gone.
    private static int _liveFrozenCount;
    public static int LiveFrozenCount { get { return _liveFrozenCount; } }
    public static void ResetLiveFrozenCount() { _liveFrozenCount = 0; }

    /// <summary>
    /// Cold, from any source. THE one door, so every source spends the same rules.
    ///
    /// <paramref name="freezes"/> is false unless the caller is a blow the knight
    /// landed and that knight can freeze (Frost Tip) — pillar 2 of the Order made
    /// structural rather than left to convention. Fields and auras pass false and
    /// physically cannot stop anything.
    ///
    /// A freeze lasts however long the chill had left, plus
    /// <paramref name="freezeBonusSeconds"/> (Deep Freeze, Heart of Ice), and
    /// breaks once it has taken <paramref name="iceBreakDamage"/> damage in total.
    /// </summary>
    /// <returns>True if this touch was the one that froze it.</returns>
    public bool ApplyCold(float speedMultiplier, float chillSeconds, bool freezes,
                          float freezeBonusSeconds = 0f, int iceBreakDamage = FrigidBoost.FreezeBreakDamage,
                          string ownerTag = null, bool frostBite = false)
    {
        if (isDead) return false;

        // Frost Bite (Frigid capstone): remember WHO put the cold on, so the tick
        // this body is about to start taking can be credited when it kills. Recorded
        // even on a touch that neither chills nor freezes below — the latest toucher
        // owns the cold, the same way the latest burn owns a fire.
        if (frostBite)
        {
            _frostBiteOwnerTag = ownerTag;
            _frostBiteActive = true;
        }

        // Already stopped. A blow landing on a statue is a SHATTER, which is the
        // caller's business and happens through TakeDamage — not more cold.
        if (IsFrozen) return false;

        // NO REPRIEVE (owner's call, 2026-09-08). A body that thaws is cold-able
        // again the same instant — there used to be two seconds of resistance here
        // to stop a knight locking one target down forever, and it was removed on
        // purpose: pillar 2 already charges for that. Freezing takes a BLOW, so a
        // knight who wants a body held permanently has to keep landing arrows on
        // it, which is an arrow a second not being spent on the rest of the wave.
        // The cost is the aiming, not an arbitrary cooldown.

        // The freeze turns whatever chill was left into ice (owner's call,
        // 2026-09-11). Read BEFORE this touch refreshes the chill, so it is the time
        // the body had left, not a fresh full chill.
        if (IsChilled && freezes)
        {
            Freeze((_chillUntil - Time.time) + freezeBonusSeconds, iceBreakDamage);
            return true;
        }

        // A fresh chill starts from nothing; a refresh keeps whichever knight's
        // cold was DEEPER and whichever deadline was LATER, the way every other
        // timed status here takes the later of two.
        if (!IsChilled) _chillMultiplier = 1f;
        _chillMultiplier = Mathf.Clamp(Mathf.Min(_chillMultiplier, speedMultiplier), 0.05f, 1f);
        _chillUntil = Mathf.Max(_chillUntil, Time.time + chillSeconds);

        if (!_countedChillApplied)
        {
            _countedChillApplied = true;
            PlayerStats.Increment("frigid.chilled");
        }

        return false;
    }

    /// <summary>
    /// Hold this body in ice. Not virtual, for exactly the reason Sleep is not:
    /// frozen means one thing for everything on the board, and a subclass that
    /// could redefine it is a subclass that can get it wrong or forget it — which
    /// is how the sleeping dart came to do nothing to a boss.
    /// </summary>
    public void Freeze(float seconds, int breakDamage = FrigidBoost.FreezeBreakDamage)
    {
        if (isDead || seconds <= 0f) return;

        // A boss is held for half as long as anything else (owner's call,
        // 2026-09-08). Applied HERE rather than at the knight's end so it covers
        // every source of cold there will ever be, and so no future upgrade can
        // forget it — the same argument that keeps this method non-virtual.
        //
        // Frigid's hold is the one effect in the game that removes a fight rather
        // than shortening it: a stopped boss is not fighting, and at rank three
        // that is fifteen seconds of a duel simply not happening. Halving it keeps
        // the Order strong against a crowd, which is what it is for, without
        // letting it switch off the encounters the run is built around.
        float hold = IsBoss ? seconds * FrigidBoost.BossFreezeMultiplier : seconds;

        bool wasFree = !IsFrozen;
        _frozenUntil = Mathf.Max(_frozenUntil, Time.time + hold);

        // Freezing spends the chill. A body coming out of the ice comes out clean,
        // which is what makes "the first slows, the second stops" a cycle the
        // player re-runs rather than a ladder they climb once.
        _chillUntil = -1f;
        _chillMultiplier = 1f;

        if (wasFree)
        {
            _iceBreakDamage = Mathf.Max(1, breakDamage);
            _iceDamageTaken = 0;
            _frozenAnchor = transform.position;
            FrostFx.ShowIce(gameObject);
            PlayerStats.Increment("frigid.frozen");

            // "Four at once" is the Order's showpiece image, so it is a feat. A
            // live count rather than a sweep of the field on every freeze: a
            // Frigid knight freezes often enough that a FindObjectsByType each
            // time would be a cost the player can feel.
            _liveFrozenCount++;
            if (_liveFrozenCount >= 4) Feats.Record(Feats.FrozenFour);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.frostFreeze);
            }
        }

        // The hold itself: this body's own behaviour is switched off for the
        // length of it, so nothing it does in Update — walking, closing on a
        // knight, counting down to its next throw, arriving somewhere that sets
        // off an attack — happens at all.
        SleepFreeze.Apply(this);

        // And anything on this object that owns its own position rather than
        // letting the enemy move itself — a mine cart, which keeps rolling however
        // still its rider is sitting — is told to stop for what the freeze has
        // LEFT to run. This is what turns one arrow into a jam.
        ISleepHold[] holds = GetComponents<ISleepHold>();
        for (int i = 0; i < holds.Length; i++)
        {
            holds[i].HoldFor(RemainingFreezeSeconds);
        }
    }

    /// <summary>
    /// Break the ice, whether a blow ended it or it simply ran out.
    ///
    /// A thawed body carries nothing away with it: it can be chilled on the next
    /// touch and frozen on the touch after that, immediately. This used to hand out
    /// two seconds of cold resistance when a freeze expired on its own; that was
    /// removed on 2026-09-08 (owner's call) so the cycle never stalls, and pillar 2
    /// is what keeps it honest — only a BLOW can freeze, so holding one body forever
    /// costs an arrow a time that the rest of the wave does not get.
    /// </summary>
    public void Thaw()
    {
        if (_frozenUntil < 0f) return;

        _frozenUntil = -1f;
        _iceDamageTaken = 0;
        _liveFrozenCount = Mathf.Max(0, _liveFrozenCount - 1);
        FrostFx.HideIce(gameObject);
        SleepFreeze.Release(this);
    }

    /// <summary>The freeze-break rule, in one place so every damage path can spend
    /// it — including the two that deliberately bypass TakeDamage. Damage adds up
    /// across hits; the ice breaks once the total reaches what the freezing knight's
    /// Deep Freeze set (5, 15, 25 or 35).</summary>
    protected void BreakFreezeIfHardEnough(int damage)
    {
        if (!IsFrozen || damage <= 0) return;
        _iceDamageTaken += damage;
        if (_iceDamageTaken >= _iceBreakDamage) Thaw();
    }

    /// <summary>Takes every trace of cold back off. Called by the cleanse and by
    /// the wave clear, which is what stops a statue made late in one wave from
    /// still standing in the next — a rank three hold runs fifteen seconds and
    /// will happily straddle a wave boundary.</summary>
    public void PurgeFrost()
    {
        _chillUntil = -1f;
        _chillMultiplier = 1f;

        // The capstone's billing goes with the cold. Cleared here rather than left
        // to TickFrostBite's own "no cold, no charge" early-out, because this is the
        // wave boundary: whatever the last wave's knights had bought, the next wave
        // starts everything on the field owing nothing.
        _frostBiteActive = false;
        _frostBiteOwnerTag = null;
        _pendingFrostDamage = 0f;
        _sinceFrostFlush = 0f;

        Thaw();
        UpdateFrostTells();
    }

    // ---- the two doors SleepFreeze keeps open while this enemy is switched off ----
    //
    // Both call straight back into the virtual member, so a subclass that
    // overrides either one is still the thing that runs.

    /// <summary>The base's per-frame pass, run by the freeze. Poison, fire and
    /// Ember's spread all live in it and none of them stop for a nap.</summary>
    internal void RunFrozenFrame()
    {
        LateUpdate();
    }

    /// <summary>A trigger that arrived while this enemy was switched off. Unity
    /// skips disabled behaviours, and a sleeping body still has to answer a
    /// shield sweeping through it.</summary>
    internal void ForwardFrozenTrigger(Collider2D other)
    {
        OnTriggerEnter2D(other);
    }

    // Whether the wave is allowed to end while this thing is still alive. Almost
    // every enemy gates the wave; the exception is scenery with health — an empty
    // mine cart is an obstacle to shoot around, not a kill the player owes.
    protected virtual bool TracksWaveCompletion => true;

    // Whether damage that lands in an AREA rather than on a target can touch this
    // thing at all: fire (ignition and ground zones), poison, and blasts.
    //
    // Iron does not burn, rot, or care about a shockwave. A cart is scenery with
    // health, and scenery that could be cleared by parking a fire zone under the
    // track would turn the whole mine into a problem you solve once, off-screen,
    // instead of an obstacle you shoot around every lap. Direct hits still land —
    // an arrow is how you are meant to answer a cart.
    //
    // Only the CART is protected. Anything riding one — a gnome, a delivery cart's
    // cargo — is meat and burns normally, so it overrides this back to false.
    //
    // Public because splash that lands through TakeDamage — a fireball's blast —
    // has to ask before it swings. A fireball's DIRECT hit still counts: that is an
    // aimed shot, and aimed shots are how a cart is meant to be answered.
    public virtual bool ImmuneToAreaDamage => false;

    /// <summary>
    /// This enemy is a BOSS, and effects that skip health rather than deal it do
    /// not work on it. Killing Blow is the one that matters today: executing a
    /// fraction of a two-and-a-half-thousand point bar is not a reward for
    /// weakening something, it is a shortcut past the fight the wave is.
    ///
    /// A virtual on the base rather than a list of type checks at the call sites,
    /// because the last version of this rule was `!(enemy is EnemyRatKing)` in
    /// PlayerProjectile and every boss written after the Rat King silently fell
    /// through it. A new boss now opts in where it is declared, next to its own
    /// health bar, instead of being remembered somewhere else.
    /// </summary>
    public virtual bool IsBoss => false;

    // Whether this counts as a member of whatever AMBUSH is open when it spawns.
    // Almost everything does — that is what makes "is this shift down yet" an
    // honest question rather than a spawn count, and it is why a boss's summons
    // hold his group open.
    //
    // An ogre does not. It is released on the wave's own clock and walks across
    // several shifts, so counting it would stall every handover behind one slow
    // body that was never part of any of them.
    public virtual bool JoinsAmbushes => true;

    // Kinship group, for equipment that reads "vermin take more damage" rather
    // than naming classes. See EnemyFamily.cs for why this is a virtual and not
    // a serialized prefab field.
    public virtual EnemyFamily Family => EnemyFamily.None;

    // "Enemies you have poisoned / set alight" counts each enemy once in its
    // life, not once per application — both statuses refresh on an already
    // affected target, and poison clouds re-apply every tick.
    private bool _countedPoisonApplied;
    private bool _countedIgniteApplied;

    protected virtual void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        glowManager = GetComponent<GlowManager>(); // Cache the component
        animator = GetComponent<Animator>(); // Cache the animator
        peakHealth = health;

        if (TracksWaveCompletion)
        {
            BaseWave.RegisterEnemy(gameObject, JoinsAmbushes); // Register for wave tracking
        }
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

        // Hits wear the ice down, whether or not the knight who threw them owns
        // Shatter; it breaks once they add up to the freezing knight's break
        // damage (5 with no Deep Freeze, +10 per rank). Poison and fire ticks
        // bypass this method entirely, so a burning body burns through the hold.
        //
        // Whatever broke it, the body walks away carrying nothing: it can be
        // chilled on the next touch and frozen on the one after, immediately.
        BreakFreezeIfHardEnough(damage);


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

        PlayAnimatorState(staggerAnimation, staggerStateName);

        yield return new WaitForSeconds(staggerDuration);

        // Always attempted, even if the stagger state never played: leaving an
        // enemy parked on a hurt frame is worse than never flinching at all.
        PlayAnimatorState(defaultAnimation, defaultStateName);

        isStaggered = false;
    }

    // Resolves a state from either authoring form — clip reference first, then the
    // name — and refuses quietly-wrong outcomes. Animator.Play on a state that
    // does not exist does nothing AND says nothing, which is exactly how a hurt
    // animation goes missing without anyone noticing it went.
    private void PlayAnimatorState(AnimationClip clip, string stateName)
    {
        if (animator == null) return;

        string state = clip != null ? clip.name : stateName;
        if (string.IsNullOrEmpty(state)) return;

        if (!animator.HasState(0, Animator.StringToHash(state)))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[{name}] Animator has no state '{state}'. If this enemy is " +
                             "driven by an .aseprite, the state name is the tag name — check it.");
#endif
            return;
        }

        animator.Play(state);
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
        // Same refusal as Ignite: no bubbles, no green glow, nothing to read as a
        // status the cart does not actually have
        if (isDead || ImmuneToAreaDamage) return;

        if (!_countedPoisonApplied)
        {
            _countedPoisonApplied = true;
            PlayerStats.Increment("applied.poison");
        }

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

                            // Hollow Fang: a death by venom is worth extra charge
                            var venomKnight = GameObject.FindWithTag(poisonSource.playerTag);
                            var venomEquipment = venomKnight != null ? venomKnight.GetComponent<EquipmentBoost>() : null;
                            if (venomEquipment != null && venomEquipment.PoisonDeathSpecial > 0)
                            {
                                GiveSpecialToPlayer(venomEquipment.PoisonDeathSpecial, poisonSource.playerTag);
                            }

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

    // Serpent Order death effects (Miasma clouds). Called from both death sites (arrow
    // and poison tick) right after isDead flips, so subclass OnDeath overrides can't
    // skip it. Plaguebringer has no death effect of its own — it makes the owner's
    // clouds hunt, which PoisonCloud works out from the tag it is given here.
    protected void TriggerPoisonDeathEffects()
    {
        if (!isPoisoned || poisonSources.Count == 0) return;

        PlayerStats.Increment("kills.poisoned");

        // One cloud per death, using the strongest contributor's Miasma level
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
        }

        if (bestMiasmaLevel > 0)
        {
            PoisonCloud.Spawn(transform.position, bestMiasmaLevel, miasmaOwnerTag, _lastMoveHeading);
        }
    }

    // ================= Ember Order =================

    // The player's ignition doors: PlayerProjectile (an arrow that carries ignite),
    // FireballProjectile, and ShieldSight once Fire Sight is bought. Every one of these
    // is aimed, so they stay the only way NEW fire enters a wave. The first two are
    // also SPENT — a shot each — which is why they may stack. The beam is neither, so
    // it lights only what is not already alight and never stacks.
    //
    // Fire started by fire never comes through here at all, and as of 2026-09-08 it
    // does not happen: burning ground COOKS what stands in it and a burning body COOKS
    // what it touches, and neither lights anything. Ground fire runs entirely through
    // the fire-AoE flag — a flag, deliberately, and not a call to Ignite, because
    // ground fire is not scarce and a stack per overlapping drop is what produced 40-70
    // damage a step.
    //
    /// <summary>
    /// Takes every status this enemy is carrying back off — the poison debt and
    /// all its sources, every burn stack — and the tells with them.
    ///
    /// One funnel, for the same reason TakeDamage is one: an effect that cleared
    /// poison but not fire, or the numbers but not the bubbles, would read as a
    /// bug rather than as a cleanse. Nothing in the game does this to an enemy
    /// except the Overseer's roar, and that is exactly why it belongs here and
    /// not in his file — the next thing that shrugs a status off asks for this
    /// instead of writing its own half of it.
    ///
    /// The flame is NOT stopped here: LateUpdate already puts it out on the first
    /// frame it finds nothing burning, so doing it twice is how the two disagree.
    /// </summary>
    public virtual void PurgeStatusEffects()
    {
        if (poisonCoroutine != null)
        {
            StopCoroutine(poisonCoroutine);
            poisonCoroutine = null;
        }
        isPoisoned = false;
        poisonTimer = 0f;
        poisonDamage = 0;
        lastPoisonTick = 0f;
        poisonSources.Clear();

        if (poisonBubbles != null)
        {
            poisonBubbles.StopBubblesAndDetach();
            poisonBubbles = null;
        }

        _burnStacks.Clear();

        // The ground's hold on it goes too, tail and all. A purged body standing in a
        // lane simply catches again on the next beat, the way it would have if it had
        // never burned — which is the right answer for a cleanse: it undoes the state,
        // it does not make the floor safe.
        _fireAoeUntil = 0f;
        _fireAoeDps = 0f;
        _fireAoeOwnerTag = null;
        _fireAoeBoost = null;
        _fireAoeIgnites = false;

        // A cleanse that left it asleep would be the one status the roar could
        // not take off, which is exactly the promise PurgeStatusEffects makes.
        WakeUp();

        // And the same goes for the ice, however long it had left to run.
        PurgeFrost();
    }

    // Each call adds an independent burn stack, so hitting a burning enemy with
    // another ignited arrow/fireball piles heat on rather than merely refreshing.
    public void Ignite(string playerTag)
    {
        AddBurn(playerTag);
    }

    // Burn stacks come from AIMED fire only — an arrow that rolled ignite, or a
    // fireball. Those are scarce and spent, so stacking them is the reward for landing
    // several. Ground fire deliberately does NOT come through here: it sets the
    // fire-AoE flag instead, because ground fire is not scarce and stacking it
    // multiplies by however many drops happen to overlap.
    //
    // Fire Sight comes through here too but can never stack: it is a beam that runs
    // every frame, so ShieldSight only calls this for a body that is not already on
    // fire. One ignition per crossing, however long it is held in the light.
    private void AddBurn(string playerTag)
    {
        // Refused outright rather than merely dealing no damage, so an iron cart
        // never wears a flame it cannot be hurt by
        if (isDead || ImmuneToAreaDamage || string.IsNullOrEmpty(playerTag)) return;

        if (!_countedIgniteApplied)
        {
            _countedIgniteApplied = true;
            PlayerStats.Increment("applied.ignite");
        }

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

        StartEmberFlame(EmberBoost.IgniteDuration);
    }

    // Visible flame riding the enemy, so a burning enemy reads as on fire and not
    // merely tinted. Re-igniting just resumes the existing one. Called by the aimed
    // ignition doors, and by the fire-AoE flag on the rare path where burning ground is
    // allowed to set a body alight — a body alight by any route has to look alight.
    private void StartEmberFlame(float glowSeconds)
    {
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
        glowManager?.StartGlow(new Color(1f, 0.45f, 0.12f), glowSeconds, 6f, 0.8f);
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

    /// <summary>
    /// The knight whose Ember sheet drives this body's trail and panic. An aimed burn
    /// wins when there is one — it is the fire the player actually spent — and the
    /// ground it is standing on fills in otherwise.
    /// </summary>
    private EmberBoost DominantFireBoost()
    {
        string tag;
        EmberBoost boost;
        BurnDps(out tag, out boost);
        return boost != null ? boost : _fireAoeBoost;
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
    //
    // While this enemy is asleep its own behaviour is switched off, so Unity is
    // not the caller: SleepFreeze runs this same pass for it. Everything below has
    // to keep working through a nap — a held enemy still burns, still rots, and
    // still catches from the ground it was put down on.
    protected virtual void LateUpdate()
    {
        if (isDead) return;

        // Ice pins a body exactly as sleep does. Kept as its own anchor rather
        // than sharing sleep's, so a body that is both slept and frozen goes back
        // to wherever the FIRST hold put it down and the second cannot shift it.
        if (IsFrozen) transform.position = _frozenAnchor;

        // A sleeping body stays exactly where it was put down. Belt and braces:
        // with Update switched off nothing should be moving it, so this only
        // catches a shove from somewhere else — and it costs one assignment.
        // Ember reads its own movement further down as the delta against the last
        // frame's position, so a pinned body correctly lays no fire trail and
        // takes no panic nudge; both fall to zero on their own.
        if (IsAsleep) transform.position = _sleepAnchor;

        if (!_emberTrackingStarted)
        {
            _emberTrackingStarted = true;
            _previousPosition = transform.position;
            _lastTrailPosition = transform.position;
        }

        // Read here, before Searing Panic, chill and shove touch the position, so the
        // gap since last frame is only the subclass's own walking. A held or idle
        // frame moves nothing and keeps the heading it had.
        Vector3 ownMove = transform.position - _previousPosition;
        if (ownMove.sqrMagnitude > 1e-8f) _lastMoveHeading = ((Vector2)ownMove).normalized;

        bool burning = PruneBurnStacks() || IsBurningFromGround;
        if (!burning && _emberFlame != null && _emberFlame.emission.enabled)
        {
            var em = _emberFlame.emission;
            em.enabled = false;
            _emberFlame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // Contact is damage and nothing else: a burning body cooks its neighbours and
        // never lights them. The GROUND is the only thing that lights anything, and it
        // does that through the fire-AoE flag in TickFire rather than from here.
        if (EmberBoost.FireSpreadEnabled && !ImmuneToAreaDamage)
        {
            _sinceSpreadCheck += Time.deltaTime;
            if (_sinceSpreadCheck >= EmberBoost.SpreadCheckInterval)
            {
                _sinceSpreadCheck = 0f;
                if (IsIgnited) BurnNeighboursByContact();
            }
        }

        if (burning)
        {
            // One knight's sheet owns trail + panic this frame — the hottest aimed
            // burn, or the ground if this body is only alight because of it
            EmberBoost dominantBoost = DominantFireBoost();
            ApplySearingPanic(dominantBoost);
            DropFireTrail(dominantBoost);
        }

        // After the panic nudge, deliberately: a body that is both burning and
        // chilled has the fire's extra ground taken back off it along with the
        // rest, which is the honest composition of the two. Unconditional, unlike
        // the block above — cold is not carried by anything the way a burn is.
        // A freeze that ran OUT rather than being broken. Noticed here rather than
        // in SleepFreeze so the tells, the pin and the frozen tally all come off on
        // the same frame the deadline passes. Outlasting one buys the body nothing:
        // the next touch of cold can chill it and the one after can stop it again.
        if (_frozenUntil > 0f && !IsFrozen) Thaw();

        ApplyChillSlow();
        UpdateFrostTells();
        TickFrostBite();

        // Last, and deliberately before _previousPosition is captured: Searing Panic
        // reads the gap between frames as "movement this body made" and multiplies it,
        // so a shove paid out any earlier would be amplified by a burn the knight had
        // nothing to do with.
        ApplyShove();

        // Every change to the position this frame has landed by now, and
        // _previousPosition still holds where last frame ended
        TrackMeasuredSpeed();

        _previousPosition = transform.position;

        TickFire();
    }

    private void TrackMeasuredSpeed()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float step = ((Vector2)(transform.position - _previousPosition)).magnitude;
        if (step > MeasuredSpeedMaxStep) return;

        float blend = 1f - Mathf.Exp(-dt / MeasuredSpeedSmoothing);
        _measuredSpeed = Mathf.Lerp(_measuredSpeed, step / dt, blend);
    }

    // Multiplies whatever movement the subclass just did, along its own heading.
    // Works regardless of how that enemy moves (MoveTowards, Lerp, waypoints).
    // Whether a burning body can be made to run faster. False for anything whose
    // position is owned by something else — a cart on rails cannot bolt, and
    // nudging it here only puts it a frame's-worth off the track before its own
    // Update snaps it back.
    protected virtual bool AcceptsSearingPanic => true;

    private void ApplySearingPanic(EmberBoost boost)
    {
        if (boost == null || IsStaggered || !AcceptsSearingPanic) return;

        float multiplier = boost.PanicSpeedMultiplier;
        if (multiplier <= 1f) return;

        Vector3 delta = transform.position - _previousPosition;
        if (delta.sqrMagnitude <= 1e-8f) return;

        transform.position += delta * (multiplier - 1f);
    }

    // ---- Bulwark's shove ----

    // NO COOLDOWN (owner's call, 2026-09-08). Every contact with the guard throws the
    // body, however fast they come — a player flicking the shield onto something can
    // keep shoving it, and that is the intended power level.
    //
    // Safe without one for two reasons. The shield carries exactly ONE trigger
    // (ShieldShape disables the authored capsule precisely so a block cannot fire its
    // callbacks twice), so a single contact cannot double-report. And Shove REPLACES
    // the outstanding displacement rather than adding to it, so a body shoved twice
    // inside one payout window travels two units from wherever it had got to, never
    // four.
    private Vector3 _shoveRemaining;
    private float _shoveSecondsLeft;

    /// <summary>
    /// Bulwark's answer to a body arriving on the guard, in one place because THREE
    /// separate trigger handlers need it.
    ///
    /// EnemyGiantSlime and EnemyRatKing both override OnTriggerEnter2D wholesale and
    /// never call base — they were written to say "a boss cannot be popped by
    /// contact", which predates this capstone by a long way. That is why Bulwark
    /// silently did nothing to the Crimson Twins until 2026-09-08: not a collider
    /// problem, just a branch that was never there. Anything that overrides the
    /// trigger from here on has to call this, or it quietly opts its enemy out.
    ///
    /// Returns true if the body was thrown, in which case the caller must not also
    /// charge the knight or destroy the enemy.
    /// </summary>
    protected bool TryBulwarkShove(Collider2D shield)
    {
        GuardianBoost guardian = shield.GetComponentInParent<GuardianBoost>();
        if (guardian == null || !guardian.HasBulwark) return false;

        Transform knight = shield.transform.parent;
        Vector2 away = knight != null
            ? (Vector2)(transform.position - knight.position)
            : (Vector2)shield.transform.right;
        if (away.sqrMagnitude < 1e-6f) away = shield.transform.right;

        Shove(away, GuardianBoost.BulwarkShoveDistance, GuardianBoost.BulwarkShoveSeconds);
        GuardianFx.ShoveBurst(shield.bounds.center, away.normalized);
        PlayerStats.Increment("guardian.shoved");
        return true;
    }

    /// <summary>
    /// Throw this body <paramref name="distance"/> units along <paramref name="direction"/>,
    /// paid out over <paramref name="seconds"/> rather than all at once — a body that
    /// jumped two units between frames reads as a glitch rather than as a shield bash.
    ///
    /// Works whatever the subclass does to move itself (MoveTowards, Lerp, waypoints),
    /// for the same reason Searing Panic does: it is applied after their Update, on top
    /// of whatever they did. Refused for anything whose position is owned by something
    /// else — a cart on rails cannot be pushed off its track, and trying only puts it a
    /// frame's-worth out of place before its own Update snaps it back.
    ///
    /// Calling it again before the last one has finished paying out REPLACES the
    /// outstanding displacement; it never stacks.
    /// </summary>
    public void Shove(Vector2 direction, float distance, float seconds)
    {
        if (isDead || positionIsScripted) return;
        if (distance <= 0f || direction.sqrMagnitude < 1e-6f) return;

        // Replaces rather than accumulates — see the field comments above.
        _shoveRemaining = (Vector3)(direction.normalized * distance);
        _shoveSecondsLeft = Mathf.Max(0.01f, seconds);
    }

    private void ApplyShove()
    {
        if (_shoveSecondsLeft <= 0f) return;

        float step = Mathf.Min(Time.deltaTime, _shoveSecondsLeft);
        Vector3 delta = _shoveRemaining * (step / _shoveSecondsLeft);

        transform.position += delta;

        // Both pins move with it. Otherwise a body shoved and then frozen or slept
        // would be dragged back to wherever it was standing when the hold took it,
        // and the shove the player just watched would silently undo itself.
        _frozenAnchor += delta;
        _sleepAnchor += delta;

        _shoveRemaining -= delta;
        _shoveSecondsLeft -= step;
    }

    // Takes back part of whatever movement the subclass just made, along its own
    // heading — the exact mirror of Searing Panic above, and it covers the same
    // sweep of enemies for the same reason: rat, bat, wolf, ogre, slime, the Rat
    // King and the Giant Slime all move themselves by a delta, and none of them
    // has to be taught anything for this to reach them.
    //
    // Not gated on IsStaggered, unlike panic: a body that is not moving has a zero
    // delta and falls out on the next line anyway, and a held one is pinned above.
    private void ApplyChillSlow()
    {
        if (!IsChilled || !AcceptsChill || positionIsScripted) return;

        float multiplier = ChillSpeedMultiplier;
        if (multiplier >= 1f) return;

        Vector3 delta = transform.position - _previousPosition;
        if (delta.sqrMagnitude <= 1e-8f) return;

        transform.position -= delta * (1f - multiplier);
    }

    // What the player actually reads. Three channels, and each is picked because
    // nothing else in the game is using it:
    //
    //   * spriteRenderer.color, NOT the GlowManager. StartGlow is first-come and
    //     exclusive, so a frost glow would be swallowed by the red hit flash half
    //     the time and would block it the rest. No enemy code writes the sprite
    //     colour, so cold gets a channel of its own that never fights poison or
    //     fire for it — a body can honestly be green, on fire, and blue at once.
    //   * animator.speed, which nothing in the codebase assigns. A walk cycle at
    //     full tempo on a half-speed body reads as ice-skating; this is the whole
    //     difference between "slowed" and "broken".
    //   * drifting motes, and a brighter shell once it is actually stopped.
    private void UpdateFrostTells()
    {
        bool cold = IsChilled || IsFrozen;

        if (spriteRenderer != null)
        {
            if (cold && !_frostTinted)
            {
                _frostTinted = true;
                _frostUntintedColor = spriteRenderer.color;
            }
            if (cold)
            {
                Color tint = IsFrozen ? FrozenTint : ChilledTint;
                spriteRenderer.color = _frostUntintedColor * tint;
            }
            else if (_frostTinted)
            {
                _frostTinted = false;
                spriteRenderer.color = _frostUntintedColor;
            }
        }

        if (animator != null)
        {
            // Zero rather than "very slow" while frozen: a body in ice is a
            // statue, and a statue that is still twitching is a bug.
            animator.speed = IsFrozen ? 0f : (IsChilled ? ChillSpeedMultiplier : 1f);
        }

        if (cold && _frostMotes == null)
        {
            _frostMotes = FrostFx.AttachEnemyChill(gameObject);
        }
        if (_frostMotes != null)
        {
            var emission = _frostMotes.emission;
            if (emission.enabled != cold)
            {
                emission.enabled = cold;
                if (!cold) _frostMotes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                else _frostMotes.Play();
            }
        }
    }

    private static readonly Color ChilledTint = new Color(0.62f, 0.82f, 1f);
    private static readonly Color FrozenTint = new Color(0.72f, 0.92f, 1f);

    // The lane carries this enemy's id so it can walk over its own drippings without
    // being billed for them on top of the burn that laid them.
    private void DropFireTrail(EmberBoost boost)
    {
        if (boost == null || !boost.HasFireTrail) return;

        _sinceTrailDrop += Time.deltaTime;
        if (_sinceTrailDrop < EmberBoost.TrailDropInterval) return;
        _sinceTrailDrop = 0f;

        // Only paint where the enemy actually travelled
        if ((transform.position - _lastTrailPosition).sqrMagnitude < MinTrailMoveSqr) return;

        // A drop outside the camera view is refused by FireField.Add, not by anything
        // here — waves spawn off screen and walk in, and a burning enemy on its approach
        // must not paint the corridor every later spawn comes down. The drop is SKIPPED
        // rather than banked: the cadence above has already reset and _lastTrailPosition
        // moves below either way, so a body that crosses the edge mid-interval starts
        // painting on the normal beat instead of dumping a held zone the moment it
        // becomes visible.
        boost.PlaceTrailZone(transform.position,
            boost.TrailZoneRadius,
            boost.TrailZoneDuration,
            gameObject.GetInstanceID());
        _lastTrailPosition = transform.position;
    }

    /// <summary>
    /// Reads the ground once a beat and sets the fire-AoE flag from it. ONE query, ONE
    /// flag: the hottest zone underfoot supplies the rate and the kill credit, and a
    /// single bit says whether that ground lights things. Overlapping zones cannot
    /// multiply either of those, which is the whole point of the flag.
    ///
    /// While the body is standing in fire the deadline is pushed forward every beat;
    /// when it walks out, the last push is what keeps it burning for the linger.
    /// </summary>
    private void UpdateFireAoeFlag()
    {
        // The tail has run out: forget the ground entirely, so the next patch this
        // body steps into starts it cleanly rather than inheriting the last one.
        if (Time.time >= _fireAoeUntil) _fireAoeIgnites = false;

        bool wasBurningFromGround = IsBurningFromGround;

        float zoneDps;
        string zoneOwner;
        bool zoneIgnites;
        bool inFire = FireField.Sample(transform.position, gameObject.GetInstanceID(),
            out zoneDps, out zoneOwner, out zoneIgnites);

        // Set every beat, true or false — this is the billing gate, and it has to fall
        // the moment the body is off the fire rather than when the flag lapses.
        _standingInFire = inFire;

        if (!inFire) return;

        _fireAoeUntil = Time.time + EmberBoost.FireAoeLinger;
        _fireAoeDps = zoneDps;

        // Only ever raised here, never lowered, so a body crossing from igniting ground
        // onto ordinary ground stays alight for the rest of the linger rather than
        // having the second zone put the first one's fire out.
        if (zoneIgnites) _fireAoeIgnites = true;

        // Resolving the knight is a tag lookup, so only do it when the owner actually
        // changes — a body standing in one lane pays for it once, not four times a
        // second.
        if (zoneOwner != _fireAoeOwnerTag)
        {
            _fireAoeOwnerTag = zoneOwner;
            GameObject owner = string.IsNullOrEmpty(zoneOwner) ? null : GameObject.FindWithTag(zoneOwner);
            _fireAoeBoost = owner != null ? owner.GetComponent<EmberBoost>() : null;
        }

        // The flame only comes on if the ground is the kind that lights things —
        // ordinary fire burns you where you stand and leaves nothing on you — and only
        // on the way IN. Restarting the glow four times a second while a body stands
        // in a lane would be the same picture at four times the cost.
        if (!wasBurningFromGround && IsBurningFromGround) StartEmberFlame(EmberBoost.FireAoeLinger);
    }

    // A burning body SCORCHES what it touches — it does not light it (owner's call,
    // 2026-09-07). Contact used to hand over a burn, which meant one ignited arrow
    // into a packed lane lit the whole lane and every newly-lit body lit its own
    // neighbours; the fire on the board stopped being anything the player granted.
    // Now a burning enemy simply cooks the bodies pressed against it, at the same
    // rate it is burning at, and the ignition doors stay arrow, fireball and nothing
    // else. Touching a burning enemy is still a real cost — it just cannot be caught.
    private void BurnNeighboursByContact()
    {
        string dominantTag;
        EmberBoost dominantBoost;
        float dps = BurnDps(out dominantTag, out dominantBoost);
        if (dps <= 0f) return;

        Collider2D[] neighbours = Physics2D.OverlapCircleAll(transform.position,
            EmberBoost.ContactSpreadRadius);

        for (int i = 0; i < neighbours.Length; i++)
        {
            EnemyBase other = neighbours[i].GetComponent<EnemyBase>();
            if (other == null || other == this || other.IsDead || other.ImmuneToAreaDamage) continue;
            other.ReceiveContactFire(dps, dominantTag);
        }
    }

    /// <summary>
    /// Told by a burning neighbour that it is being cooked. Held as a rate with an
    /// expiry rather than damage applied on the spot, so it flows through the same
    /// once-a-second flush as every other fire and a body between two burning
    /// neighbours still only pays the hotter of them — see <see cref="TickFire"/>.
    /// </summary>
    public void ReceiveContactFire(float dps, string ownerTag)
    {
        if (isDead || ImmuneToAreaDamage || dps <= 0f) return;

        // Hotter wins for the rest of this beat; the window is a beat and a half so
        // a spread check that lands just after a fire tick is not lost.
        if (Time.time >= _contactFireUntil || dps > _contactFireDps)
        {
            _contactFireDps = dps;
            _contactFireOwnerTag = ownerTag;
        }
        _contactFireUntil = Time.time + EmberBoost.SpreadCheckInterval * 1.5f;
    }

    // Fire damage has two sources — being ignited (on-body) and the field (ground fire,
    // or a burning body in contact) — and an enemy pays BOTH, on separate clocks, as
    // separate numbers. Hottest-wins survives only WITHIN the field channel, where it
    // is doing real work: overlapping zones are one fire, and a zone next to a burning
    // neighbour is still one fire.
    //
    // An ignited enemy walking its OWN trail still isn't billed twice for one fire, but
    // that is now FireField.Sample's job rather than this one's — it skips the zones
    // that body dripped. Standing in somebody else's fire while carrying your own burn
    // is two fires and costs two fires.
    //
    // Enemies poll the field rather than the field damaging enemies; that one-way
    // direction is what keeps FireField structurally unable to reach Ignite().
    private void TickFire()
    {
        // Not merely zero damage — the field is never sampled, so a cart parked in
        // a fire zone costs nothing to have on the track
        if (ImmuneToAreaDamage) return;

        _sinceFireTick += Time.deltaTime;
        if (_sinceFireTick < FireTickInterval) return;

        float elapsed = _sinceFireTick;
        _sinceFireTick = 0f;

        // Read the ground once, here, and let the flag carry the answer everywhere
        // else. This is the only place the field is sampled.
        UpdateFireAoeFlag();

        // ---- Channel 1: ignition ----
        //
        // The SUM of every live burn stack, each sized to its igniting knight's Ember
        // investment. Stacking is what makes a second ignited arrow pile heat on
        // instead of merely refreshing, and it is confined to AIMED ignitions for
        // exactly that reason — ground fire never adds a stack.
        if (IsIgnited)
        {
            string dominantTag;
            EmberBoost dominantBoost;
            float burnDps = BurnDps(out dominantTag, out dominantBoost);
            if (burnDps > 0f)
            {
                _pendingIgniteDamage += burnDps * elapsed;
                // Survives to the flush even if the burn expires in the meantime
                _lastIgniteOwnerTag = dominantTag;
            }
        }

        // ---- Channel 2: the field ----
        //
        // Ground fire is ONE zone's worth whether the body stands in one lane or in
        // six overlapping ones. Billed only for the beats the body is ACTUALLY in it —
        // the flag's 2s linger keeps the state from chattering, it does not keep the
        // meter running (see _standingInFire). A burning neighbour in contact competes
        // for the same channel on the same hottest-wins rule: being in a fire zone next
        // to a burning body bills one field, not two.
        //
        // THIS CHANNEL IS OFF SCREEN-GATED and the ignition channel above is not.
        // FireField.Sample refuses any point outside the camera view, so a body that
        // has not walked on yet pays nothing for ground it is standing on — while a
        // body an arrow lit goes on burning wherever it goes, because that fire rides
        // the body rather than the floor.
        float fieldDps = 0f;
        string fieldOwner = null;

        if (_standingInFire)
        {
            fieldDps = _fireAoeDps;
            fieldOwner = _fireAoeOwnerTag;
        }

        if (Time.time < _contactFireUntil && _contactFireDps > fieldDps)
        {
            fieldDps = _contactFireDps;
            fieldOwner = _contactFireOwnerTag;
        }

        if (fieldDps > 0f)
        {
            _pendingFieldDamage += fieldDps * elapsed;
            _lastFieldOwnerTag = fieldOwner;
        }

        // ---- The flushes ----
        //
        // Each channel lands as ONE number per second, so the figure on screen IS that
        // channel's dps: a base burn pops "3", a full build "7" or "8". Flushing on every
        // accumulator crossing instead showed a stream of "1"s that read as 1 dps no
        // matter the actual rate.
        //
        // Both timers advance every tick and reset only when they fire, so the half
        // second they start out of phase is a half second they stay out of phase — the
        // two figures alternate for as long as the body is paying both.
        _sinceIgniteFlush += elapsed;
        if (_sinceIgniteFlush >= FireFlushInterval)
        {
            _sinceIgniteFlush = 0f;
            FlushFireChannel(ref _pendingIgniteDamage, _lastIgniteOwnerTag, IgniteDamageColor);
        }

        _sinceFieldFlush += elapsed;
        if (_sinceFieldFlush >= FireFlushInterval)
        {
            _sinceFieldFlush = 0f;
            FlushFireChannel(ref _pendingFieldDamage, _lastFieldOwnerTag, FieldDamageColor);
        }
    }

    // Fractions are carried, never dropped: a channel running at 3.5 dps pays 3 one
    // second and 4 the next rather than 3 forever.
    private void FlushFireChannel(ref float pending, string ownerTag, Color color)
    {
        if (pending < 1f) return;

        int whole = Mathf.FloorToInt(pending);
        pending -= whole;
        ApplyFireDamage(whole, ownerTag, color);
    }

    // Deliberately NOT routed through TakeDamage: that fires StaggerRoutine, and a
    // once-a-second stagger would stun-lock anything standing in fire (subclasses
    // skip movement while staggered), freezing the very movement Fire Trail needs.
    // Mirrors the poison tick's inline damage path instead.
    protected void ApplyFireDamage(int damage, string ownerTag, Color color)
    {
        if (isDead || ImmuneToAreaDamage || damage <= 0) return;

        peakHealth = Mathf.Max(peakHealth, health);
        health -= damage;
        ShowDamageText(damage, color); // orange for a burn, gold for the ground

        if (health > 0)
        {
            PlayBurnTick();
            return;
        }

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

    /// <summary>
    /// Frost Bite (Frigid capstone): cold that kills.
    ///
    /// Charged only while this body is actually chilled or frozen AND the knight
    /// whose cold is on it owns the capstone, so every other build pays nothing for
    /// this beyond the two field reads below.
    ///
    /// The rate is the Order's own cycle priced as damage: a slowed body bills less
    /// than a stopped one, so "chill it, then stop it" is also the damage upgrade.
    /// </summary>
    private void TickFrostBite()
    {
        // Not merely zero damage — a cart parked in a ward costs nothing to have on
        // the track, the same exemption fire makes.
        if (!_frostBiteActive || ImmuneToAreaDamage) return;

        bool frozen = IsFrozen;
        if (!frozen && !IsChilled)
        {
            // Cold gone: drop the part-charged tick rather than banking it for the
            // next chill. A body that walked out of a ward has genuinely shrugged it
            // off, and saving the fraction would let a knight sweep a crowd in and
            // out of the ring to stack up free damage.
            _pendingFrostDamage = 0f;
            _sinceFrostFlush = 0f;
            return;
        }

        float dps = frozen ? FrigidBoost.FrostBiteFrozenDps : FrigidBoost.FrostBiteChilledDps;
        _pendingFrostDamage += dps * Time.deltaTime;

        _sinceFrostFlush += Time.deltaTime;
        if (_sinceFrostFlush < FrigidBoost.FrostBiteFlushInterval) return;
        _sinceFrostFlush = 0f;

        if (_pendingFrostDamage < 1f) return;

        int whole = Mathf.FloorToInt(_pendingFrostDamage);
        _pendingFrostDamage -= whole;
        ApplyFrostDamage(whole, _frostBiteOwnerTag);
    }

    /// <summary>
    /// Damage from Frost Bite. Mirrors ApplyFireDamage, with one difference that
    /// matters: it NEVER calls BreakFreezeIfHardEnough.
    ///
    /// Cold cannot break its own ice. Ice damage adds up across hits, so if these
    /// ticks counted, the capstone would wear down and break every statue it makes.
    /// </summary>
    private void ApplyFrostDamage(int damage, string ownerTag)
    {
        if (isDead || ImmuneToAreaDamage || damage <= 0) return;

        peakHealth = Mathf.Max(peakHealth, health);
        health -= damage;
        ShowDamageText(damage, new Color(0.55f, 0.85f, 1f)); // pale ice

        if (health > 0) return;

        isDead = true;
        // A poisoned body finished off by cold still owes Serpent its death effects,
        // exactly as one finished off by fire does.
        TriggerPoisonDeathEffects();
        PlayerStats.Increment("kills.frostbitten");

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

    // Shortest gap allowed between two burn puffs anywhere on screen.
    private const float BurnTickSoundInterval = 0.2f;
    private static float _lastBurnTickSound = -1f;

    // Burn damage lands once a second per burning enemy, and nothing synchronises
    // those clocks — a pack caught by one Fire Trail would otherwise fire a dozen
    // puffs inside the same second and turn the whole thing into static. The sound
    // is a texture for "something is burning", not a per-enemy readout, so one
    // voice at a time across the whole field is exactly right.
    private static void PlayBurnTick()
    {
        if (AudioManager.Instance == null) return;

        // >= guards the case where the clock ran backwards on us: with domain
        // reload off, this static outlives the play session that stamped it, and a
        // plain subtraction would then read as "0.2s hasn't passed" forever.
        float now = Time.time;
        if (now >= _lastBurnTickSound && now - _lastBurnTickSound < BurnTickSoundInterval) return;

        _lastBurnTickSound = now;
        AudioManager.Instance.PlaySFX(AudioManager.Instance.burnTick);
    }

    // A blast — a powder keg going up. Like poison and fire it sidesteps
    // TakeDamage, because the stagger that comes with a normal hit would freeze
    // whatever the blast caught for a moment at exactly the point the player is
    // trying to read the chaos. It gets its own colour for the same reason those
    // do: on screen, white numbers mean "something detonated near this", which is
    // information the player cannot otherwise get from a crowd of red.
    //
    // ownerTag credits the knight who set it off, so blowing a keg over a pack
    // pays them the special for everything it kills.
    //
    // Carts shrug this off with the rest of the area damage. Kegs are carts too,
    // so keg-to-keg coupling does NOT come through here — see
    // EnemyKegCart.ChainDetonate, which is sympathetic detonation rather than
    // damage and is the one thing allowed past the immunity.
    public void ApplyBlastDamage(int damage, string ownerTag)
    {
        if (isDead || ImmuneToAreaDamage || damage <= 0) return;

        peakHealth = Mathf.Max(peakHealth, health);
        health -= damage;
        BreakFreezeIfHardEnough(damage);
        ShowDamageText(damage, Color.white);

        if (health > 0) return;

        isDead = true;
        TriggerPoisonDeathEffects();
        PlayerStats.Increment("kills.blasted");

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
        // Killed mid-ice: give the frozen tally back before isDead makes IsFrozen
        // read false and the body stops being able to account for itself. Shatter
        // kills go through here, so this is not a rare path.
        Thaw();

        // Killed mid-nap: hand the behaviour back before anything downstream runs
        // on a corpse that is still switched off
        SleepFreeze.Release(this);

        if (goldOnDeath > 0)
        {
            GoldManager.Instance?.AddGold(goldOnDeath);
        }

        PlayerStats.Increment($"kills.{StatKey}");

        if (Family != EnemyFamily.None)
        {
            PlayerStats.Increment($"kills.family.{Family.ToString().ToLowerInvariant()}");
        }

        // Per-map kill counts, for quests scoped to one map ("clear the camp
        // fields"). Null on a scene with no wave manager, e.g. a test bed.
        var map = WaveManager.ActiveInstance != null ? WaveManager.ActiveInstance.CurrentMap : null;
        if (map != null && !string.IsNullOrEmpty(map.MapId))
        {
            PlayerStats.Increment($"kills.map.{map.MapId}");
        }

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

            // Bulwark (Guardian capstone): the guard stops being a trade.
            //
            // Every other knight in the game buys this body's removal with their own
            // health — the branch below deletes the enemy and charges the knight for
            // it. Bulwark refuses the exchange: nothing is paid, nothing is deleted,
            // the body is simply thrown off the shield and has to come again. It is
            // the Order's pillar taken to its end, that being hit is never the thing
            // that pays out.
            if (TryBulwarkShove(other)) return;

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
