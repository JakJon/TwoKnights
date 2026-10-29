using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Between the Mirrors — the skeleton's first wave (owner's design, 2026-10-27).
//
// The board (owner, 2026-10-27; the layout is Keep Between the Mirrors):
//
//   * Green and cyan stand on the left and right edges at y = 1.5 and -1.5,
//     green with green and cyan with cyan, so a shot out of one side comes back
//     in on the other at the same height.
//   * Orange lies flat across the top and bottom of the view over the red (left)
//     knight, purple over the blue (right) knight. A shot up out of the top comes
//     back in from the bottom, still going up.
//
// Skeletons walk in from the sides at y = 3 and -3, above and below the edge
// panes, and (Top Centre) down from the top between the orange and purple panes,
// a group at a time. Tier I: top left and bottom right. Tier II: those two and
// the top centre, three at a time (owner, 2026-10-27); the top-centre one is
// level with both knights, so he goes for the right knight in the first group
// and the left in the second. Each one walks in, picks the nearer knight, and does
// its walk-in, circle, rattle and charge (see EnemySkeleton). The next group
// comes in on a fixed timer: when the group before it is NextGroupAt (75%) of
// the way from coming out to striking, worked out from the geometry when the
// wave starts.
//
// Why the mirrors matter: a skeleton keeps its shield on the nearest knight, so
// the knight it is going for mostly sees shield. The partner can hit its side,
// and a shot sent round through a pair comes in on its side or back.
//
// Rock (2026-10-27) comes out of the corners (RockVolley origin Corner), and from
// the corner of the GATE that knight's skeletons walk in through: tier I's left
// knight from the top-left corner, its right knight from the bottom right. Each
// guard is pulled toward its own skeleton — at its shield, the one direction an
// arrow is wasted — so a knight has to turn away from the rock between volleys
// to put shots into the partner's skeleton. It runs while the first group walks
// in and stops well before it charges (rule 5). Rocks fly over the mirrors.
//
// Tier ladder (five tiers in mind), always one knight at a time until IV:
//   I    Singles then Pairs, each knight from its gate's corner, salvo 1
//   II   Flips, top corner then bottom, salvo 3, window 0.8
//   III  Flips at 0.6 and a corner Mirror (both knights at once), salvo 5
//   IV   corner Scissors, salvo 8, window 0.4
//   V    corner Scissors twice a cycle, salvo 8, window 0.1
[CreateAssetMenu(fileName = "BetweenTheMirrors", menuName = "Waves/Between the Mirrors")]
public class BetweenTheMirrors : BaseWave
{
    public enum Gate
    {
        TopLeft,
        MiddleLeft,
        BottomLeft,
        TopRight,
        MiddleRight,
        BottomRight,
        // Down from the top, between the orange and purple panes over the knights.
        // Level with both knights, so the group number says which he takes.
        TopCentre
    }

    [Header("Board")]
    [Tooltip("Stood up the moment the wave starts; cleared when the next wave begins")]
    [SerializeField] private MirrorLayout mirrorLayout;

    [Tooltip("y of the middle of the top, middle and bottom gaps between the panes")]
    [SerializeField] private float topGap = 3f;
    [SerializeField] private float middleGap = 0f;
    [SerializeField] private float bottomGap = -3f;

    [Tooltip("x a skeleton is put down at, off the side of the view (the left side uses minus this)")]
    [SerializeField] private float spawnX = 12f;
    [Tooltip("x of the point just inside the mirror column a skeleton walks to before it picks a knight. Panes sit at 9.4.")]
    [SerializeField] private float entryX = 8.3f;

    [Tooltip("Top Centre gate: y his middle is put down at, above the view, and y it walks down to before he picks a knight. The flat panes' lower edge is at 4.6 and his head is about 0.45 above his middle.")]
    [SerializeField] private float centreSpawnY = 7f;
    [SerializeField] private float centreEntryY = 3.9f;

    [Header("Skeletons")]
    [SerializeField] private GameObject skeletonPrefab;

    [Tooltip("The gates one group comes through, in the order they come out")]
    [SerializeField] private List<Gate> gates = new List<Gate> { Gate.TopLeft, Gate.BottomRight };

    [Tooltip("How many groups come in. Every group uses the same gates.")]
    [SerializeField] private int groups = 2;

    [Tooltip("Seconds between one member of a group coming out and the next. 0 sends the group together.")]
    [SerializeField] private float memberStagger = 0f;

    [Tooltip("The next group comes out when the group before it is this far through its run, from coming out to striking")]
    [Range(0.1f, 1f)]
    [SerializeField] private float nextGroupAt = 0.75f;

