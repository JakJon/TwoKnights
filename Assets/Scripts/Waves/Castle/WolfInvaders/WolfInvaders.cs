using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Wolf Invaders — two packs snake down the outside edges while the mirrors wrap
// the arena left to right.
//
// The board (owner's design, 2026-10-26):
//
//   * Four mirror pairs line the left and right edges of the view, stacked into
//     one near-solid column per side with open space above and below it: top
//     left with top right, and so on down. Under the castle's rule a
//     shot keeps its heading through a pane, so a shot that leaves the arena on
//     one side comes back in on the other side at the same height, still going
//     the same way.
//   * One pack comes in at the top left and one at the bottom right, each a line
//     of wolves (tier I: black, brown, black; tier II: black, brown, brown, grey, grey, black — owner added the tails 2026-10-27).
//     A pack runs vertical zigzags, working inward one run at a time, and then
//     each wolf breaks off and charges its own knight: the top-left pack goes for
//     the left knight, the bottom-right pack for the right.
//
// Why the mirrors matter here: each pack runs right in front of the edge panes.
// A knight shooting OUTWARD, toward his own edge, wraps into the far side of
// the arena and hits his partner's pack from behind; a shot at his own pack that
// misses carries on round to the other one. Neither knight can comfortably kill
// his own pack alone at base fire rate, so the wrap is how they share the work.
//
// The pack holds its spacing on the path by running at one shared speed
// (EnemyWolf.SetPathSpeed), so the order the player sees is the order that
// arrives. Once a wolf leaves the path it charges at its own type's speed.
//
// Rock: the board is a half-turn copy of itself — a pack at the top left, a pack
// at the bottom right — and so is the rock (2026-10-27). Both knights are hit at
// once from opposite ends of their columns (Mirror): the left knight from above,
// where his pack starts, the right from below, where his starts, so each guard
// is pulled up or down off the edge it wants to shoot out through. Then the
// other way round. It runs while the packs are out on the far runs and stops
// before the first charge (rule 5). Rocks fly over the mirrors.
//
// Tier ladder (five tiers in mind): salvo 1 / 3 / 3 / 5 / 8. From tier III the
// Mirror becomes a Scissors (both knights then flip), its window closing
// 1.0 / 0.4 / 0.1 over III-V.
[CreateAssetMenu(fileName = "WolfInvaders", menuName = "Waves/Wolf Invaders")]
public class WolfInvaders : BaseWave
{
    [Header("Board")]
    [Tooltip("Stood up the moment the wave starts; cleared when the next wave begins")]
    [SerializeField] private MirrorLayout mirrorLayout;

    [Header("Packs")]
    [Tooltip("One pack, front to back. Both packs are the same.")]
    [SerializeField] private List<WolfType> pack = new List<WolfType> { WolfType.Black, WolfType.Brown };

    [Tooltip("World units per second along the zigzag, shared by the whole pack so it keeps its order. 2.5 is the black wolf's own pace.")]
    [SerializeField] private float snakeSpeed = 2.5f;

    [Tooltip("World units between one wolf of a pack and the next while they run the path")]
    [SerializeField] private float packGap = 3f;

    [Tooltip("x of each vertical run for the TOP-LEFT pack, outermost first. The bottom-right pack runs the same path turned half round (x flipped, starting at the bottom). Keep the innermost run at least ~2.2 units out from a knight so the pack never brushes a shield before it charges.")]
    [SerializeField] private List<float> runs = new List<float> { -8.3f, -7.3f, -6.3f, -5.3f, -4.3f };

    [Tooltip("Top and bottom ends of every run (the wolf's own position; its body sits about 0.38 above it)")]
    [SerializeField] private float runTop = 4f;
    [SerializeField] private float runBottom = -4.4f;

    [Tooltip("Seconds between the top-left pack setting off and the bottom-right one. 0 sends them together.")]
    [SerializeField] private float secondPackDelay = 0f;

    [Header("Rock")]
    [Tooltip("Cycled in order for Projectile Window seconds. Rocks fly over the mirrors. See the file header for the tier ladder.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [Tooltip("Seconds of rock from the start of the wave. A rock takes about four seconds to land at 1.75, so end it at least six seconds before the first wolf charges.")]
    [SerializeField] private float projectileWindow = 9f;

    [Tooltip("World units per second for this wave's rock. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 0f;

    [Header("Orbs")]
    [SerializeField] private OrbRun manaOrbs = new OrbRun();
    [SerializeField] private OrbRun healthOrbs = new OrbRun { healthOrb = true, count = 0 };

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        spawner.Mirrors.Lay(mirrorLayout);

        Coroutine mana = spawner.StartCoroutine(manaOrbs.Release(spawner));
        Coroutine health = spawner.StartCoroutine(healthOrbs.Release(spawner));

        Coroutine shafts = volleys != null && volleys.Count > 0
            ? spawner.StartCoroutine(RockVolley.WorkTheShafts(spawner, volleys, projectileWindow, rockSpeed))
            : null;

        List<Vector2> leftPath = SnakePath(false);
        List<Vector2> rightPath = SnakePath(true);

        float speed = Mathf.Max(0.5f, snakeSpeed);
        float spacing = Mathf.Max(0.5f, packGap) / speed;
        float second = Mathf.Max(0f, secondPackDelay);

        // Every wolf is scheduled now with its delay, so the pack's spacing is
        // exact rather than at the mercy of frame timing
        for (int i = 0; i < pack.Count; i++)
        {
            spawner.SpawnWolf(leftPath, spawner.LeftPlayer, pack[i], i * spacing, speed);
            spawner.SpawnWolf(rightPath, spawner.RightPlayer, pack[i], second + i * spacing, speed);
        }

        // SpawnWolf instantiates after its delay, so the wave holds until the
        // last wolf actually exists — otherwise an early clear of the first
        // wolves could read as the wave being over.
        float lastOut = second + Mathf.Max(0, pack.Count - 1) * spacing;
        yield return new WaitForSeconds(lastOut + 0.1f);

        if (shafts != null) yield return shafts;

        // Everything is out. An orb whose timer has not come up yet goes now
        // rather than being stopped unpaid — see OrbRun.SendFirstIfWaiting.
        manaOrbs.SendFirstIfWaiting(spawner);
        healthOrbs.SendFirstIfWaiting(spawner);
        spawner.StopCoroutine(mana);
        spawner.StopCoroutine(health);

        MarkSpawningComplete();
        yield return null;
    }

    // The zigzag: down the first run, across, up the next, across, and so on.
    // The bottom-right pack's copy is the same path turned half round, so it
    // starts at the bottom of the rightmost run and works inward going up first.
    private List<Vector2> SnakePath(bool turnedRound)
    {
        var path = new List<Vector2>();
        for (int i = 0; i < runs.Count; i++)
        {
            bool down = i % 2 == 0;
            float start = down ? runTop : runBottom;
            float end = down ? runBottom : runTop;
            path.Add(Place(runs[i], start, turnedRound));
            path.Add(Place(runs[i], end, turnedRound));
        }
        return path;
    }

    private Vector2 Place(float x, float y, bool turnedRound)
    {
        return turnedRound ? new Vector2(-x, runTop + runBottom - y) : new Vector2(x, y);
    }
}
