using UnityEngine;

// The freeze behind a sleeping dart - and, since the Frigid Order, behind ice as
// well. Both ask the same thing of the board and both get it the same way, so
// there is one of these rather than two; EnemyBase.IsHeld is the single question
// it asks, and the two states stay separate above it so their tells can differ.
//
// EnemyBase can say an enemy is asleep, but it cannot make one hold still by
// asking: every enemy moves itself in its own Update, and a status each of them
// has to be taught to honour is a status that works on the ones somebody
// remembered. That is how the dart came to land on a boss with a health bar and
// do nothing at all. So sleep does not ask. It switches the enemy behaviour OFF
// for the length of the nap, which stops the walk and everything the walk was
// driving - a King who only fires on ARRIVING at a stop, an Overseer who only
// drops powder on CROSSING the midpoint, a mob whose whole attack is reaching a
// knight - for every enemy in the game and every one added after it.
//
// Switching a behaviour off costs two things, and this component is what pays
// them back, because both have to keep working while an enemy sleeps:
//
//   * the base's own per-frame pass, which is where poison, fire and Ember's
//     spread live. A dart that stopped a body BURNING would be a nerf wearing a
//     status effect's clothes - and the dart is a Shadow-and-Serpent upgrade, so
//     holding something still while the venom works is the entire point of it.
//   * the enemy's trigger handling. A sleeping rat still has to pop when a
//     shield sweeps through it; an arrow still has to stop at a sleeping cart.
//
// Coroutines are deliberately NOT stopped: Unity keeps running them through a
// disabled behaviour, so an attack already in flight when the dart lands - a
// telegraphed fan, a volley mid-burst - completes and then the enemy drops. That
// is the honest reading of it. The wind-up was shown, the player saw it coming,
// and the dart answers the NEXT one.
[DisallowMultipleComponent]
public class SleepFreeze : MonoBehaviour
{
    private EnemyBase _enemy;
    private bool _frozen;
    private bool _wasEnabled;

    /// <summary>Put <paramref name="enemy"/> under. Safe to call again on one
    /// already frozen — a second dart moves the deadline and nothing else.</summary>
    public static void Apply(EnemyBase enemy)
    {
        if (enemy == null || !enemy.IsHeld) return;

        SleepFreeze freeze = enemy.GetComponent<SleepFreeze>();
        if (freeze == null) freeze = enemy.gameObject.AddComponent<SleepFreeze>();
        freeze.Freeze(enemy);
    }

    /// <summary>Let it go early - a cleanse, a shattered freeze, or dying
    /// mid-nap. Hands the behaviour back exactly as enabled as it was found.
    ///
    /// Refuses while the OTHER hold is still running. A body that is both slept
    /// and frozen has two reasons to stand still, and the first one to end must
    /// not hand the behaviour back and let it walk away wearing the other one's
    /// tell - which is exactly what would happen the moment a shattered statue
    /// was also carrying a dart.</summary>
    public static void Release(EnemyBase enemy)
    {
        if (enemy == null || enemy.IsHeld) return;
        SleepFreeze freeze = enemy.GetComponent<SleepFreeze>();
        if (freeze != null) freeze.Thaw();
    }

    // Kept on the enemy once added rather than destroyed on waking, dormant
    // between naps. A component destroyed on Tuesday is still handed back by
    // GetComponent for the rest of the frame, so a dart landing on the same frame
    // a nap ended would find the dying freeze, decline to make a new one, and
    // leave the enemy wide awake wearing the Zs. One idle component is a cheaper
    // thing to carry than that bug.
    private void Freeze(EnemyBase enemy)
    {
        _enemy = enemy;
        if (_frozen) return;

        _frozen = true;
        _wasEnabled = enemy.enabled;
        enemy.enabled = false;
    }

    private void Thaw()
    {
        if (!_frozen) return;
        _frozen = false;
        if (_enemy != null) _enemy.enabled = _wasEnabled;
    }

    // The base's frame runs from here while the enemy itself is switched off.
    // LateUpdate rather than Update for the same reason EnemyBase uses it: it is
    // the pass that comes after everything else has had its say.
    private void LateUpdate()
    {
        if (!_frozen) return;

        if (_enemy == null)
        {
            _frozen = false;
            return;
        }

        // The natural end of either hold. Which one it was, and what a body has
        // earned by outlasting it, is EnemyBase's business - it notices the passed
        // deadline in its own LateUpdate on the very next frame, once its
        // behaviour is its own again.
        if (!_enemy.IsHeld)
        {
            Thaw();
            return;
        }

        // Only while the enemy really is switched off. If something else has
        // turned it back on, Unity is calling its LateUpdate already and a second
        // call from here would tick poison and fire twice in one frame.
        if (!_enemy.enabled) _enemy.RunFrozenFrame();
    }

    // Physics callbacks skip disabled behaviours, so they are handed over here.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_frozen && _enemy != null && !_enemy.enabled) _enemy.ForwardFrozenTrigger(other);
    }

    // The enemy being destroyed out from under a nap
    private void OnDestroy()
    {
        Thaw();
    }
}
