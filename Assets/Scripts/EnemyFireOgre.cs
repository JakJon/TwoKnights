using UnityEngine;

// The red one. Walks at its knight exactly as the green one does, and throws a
// fireball at the OTHER one every few seconds.
//
// That split is the whole design. The knights cannot cover for each other —
// shield facing is shooting direction — so one fire ogre puts a different
// question to each half of the board at the same time: the knight it is walking
// at has a body to shoot before it arrives, and the knight it is throwing at
// has a fireball to guard and nothing he can do about the ogre itself. Neither
// can solve the other's half, and the fireballs keep coming for exactly as long
// as the first knight takes to answer the body.
//
// Which is why killing it quickly is the answer, and why it is worth more.
public class EnemyFireOgre : EnemyOgre
{
    [Header("Fire")]
    [Tooltip("Needs an EnemyFireball. The Crimson Twins' fireball is the one this is tuned against.")]
    [SerializeField] private GameObject fireballPrefab;

    [Tooltip("Seconds between throws. The first is held back by this much as well, so an ogre put down beside a knight cannot open with one.")]
    [SerializeField] private float throwInterval = 10f;

    [Tooltip("Warning glow before each throw. The knight being thrown at is by definition looking somewhere else, so the tell is on the OGRE and has to last long enough to turn a guard around.")]
    [SerializeField] private float telegraphSeconds = 0.7f;

    [Tooltip("World units per second, matching the Crimson Twins")]
    [SerializeField] private float fireballSpeed = 2.4f;

    [SerializeField] private int fireballDamage = 15;

    [Tooltip("Killed after this long no matter what, so a throw at a knight who has since died cannot linger")]
    [SerializeField] private float fireballLifetime = 12f;

    [Tooltip("Scale of the fireball. The twins throw theirs at 2.2 from bodies six units wide; an ogre is one unit, so its own are smaller.")]
    [SerializeField] private float fireballScale = 1.2f;

    [Tooltip("Where the throw leaves the body, measured from the pivot at its feet")]
    [SerializeField] private Vector2 throwOffset = new Vector2(0f, 0.75f);

    private float _nextThrowAt;
    private bool _telegraphing;

    protected override void Start()
    {
        base.Start();
        specialOnDeath = 16;
        goldOnDeath = 2;
        _nextThrowAt = Time.time + Mathf.Max(0.1f, throwInterval);
    }

    protected override void Update()
    {
        base.Update();

        if (isDead || fireballPrefab == null) return;

        // A staggered ogre is not throwing. The flinch has to be worth
        // something, and this is the only thing it can take away.
        if (IsStaggered) return;

        if (Time.time < _nextThrowAt) return;

        _nextThrowAt = Time.time + Mathf.Max(0.1f, throwInterval);

        if (telegraphSeconds > 0f)
        {
            _telegraphing = true;
            glowManager?.StartGlow(new Color(1f, 0.35f, 0.15f), telegraphSeconds, 9f, 0.7f);
            Invoke(nameof(Throw), telegraphSeconds);
        }
        else
        {
            Throw();
        }
    }

    // The far knight, chosen once at spawn alongside the one it walks at. Falls
    // back to its own target when the other knight is gone, so a lone knight
    // still gets thrown at rather than the ogre going quiet.
    private void Throw()
    {
        _telegraphing = false;
        if (isDead || fireballPrefab == null) return;

        Transform mark = FarKnight != null ? FarKnight : Target;
        if (mark == null) return;

        Vector2 from = (Vector2)transform.position + throwOffset;
        EnemyFireball.Launch(fireballPrefab, from, mark,
            EnemyFireball.Mode.Straight, fireballSpeed, fireballDamage,
            0f, 0f, 0f, 1f, fireballLifetime, fireballScale);
    }

    // A queued Invoke would otherwise throw out of a dead ogre a beat after it
    // fell, which reads as a free hit from nothing.
    protected override void OnDeath()
    {
        if (_telegraphing) CancelInvoke(nameof(Throw));
        base.OnDeath();
    }
}
