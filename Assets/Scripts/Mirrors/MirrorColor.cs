using UnityEngine;

// Which pane pairs with which. Colour IS the pairing on this map: a colour
// appears exactly twice on the board and those two panes are twins, so a player
// works out where a shot will come out by reading the hue rather than by
// remembering an authored link. Four colours means at most four pairs.
//
// The names are the art files — Assets/Graphics/palletes/mirror_<colour>.aseprite.
public enum MirrorColor
{
    Green = 0,
    Cyan = 1,
    Vermilion = 2,
    Magenta = 3
}

// Which way a pane's FRONT looks.
//
// A pane is not one-way — a shot may enter either face — so this is not "the
// side that works". It is the reference direction the redirect maths is measured
// against, and it is also what decides which way the pane stands:
//
//   Left / Right -> the face runs up-down, so this is the TALL 1x2 pane, and it
//                   catches shots travelling roughly horizontally.
//   Up / Down    -> the face runs left-right, so this is the WIDE 2x1 pane, and
//                   it catches shots travelling roughly vertically.
//
// The shape is DERIVED from the facing rather than authored beside it, and that
// is deliberate: "a tall pane facing up" is not a thing, and a layout that could
// express it would eventually contain one.
public enum MirrorFacing
{
    Left = 0,
    Right = 1,
    Up = 2,
    Down = 3
}

// What the exit direction is, once a shot has gone in.
//
// The two rules disagree about a case that matters, so the layout says which one
// it was authored against rather than the answer being welded into the code:
//
//   * MirrorAboutFacing is true portal behaviour. A shot that enters one pane's
//     front leaves its twin's front, so two panes AIMED AT EACH OTHER bounce it
//     between them forever, and two panes facing the SAME way turn it around.
//   * PreserveDirection is the rule the design doc first wrote down. A shot
//     travelling right leaves travelling right, whatever the panes are doing.
//     A loop needs the exit pane placed UPSTREAM of the entry pane instead —
//     the same trick RailLayout's teleport table uses to wrap a straight track
//     around off-frame.
//
// They agree whenever the twins are anti-parallel and the shot runs along their
// axis, which is why the difference is easy to miss until a layout leans on it.
public enum MirrorRedirect
{
    MirrorAboutFacing = 0,
    PreserveDirection = 1
}

// The geometry a facing implies. Kept here, next to the enum, so the pane, the
// layout and the editor gizmos can never disagree about which way a pane points.
public static class MirrorFacings
{
    /// <summary>Unit vector out of the pane's front face.</summary>
    public static Vector2 Normal(this MirrorFacing facing)
    {
        switch (facing)
        {
            case MirrorFacing.Left: return Vector2.left;
            case MirrorFacing.Right: return Vector2.right;
            case MirrorFacing.Up: return Vector2.up;
            default: return Vector2.down;
        }
    }

    /// <summary>
    /// Unit vector along the pane's face — the direction "further along the
    /// mirror" runs in. The normal turned a quarter turn anticlockwise, so a
    /// tall pane's tangent points up and a wide pane's points left.
    /// </summary>
    public static Vector2 Tangent(this MirrorFacing facing)
    {
        Vector2 n = facing.Normal();
        return new Vector2(-n.y, n.x);
    }

    /// <summary>Does this facing stand the pane up (1x2) rather than lay it flat (2x1)?</summary>
    public static bool IsTall(this MirrorFacing facing)
    {
        return facing == MirrorFacing.Left || facing == MirrorFacing.Right;
    }
}
