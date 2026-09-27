using System.Collections;
using System.Net.NetworkInformation;
using UnityEngine;

[CreateAssetMenu(fileName = "SlimyWave", menuName = "Waves/Slimy")]
public class Slimy : BaseWave
{
    [SerializeField] private int waveOccurences = 1;
    [Tooltip("Which occurrence the wave opens on. The occurrence index picks the slime tier, " +
             "so this is the difficulty of the first group, not just an offset: 1 is the plain " +
             "tier-1 group, 2 leads with mid slimes, 3 with kings.")]
    [SerializeField] private int firstOccurrence = 1;
    [SerializeField] private int projectilesPerStraight = 1;
    // Seconds, so it has to be a float: authored as an int it silently truncated
    // anything under a second to zero, and a zero here means the whole line leaves
    // on the stagger floor as one unblockable wall.
    [SerializeField] private float delayBetweenProjectiles = 1f;
    [SerializeField] private float delayBetweenStraights = 1f;
    [SerializeField] private int amountOfStraights = 8;
    [Tooltip("Amount of base slimes, if mid slimes / 2, if king slime / 3")]
    [SerializeField] private int amountOfSlimesOnEachSide = 4;

    [Header("Tier III and up")]
    [Tooltip("Turns the wall into an AMBUSH the wave waits on, instead of a fixed schedule. " +
             "The old shape ran its straights on a stopwatch whatever the players did, which is " +
             "why the deep tiers dragged: the slimes were long dead and the rock was still coming. " +
             "Here the group sets the pace - one knight faces a ring of rock, the other a straight " +
             "line that keeps arriving until the wall is down, and the wave moves on the moment it is.")]
    [SerializeField] private bool ambushWall = false;

    [Tooltip("Seconds between one vertical slot of the wall and the next. The wall used to arrive " +
             "in one frame at every height at once, which reads as a single object rather than as " +
             "something closing in. Stagger it and the knights can work it from one end.")]
    [SerializeField] private float slimeStagger = 0.6f;

    [Tooltip("Seconds between straight-line volleys while the wall is alive")]
    [SerializeField] private float straightInterval = 2.5f;

    [Tooltip("Rocks in each pass of the ring, spread over its half-circle")]
    [SerializeField] private int ringRocks = 5;

    [Tooltip("Seconds between one pass of the ring and the next")]
    [SerializeField] private float ringInterval = 3.5f;

    // The ring is a HALF circle on the knight's own side, never a full one: the far
    // half of a full ring sits past the other knight, and a rock thrown from there
    // is absorbed by the wrong shield before it ever arrives (design rule 2).
    // Bodies in one side's column, however the arithmetic falls out. The group is
    // what the wave WAITS ON and slimes split, so five size-twos is already fifteen
    // bodies before it reads as clear.
    private const int MaxSlimesPerGroup = 5;

    // ONE, never a dial (owner, 2026-09-15). The knight under the straight line is
    // holding an angle against a file of rock, and a shield facing is also a
    // shooting direction - they cannot answer a wall and hold the line at once.
    // Anything more than a single slime on that side is a bill, not a fight.
    private const int SlimesOnStraightSide = 1;

    private const float RingDegrees = 180f;
    private const float RingRadius = 12f;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        // Counting from firstOccurrence rather than 1: the index is the tier
        // selector below, so skipping the soft opener has to mean starting later
        // in the sequence, not running fewer of it from the front.
        int lastOccurrence = firstOccurrence + waveOccurences - 1;

        if (ambushWall)
        {
            yield return AmbushWalls(spawner, lastOccurrence);
            MarkSpawningComplete();
            yield break;
        }

        for (int wave = firstOccurrence; wave <= lastOccurrence; wave++)
        {

            // Spawn slimes based on the wave number
            if (wave % 2 == 0)
            {
                spawner.SpawnOrb(new Vector2(-12, 3), new Vector2(12, 3), false);
                // variable that divides the amount of slimes by 2 and rounds down
                int midSlimes = Mathf.FloorToInt(amountOfSlimesOnEachSide / 2f);
                for (int i = 0; i < midSlimes; i++)
                {
                    spawner.SpawnSlime(2, new Vector2(-12 - (i * 2), i * 4), 0f, spawner.LeftPlayer);
                    spawner.SpawnSlime(2, new Vector2(12 + (i * 2), -i * 4), 0f, spawner.RightPlayer);
                }
            }
            else if (wave % 3 == 0)
            {
                spawner.SpawnOrb(new Vector2(-12, -3), new Vector2(12, -3), true);
                // variable that divides the amount of slimes by 3 and rounds down
                int kingSlimes = Mathf.FloorToInt(amountOfSlimesOnEachSide / 3f);
                for (int i = 0; i < kingSlimes; i++)
                {
                    spawner.SpawnSlime(3, new Vector2(-12 - (i * 2), (i + 1)* 5), 0f, spawner.LeftPlayer);
                    spawner.SpawnSlime(3, new Vector2(12 + (i * 2), (-i - 1) * 5), 0f, spawner.RightPlayer);
                }
            }
            else
            {
                for (int i = 0; i < amountOfSlimesOnEachSide; i++)
                {
                    spawner.SpawnSlime(1, new Vector2(-12 - (i * 2), i * 2), 0f, spawner.LeftPlayer);
                    spawner.SpawnSlime(1, new Vector2(12 + (i * 2), -i * 2), 0f, spawner.RightPlayer);
                }
            }

            for (int straight = 1; straight < amountOfStraights; straight++)
            {
                if (straight % 2 == 0)
                {
                    spawner.SpawnProjectileStraight(
                        spawner.aboveLeftPlayer, 
                        spawner.LeftPlayer, 
                        projectilesPerStraight, 
                        delayBetweenProjectiles
                     );

                    spawner.SpawnProjectileStraight(
                        spawner.belowRightPlayer,
                        spawner.RightPlayer,
                        projectilesPerStraight,
                        delayBetweenProjectiles
                     );
                }
                else                 {
                    spawner.SpawnProjectileStraight(
                        spawner.belowLeftPlayer,
                        spawner.LeftPlayer,
                        projectilesPerStraight,
                        delayBetweenProjectiles
                     );
                    spawner.SpawnProjectileStraight(
                        spawner.aboveRightPlayer,
                        spawner.RightPlayer,
                        projectilesPerStraight,
                        delayBetweenProjectiles
                     );
                }
                yield return new WaitForSeconds(delayBetweenStraights);
            }
        }
        
