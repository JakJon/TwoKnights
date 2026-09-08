using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A group of rats dropped in along the top and/or bottom of the board.
///
/// The mine's haulage waves are all about a line of track and the things riding
/// it, which means every answer they ask for is a shot along that line. A flank
/// is the counter-question: vermin arriving from off the top and bottom edges,
/// nowhere near the rails, so a guard aimed at the freight is aimed away from
/// them. They are what stops a haulage wave being solved by pointing at the
/// track and holding.
///
/// Positions are AUTHORED, not rolled — a flank of five lands in the same five
/// places every time it is fought, spread evenly across the width of the view
/// and never outside it (see <see cref="Lane"/>).
/// </summary>
[System.Serializable]
public class RatFlank
{
    [Tooltip("Authoring only — names the group in the inspector")]
    public string label;

    [Tooltip("Rats that drop in along the TOP edge")]
    public int fromTop;

    [Tooltip("Rats that drop in along the BOTTOM edge")]
    public int fromBottom;

    [Tooltip("Seconds from the start of the wave before this group's first rat. Groups are staggered by giving each a later one.")]
    public float firstAt = 5f;

    [Tooltip("Seconds between one rat of this group arriving and the next")]
    public float interval = 1.2f;

    [Tooltip("Cycled in order as the group is placed. Empty means no rats — the Spawner's grey is NOT assumed, because a wave that forgot to author a type should read as empty rather than quietly spawn something.")]
    public List<GameObject> ratTypes = new List<GameObject>();

    [Tooltip("How far above and below the knights the group holds station. 4.3 keeps a rat's whole pacing sweep near the edge of the view and never within four units of a knight.")]
    public float stationHeight = 4.3f;

    [Tooltip("Horizontal gap between neighbours in the same group. The group is centred on the shaft, and the outermost pair is clamped inside the view however many there are.")]
    public float laneSpacing = 2.6f;

    /// <summary>Furthest a rat is ever stationed from the centre line.</summary>
    private const float MaxLane = 8.5f;

    public int Total => Mathf.Max(0, fromTop) + Mathf.Max(0, fromBottom);

    /// <summary>
    /// Releases the group, top row first. Every rat is added to
    /// <paramref name="roster"/> so the wave can tell when this flank in
    /// particular is dead, which is a different question from whether the wave is.
    /// </summary>
    public IEnumerator Release(Spawner spawner, List<GameObject> roster)
    {
        if (Total == 0 || ratTypes == null || ratTypes.Count == 0) yield break;

        yield return new WaitForSeconds(Mathf.Max(0f, firstAt));

        float gap = Mathf.Max(0.1f, interval);
        int placed = 0;
        int type = 0;

        for (int side = 0; side < 2; side++)
        {
            bool top = side == 0;
            int count = Mathf.Max(0, top ? fromTop : fromBottom);

            for (int i = 0; i < count; i++)
            {
                if (placed > 0) yield return new WaitForSeconds(gap);

                float x = Lane(i, count);
                float y = top ? Mathf.Abs(stationHeight) : -Mathf.Abs(stationHeight);

                // It comes in from off that edge rather than walking in from the
                // side, so "from the top" is what the player actually sees.
                Vector2 entry = new Vector2(x, top ? 9f : -9f);

                // Assigned to the knight on its own half, so the group is a
                // threat that eventually closes rather than scenery up top.
                Transform knight = x < 0f ? spawner.LeftPlayer : spawner.RightPlayer;

                GameObject prefab = ratTypes[type++ % ratTypes.Count];
                if (prefab != null)
                {
                    spawner.SpawnRat(new Vector2(x, y), prefab, 0f, knight,
                                     bypassStrengthGate: false, entryPoint: entry, roster: roster);
                }
                placed++;
            }
        }
    }

    // Evenly spread across the middle of the view and centred on the shaft. The
    // spacing is squeezed rather than the group widened once a big group would
    // otherwise reach past MaxLane, so a flank of seven is still all on screen.
    private float Lane(int index, int count)
    {
        if (count <= 1) return 0f;
        float spacing = Mathf.Max(0.5f, laneSpacing);
        float half = spacing * (count - 1) * 0.5f;
        if (half > MaxLane) spacing = MaxLane * 2f / (count - 1);
        return (index - (count - 1) * 0.5f) * spacing;
    }

    /// <summary>
    /// True once every rat this flank put out is gone. Nulls are dropped as they
    /// are found, so this is also how the roster is kept from growing forever.
    /// </summary>
    public static bool AllDead(List<GameObject> roster)
    {
        if (roster == null) return true;
        roster.RemoveAll(rat => rat == null);
        return roster.Count == 0;
    }
}
