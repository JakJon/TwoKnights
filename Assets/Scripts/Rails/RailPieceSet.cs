using System.Collections.Generic;
using UnityEngine;

// The mine's tileset: which prefab draws each direction of track.
//
// This is an ASSET rather than a list on the RailNetwork component, and that is
// the whole point of it. The scene-side table can only be filled with a scene
// open and not in play mode, and when it isn't filled nothing looks broken —
// RailNetwork falls back to the horizontal tile, so the track still lays, just
// entirely out of straights. A shaft made of horizontal tiles reads as slightly
// odd art, not as a missing reference, which is exactly the kind of failure that
// survives several playtests.
//
// A layout points at one of these, so the tileset travels with the track shapes
// that need it and can be wired without touching a scene at all.
[CreateAssetMenu(fileName = "RailPieceSet", menuName = "Maps/Rail Piece Set")]
public class RailPieceSet : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public RailPieceKind kind;
        public GameObject prefab;
    }

    [Tooltip("One prefab per direction. Kinds left out fall through to the RailNetwork's own table, then to its fallback piece.")]
    [SerializeField] private List<Entry> pieces = new List<Entry>();

    [Tooltip("Planted over every drop point a layout declares. Track furniture rather than a direction, but it lives here for the same reason the rest does — so it can be wired without opening a scene.")]
    [SerializeField] private GameObject dropFlagPrefab;

    [Header("Switch track (cart sorting)")]
    [Tooltip("The pieces in red, for the corner of a fork that the red lever turns. Only the corners are needed. See RailSwitchColor.")]
    [SerializeField] private List<Entry> redPieces = new List<Entry>();

    [Tooltip("The pieces in blue, for the corner of a fork that the blue lever turns.")]
    [SerializeField] private List<Entry> bluePieces = new List<Entry>();

    [Tooltip("The flag for a drop point marked Red")]
    [SerializeField] private GameObject redFlagPrefab;

    [Tooltip("The flag for a drop point marked Blue")]
    [SerializeField] private GameObject blueFlagPrefab;

    [Tooltip("The flag for a drop point marked Green")]
    [SerializeField] private GameObject greenFlagPrefab;

    [Tooltip("The flag for a drop point marked Yellow")]
    [SerializeField] private GameObject yellowFlagPrefab;

    public GameObject DropFlagPrefab => dropFlagPrefab;

    /// <summary>The prefab for <paramref name="kind"/>, or null if unmapped.</summary>
    public GameObject For(RailPieceKind kind)
    {
        return Find(pieces, kind);
    }

    /// <summary>
    /// The prefab for <paramref name="kind"/> in a switch colour. A colour with no
    /// piece of its own gets the ordinary one, so the track still lays — it just
    /// cannot be told from the rest of the rail.
    /// </summary>
    public GameObject For(RailPieceKind kind, RailSwitchColor color)
    {
        GameObject colored = color == RailSwitchColor.Red ? Find(redPieces, kind)
                           : color == RailSwitchColor.Blue ? Find(bluePieces, kind)
                           : null;
        return colored != null ? colored : Find(pieces, kind);
    }

    /// <summary>The flag for a drop point of this colour; the ordinary flag for None or an unwired colour.</summary>
    public GameObject DropFlagFor(CartColor color)
    {
        GameObject colored = color == CartColor.Red ? redFlagPrefab
                           : color == CartColor.Blue ? blueFlagPrefab
                           : color == CartColor.Green ? greenFlagPrefab
                           : color == CartColor.Yellow ? yellowFlagPrefab
                           : null;
        return colored != null ? colored : dropFlagPrefab;
    }

    private static GameObject Find(List<Entry> table, RailPieceKind kind)
    {
        for (int i = 0; i < table.Count; i++)
        {
            if (table[i].kind == kind && table[i].prefab != null) return table[i].prefab;
        }
        return null;
    }
}
