using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HoldTheLine", menuName = "Waves/Hold The Line")]
public class HoldTheLine : PinnedWave
{
    // Mob half of the pinning family (see PinnedWave). The pinned knight eats the
    // rock belt for the whole wave; every beat below lands on the FREE knight, who
    // has to clear all of it alone.
    //
    // EVERY WOLF STALKS BEFORE IT COMMITS. A wolf that walks straight in covers the
    // 11 units from the edge to its knight in about 3.5s, but base time-to-kill is
    // 4.5s brown / 7.5s grey / 9s black - so a straight approach cannot be answered,
    // only eaten, and eating it on the shield ends the special streak. Each wolf
    // therefore paces an exposed lane on the free knight's half first, and wolves in
    // the same beat pace for STAGGERED lengths so they commit one at a time rather
    // than as an unkillable clump.
    //
    // Beat times are spread to cover the pin window, so the pinned knight is never
    // holding a belt while their partner has nothing left to do.
    //
    // Every bat spawns on the side that sends it at the free knight (see
    // PinnedWave.BatSpawnX), so the post-gate dark bat can never confuse the knight
    // who is holding the belt.

    [Tooltip("1-4. Selects the authored mob progression")]
    [SerializeField] private int tier = 1;
    [Tooltip("Seconds the first wolf of a beat paces in the open before it commits")]
    [SerializeField] private float wolfPaceSeconds = 10f;
    [Tooltip("Extra pacing seconds for each further wolf in the same beat, so they arrive in sequence")]
    [SerializeField] private float wolfPaceStagger = 6f;
    [Tooltip("Scales every gap between beats; below 1 tightens the whole wave")]
    [SerializeField] private float beatSpacingFactor = 1f;

    // EnemyWolf.SetWolfType
    private const float BrownSpeed = 3.5f;
    private const float GreySpeed = 3f;
    private const float BlackSpeed = 2.5f;

    protected override IEnumerator SpawnFreeKnightContent(Spawner spawner)
    {
        switch (Mathf.Clamp(tier, 1, 4))
        {
            case 2:  return this.TierTwo(spawner);
            case 3:  return this.TierThree(spawner);
            case 4:  return this.TierFour(spawner);
            default: return this.TierOne(spawner);
        }
    }

    // Tier 1 - about 23 base arrows across a 38s pin. The belt never moves.
    private IEnumerator TierOne(Spawner spawner)
    {
        Transform knight = this.FreeTransform(spawner);
        float side = FreeSideSign;

        // Read-and-shoot warmup while the belt settles into its rhythm
        spawner.SpawnBat(new Vector2(this.BatSpawnX(11f), 5f), 0f);
        spawner.SpawnBat(new Vector2(this.BatSpawnX(11f), -5f), 1.2f);
        yield return this.Beat(7f);

        // One grey wolf, pacing well past its 7.5s kill time
        spawner.SpawnWolf(this.StalkPath(side, 8f, wolfPaceSeconds, GreySpeed), knight, WolfType.Grey);
        yield return this.Beat(15f);

        // Bats over a slime. The slime is slow, so the free knight chooses what to
        // spend each reload on rather than being told
        spawner.SpawnSlime(2, new Vector2(side * 11f, -4f), 0f, knight);
        spawner.SpawnBat(new Vector2(this.BatSpawnX(11f), 6f), 1.5f);
        spawner.SpawnBat(new Vector2(this.BatSpawnX(12f), -6f), 3f);
        yield return this.Beat(9f);

        // Closer: two grey wolves pacing different lanes for different lengths, so
        // the second is still exposed while the first is being finished
        spawner.SpawnWolf(this.StalkPath(side, 8f, wolfPaceSeconds, GreySpeed), knight, WolfType.Grey, 0f);
        spawner.SpawnWolf(this.StalkPath(side, 6f, wolfPaceSeconds + wolfPaceStagger, GreySpeed), knight, WolfType.Grey, 0f);
    }

    // Tier 2 - about 26 base arrows across a 44s pin, one reposition.
    private IEnumerator TierTwo(Spawner spawner)
    {
        Transform knight = this.FreeTransform(spawner);
        float side = FreeSideSign;

        spawner.SpawnBat(new Vector2(this.BatSpawnX(11f), 5.5f), 0f);
        spawner.SpawnBat(new Vector2(this.BatSpawnX(12f), -5.5f), 1.6f);
        spawner.SpawnBat(new Vector2(this.BatSpawnX(11f), 3f), 3.2f);
        yield return this.Beat(11f);

        // Two wolves, but sequenced: the brown commits first and dies fast, the grey
        // is still pacing when it does
        spawner.SpawnWolf(this.StalkPath(side, 8f, wolfPaceSeconds, BrownSpeed), knight, WolfType.Brown, 0f);
        spawner.SpawnWolf(this.StalkPath(side, 6f, wolfPaceSeconds + wolfPaceStagger, GreySpeed), knight, WolfType.Grey, 0f);
        yield return this.Beat(17f);

        // Crux: the rat pair and the king slime arrive together. Two rats is the
        // ceiling for one knight, and the slime splits twice on top of that
        spawner.SpawnRat(new Vector2(side * 7f, 3f), spawner.brownRat, 0f, knight);
        spawner.SpawnRat(new Vector2(side * 7f, -3f), spawner.brownRat, 0.8f, knight);
        spawner.SpawnSlime(3, new Vector2(side * 11f, 0f), 0f, knight);
    }

