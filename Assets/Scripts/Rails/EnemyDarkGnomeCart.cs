using System.Collections.Generic;
using UnityEngine;

// The Overseer (WORKING NAME — one string on the prefab and one in MineQuests):
// the black gnome in the gold cart, and the answer to what was turning the
// millstone. He is the Mine's true boss.
//
// Rebuilt 2026-10-19 to the owner's sketch. He rides a straight track of his own
// across the top of the screen, outside the three crimson loops that wall the
// knights in, and the only thing he does is throw pickaxes — six, eight, ten
// and then twelve at a time as he is cut down.
//
// He has no throw clock of his own. The wave's attack list (DarkGnome) decides
// when he throws and at whom, because his axes and the rock come down on the
// same two guards: one clock for both is the only way to promise that the axes
// never land while a knight is blocking something from another direction. All
// he decides is whether he CAN throw right now — he has to be on screen, awake,
// not frozen, not roaring, and far enough from the edge that the whole throw
// leaves before he does.
//
// At each end of his track he either wraps (off one side, back in on the other)
// or turns round, off-screen, and comes back. Which one is the wave's to say,
// and it changes with his health.
//
// At the last stand he stops, roars, and comes back faster. The roar is the one
// moment in the mine where a status is taken OFF something — see BeginLastStand
// for why that is a promise and not a cheat.
public class EnemyDarkGnomeCart : EnemyGnomeCart
{
    // The quests know him as the Overseer, so the kill counter says so. Without
    // this the key would be minted from the class name as "kills.darkgnomecart",
    // which nothing reads and no player would recognise.
    protected override string StatKey => "overseer";

    [Header("Pickaxes")]
    [Tooltip("What he throws. Needs an EnemyPickaxe.")]
    [SerializeField] private GameObject pickaxePrefab;

    [Tooltip("How fast his axes fly compared with an ordinary gnome's: 0.5 is half as fast (owner, 2026-10-20). Same arc, twice the time in the air — the attack list reads the flight time from here, so its timing follows.")]
    [SerializeField] private float axePace = 0.5f;

    [Tooltip("He only lets an axe go while the knight it is meant for is this close, measured as the ordinary pickaxe gnome measures it — the same rule and the same value as Enemy_GnomePickaxeCart's throwRange, so he never throws from the far edges of the screen (owner, 2026-10-20).")]
    [SerializeField] private float throwRange = 9f;

    [Header("The bomb run")]
    [Tooltip("What he drops on a bomb run (owner, 2026-10-20): three in one pass — left of the left knight, between the knights, right of the right knight — 15 seconds after each ambush's blue gnome dies. Needs an EnemyBomb.")]
    [SerializeField] private GameObject bombPrefab;

    [Header("The last stand")]
    [Tooltip("He holds still for this long as he roars, and nothing can touch him while he does.")]
    [SerializeField] private float roarSeconds = 2f;

    [Tooltip("Shown over the health bar and on the death screen.")]
    [SerializeField] private string bossTitle = "The Overseer";

    [Header("Death")]
    [Tooltip("Played on top of the ordinary death sound when he goes down.")]
    [SerializeField] private SoundEffect deathCry;

    // A gate is how EnemyGnomeCart is built; his is never stamped, because he
    // never throws through it
    private static readonly ThrowGate SharedGate = new ThrowGate();

    private float _maxHealth;
    private float _invincibleUntil = -1f;
    private bool _lastStand;

    // The last stand can be reached while he is off-screen, turning or wrapping.
    // A roar nobody sees is a roar wasted, so it waits until he is back in view.
    private bool _roarPending;
    private float _roarSpeed;

    // The bar is built at zero opacity when he is spawned, off-screen, and fades
    // in with the horn the moment he rolls into view (owner, 2026-10-20) — by
    // then the first ambush is out and the wave name is long gone
    private bool _announced;

    // His track: the lane he rides east on and the lane he comes back west on.
    // Both are the same rails; the second lays no pieces of its own.
    private int _eastLine = -1;
    private int _westLine = -1;
    private OverseerTurns _turns = OverseerTurns.Wrap;
    private int _endsReached;

    private float _lastThrowSoundAt = float.NegativeInfinity;

    protected override ThrowGate Gate => SharedGate;
    protected override string ThrowName => "pickaxe";

    // The wave ends on him, so he has to be a kill the wave is counting
    protected override bool TracksWaveCompletion => true;

    // No execute: see EnemyBase.IsBoss
    public override bool IsBoss => true;

    /// <summary>True while the roar is running. Damage simply does not land.</summary>
    public bool IsInvincible => Time.time < _invincibleUntil;

