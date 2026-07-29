using System.Collections.Generic;
using UnityEngine;

// Which rail sprite a cell uses. Only Horizontal has art today; the rest are
// declared so layouts (and the piece table on RailNetwork) can be authored
// against the full vocabulary before the sprites land — an unmapped kind falls
// back to the horizontal piece and logs once.
public enum RailPieceKind
{
    Horizontal = 0,
    Vertical = 1,
    CornerNorthEast = 2, // track enters from the north, leaves to the east
    CornerNorthWest = 3,
    CornerSouthEast = 4,
    CornerSouthWest = 5
}

public enum RailAxis
{
    Horizontal = 0,
    Vertical = 1
}

// Which way traffic runs on a stretch of track. The axis says whether the run
// lies east-west or north-south; this says which of its two ends is the mouth.
// Carts read it and never decide for themselves — "follow the way the rail is
// facing" is a property of the track, authored on the layout.
public enum RailFlow
{
    Forward = 0, // east on a Horizontal run, north on a Vertical one
    Reverse = 1
}

// One straight line of rail pieces. Every run is axis-aligned, so a run is
// fully described by its axis, the cross-axis coordinate it sits on, and where
// along the axis it starts and stops.
[System.Serializable]
public struct RailRun
{
    [Tooltip("Editor-only note, e.g. \"upper track\"")]
    public string label;

    public RailPieceKind piece;
    public RailAxis axis;

    [Tooltip("Cross-axis world coordinate: the y of a Horizontal run, the x of a Vertical one")]
    public float offset;

    [Tooltip("Run the whole camera width (Horizontal) or height (Vertical) instead of using start/count, so the track leaves the frame on both sides")]
    public bool spanViewport;

    [Tooltip("Extra cells past each viewport edge when spanning — keeps the ends off-screen at any aspect")]
    public int overhangCells;

    [Tooltip("Along-axis world coordinate of the first cell (ignored when spanning)")]
    public float start;

    [Tooltip("Cells in the run (ignored when spanning)")]
    public int count;

    [Tooltip("Which way carts travel. Forward = east on a Horizontal run, north on a Vertical one.")]
    public RailFlow flow;

    [Tooltip("Lay this run from its far end back, reversing the fall cascade. Cosmetic only — it does not change which way carts travel.")]
    public bool layFromFarEnd;
}

// Where a single piece ends up, in the order it should fall
public struct RailPlacement
{
    public RailPieceKind Piece;
    public Vector2 Position;
}

// A run resolved into world space and oriented in travel order: a rider enters
// at Entry and leaves Length units later, walking in Sign's direction.
//
// This is the whole contract a cart needs. It deliberately says nothing about
// the individual pieces, so a cart can be released onto a line while the track
// behind it is still cascading into place.
public struct RailLine
{
    public string Label;
    public RailAxis Axis;

    /// <summary>Cross-axis world coordinate: the y of a Horizontal line, the x of a Vertical one.</summary>
    public float Offset;

    /// <summary>Along-axis world coordinate of the cell a rider enters at.</summary>
    public float Entry;

    /// <summary>Along-axis distance from the entry cell to the exit cell.</summary>
    public float Length;

    /// <summary>+1 when travel runs towards increasing world coordinates, -1 otherwise.</summary>
    public float Sign;

    /// <summary>Unit vector a rider travels along.</summary>
    public Vector2 Direction => Axis == RailAxis.Horizontal
        ? new Vector2(Sign, 0f)
        : new Vector2(0f, Sign);

    /// <summary>
    /// World point <paramref name="distanceFromEntry"/> units along the line.
    /// Negative values sit behind the mouth and positive ones past the exit, so
    /// a rider can lead in from off-screen and roll out the far side.
    /// </summary>
    public Vector2 PointAt(float distanceFromEntry)
    {
        float along = Entry + Sign * distanceFromEntry;
        return Axis == RailAxis.Horizontal
            ? new Vector2(along, Offset)
            : new Vector2(Offset, along);
    }

