using UnityEngine;

// A runaway cart: twenty hit points of iron rolling down the track with nobody in
// it. It is scenery that fights back only by being in the way — an arrow spent on
// it is an arrow that did not reach anything that matters.
//
// So it is deliberately NOT a kill the player owes:
//   * no special, no gold — see the "reward no streak points" rule below;
//   * TracksWaveCompletion is false, so a wave ends with carts still circling.
//     A gnome cart flips that back on: the rider IS a kill, and the wreck it
//     leaves behind reverts to being furniture.
//   * shields and knights cannot pop it. EnemyBase's default collision would
//     destroy the cart the instant it brushed a shield, which is right for a rat
//     charging in and wrong for two hundred kilos of iron on rails.
[RequireComponent(typeof(MineCart))]
public class EnemyMineCart : EnemyBase
{
    // Inherited by every cart variant — keg, delivery, gnome — so "destroy carts"
    // counts the whole rolling stock
    public override EnemyFamily Family => EnemyFamily.Cart;

    protected MineCart Cart { get; private set; }

    // Furniture by default. EnemyGnomeMineCart overrides it — the wave waits on
    // the gnome, never on the cart he was riding in.
    protected override bool TracksWaveCompletion => false;

    // Iron does not burn, rot, or flinch at a shockwave. Fire, poison and blasts
    // all wash straight over a cart; only a direct hit answers one. See
    // EnemyBase.ImmuneToAreaDamage for why scenery has to work this way.
    public override bool ImmuneToAreaDamage => true;

    // Moot while the cart cannot be ignited at all, but kept honest: MineCart owns
    // the position, so Searing Panic's nudge would only show up as a frame of
    // wobble before the next Reposition pulls it back onto the rail.
    protected override bool AcceptsSearingPanic => false;

    // And the same for cold, for the same reason: MineCart owns the position, so
    // pulling the body back here would show up as a frame of wobble before the
    // next Reposition puts it back on the rail. A chilled cart slows through its
    // own travel instead - see MineCart.ChillScale - and a frozen one stops dead
    // through HoldFor, which is already how a darted cart jams the run behind it.
    protected override bool AcceptsChill => false;

    protected override void Awake()
    {
        Cart = GetComponent<MineCart>();
        base.Awake();
    }

    /// <summary>
    /// "Ten empty carts" means the ones carrying nothing, so this counts an exact
    /// type match rather than an is-a: a keg cart, a delivery cart and a gnome's
    /// cart are all EnemyMineCarts, and none of them is empty.
    ///
    /// Hung off OnDeath rather than Shatter, deliberately — Shatter is the
    /// Overseer running one down, which nobody earned.
    /// </summary>
    protected override void OnDeath()
    {
        base.OnDeath();
        if (GetType() == typeof(EnemyMineCart))
        {
            QuestTally.Total(MineStats.EmptyCartsBroken);
        }
    }

    protected virtual void Start()
    {
        attributes = EnemyType.Ground;

        // An obstacle pays nothing. Set here rather than left to the prefab so a
        // mis-authored inspector value can never quietly turn carts into a
        // special-charging farm.
        specialOnHit = 0;
        specialOnDeath = 0;
        goldOnDeath = 0;
    }

    /// <summary>
    /// Broken by something that was never a shot — today, the Overseer's cart
    /// arriving at speed. It makes the same noise a shot cart makes, because to
    /// the player it is the same event: iron coming apart.
    ///
    /// Deliberately NOT routed through TakeDamage or OnDeath. Both hand out
    /// credit — kill stats, the per-family counter that "Off the Rails" reads,
    /// and a warning per call for a kill with no projectile behind it. Nobody
    /// earned this one, so nobody is paid for it.
    /// </summary>
    public void Shatter()
    {
        if (isDead) return;
        isDead = true;

        if (deathSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(deathSound);
        }

        Destroy(gameObject);
    }

    // A cart eats arrows and ignores everything else. The arrow is destroyed here
    // as well as in PlayerProjectile so that a shot which lands on the frame the
    // cart dies still stops at the iron.
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerLeftProjectile") || other.CompareTag("PlayerRightProjectile"))
        {
            Destroy(other.gameObject);
        }
    }
}
