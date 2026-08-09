using UnityEngine;

// The pickaxe thrower: the green gnome. Where the bomb gnome is a landmark
// threat — it only ever acts at the midpoint, and you can watch it coming — this
// one is a clock. It throws as soon as its timer is up, from wherever it happens
// to be, at whichever knight it is nearer.
//
// That makes the two riders answerable in different ways. The bomb is solved by
// position (know where it will fall, be ready there); the pickaxe is solved by
// attention (it can come at either knight, from anywhere along the track). Put
// both on the same loop and the knights cannot settle into one habit.
//
// The only positional rule is that it will not throw from off-frame: the loop
// spends part of every lap outside the camera, and a pickaxe launched from out
// there is just a wasted cooldown.
//
// The PURPLE variant flips the one thing that made this rider survivable, which
// is that he only ever asks one knight a question. He throws two, one at each,
// so the throw cannot be answered by turning a single guard — both knights have
// to be facing the right way at the same moment, and they are on opposite sides
// of the board. Everything else about him is identical, which is deliberate: the
// colour is the only warning the player gets, so the colour has to be the only
// thing that differs.
public class EnemyGnomePickaxeCart : EnemyGnomeCart
{
    [Header("Pickaxe")]
    [Tooltip("Thrown at the nearer knight. Needs an EnemyPickaxe.")]
    [SerializeField] private GameObject pickaxePrefab;

    [Tooltip("Only throws once a knight is this close. Keeps the throw out of the far corners of the track, where a pickaxe is just a long lob nobody has to react to.")]
    [SerializeField] private float throwRange = 6f;

    [Tooltip("The purple gnome: one pickaxe at EACH knight on the same throw, instead of one at whoever is nearer. The range check still runs against the nearer knight, so the far axe is a long lob by design — that is the tell, and the reason both guards have to answer at once.")]
    [SerializeField] private bool throwsAtBothKnights;

    private static readonly ThrowGate SharedGate = new ThrowGate();

    private Transform _target;

    protected override ThrowGate Gate => SharedGate;
    protected override string ThrowName => throwsAtBothKnights ? "pair of pickaxes" : "pickaxe";

    protected override bool ShouldRelease()
    {
        _target = NearestKnight();
        if (_target == null) return false;

        // True distance, not just horizontal: the rider sits a few units above
        // the knights, so a range measured across the ground alone would let it
        // throw from a good deal further out along the track than it looks.
        float range = Mathf.Max(0.1f, throwRange);
        return ((Vector2)(AimPoint(_target) - transform.position)).sqrMagnitude <= range * range;
    }

    protected override void Release()
    {
        if (pickaxePrefab == null || _target == null) return;

        if (!throwsAtBothKnights)
        {
            EnemyPickaxe.Throw(pickaxePrefab, ThrowPoint, AimPoint(_target));
            return;
        }

        // Both, on the same beat. That is the entire point of the purple gnome:
        // the ordinary one is answered by whichever knight it picked, and this one
        // cannot be — there is no spare guard to lend. A missing knight is skipped
        // rather than turned into a second axe at the survivor, because "one each"
        // has to stay literally true or the variant reads as a damage buff.
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
}
