using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Wolves that lap the knights before they close.
//
// The circling is the whole tell: a wolf enters above the left knight (every
// other one under the right knight, on the same path turned half round), runs a
// figure of eight around both of them, and only then turns and comes in. The
// number of laps is therefore the timer on the whole pack — it is how long the
// players get to thin it before anything reaches them.
//
// SHIFTS, NOT ONE RELEASE. Each ambush waits for the last one to be dead
// before it sets off, so the wave is a second fight rather than a longer first
// one, and the players get a beat between them to reload.
[CreateAssetMenu(fileName = "WolfCircles", menuName = "Waves/Wolf Circles")]
public class WolfCircles : BaseWave
{
    [System.Serializable]
    public class Ambush
    {
        [Tooltip("Authoring only — names the shift in the inspector and the dev log")]
        public string label;

        [Tooltip("Wolves in this shift")]
        public int wolfCount = 6;

        [Tooltip("Laps of the figure of eight before a wolf turns on its knight. THE TIMER: every lap removed is time the players do not get.")]
        public int additionalCircles = 2;

        [Tooltip("Quiet before this shift's first wolf — the beat after the last one was cleared")]
        public float leadIn = 1f;

        [Tooltip("Seconds between one wolf entering and the next")]
        public float spawnInterval = 1f;

        [Tooltip("Cycled in order as the shift is released. Empty falls back to grey.")]
        public List<WolfType> packOrder = new List<WolfType> { WolfType.Grey };

        [Tooltip("Rocks thrown while this shift is out, one at a time, the first as it sets off. They go round in a fixed order: left knight from above, right from above, left from below, right from below. A shift killed early does not throw the rest. THE TIER DIAL for this wave's rock: 8 / 10 / 12 / 14 across tiers I to IV. 0 for none.")]
        public int rocks = 0;

        [Tooltip("Seconds between one of this shift's rocks and the next. Each knight is thrown at every other rock, so a knight has twice this long between two of his own.")]
        public float rockInterval = 3f;
    }

    [Tooltip("In order — one entry per shift. Each waits for the last to be dead before it sets off.")]
    [SerializeField] private List<Ambush> ambushes = new List<Ambush>();

    [Header("Rock")]
    // This wave threw no rock whatsoever, which rule 7 does not allow and which
    // the ambush design makes worse rather than better: a shift is not over until
    // it is dead, so a wave with nothing but wolves in it pays a slow clear with
    // silence. Rock underneath the shifts is the standing pressure the ambush
    // contract asks for — stalling now costs health instead of buying a rest.
    //
    // TWO WAYS TO AUTHOR IT, and a tier uses one of them. Tiers I to IV count
    // their rock out shift by shift (Ambush.rocks — see ThrowRock). Tier V runs
    // the volleys below under the whole wave instead. Both at once would put two
    // schedules on one knight with nothing keeping their arrivals apart.
    [Tooltip("Cycled in order underneath the whole wave (see RockVolley). Leave empty on a tier that gives its shifts rocks of their own.")]
    [SerializeField] private List<RockVolley> volleys = new List<RockVolley>();

    [Tooltip("Seconds of firing from the start of the wave. The shifts are gated on kills rather than on a clock, so this cannot be matched to the wave exactly — size it LONG, past a slow clear of the last shift, since volleys stop being issued when it runs out but whatever is already falling still lands.")]
    [SerializeField] private float projectileWindow = 60f;

    [Tooltip("World units per second for this wave's rock. 0 leaves the prefab alone.")]
    [SerializeField] private float rockSpeed = 0f;

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        // Beside the shifts, never inside them — the shifts have no length of
        // their own, so rock paced off them would stop whenever the players
        // happened to be quick.
        Coroutine shafts = spawner.StartCoroutine(
            RockVolley.WorkTheShafts(spawner, volleys, projectileWindow, rockSpeed));

        for (int i = 0; i < ambushes.Count; i++)
        {
            Ambush shift = ambushes[i];
            if (shift == null) continue;

            int count = Mathf.Max(0, shift.wolfCount);
            if (count == 0) continue;

            if (shift.leadIn > 0f) yield return new WaitForSeconds(shift.leadIn);

            BeginAmbush(count);

            // Beside the release, not after it: the first wolves are already
            // circling while the last ones are still coming in.
            Coroutine rock = shift.rocks > 0 ? spawner.StartCoroutine(ThrowRock(spawner, shift)) : null;

            yield return spawner.StartCoroutine(Release(spawner, shift, count));
            MarkAmbushReleased();

            if (i < ambushes.Count - 1)
            {
                yield return WaitForAmbushClear();

                // Stopped by hand. The next BeginAmbush opens a new group, and a
                // rock loop still waiting on its gap would read that group as its
                // own and throw what it had left on top of the next shift's.
                if (rock != null) spawner.StopCoroutine(rock);
            }
            else if (rock != null)
            {
                // The last shift is not waited on — the wave's own enemy tracking
                // closes it out, exactly as a single-release wave always has. Its
                // rock is: spawning is not complete while a throw is still owed,
                // or the wave could end with one more to come.
                yield return rock;
            }
        }

