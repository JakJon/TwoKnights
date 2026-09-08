using System.Collections;
using UnityEngine;

// The Overseer (WORKING NAME — one string on the prefab and one in MineQuests):
// the black gnome in the gold cart, and the answer to what was turning the
// millstone. He is the Mine's true boss, and the only rider the mine has ever put
// on the track that the track outlives rather than the other way round.
//
// He is an ordinary gnome cart in every way the player reads off the screen: same
// silhouette, same loop, same cart. What differs is that he does BOTH jobs at
// once — the green gnome's pickaxes and the bomb gnome's midpoint powder — on two
// clocks that know nothing about each other, so there is no beat in his lap where
// he is safe to stand in front of.
//
// The two clocks are deliberately different KINDS of clock, and that is what lets
// him carry a fight alone:
//   * The pickaxes are a TIMER, and it thins with the crowd exactly like every
//     other gnome's (see EnemyGnomeCart) — alone on the track he throws at the
//     one-rider rate, and a shift out with him slows him to theirs. He keeps his
//     OWN gate though, so he and the grey gnomes can throw on the same frame:
//     sharing one would mean two throwers doing one thrower's work, and the fight
//     was built around him never going quiet.
//   * The bombs are a POSITION. One every time he crosses the line between the
//     knights, four of powder and then one of green, forever. That is a fast rate
//     for a bomb and it is only fair because the player can see it coming: the
//     tell is the cart approaching the middle of the screen, the same tell the
//     ordinary bomb gnome has trained them to read all map.
//
// At the last stand he stops, roars, and comes back faster with the axes at
// double rate. The powder is deliberately NOT doubled with them — it is already
// a position rather than a timer, so the extra speed pays it out oftener on its
// own, and two bombs at once are read by standing between them. The roar is also
// the one moment in the mine where a status is taken OFF something — see
// BeginLastStand for why that is a promise and not a cheat.
public class EnemyDarkGnomeCart : EnemyGnomeCart
{
    [Header("Pickaxes")]
    [Tooltip("One at EACH knight on every throw, like the purple gnome — there is only one of him, so a throw that could be answered by turning a single guard would be no question at all.")]
    [SerializeField] private GameObject pickaxePrefab;

    [Tooltip("Every Nth throw is a volley instead of a single pair. 3 means pair, pair, VOLLEY, and round again. 0 turns volleys off.")]
    [SerializeField] private int volleyEveryNthThrow = 3;

    [Tooltip("Pairs in a volley. Each pair is one axe at each knight on the same frame, so three pairs is three at each of them.")]
    [SerializeField] private int volleyPairs = 3;

    [Tooltip("Seconds between one pair of the volley and the next.")]
    [SerializeField] private float volleySpacing = 0.15f;

    [Tooltip("Quiet after a volley before the ordinary cooldown starts counting again. This is the breath the knights get to reposition in, and it is what stops the volley from being a straight damage multiplier.")]
    [SerializeField] private float volleyRecovery = 1.5f;

    [Header("Bombs")]
    [Tooltip("Dropped at the exact midpoint between the knights, every crossing. Needs an EnemyBomb.")]
    [SerializeField] private GameObject bombPrefab;

    [Tooltip("Every Nth bomb instead of the ordinary one. Needs an EnemyBomb with Poisons set.")]
    [SerializeField] private GameObject poisonBombPrefab;

    [Tooltip("Ordinary bombs between each green one. 4 means powder, powder, powder, powder, green, and round again.")]
    [SerializeField] private int bombsBetweenPoison = 4;

    [Tooltip("Must be at least this far above OR below the knights to throw, exactly as the ordinary bomb gnome measures it — a bomb released level with them arrives before anyone could shoot it.")]
    [SerializeField] private float minVerticalClearance = 1.5f;

    [Header("Ramming")]
    [Tooltip("An empty cart this close ahead of him on the same run is broken instead of shouldered along. Roughly one cart length — the two are touching by the time it goes.")]
    [SerializeField] private float ramReach = 1.2f;

    [Header("The last stand")]
    [Tooltip("He holds still for this long as he roars, and nothing can touch him while he does.")]
    [SerializeField] private float roarSeconds = 2f;

    [Tooltip("What the cart's own speed is multiplied by when he comes back up. On TOP of whatever gear the track is already in.")]
    [SerializeField] private float lastStandSpeedScale = 1.275f;

    [Tooltip("Pickaxe cooldown multiplier after the roar. 0.5 is double the throw rate, at whatever the crowd has thinned it to.")]
    [SerializeField] private float lastStandThrowScale = 0.5f;

    [Tooltip("Shown over the health bar and on the death screen.")]
    [SerializeField] private string bossTitle = "The Overseer";

