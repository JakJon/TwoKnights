using System.Collections.Generic;
using UnityEngine;

// A closed loop of track measured as one distance. A loop is several straight
// lines joined at their corners by pads, and "how far round is this cart" is a
// question none of those lines can answer alone; this walks the pads once and
// keeps the answer.
//
// Distance 0 is where a cart enters the start line (its pad's landing spot), and
// the loop's length is exactly what RailNetwork.TryMeasureLoop reports, so the
// two always agree.
public class RailLoopMap
{
    private readonly Dictionary<int, float> _offsets = new Dictionary<int, float>();
    private readonly List<int> _order = new List<int>();
    private readonly List<float> _entryAlong = new List<float>();
    private readonly List<float> _exitAlong = new List<float>();
    private readonly List<float> _startsAt = new List<float>();

    /// <summary>Distance once round.</summary>
    public float Length { get; private set; }

    /// <summary>The line the loop is measured from.</summary>
    public int StartLine { get; private set; }

    /// <summary>
    /// Walk the loop that <paramref name="startLine"/> is part of. Null if the
    /// track from there does not come back round to it.
    /// </summary>
    public static RailLoopMap Build(RailNetwork rails, int startLine)
    {
        if (rails == null) return null;

        // Where a cart lands on the start line: the far end of whichever pad
        // feeds it from inside the loop. Found by walking round once first.
        var map = new RailLoopMap { StartLine = startLine };

        int line = startLine;
        float entry = 0f;
        bool entryKnown = false;
        var visited = new HashSet<int>();

        // First lap finds the start line's own landing spot
        while (visited.Add(line))
        {
            RailTeleporter pad = rails.ExitPadFor(line);
            if (pad == null || pad.Destination == null) return null;
            if (pad.Destination.LineIndex == startLine)
            {
                entry = pad.Destination.DistanceAlong;
                entryKnown = true;
                break;
            }
            line = pad.Destination.LineIndex;
        }
        if (!entryKnown) return null;

        // Second lap records every line in order
        line = startLine;
        float along = entry;
        float total = 0f;
        visited.Clear();

        while (visited.Add(line))
        {
            RailTeleporter pad = rails.ExitPadFor(line);
            if (pad == null || pad.Destination == null) return null;

            map._order.Add(line);
            map._entryAlong.Add(along);
            map._exitAlong.Add(pad.DistanceAlong);
            map._startsAt.Add(total);
            map._offsets[line] = total - along;

            total += pad.DistanceAlong - along;
            line = pad.Destination.LineIndex;
            along = pad.Destination.DistanceAlong;

            if (line == startLine) break;
        }

        map.Length = total;
        return total > 0f ? map : null;
    }

    /// <summary>Is this line part of the loop?</summary>
    public bool Contains(int line) => _offsets.ContainsKey(line);

    /// <summary>
    /// How far round the loop a point <paramref name="along"/> units past the
    /// mouth of <paramref name="line"/> is, in [0, Length). False if the line is
    /// not part of this loop.
    /// </summary>
    public bool TryPosition(int line, float along, out float distance)
    {
        distance = 0f;
        float offset;
        if (!_offsets.TryGetValue(line, out offset)) return false;
        distance = Wrap(offset + along);
        return true;
    }

    /// <summary>Which line, and how far along it, a distance round the loop falls on.</summary>
    public void Locate(float distance, out int line, out float along)
    {
        distance = Wrap(distance);
        for (int i = _order.Count - 1; i >= 0; i--)
        {
            if (distance >= _startsAt[i])
            {
                line = _order[i];
                along = _entryAlong[i] + (distance - _startsAt[i]);
                return;
            }
        }
        line = _order.Count > 0 ? _order[0] : StartLine;
        along = _entryAlong.Count > 0 ? _entryAlong[0] + distance : distance;
    }

    /// <summary>Folds any distance into [0, Length).</summary>
    public float Wrap(float distance)
    {
        if (Length <= 0f) return 0f;
        distance %= Length;
        if (distance < 0f) distance += Length;
        return distance;
    }

    /// <summary>Shortest way between two distances round the loop, either direction.</summary>
    public float Gap(float a, float b)
    {
        float d = Mathf.Abs(Wrap(a) - Wrap(b));
        return Mathf.Min(d, Length - d);
    }
}
