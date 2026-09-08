using System.Collections;
using UnityEngine;

// The castle's proving ground, and the mirror system's equivalent of standing a
// track up in an empty room to watch a cart go round it.
//
// It lays a board of mirrors and spawns NOTHING. No rats, no skeletons, no rocks
// down a shaft. That is the whole design: the mechanic being tested is what
// happens to a shot, and any enemy on the board would be a second thing to look
// at while trying to read the first. A wave with nothing in it is not a wave —
// it is an instrument.
//
// Because there is nothing to kill, there is nothing to end it. A wave finishes
// when its spawning is done and the field is empty, and this one's field is empty
// from the first frame, so it would otherwise close before a single arrow had been
// fired. The hold below is what keeps it open, and it is a real duration rather
// than an endless one on purpose: a wave that never ends leaves the run with no
// way out but quitting.
//
// What to look for, with the layout the castle ships (see PallidKeepWiring, which
// authors it):
//
//   * GREEN, high above the knights — the two panes are aimed at each other, so a
//     shot that goes in HORIZONTALLY runs the corridor between them and never
//     leaves. Only a level shot loops cleanly; a shot entering at a slight climb
//     keeps that climb on every pass and walks its way out of the top. Flat
//     mirrors cannot focus, so that is the mechanic being honest rather than a
//     bug. Note also that the knights' arrows are destroyed on a four-second
//     lifetime (PlayerShooter.projectileLifetime), so "forever" is four seconds
//     of laps — plenty to watch, but it is a timer, not the mirrors giving up.
//   * CYAN, low — a tall pane twinned with a WIDE one, so the turn between their
//     facings is a right angle. A shot fired left comes out of the far side
//     travelling up. This is the case that proves the maths is a real rotation
//     and not a special case for panes that happen to be parallel.
//   * VERMILION, just below the knights — the twins face the SAME way, which is a
//     half turn. A shot into the right-hand pane comes out of the left-hand one
//     travelling LEFT: reversed as well as moved. This is the pair that shows why
//     parallel twins are rarely what a real wave wants, since the shot leaves the
//     board on the side it was sent away from.
//   * MAGENTA, top left and bottom right — a vertical wrap. A shot that leaves the
//     top of the arena comes back in at the bottom, still climbing.
//
// And the rule the design doc asks to be kept rather than fixed: a shot can come
// back and hit the knight who fired it. The vermilion pair is the one that will
// do it first.
[CreateAssetMenu(fileName = "LookingGood", menuName = "Waves/Looking Good")]
public class LookingGood : BaseWave
{
    [Header("Mirrors")]
    [Tooltip("Stood up the moment the wave starts; cleared when the next wave begins")]
    [SerializeField] private MirrorLayout mirrorLayout;

    [Header("Hold")]
    [Tooltip("Seconds the empty board stays open before the wave closes itself. Long enough to work through every pair, short enough that the run is not stuck.")]
    [SerializeField] private float viewingSeconds = 120f;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        MirrorNetwork mirrors = spawner.Mirrors;
        if (mirrors == null)
        {
            Debug.LogWarning("[LookingGood] No MirrorNetwork could be built, so there is nothing to shoot at.");
            MarkSpawningComplete();
            yield break;
        }

        if (mirrorLayout == null)
        {
            Debug.LogWarning("[LookingGood] No mirror layout wired — the wave is an empty room.");
        }

        mirrors.Lay(mirrorLayout);

        // Nothing else is coming. Said before the hold rather than after it so the
        // wave's own bookkeeping is settled while the player is still shooting.
        MarkSpawningComplete();

        yield return new WaitForSeconds(Mathf.Max(1f, viewingSeconds));
    }
}