        // Mark spawning as complete so the wave knows to start checking for enemy deaths
        MarkSpawningComplete();
        
        // The wave will now automatically complete when all enemies are killed
        yield return null; // Required for IEnumerator even though we're not waiting
    }

    // ---- Tier III and up ----

    /// <summary>
    /// One group of wall per occurrence, each waited on before the next is sent.
    /// While a wall is alive one knight is under a ring of rock and the other under
    /// a straight line, and WHICH knight gets which alternates by occurrence — so a
    /// wave teaches both problems rather than handing one player the same job twice.
    /// </summary>
    private IEnumerator AmbushWalls(Spawner spawner, int lastOccurrence)
    {
        // FIXED FOR THE WHOLE WAVE, never alternating. Swapping the two patterns
        // between occurrences meant a knight who had just been reading a straight
        // line was handed a ring while the last of that line was still in the air —
        // two streams from two directions on one shield, which is unblockable
        // rather than hard. Each knight learns one problem and keeps it.
        Transform ringKnight     = spawner.LeftPlayer;
        Transform straightKnight = spawner.RightPlayer;

        for (int wave = firstOccurrence; wave <= lastOccurrence; wave++)
        {

            int perSide = SlimesPerSide(wave);
            int size = SlimeSize(wave);

            // Declared, because every one of these spawns after a delay and so
            // registers late. A group whose first slime dies before its second
            // exists reads as clear, and the next wall lands on top of it.
            BeginAmbush(perSide + SlimesOnStraightSide);
            SpawnWall(spawner, wave, size, perSide, ringKnight, straightKnight);
            MarkAmbushReleased();

            // Both patterns run for exactly as long as the wall does
            Coroutine ring     = spawner.StartCoroutine(RingUntilClear(spawner, ringKnight));
            Coroutine straight = spawner.StartCoroutine(StraightUntilClear(spawner, straightKnight));

            yield return WaitForAmbushClear();
            yield return ring;
            yield return straight;

            // Both patterns stop the moment the wall is down, but whatever they
            // released last is still in the air. Let it land before the next wall
            // opens, or the tail of one occurrence arrives on top of the head of
            // the next.
            yield return new WaitForSeconds(TailSeconds(spawner, ringKnight));
        }
    }

    /// <summary>
    /// How long the last thing a pattern released needs before it has arrived:
    /// the release itself plus the flight in from the ring radius.
    /// </summary>
    private float TailSeconds(Spawner spawner, Transform knight)
    {
        float release = Mathf.Max(PassSeconds(Mathf.Max(2, ringRocks)),
                                  PassSeconds(projectilesPerStraight));
        float flight = spawner.ProjectileArcFlightSeconds(
            knight, new Vector2(knight.position.x, RingRadius));
        return release + flight;
    }

    /// <summary>
    /// A slot on one edge, stacked OUTWARD from it. The sign matters and got this
    /// wrong once: written as edge minus sign(edge) * offset, the left column walks
    /// back IN across the arena — -12, -10, -8 ... and by the seventh slime it is
    /// spawning at x = 0, in the middle of the view, in front of the players.
    /// Everything spawns out of frame or it is not a wave, it is a magic trick.
    /// </summary>
    private static Vector2 EdgeSlot(float edge, int index, float y)
    {
        return new Vector2(edge + Mathf.Sign(edge) * (index * 2f), y);
    }

    /// <summary>Seconds a single file or arc takes to finish LEAVING.</summary>
    private float PassSeconds(int count)
    {
        return Mathf.Max(0, count - 1) * Mathf.Max(Spawner.MinProjectileStagger, delayBetweenProjectiles);
    }

    // Sizes are picked the same way the fixed path picks them, so a tier reads the
    // same whichever path it is on.
    private int SlimeSize(int wave)
    {
        if (wave % 2 == 0) return 2;
        if (wave % 3 == 0) return 3;
        return 1;
    }

    private int SlimesPerSide(int wave)
    {
        int size = SlimeSize(wave);
        int n = size == 2 ? Mathf.FloorToInt(amountOfSlimesOnEachSide / 2f)
              : size == 3 ? Mathf.FloorToInt(amountOfSlimesOnEachSide / 3f)
              : amountOfSlimesOnEachSide;
        // Capped, because the group is what the wave WAITS ON and slimes split:
        // seven size-twos is already twenty-one bodies before it reads as clear.
        // The divisors keep the slime MASS even across the sizes, which is not the
        // same thing as keeping the number of targets sane.
        return Mathf.Clamp(n, 1, MaxSlimesPerGroup);
    }

    /// <summary>
    /// The wall itself: a column of slimes down each edge, one slot at a time
    /// rather than every height on one frame.
    /// </summary>
    /// <summary>
    /// The wall, and it is NOT symmetrical. The ring knight gets the column — they
    /// are reading rock that sweeps around them and can break off to shoot between
    /// passes. The knight under the straight line gets a handful and no more: their
    /// guard is pointed at the file and pointing it anywhere else is taking the
    /// file on the chin, so a wall on that side is not a fight, it is a bill.
    /// </summary>
    private void SpawnWall(Spawner spawner, int wave, int size, int perSide,
                           Transform ringKnight, Transform straightKnight)
    {
        if (wave % 2 == 0) spawner.SpawnOrb(new Vector2(-12, 3), new Vector2(12, 3), false);
        else if (wave % 3 == 0) spawner.SpawnOrb(new Vector2(-12, -3), new Vector2(12, -3), true);

        float ringEdge     = ringKnight == spawner.LeftPlayer ? -12f : 12f;
        float straightEdge = ringKnight == spawner.LeftPlayer ? 12f : -12f;

        for (int i = 0; i < perSide; i++)
        {
            float delay = i * Mathf.Max(0f, slimeStagger);
            float y = perSide > 1 ? Mathf.Lerp(-5.5f, 5.5f, (float)i / (perSide - 1)) : 0f;
            spawner.SpawnSlime(size, EdgeSlot(ringEdge, i, y), delay, ringKnight);
        }

        // Exactly one, coming in level with the knight so it is a single clean
        // thing to time between passes of the file.
        for (int i = 0; i < SlimesOnStraightSide; i++)
        {
            spawner.SpawnSlime(size, EdgeSlot(straightEdge, i, 0f),
                               Mathf.Max(0f, slimeStagger), straightKnight);
        }
    }

    /// <summary>
    /// Rock from all around one knight's own half, a pass at a time, for as long as
    /// the wall stands. Half a circle, never a full one — see RingDegrees.
    /// </summary>
    private IEnumerator RingUntilClear(Spawner spawner, Transform knight)
    {
        bool left = knight == spawner.LeftPlayer;
        // Opens above the knight and sweeps down the outside: every spawn point
        // stays on that knight's own side of the arena.
        Vector2 start = new Vector2(knight.position.x, RingRadius);
        var direction = left ? Spawner.ArcDirection.CounterClockwise
                             : Spawner.ArcDirection.Clockwise;

        while (!IsAmbushClear())
        {
            spawner.SpawnProjectileArc(knight, direction, start, RingDegrees,
                                       Mathf.Max(2, ringRocks), delayBetweenProjectiles);

            // Measured from the END of the pass, not its start. A gap measured the
            // other way is eaten by the pass's own release time, and the next pass
            // opens while this one is still leaving.
            float wait = PassSeconds(Mathf.Max(2, ringRocks)) + ringInterval;
            float waited = 0f;
            while (waited < wait && !IsAmbushClear())
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }
    }

    /// <summary>
    /// A straight line at the other knight, alternating over and under, until the
    /// wall is down. The alternation is what stops it being a single held angle.
    /// </summary>
    private IEnumerator StraightUntilClear(Spawner spawner, Transform knight)
    {
        bool left = knight == spawner.LeftPlayer;
        int pass = 0;

        while (!IsAmbushClear())
        {
            Vector2 from = (pass % 2 == 0)
                ? (left ? spawner.aboveLeftPlayer : spawner.aboveRightPlayer)
                : (left ? spawner.belowLeftPlayer : spawner.belowRightPlayer);

            spawner.SpawnProjectileStraight(from, knight, projectilesPerStraight,
                                            delayBetweenProjectiles);
            pass++;

            // From the END of the file. This is the one that made the wave
            // impossible: a file of eleven rocks takes four and a half seconds to
            // leave, the next pass was opening after two and a bit, and the two
            // passes come from OPPOSITE sides — so for two seconds the knight was
            // being thrown at from above and below at once, with one shield.
            float wait = PassSeconds(projectilesPerStraight) + straightInterval;
            float waited = 0f;
            while (waited < wait && !IsAmbushClear())
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }
    }
}