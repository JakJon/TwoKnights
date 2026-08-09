using System.Collections.Generic;
using UnityEngine;

// A cart loaded with powder. Ten hit points — one arrow — and it goes up where it
// stands, taking everything nearby with it, the knights included.
//
// It inverts what a cart is for. Every other cart on the track is an obstacle
// that eats the arrows you spend on it; this one repays the arrow, but only if
// you spend it at the right moment. Shoot it the instant you see it and you have
// wasted a keg on empty track. Let it come round to where the traffic is and one
// arrow clears the lot. Shoot it while it is over your own head and you will
// take fifteen for the privilege.
//
// So it is the first thing on the rail whose timing the PLAYER controls, and that
// is the whole reason it is worth having: everything else about the mine is a
// schedule you have to survive, and this is a schedule you get to exploit.
//
// It pays nothing for itself, though — it is still a cart, and carts are not a
// special-charging farm (see EnemyMineCart). The only thing a keg is worth is
// what it kills.
//
// THE RADIUS IS GEOMETRY, not feel. The rule it has to satisfy is: a keg going
// off on the stretch of rail over or under a knight catches him, and a keg going
// off out at an elbow does not. Measured against the knight's collider rather
// than his pivot, and against the cart's box rather than its centre:
//
//   * Mine Circuit runs at y = +/-3 and the knight's box reaches y = +/-0.49, so
//     a keg directly overhead is 2.51 units clear. Anything under that and the
//     wave's own promise — "pop it over your own head and you take fifteen for
//     the privilege" — is simply false, which is where 2.35 left it.
//   * Its elbows sit at (+/-7, +/-3), which is 5.30 from the nearer knight.
//   * Mine Ring's elbows are the tight ones at 3.07, and they are what caps this
//     from above: past that, powder parked in a corner starts reaching the fight.
//
// So the window is (2.51, 3.07) and the number sits in the middle of it. That
// leaves about 1.2 units of horizontal slop around a knight, which is the whole
// margin the player is shooting for.
public class EnemyKegCart : EnemyMineCart
{
    [Header("Blast")]
    [Tooltip("Big enough to reach a knight from the rail running over or under him, tight enough that a keg popped out at an elbow is wasted powder. See the geometry note above the class.")]
    [SerializeField] private float blastRadius = 2.8f;

    [Tooltip("Dealt to every enemy and every knight inside the radius")]
    [SerializeField] private int blastDamage = 15;

    [Tooltip("How far a blast can set off ANOTHER keg. Deliberately much shorter than the blast itself — a hair over one cell, so only a keg coupled directly to this one goes with it.")]
    [SerializeField] private float chainRadius = 1.3f;

    // Whoever landed the shot that killed it, so the blast's kills pay the right
    // knight. Null when a burn or a poison tick finishes it, which is fine — the
    // keg still goes up, it just pays nobody.
    private string _triggerTag;

    protected override void OnAfterDamageApplied(int damage, GameObject projectile)
    {
        string tag = PlayerTagFromProjectile(projectile);
        if (!string.IsNullOrEmpty(tag)) _triggerTag = tag;
    }

    // Every death path detonates, not just an arrow — a keg that quietly expired
    // because the damage came from the wrong source would read as a bug. In
    // practice that now means an arrow or a neighbouring keg: a keg is a cart, so
    // fire and poison no longer whittle one down (EnemyBase.ImmuneToAreaDamage).
    // Popping one is a shot you have to actually take.
    protected override void OnDeath()
    {
        Detonate();
        base.OnDeath();
    }

    /// <summary>
    /// Set off by a neighbouring keg. This is sympathetic detonation, not damage,
    /// which is why it exists at all: a keg is a cart and carts ignore blasts, so
    /// routing coupling through ApplyBlastDamage would have quietly killed every
    /// chain the moment carts became immune. The powder does not care that the
    /// iron around it is fireproof.
    /// </summary>
    public void ChainDetonate(string ownerTag)
    {
        if (isDead) return;

        // Credit travels down the chain, so the knight who lit the first fuse is
        // paid for everything the whole string kills
        if (!string.IsNullOrEmpty(ownerTag)) _triggerTag = ownerTag;

        // Set before OnDeath so the neighbour we are about to reach skips us on
        // its own sweep — that IsDead check is what stops the chain recursing
        isDead = true;
        health = 0f;
        OnDeath();
    }

    private void Detonate()
    {
        Vector2 center = Body();

        // Collected before anything is applied. A keg inside the radius dies to
        // this and detonates in turn from inside its own OnDeath, which would
        // otherwise mutate the physics results out from under the loop.
        var enemies = new List<EnemyBase>();
        var coupled = new List<EnemyKegCart>();
        var knights = new HashSet<PlayerHealth>();

        foreach (var col in Physics2D.OverlapCircleAll(center, blastRadius))
        {
            if (col.CompareTag("PlayerLeft") || col.CompareTag("PlayerRight"))
            {
                PlayerHealth knight = col.GetComponent<PlayerHealth>();
                if (knight != null) knights.Add(knight);
                continue;
            }

            EnemyBase enemy = col.GetComponent<EnemyBase>();
            if (enemy == null || enemy == this || enemy.IsDead) continue;

            // Kegs propagate on a MUCH shorter leash than the blast reaches.
            // Without this, one arrow into a packed train takes the whole train:
            // the blast easily spans three carts either side, each of those takes
            // three more, and the ring is gone in a single chain that the player
            // neither aimed nor could have stopped. Coupling only to the cart
            // actually touching this one is what turns the train from one big
            // fuse into a thing you can cut a measured hole in.
            var keg = enemy as EnemyKegCart;
            if (keg != null)
            {
                // Centre to centre, not pivot to pivot: cart pivots sit at the
                // base and a cart on a shaft is turned a quarter turn, so pivots
                // are a poor measure of how close two of them really are.
                Collider2D otherBox = keg.GetComponent<Collider2D>();
                Vector2 other = otherBox != null
                    ? (Vector2)otherBox.bounds.center
                    : (Vector2)keg.transform.position;
                if (Vector2.Distance(center, other) > chainRadius) continue;

                // Coupled, not damaged — a keg would shrug the blast off like any
                // other cart, so the chain has to go round the immunity
                coupled.Add(keg);
                continue;
            }

            enemies.Add(enemy);
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null || enemies[i].IsDead) continue;
            enemies[i].ApplyBlastDamage(blastDamage, _triggerTag);
        }

        for (int i = 0; i < coupled.Count; i++)
        {
            if (coupled[i] == null || coupled[i].IsDead) continue;
            coupled[i].ChainDetonate(_triggerTag);
        }

        foreach (var knight in knights)
        {
            knight.TakeDamage(blastDamage, DisplayName, DamageKind.Blast);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.fireballExplode);
        }

        BombFx.Explode(center, blastRadius);
    }

    // The cart pivots at its base; the blast should come from the middle of the
    // bed, which is where the powder is drawn
    private Vector2 Body()
    {
        Collider2D box = GetComponent<Collider2D>();
        return box != null ? (Vector2)box.bounds.center : (Vector2)transform.position;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.7f);
        Gizmos.DrawWireSphere(Body(), blastRadius);
    }
#endif
}