    [Header("Rock")]
    [Tooltip("Cycled in order for Projectile Window seconds. Rocks fly over the mirrors. See the file header for the tier ladder.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [Tooltip("Seconds of rock from the start of the wave. End it at least six seconds before the first charge.")]
    [SerializeField] private float projectileWindow = 14f;

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

        EnemySkeleton template = skeletonPrefab != null ? skeletonPrefab.GetComponent<EnemySkeleton>() : null;
        if (template == null)
        {
            Debug.LogError($"[{name}] No skeleton prefab with an EnemySkeleton on it — nothing to send.");
        }
        else
        {
            float stagger = Mathf.Max(0f, memberStagger);
            float groupRun = GroupRunSeconds(spawner, template, stagger);
            float nextAfter = groupRun * nextGroupAt;

#if UNITY_EDITOR
            Debug.Log($"[Skeletons] {name}: a group takes {groupRun:0.0}s from coming out to its last strike; " +
                      $"the next group comes out {nextAfter:0.0}s after the one before it.");
#endif

            for (int g = 0; g < Mathf.Max(1, groups); g++)
            {
                if (g > 0) yield return new WaitForSeconds(Mathf.Max(0f, nextAfter - stagger * (gates.Count - 1)));

                for (int i = 0; i < gates.Count; i++)
                {
                    if (i > 0 && stagger > 0f) yield return new WaitForSeconds(stagger);
                    // A Top Centre skeleton is level with both knights; groups take
                    // turns which knight he goes for, right first
                    Send(gates[i], g % 2 == 0);
                }
            }
        }

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

    private void Send(Gate gate, bool tieGoesRight)
    {
        // Made where he starts, not made at the origin and moved: nothing of his
        // (the shield above all) gets a frame in the middle of the arena
        float lift = BodyLift(skeletonPrefab);
        GameObject made = Instantiate(skeletonPrefab, SpawnFeet(gate, lift), Quaternion.identity);

        EnemySkeleton skeleton = made.GetComponent<EnemySkeleton>();
        if (skeleton != null)
        {
            skeleton.SetTieBreak(tieGoesRight);
            skeleton.SetEntry(EntryFeet(gate, lift));
        }
    }

    // ---- Where the gates are ----

    private static bool IsLeft(Gate gate) => gate == Gate.TopLeft || gate == Gate.MiddleLeft || gate == Gate.BottomLeft;

    private float GapY(Gate gate)
    {
        switch (gate)
        {
            case Gate.TopLeft:
            case Gate.TopRight: return topGap;
            case Gate.MiddleLeft:
            case Gate.MiddleRight: return middleGap;
            default: return bottomGap;
        }
    }

    // The skeleton's pivot is at his feet, so his feet go below the gap's middle
    // by however far his body's middle sits above them
    private Vector2 SpawnFeet(Gate gate, float lift) =>
        gate == Gate.TopCentre
            ? new Vector2(0f, centreSpawnY - lift)
            : new Vector2(IsLeft(gate) ? -spawnX : spawnX, GapY(gate) - lift);

    private Vector2 EntryFeet(Gate gate, float lift) =>
        gate == Gate.TopCentre
            ? new Vector2(0f, centreEntryY - lift)
            : new Vector2(IsLeft(gate) ? -entryX : entryX, GapY(gate) - lift);

    private static float BodyLift(GameObject skeleton)
    {
        EnemySkeleton s = skeleton.GetComponent<EnemySkeleton>();
        return s != null ? s.BodyLift : 0.45f;
    }

    // ---- The timer ----

    // Seconds from the first member coming out to the last one touching a guard
    // (EnemySkeleton.EstimateRunSeconds), so a retune of the prefab moves the
    // timer with it.
    private float GroupRunSeconds(Spawner spawner, EnemySkeleton skeleton, float stagger)
    {
        float lift = skeleton.BodyLift;
        float longest = 0f;

        for (int i = 0; i < gates.Count; i++)
        {
            Vector2 spawn = SpawnFeet(gates[i], lift) + Vector2.up * lift;
            Vector2 entry = EntryFeet(gates[i], lift) + Vector2.up * lift;

            Vector2 left = EnemySkeleton.KnightMiddle(spawner.LeftPlayer);
            Vector2 right = EnemySkeleton.KnightMiddle(spawner.RightPlayer);
            Vector2 knight = (entry - left).sqrMagnitude <= (entry - right).sqrMagnitude ? left : right;

            longest = Mathf.Max(longest, i * stagger + skeleton.EstimateRunSeconds(spawn, entry, knight));
        }

        return longest;
    }
}
