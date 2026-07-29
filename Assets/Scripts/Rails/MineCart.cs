using UnityEngine;

// A cart riding the mine's track. It follows the RailLine it was put on and
// nothing else — which way that stretch of track faces is authored on the
// layout, so a cart never decides its own direction.
//
// It rides the line rather than the pieces, which is what lets a wave release
// a cart the instant the track is asked for, while the rails behind it are
// still cascading into place.
//
// Distance is measured from the lead-in point, a little behind the mouth of
// the line, so the cart rolls in from off-screen already at speed and rolls
// out the far side before it despawns.
[RequireComponent(typeof(SpriteRenderer))]
public class MineCart : MonoBehaviour
{
    [Header("Motion")]
    [Tooltip("World units per second along the track")]
    [SerializeField] private float speed = 6f;

    [Tooltip("Start this far back from the line's first cell, so the cart is already moving when it enters frame")]
    [SerializeField] private float leadIn = 2f;

    [Tooltip("Keep rolling this far past the line's last cell before despawning")]
    [SerializeField] private float runOut = 2f;

    [Tooltip("Lift off the rail piece's pivot. The cart art is drawn top-down on the same 32px grid as the rail, so 0 lands its wheels exactly on the two rail bands.")]
    [SerializeField] private float railOffset = 0f;

    [Header("Presentation")]
    [Tooltip("Leave empty to use this object's own renderer")]
    [SerializeField] private SpriteRenderer body;

    [Tooltip("The art is drawn facing east; westward travel mirrors it")]
    [SerializeField] private bool artFacesEast = true;

    private RailLine _line;
    private float _travelled;
    private bool _riding;

    public bool IsRiding => _riding;
    public RailLine Line => _line;

    /// <summary>Units per second. Set before Ride, or pass a speed to Ride.</summary>
    public float Speed
    {
        get { return speed; }
        set { speed = value; }
    }

    private void Awake()
    {
        if (body == null) body = GetComponent<SpriteRenderer>();
    }

    /// <summary>Put this cart on <paramref name="line"/> at its own speed.</summary>
    public void Ride(RailLine line)
    {
        Ride(line, speed);
    }

    /// <summary>Put this cart on <paramref name="line"/> and start it rolling.</summary>
    public void Ride(RailLine line, float ridingSpeed)
    {
        _line = line;
        speed = ridingSpeed;
        _travelled = 0f;
        _riding = true;
        FaceAlong(line);
        Reposition();
    }

    /// <summary>Leave the cart where it is. It will not resume on its own.</summary>
    public void Halt()
    {
        _riding = false;
    }

    private void Update()
    {
        if (!_riding) return;

        _travelled += speed * Time.deltaTime;
        Reposition();

        if (_travelled - leadIn > _line.Length + runOut) Destroy(gameObject);
    }

    private void Reposition()
    {
        Vector2 point = _line.PointAt(_travelled - leadIn);
        transform.position = new Vector3(point.x, point.y + railOffset, transform.position.z);
    }

    private void FaceAlong(RailLine line)
    {
        Vector2 direction = line.Direction;

        if (line.Axis == RailAxis.Vertical)
        {
            // The art is top-down, so turning it a quarter turn genuinely puts
            // the cart along a shaft — the wheels end up on what will be the
            // vertical piece's two rails once that art exists.
            transform.rotation = Quaternion.Euler(0f, 0f, direction.y >= 0f ? 90f : -90f);
            return;
        }

        transform.rotation = Quaternion.identity;
        if (body != null) body.flipX = artFacesEast ? direction.x < 0f : direction.x > 0f;
    }
}