    // His own, so he never waits on a grey gnome's throw and no grey gnome ever
    // waits on his. The thinning ladder is still shared — that reads riders, not
    // gates — which is the half of the pacing that has to stay common.
    private static readonly ThrowGate SharedGate = new ThrowGate();

    private float _maxHealth;
    private float _invincibleUntil = -1f;
    private bool _lastStand;

    private int _pickaxeThrows;
    private float _throwsHeldUntil = -1f;

    // The banner beat every boss in the game shares: the bar is built at zero
    // opacity, the horn sounds, and it fades in once the wave name has cleared
    // the screen. Same figure as the Rat King's — one boss arriving differently
    // from another reads as a bug in the one that arrives second.
    private const float HealthBarRevealDelay = 3.5f;

    private int _bombsThrown;
    private float _lastSide;
    private float _midX;
    private bool _crossedThisFrame;

    protected override ThrowGate Gate => SharedGate;
    protected override string ThrowName => _lastStand ? "pair of pickaxes (last stand)" : "pair of pickaxes";

    // The wave ends on him, so he has to be a kill the wave is counting
    protected override bool TracksWaveCompletion => true;

    // No execute: see EnemyBase.IsBoss
    public override bool IsBoss => true;

    /// <summary>True while the roar is running. Damage simply does not land.</summary>
    public bool IsInvincible => Time.time < _invincibleUntil;

    /// <summary>He has roared and come back. Read by the wave for its dev log.</summary>
    public bool InLastStand => _lastStand;

    protected override void Start()
    {
        base.Start();

        // Base sets the ordinary rider's payout; a boss is worth rather more than
        // one gold and the special bar should fill on the fight it is spent in
        specialOnHit = 5;
        specialOnDeath = 40;
        goldOnDeath = 10;

        _maxHealth = health;
        BossHealthBar.Show(string.IsNullOrEmpty(bossTitle) ? DisplayName : bossTitle);
        BossHealthBar.SetHealth(health, _maxHealth);
        AudioManager.Instance?.PlaySFX(AudioManager.Instance.bossBanner);
        StartCoroutine(RevealHealthBarAfterBanner());
    }

    // Show() only BUILDS the bar — it deliberately leaves it at opacity 0 so the
    // fade can be timed against the wave-name banner. Without this the name plate
    // is wired, correct, and permanently invisible.
    private IEnumerator RevealHealthBarAfterBanner()
    {
        yield return new WaitForSeconds(HealthBarRevealDelay);
        if (!isDead) BossHealthBar.Reveal();
    }

    // Every frame, cooldown or not: a crossing lasts exactly one frame, so a
    // tracker that only ran while a gate was open would come back from one with a
    // stale side and read its first frame as a crossing it never made.
    protected override void TrackPosition()
    {
        _crossedThisFrame = false;
        if (!TryKnightMidpoint(out _midX)) return;

        float side = Mathf.Sign(transform.position.x - _midX);
        _crossedThisFrame = _lastSide != 0f && side != _lastSide;
        _lastSide = side;
    }

    protected override void Update()
    {
        if (isDead) return;

        // Held still, untouchable, and staying clean: a poison applied on the
        // frame before the roar would otherwise keep ticking through a window the
        // fight promised was safe, and the strip has to hold for the whole roar
        // rather than for the instant it started.
        if (IsInvincible)
        {
            if (IsPoisoned || IsIgnited) PurgeStatusEffects();
            return;
        }

        // Pickaxes, on the base's two gates — unless a volley is running or its
        // recovery is. TrackPosition still has to run in that case: base.Update
        // is what normally calls it, and the midpoint crossing it watches for
        // lasts a single frame, so skipping it would cost him a bomb.
        if (Time.time >= _throwsHeldUntil) base.Update();
        else TrackPosition();

        // Bombs, on their own clock — which is the track, not a timer. Deliberately
        // outside the pickaxe gates, and outside the volley hold too: the two
        // weapons are meant to be able to leave on the same frame.
        ThrowBombIfCrossing();

        Ram();

        if (_maxHealth > 0f) BossHealthBar.SetHealth(isDead ? 0f : health, _maxHealth);
    }

    // ---- pickaxes ----

    protected override bool ShouldRelease()
    {
        // No range test, unlike the green gnome — his loop is the whole frame and
        // he is one rider, so a throw skipped for distance is a beat of nothing.
        // The one rule is HEIGHT: he throws from above the knights and nowhere
        // else. An axe lobbed up from the bottom run arrives on a line the guard
        // is not built to answer, and the loop spends half its length down there;
        // gating on it also means the top of the lap is the dangerous half, which
        // is a thing the player can watch coming.
        return IsAboveKnights();
    }

