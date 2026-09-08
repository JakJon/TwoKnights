using System.Collections.Generic;
using UnityEngine;

// Owns the castle's mirrors: builds a MirrorLayout into live panes and takes the
// old set down when the next wave starts. The direct sibling of RailNetwork, and
// deliberately the smaller of the two — a mirror does not fall into place, does
// not carry traffic and does not need to be measured round, so there is no
// cascade, no speed scale and no loop arithmetic here.
//
// What it does own that a pane cannot is the PAIRING. A pane knows its colour;
// only something looking at the whole board can say which other pane that colour
// belongs to, and only something looking at the whole board can notice that a
// colour was authored once, or three times, and say so.
public class MirrorNetwork : MonoBehaviour
{
    [Tooltip("Empty the panes are parented under. Created at runtime when unset.")]
    [SerializeField] private Transform paneParent;

    private readonly List<MirrorPane> _live = new List<MirrorPane>();
    private MirrorLayout _layout;

    public static MirrorNetwork Instance { get; private set; }

    /// <summary>Panes currently on the field.</summary>
    public IReadOnlyList<MirrorPane> Live => _live;

    /// <summary>The layout currently laid, or null.</summary>
    public MirrorLayout Layout => _layout;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// The network for this scene, building one if the scene has none.
    ///
    /// Self-provisioning on purpose, following ArenaLighting.Ensure(). The castle
    /// shares the arena scene with the forest and the mine, and a MirrorNetwork
    /// sitting in it permanently would be a component every other map carries
    /// around for nothing. A map that never asks for mirrors never gets one made.
    /// </summary>
    public static MirrorNetwork Ensure()
    {
        if (Instance != null) return Instance;

        MirrorNetwork found = FindFirstObjectByType<MirrorNetwork>(FindObjectsInactive.Include);
        if (found != null)
        {
            Instance = found;
            return found;
        }

        var host = new GameObject("MirrorNetwork");
        return host.AddComponent<MirrorNetwork>();
    }

    /// <summary>
    /// Replace whatever is standing with <paramref name="layout"/>. Panes are all
    /// up in the same frame — unlike the mine's track there is nothing to wait
    /// out, so a wave can shoot through them the moment it has laid them.
    /// </summary>
    public void Lay(MirrorLayout layout)
    {
        Clear();
        _layout = layout;
        if (layout == null) return;

        MirrorPaneSet set = layout.PaneSet;
        if (set == null || set.PanePrefab == null)
        {
            Debug.LogWarning($"[MirrorNetwork] {layout.name} has no pane set (or no prefab in it), " +
                             "so there is nothing to build. Wire its Pane Set field.");
            return;
        }

        Transform parent = ResolveParent();
        var entries = layout.Panes;

        for (int i = 0; i < entries.Count; i++)
        {
            MirrorPaneEntry entry = entries[i];

            Sprite[] frames = set.FramesFor(entry.color);
            if (frames == null)
            {
                Debug.LogWarning($"[MirrorNetwork] {set.name} has no art for {entry.color}, " +
                                 $"so pane '{entry.label}' is skipped — and its twin is now alone.");
                continue;
            }

            var instance = Instantiate(set.PanePrefab, entry.position, Quaternion.identity, parent);
            instance.name = $"Mirror_{entry.color}_{i}";

            var pane = instance.GetComponent<MirrorPane>();
            if (pane == null)
            {
                Debug.LogWarning($"[MirrorNetwork] {set.PanePrefab.name} has no MirrorPane component; " +
                                 "it will just stand there.");
                Destroy(instance);
                continue;
            }

            pane.Configure(entry.label, entry.color, entry.facing, layout.Redirect,
                           frames, set.ShimmerInterval);
            _live.Add(pane);
        }

        Pair();
    }

    public void Clear()
    {
        for (int i = 0; i < _live.Count; i++)
        {
            if (_live[i] != null) Destroy(_live[i].gameObject);
        }
        _live.Clear();
        _layout = null;
    }

    /// <summary>The panes of one colour currently standing.</summary>
    public void CollectByColor(MirrorColor color, List<MirrorPane> into)
    {
        if (into == null) return;
        into.Clear();
        for (int i = 0; i < _live.Count; i++)
        {
            if (_live[i] != null && _live[i].PaneColor == color) into.Add(_live[i]);
        }
    }

    // Colour is the pairing, so this is where a layout's one hard rule is
    // enforced: exactly two of each colour. Both failures are LOUD rather than
    // quietly tolerated, because both produce a board that looks finished and is
    // not — a lone pane swallows every shot that touches it and gives nothing
    // back, and a third pane is a twin that some shots reach and others do not,
    // which is unreadable in exactly the way this whole mechanic must never be.
    private void Pair()
    {
        var ofColor = new List<MirrorPane>();

        foreach (MirrorColor color in System.Enum.GetValues(typeof(MirrorColor)))
        {
            CollectByColor(color, ofColor);
            if (ofColor.Count == 0) continue;

            if (ofColor.Count == 1)
            {
                Debug.LogWarning($"[MirrorNetwork] {LayoutName()} stands one {color} pane. " +
                                 "A pane with no twin has nowhere to put a shot, so it does nothing at all. " +
                                 "Colours come in pairs.");
                continue;
            }

            ofColor[0].PairWith(ofColor[1]);
            ofColor[1].PairWith(ofColor[0]);

            if (ofColor.Count > 2)
            {
                Debug.LogWarning($"[MirrorNetwork] {LayoutName()} stands {ofColor.Count} {color} panes. " +
                                 "Colour IS the pairing, so a third has no meaning — the first two are twinned " +
                                 "and the rest are left dead. Use another colour.");
            }
        }
    }

    private string LayoutName()
    {
        return _layout != null ? _layout.name : "The layout";
    }

    private Transform ResolveParent()
    {
        if (paneParent == null)
        {
            var holder = new GameObject("Mirrors");
            holder.transform.SetParent(transform, false);
            paneParent = holder.transform;
        }
        return paneParent;
    }
}
