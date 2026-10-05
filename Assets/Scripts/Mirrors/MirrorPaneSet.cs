using System.Collections.Generic;
using UnityEngine;

// The castle's mirror tileset: one pane prefab and the art for each colour.
//
// A sibling of RailPieceSet, and the same reasoning about where it lives — it is
// an ASSET, so a layout can be wired without opening a scene. It differs from the
// rail set in holding SPRITES rather than a prefab per variant, because that is
// what actually differs between panes. A corner rail and a straight rail are
// different objects; a green pane and a cyan pane are one object painted twice,
// and giving each its own prefab would mean four prefabs to keep in step every
// time the pane grows a component.
[CreateAssetMenu(fileName = "MirrorPaneSet", menuName = "Maps/Mirror Pane Set")]
public class MirrorPaneSet : ScriptableObject
{
    [System.Serializable]
    public struct ColorEntry
    {
        public MirrorColor color;

        [Tooltip("Frames in order. The mirror art is currently ONE frame, so panes are still — add a second and they shimmer between them at Shimmer Interval, no code change.")]
        public Sprite[] frames;
    }

    [Tooltip("Instantiated for every pane in a layout. Needs a MirrorPane and a trigger BoxCollider2D.")]
    [SerializeField] private GameObject panePrefab;

    [SerializeField] private List<ColorEntry> colors = new List<ColorEntry>();

    [Tooltip("Seconds a shimmer frame is held. Shared by every pane so a pair stays in step.")]
    [SerializeField] private float shimmerInterval = 0.18f;

    [Tooltip("Seconds the glass sits still on its first frame between one glint and the next. Shared by every pane, for the same reason the interval is.")]
    [SerializeField] private float shimmerRest = 4.5f;

    public GameObject PanePrefab => panePrefab;
    public float ShimmerInterval => Mathf.Max(0.02f, shimmerInterval);
    public float ShimmerRest => Mathf.Max(0f, shimmerRest);

    /// <summary>The art for one colour, or null if the set has none for it.</summary>
    public Sprite[] FramesFor(MirrorColor color)
    {
        for (int i = 0; i < colors.Count; i++)
        {
            if (colors[i].color == color && colors[i].frames != null && colors[i].frames.Length > 0)
            {
                return colors[i].frames;
            }
        }
        return null;
    }
}
