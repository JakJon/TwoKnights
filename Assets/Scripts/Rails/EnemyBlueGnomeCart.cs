using UnityEngine;

// The blue gnome: one rides in every Overseer ambush (owner, 2026-10-19), and he
// is the key to the fight. Kill him and every crimson cart on the board turns
// into an ordinary grey cart for a while — see EnemyCrimsonCart — so the walls
// round the knights can finally be broken.
//
// He throws nothing. He does not need to: the knights want him dead more than
// anything else on the track, and he rides the outer loop behind two rings of
// crimson iron. The sparkles are so he can be picked out of a crowd of gnomes
// at a glance.
//
// He is a rider like any other, so he gates the wave, leaves a grey wreck, and
// can be killed by anything that kills a gnome — poison and fire included. Every
// death path runs through OnDeath, so every one of them opens the window.
public class EnemyBlueGnomeCart : EnemyGnomeCart
{
    [Header("The window")]
    [Tooltip("Seconds every crimson cart stays grey and breakable after he dies.")]
    [SerializeField] private float windowSeconds = 15f;

    [Tooltip("The last part of that window, in seconds, during which the carts blink crimson to warn it is closing.")]
    [SerializeField] private float blinkSeconds = 5f;

    [Tooltip("Played as the window opens. Optional.")]
    [SerializeField] private SoundEffect windowSound;

    // His own gate, though he never uses it: a gate is how EnemyGnomeCart is built
    private static readonly ThrowGate SharedGate = new ThrowGate();

    protected override ThrowGate Gate => SharedGate;
    protected override string ThrowName => "nothing";

    protected override void Start()
    {
        base.Start();
        SparkleFx.ShowBlueSparkles(gameObject);
    }

    protected override bool ShouldRelease() => false;

    protected override void Release() { }

    protected override void OnDeath()
    {
        EnemyCrimsonCart.OpenWindow(windowSeconds, blinkSeconds);

        if (windowSound != null && windowSound.clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(windowSound);
        }

        base.OnDeath();
    }
}