    /// <summary>
    /// Strictly higher than BOTH knights, measured off their bodies rather than
    /// their pivots (which sit at the feet, half a knight low).
    /// </summary>
    private bool IsAboveKnights()
    {
        float highest = float.NegativeInfinity;
        highest = Mathf.Max(highest, KnightY("PlayerLeft"));
        highest = Mathf.Max(highest, KnightY("PlayerRight"));

        // No knights at all: nothing to throw at, and nothing to be above
        if (float.IsNegativeInfinity(highest)) return false;
        return transform.position.y > highest;
    }

    private static float KnightY(string tag)
    {
        GameObject knight = GameObject.FindWithTag(tag);
        if (knight == null) return float.NegativeInfinity;
        Collider2D body = knight.GetComponent<Collider2D>();
        return body != null ? body.bounds.center.y : knight.transform.position.y;
    }

    protected override void Release()
    {
        if (pickaxePrefab == null) return;

        _pickaxeThrows++;
        if (volleyEveryNthThrow > 0 && volleyPairs > 1
            && (_pickaxeThrows % volleyEveryNthThrow) == 0)
        {
            StartCoroutine(Volley());
            return;
        }

        ThrowPair();
    }

    /// <summary>
    /// The third throw is not a throw, it is a burst: five pairs in a row, one
    /// axe at each knight per pair, close enough together that turning a guard
    /// through them is a decision rather than a reaction. Then he goes quiet for
    /// a beat, which is the half of it that keeps the fight readable — a volley
    /// with no recovery behind it is just a damage multiplier with extra steps.
    ///
    /// Both knights are asked at once on every pair, so there is never a spare
    /// guard to lend across the board.
    /// </summary>
    private IEnumerator Volley()
    {
        for (int i = 0; i < volleyPairs; i++)
        {
            if (isDead) yield break;
            if (!IsInvincible)
            {
                ThrowPair();

                // Every throw is heard. The base class plays the sound once, right
                // after Release() hands back — which covers the FIRST pair on this
                // same frame — so the rest are voiced here. Once per pair rather
                // than once per axe: the two leave together, and the same clip
                // fired twice on one frame is just the clip at double amplitude
                // (see the clipping rule in the sfx notes), not two throws.
                if (i > 0 && throwSound != null && throwSound.clip != null)
                {
                    AudioManager.Instance?.PlaySFX(throwSound);
                }
            }
            if (i < volleyPairs - 1) yield return new WaitForSeconds(Mathf.Max(0.02f, volleySpacing));
        }

        _throwsHeldUntil = Time.time + Mathf.Max(0f, volleyRecovery);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Overseer] volley of {volleyPairs} pairs at {volleySpacing:F2}s, " +
                  $"quiet until t={_throwsHeldUntil:F2}");
#endif
    }

    private void ThrowPair()
    {
        ThrowAt(GameObject.FindWithTag("PlayerLeft"));
        ThrowAt(GameObject.FindWithTag("PlayerRight"));
    }

    private void ThrowAt(GameObject knight)
    {
        if (knight == null) return;
        EnemyPickaxe.Throw(pickaxePrefab, ThrowPoint, AimPoint(knight.transform));
    }

    // The knight's pivot is at its feet, so throwing at transform.position buries
    // the arc in the floor in front of it. Aim at the middle of the body.
    private static Vector3 AimPoint(Transform knight)
    {
        Collider2D body = knight.GetComponent<Collider2D>();
        return body != null ? body.bounds.center : knight.position;
    }

    // Doubled at the last stand, on top of whatever the crowd has thinned it to,
    // so "twice as fast" stays true whether he is alone or leading a shift
    protected override float EffectiveCooldown()
    {
        float cooldown = base.EffectiveCooldown();
        return _lastStand ? cooldown * Mathf.Max(0.05f, lastStandThrowScale) : cooldown;
    }

    // ---- bombs ----

    private void ThrowBombIfCrossing()
    {
        if (!_crossedThisFrame || bombPrefab == null) return;
        if (!HasThrowClearance()) return;

        // Counted before the throw so the cycle is stated in the log the same way
        // it is authored: four of powder, then the green one
        bool poison = poisonBombPrefab != null
                      && bombsBetweenPoison > 0
                      && (_bombsThrown % (bombsBetweenPoison + 1)) == bombsBetweenPoison;
        _bombsThrown++;

        GameObject prefab = poison ? poisonBombPrefab : bombPrefab;

        Vector3 at = ThrowPoint;
        at.x = _midX;

        // One on the line, at the last stand as much as before it. The powder is
        // the weapon the knights answer by MOVING, and a pair straddling the
        // midpoint was answered by standing where neither of them was — which made
        // the doubled bomb easier to read than the single, not harder. The roar's
        // teeth are the pickaxes and the speed; the powder stays the metronome it
        // has been all fight, only arriving oftener because he crosses oftener.
        Instantiate(prefab, at, Quaternion.identity);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Overseer] bomb #{_bombsThrown} ({(poison ? "GREEN" : "powder")}) at x={_midX:F2}, " +
                  $"y={transform.position.y:F2}");