        // Waited on rather than abandoned: rock still to be issued is rock the wave
        // is responsible for, and marking spawning complete before the last volley
        // exists would let the wave end with stone still to come.
        yield return shafts;

        // Mark spawning as complete so the wave knows to start checking for enemy deaths
        MarkSpawningComplete();
    }

    private IEnumerator Release(Spawner spawner, Ambush shift, int count)
    {
        var circles = new List<Vector2> // Start with an entry point above left player
        {
            spawner.aboveLeftPlayer,
            new Vector2(-2, 2.5f)
        };

        for (int c = 0; c < Mathf.Max(0, shift.additionalCircles); c++)
        {
            circles.AddRange(WolfMovementPatterns.CircleLeftThenRight);
        }

        // Every other wolf runs the same path turned half round the middle of
        // the arena: in from under the right knight, the figure of eight the
        // other way about, and in on the left knight at the end. Alternating
        // rather than split down the middle, so both knights are busy from the
        // first pair onwards.
        var mirrored = new List<Vector2>(circles.Count);
        foreach (Vector2 point in circles) mirrored.Add(-point);

        float interval = Mathf.Max(0.1f, shift.spawnInterval);

        for (int i = 0; i < count; i++)
        {
            bool flipped = (i % 2) == 1;
            spawner.SpawnWolf(flipped ? mirrored : circles,
                flipped ? spawner.LeftPlayer : spawner.RightPlayer, TypeFor(shift, i), 0f);
            if (i < count - 1) yield return new WaitForSeconds(interval);
        }
    }

    // The floor on Ambush.rockInterval. Each rock here is its own throw, so the
    // gap between two of them is the whole of what keeps them off one frame.
    private const float MinRockInterval = 0.5f;

    // A shift's own rock: `rocks` of them, one every `rockInterval`, for as long
    // as the shift is alive.
    //
    // ONE ROCK, ONE KNIGHT, ROUND IN A FIXED ORDER: left from above, right from
    // above, left from below, right from below, and round again. Each knight is
    // therefore thrown at every other rock and from the other side each time, so
    // what he is asked is to be back over the top, then back underneath, between
    // wolves. Straight down a knight's own shaft, so nothing crosses the middle,
    // and the order starts again with every shift, so it is the same every time
    // the wave is fought.
    //
    // The first rock goes as the shift sets off. A rock is in the air for six
    // seconds or so at the forest's speed, so even that one lands with the first
    // wolves already on their lap. Authored to run out about when the first wolf
    // finishes circling and turns: the rock is for the laps, and the charge is
    // left to the arrows.
    //
    // A dead shift throws nothing more. The rock belongs to the wolves it was
    // sent with, and a quick clear is paid with the quiet it earned.
    private IEnumerator ThrowRock(Spawner spawner, Ambush shift)
    {
        float gap = Mathf.Max(MinRockInterval, shift.rockInterval);

        for (int i = 0; i < shift.rocks; i++)
        {
            // Watched every frame of the gap rather than slept through, so the
            // rock stops on the kill that ends the shift. Never clear while the
            // shift is still being released, so a first wolf shot down before
            // the second exists does not end it early.
            float throwAt = i == 0 ? Time.time : Time.time + gap;
            while (Time.time < throwAt)
            {
                if (IsAmbushClear()) yield break;
                yield return null;
            }
            if (IsAmbushClear()) yield break;

            bool right = (i % 2) == 1;
            bool below = ((i / 2) % 2) == 1;

            Transform knight = right ? spawner.RightPlayer : spawner.LeftPlayer;
            Vector2 from = right
                ? (below ? spawner.belowRightPlayer : spawner.aboveRightPlayer)
                : (below ? spawner.belowLeftPlayer : spawner.aboveLeftPlayer);

            spawner.SpawnProjectile(knight, from, 0f, rockSpeed);
        }
    }

    private static WolfType TypeFor(Ambush shift, int index)
    {
        if (shift.packOrder == null || shift.packOrder.Count == 0) return WolfType.Grey;
        return shift.packOrder[index % shift.packOrder.Count];
    }
}
