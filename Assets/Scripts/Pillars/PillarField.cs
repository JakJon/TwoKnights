using System.Collections.Generic;
using UnityEngine;

// One straight row of pillars, authored on the wave that stands it up.
[System.Serializable]
public struct PillarRow
{
    [Tooltip("Editor-only note, e.g. \"above the knights\"")]
    public string label;

    [Tooltip("World y of the row's centre line. The knights' shields turn round y = 0, so +-2 leaves a one-unit gap over their heads and under their feet.")]
    public float y;

    [Tooltip("Centre of the first pillar")]
    public float fromX;

    [Tooltip("Centre of the last pillar (inclusive)")]
    public float toX;

    [Tooltip("Centre to centre. A pillar is one unit wide, so 2 leaves a one-unit gap between neighbours.")]
    public float step;

    [Tooltip("Extra height for single pillars, left to right (the first entry is the pillar at From X). Missing entries are 0. Negative moves a pillar down.")]
    public List<float> yNudges;

    [Tooltip("Degrees every pillar in the row is turned, anticlockwise. The torch is drawn on the pillar's bottom edge: 90 puts it on the right, -90 on the left. The pillar is square, so its footprint does not change.")]
    public float turn;
}

// The castle's pillars: stands rows of them up for a wave and takes them down
// when the next wave starts. A sibling of MirrorNetwork and built the same way —
// at runtime, on first use, so the forest and the mine never carry one.
//
// It owns one thing a single pillar cannot: the GAPS. A rat walking at a knight
// asks the field for a way through, and is handed the gap nearest its straight
// line, so it comes down between the pillars instead of through them.
public class PillarField : MonoBehaviour
{
    // A pillar is drawn on a 32x32 canvas at 32 pixels per unit: one unit square.
    public const float PillarSize = 1f;

    private struct Row
    {
        public float y;
        public List<float> xs; // sorted
        public float bottom;   // lowest pillar edge in the row
        public float top;      // highest pillar edge in the row
    }

    private readonly List<GameObject> _live = new List<GameObject>();
    private readonly List<Row> _rows = new List<Row>();
    private Transform _parent;

    public static PillarField Instance { get; private set; }

    public bool HasPillars => _rows.Count > 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static PillarField Ensure()
    {
        if (Instance != null) return Instance;

        PillarField found = FindFirstObjectByType<PillarField>(FindObjectsInactive.Include);
        if (found != null)
        {
            Instance = found;
            return found;
        }

        var host = new GameObject("PillarField");
        return host.AddComponent<PillarField>();
    }

    /// <summary>Replace whatever is standing with <paramref name="rows"/>.</summary>
    public void Lay(GameObject pillarPrefab, IReadOnlyList<PillarRow> rows)
    {
        Clear();
        if (rows == null || rows.Count == 0) return;

        if (pillarPrefab == null)
        {
            Debug.LogWarning("[PillarField] No pillar prefab wired, so no pillars stand. Wire the wave's Pillar Prefab.");
            return;
        }

        if (_parent == null)
        {
            var holder = new GameObject("Pillars");
            holder.transform.SetParent(transform, false);
            _parent = holder.transform;
        }

        for (int r = 0; r < rows.Count; r++)
        {
            PillarRow row = rows[r];
            float step = Mathf.Max(PillarSize, Mathf.Abs(row.step));
            float from = Mathf.Min(row.fromX, row.toX);
            float to = Mathf.Max(row.fromX, row.toX);
            float half = PillarSize * 0.5f;

            var xs = new List<float>();
            float bottom = row.y - half;
            float top = row.y + half;

            // Counted rather than stepped so a zero or tiny step cannot run away
            int count = Mathf.FloorToInt((to - from) / step + 0.001f) + 1;
            for (int i = 0; i < count; i++)
            {
                float x = from + i * step;
                float nudge = row.yNudges != null && i < row.yNudges.Count ? row.yNudges[i] : 0f;
                float y = row.y + nudge;

                var pillar = Instantiate(pillarPrefab, new Vector3(x, y, 0f), Quaternion.Euler(0f, 0f, row.turn), _parent);
                pillar.name = $"Pillar_{r}_{i}";
                _live.Add(pillar);
                xs.Add(x);
                bottom = Mathf.Min(bottom, y - half);
                top = Mathf.Max(top, y + half);
            }

            _rows.Add(new Row { y = row.y, xs = xs, bottom = bottom, top = top });
        }
    }

    public void Clear()
    {
        for (int i = 0; i < _live.Count; i++)
        {
            if (_live[i] != null) Destroy(_live[i]);
        }
        _live.Clear();
        _rows.Clear();
    }

    /// <summary>
    /// Waypoints that take a walker from <paramref name="from"/> to
    /// <paramref name="to"/> through the gaps rather than the pillars. Adds
    /// nothing when the straight line crosses no row.
    ///
    /// <paramref name="bodyLow"/> and <paramref name="bodyHigh"/> are how far the
    /// walker's body reaches below and above its own position, so it is put down
    /// fully clear of the row on each side, whatever its pivot.
    /// </summary>
    public void Route(Vector2 from, Vector2 to, List<Vector2> into, float bodyLow, float bodyHigh)
    {
        if (into == null || _rows.Count == 0) return;

        const float Margin = 0.05f;

        // Rows in the order the walker meets them
        var crossed = new List<Row>();
        for (int i = 0; i < _rows.Count; i++)
        {
            float y = _rows[i].y;
            if ((from.y > y && to.y < y) || (from.y < y && to.y > y)) crossed.Add(_rows[i]);
        }
        crossed.Sort((a, b) => Mathf.Abs(a.y - from.y).CompareTo(Mathf.Abs(b.y - from.y)));

        Vector2 at = from;
        for (int i = 0; i < crossed.Count; i++)
        {
            Row row = crossed[i];
            float t = Mathf.InverseLerp(at.y, to.y, row.y);
            float crossX = Mathf.Lerp(at.x, to.x, t);
            float gap = NearestGap(row, crossX);

            // Clear of the row on the near side, then clear of it on the far side.
            // The row's band covers every pillar in it, nudged ones included.
            float above = row.top - bodyLow + Margin;
            float below = row.bottom - bodyHigh - Margin;
            bool goingDown = at.y > row.y;

            Vector2 enter = new Vector2(gap, goingDown ? above : below);
            Vector2 leave = new Vector2(gap, goingDown ? below : above);
            into.Add(enter);
            into.Add(leave);
            at = leave;
        }
    }

    // The middle of the gap nearest x: between two neighbours, or just past
    // either end of the row.
    private static float NearestGap(Row row, float x)
    {
        List<float> xs = row.xs;
        if (xs == null || xs.Count == 0) return x;

        float clear = PillarSize * 0.5f + 0.6f;
        float best = xs[0] - clear;
        float bestDistance = Mathf.Abs(best - x);

        for (int i = 0; i < xs.Count; i++)
        {
            float candidate = i < xs.Count - 1 ? (xs[i] + xs[i + 1]) * 0.5f : xs[i] + clear;
            float distance = Mathf.Abs(candidate - x);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }
        return best;
    }
}