    // Tier 3 - wolf-heavy, plus the first dark bat. 51s pin, two repositions.
    private IEnumerator TierThree(Spawner spawner)
    {
        Transform knight = this.FreeTransform(spawner);
        float side = FreeSideSign;

        // Nine arrows of wolf off the banner, staggered so they never land together
        spawner.SpawnWolf(this.StalkPath(side, 8f, wolfPaceSeconds, BlackSpeed), knight, WolfType.Black, 0f);
        spawner.SpawnWolf(this.StalkPath(side, 6f, wolfPaceSeconds + wolfPaceStagger, BrownSpeed), knight, WolfType.Brown, 0f);
        yield return this.Beat(18f);

        // Four bats on a steady beat. Post-gate the 4th call of the wave is the dark
        // bat, so the confusion lands here, on the free knight, mid-pin
        spawner.SpawnBat(new Vector2(this.BatSpawnX(11f), 6f), 0f);
        spawner.SpawnBat(new Vector2(this.BatSpawnX(12f), -6f), 1.4f);
        spawner.SpawnBat(new Vector2(this.BatSpawnX(11f), 4f), 2.8f);
        spawner.SpawnBat(new Vector2(this.BatSpawnX(12f), -4f), 4.2f);
        yield return this.Beat(16f);

        // Closer: a rat pair and a bat pair together, ground and air at once
        spawner.SpawnRat(new Vector2(side * 7f, 2.5f), spawner.brownRat, 0f, knight);
        spawner.SpawnRat(new Vector2(side * 7f, -2.5f), spawner.brownRat, 0.7f, knight);
        spawner.SpawnBat(new Vector2(this.BatSpawnX(11f), 5f), 1.5f);
        spawner.SpawnBat(new Vector2(this.BatSpawnX(12f), -5f), 2.5f);
    }

    // Tier 4 - about 47 base arrows across a 60s pin, three repositions. Only
    // clearable by an upgraded knight, which is the point of a wave-17+ slot.
    private IEnumerator TierFour(Spawner spawner)
    {
        Transform knight = this.FreeTransform(spawner);
        float side = FreeSideSign;

        // Three wolves on three lanes, each pacing longer than the last so they
        // arrive in single file instead of as one unanswerable wall
        spawner.SpawnWolf(this.StalkPath(side, 9f, wolfPaceSeconds, BlackSpeed), knight, WolfType.Black, 0f);
        spawner.SpawnWolf(this.StalkPath(side, 7f, wolfPaceSeconds + wolfPaceStagger, BlackSpeed), knight, WolfType.Black, 0f);
        spawner.SpawnWolf(this.StalkPath(side, 5f, wolfPaceSeconds + wolfPaceStagger * 2f, BrownSpeed), knight, WolfType.Brown, 0f);
        yield return this.Beat(24f);

        // Air and ground together: four bats over two brown rats
        spawner.SpawnBat(new Vector2(this.BatSpawnX(11f), 6f), 0f);
        spawner.SpawnBat(new Vector2(this.BatSpawnX(12f), -6f), 1f);
        spawner.SpawnBat(new Vector2(this.BatSpawnX(11f), 4f), 2f);
        spawner.SpawnBat(new Vector2(this.BatSpawnX(12f), -4f), 3f);
        spawner.SpawnRat(new Vector2(side * 7f, 3f), spawner.brownRat, 0f, knight);
        spawner.SpawnRat(new Vector2(side * 7f, -3f), spawner.brownRat, 0.8f, knight);
        yield return this.Beat(18f);

        // Closer: two king slimes and two bats, all at once
        spawner.SpawnSlime(3, new Vector2(side * 11f, 3f), 0f, knight);
        spawner.SpawnSlime(3, new Vector2(side * 11f, -3f), 0.6f, knight);
        spawner.SpawnBat(new Vector2(this.BatSpawnX(11f), 5.5f), 1.2f);
        spawner.SpawnBat(new Vector2(this.BatSpawnX(12f), -5.5f), 2.2f);
    }

    private WaitForSeconds Beat(float seconds)
    {
        return new WaitForSeconds(Mathf.Max(0.1f, seconds * beatSpacingFactor));
    }

    // Enter from off-screen, then pace a vertical lane on the free knight's half for
    // roughly paceSeconds before the path runs out and EnemyWolf switches to chasing
    // (EnemyWolf.FollowPath). laneX is measured from the arena centre, so separate
    // lanes keep simultaneous wolves visually distinct and individually shootable.
    private List<Vector2> StalkPath(float side, float laneX, float paceSeconds, float speed)
    {
        const float laneHalf = 2.5f;
        const float entryX = 13f;

        List<Vector2> path = new List<Vector2> { new Vector2(side * entryX, -laneHalf) };

        // The walk in from the edge is already exposed time, so it counts against
        // the budget rather than being added on top of it
        float budget = Mathf.Max(0f, paceSeconds * speed - Mathf.Abs(entryX - laneX));
        int legs = Mathf.Max(1, Mathf.RoundToInt(budget / (laneHalf * 2f)));
        for (int i = 0; i < legs; i++)
        {
            path.Add(new Vector2(side * laneX, i % 2 == 0 ? laneHalf : -laneHalf));
        }
        return path;
    }
}

