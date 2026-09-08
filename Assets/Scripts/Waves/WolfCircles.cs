using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Wolves that lap the knights before they close.
//
// The circling is the whole tell: a wolf enters above the left knight, runs a
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
    }

    [Tooltip("In order — one entry per shift. Each waits for the last to be dead before it sets off.")]
    [SerializeField] private List<Ambush> ambushes = new List<Ambush>();

    public override IEnumerator SpawnWave(Spawner spawner)
    {
        for (int i = 0; i < ambushes.Count; i++)
        {
            Ambush shift = ambushes[i];
            if (shift == null) continue;

            int count = Mathf.Max(0, shift.wolfCount);
            if (count == 0) continue;

            if (shift.leadIn > 0f) yield return new WaitForSeconds(shift.leadIn);

            BeginAmbush(count);
            yield return spawner.StartCoroutine(Release(spawner, shift, count));
            MarkAmbushReleased();

            // The last shift is not waited on — the wave's own enemy tracking
            // closes it out, exactly as a single-release wave always has.
            if (i < ambushes.Count - 1) yield return WaitForAmbushClear();
        }

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

        float interval = Mathf.Max(0.1f, shift.spawnInterval);

        for (int i = 0; i < count; i++)
        {
            spawner.SpawnWolf(circles, spawner.RightPlayer, TypeFor(shift, i), 0f);
            if (i < count - 1) yield return new WaitForSeconds(interval);
        }
    }

    private static WolfType TypeFor(Ambush shift, int index)
    {
        if (shift.packOrder == null || shift.packOrder.Count == 0) return WolfType.Grey;
        return shift.packOrder[index % shift.packOrder.Count];
    }
}
