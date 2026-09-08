using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Which knight an ogre comes for, when the wave wants to say so outright.</summary>
public enum OgreMark
{
    /// <summary>Whichever knight the entry point is nearer to. Ambiguous on the centre line — see OgreEntry.</summary>
    Nearest = 0,
    LeftKnight = 1,
    RightKnight = 2
}

/// <summary>
/// One authored arrival: a point off the edge of the frame, and the knight the
/// ogre that enters there is coming for.
///
/// THE POINT IS THE DECISION. An ogre walks a straight line from here to its
/// knight and never deviates, so the entry chooses which part of the board it
/// crosses on the way — and that is how a band is fitted to a layout. A wave
/// whose track owns the horizontal band sends its ogres down through it; a wave
/// whose shafts own the edges sends them up the middle. Coming in from the sides
/// every time would put them in whatever lane the wave already has traffic in
/// half the time and in dead space the other half.
///
/// <see cref="mark"/> exists because Nearest cannot answer an entry on the centre
/// line: an ogre dropped at (0, 8) is exactly as far from both knights, and the
/// tie-break would hand it to the same one forever.
/// </summary>
[System.Serializable]
public class OgreEntry
{
    [Tooltip("Authoring only — say what this entry crosses, e.g. \"down through the upper haul road\"")]
    public string label;

    [Tooltip("Where it walks in from. MUST be off-frame: the view is x +/-10 and y +/-5.625, so at least one of those has to be cleared or the ogre appears out of nothing in front of the players.")]
    public Vector2 at;

    [Tooltip("Which knight it comes for. Nearest reads it off the entry point, which is what you want for anything clearly on one side; name a knight outright for an entry near the centre line, where nearest is a coin toss the code has to break the same way every time.")]
    public OgreMark mark = OgreMark.Nearest;
}

/// <summary>
/// The ogres a wave sends, and the only thing a wave has to author to get them.
///
/// Arrivals come from <see cref="entries"/>, cycled in order — so a band of five
/// against a list of four uses the first entry twice, and a wave states its own
/// geometry once rather than repeating it per tier. Nothing is rolled: the same
/// tier sends the same ogres from the same points in the same order every time
/// it is fought.
///
/// With no entries authored, the band falls back to walking in alternately from
/// the left and right edges at knight height. That is a sane default rather than
/// a good one — a wave that has thought about where its ogres should come from
/// will always beat it.
/// </summary>
[System.Serializable]
public class OgreBand
{
    [Tooltip("Authoring only — names the band in the inspector")]
    public string label;

    [Tooltip("How many walk in over the wave, drawn from the entry list in order.")]
    public int count;

    [Tooltip("Seconds from the start of the wave before the first one enters. Held well back on purpose: an ogre in the opening beat is a body the players meet before they have read what the wave is.")]
    public float firstAt = 8f;

    [Tooltip("Seconds between one entering and the next. THE REAL DIAL — 80 health at 0.75 units per second is about twelve seconds of one knight's whole output, so an interval under that is a band that arrives faster than it can be cleared.")]
    public float interval = 7f;

    [Tooltip("0 = every ogre is the plain one. 3 = every third is the fire ogre, which throws at the knight it is NOT walking at. Counting, not rolling.")]
    public int fireEveryNth;

    [Tooltip("Where they walk in from, used in order and then round again. THIS is where a band is fitted to its wave — see OgreEntry. Empty falls back to alternating left and right edges at knight height.")]
    public List<OgreEntry> entries = new List<OgreEntry>();

    [Header("Fallback (only used when Entries is empty)")]
    [Tooltip("Start the edge alternation on the RIGHT. Set it on alternate TIERS: without it the left knight meets the first ogre of every band, and — because the fire cadence lands on the same slots every time — a band with exactly one fire ogre always walks it at the left knight. Authored entry lists carry the same duty by rotating their own order per tier.")]
    public bool startOnRight;

    [Tooltip("How far out the fallback enters, either side of the middle")]
    public float entryX = 11f;

    [Tooltip("The height the fallback enters at")]
    public float entryY = -0.5f;

    [Tooltip("How far the second and third ogre on a side are stepped off that line, so a queue enters as three readable bodies instead of one")]
    public float laneStep = 1.1f;

    // The visible frame. An entry inside this is an ogre appearing out of thin
    // air in front of the players.
    private const float ViewHalfWidth = 10f;
    private const float ViewHalfHeight = 5.625f;

    public int Total => Mathf.Max(0, count);

    /// <summary>
    /// Walks the band in. Every ogre is added to <paramref name="roster"/> so a
    /// wave can tell when its ogres in particular are down — a different
    /// question from whether the wave is over.
    /// </summary>
    public IEnumerator Release(Spawner spawner, List<GameObject> roster)
    {
        int total = Total;
        if (total == 0) yield break;

        yield return new WaitForSeconds(Mathf.Max(0f, firstAt));

        float gap = Mathf.Max(0.5f, interval);
        bool authored = entries != null && entries.Count > 0;

        for (int i = 0; i < total; i++)
        {
            if (i > 0) yield return new WaitForSeconds(gap);

            Vector2 at;
            OgreMark mark;

            if (authored)
            {
                OgreEntry entry = entries[i % entries.Count];
                at = entry.at;
                mark = entry.mark;
                WarnIfOnScreen(entry, i);
            }
            else
            {
                at = FallbackEntry(i);
                mark = OgreMark.Nearest;
            }

            bool fire = fireEveryNth > 0 && ((i + 1) % fireEveryNth) == 0;
            spawner.SpawnOgre(at, 0f, fire, roster, KnightFor(spawner, mark));
        }
    }

    private static Transform KnightFor(Spawner spawner, OgreMark mark)
    {
        if (mark == OgreMark.LeftKnight) return spawner.LeftPlayer;
        if (mark == OgreMark.RightKnight) return spawner.RightPlayer;
        return null; // the ogre reads the nearer knight off where it landed
    }

    // Alternating edges at knight height, stepped up and down so a queue on one
    // side enters as separate bodies. Only reached by a band with no entries.
    private Vector2 FallbackEntry(int index)
    {
        bool left = ((index % 2) == 0) != startOnRight;
        int ring = index / 2;
        float step = ((ring % 3) - 1) * laneStep;
        return new Vector2(left ? -Mathf.Abs(entryX) : Mathf.Abs(entryX), entryY + step);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void WarnIfOnScreen(OgreEntry entry, int index)
    {
        if (Mathf.Abs(entry.at.x) > ViewHalfWidth || Mathf.Abs(entry.at.y) > ViewHalfHeight) return;
        Debug.LogWarning($"[OgreBand] Entry {index} ('{entry.label}') is at {entry.at}, which is INSIDE " +
                         $"the view (x +/-{ViewHalfWidth}, y +/-{ViewHalfHeight}). The ogre will appear " +
                         "in front of the players rather than walking in from off-frame.");
    }
#else
    private void WarnIfOnScreen(OgreEntry entry, int index) { }
#endif

    /// <summary>
    /// True once every ogre this band put out is gone. Nulls are dropped as they
    /// are found, which is also what keeps the roster from growing forever.
    /// </summary>
    public static bool AllDead(List<GameObject> roster)
    {
        if (roster == null) return true;
        roster.RemoveAll(ogre => ogre == null);
        return roster.Count == 0;
    }
}
