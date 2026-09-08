using UnityEngine;

// The mine's slow problem.
//
// Everything else down here is a timing puzzle: a cart is answered on the beat
// it passes, a pickaxe on the beat it lands, a rat when it finally comes down.
// The ogre is the opposite — it picks ONE knight at the moment it spawns and
// walks at him in a straight line for as long as it takes. It cannot be dodged,
// it cannot be baited across the board, and it never changes its mind.
//
// So it is not a reaction test, it is a BUDGET. Eighty health at half a slime's
// pace is a fixed number of arrows and a fixed number of seconds to spend them
// in, and the wave around it is what makes those seconds expensive. It belongs
// in waves that are already asking a knight to look somewhere else.
//
// Forty damage on arrival — nearly half a fresh knight — is what stops "deal
// with it later" from being free.
public class EnemyOgre : EnemyBase
{
    public override EnemyFamily Family => EnemyFamily.Brute;

    // An ogre runs BESIDE a wave's shifts, not in one. It is released on the
    // wave's own clock and is still walking across two or three handovers, so
    // counting it as a member would hold every one of them open until it was
    // dead — a wave paced by the slowest thing on the board.
    public override bool JoinsAmbushes => false;

    [Header("Ogre")]
    [Tooltip("World units per second. About half a slime's — mind that a slime's REAL speed is 0.2 to 0.5, set by size in EnemySlime.InitializeSlime, and that the 1.5 serialized on the slime prefab is overwritten on every spawn and never used.")]
    [SerializeField] protected float moveSpeed = 0.1875f;

    [Tooltip("Dealt to the knight it reaches. The ogre is spent on arrival, exactly like every other body that touches a knight.")]
    [SerializeField] protected int knightDamage = 40;

    /// <summary>
    /// The knight it picked at spawn. Never reassigned — that fixity IS the
    /// enemy. A brute that retargeted the nearer knight as the fight moved
    /// would be a chase; this is a debt with a delivery date.
    /// </summary>
    protected Transform Target { get; private set; }

    /// <summary>
    /// The one it is NOT walking at. Held for the fire variant, which throws at
    /// the far knight so a single ogre cannot be answered by one guard.
    /// </summary>
    protected Transform FarKnight { get; private set; }

    /// <summary>
    /// Names the knight outright instead of letting the ogre read the nearer one
    /// off where it landed. Must be called before Start runs — i.e. the same
    /// frame as Instantiate, which is the contract EnemyRat.SetEntryPoint uses.
    ///
    /// It exists for entries near the centre line, where the two knights are
    /// equidistant and "nearest" is a tie the code would break the same way
    /// every time, quietly handing every such ogre to the same player.
    /// </summary>
    public void AssignTarget(Transform knight, Transform other)
    {
        if (knight == null) return;
        Target = knight;
        FarKnight = other;
        _assigned = true;
    }

    private bool _assigned;

    protected virtual void Start()
    {
        attributes = EnemyType.Ground;
        specialOnHit = 5;
        specialOnDeath = 12;
        playerDamage = knightDamage;

        if (AudioManager.Instance != null)
        {
            // Borrowed from the wolf until the ogre has a voice of its own — a
            // silent enemy reads as one that is not taking damage.
            hurtSound = AudioManager.Instance.wolfHurt;
            deathSound = AudioManager.Instance.wolfDeath;
        }

        ChooseKnights();
    }

    // Whichever knight it is closest to WHERE IT LANDED. A wave places an ogre
    // by choosing an off-frame point, so the spawn is the authoring and this
    // only has to read it back — unless the wave named a knight outright, which
    // is what an entry on the centre line has to do.
    private void ChooseKnights()
    {
        if (_assigned) return;

        GameObject left = GameObject.FindWithTag("PlayerLeft");
        GameObject right = GameObject.FindWithTag("PlayerRight");

        if (left == null && right == null) return;
        if (left == null) { Target = right.transform; return; }
        if (right == null) { Target = left.transform; return; }

        float toLeft = Vector2.Distance(transform.position, left.transform.position);
        float toRight = Vector2.Distance(transform.position, right.transform.position);

        bool nearerLeft = toLeft <= toRight;
        Target = nearerLeft ? left.transform : right.transform;
        FarKnight = nearerLeft ? right.transform : left.transform;
    }

    // Virtual so the fire variant can walk AND throw in one tick rather than
    // reimplementing the walk.
    protected virtual void Update()
    {
        if (isDead) return;
        // An ogre that kept walking through its flinch would have no tell at all
        if (IsStaggered) return;
        if (Target == null) return;

        Vector2 here = transform.position;
        Vector2 there = Target.position;
        transform.position = Vector2.MoveTowards(here, there, moveSpeed * Time.deltaTime);
        UpdateSpriteDirection(there - here);
    }

    protected override int GetPlayerCollisionDamage()
    {
        return knightDamage;
    }
}
