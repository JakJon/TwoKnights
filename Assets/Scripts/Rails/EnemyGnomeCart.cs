using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Shared machinery for a gnome riding a mine cart and throwing something at the
// knights. Subclasses decide two things and nothing else: WHEN the throw goes out
// (ShouldRelease) and WHAT it is (Release).
//
// Everything here is timing, and the timing has a subtlety worth stating. Each
// rider honours its own cooldown, but riders on a looping track are spaced evenly
// around it BY DESIGN, so two gnomes with a ten second cooldown apiece produce a
// throw every five seconds — each timer perfectly correct, the wave twice as
// heavy as authored. So there are two gates: the rider's own, and one shared by
// every gnome of the same kind. The cooldown is a property of the WAVE, not of
// the individual, and only the shared gate can express that.
//
// The other half of that arithmetic is what happens as a shift is cleared. Six
// gnomes throwing on a shared cooldown is a steady drumbeat; the last one alive,
// on the same cooldown, is near silence — so the tail of every shift went quiet
// exactly when the player had the least left to do, and the safest way to play a
// shift was slowly. So the cooldown SHORTENS as the riders thin out, and it does
// so as three waits authored outright rather than as fractions of one: the beat
// the player hears is the whole design, and a multiplier states it in a unit
// nobody can hear. The pressure a shift applies is meant to be a property of the
// shift, not of how much of it is still standing, and killing gnomes has to buy
// space rather than silence.
//
// Killing the rider does not clear the track: the cart keeps rolling as an empty
// wreck, still 20 more hit points of obstacle.
public abstract class EnemyGnomeCart : EnemyMineCart
{
    // The wave-wide half of the cooldown. One instance per concrete gnome type —
    // bomb throwers pace bomb throwers, pickaxe throwers pace pickaxe throwers,
    // and the two kinds do not suppress each other.
    //
    // It records WHEN the last throw went out rather than when the next one is
    // allowed, which is what lets the thinning multiplier apply retroactively: a
    // gnome killed during someone else's cooldown shortens the wait that is
    // already running instead of only the one after it.
    protected sealed class ThrowGate
    {
        public float LastThrowAt = float.NegativeInfinity;
        public float StampedAt = float.NegativeInfinity;
    }

    // Every rider currently on the track, of every kind. The thinning multiplier
    // counts gnomes the way the player does — one gnome left is one gnome left,
    // whether he throws rock or powder — so a bomb thrower and a pickaxe thrower
    // alive together are two, and neither gets the last-one-standing rate.
    private static readonly List<EnemyGnomeCart> LiveRiders = new List<EnemyGnomeCart>();

    [Header("Throwing")]
    [Tooltip("Seconds after spawn before this rider's first throw can go out")]
    [SerializeField] protected float armDelay = 8f;

    [Tooltip("Seconds between throws while three or more riders are up. Enforced per rider AND across every gnome of this kind.")]
    [SerializeField] protected float throwCooldown = 10f;

    [Tooltip("The wait once only two riders are left anywhere on the track.")]
    [SerializeField] protected float throwCooldownTwoLeft = 5f;

    [Tooltip("The wait once one rider is left. This is the fastest the kind can ever throw.")]
    [SerializeField] protected float throwCooldownOneLeft = 3.5f;

    [Tooltip("Offset from the cart's pivot to where the throw leaves it")]
    [SerializeField] protected Vector2 throwOffset = new Vector2(0f, 0.2f);

    [Tooltip("Played the moment the throw goes out. Lives on the prefab so each rider can sound like its own weapon.")]
    [SerializeField] protected SoundEffect throwSound;

    [Header("Wreck")]
    [Tooltip("Left rolling in the rider's place when he dies. Needs a MineCart.")]
    [SerializeField] private GameObject wreckPrefab;

    private float _armedAt;
    private float _lastThrowAt = float.NegativeInfinity;

    /// <summary>The gate every gnome of this concrete type shares.</summary>
    protected abstract ThrowGate Gate { get; }

    /// <summary>Is this the frame to throw? Called only once both cooldowns are up.</summary>
    protected abstract bool ShouldRelease();

    /// <summary>Put the projectile in the air. Only called when ShouldRelease said so.</summary>
    protected abstract void Release();

