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

    // One "every Nth of these turns up nastier" rule. The mine has three of them —
    // poison bombs, the twin-throwing gnome, the golden cage — and they are all
    // the same idea, so they are all the same table rather than three counters
    // hidden in three different wave scripts.
    //
    // It lives HERE, on the one funnel every cart in every rail wave goes through
    // (SpawnCart), because the rule is about the mine rather than about any one
    // wave. A wave author writes "bomb gnome" and gets whatever the mine is
    // serving at this point in the run, without having to remember that the mine
    // serves anything else.
    [System.Serializable]
    public struct CartVariant
    {
        [Tooltip("Editor-only note, e.g. \"poison bomb gnome\"")]
        public string label;

        [Tooltip("What the waves actually author")]
        public GameObject standard;

        [Tooltip("What replaces it when the count comes round")]
        public GameObject variant;

        [Tooltip("2 = every other one, 3 = every third. Reset at the top of every wave so a wave's content never depends on the run that led to it.")]
        public int everyNth;

        [Tooltip("Wave number this starts applying from. 11 = 'after wave ten'. 0 or 1 = from the start of the run.")]
        public int fromWaveNumber;

        [Tooltip("Rules sharing a group name share ONE count. That is how 'every third monster cage' stays every third when the cages come in two flavours: a rat crate and a bat crate are one thing to the player, and each keeps its own authored cargo because each has its own golden version. Blank = this rule counts alone.")]
        public string countGroup;
    }

    [Tooltip("Prefab per piece kind. Kinds without art fall back to the horizontal piece.")]
    [SerializeField] private List<PieceEntry> pieces = new List<PieceEntry>();

    [Tooltip("Harder rolling stock the mine starts mixing in later in a run. See CartVariant — the counts reset every wave.")]
    [SerializeField] private List<CartVariant> cartVariants = new List<CartVariant>();

    [Tooltip("Used for any kind missing from the table above")]
    [SerializeField] private GameObject fallbackPiecePrefab;

    [Tooltip("Empty the pieces are parented under. Created at runtime when unset.")]
    [SerializeField] private Transform trackParent;

    [SerializeField] private Camera viewCamera;

    [Tooltip("How far clear of the rail a drop flag is planted, across the track's axis")]
    [SerializeField] private float flagOffset = 0.9f;

    private readonly List<RailPlacement> _placements = new List<RailPlacement>();
    private readonly List<RailSegment> _live = new List<RailSegment>();
    private readonly List<RailLine> _lines = new List<RailLine>();
    private readonly List<MineCart> _carts = new List<MineCart>();
    private readonly List<RailTeleporter> _pads = new List<RailTeleporter>();
    private readonly List<RailDropPoint> _drops = new List<RailDropPoint>();
    private readonly Dictionary<string, int> _variantCounts = new Dictionary<string, int>();
    private RailLayout _layout; // the one currently laid; owns the tileset
    private bool _warnedMissingPiece;
    private bool _warnedMissingFlag;

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

    /// <summary>
    /// How much faster than authored the whole track is running. 1 is the mine's
    /// ordinary pace, and every layout starts there.
    ///
    /// It is a property of the TRACK rather than of any cart, and it has to be:
    /// carts on a packed ring are welded into one train by the spacing sweep, so
    /// speeding up a subset of them does not make those carts faster — it makes
    /// them shoulder the iron in front of them, and what the player sees is a
    /// ring grinding against itself. One scale for everything riding is the only
    /// version of "the wheel turns faster" that is actually a wheel turning
    /// faster.
    /// </summary>
    public float SpeedScale { get; private set; } = 1f;

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
        _layout = layout;

        // Every rail wave lays its track as its first act, so this is where a wave
        // begins as far as the mine is concerned. The variant counters restart
        // here and nowhere else: carried across waves they would make the third
        // Choo Choo of a run deal a different shift than the first one did, and
        // wave content has to be reproducible on its own (no-randomness pillar).
        ResetVariantCounts();

        if (layout == null) return 0f;

        Vector2 center, halfExtents;
        GetViewBounds(out center, out halfExtents);
        layout.BuildPlacements(_placements, center, halfExtents);
        // Resolved up front, before a single piece has landed, so a wave can
        // release carts against the finished shape of the track immediately
        layout.BuildLines(_lines, center, halfExtents);
        BuildPads(layout);
        BuildDrops(layout);

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

    /// <summary>
    /// The pad guarding the far end of <paramref name="lineIndex"/>, if the
    /// layout wired one. Null means carts on that line simply roll off and
    /// despawn — the behaviour of every layout with no teleport table.
    /// </summary>
    public RailTeleporter ExitPadFor(int lineIndex)
    {
        for (int i = 0; i < _pads.Count; i++)
        {
            RailTeleporter pad = _pads[i];
            if (pad != null && pad.IsExit && pad.LineIndex == lineIndex) return pad;
        }
        return null;
    }

    /// <summary>Every marked unloading spot on the current track.</summary>
    public IReadOnlyList<RailDropPoint> Drops => _drops;

    /// <summary>
    /// Distance a cart covers going once around, starting from
    /// <paramref name="startLine"/>, or false if the track does not come back
    /// on itself. Lets a wave fill a ring exactly rather than being told how
    /// many carts to use — a count authored by hand stops matching the moment
    /// the layout is retuned, and the symptom is a seam in the train.
    /// </summary>
    public bool TryMeasureLoop(int startLine, out float length)
    {
        length = 0f;

        var visited = new HashSet<int>();
        int line = startLine;

        while (visited.Add(line))
        {
            RailTeleporter pad = ExitPadFor(line);
            if (pad == null || pad.Destination == null) return false; // open route

            // Distance covered on this line is from wherever a cart entered it to
            // wherever it leaves; summed round the cycle the entry terms cancel.
            length += pad.DistanceAlong - pad.Destination.DistanceAlong;
            line = pad.Destination.LineIndex;

            if (line == startLine) return length > 0f;
        }

        return false; // walked into a knot that never returns to the start
    }

    /// <summary>
    /// Wind the whole track up (or back down) to <paramref name="scale"/> times
    /// the speed its carts were released at. Applied as a change of gear rather
    /// than as an absolute speed, so a wave that released some carts faster than
    /// others keeps that difference.
    ///
    /// Carts released AFTER this are put on the track already up to speed — see
    /// SpawnCart — so a ring that is still filling when the gear changes does not
    /// end up with a slow tail.
    /// </summary>
    public void SetSpeedScale(float scale)
    {
        scale = Mathf.Max(0.05f, scale);
        if (Mathf.Approximately(scale, SpeedScale)) return;

        float gear = scale / SpeedScale;
        SpeedScale = scale;

        for (int i = 0; i < _carts.Count; i++)
        {
            if (_carts[i] != null) _carts[i].Speed *= gear;
        }
    }

    /// <summary>
    /// Take ownership of a cart that was not spawned through SpawnCart — the
    /// wreck a gnome cart leaves behind. Without this the replacement outlives
    /// the track it is riding.
    /// </summary>
    public void Adopt(MineCart cart)
    {
        if (cart == null || _carts.Contains(cart)) return;
        _carts.Add(cart);
        cart.transform.SetParent(ResolveParent(), true);
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

        cartPrefab = Substitute(cartPrefab);

        var instance = Instantiate(cartPrefab, line.EntryPoint, Quaternion.identity, ResolveParent());
        var cart = instance.GetComponent<MineCart>();
        if (cart == null)
        {
            Debug.LogWarning($"[RailNetwork] {cartPrefab.name} has no MineCart component; it cannot ride the track.");
            Destroy(instance);
            return null;
        }

        // Whatever gear the track is in, a cart joining it now joins at that gear
        if (speed > 0f) cart.Ride(line, speed * SpeedScale);
        else cart.Ride(line, cart.Speed * SpeedScale);

        _carts.Add(cart);
        return cart;
    }

    /// <summary>
    /// What actually rolls out when a wave asks for <paramref name="cartPrefab"/>.
    /// Usually the prefab itself; on the count, the nastier version of it.
    ///
    /// Counting, not rolling — the mine deals the same shift every time this wave
    /// comes up at this point in a run (no-randomness pillar). The count advances
    /// only once the wave gate is open, so "every other one after wave ten" means
    /// the second qualifying cart rather than the second cart overall.
    /// </summary>
    private GameObject Substitute(GameObject cartPrefab)
    {
        if (cartVariants.Count == 0) return cartPrefab;

        int wave = WaveManager.ActiveInstance != null
            ? WaveManager.ActiveInstance.CurrentWaveNumber
            : 1;

        for (int i = 0; i < cartVariants.Count; i++)
        {
            CartVariant rule = cartVariants[i];
            if (rule.standard != cartPrefab || rule.variant == null) continue;
            if (wave < rule.fromWaveNumber) continue;

            string group = string.IsNullOrEmpty(rule.countGroup) ? "#" + i : rule.countGroup;
            int every = Mathf.Max(1, rule.everyNth);

            int seen;
            _variantCounts.TryGetValue(group, out seen);
            seen++;
            _variantCounts[group] = seen;

            if (seen % every != 0) continue;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[RailNetwork] Cart {seen} of '{group}' this wave — swapping " +
                      $"{rule.standard.name} for {rule.variant.name} (every {every}, from wave {rule.fromWaveNumber}).");
#endif
            return rule.variant;
        }

        return cartPrefab;
    }

    private void ResetVariantCounts()
    {
        _variantCounts.Clear();
    }

    public void ClearAll()
    {
        // Back into first gear with the track that was wound up. A wave inherits
        // the mine's ordinary pace and has to ask for anything else.
        SpeedScale = 1f;

        for (int i = 0; i < _carts.Count; i++)
        {
            if (_carts[i] != null) Destroy(_carts[i].gameObject);
        }
        _carts.Clear();

        for (int i = 0; i < _pads.Count; i++)
        {
            if (_pads[i] != null) Destroy(_pads[i].gameObject);
        }
        _pads.Clear();

        for (int i = 0; i < _drops.Count; i++)
        {
            if (_drops[i] != null) Destroy(_drops[i].gameObject);
        }
        _drops.Clear();

        for (int i = 0; i < _live.Count; i++)
        {
            if (_live[i] != null) Destroy(_live[i].gameObject);
        }
        _live.Clear();
        _lines.Clear();
        LayDuration = 0f;
    }

    // Two bare objects per authored link — an exit pad past the source run's far
    // end and an entry pad behind the destination run's mouth. They carry no art
    // and no collider; they exist so the hand-off has an authored place to happen
    // and so the wiring can be read off the Scene view (RailTeleporter gizmos).
    private void BuildPads(RailLayout layout)
    {
        var links = layout.Teleports;
        if (links == null || links.Count == 0) return;

        Transform parent = ResolveParent();

        for (int i = 0; i < links.Count; i++)
        {
            RailTeleport link = links[i];

            RailLine from, to;
            if (!TryGetLine(link.fromRun, out from) || !TryGetLine(link.toRun, out to))
            {
                Debug.LogWarning($"[RailNetwork] Teleport '{link.label}' points at run " +
                                 $"{link.fromRun}->{link.toRun}, but the layout only laid " +
                                 $"{_lines.Count} run(s). The link is skipped.");
                continue;
            }

            float exitAlong = from.Length + Mathf.Max(0f, link.exitOvershoot);
            float entryAlong = -Mathf.Max(0f, link.entryLead);

            RailTeleporter exit = NewPad($"RailExit_{i}", parent);
            RailTeleporter entry = NewPad($"RailEntry_{i}", parent);

            exit.Configure(link.label, from.Index, exitAlong, from.PointAt(exitAlong));
            entry.Configure(link.label, to.Index, entryAlong, to.PointAt(entryAlong));
            exit.LinkTo(entry);

            _pads.Add(exit);
            _pads.Add(entry);
        }
    }

    // One flagged marker per drop the layout declares. The flag is a child rather
    // than the marker itself so the marker can sit exactly ON the rail — which is
    // where the cart's distance check has to happen — while the pennant stands
    // clear above the track where it can actually be read.
    private void BuildDrops(RailLayout layout)
    {
        var drops = layout.Drops;
        if (drops == null || drops.Count == 0) return;

        Transform parent = ResolveParent();
        GameObject flagPrefab = layout.PieceSet != null ? layout.PieceSet.DropFlagPrefab : null;

        for (int i = 0; i < drops.Count; i++)
        {
            RailDrop drop = drops[i];

            RailLine line;
            if (!TryGetLine(drop.run, out line))
            {
                Debug.LogWarning($"[RailNetwork] Drop '{drop.label}' points at run {drop.run}, " +
                                 $"but the layout only laid {_lines.Count} run(s). It is skipped.");
                continue;
            }

            var host = new GameObject($"RailDrop_{i}");
            host.transform.SetParent(parent, false);
            var marker = host.AddComponent<RailDropPoint>();
            marker.Configure(drop.label, line.Index, drop.distanceAlong, line.PointAt(drop.distanceAlong));

            if (flagPrefab != null)
            {
                // Planted BESIDE the track, never on it. "One unit up" only reads
                // as beside for a horizontal run — on a vertical shaft it plants
                // the flag one cell further along the rail, in the path of the
                // carts. So the nudge runs across the line's axis, and outward,
                // away from the middle of the board where the fight is.
                float side = line.Offset >= 0f ? 1f : -1f;
                Vector3 nudge = line.Axis == RailAxis.Horizontal
                    ? new Vector3(0f, side * flagOffset, 0f)
                    : new Vector3(side * flagOffset, 0f, 0f);

                GameObject flag = Instantiate(flagPrefab, host.transform);
                flag.transform.localPosition = nudge;
            }
            else if (!_warnedMissingFlag)
            {
                _warnedMissingFlag = true;
                Debug.LogWarning("[RailNetwork] No drop flag prefab on the layout's RailPieceSet — " +
                                 "drop points will be invisible, which makes a delivery route unreadable.");
            }

            _drops.Add(marker);
        }
    }

    private RailTeleporter NewPad(string name, Transform parent)
    {
        var host = new GameObject(name);
        host.transform.SetParent(parent, false);
        return host.AddComponent<RailTeleporter>();
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

    // Layout's own tileset first, then this component's table, then the fallback.
    // The layout wins because it is an ASSET: it can be wired without a scene
    // open, which the table below cannot (see RailPieceSet for why that matters).
    // The table stays as a per-scene override.
    private GameObject PrefabFor(RailPieceKind kind)
    {
        if (_layout != null && _layout.PieceSet != null)
        {
            GameObject fromSet = _layout.PieceSet.For(kind);
            if (fromSet != null) return fromSet;
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].kind == kind && pieces[i].prefab != null) return pieces[i].prefab;
        }

        if (!_warnedMissingPiece && kind != RailPieceKind.Horizontal)
        {
            _warnedMissingPiece = true;
            Debug.LogWarning($"[RailNetwork] No prefab for rail piece '{kind}' — using the fallback piece. " +
                             $"Add it to {(_layout != null ? _layout.name : "the layout")}'s RailPieceSet.");
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
