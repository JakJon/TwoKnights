using System.Collections.Generic;
using UnityEngine;

// Owns the mine's track: builds a RailLayout into live rail pieces and tears
// the old track down when the next layout (or the next wave) arrives.
//
// Pieces are all instantiated in the same frame — the track exists as one
// object from the moment it is asked for — and the cascade comes from each
// piece being handed a start delay, not from spacing out the spawns. That way
// a wave never has to wait on the network to finish laying before it can
// reason about the track.
public class RailNetwork : MonoBehaviour
{
    [System.Serializable]
    public struct PieceEntry
    {
        public RailPieceKind kind;
        public GameObject prefab;
    }

    [Tooltip("Prefab per piece kind. Kinds without art fall back to the horizontal piece.")]
    [SerializeField] private List<PieceEntry> pieces = new List<PieceEntry>();

    [Tooltip("Used for any kind missing from the table above")]
    [SerializeField] private GameObject fallbackPiecePrefab;

    [Tooltip("Empty the pieces are parented under. Created at runtime when unset.")]
    [SerializeField] private Transform trackParent;

    [SerializeField] private Camera viewCamera;

    private readonly List<RailPlacement> _placements = new List<RailPlacement>();
    private readonly List<RailSegment> _live = new List<RailSegment>();
    private readonly List<RailLine> _lines = new List<RailLine>();
    private readonly List<MineCart> _carts = new List<MineCart>();
    private bool _warnedMissingPiece;

    public static RailNetwork Instance { get; private set; }

    /// <summary>Pieces currently on the field.</summary>
    public IReadOnlyList<RailSegment> Live => _live;

    /// <summary>
    /// The current track as travel-ordered lines, one per run of the laid
    /// layout and in the same order. This is what riders follow.
    /// </summary>
    public IReadOnlyList<RailLine> Lines => _lines;

    public int LineCount => _lines.Count;

    /// <summary>Seconds until the last piece of the most recent Lay has landed.</summary>
    public float LayDuration { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (viewCamera == null) viewCamera = Camera.main;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Replace the current track with <paramref name="layout"/>. Returns the
    /// seconds the full cascade takes, so a wave can pace itself against it.
    /// </summary>
    public float Lay(RailLayout layout)
    {
        ClearAll();
        LayDuration = 0f;
        if (layout == null) return 0f;

        Vector2 center, halfExtents;
        GetViewBounds(out center, out halfExtents);
        layout.BuildPlacements(_placements, center, halfExtents);
        // Resolved up front, before a single piece has landed, so a wave can
        // release carts against the finished shape of the track immediately
        layout.BuildLines(_lines, center, halfExtents);

        Transform parent = ResolveParent();
        float stagger = layout.FallStagger;

        for (int i = 0; i < _placements.Count; i++)
        {
            var placement = _placements[i];
            GameObject prefab = PrefabFor(placement.Piece);
            if (prefab == null) continue;

            var instance = Instantiate(prefab, placement.Position, Quaternion.identity, parent);
            var segment = instance.GetComponent<RailSegment>();
            if (segment == null)
            {
                Debug.LogWarning($"[RailNetwork] {prefab.name} has no RailSegment; it will just sit there.");
                continue;
            }

            float delay = i * stagger;
            segment.Drop(delay);
            LayDuration = Mathf.Max(LayDuration, delay + segment.FallDuration);
            _live.Add(segment);
        }

        return LayDuration;
    }

    /// <summary>Build the layout already settled — no fall, no dust.</summary>
    public void LaySettled(RailLayout layout)
    {
        Lay(layout);
        for (int i = 0; i < _live.Count; i++)
        {
            if (_live[i] != null) _live[i].SnapSettled();
        }
        LayDuration = 0f;
    }

    /// <summary>Look up one of the current track's lines by index.</summary>
    public bool TryGetLine(int index, out RailLine line)
    {
        if (index >= 0 && index < _lines.Count)
        {
            line = _lines[index];
            return true;
        }
        line = default(RailLine);
        return false;
    }

    /// <summary>
    /// Put a cart on line <paramref name="lineIndex"/> and start it rolling the
    /// way that line faces. <paramref name="speed"/> of 0 or less leaves the
    /// prefab's own speed alone. The cart is parented to the track, so it comes
    /// down with the rails when the next wave clears them. Null if there is no
    /// such line or the prefab is not a cart.
    /// </summary>
    public MineCart SpawnCart(GameObject cartPrefab, int lineIndex, float speed = 0f)
    {
        RailLine line;
        if (cartPrefab == null || !TryGetLine(lineIndex, out line)) return null;

        var instance = Instantiate(cartPrefab, line.EntryPoint, Quaternion.identity, ResolveParent());
        var cart = instance.GetComponent<MineCart>();
        if (cart == null)
        {
            Debug.LogWarning($"[RailNetwork] {cartPrefab.name} has no MineCart component; it cannot ride the track.");
            Destroy(instance);
            return null;
        }

        if (speed > 0f) cart.Ride(line, speed);
        else cart.Ride(line);

        _carts.Add(cart);
        return cart;
    }

    public void ClearAll()
    {
        for (int i = 0; i < _carts.Count; i++)
        {
            if (_carts[i] != null) Destroy(_carts[i].gameObject);
        }
        _carts.Clear();

        for (int i = 0; i < _live.Count; i++)
        {
            if (_live[i] != null) Destroy(_live[i].gameObject);
        }
        _live.Clear();
        _lines.Clear();
        LayDuration = 0f;
    }

    private Transform ResolveParent()
    {
        if (trackParent == null)
        {
            var holder = new GameObject("Rails");
            holder.transform.SetParent(transform, false);
            trackParent = holder.transform;
        }
        return trackParent;
    }

    private GameObject PrefabFor(RailPieceKind kind)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].kind == kind && pieces[i].prefab != null) return pieces[i].prefab;
        }

        if (!_warnedMissingPiece && kind != RailPieceKind.Horizontal)
        {
            _warnedMissingPiece = true;
            Debug.LogWarning($"[RailNetwork] No prefab for rail piece '{kind}' — using the fallback piece. " +
                             "Add it to the piece table once the sprite exists.");
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].kind == RailPieceKind.Horizontal && pieces[i].prefab != null) return pieces[i].prefab;
        }
        return fallbackPiecePrefab;
    }

    // Visible world rect of the camera. Falls back to the playfield bounds the
    // waves are written against if there is no orthographic camera to ask.
    private void GetViewBounds(out Vector2 center, out Vector2 halfExtents)
    {
        var cam = viewCamera != null ? viewCamera : Camera.main;
        if (cam != null && cam.orthographic)
        {
            center = cam.transform.position;
            halfExtents = new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize);
            return;
        }

        center = Vector2.zero;
        halfExtents = new Vector2(10f, 5.625f);
    }
}