    /// <summary>A human name for the dev log — "bomb", "pickaxe".</summary>
    protected abstract string ThrowName { get; }

    // The rider is the kill; the cart he is sitting in is not
    protected override bool TracksWaveCompletion => true;

    // ...and by the same split, the rider is meat. The cart under him shrugs off
    // fire and poison; he does not, so Serpent and Ember still have an answer to
    // the mine. Killing him leaves the wreck, which is iron again and immune.
    public override bool ImmuneToAreaDamage => false;

    protected override void Awake()
    {
        base.Awake();
        if (Cart != null) Cart.Teleported += HandleTeleported;
        if (!LiveRiders.Contains(this)) LiveRiders.Add(this);
    }

    protected virtual void OnDestroy()
    {
        if (Cart != null) Cart.Teleported -= HandleTeleported;
        LiveRiders.Remove(this);
    }

    protected override void Start()
    {
        base.Start();

        // The rider is worth something even though his cart is not
        specialOnHit = 5;
        specialOnDeath = 10;
        goldOnDeath = 1;

        _armedAt = Time.time + Mathf.Max(0f, armDelay);
    }

    /// <summary>
    /// Runs every frame, before either cooldown is consulted. Anything that has to
    /// watch the cart continuously — a midpoint crossing happens on one frame and
    /// is gone — belongs here, not in ShouldRelease, which is only called once the
    /// gates are open and would otherwise be blind for the whole cooldown.
    /// </summary>
    protected virtual void TrackPosition() { }

    // The rider flinches; the CART is what the player actually sees stop. Applied
    // only to ridden carts: an empty cart exists to be shot at, and braking one
    // on every arrow would let a player park the traffic wherever they liked.
    protected override IEnumerator StaggerRoutine()
    {
        if (Cart != null) Cart.HoldFor(staggerDuration);
        yield return base.StaggerRoutine();
    }

