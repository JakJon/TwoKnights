using UnityEngine;

/// <summary>
/// Turns the knight to follow his own shield. The shield is the only thing either
/// player actually steers, so it doubles as the aim: whichever way it is held is the
/// way he is looking, and the body swaps between four drawn poses to match.
/// </summary>
/// <remarks>
/// The sectors come off ShieldOrbit.CurrentAngle, which is transform.eulerAngles.z and
/// therefore 0 at the shield's rightmost point, rising anticlockwise. Shield above the
/// knight means he has turned away from the camera, so the top of the circle is the
/// back poses and the bottom the front ones; left and right follow which side of him
/// the shield is on.
///
/// The four sectors are NOT equal. The back poses hide his face, so they are worth
/// rationing: backSectorDegrees sets how much of the circle each one gets and the two
/// front sectors split the rest. That drags the horizontal boundary well above the
/// horizon while leaving straight up and straight down as the left/right divide. At
/// the default 50 each for the back, the fronts get 130 each, so he faces the camera
/// through 260 degrees of the circle and shows his back through 100.
///
///          90                  40 -  90   back right   (50)
///           |                  90 - 140   back left    (50)
///     back  |  back           140 - 270   front left  (130)
///     left  |  right          270 - 400   front right (130, wraps past 0)
/// 140 ------+------ 40
///    front  |  front
///    left   |  right
///           |
///          270
///
/// This drives SpriteRenderer.sprite directly, and nothing else writes that field. The
/// front poses are two-frame files now (frame 2 is the "blood thirst" face), so their
/// imports generate an Animator controller again — harmless only because the knight
/// prefabs carry no Animator. Adding one back would key the sprite and fight this.
///
/// While Blood Tithe is open (<see cref="BloodThirst"/>) each pose swaps for its blood
/// thirst frame. The back poses hide the visor, so they have none and stay as they are.
/// </remarks>
[RequireComponent(typeof(SpriteRenderer))]
public class KnightFacing : MonoBehaviour
{
    public enum Facing
    {
        BackRight,
        BackLeft,
        FrontLeft,
        FrontRight
    }

    [Header("Poses")]
    [Tooltip("Shield held up and to the right — from the horizon boundary round to straight up.")]
    [SerializeField] private Sprite backRight;

    [Tooltip("Shield held up and to the left — from straight up round to the horizon boundary.")]
    [SerializeField] private Sprite backLeft;

    [Tooltip("Shield held low and to the left — from the horizon boundary down to straight down.")]
    [SerializeField] private Sprite frontLeft;

    [Tooltip("Shield held low and to the right — from straight down round past 0 to the horizon boundary.")]
    [SerializeField] private Sprite frontRight;

    [Header("Blood Tithe")]
    [Tooltip("The 'blood thirst' frame of each pose, worn while Blood Tithe is open. " +
             "A pose left empty keeps its normal sprite.")]
    [SerializeField] private Sprite bloodBackRight;
    [SerializeField] private Sprite bloodBackLeft;
    [SerializeField] private Sprite bloodFrontLeft;
    [SerializeField] private Sprite bloodFrontRight;

    [Header("Shape")]
    [Tooltip("Width in degrees of EACH back sector. The two front sectors split whatever is " +
             "left over, so 50 here gives 130 to each front pose. 90 gives four even " +
             "quadrants; 0 retires the back poses and he always faces the camera.")]
    [SerializeField, Range(0f, 90f)] private float backSectorDegrees = 50f;

    [Header("Source")]
    [Tooltip("Left empty, the shield is found among this knight's children on the first frame.")]
    [SerializeField] private ShieldOrbit shield;

    private SpriteRenderer _renderer;

    // Seeded to a value no quadrant maps to, so the first evaluation always paints
    // rather than trusting whatever pose the prefab happened to be saved wearing.
    private Facing _current = (Facing)(-1);
    private bool _paintedBloodThirst;

    public Facing CurrentFacing => _current;

    /// <summary>True while the knight wears his blood thirst frames. Held by
    /// BloodTitheWindow for exactly as long as the window is open.</summary>
    public bool BloodThirst { get; set; }

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        if (shield == null) shield = GetComponentInChildren<ShieldOrbit>(true);
    }

    // LateUpdate: ShieldOrbit writes CurrentAngle in Update, so reading it here is
    // reading this frame's angle rather than last frame's.
    private void LateUpdate()
    {
        if (shield == null) return;

        Facing wanted = FacingFor(shield.CurrentAngle, backSectorDegrees);
        if (wanted == _current && BloodThirst == _paintedBloodThirst) return;

        Sprite pose = PoseFor(wanted);
        if (pose == null) return; // an unassigned pose holds the last good one

        if (BloodThirst)
        {
            Sprite blood = BloodPoseFor(wanted);
            if (blood != null) pose = blood;
        }

        _current = wanted;
        _paintedBloodThirst = BloodThirst;
        _renderer.sprite = pose;
    }

    /// <summary>Sector for a shield angle in degrees, giving each back sector
    /// <paramref name="backSectorDegrees"/> of the circle and splitting the remainder
    /// between the two front ones. Any angle is accepted; negatives and angles past a
    /// full turn wrap.</summary>
    public static Facing FacingFor(float angleDegrees, float backSectorDegrees)
    {
        // Each back sector runs from the horizon boundary up to the vertical, so
        // shrinking it lifts that boundary and the figure stays symmetric left to right.
        float lift = 90f - Mathf.Clamp(backSectorDegrees, 0f, 90f);
        float angle = Mathf.Repeat(angleDegrees, 360f);

        // Front right owns 270 through 360 and then on to `lift`, so the wrapped tail
        // of it has to be answered before the back sectors get a look.
        if (angle < lift) return Facing.FrontRight;
        if (angle < 90f) return Facing.BackRight;
        if (angle < 180f - lift) return Facing.BackLeft;
        if (angle < 270f) return Facing.FrontLeft;
        return Facing.FrontRight;
    }

    private Sprite PoseFor(Facing facing)
    {
        switch (facing)
        {
            case Facing.BackRight: return backRight;
            case Facing.BackLeft: return backLeft;
            case Facing.FrontLeft: return frontLeft;
            default: return frontRight;
        }
    }

    private Sprite BloodPoseFor(Facing facing)
    {
        switch (facing)
        {
            case Facing.BackRight: return bloodBackRight;
            case Facing.BackLeft: return bloodBackLeft;
            case Facing.FrontLeft: return bloodFrontLeft;
            default: return bloodFrontRight;
        }
    }
}