    /// <summary>He has roared and come back. Read by the wave for its dev log.</summary>
    public bool InLastStand => _lastStand;

    /// <summary>Seconds from one of his throws leaving to it landing.</summary>
    public float AxeFlightTime => EnemyPickaxe.FlightTimeOf(pickaxePrefab) / Mathf.Clamp(axePace, 0.05f, 4f);

    protected override void Awake()
    {
        base.Awake();
        if (Cart != null) Cart.EndOfLine = HandleEndOfLine;
    }

    protected override void Start()
    {
        base.Start();

        // Base sets the ordinary rider's payout; a boss is worth rather more than
        // one gold and the special bar should fill on the fight it is spent in
        specialOnHit = 5;
        specialOnDeath = 40;
        goldOnDeath = 10;

        _maxHealth = health;
        // Show() only BUILDS the bar, at opacity 0; Update reveals it as he
        // comes into view. Without that the name plate is wired, correct, and
        // permanently invisible.
        BossHealthBar.Show(string.IsNullOrEmpty(bossTitle) ? DisplayName : bossTitle);
        BossHealthBar.SetHealth(health, _maxHealth);
    }

    protected override void Update()
    {
        if (isDead) return;

        if (!_announced && InView(0f))
        {
            _announced = true;
            BossHealthBar.Reveal();
            AudioManager.Instance?.PlaySFX(AudioManager.Instance.bossBanner);
        }

        // Well inside the edge, so the stop happens where it can be seen
        if (_roarPending && InView(1.5f))
        {
            _roarPending = false;
            Roar();
        }

        // Held still, untouchable, and staying clean: a poison applied on the
        // frame before the roar would otherwise keep ticking through a window the
        // fight promised was safe, and the strip has to hold for the whole roar
        // rather than for the instant it started.
        if (IsInvincible)
        {
            if (IsPoisoned || IsIgnited) PurgeStatusEffects();
            return;
        }

        // Deliberately NOT base.Update: that is the ordinary gnome's throw clock,
        // and his throws are the wave's attack list (DarkGnome), never a timer
        if (_maxHealth > 0f) BossHealthBar.SetHealth(health, _maxHealth);
    }

    // Never asked — base.Update is not run — but EnemyGnomeCart needs both
    protected override bool ShouldRelease() => false;
    protected override void Release() { }

    // ---- his track ----

    /// <summary>The two lanes of his track: east, and the way back west after a U-turn.</summary>
    public void SetTrack(int eastLine, int westLine)
    {
        _eastLine = eastLine;
        _westLine = westLine;
    }

    /// <summary>
    /// What he does at the ends of his track from now on. Changing it starts the
    /// count again, so "wrap, wrap, U-turn" opens with two wraps whenever it begins.
    /// </summary>
    public void SetTurns(OverseerTurns turns)
    {
        if (turns == _turns) return;
        _turns = turns;
        _endsReached = 0;
    }

    /// <summary>His speed, in world units per second. Only his — the loops never change.</summary>
    public void SetSpeed(float unitsPerSecond)
    {
        if (Cart != null) Cart.Speed = Mathf.Max(0.1f, unitsPerSecond);
    }

    // He has rolled off one end of his lane. Both answers put him back on the
    // rails at once, so nothing despawns; the cart raises Teleported for us.
    private bool HandleEndOfLine(MineCart cart)
    {
        RailNetwork rails = RailNetwork.Instance;
        if (rails == null) return false;

        _endsReached++;
        bool uTurn = _turns == OverseerTurns.UTurn
                     || (_turns == OverseerTurns.WrapWrapUTurn && _endsReached % 3 == 0);

        int current = cart.Line.Index;
        int other = current == _eastLine ? _westLine : (current == _westLine ? _eastLine : -1);

        RailLine back;
        if (uTurn && other >= 0 && rails.TryGetLine(other, out back))
        {
            // The same spot on the other lane: as far behind its mouth as he has
            // run past the end of this one. The turn happens off-screen, so all the
            // player sees is him coming back the way he went.
            float overrun = cart.Along - cart.Line.Length;
            cart.Resume(back, -overrun, cart.Speed);
            return true;
        }

        // Round to the far side, still going the same way
        cart.Resume(cart.Line, -cart.LeadIn, cart.Speed);
        return true;
    }

    // ---- throwing, on the wave's word ----

    /// <summary>Why he cannot let an axe go at a knight right now, for the wave's dev log.</summary>
    public enum NotReady
    {
        Ready,
        Gone,        // dead, or no cart
        Roaring,
        Held,        // asleep or frozen
        OutOfRange   // that knight is further off than throwRange: he is out towards an edge
    }

