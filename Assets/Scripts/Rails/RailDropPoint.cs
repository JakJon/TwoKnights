using UnityEngine;

// A marked unloading spot on the track, with a flag planted over it.
//
// Unlike a teleporter — which is invisible by contract, because a cart popping
// from one edge to the other must never be seen — this one exists to BE seen.
// The delivery route only works as a race if the player can read the whole thing
// at a glance: that cart, going that way, is going to open there. Take the flag
// away and a delivery cart is just an obstacle that eventually turns into a rat
// somewhere you weren't looking.
public class RailDropPoint : MonoBehaviour
{
    /// <summary>Which of the network's lines this sits on.</summary>
    public int LineIndex { get; private set; }

    /// <summary>Distance from that line's mouth, matching MineCart.Along.</summary>
    public float DistanceAlong { get; private set; }

    public string Label { get; private set; }

    public void Configure(string label, int lineIndex, float distanceAlong, Vector2 position)
    {
        Label = label;
        LineIndex = lineIndex;
        DistanceAlong = distanceAlong;
        transform.position = new Vector3(position.x, position.y, 0f);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.94f, 0.57f, 0.25f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up);
    }
#endif
}