#endif
    }

    // Distance, not height — the rule is the same either side of the knights, and
    // the loop runs track under them as well as over them
    private bool HasThrowClearance()
    {
        GameObject left = GameObject.FindWithTag("PlayerLeft");
        if (left == null) return true;

        Collider2D body = left.GetComponent<Collider2D>();
        float knightY = body != null ? body.bounds.center.y : left.transform.position.y;
        return Mathf.Abs(transform.position.y - knightY) >= minVerticalClearance;
    }

    // The teleporter throws the cart from one edge of the loop to the other, which
    // sails clean across the midpoint. Without this he drops a bomb off-frame on
    // every lap.
    protected override void HandleTeleported()
    {
        _lastSide = 0f;
        _crossedThisFrame = false;
    }

    // ---- ramming ----

    /// <summary>
    /// He does not queue. An empty cart caught on the same run in front of him
    /// comes apart, with the same noise one makes when it is shot.
    ///
    /// Only EXACTLY a plain cart: kegs, riders and delivery carts all subclass
    /// EnemyMineCart, and none of them should burst merely because he arrived —
    /// the powder in particular has to stay something the PLAYER spends. So the
    /// empties between the gnomes of his own haul are what he clears, which is
    /// the shape worth having: he opens the firing line on his own train and
    /// then sits nose to tail behind the riders he cannot break.
    /// </summary>
    private void Ram()
    {
        if (Cart == null) return;

        MineCart ahead = MineCart.FirstAhead(Cart, Mathf.Max(0.1f, ramReach));
        if (ahead == null) return;

        var cart = ahead.GetComponent<EnemyMineCart>();
        if (cart == null || cart.GetType() != typeof(EnemyMineCart)) return;

        cart.Shatter();
    }

    // ---- the roar ----

    /// <summary>
    /// Called by the wave when his health crosses the last threshold. He stops
    /// where he is, is untouchable for the length of the roar, and everything on
    /// him is taken off — then he comes back up faster, throwing axes twice as
    /// often. The bombs keep their one-per-crossing rule and get their raise out
    /// of the speed instead.
    ///
    /// The strip is a real promise and not a cheat: he is meat like every other
    /// rider (ImmuneToAreaDamage is false), so poison and fire have worked on him
    /// all fight and will work on him again the moment the roar ends. What the
    /// roar buys is the ticks that would have landed during it — which is exactly
    /// the debt a player who leaned on damage-over-time to get him here is owed a
    /// chance to re-earn.
    /// </summary>
    public void BeginLastStand()
    {
        if (_lastStand || isDead) return;
        _lastStand = true;

        float window = Mathf.Max(0.1f, roarSeconds);
        _invincibleUntil = Time.time + window;

        // The cart is what the player sees stop — EnemyBase's own flags cannot
        // produce a visible halt, the cart owns its position
        if (Cart != null) Cart.HoldFor(window);

        PurgeStatusEffects();

        // White, and held for exactly as long as he cannot be hurt: the halo IS
        // the reason arrows are doing nothing, and a player who cannot see it
        // reads the next four seconds as the boss being broken.
        DawnFx.Blessing(gameObject);
        DawnFx.ShowInvulnerableAura(gameObject, window);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.bossRoar);
        }

        // Speed applied AFTER the hold: the cart resumes at the new gear rather
        // than crawling out of the roar at the old one
        if (Cart != null) Cart.Speed *= Mathf.Max(0.1f, lastStandSpeedScale);

        Debug.Log($"[Overseer] Last stand at {health:F0} HP — {window}s untouchable, " +
                  $"cart at {(Cart != null ? Cart.Speed : 0f):F2}, pickaxes x{1f / Mathf.Max(0.05f, lastStandThrowScale):F1}");
    }

    public override void TakeDamage(int damage, GameObject projectile)
    {
        // Refused outright rather than reduced to nothing, so no number floats and
        // no flash fires — the halo is the whole answer to "why did that do
        // nothing", and a 0 over his head would be a second, worse answer
        if (IsInvincible) return;
        base.TakeDamage(damage, projectile);

        if (_maxHealth > 0f) BossHealthBar.SetHealth(isDead ? 0f : health, _maxHealth);
    }

    public override float GetMaxHealth() => _maxHealth > 0f ? _maxHealth : health;

    protected override void OnDeath()
    {
        BossHealthBar.Hide();
        base.OnDeath();
    }
}