    /// <summary>The ordinary pickaxe gnome's throw range, which he keeps to.</summary>
    public float ThrowRange => throwRange;

    /// <summary>
    /// Can he let an axe go at <paramref name="knight"/> this frame? The ordinary
    /// pickaxe gnome's rule (EnemyGnomePickaxeCart.ShouldRelease): only while that
    /// knight is within throwRange of him, measured the same way, so nothing is
    /// lobbed from the far edges of the screen (owner, 2026-10-20). Asked for each
    /// axe's own knight, so an axe at the far knight waits until he is closer to
    /// it. Also no while he is asleep, frozen, roaring or dead.
    /// </summary>
    public NotReady WhyNotReady(Transform knight)
    {
        if (isDead || Cart == null || !Cart.IsRiding) return NotReady.Gone;
        if (IsInvincible || _roarPending) return NotReady.Roaring;
        if (IsHeld) return NotReady.Held;
        if (knight == null) return NotReady.Gone;

        Vector2 offset = (Vector2)(AimPoint(knight) - transform.position);
        return offset.sqrMagnitude <= throwRange * throwRange ? NotReady.Ready : NotReady.OutOfRange;
    }

    /// <summary>
    /// The earliest moment at or after <paramref name="from"/> that he can throw
    /// every axe of <paramref name="axes"/> — each at its own knight, at its own
    /// moment, with that knight in range as it leaves — going by where he is now,
    /// how fast he is going and how he turns at the ends. A forecast, kept a little
    /// inside the range: a dart, a freeze or a run of hit-pauses can still make him
    /// late, and the wave's attack list covers that by waiting for him.
    ///
    /// An entry too long to throw in one pass at his speed is started as soon as
    /// its first axe can go; the rest wait for him to come back into range.
    /// </summary>
    public float ReadyAt(float from, List<OverseerAttack.AxeShot> axes, Transform left, Transform right)
    {
        RailNetwork rails = RailNetwork.Instance;
        if (isDead || Cart == null || !Cart.IsRiding || rails == null || _roarPending
            || axes == null || axes.Count == 0 || left == null || right == null)
        {
            return from;
        }

        float now = Time.time;
        int samples = Mathf.CeilToInt(ForecastHorizon / ForecastStep) + 1;
        if (_path == null || _path.Length < samples) _path = new float[samples];
        TracePath(rails, now, samples);

        Vector2 leftAim = AimPoint(left);
        Vector2 rightAim = AimPoint(right);
        float y = transform.position.y;
        float reach = Mathf.Max(0.5f, throwRange - ForecastMargin);
        int first = Mathf.Max(0, Mathf.CeilToInt((from - now) / ForecastStep));

        for (int k = first; k < samples; k++)
        {
            if (Fits(k, axes, axes.Count, samples, leftAim, rightAim, y, reach)) return now + k * ForecastStep;
        }
        for (int k = first; k < samples; k++)
        {
            if (Fits(k, axes, 1, samples, leftAim, rightAim, y, reach)) return now + k * ForecastStep;
        }
        return from;
    }

    // Would the first `count` axes all be in range if the entry started at sample k?
    private bool Fits(int k, List<OverseerAttack.AxeShot> axes, int count, int samples,
                      Vector2 leftAim, Vector2 rightAim, float y, float reach)
    {
        for (int i = 0; i < count; i++)
        {
            int j = k + Mathf.RoundToInt(axes[i].Release / ForecastStep);
            if (j >= samples) return false;

            Vector2 aim = axes[i].Right ? rightAim : leftAim;
            float dx = _path[j] - aim.x;
            float dy = y - aim.y;
            if (dx * dx + dy * dy > reach * reach) return false;
        }
        return true;
    }

    // Where he will be, sample by sample from now: x along his track, standing
    // still for whatever roar, dart or freeze he is in, and wrapping or turning at
    // the ends exactly as HandleEndOfLine will
    private void TracePath(RailNetwork rails, float now, int samples)
    {
        float stall = Mathf.Max(0f, _invincibleUntil - now);
        stall = Mathf.Max(stall, RemainingSleepSeconds);
        stall = Mathf.Max(stall, RemainingFreezeSeconds);

        float speed = Cart.Speed;
        RailLine line = Cart.Line;
        float along = Cart.Along;
        int ends = _endsReached;

        for (int k = 0; k < samples; k++)
        {
            _path[k] = line.PointAt(along).x;

            if (k * ForecastStep < stall) continue;

            along += speed * ForecastStep;
            if (along <= line.Length + Cart.RunOut) continue;

            ends++;
            bool uTurn = _turns == OverseerTurns.UTurn
                         || (_turns == OverseerTurns.WrapWrapUTurn && ends % 3 == 0);
            int other = line.Index == _eastLine ? _westLine : (line.Index == _westLine ? _eastLine : -1);
            RailLine back;
            if (uTurn && other >= 0 && rails.TryGetLine(other, out back))
            {
                float overrun = along - line.Length;
                line = back;
                along = -overrun;
            }
            else
            {
                along = -Cart.LeadIn;
            }
        }
    }

