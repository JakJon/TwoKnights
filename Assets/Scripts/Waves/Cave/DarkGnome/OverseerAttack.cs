using System.Collections.Generic;
using UnityEngine;

// The data the Overseer fight is authored in (owner, 2026-10-19). Each stage is
// one thousand of his health: how fast he rides, how he turns at the ends of his
// track, how many axes he throws at once, how big the gnome ambushes are, and a
// list of attacks that loops for as long as the stage lasts.
//
// His axes are entries in that list, not a timer of his own. That is the whole
// point of it: a knight has one guard facing one way, so a pickaxe from him
// landing while the knight is blocking rock from somewhere else cannot be
// blocked at all. Putting both in one list means one clock decides when
// everything lands, and every gap in the list is a gap for both.

public enum OverseerAttackKind
{
    Rock = 0,
    Axes = 1,

    // Rock at one knight while he throws axes at the other (owner, 2026-10-20:
    // the patterns were "too simple and similar"). Each knight still answers one
    // thing at a time — they are just different things, at the same time. Give
    // the rock and the axes DIFFERENT knights; the paper check flags it if not.
    Together = 2
}

/// <summary>Who an axe entry is thrown at.</summary>
public enum AxeTarget
{
    Left = 0,         // every axe at the left knight
    Right = 1,        // every axe at the right knight
    Split = 2,        // alternating left and right, one after the other: both knights at once
    BackAndForth = 3  // a few at one knight, then a few at the other, and back
}

/// <summary>What he does each time he reaches an end of his track.</summary>
public enum OverseerTurns
{
    Wrap = 0,          // off one side, back in on the other, same way
    UTurn = 1,         // turns round off-screen and comes back
    WrapWrapUTurn = 2  // wrap, wrap, U-turn, and round again
}

[System.Serializable]
public class OverseerAttack
{
    [Tooltip("Authoring only")]
    public string label;

    public OverseerAttackKind kind = OverseerAttackKind.Rock;

    [Tooltip("Rock and Together. Its own restAfter is not used — this entry's restAfter is.")]
    public RockVolley rock = new RockVolley();

    [Tooltip("Together only: seconds from his first axe landing to the rock's first landing. 0 lands them together; more lets the axes open and the rock join in.")]
    public float rockOffset;

    [Tooltip("Axes and Together. How many is the stage's to say.")]
    public AxeTarget axeTarget = AxeTarget.Split;

    [Tooltip("Axes only: Split and BackAndForth open on the right knight instead of the left.")]
    public bool startRight;

    [Tooltip("BackAndForth only: axes at one knight before switching to the other.")]
    public int axesPerTurn = 2;

    [Tooltip("Axes only: seconds between one axe leaving and the next. Never under the house floor (Spawner.MinProjectileStagger). Every axe flies for the same time, so this is also the gap between their landings.")]
    public float axeStagger = 0.12f;

    [Tooltip("BackAndForth only: extra quiet between one knight's turn and the other's, on top of the stagger.")]
    public float turnGap = 0.4f;

    [Tooltip("THE FIRING WINDOW: seconds between this entry's LAST landing and the next entry's FIRST. Measured on landings, not throws, so a far rock and a near axe cannot close it up.")]
    public float restAfter = 2.5f;

    /// <summary>One axe of an entry: which knight, and when it leaves (seconds from the first).</summary>
    public struct AxeShot
    {
        public bool Right;
        public float Release;
    }

    /// <summary>
    /// The throws an axe entry makes when <paramref name="count"/> axes are
    /// asked for. Returns the seconds from the first throw to the last.
    /// </summary>
    public float PlanAxes(int count, List<AxeShot> into)
    {
        into.Clear();
        count = Mathf.Max(1, count);
        float stagger = Mathf.Max(Spawner.MinProjectileStagger, axeStagger);
        int perTurn = Mathf.Max(1, axesPerTurn);
        float t = 0f;

        for (int i = 0; i < count; i++)
        {
            bool right;
            switch (axeTarget)
            {
                case AxeTarget.Left:  right = false; break;
                case AxeTarget.Right: right = true; break;
                case AxeTarget.Split: right = startRight ^ (i % 2 == 1); break;
                default:              right = startRight ^ ((i / perTurn) % 2 == 1); break;
            }

            into.Add(new AxeShot { Right = right, Release = t });

            if (i == count - 1) break;
            t += stagger;
            if (axeTarget == AxeTarget.BackAndForth && (i + 1) % perTurn == 0) t += Mathf.Max(0f, turnGap);
        }

        return t;
    }
}

[System.Serializable]
public class OverseerStage
{
    [Tooltip("Authoring only")]
    public string label;

    [Tooltip("This stage starts the first frame he is at or below this much health. The first stage's value is his full health.")]
    public float fromHealth = 4000f;

    [Tooltip("His speed, as a multiple of the mine's ordinary track speed. Only he speeds up — the loops never do.")]
    public float speedScale = 1.5f;

    [Tooltip("What he does at each end of his track")]
    public OverseerTurns turns = OverseerTurns.Wrap;

    [Tooltip("Axes in every axe entry of this stage")]
    public int axesPerThrow = 6;

    [Tooltip("Gnomes in each ambush that starts during this stage, the blue one included")]
    public int gnomesPerAmbush = 3;

    [Tooltip("As this stage starts, the inner and middle loops get back every crimson cart they have lost: each one falls from above into the place it started in (owner, 2026-10-20: at the start of phase 3).")]
    public bool refillInnerLoops;

    [Tooltip("Green ogres sent as this stage starts — the first stage's as he rolls in (owner, 2026-10-20: from the bottom corners, and later ones from the bottom centre)")]
    public List<OverseerOgre> ogres = new List<OverseerOgre>();

    [Tooltip("A second blue gnome rides out on his own before each bomb run in this stage, timed to come onto the loop secondBlueLead seconds before the first bomb (owner, 2026-10-20: the first three phases, not the last). Killing him opens another window; he brings no bomb run of his own and never moves the next ambush.")]
    public bool secondBlueGnome;

    [Tooltip("Played in order, round and round, for as long as the stage lasts. A new stage starts its own list from the top.")]
    public List<OverseerAttack> attacks = new List<OverseerAttack>();
}

/// <summary>One green ogre a stage sends: where it walks in from, which knight it walks at, and when.</summary>
[System.Serializable]
public class OverseerOgre
{
    [Tooltip("Authoring only")]
    public string label;

    [Tooltip("Where it enters. Off-frame (beyond x +/-10 or y +/-5.6); it walks a straight line from here to its knight.")]
    public Vector2 at;

    [Tooltip("The knight it comes for. Nearest is fine from a corner; an ogre on the centre line has to be told.")]
    public OgreMark mark = OgreMark.Nearest;

    [Tooltip("Seconds after the stage starts")]
    public float after;
}

[System.Serializable]
public class OverseerOrbDrop
{
    [Tooltip("Authoring only")]
    public string label;

    [Tooltip("Released the first frame he is at or below this much health")]
    public float atHealth = 3000f;

    [Tooltip("Health orb if on, mana orb if off")]
    public bool healthOrb = true;

    [Tooltip("Where it starts. Off-frame.")]
    public Vector2 from;

    [Tooltip("Where it is heading. Off-frame on the other side.")]
    public Vector2 to;
}
