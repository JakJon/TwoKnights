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

    
    private TextMeshPro textMesh;
    private Color originalColor;
    // Base world position captured at spawn; can be adjusted via PushUp()
    private Vector3 basePosition;
    // Extra Y offset so existing texts can be nudged upward when new ones spawn
    private float externalYOffset = 0f;

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
    }

    public void Initialize(int damage, Color? color = null)
    {
        if (textMesh != null)
        {
            // minus sign and then the dmage value
            textMesh.text = $"-{damage}";
            // Use provided color or default to red
            textMesh.color = color ?? Color.red;
            ApplySize(damage);
        }
        
        // Adjust starting position (lower the text)
        transform.position += Vector3.down * 0.0f; // Adjust 0.3f to your preference
        // Capture base world position for animation calculations
        basePosition = transform.position;
        
        StartCoroutine(AnimateText());
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
            
            // Move the text upward
            transform.position = basePosition + (Vector3.up * externalYOffset) + (moveDirection * moveSpeed * elapsed);
            
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
