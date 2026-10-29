using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Back to Back — the knights save each other by turning away (pitched and
// approved 2026-10-27).
//
// The board:
//
//   * A solid wall of pillars straight down the middle of the arena (x = 0),
//     top to bottom. No shot crosses it, so neither knight can shoot into the
//     other's half. Rocks fly over it.
//   * Two mirror pairs stand on the side edges, green at y = 1.5 and cyan at
//     y = -1.5, left twinned with right. A shot out through one edge comes back
//     in through the other, still going the same way.
//   * Skeletons come in from the side edges, above and below the panes, and
//     circle on the OUTER side of their own knight.
//
// Why it plays the way it does: a skeleton keeps its shield on the nearest
// knight, which here is always its own (the other is behind the wall and further
// away). So its own knight can never hurt it. The partner cannot see it either —
// the wall is in the way. The one way in is round the back: the right knight
// fires RIGHT, out through his own edge pane, and the shot comes in at the left
// edge heading right, into the backs of the left knight's skeletons. It then
// passes through the left knight (knights do not stop arrows) and dies on the
// wall, so a missed save never carries on into the right half. Each knight saves
// the other by turning his back on his own threat — and has to aim past his own
// skeleton, which is circling in the lane out to his pane.
//
// Rock comes over the wall (RockVolley origin Centre): from the top or bottom
// middle of the screen at each knight, about 16 degrees off straight down from
// the inside. It pulls each guard inward toward the wall — away from the outward
// facing the save needs. It runs while the first group walks in and stops well
// before the first charge (rule 5).
//
// Tier ladder (five tiers in mind):
//   I    one skeleton per knight (top left, bottom right), two groups;
//        singles over the wall, alternating knights, salvo 1
//   II   two per knight (all four corner gates, a second apart), two groups;
//        over-the-wall flips, top then bottom, salvo 3, window 0.8
//   III  the middle side gates too; salvo 5, plus both knights at once
//   IV   salvo 8, flip window 0.4
//   V    salvo 8, flip window 0.1, both knights at once twice a cycle
[CreateAssetMenu(fileName = "BackToBack", menuName = "Waves/Back to Back")]
public class BackToBack : BaseWave
{
    public enum Gate
    {
        TopLeft,
        MiddleLeft,
        BottomLeft,
        TopRight,
        MiddleRight,
        BottomRight
    }

    [Header("Board")]
    [Tooltip("Stood up the moment the wave starts; cleared when the next wave begins")]
    [SerializeField] private MirrorLayout mirrorLayout;

    [Tooltip("Castle_Pillar.prefab")]
    [SerializeField] private GameObject pillarPrefab;

    [Tooltip("x of the wall, and the y of its top and bottom pillars' middles. Pillars are one unit square and stacked with no gap; +-5 runs it the full height of the view.")]
    [SerializeField] private float wallX = 0f;
    [SerializeField] private float wallTop = 5f;
    [SerializeField] private float wallBottom = -5f;

    [Tooltip("y of a skeleton's middle as it comes in through the top, middle and bottom side gates. The edge panes span 0.5 to 2.5 and -0.5 to -2.5.")]
    [SerializeField] private float topGate = 3.5f;
    [SerializeField] private float middleGate = 0f;
    [SerializeField] private float bottomGate = -3.5f;

    [Tooltip("x a skeleton is put down at, off the side of the view (the left side uses minus this)")]
    [SerializeField] private float spawnX = 12f;
    [Tooltip("x of the point just inside the edge panes a skeleton walks to before it picks a knight. Panes sit at 9.4.")]
    [SerializeField] private float entryX = 8.3f;

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
    [Tooltip("Cycled in order for Projectile Window seconds. Over the wall: origin Centre. See the file header for the tier ladder.")]
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
        spawner.Pillars.Lay(pillarPrefab, Wall());

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
                    Send(gates[i], template.BodyLift);
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

    // One single-pillar row per block of the wall, so the wall is a column. Each
    // block is turned a quarter, alternately, so the torches run down the wall
    // right, left, right (owner, 2026-10-27). Still stacked with no gap: the wall
    // has to stop every shot, and turned, a block's stone is a full 32 px tall,
    // so neighbours meet.
    //
    // The stone is drawn 2 px above the middle of its tile (the torch takes the
    // bottom four rows), so turned it would sit 2 px off to the side away from
    // its torch, and the wall would zigzag. Each block is nudged that 2 px back
    // toward its torch to keep the stones in one straight line.
    private const float TurnedStoneNudge = 2f / 32f;

    private List<PillarRow> Wall()
    {
        var rows = new List<PillarRow>();
        int count = Mathf.FloorToInt(Mathf.Abs(wallTop - wallBottom) + 0.001f) + 1;
        float from = Mathf.Min(wallTop, wallBottom);
        for (int i = 0; i < count; i++)
        {
            bool torchRight = i % 2 == 0;
            float x = wallX + (torchRight ? TurnedStoneNudge : -TurnedStoneNudge);
            rows.Add(new PillarRow
            {
                label = "wall", y = from + i, fromX = x, toX = x, step = 1f,
                turn = torchRight ? 90f : -90f
            });
        }
        return rows;
    }

    // Made where he starts, so nothing of his gets a frame anywhere else
    private void Send(Gate gate, float lift)
    {
        GameObject made = Instantiate(skeletonPrefab, Middle(gate, spawnX) - Vector2.up * lift, Quaternion.identity);
        EnemySkeleton skeleton = made.GetComponent<EnemySkeleton>();
        if (skeleton != null) skeleton.SetEntry(Middle(gate, entryX) - Vector2.up * lift);
    }

    // ---- Where the gates are ----

    private static bool IsLeft(Gate gate) => gate == Gate.TopLeft || gate == Gate.MiddleLeft || gate == Gate.BottomLeft;

    private float GateY(Gate gate)
    {
        switch (gate)
        {
            case Gate.TopLeft:
            case Gate.TopRight: return topGate;
            case Gate.MiddleLeft:
            case Gate.MiddleRight: return middleGate;
            default: return bottomGate;
        }
    }

    // Where his MIDDLE is, at this distance out on the gate's side
    private Vector2 Middle(Gate gate, float x) => new Vector2(IsLeft(gate) ? -x : x, GateY(gate));

    // ---- The timer ----

    // Seconds from the first member coming out to the last one touching a guard
    // (EnemySkeleton.EstimateRunSeconds). A side gate's skeleton always goes for
    // the knight on its own side.
    private float GroupRunSeconds(Spawner spawner, EnemySkeleton skeleton, float stagger)
    {
        float longest = 0f;
        for (int i = 0; i < gates.Count; i++)
        {
            Transform knight = IsLeft(gates[i]) ? spawner.LeftPlayer : spawner.RightPlayer;
            float run = skeleton.EstimateRunSeconds(Middle(gates[i], spawnX), Middle(gates[i], entryX),
                                                    EnemySkeleton.KnightMiddle(knight));
            longest = Mathf.Max(longest, i * stagger + run);
        }
        return longest;
    }
}