    private float[] _path;

    // Kept this far inside the range, so a forecast that is a little out still
    // lands inside what WhyNotReady accepts
    private const float ForecastMargin = 0.4f;
    private const float ForecastHorizon = 25f;
    private const float ForecastStep = 0.05f;

    // ---- the bomb run ----

    /// <summary>Has he anything to drop on a bomb run?</summary>
    public bool HasBombs => bombPrefab != null;

    /// <summary>
    /// Seconds from a bomb leaving his cart to it reaching the knights' level and
    /// going off — it drifts straight down at the bomb's own speed.
    /// </summary>
    public float BombFlightTime
    {
        get
        {
            float speed = EnemyBomb.TravelSpeedOf(bombPrefab);
            if (speed <= 0.0001f) return 0f;
            return Mathf.Abs(ThrowPoint.y - KnightLevel()) / speed;
        }
    }

    /// <summary>One bomb, dropped from his track straight above <paramref name="x"/>.</summary>
    public void DropBombAt(float x)
    {
        if (bombPrefab == null) return;
        Vector3 at = ThrowPoint;
        at.x = x;
        Instantiate(bombPrefab, at, Quaternion.identity);
    }

    /// <summary>
    /// When, at or after <paramref name="from"/>, he will next cross every one of
    /// <paramref name="points"/> on a single pass — no wrap or turn in between —
    /// going by the same forecast ReadyAt uses. <paramref name="times"/> gets the
    /// moment he crosses each point. False if no pass in the forecast does.
    /// </summary>
    public bool PlanBombRun(float from, float[] points, float[] times)
    {
        RailNetwork rails = RailNetwork.Instance;
        if (isDead || Cart == null || !Cart.IsRiding || rails == null || points == null || times == null
            || points.Length == 0 || times.Length < points.Length)
        {
            return false;
        }

        float now = Time.time;
        int samples = Mathf.CeilToInt(ForecastHorizon / ForecastStep) + 1;
        if (_path == null || _path.Length < samples) _path = new float[samples];
        TracePath(rails, now, samples);

        float lowest = points[0], highest = points[0];
        for (int i = 1; i < points.Length; i++)
        {
            lowest = Mathf.Min(lowest, points[i]);
            highest = Mathf.Max(highest, points[i]);
        }

        // Further than this in one step is a wrap or a turn, not a ride
        float jump = Mathf.Max(0.5f, Cart.Speed * ForecastStep * 3f);
        int first = Mathf.Max(1, Mathf.CeilToInt((from - now) / ForecastStep));

        for (int k = first; k < samples; k++)
        {
            float a = _path[k - 1], b = _path[k];
            if (Mathf.Abs(b - a) > jump || Mathf.Approximately(a, b)) continue;

            bool east = b > a;
            if (!Crosses(a, b, east ? lowest : highest)) continue;

            for (int i = 0; i < points.Length; i++) times[i] = -1f;
            int found = 0;
            for (int j = k; j < samples && found < points.Length; j++)
            {
                float pa = _path[j - 1], pb = _path[j];
                if (Mathf.Abs(pb - pa) > jump || (east ? pb < pa : pb > pa)) break;

                for (int i = 0; i < points.Length; i++)
                {
                    if (times[i] >= 0f || !Crosses(pa, pb, points[i])) continue;
                    times[i] = now + j * ForecastStep;
                    found++;
                }
            }
            if (found == points.Length) return true;
        }
        return false;
    }

    private static bool Crosses(float a, float b, float point)
    {
        return (a - point) * (b - point) <= 0f && !Mathf.Approximately(a, b);
    }

    private static float KnightLevel()
    {
        GameObject left = GameObject.FindWithTag("PlayerLeft");
        GameObject right = GameObject.FindWithTag("PlayerRight");
        float sum = 0f;
        int count = 0;
        if (left != null) { sum += AimPoint(left.transform).y; count++; }
        if (right != null) { sum += AimPoint(right.transform).y; count++; }
        return count > 0 ? sum / count : 0f;
    }

