using System.Collections.Generic;
using UnityEngine;

// One pane in a layout: where it stands, what colour it is, and which way its
// front looks. The facing also decides whether it is the tall pane or the wide
// one — see MirrorFacing.
[System.Serializable]
public struct MirrorPaneEntry
{
    [Tooltip("Editor-only note, e.g. \"loop, right end\"")]
    public string label;

    public MirrorColor color;

    [Tooltip("Which way the front looks. Left/Right stand the pane up (1x2); Up/Down lay it flat (2x1).")]
    public MirrorFacing facing;

    [Tooltip("World position of the pane's CENTRE — not its feet. See MirrorPane for why.")]
    public Vector2 position;
}

// A named arrangement of mirrors a wave can ask for, the way a mine wave asks for
// a RailLayout. Waves reference the asset rather than hardcoding coordinates, so
// the castle's furniture can be retuned without a recompile.
//
// The redirect rule lives here rather than on the pane because it is a property
// of the ARRANGEMENT: the same eight panes read as a completely different board
// under the two rules, and a layout with some panes on one rule and some on the
// other would be unreadable by design.
[CreateAssetMenu(fileName = "MirrorLayout", menuName = "Maps/Mirror Layout")]
public class MirrorLayout : ScriptableObject
{
    [Tooltip("Which prefab draws a pane and what each colour looks like. Lives on the layout so it can be wired without opening a scene — see MirrorPaneSet.")]
    [SerializeField] private MirrorPaneSet paneSet;

    [Tooltip("How a shot's heading survives the trip. See MirrorRedirect — this changes what a given arrangement DOES, so it is authored, not assumed.")]
    [SerializeField] private MirrorRedirect redirect = MirrorRedirect.MirrorAboutFacing;

    [Tooltip("Every pane on the board. A colour must appear exactly twice — MirrorNetwork says so loudly if it does not.")]
    [SerializeField] private List<MirrorPaneEntry> panes = new List<MirrorPaneEntry>();

    public MirrorPaneSet PaneSet => paneSet;
    public MirrorRedirect Redirect => redirect;
    public IReadOnlyList<MirrorPaneEntry> Panes => panes;
}
