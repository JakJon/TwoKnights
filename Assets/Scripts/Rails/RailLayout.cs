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

    [Tooltip("Lay this run from its far end back, reversing the fall cascade")]
    public bool layFromFarEnd;
}

// Where a single piece ends up, in the order it should fall
public struct RailPlacement
{
    public RailPieceKind Piece;
    public Vector2 Position;
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
            float axisCenter = horizontal ? viewCenter.x : viewCenter.y;
            float halfExtent = horizontal ? viewHalfExtents.x : viewHalfExtents.y;

            float first;
            int cellCount;

            if (run.spanViewport)
            {
                // Centre the run on the camera and push both ends past the edge
                int perSide = Mathf.CeilToInt(halfExtent / cell) + Mathf.Max(0, run.overhangCells);
                cellCount = perSide * 2 + 1;
                first = axisCenter - perSide * cell;
            }
            else
            {
                cellCount = Mathf.Max(0, run.count);
                first = run.start;
            }

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
}