    public Vector2 EntryPoint => PointAt(0f);
    public Vector2 ExitPoint => PointAt(Length);
}

// A named track shape a wave can ask for: "straight horizontal above the
// knights", "a loop", "vertical shafts". Waves reference the asset rather than
// hardcoding coordinates, so the mine's track can be retuned without a recompile.
[CreateAssetMenu(fileName = "RailLayout", menuName = "Maps/Rail Layout")]
public class RailLayout : ScriptableObject
{
    [Tooltip("World size of one rail piece. The 32px sprite at 32 PPU is 1 unit.")]
    [SerializeField] private float cellSize = 1f;

    [Tooltip("Seconds between one piece landing and the next starting to fall. 0 = the whole track drops at once.")]
    [SerializeField] private float fallStagger = 0.15f;

    [SerializeField] private List<RailRun> runs = new List<RailRun>();

    public float CellSize => Mathf.Max(0.01f, cellSize);
    public float FallStagger => Mathf.Max(0f, fallStagger);
    public IReadOnlyList<RailRun> Runs => runs;

    // Expands the runs into ordered placements. `viewHalfExtents` is the
    // camera's visible half-width/half-height in world units and `viewCenter`
    // its centre — both only matter for spanViewport runs.
    public void BuildPlacements(List<RailPlacement> into, Vector2 viewCenter, Vector2 viewHalfExtents)
    {
        if (into == null) return;
        into.Clear();

        float cell = CellSize;

        foreach (var run in runs)
        {
            bool horizontal = run.axis == RailAxis.Horizontal;

            float first;
            int cellCount;
            ResolveRun(run, viewCenter, viewHalfExtents, out first, out cellCount);

            for (int i = 0; i < cellCount; i++)
            {
                int index = run.layFromFarEnd ? cellCount - 1 - i : i;
                float along = first + index * cell;
                into.Add(new RailPlacement
                {
                    Piece = run.piece,
                    Position = horizontal
                        ? new Vector2(along, run.offset)
                        : new Vector2(run.offset, along)
                });
            }
        }
    }

    // Expands the runs into travel-ordered lines — one per run, in the same
    // order as `Runs`, so a wave can name the track a cart should ride by
    // index. Runs that resolve to no cells are still emitted, as zero-length
    // lines, to keep that indexing stable.
    public void BuildLines(List<RailLine> into, Vector2 viewCenter, Vector2 viewHalfExtents)
    {
        if (into == null) return;
        into.Clear();

        float cell = CellSize;

        foreach (var run in runs)
        {
            float first;
            int cellCount;
            ResolveRun(run, viewCenter, viewHalfExtents, out first, out cellCount);

            float span = Mathf.Max(0, cellCount - 1) * cell;
            bool forward = run.flow == RailFlow.Forward;

            into.Add(new RailLine
            {
                Label = run.label,
                Axis = run.axis,
                Offset = run.offset,
                Entry = forward ? first : first + span,
                Length = span,
                Sign = forward ? 1f : -1f
            });
        }
    }

    // Where a run starts along its axis and how many cells it covers. Shared by
    // BuildPlacements and BuildLines so the sprites and the travel path can
    // never drift apart.
    private void ResolveRun(RailRun run, Vector2 viewCenter, Vector2 viewHalfExtents,
                            out float first, out int cellCount)
    {
        bool horizontal = run.axis == RailAxis.Horizontal;

        if (run.spanViewport)
        {
            // Centre the run on the camera and push both ends past the edge
            float axisCenter = horizontal ? viewCenter.x : viewCenter.y;
            float halfExtent = horizontal ? viewHalfExtents.x : viewHalfExtents.y;
            int perSide = Mathf.CeilToInt(halfExtent / CellSize) + Mathf.Max(0, run.overhangCells);
            cellCount = perSide * 2 + 1;
            first = axisCenter - perSide * CellSize;
            return;
        }

        cellCount = Mathf.Max(0, run.count);
        first = run.start;
    }
}
