using UnityEngine;
using TMPro;
using System.Collections;

public class DamageText : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private Vector3 moveDirection = Vector3.up;
    [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    // ---- size by damage ----
    //
    // A number's size IS the number. A one-point poison tick and a fifty-point
    // powder rock both used to print at the same 2.5, which meant the only way
    // to tell a graze from a disaster was to read the digits — on a screen with
    // two knights, a pack and a health bar to watch, nobody reads the digits.
    //
    // Scaled off a curve rather than straight off the number: linear puts almost
    // everything the game actually deals down at the small end, because ordinary
    // hits are ten and the top of the range is fifty. The power below is picked
    // so a normal arrow lands about where the old fixed size was, and everything
    // is legible as bigger or smaller than that.
    [Header("Size by damage")]
    [Tooltip("Damage that prints at the smallest size — a poison tick.")]
    [SerializeField] private int smallestDamage = 1;

    [Tooltip("Damage that prints at the largest size. Anything above this is drawn at the same size; the number itself says how far above.")]
    [SerializeField] private int largestDamage = 50;

    [Tooltip("Font size at smallestDamage")]
    [SerializeField] private float smallestSize = 1.3f;

    [Tooltip("Font size at largestDamage")]
    [SerializeField] private float largestSize = 9f;

    [Tooltip("Below 1 the small end of the range is stretched out, so the ten-point hits the game is mostly made of are told apart from each other instead of all bunching at the bottom. 0.7 puts a plain arrow at roughly the old fixed size.")]
    [SerializeField] private float sizeCurve = 0.7f;

    // ---- depth ----
    //
    // A number printed at its parent's own z sorts against that parent by whatever
    // order the renderers happen to be in, which meant a hit on a big body — a
    // slime, an ogre, a boss — regularly printed BEHIND the thing it was reporting
    // on. Pulling it a little toward the camera settles it: everything on the
    // field shares one sorting layer and order (see FX_DamageText / the enemy and
    // knight prefabs), so within that layer Unity falls back to distance from the
    // camera, and the camera looks along +z from z = -10. Nearer is smaller z.
    //
    // Small on purpose. This is a tie-break between things standing at the same
    // depth, not a licence to jump in front of scenery the arena puts closer.
    [Header("Depth")]
    [Tooltip("How far toward the camera the number sits in front of the object it is reporting on. Smaller z is nearer.")]
    [SerializeField] private float zLift = 0.1f;

    [Header("Screen bounds")]
    [Tooltip("Fraction of the viewport kept clear at each edge, so a number spawned on (or drifting into) an enemy standing off the edge of the screen gets pulled back onto it instead of printing somewhere the player can never read.")]
    [SerializeField] private float viewportMargin = 0.06f;

    private TextMeshPro textMesh;
    private Color originalColor;
    // Base world position captured at spawn; can be adjusted via PushUp()
    private Vector3 basePosition;
    // Extra Y offset so existing texts can be nudged upward when new ones spawn
    private float externalYOffset = 0f;
    private Camera _camera;

    void Awake()
    {
        // Ensure this object is marked as transient (not to be cloned when enemies duplicate)
        var markerType = System.Type.GetType("TransientEffect");
        if (markerType != null && GetComponent(markerType) == null)
        {
            gameObject.AddComponent(markerType);
        }
        // First try to find TextMeshPro on this object
        textMesh = GetComponent<TextMeshPro>();
        
        // If not found, look in children
        if (textMesh == null)
        {
            textMesh = GetComponentInChildren<TextMeshPro>();
        }
        
        if (textMesh != null)
        {
            originalColor = textMesh.color;
        }

        _camera = Camera.main;
    }

    // Pulls a world position back onto the visible screen, with a small margin so a
    // number never prints flush against the very edge of the frame. Used both at
    // spawn (an enemy hit right at the edge of the arena) and every animation frame
    // (a number that starts on-screen but drifts up past the top as it floats).
    private Vector3 ClampToView(Vector3 worldPosition)
    {
        if (_camera == null) return worldPosition;

        Vector3 viewportPoint = _camera.WorldToViewportPoint(worldPosition);
        if (viewportPoint.z <= 0f) return worldPosition; // behind the camera; leave it alone

        viewportPoint.x = Mathf.Clamp(viewportPoint.x, viewportMargin, 1f - viewportMargin);
        viewportPoint.y = Mathf.Clamp(viewportPoint.y, viewportMargin, 1f - viewportMargin);
        return _camera.ViewportToWorldPoint(viewportPoint);
    }

    /// <summary>
    /// The game's gold, the one the wave banner and the NPCs speak in. Healing gets
    /// it because gold is already what the game means by "something good is
    /// happening to you", and because nothing else prints in it now that burning
    /// ground pays in the same ember orange as the burn itself.
    /// </summary>
    public static readonly Color HealGold = new Color(0.957f, 0.667f, 0.212f, 1f);

    public void Initialize(int damage, Color? color = null)
    {
        Show(damage, "-", color ?? Color.red);
    }

    /// <summary>
    /// Mending, rather than harm: a plus instead of a minus, and gold instead of
    /// red. Deliberately the same object, the same stack and the same size curve a
    /// hit uses — a heal drawn in some other style would read as UI arriving over
    /// the fight, instead of as one more line in the conversation the damage
    /// numbers are already having.
    /// </summary>
    public void InitializeHeal(int amount, Color? color = null)
    {
        Show(amount, "+", color ?? HealGold);
    }

    private void Show(int amount, string sign, Color color)
    {
        if (textMesh != null)
        {
            textMesh.text = sign + amount;
            textMesh.color = color;
            ApplySize(amount);
        }

        // Capture base world position for animation calculations, pulled onto
        // screen first so a number landing off the edge of the arena doesn't print
        // somewhere nobody is looking.
        basePosition = ClampToView(LiftAboveParent(transform.position));
        transform.position = basePosition;
        
        StartCoroutine(AnimateText());
    }

    // Parks the number just in front of whatever it is parented to. Done off the
    // PARENT's z rather than the number's own so the lift is the same whether the
    // spawner handed us the body's position or an offset one, and so it survives an
    // enemy that lives at some z of its own.
    private Vector3 LiftAboveParent(Vector3 worldPosition)
    {
        Transform parent = transform.parent;
        if (parent == null) return worldPosition;

        worldPosition.z = parent.position.z - zLift;
        return worldPosition;
    }

    /// <summary>
    /// How big a hit of <paramref name="damage"/> prints. Public and static so
    /// callers that stack these can ask how much room one is about to take up.
    /// </summary>
    public float SizeFor(int damage)
    {
        int low = Mathf.Max(0, smallestDamage);
        int high = Mathf.Max(low + 1, largestDamage);

        float t = Mathf.InverseLerp(low, high, Mathf.Clamp(damage, low, high));
        t = Mathf.Pow(t, Mathf.Max(0.05f, sizeCurve));
        return Mathf.Lerp(smallestSize, largestSize, t);
    }

    private void ApplySize(int damage)
    {
        float size = SizeFor(damage);
        textMesh.fontSize = size;

        // TMP keeps its own base for auto-sizing; without this a prefab that
        // ever has auto-sizing switched on would snap straight back to 2.5.
        textMesh.fontSizeMin = size;
        textMesh.fontSizeMax = size;
    }

    // Public API to nudge the text upward while it animates
    public void PushUp(float amount)
    {
        externalYOffset += amount;
    }

    private IEnumerator AnimateText()
    {
        float elapsed = 0f;
        // basePosition is captured in Initialize and combined with any externalYOffset
        
        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            
            // Move the text upward, clamped back onto screen every frame so the
            // float-up drift can never carry it past the top edge either.
            transform.position = ClampToView(basePosition + (Vector3.up * externalYOffset) + (moveDirection * moveSpeed * elapsed));
            
            // Fade out using the animation curve - make fade happen mostly at the end
            if (textMesh != null)
            {
                Color color = textMesh.color;
                // Use a power curve to make fade happen quickly at the end
                float fadeProgress = Mathf.Pow(progress, 3f); // Cubic curve for quick fade at end
                color.a = alphaCurve.Evaluate(fadeProgress);
                textMesh.color = color;
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Destroy the text object when animation is complete
        Destroy(gameObject);
    }
}
