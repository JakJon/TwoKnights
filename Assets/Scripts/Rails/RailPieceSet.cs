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

    public GameObject DropFlagPrefab => dropFlagPrefab;

    /// <summary>The prefab for <paramref name="kind"/>, or null if unmapped.</summary>
    public GameObject For(RailPieceKind kind)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].kind == kind && pieces[i].prefab != null) return pieces[i].prefab;
        }
        return null;
    }
}