    protected virtual void Update()
    {
        if (isDead) return;

        // A sleeping gnome does not throw. Sleep() already stopped the cart; this
        // is the other half of it - a shift that halts but keeps shelling the
        // knights reads as the dart having done nothing. TrackPosition still runs
        // so the position bookkeeping stays honest across the nap.
        if (IsAsleep)
        {
            TrackPosition();
            return;
        }

        TrackPosition();

        if (Time.time < _armedAt) return;

        // Read fresh every frame rather than baked in when the last throw went
        // out — that is the whole point of the thinning rule, and a cooldown
        // computed at stamp time would leave the last gnome standing waiting out
        // a wait that was sized for a shift that no longer exists.
        float cooldown = EffectiveCooldown();
        if (Time.time - _lastThrowAt < cooldown) return;
        if (Time.time - SharedLastThrow() < cooldown) return;
        if (!ShouldRelease()) return;

        _lastThrowAt = Time.time;
        StampShared(Time.time);

        Release();

        // One sound per throw, and throws are already gated to one every ten
        // seconds per weapon — so this cannot stack the way a per-spawn sound does
        if (throwSound != null && throwSound.clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(throwSound);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // The cooldown is the one thing about these riders that cannot be read off
        // the screen — two gnomes interleaving looks exactly like one gnome
        // cheating, and the thinned waits make the same throw rate correct at one
        // figure and wrong at another. This makes both checkable. The height goes
        // in too, because a bomb thrown from the lower track is a different event
        // from one dropped over the top and the two are indistinguishable by x.
        Debug.Log($"[GnomeCart] {name} threw a {ThrowName} at t={Time.time:F2} " +
                  $"(x={transform.position.x:F2}, y={transform.position.y:F2}); {RiderCount()} rider(s) up, " +
                  $"cooldown {cooldown:F2}s, next throw of this kind no earlier than {Time.time + cooldown:F2}");
#endif
    }

    /// <summary>
    /// The wait between throws as it stands right now: whichever of the three
    /// authored waits matches the number of riders still up. Both halves of the
    /// gate use it, so the shared pace and the individual one thin out together.
    /// </summary>
    protected virtual float EffectiveCooldown()
    {
        float full = Mathf.Max(0f, throwCooldown);

        switch (RiderCount())
        {
            case 0:
            case 1: return Thinned(throwCooldownOneLeft, full);
            case 2: return Thinned(throwCooldownTwoLeft, full);
            default: return full;
        }
    }

    // A thinned wait left at zero — an unauthored field, or a prefab saved before
    // these two existed — would read as "throw every frame". That is a far worse
    // way to be wrong than "did not speed up", so a value that cannot have been
    // meant falls back to the full cooldown instead of being honoured.
    private static float Thinned(float authored, float full)
    {
        return authored > 0f ? authored : full;
    }

    // Riders still worth counting. Dead ones linger for a frame or two before the
    // object goes, and a corpse must not be what keeps the survivor slow.
    private static int RiderCount()
    {
        int count = 0;
        for (int i = LiveRiders.Count - 1; i >= 0; i--)
        {
            EnemyGnomeCart rider = LiveRiders[i];
            if (rider == null)
            {
                LiveRiders.RemoveAt(i);
                continue;
            }
            if (!rider.isDead) count++;
        }
        return count;
    }

    /// <summary>
    /// Called after the teleporter has picked the cart up at one edge and set it
    /// down at the other. Subclasses that watch the cart's position for a crossing
    /// must forget what they saw, or the jump reads as a crossing it never made.
    /// </summary>
    protected virtual void HandleTeleported() { }

    // Statics outlive a scene reload — and, with domain reload disabled, a whole
    // Play session — while Time.time restarts at zero. A stamp from later than
    // "now" is therefore a stamp from a previous run, and gating this wave on it
    // would silence the first gnome for as long as the last run went on.
    private float SharedLastThrow()
    {
        ThrowGate gate = Gate;
        if (gate.StampedAt > Time.time)
        {
            gate.StampedAt = float.NegativeInfinity;
            gate.LastThrowAt = float.NegativeInfinity;
        }
        return gate.LastThrowAt;
    }

    private void StampShared(float thrownAt)
    {
        ThrowGate gate = Gate;
        gate.StampedAt = thrownAt;
        gate.LastThrowAt = thrownAt;
    }

    /// <summary>Where a throw leaves the cart.</summary>
    protected Vector3 ThrowPoint =>
        transform.position + new Vector3(throwOffset.x, throwOffset.y, 0f);

    /// <summary>The knight nearer this cart right now, or null if neither is around.</summary>
    protected Transform NearestKnight()
    {
        GameObject left = GameObject.FindWithTag("PlayerLeft");
        GameObject right = GameObject.FindWithTag("PlayerRight");
        if (left == null) return right != null ? right.transform : null;
        if (right == null) return left.transform;

        float x = transform.position.x;
        return Mathf.Abs(left.transform.position.x - x) <= Mathf.Abs(right.transform.position.x - x)
            ? left.transform
            : right.transform;
    }

    /// <summary>Midpoint between the two knights, or false if either is missing.</summary>
    protected bool TryKnightMidpoint(out float midX)
    {
        midX = 0f;
        GameObject left = GameObject.FindWithTag("PlayerLeft");
        GameObject right = GameObject.FindWithTag("PlayerRight");
        if (left == null || right == null) return false;

        midX = (left.transform.position.x + right.transform.position.x) * 0.5f;
        return true;
    }

    // Reached from every death path — arrow, poison tick, burn — because they all
    // funnel through EnemyBase.OnDeath.
    protected override void OnDeath()
    {
        LeaveWreck();
        base.OnDeath();
    }

    private void LeaveWreck()
    {
        if (wreckPrefab == null || Cart == null || !Cart.IsRiding) return;

        GameObject go = Instantiate(wreckPrefab, transform.position, Quaternion.identity, transform.parent);
        MineCart wreck = go.GetComponent<MineCart>();
        if (wreck == null)
        {
            Debug.LogWarning($"[EnemyGnomeCart] {wreckPrefab.name} has no MineCart; the wreck cannot ride on.");
            Destroy(go);
            return;
        }

        // Same line, same distance travelled, same speed — the swap happens inside
        // one frame, so on screen the gnome simply vanishes out of a cart that
        // never breaks stride.
        wreck.Resume(Cart.Line, Cart.Along, Cart.Speed);
        if (RailNetwork.Instance != null) RailNetwork.Instance.Adopt(wreck);
    }
}