    // Is his cart this far inside the edge of the screen?
    private bool InView(float inset)
    {
        float centre, halfWidth;
        ViewSpan(out centre, out halfWidth);
        return Mathf.Abs(transform.position.x - centre) <= halfWidth - Mathf.Max(0f, inset);
    }

    /// <summary>
    /// One pickaxe at <paramref name="knight"/>, from wherever he is, thrown on
    /// behalf of <paramref name="owner"/> — the attack list entry that booked it.
    /// </summary>
    public void ThrowAxeAt(Transform knight, object owner = null)
    {
        if (pickaxePrefab == null || knight == null) return;

        EnemyPickaxe.Throw(pickaxePrefab, ThrowPoint, AimPoint(knight), knight, owner, axePace);

        // Every throw is heard, but the same clip twice inside a few frames is the
        // clip at double amplitude rather than two throws (the clipping rule)
        if (throwSound != null && throwSound.clip != null && AudioManager.Instance != null
            && Time.time - _lastThrowSoundAt >= 0.09f)
        {
            AudioManager.Instance.PlaySFX(throwSound);
            _lastThrowSoundAt = Time.time;
        }
    }

    // The knight's pivot is at its feet, so throwing at transform.position buries
    // the arc in the floor in front of it. Aim at the middle of the body.
    private static Vector3 AimPoint(Transform knight)
    {
        Collider2D body = knight.GetComponent<Collider2D>();
        return body != null ? body.bounds.center : knight.position;
    }

    private static void ViewSpan(out float centre, out float halfWidth)
    {
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            centre = cam.transform.position.x;
            halfWidth = cam.orthographicSize * cam.aspect;
            return;
        }
        centre = 0f;
        halfWidth = 10f;
    }

    // ---- the roar ----

    /// <summary>
    /// Called by the wave when his health crosses the last stand. He stops where
    /// he is, is untouchable for the length of the roar, and everything on him is
    /// taken off — then he comes back up at <paramref name="newSpeed"/>. If he
    /// is off-screen when it is reached, the roar waits until he is back in view.
    ///
    /// The strip is a real promise and not a cheat: he is meat like every other
    /// rider (ImmuneToAreaDamage is false), so poison and fire have worked on him
    /// all fight and will work on him again the moment the roar ends. What the
    /// roar buys is the ticks that would have landed during it — which is exactly
    /// the debt a player who leaned on damage-over-time to get him here is owed a
    /// chance to re-earn.
    /// </summary>
    public void BeginLastStand(float newSpeed)
    {
        if (_lastStand || isDead) return;
        _lastStand = true;
        _roarSpeed = newSpeed;

        if (InView(1.5f)) Roar();
        else _roarPending = true;
    }

    private void Roar()
    {
        float newSpeed = _roarSpeed;
        float window = Mathf.Max(0.1f, roarSeconds);
        _invincibleUntil = Time.time + window;

        // The cart is what the player sees stop — EnemyBase's own flags cannot
        // produce a visible halt, the cart owns its position
        if (Cart != null) Cart.HoldFor(window);

        PurgeStatusEffects();

        // White, and held for exactly as long as he cannot be hurt: the halo IS
        // the reason arrows are doing nothing, and a player who cannot see it
        // reads the next two seconds as the boss being broken.
        DawnFx.Blessing(gameObject);
        DawnFx.ShowInvulnerableAura(gameObject, window);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.bossRoar);
        }

        // Applied now, while he is held: he comes out of the roar at the new
        // gear rather than crawling out of it at the old one
        SetSpeed(newSpeed);

        Debug.Log($"[Overseer] Last stand at {health:F0} HP — {window}s untouchable, " +
                  $"then {(Cart != null ? Cart.Speed : 0f):F2} u/s");
    }

    protected override bool RefusesHits => IsInvincible;

    public override void TakeDamage(int damage, GameObject projectile, int holyPortion = 0)
    {
        // Refused outright rather than reduced to nothing, so no number floats and
        // no flash fires — the halo is the whole answer to "why did that do
        // nothing", and a 0 over his head would be a second, worse answer
        if (IsInvincible) return;
        base.TakeDamage(damage, projectile, holyPortion);

        if (_maxHealth > 0f) BossHealthBar.SetHealth(isDead ? 0f : health, _maxHealth);
    }

    public override float GetMaxHealth() => _maxHealth > 0f ? _maxHealth : health;

    protected override void OnDeath()
    {
        BossHealthBar.Hide();

        if (deathCry != null && deathCry.clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(deathCry);
        }

        base.OnDeath();
    }
}
