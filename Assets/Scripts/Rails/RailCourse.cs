using System.Collections.Generic;
using UnityEngine;

// The track as a WALKABLE PATH, for the things in the mine that are not carts.
//
// A cart never needs this. MineCart rides one RailLine at a time and lets the
// teleport pads hand it on at each corner, so the shape of the whole circuit is
// something it discovers a stretch at a time and never has to hold in its head.
// A wolf cannot work that way: EnemyWolf is handed its entire route up front as
// a list of waypoints and walks it in straight lines, so anything that follows
// the rails ON FOOT needs the circuit resolved into a polyline BEFORE it is
// spawned.
//
// The corners come off the same teleport pads the carts use, so a course and a
// cart ride are the same road by construction — a pack keeps station with the
// traffic because it is literally walking the traffic's line, not a hand-typed
// approximation of it that stops matching the first time a layout is retuned.
//
// Only the corners are emitted. MoveTowards walks a straight line between
// waypoints, so a run of track needs exactly two points however long it is.
public static class RailCourse
{
    // A course cannot cross more stretches than this. On a closed loop the walk
    // would otherwise circle forever if it were ever asked for a distance longer
    // than the lap; on an open route it stops at the end of the rail by itself.
    private const int MaxStretches = 32;

    /// <summary>
    /// Where a walker joins the track so that it arrives from off-frame, in the
    /// same units <see cref="Build"/> takes. Mirrors MineCart's own lead-in: with
    /// the flow it starts behind the mouth, against it, past the far end.
    /// </summary>
    public static float MouthAlong(RailLine line, float leadIn, bool againstTheFlow)
    {
        float lead = Mathf.Max(0f, leadIn);
        return againstTheFlow ? line.Length + lead : -lead;
    }

    /// <summary>
    /// Walk the laid track from <paramref name="startAlong"/> units past the
    /// mouth of <paramref name="startLine"/> for up to <paramref name="distance"/>
    /// units, and return the corners of the path in travel order.
    /// </summary>
    /// <param name="againstTheFlow">
    /// Walk the road backwards, into the oncoming traffic. This is the whole
    /// reason the helper takes a direction rather than assuming one: a pack
    /// running with the carts keeps a frozen relationship with them (equal speeds
    /// on a loop never close), while a pack running against them meets every cart
    /// on the ring in turn.
    /// </param>
    /// <param name="outwardOffset">
    /// How far to one side of the rail the path runs, away from the middle of the
    /// board. Same convention as the drop flags (see RailNetwork.BuildDrops): the
    /// nudge is across the line's axis and outward, so a walker skirts the wall
    /// with the iron between it and the knights rather than riding inside it.
    /// </param>
    public static List<Vector2> Build(RailNetwork rails, int startLine, float startAlong,
                                      float distance, bool againstTheFlow, float outwardOffset)
    {
        var points = new List<Vector2>();
        if (rails == null || rails.LineCount == 0) return points;

        int index = Mathf.Clamp(startLine, 0, rails.LineCount - 1);
        float along = startAlong;
        float left = Mathf.Max(0f, distance);

        RailLine line;
        if (!rails.TryGetLine(index, out line)) return points;
        points.Add(Beside(line, line.PointAt(along), outwardOffset));

        for (int stretch = 0; stretch < MaxStretches && left > 0f; stretch++)
        {
            if (!rails.TryGetLine(index, out line)) break;

            // Where this stretch ends in the direction being walked, and where
            // the walk carries on afterwards. Forward that is the exit pad;
            // backward it is the pad that feeds this line, found by asking every
            // other line where its own traffic goes.
            float edge;
            int nextLine;
            float nextAlong;
            bool carriesOn;

            if (againstTheFlow)
            {
                float joinsAt;
                carriesOn = TryPreviousStretch(rails, index, out nextLine, out nextAlong, out joinsAt);
                edge = carriesOn ? joinsAt : 0f;
            }
            else
            {
                RailTeleporter pad = rails.ExitPadFor(index);
                carriesOn = pad != null && pad.Destination != null;
                edge = carriesOn ? pad.DistanceAlong : line.Length;
                nextLine = carriesOn ? pad.Destination.LineIndex : index;
                nextAlong = carriesOn ? pad.Destination.DistanceAlong : along;
            }

            float step = Mathf.Min(left, Mathf.Abs(edge - along));
            if (step > 0f)
            {
                along += againstTheFlow ? -step : step;
                left -= step;
                points.Add(Beside(line, line.PointAt(along), outwardOffset));
            }

            // An open route simply runs out: the walker gets however much road
            // there was and peels off at the end of it rather than stopping dead
            if (left <= 0f || !carriesOn) break;

            index = nextLine;
            along = nextAlong;

            // The far side of the elbow, emitted as its own waypoint. The two
            // pads sit at the same world point, but the OFFSET does not: one run
            // is nudged across y and the next across x, so without this the
            // walker cuts the corner on a diagonal that clips a good unit inside
            // it. On a course that turns three or four times that adds up to a
            // path visibly tighter than the track it is meant to be following.
            if (rails.TryGetLine(index, out line))
            {
                points.Add(Beside(line, line.PointAt(along), outwardOffset));
            }
        }

        return points;
    }

