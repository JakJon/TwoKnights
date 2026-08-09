using UnityEngine;

// An off-frame pad on the mine's track. A cart that reaches one is picked up and
// set down at its Destination, still rolling, still at the same speed — which is
// what turns a straight run into an endless loop without the player ever seeing
// a cart pop out of existence.
//
// Pads are created by RailNetwork from the layout's teleport table and always sit
// past the viewport edge, so they are invisible by construction. They carry no
// collider: a cart consults the pad on its line by distance travelled instead of
// waiting on a trigger, which keeps the hand-off exact at any frame rate and lets
// the overshoot carry across so speed never stutters at the seam.
public class RailTeleporter : MonoBehaviour
{
    /// <summary>Which of the network's lines this pad sits on.</summary>
    public int LineIndex { get; private set; }

    /// <summary>
    /// Signed distance from that line's mouth. Exit pads sit past the far end
    /// (greater than the line's length); entry pads sit behind the mouth, so
    /// this is negative for them.
    /// </summary>
    public float DistanceAlong { get; private set; }

    /// <summary>Where a cart that reaches this pad reappears. Null on entry pads.</summary>
    public RailTeleporter Destination { get; private set; }

    /// <summary>Editor-only note copied off the layout entry.</summary>
    public string Label { get; private set; }

    public bool IsExit => Destination != null;

    public void Configure(string label, int lineIndex, float distanceAlong, Vector2 position)
    {
        Label = label;
        LineIndex = lineIndex;
        DistanceAlong = distanceAlong;
        transform.position = new Vector3(position.x, position.y, 0f);
    }

    public void LinkTo(RailTeleporter destination)
    {
        Destination = destination;
    }

#if UNITY_EDITOR
    // The whole point of these is that they are never seen in play, so the only
    // way to check a layout's wiring is in the Scene view
    private void OnDrawGizmos()
    {
        Gizmos.color = IsExit ? new Color(1f, 0.55f, 0.15f, 0.9f) : new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);

        if (Destination != null)
        {
            Gizmos.DrawLine(transform.position, Destination.transform.position);
        }
    }
#endif
}