    /// <summary>
    /// Turn a one-way course into an out-and-back one: the walker runs the road,
    /// turns on the spot and runs it again, <paramref name="reversals"/> times,
    /// before it peels off the end and does whatever it was going to do anyway.
    ///
    /// This is the cheapest road there is. A wave that wants its pack exposed for
    /// longer has three ways to buy it — lay more track, slow the walkers down, or
    /// send them over the track it already has more than once — and only the last
    /// one costs nothing and changes nothing else. A longer lap needs a layout, and
    /// a slower wolf is a different animal.
    ///
    /// An ODD number of turns leaves the walker at the end it CAME IN AT, which is
    /// the other end of the board from where a one-way course would have dropped
    /// it. Callers that pick a target off the last waypoint therefore pick a
    /// different knight on an odd count than on an even one. That is the honest
    /// answer — the walker turns on whoever it finishes beside — but it is worth
    /// knowing before authoring an odd count into a wave whose two lanes are meant
    /// to belong to two different players.
    /// </summary>
    /// <param name="trimFromStart">
    /// How much of the head of the course is off-frame — the lead-in the course was
    /// built with. Every leg after the first turns at the far side of it instead of
    /// at the true start, because a walker that ran all the way back to where it
    /// came from would walk out of shot and back in, which reads as a bug rather
    /// than as a patrol.
    /// </param>
    public static List<Vector2> WithReversals(List<Vector2> course, int reversals,
                                              float trimFromStart)
    {
        if (course == null || course.Count < 2 || reversals <= 0) return course;

        // The stretch that gets walked over and over: the course with its off-frame
        // head pulled up to the mouth of the rail.
        var body = new List<Vector2>(course);
        Vector2 head = body[0];
        Vector2 next = body[1];
        float lead = Mathf.Min(Mathf.Max(0f, trimFromStart), Vector2.Distance(head, next));
        body[0] = head + (next - head).normalized * lead;

        // A lead-in longer than the first leg swallows it whole, leaving two
        // waypoints on the same spot for the walker to sit on
        if (Vector2.Distance(body[0], body[1]) < 0.01f) body.RemoveAt(0);
        if (body.Count < 2) return course;

        var walked = new List<Vector2>(course);
        for (int leg = 1; leg <= reversals; leg++)
        {
            // The turn itself needs no waypoint of its own: the walker is standing
            // on the end of the leg it just finished, so the next point in the list
            // is already behind it and it turns to face it. Re-emitting the corner
            // would only give it a waypoint it has already reached.
            if (leg % 2 == 1)
            {
                for (int i = body.Count - 2; i >= 0; i--) walked.Add(body[i]);
            }
            else
            {
                for (int i = 1; i < body.Count; i++) walked.Add(body[i]);
            }
        }

        return walked;
    }

    // Which line feeds `lineIndex`, and where. RailNetwork only publishes the
    // forward link (ExitPadFor), because that is all a cart ever needs — so
    // walking backwards means asking every line whether its traffic ends up here.
    // Cheap: a layout has a handful of runs, and a course is built once per
    // walker rather than per frame.
    private static bool TryPreviousStretch(RailNetwork rails, int lineIndex,
                                           out int previousLine, out float previousAlong, out float joinsAt)
    {
        for (int i = 0; i < rails.LineCount; i++)
        {
            RailTeleporter exit = rails.ExitPadFor(i);
            if (exit == null || exit.Destination == null) continue;
            if (exit.Destination.LineIndex != lineIndex) continue;

            previousLine = i;
            previousAlong = exit.DistanceAlong;          // where traffic leaves that line
            joinsAt = exit.Destination.DistanceAlong;    // where it arrives on this one
            return true;
        }

        previousLine = -1;
        previousAlong = 0f;
        joinsAt = 0f;
        return false;
    }

    // Across the line's own axis and outward, away from the middle of the board.
    // A per-line nudge means the two halves of a corner are offset on different
    // axes, so a walker cuts the elbow slightly wide — which is what a thing on
    // legs does anyway, and is why this is not worth reconciling.
    private static Vector2 Beside(RailLine line, Vector2 point, float outward)
    {
        if (Mathf.Approximately(outward, 0f)) return point;

        float side = line.Offset >= 0f ? 1f : -1f;
        return line.Axis == RailAxis.Horizontal
            ? new Vector2(point.x, point.y + side * outward)
            : new Vector2(point.x + side * outward, point.y);
    }
}
